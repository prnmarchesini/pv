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
/// <param name="Row">
/// O número da fileira, a partir de 1 na primeira fileira que tem mesa (uma
/// linha de alinhamento afastada da área gera fileiras vazias antes dela, e
/// elas não recebem número). Zero numa mesa pulada por sobreposição.
/// </param>
/// <param name="Number">O número da mesa na fileira, a partir de 1 no sentido da linha. Zero numa mesa pulada.</param>
/// <param name="Origin">O canto de partida: início da mesa, na borda mais próxima da linha.</param>
/// <param name="DirectionRadians">
/// A direção da fileira, em radianos a partir do +X, anti-horário (a
/// convenção matemática, a mesma de <c>Math.Atan2</c>). NÃO é azimute: o
/// azimute topográfico, do norte e horário, é o que <see cref="Transform.Azimuth"/>
/// espera, e a conversão mora em <see cref="RowOrientation"/>, num lugar só.
/// </param>
/// <param name="Length">O comprimento ao longo da fileira.</param>
/// <param name="PlanDepth">O fundo em planta.</param>
/// <param name="Corners">
/// Os quatro cantos em ordem: origem, fim do comprimento, canto oposto, fim
/// do fundo. Z é sempre zero: a cota é da amostragem, não da distribuição.
/// </param>
/// <param name="PartlyOutside">
/// Se alguma parte da mesa cai fora da área: um canto fora, um vértice da
/// área dentro dela, ou uma aresta da área atravessando-a. A mesa fica
/// mesmo assim — "mantida e pintada inteira com uma cor própria, para o
/// engenheiro decidir".
/// </param>
public sealed record PlacedTable(
    int Row,
    int Number,
    Point3 Origin,
    double DirectionRadians,
    double Length,
    double PlanDepth,
    IReadOnlyList<Point3> Corners,
    bool PartlyOutside)
{
    /// <summary>O letreiro do plano de requisitos: F1.1, F1.2, F2.1… A mesa pulada não tem.</summary>
    public string Label => Row > 0 ? $"F{Row}.{Number}" : "(sobreposta)";
}

/// <summary>Uma fileira: as mesas em sequência sobre a mesma reta.</summary>
/// <param name="Number">O número da fileira, a partir de 1.</param>
/// <param name="Segment">O trecho da linha de alinhamento de que ela nasce.</param>
/// <param name="Tables">As mesas, no sentido da linha.</param>
public sealed record PlanRow(int Number, int Segment, IReadOnlyList<PlacedTable> Tables);

/// <summary>O resultado da distribuição em planta.</summary>
public sealed class PlanLayout
{
    internal PlanLayout(IReadOnlyList<PlanRow> rows, IReadOnlyList<PlacedTable> overlapping)
    {
        Rows = rows;
        Overlapping = overlapping;
        Tables = rows.SelectMany(f => f.Tables).ToList();
    }

    /// <summary>As fileiras com pelo menos uma mesa, na ordem de criação.</summary>
    public IReadOnlyList<PlanRow> Rows { get; }

    /// <summary>Todas as mesas colocadas, fileira por fileira.</summary>
    public IReadOnlyList<PlacedTable> Tables { get; }

    /// <summary>
    /// As mesas que NÃO foram colocadas porque pisariam em mesa de outra
    /// família (outro trecho do alinhamento), com a posição em que teriam
    /// ficado. Devolvidas, e não só contadas: o desenho (5.7) as pinta com
    /// cor própria, e o engenheiro vê onde ficou vazio. Não estão em
    /// <see cref="Rows"/> nem em <see cref="Tables"/>.
    /// </summary>
    public IReadOnlyList<PlacedTable> Overlapping { get; }

    /// <summary>Quantas mesas foram puladas por sobreposição.</summary>
    public int SkippedForOverlap => Overlapping.Count;

    /// <summary>Quantas mesas colocadas caíram parcialmente fora da área.</summary>
    public int PartlyOutsideCount => Tables.Count(m => m.PartlyOutside);
}

/// <summary>
/// A distribuição em planta: fileiras a partir da linha de alinhamento, em
/// pitch, para o lado que o usuário clicou, com mesas enfileiradas dentro da
/// área.
///
/// A regra é a do plano de requisitos: "o usuário desenha uma linha e diz: as
/// mesas começam aqui e seguem para aquele lado". A fileira 1 encosta na
/// linha; a fileira seguinte está a um pitch dela, medido de borda a borda.
/// Cada fileira é cortada pela área em trechos, e em cada trecho as mesas
/// entram do início para o fim, com o espaçamento entre elas; a última, que
/// passa da borda, fica e é marcada.
///
/// <b>A fileira segue a linha, e não o azimute da configuração.</b> O plano
/// diz "azimute diferente, fileira diferente (linhas de alinhamento distintas
/// na mesma área)": a orientação de cada fileira vem do trecho da linha de
/// que ela nasce, e é a única convenção em que uma linha quebrada faz sentido.
/// A mesa fica alinhada com a fileira, sempre: a célula que a distribuição
/// reserva tem exatamente o comprimento por o fundo, e girá-la por qualquer
/// ângulo invadiria a vizinha. O azimute da configuração serve para uma
/// coisa só: escolher, entre os dois lados perpendiculares à fileira, qual é
/// a subida da mesa (<see cref="RowOrientation"/>), e avisar quando a linha
/// traçada diverge muito dele.
///
/// Uma linha reta gera fileiras infinitas, cortadas só pela área. Uma linha
/// quebrada gera uma família de fileiras por trecho, cada família limitada à
/// faixa do seu trecho (as perpendiculares pelas pontas); onde as faixas se
/// cruzam, a segunda família não pisa na primeira — a mesa que pisaria é
/// devolvida em <see cref="PlanLayout.Overlapping"/>, com posição, e não
/// colocada. O motor não move mesa para caber.
///
/// Tudo aqui é planta: Z entra zero e sai zero. A cota é da amostragem (5.2).
/// </summary>
public static class RowDistributor
{
    /// <summary>Folga geométrica, em metro.</summary>
    private const double Tolerancia = 1e-6;

    /// <summary>
    /// Quanto as linhas de corte da fileira se afastam das bordas da faixa,
    /// em metro. A fileira 1 encosta na linha de alinhamento, que quase sempre
    /// é a borda da área: cortar exatamente na borda é tangente, e tangente
    /// não entra. Um milímetro para dentro resolve sem mudar nada que se
    /// enxergue.
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
    private const double MaiorMedida = 50.0;

    /// <summary>
    /// Distribui as mesas.
    /// </summary>
    /// <param name="area">O contorno da área de implantação, em planta.</param>
    /// <param name="alignment">A linha de alinhamento, no sentido em que foi traçada.</param>
    /// <param name="side">De que lado da linha ficam as mesas.</param>
    /// <param name="pitch">A distância entre fileiras, de borda a borda, em metro.</param>
    /// <param name="gap">O espaçamento entre mesas vizinhas de uma fileira, em metro.</param>
    /// <param name="table">O que a mesa ocupa em planta.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Área com menos de três vértices, linha sem trecho com comprimento,
    /// pitch que não deixa a fileira caber (menor ou igual ao fundo), medida
    /// abaixo de <see cref="MenorMedida"/>, não finita, ou espaçamento negativo.
    /// </exception>
    /// <exception cref="ArgumentException">Lado "em cima", que não é lado.</exception>
    public static PlanLayout Distribute(
        IReadOnlyList<Point3> area,
        IReadOnlyList<Point3> alignment,
        LineSide side,
        double pitch,
        double gap,
        TableFootprint table)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(alignment);
        ArgumentNullException.ThrowIfNull(table);

        Conferir(area, alignment, side, pitch, gap, table);

        var trechos = Trechos(alignment);
        var fileiras = new List<PlanRow>();
        var puladas = new List<PlacedTable>();

        // As mesas colocadas, por família, para a conferência de sobreposição
        // entre famílias. Com um trecho só não há outra família, e a lista
        // nem é consultada.
        var familias = new List<List<PlacedTable>>();

        for (var t = 0; t < trechos.Count; t++)
        {
            var (a, b) = trechos[t];
            var familia = new List<PlacedTable>();

            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var comprimento = Math.Sqrt(dx * dx + dy * dy);

            var direcao = new Point3(dx / comprimento, dy / comprimento, 0);

            // A normal para o lado das mesas. LineSides.SignedDistance é
            // positiva à direita do sentido a→b: a direita é (dy, −dx).
            var normal = side == LineSide.Right
                ? new Point3(direcao.Y, -direcao.X, 0)
                : new Point3(-direcao.Y, direcao.X, 0);

            var rumo = Math.Atan2(direcao.Y, direcao.X);

            // Até onde a área vai para o lado das mesas, medido da linha.
            var alcance = area.Max(v => (v.X - a.X) * normal.X + (v.Y - a.Y) * normal.Y);

            // Linha reta: fileiras infinitas. Linha quebrada: cada família
            // fica na faixa do seu trecho.
            var inicioDaFaixa = trechos.Count == 1 ? double.NegativeInfinity : 0;
            var fimDaFaixa = trechos.Count == 1 ? double.PositiveInfinity : comprimento;

            for (var k = 0; k * pitch < alcance - Tolerancia; k++)
            {
                var afastamento = k * pitch;
                var mesas = new List<PlacedTable>();

                foreach (var (inicioDoTrecho, fimDoTrecho) in Trechos(area, a, direcao, normal, afastamento, table.PlanDepth))
                {
                    var inicio = Math.Max(inicioDoTrecho, inicioDaFaixa);
                    var fim = Math.Min(fimDoTrecho, fimDaFaixa);

                    if (fim - inicio <= Tolerancia) continue;

                    for (var s = inicio; s < fim - Tolerancia; s += table.Length + gap)
                    {
                        var origem = new Point3(
                            a.X + direcao.X * s + normal.X * afastamento,
                            a.Y + direcao.Y * s + normal.Y * afastamento,
                            0);

                        var mesa = Montar(fileiras.Count + 1, mesas.Count + 1, origem, direcao, normal, rumo, table, area);

                        if (t > 0 && PisaEmOutraFamilia(mesa, familias))
                        {
                            puladas.Add(mesa with { Row = 0, Number = 0 });
                            continue;
                        }

                        mesas.Add(mesa);
                        familia.Add(mesa);
                    }
                }

                if (mesas.Count > 0) fileiras.Add(new PlanRow(fileiras.Count + 1, t, mesas));
            }

            familias.Add(familia);
        }

        return new PlanLayout(fileiras, puladas);
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

        // Prefiltro barato: mesas mais longe que a soma das meias diagonais
        // não se tocam, e num alinhamento quebrado a maioria dos pares é
        // assim.
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
    /// parâmetro ao longo da direção.
    ///
    /// É a projeção, sobre a direção da fileira, da parte da área que cai
    /// dentro da faixa. Essa parte é limitada por pedaços de aresta da área
    /// que estão na faixa e por pedaços das bordas da faixa que estão na
    /// área, e a projeção de uma região é a projeção do seu contorno: então
    /// os intervalos são a união (1) dos cruzamentos das bordas da faixa com
    /// a área e (2) das arestas da área recortadas pela faixa.
    ///
    /// As bordas são cortadas um milímetro para dentro (<see cref="Recuo"/>)
    /// porque a fileira 1 tem a borda de cá em cima da linha de alinhamento,
    /// que costuma ser a borda da área, e tangente não entra; a linha do meio
    /// entra também, por redundância barata. As arestas recortadas são o que
    /// garante que toda parte da área dentro da faixa, por menor que seja,
    /// ganha o seu intervalo — inclusive uma área fininha que não toca
    /// nenhuma das três linhas.
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
            // Coordenadas da aresta no sistema da fileira: u ao longo, v para o lado.
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
            {
                uniao[^1] = (uniao[^1].Inicio, Math.Max(uniao[^1].Fim, fim));
            }
            else
            {
                uniao.Add((inicio, fim));
            }
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
        int fileira, int numero, Point3 origem, Point3 direcao, Point3 normal, double rumo,
        TableFootprint mesa, IReadOnlyList<Point3> area)
    {
        var fim = new Point3(origem.X + direcao.X * mesa.Length, origem.Y + direcao.Y * mesa.Length, 0);
        var oposto = new Point3(fim.X + normal.X * mesa.PlanDepth, fim.Y + normal.Y * mesa.PlanDepth, 0);
        var fundo = new Point3(origem.X + normal.X * mesa.PlanDepth, origem.Y + normal.Y * mesa.PlanDepth, 0);

        Point3[] cantos = [origem, fim, oposto, fundo];

        return new PlacedTable(
            fileira, numero, origem, rumo, mesa.Length, mesa.PlanDepth, cantos,
            ParcialmenteFora(cantos, origem, direcao, normal, mesa, area));
    }

    /// <summary>
    /// Se alguma parte da mesa está fora da área. Três perguntas, e as três
    /// são necessárias: um canto fora (a mesa passa da borda); um vértice da
    /// área dentro da mesa (a área faz um recorte que entra por ela, com os
    /// quatro cantos ainda dentro — a revisão do 5.1 pegou a versão que só
    /// olhava os cantos); uma aresta da área atravessando uma aresta da mesa
    /// (o recorte entra e sai sem deixar vértice dentro). Borda conta como
    /// dentro (<see cref="Polygons.Contains"/>) e encostar não é atravessar
    /// (<see cref="Polygons.SegmentsCross"/>): a mesa encostada na borda da
    /// área é inteira, não parcial.
    /// </summary>
    private static bool ParcialmenteFora(
        Point3[] cantos, Point3 origem, Point3 direcao, Point3 normal, TableFootprint mesa, IReadOnlyList<Point3> area)
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

    private static bool PisaEmOutraFamilia(PlacedTable mesa, List<List<PlacedTable>> familias)
    {
        foreach (var familia in familias)
        {
            foreach (var outra in familia)
            {
                if (Overlap(mesa, outra)) return true;
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
    /// é pulado: um trecho de 5 mm daria uma família inteira de fileiras
    /// com o rumo do tremor da mão.
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
        double pitch, double gap, TableFootprint mesa)
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
