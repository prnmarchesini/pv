using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// O que uma mesa ocupa em planta, vista de cima.
/// </summary>
/// <param name="Length">O comprimento, ao longo da fileira, em metro.</param>
/// <param name="PlanDepth">
/// O fundo em planta, na direção perpendicular à fileira, em metro: o fundo
/// da mesa vezes o cosseno da inclinação. Quem chama faz essa conta, porque
/// é ele que sabe a inclinação; aqui só se sabe o que a mesa cobre no chão.
/// </param>
public sealed record TableFootprint(double Length, double PlanDepth);

/// <summary>Uma mesa colocada em planta, ainda sem cota.</summary>
/// <param name="Row">O número da fileira, a partir de 1 na fileira que nasce no início da linha de alinhamento.</param>
/// <param name="Number">O número da mesa na fileira, a partir de 1 na mesa que encosta na linha de alinhamento.</param>
/// <param name="Origin">O canto de partida: início da mesa, na borda baixa (a voltada contra o azimute).</param>
/// <param name="DirectionRadians">
/// A direção da fileira, em radianos a partir do +X, anti-horário (a
/// convenção matemática, a mesma de <c>Math.Atan2</c>): perpendicular ao
/// azimute da configuração, para o lado das mesas. NÃO é azimute: o azimute
/// topográfico, do norte e horário, é o que <see cref="Transform.Azimuth"/>
/// espera, e a conversão mora em <see cref="RowOrientation"/>, num lugar só.
/// </param>
/// <param name="Length">O comprimento ao longo da fileira.</param>
/// <param name="PlanDepth">O fundo em planta.</param>
/// <param name="Corners">
/// Os quatro cantos em ordem: origem, fim do comprimento, canto oposto, fim
/// do fundo. O fundo (canto 3 − canto 0) aponta no sentido do azimute, a
/// subida da mesa. Z é sempre zero: a cota é da amostragem, não da
/// distribuição.
/// </param>
/// <param name="PartlyOutside">
/// Sempre falso desde 26/09/2026: mesa que passa da área não é colocada
/// (decisão do Renan na tela). O campo fica porque a análise de borda (4.3)
/// o lê; hoje ela não tem o que pintar.
/// </param>
/// <param name="Kind">
/// Qual das mesas da usina é esta, pelo índice na lista de tipos que a
/// distribuição recebeu (passo 8.6: 28 e 14 módulos na mesma usina). Zero
/// quando só há um tipo.
/// </param>
/// <param name="TriedAll">
/// A distribuição testou no terreno todas as mesas que cabiam neste lugar,
/// pela prioridade, e nenhuma ficou boa: ficou a de maior prioridade
/// (02/10/2026: "tentei todas as mesas possíveis, nenhuma ficou boa, então
/// deixei a primeira opção"; o desenho pinta de roxo).
/// </param>
public sealed record PlacedTable(
    int Row,
    int Number,
    Point3 Origin,
    double DirectionRadians,
    double Length,
    double PlanDepth,
    IReadOnlyList<Point3> Corners,
    bool PartlyOutside,
    int Kind = 0,
    bool TriedAll = false)
{
    /// <summary>O letreiro do plano de requisitos: F1.1, F1.2, F2.1…</summary>
    public string Label => $"F{Row}.{Number}";
}

/// <summary>Uma fileira: as mesas em sequência sobre a mesma reta.</summary>
/// <param name="Number">O número da fileira, a partir de 1.</param>
/// <param name="Tables">As mesas, da linha de alinhamento para fora.</param>
public sealed record PlanRow(int Number, IReadOnlyList<PlacedTable> Tables);

/// <summary>O resultado da distribuição em planta.</summary>
public sealed class PlanLayout
{
    internal PlanLayout(IReadOnlyList<PlanRow> rows, int droppedOutside)
    {
        Rows = rows;
        DroppedOutside = droppedOutside;
        Tables = rows.SelectMany(f => f.Tables).ToList();
    }

    /// <summary>As fileiras com pelo menos uma mesa, na ordem de criação.</summary>
    public IReadOnlyList<PlanRow> Rows { get; }

    /// <summary>Todas as mesas colocadas, fileira por fileira.</summary>
    public IReadOnlyList<PlacedTable> Tables { get; }

    /// <summary>
    /// Quantas posições de mesa foram descartadas por passar da área: a
    /// última de um trecho, que não cabe inteira, e a que um recorte da
    /// área invade. Contadas para o relatório; não desenhadas.
    /// </summary>
    public int DroppedOutside { get; }
}

/// <summary>
/// A distribuição em planta.
///
/// Três regras, na ordem em que o Renan as deu ao reprovar as duas primeiras
/// versões na tela (26/09/2026):
///
/// 1. <b>A fileira corre perpendicular ao AZIMUTE da configuração, sempre.</b>
///    Numa usina que olha para o norte (subida para o sul), toda fileira é
///    leste-oeste, não importa como a linha de alinhamento foi traçada. As
///    fileiras se sucedem no sentido do azimute, uma a cada pitch, e o fundo
///    da célula corre no sentido do azimute (é a subida da mesa).
/// 2. <b>A linha de alinhamento é mestra só do alinhamento LATERAL das mesas:</b>
///    ela diz onde cada fileira começa. Cada fileira nasce onde a sua faixa
///    cruza a linha, e a mesa 1 encosta ali; as seguintes vêm depois do
///    espaçamento, para o lado clicado, até a área acabar. Uma linha quebrada
///    dá um começo escalonado, e é para isso que ela existe; uma linha que
///    cruza a mesma faixa duas vezes (zigue-zague) começa a fileira no
///    cruzamento mais adiantado, e o que fica atrás dele fica vazio. A
///    fileira 1 é a que nasce no início da linha (primeiro clique), e as
///    faixas se sucedem no sentido em que a linha caminha do primeiro ao
///    último vértice.
/// 3. <b>Mesa não passa da área.</b> A que não cabe inteira no trecho, ou que
///    um recorte da área invade, não é colocada: conta em
///    <see cref="PlanLayout.DroppedOutside"/> e só.
///
/// A célula que a distribuição reserva tem exatamente o comprimento por o
/// fundo; girá-la invadiria a vizinha. Fileiras em faixas distintas nunca se
/// sobrepõem, então não há mais verificação de sobreposição aqui
/// (<see cref="Overlap"/> fica para quem precisar).
///
/// Tudo aqui é planta: Z entra zero e sai zero. A cota é da amostragem (5.2).
/// </summary>
public static class RowDistributor
{
    /// <summary>Folga geométrica, em metro.</summary>
    private const double Tolerancia = 1e-6;

    /// <summary>
    /// Quanto as linhas de corte da faixa se afastam das bordas dela, em
    /// metro. A primeira faixa costuma ter a borda em cima de uma borda da
    /// área: cortar exatamente na borda é tangente, e tangente não entra. Um
    /// milímetro para dentro resolve sem mudar nada que se enxergue.
    /// </summary>
    private const double Recuo = 1e-3;

    /// <summary>
    /// Duas mesas encostadas (borda com borda) não se sobrepõem; abaixo de um
    /// milímetro de interseção é encosto, não invasão.
    /// </summary>
    private const double EncostoTolerado = 1e-3;

    /// <summary>
    /// Menor medida aceita para mesa e pitch, em metro. Sem um piso, uma mesa
    /// de um décimo de milímetro gerava dez mil mesas por metro, e uma de
    /// 1e-15 travava o laço: o passo somado sumia abaixo da precisão do
    /// double. É o mesmo piso da linha de referência (<see cref="LineSides.ComprimentoMinimo"/>).
    /// </summary>
    public const double MenorMedida = LineSides.ComprimentoMinimo;

    /// <summary>Maior medida aceita, em metro, como rede para erro de escala.</summary>
    public const double MaiorMedida = 50.0;

    /// <summary>
    /// Distribui as mesas.
    /// </summary>
    /// <param name="area">O contorno da área de implantação, em planta.</param>
    /// <param name="alignment">A linha de alinhamento lateral, no sentido em que foi traçada: a fileira 1 nasce no início.</param>
    /// <param name="side">Para que lado da linha correm as fileiras.</param>
    /// <param name="pitch">A distância entre fileiras, de início a início, no sentido do azimute, em metro.</param>
    /// <param name="gap">O espaçamento entre mesas vizinhas de uma fileira, em metro.</param>
    /// <param name="table">O que a mesa ocupa em planta.</param>
    /// <param name="upslopeAzimuthRadians">O azimute da subida da mesa (do norte, horário), da configuração.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Área com menos de três vértices, linha sem trecho com comprimento,
    /// pitch que não deixa a fileira caber (menor ou igual ao fundo), medida
    /// abaixo de <see cref="MenorMedida"/>, não finita, ou espaçamento negativo.
    /// </exception>
    /// <exception cref="ArgumentException">Lado "em cima", ou linha de alinhamento paralela às fileiras (não há como escolher o lado).</exception>
    public static PlanLayout Distribute(
        IReadOnlyList<Point3> area,
        IReadOnlyList<Point3> alignment,
        LineSide side,
        double pitch,
        double gap,
        TableFootprint table,
        double upslopeAzimuthRadians)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Distribute(area, alignment, side, pitch, gap, [table], [1], upslopeAzimuthRadians);
    }

    /// <summary>
    /// A distribuição com mais de um tipo de mesa (passo 8.6, Melhorias.docx:
    /// "posso ter uma mesa de 28 módulos e uma mesa de 14 módulos"). Em cada
    /// trecho de fileira dentro da área, as mesas pela ordem de prioridade da
    /// lista (<see cref="Combinacao"/>). Com um tipo só é exatamente a
    /// distribuição de sempre.
    ///
    /// Todos os tipos ocupam a mesma faixa da fileira: o fundo em planta que
    /// vale é o maior deles.
    ///
    /// Com <paramref name="fits"/> (o teste da mesa no terreno), cada lugar
    /// da fileira é decidido olhando o terreno (Renan, 02/10/2026: "deveria
    /// testar com o módulo de 14 quando a primeira opção, que é 28, não
    /// couber; se a segunda opção não couber, volta à de 28, mas pinta de
    /// outra cor"): a primeira mesa da lista que cabe no trecho e fica boa
    /// no terreno; se nenhuma fica boa, a primeira que cabe, com
    /// <see cref="PlacedTable.TriedAll"/>. A mesa seguinte começa logo
    /// depois da escolhida: a fileira não fica com buraco.
    /// </summary>
    /// <param name="tables">Os tipos de mesa, em planta.</param>
    /// <param name="modules">Os módulos de cada tipo, na mesma ordem.</param>
    /// <param name="fits">Se a mesa, sozinha, fica boa no terreno; null distribui só pelo comprimento.</param>
    public static PlanLayout Distribute(
        IReadOnlyList<Point3> area,
        IReadOnlyList<Point3> alignment,
        LineSide side,
        double pitch,
        double gap,
        IReadOnlyList<TableFootprint> tables,
        IReadOnlyList<int> modules,
        double upslopeAzimuthRadians,
        Func<PlacedTable, bool>? fits = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(alignment);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(modules);

        if (tables.Count == 0) throw new ArgumentException("A usina precisa de pelo menos um tipo de mesa.", nameof(tables));
        if (modules.Count != tables.Count) throw new ArgumentException("Um número de módulos por tipo de mesa.", nameof(modules));
        if (modules.Any(m => m < 1)) throw new ArgumentOutOfRangeException(nameof(modules), "Todo tipo de mesa tem pelo menos um módulo.");

        // A faixa é a do tipo mais fundo; os outros cabem nela.
        var table = new TableFootprint(tables.Max(t => t.Length), tables.Max(t => t.PlanDepth));

        foreach (var tipo in tables) Conferir(area, alignment, side, pitch, gap, tipo, upslopeAzimuthRadians);

        var trechos = Trechos(alignment);

        // O eixo do azimute (a subida, e o fundo da célula) e a direção da
        // fileira, perpendicular a ele, para o lado clicado. O lado é decidido
        // pela linha inteira, do primeiro ao último vértice.
        var subida = new Point3(Math.Sin(upslopeAzimuthRadians), Math.Cos(upslopeAzimuthRadians), 0);
        var direcao = DirecaoDaFileira(subida, trechos, side);
        var rumo = Math.Atan2(direcao.Y, direcao.X);

        // O sistema da usina: u ao longo da fileira, v ao longo da subida,
        // origem no início da linha (é onde nasce a fileira 1).
        var origem = trechos[0].A;
        var vertices = alignment.Select(p => Local(p, origem, direcao, subida)).ToList();
        var vMin = vertices.Min(p => p.V);
        var vMax = vertices.Max(p => p.V);

        // A fileira 1 encosta no início da linha e a célula dela cresce no
        // sentido em que a linha caminha (subindo ou descendo o azimute); as
        // seguintes vêm a cada pitch nesse sentido. Só nascem fileiras cujas
        // faixas cruzam a linha.
        var sentido = vertices[^1].V >= vertices[0].V ? 1 : -1;
        var alcance = (int)Math.Ceiling((Math.Max(Math.Abs(vMin), Math.Abs(vMax)) + table.PlanDepth) / pitch) + 1;

        var fileiras = new List<PlanRow>();
        var descartadas = 0;

        for (var k = -alcance; k <= alcance; k++)
        {
            var afastamento = sentido > 0 ? k * pitch : -k * pitch - table.PlanDepth;

            if (afastamento + table.PlanDepth < vMin - Tolerancia || afastamento > vMax + Tolerancia) continue;

            // Onde a faixa cruza a linha: o ponto mais adiantado para o lado
            // das mesas entre as três linhas da faixa, para nenhum canto da
            // mesa 1 ficar atrás da linha.
            var comeco = ComecoDaFileira(trechos, origem, direcao, subida, afastamento, table.PlanDepth);
            if (comeco is null) continue;

            var mesas = new List<PlacedTable>();

            // Põe a mesa do tipo dado na estação s; null se um recorte da
            // área entra por ela (descartada, e contada se contar).
            PlacedTable? Colocar(double s, TableFootprint tipo, int indice, bool contar = true)
            {
                var canto = new Point3(
                    origem.X + direcao.X * s + subida.X * afastamento,
                    origem.Y + direcao.Y * s + subida.Y * afastamento,
                    0);

                var mesa = Montar(fileiras.Count + 1, mesas.Count + 1, canto, direcao, subida, rumo, tipo);

                if (ParcialmenteFora(mesa.Corners, canto, direcao, subida, tipo, area))
                {
                    if (contar) descartadas++;
                    return null;
                }

                return mesa with { Number = mesas.Count + 1, Kind = indice };
            }

            foreach (var (inicioDoTrecho, fimDoTrecho) in Trechos(area, origem, direcao, subida, afastamento, table.PlanDepth))
            {
                var inicio = Math.Max(inicioDoTrecho, comeco.Value);
                var fim = fimDoTrecho;

                if (fim - inicio <= Tolerancia) continue;

                if (tables.Count == 1)
                {
                    // Um tipo só: a distribuição de sempre, mesa atrás de mesa.
                    var unica = tables[0];

                    for (var s = inicio; s < fim - Tolerancia; s += unica.Length + gap)
                    {
                        // Não cabe inteira no trecho: descartada, e o trecho acabou.
                        if (s + unica.Length > fim + Tolerancia)
                        {
                            descartadas++;
                            break;
                        }

                        if (Colocar(s, unica, 0) is { } mesa) mesas.Add(mesa);
                    }

                    continue;
                }

                if (fits is not null)
                {
                    // Lugar a lugar, olhando o terreno.
                    var s = inicio;

                    while (true)
                    {
                        var cabem = Enumerable.Range(0, tables.Count).Where(t => s + tables[t].Length <= fim + Tolerancia).ToList();
                        if (cabem.Count == 0) break;

                        PlacedTable? escolhida = null;
                        PlacedTable? primeira = null;
                        var testadas = 0;

                        foreach (var t in cabem)
                        {
                            if (Colocar(s, tables[t], t, contar: false) is not { } candidata) continue;

                            primeira ??= candidata;
                            testadas++;

                            if (fits(candidata))
                            {
                                escolhida = candidata;
                                break;
                            }
                        }

                        if (primeira is null)
                        {
                            // Um recorte da área entra por todas: o lugar da
                            // primeira fica vazio, como na distribuição de sempre.
                            descartadas++;
                            s += tables[cabem[0]].Length + gap;
                            continue;
                        }

                        var mesa = escolhida ?? primeira with { TriedAll = testadas > 1 };
                        mesas.Add(mesa);
                        s += mesa.Length + gap;
                    }

                    if (s < fim - Tolerancia && fim - s > tables.Min(t => t.Length) * 0.5) descartadas++;
                    continue;
                }

                // Vários tipos: pela prioridade da lista, no comprimento do trecho.
                var posicao = inicio;

                foreach (var tipo in Combinacao(fim - inicio, tables, modules, gap))
                {
                    if (Colocar(posicao, tables[tipo], tipo) is { } mesa) mesas.Add(mesa);
                    posicao += tables[tipo].Length + gap;
                }

                if (posicao < fim - Tolerancia && fim - posicao > tables.Min(t => t.Length) * 0.5) descartadas++;
            }

            if (mesas.Count > 0) fileiras.Add(new PlanRow(fileiras.Count + 1, mesas));
        }

        return new PlanLayout(fileiras, descartadas);
    }

    /// <summary>
    /// Os tipos de mesa (índices) que cabem num trecho, pela PRIORIDADE da
    /// lista (Renan, 02/10/2026: "o primeiro da lista vai ser a prioridade,
    /// sempre vai tentar encaixar o primeiro, se não der, aí o segundo"): o
    /// máximo de mesas do primeiro tipo; no que sobra, do segundo; e assim
    /// por diante. Cada mesa ocupa o comprimento dela mais o espaçamento, e o
    /// último espaçamento sobra. Arredondado ao milímetro para cima: nunca
    /// passa do trecho.
    /// </summary>
    /// <param name="modulos">Não entra na escolha (fica pela assinatura de quem chama).</param>
    public static IReadOnlyList<int> Combinacao(double comprimento, IReadOnlyList<TableFootprint> tipos, IReadOnlyList<int> modulos, double gap)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        if (!double.IsFinite(comprimento) || comprimento <= 0) return [];

        var restante = (long)Math.Floor((comprimento + gap) * 1000 + 1e-6);
        var escolhidos = new List<int>();

        for (var t = 0; t < tipos.Count; t++)
        {
            var passo = (long)Math.Ceiling((tipos[t].Length + gap) * 1000 - 1e-6);
            if (passo <= 0) continue;

            var quantas = restante / passo;
            for (var k = 0; k < quantas; k++) escolhidos.Add(t);
            restante -= quantas * passo;
        }

        return escolhidos;
    }

    /// <summary>
    /// A direção da fileira: perpendicular à subida, para o lado clicado da
    /// linha. Dos dois perpendiculares, o que aponta para o lado das mesas em
    /// relação à linha inteira (do primeiro ao último vértice).
    /// </summary>
    private static Point3 DirecaoDaFileira(Point3 subida, List<(Point3 A, Point3 B)> trechos, LineSide side)
    {
        var a = trechos[0].A;
        var b = trechos[^1].B;
        var tx = b.X - a.X;
        var ty = b.Y - a.Y;
        var comprimento = Math.Sqrt(tx * tx + ty * ty);

        if (comprimento < LineSides.ComprimentoMinimo)
        {
            // Linha que volta ao ponto de partida: vale o primeiro trecho.
            (tx, ty) = (trechos[0].B.X - a.X, trechos[0].B.Y - a.Y);
            comprimento = Math.Sqrt(tx * tx + ty * ty);
        }

        tx /= comprimento;
        ty /= comprimento;

        // A normal da linha para o lado das mesas: a direita de a→b é (ty, −tx).
        var (nx, ny) = side == LineSide.Right ? (ty, -tx) : (-ty, tx);

        // Os dois perpendiculares à subida.
        var candidata = new Point3(subida.Y, -subida.X, 0);
        var produto = candidata.X * nx + candidata.Y * ny;

        if (Math.Abs(produto) < 1e-9)
        {
            throw new ArgumentException(
                "A linha de alinhamento está paralela às fileiras (perpendicular ao azimute): não há como saber para que lado as fileiras vão. "
                + "Trace a linha atravessando as fileiras.", nameof(side));
        }

        return produto > 0 ? candidata : new Point3(-candidata.X, -candidata.Y, 0);
    }

    /// <summary>
    /// Onde a faixa [afastamento, afastamento + fundo] cruza a linha de
    /// alinhamento, como o maior u entre os cruzamentos das três linhas da
    /// faixa (bordas e meio) com os trechos. Null se a faixa não cruza a linha.
    /// </summary>
    private static double? ComecoDaFileira(
        List<(Point3 A, Point3 B)> trechos, Point3 origem, Point3 direcao, Point3 subida, double afastamento, double fundo)
    {
        double? maior = null;

        foreach (var v in new[] { afastamento + Recuo, afastamento + fundo / 2, afastamento + fundo - Recuo })
        {
            foreach (var (a, b) in trechos)
            {
                var (ua, va) = Local(a, origem, direcao, subida);
                var (ub, vb) = Local(b, origem, direcao, subida);

                // O trecho cruza a linha v? (inclusive nas pontas, para uma
                // linha que termina exatamente na faixa contar)
                if ((va - v) * (vb - v) > 0) continue;

                double u;

                if (Math.Abs(vb - va) <= Tolerancia)
                {
                    // Trecho deitado na própria linha v: vale o ponto mais adiantado.
                    u = Math.Max(ua, ub);
                }
                else
                {
                    u = ua + (ub - ua) * (v - va) / (vb - va);
                }

                if (maior is null || u > maior) maior = u;
            }
        }

        return maior;
    }

    /// <summary>
    /// Se as duas mesas têm interseção de área em planta. Encostar não é
    /// sobrepor. É o teste de separação por eixos dos dois retângulos: se
    /// existe um eixo (lado de um dos dois) em que as projeções não se
    /// cruzam, eles não se cruzam.
    /// </summary>
    public static bool Overlap(PlacedTable a, PlacedTable b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var (cax, cay) = Centro(a);
        var (cbx, cby) = Centro(b);
        var alcance = (Diagonal(a) + Diagonal(b)) / 2;

        if ((cax - cbx) * (cax - cbx) + (cay - cby) * (cay - cby) > alcance * alcance) return false;

        foreach (var eixo in Eixos(a).Concat(Eixos(b)))
        {
            var (minA, maxA) = Projecao(a.Corners, eixo);
            var (minB, maxB) = Projecao(b.Corners, eixo);

            if (minA >= maxB - EncostoTolerado || minB >= maxA - EncostoTolerado) return false;
        }

        return true;
    }

    /// <summary>
    /// Onde a faixa da fileira está dentro da área, como intervalos do
    /// parâmetro u ao longo da direção da fileira.
    ///
    /// É a projeção, sobre a direção da fileira, da parte da área que cai
    /// dentro da faixa. Essa parte é limitada por pedaços de aresta da área
    /// que estão na faixa e por pedaços das bordas da faixa que estão na
    /// área, e a projeção de uma região é a projeção do seu contorno: então
    /// os intervalos são a união (1) dos cruzamentos das bordas da faixa com
    /// a área e (2) das arestas da área recortadas pela faixa.
    ///
    /// As bordas são cortadas um milímetro para dentro (<see cref="Recuo"/>),
    /// porque tangente não entra; a linha do meio entra também, por
    /// redundância barata. As arestas recortadas são o que garante que toda
    /// parte da área dentro da faixa, por menor que seja, ganha o seu
    /// intervalo.
    /// </summary>
    private static List<(double Inicio, double Fim)> Trechos(
        IReadOnlyList<Point3> area, Point3 a, Point3 direcao, Point3 normal, double afastamento, double fundo)
    {
        var intervalos = new List<(double, double)>();

        foreach (var lateral in new[] { afastamento + Recuo, afastamento + fundo / 2, afastamento + fundo - Recuo })
        {
            var origem = new Point3(a.X + normal.X * lateral, a.Y + normal.Y * lateral, 0);
            var cruzamentos = Polygons.Crossings(area, origem, direcao);

            for (var c = 0; c + 1 < cruzamentos.Count; c += 2)
                intervalos.Add((cruzamentos[c], cruzamentos[c + 1]));
        }

        var n = area.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (u0, v0) = Local(area[j], a, direcao, normal);
            var (u1, v1) = Local(area[i], a, direcao, normal);

            if (Recortar(ref u0, ref v0, ref u1, ref v1, afastamento, afastamento + fundo))
                intervalos.Add((Math.Min(u0, u1), Math.Max(u0, u1)));
        }

        intervalos.Sort((p, q) => p.Item1.CompareTo(q.Item1));

        var uniao = new List<(double Inicio, double Fim)>();

        foreach (var (inicio, fim) in intervalos)
        {
            if (uniao.Count > 0 && inicio <= uniao[^1].Fim + Tolerancia)
                uniao[^1] = (uniao[^1].Inicio, Math.Max(uniao[^1].Fim, fim));
            else
                uniao.Add((inicio, fim));
        }

        return uniao;
    }

    /// <summary>
    /// Recorta o segmento (u0,v0)–(u1,v1) à faixa vMin ≤ v ≤ vMax. Devolve
    /// falso se nada dele está na faixa.
    /// </summary>
    private static bool Recortar(ref double u0, ref double v0, ref double u1, ref double v1, double vMin, double vMax)
    {
        if ((v0 < vMin && v1 < vMin) || (v0 > vMax && v1 > vMax)) return false;

        if (Math.Abs(v1 - v0) <= Tolerancia) return true;

        var du = (u1 - u0) / (v1 - v0);

        if (v0 < vMin) { u0 += (vMin - v0) * du; v0 = vMin; }
        if (v0 > vMax) { u0 += (vMax - v0) * du; v0 = vMax; }
        if (v1 < vMin) { u1 += (vMin - v1) * du; v1 = vMin; }
        if (v1 > vMax) { u1 += (vMax - v1) * du; v1 = vMax; }

        return true;
    }

    private static (double U, double V) Local(Point3 p, Point3 a, Point3 direcao, Point3 normal)
    {
        var px = p.X - a.X;
        var py = p.Y - a.Y;

        return (px * direcao.X + py * direcao.Y, px * normal.X + py * normal.Y);
    }

    private static PlacedTable Montar(
        int fileira, int numero, Point3 origem, Point3 direcao, Point3 normal, double rumo, TableFootprint mesa)
    {
        var fim = new Point3(origem.X + direcao.X * mesa.Length, origem.Y + direcao.Y * mesa.Length, 0);
        var oposto = new Point3(fim.X + normal.X * mesa.PlanDepth, fim.Y + normal.Y * mesa.PlanDepth, 0);
        var fundo = new Point3(origem.X + normal.X * mesa.PlanDepth, origem.Y + normal.Y * mesa.PlanDepth, 0);

        Point3[] cantos = [origem, fim, oposto, fundo];

        return new PlacedTable(fileira, numero, origem, rumo, mesa.Length, mesa.PlanDepth, cantos, false);
    }

    /// <summary>
    /// Se alguma parte da mesa está fora da área. Três perguntas, e as três
    /// são necessárias: um canto fora (a mesa passa da borda); um vértice da
    /// área dentro da mesa (a área faz um recorte que entra por ela, com os
    /// quatro cantos ainda dentro); uma aresta da área atravessando uma
    /// aresta da mesa (o recorte entra e sai sem deixar vértice dentro).
    /// Borda conta como dentro (<see cref="Polygons.Contains"/>) e encostar
    /// não é atravessar (<see cref="Polygons.SegmentsCross"/>): a mesa
    /// encostada na borda da área é inteira.
    /// </summary>
    private static bool ParcialmenteFora(
        IReadOnlyList<Point3> cantos, Point3 origem, Point3 direcao, Point3 normal, TableFootprint mesa, IReadOnlyList<Point3> area)
    {
        foreach (var canto in cantos)
        {
            if (!Polygons.Contains(area, canto.X, canto.Y)) return true;
        }

        var n = area.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (u, v) = Local(area[i], origem, direcao, normal);

            if (u > Tolerancia && u < mesa.Length - Tolerancia && v > Tolerancia && v < mesa.PlanDepth - Tolerancia)
                return true;

            for (var c = 0; c < 4; c++)
            {
                if (Polygons.SegmentsCross(area[j], area[i], cantos[c], cantos[(c + 1) % 4])) return true;
            }
        }

        return false;
    }

    private static (double X, double Y) Centro(PlacedTable mesa) =>
        ((mesa.Corners[0].X + mesa.Corners[2].X) / 2, (mesa.Corners[0].Y + mesa.Corners[2].Y) / 2);

    private static double Diagonal(PlacedTable mesa) =>
        Math.Sqrt(mesa.Length * mesa.Length + mesa.PlanDepth * mesa.PlanDepth);

    private static IEnumerable<Point3> Eixos(PlacedTable mesa)
    {
        var c = mesa.Corners;

        yield return new Point3(c[1].X - c[0].X, c[1].Y - c[0].Y, 0);
        yield return new Point3(c[3].X - c[0].X, c[3].Y - c[0].Y, 0);
    }

    private static (double Min, double Max) Projecao(IReadOnlyList<Point3> cantos, Point3 eixo)
    {
        var comprimento = Math.Sqrt(eixo.X * eixo.X + eixo.Y * eixo.Y);
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;

        foreach (var canto in cantos)
        {
            var p = (canto.X * eixo.X + canto.Y * eixo.Y) / comprimento;

            if (p < min) min = p;
            if (p > max) max = p;
        }

        return (min, max);
    }

    /// <summary>
    /// Os trechos da linha com comprimento de verdade em planta. Trecho mais
    /// curto que <see cref="LineSides.ComprimentoMinimo"/> é clique duplo, e
    /// é pulado.
    /// </summary>
    private static List<(Point3 A, Point3 B)> Trechos(IReadOnlyList<Point3> alinhamento)
    {
        var trechos = new List<(Point3, Point3)>();

        for (var i = 0; i + 1 < alinhamento.Count; i++)
        {
            var a = alinhamento[i];
            var b = alinhamento[i + 1];

            var dx = b.X - a.X;
            var dy = b.Y - a.Y;

            if (Math.Sqrt(dx * dx + dy * dy) >= LineSides.ComprimentoMinimo) trechos.Add((a, b));
        }

        if (trechos.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alinhamento), alinhamento.Count,
                "A linha de alinhamento não tem nenhum trecho com comprimento em planta.");
        }

        return trechos;
    }

    private static void Conferir(
        IReadOnlyList<Point3> area, IReadOnlyList<Point3> alinhamento, LineSide lado,
        double pitch, double gap, TableFootprint mesa, double azimute)
    {
        if (area.Count < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(area), area.Count,
                "A área precisa de pelo menos três vértices.");
        }

        foreach (var v in area.Concat(alinhamento))
        {
            if (!v.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(area), "Há vértice não finito.");
        }

        if (lado == LineSide.On)
            throw new ArgumentException("\"Em cima da linha\" não é lado para as mesas.", nameof(lado));

        if (!double.IsFinite(azimute))
            throw new ArgumentOutOfRangeException(nameof(azimute), "O azimute não é um número.");

        if (!Medida(mesa.Length))
            throw new ArgumentOutOfRangeException(nameof(mesa), mesa.Length, "O comprimento da mesa não é uma medida válida.");

        if (!Medida(mesa.PlanDepth))
            throw new ArgumentOutOfRangeException(nameof(mesa), mesa.PlanDepth, "O fundo da mesa em planta não é uma medida válida.");

        if (!Medida(pitch))
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "O pitch não é uma medida válida.");

        if (pitch <= mesa.PlanDepth + Tolerancia)
        {
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch,
                $"O pitch ({pitch:0.###} m) precisa ser maior que o fundo da mesa em planta "
                + $"({mesa.PlanDepth:0.###} m), senão as fileiras se sobrepõem.");
        }

        if (!double.IsFinite(gap) || gap < 0 || gap > MaiorMedida)
            throw new ArgumentOutOfRangeException(nameof(gap), gap, "O espaçamento entre mesas não é uma medida válida.");
    }

    private static bool Medida(double valor) =>
        double.IsFinite(valor) && valor >= MenorMedida && valor <= MaiorMedida;
}
