using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Como o inversor é posto em campo (Renan, 10/10/2026).</summary>
public enum InverterPlacementMode
{
    /// <summary>Dentro de uma área (o retângulo que o usuário desenhou: sala, skid...).</summary>
    Area,

    /// <summary>Pelo menor cabo CC das strings dele, ao lado da vala, no Gerar da rota CC.</summary>
    Automatic,
}

/// <summary>
/// O local de um inversor: numa área (<see cref="Site"/> é o GUID da marca da
/// polilinha) ou automático pelas strings (sem área). Sem registro, o
/// inversor é posto à mão (Pôr em campo), como sempre foi.
/// </summary>
public sealed record InverterPlacement(Guid Inverter, InverterPlacementMode Mode, Guid Site)
{
    public const int FieldCount = 3;

    public IReadOnlyList<string> ToFields() => [Inverter.ToString("D"), Mode.ToString(), Site.ToString("D")];

    public static InverterPlacement? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Guid.TryParse(c[0], out var inversor) || inversor == Guid.Empty) return null;
        if (!Enum.TryParse<InverterPlacementMode>(c[1], out var modo) || !Enum.IsDefined(modo) || !Guid.TryParse(c[2], out var area)) return null;
        if (modo == InverterPlacementMode.Area && area == Guid.Empty) return null;
        return new InverterPlacement(inversor, modo, modo == InverterPlacementMode.Area ? area : Guid.Empty);
    }

    /// <summary>Troca (ou tira, com <paramref name="novo"/> null) o local dos inversores <paramref name="ids"/> na lista.</summary>
    public static List<InverterPlacement> With(IEnumerable<InverterPlacement> lista, IReadOnlyCollection<Guid> ids, InverterPlacementMode? novo, Guid area = default)
    {
        var saida = lista.Where(p => !ids.Contains(p.Inverter)).ToList();
        if (novo is { } m) saida.AddRange(ids.Select(i => new InverterPlacement(i, m, m == InverterPlacementMode.Area ? area : Guid.Empty)));
        return saida;
    }
}

/// <summary>A marca de área de inversores na polilinha do usuário: o GUID e o nome ("Área 1").</summary>
public sealed record SiteMark(Guid Id, string Name)
{
    public const string Tipo = "AreaInversores";
    public const int FieldCount = 2;

    /// <summary>O nome mais comprido de uma área.</summary>
    public const int MaxNameLength = 40;

    public IReadOnlyList<string> ToFields() => [Id.ToString("D"), Name];

    public static SiteMark? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var id) && id != Guid.Empty ? new SiteMark(id, c[1]) : null;

    /// <summary>O primeiro "Área N" que ainda não é nome de nenhuma (N a partir de quantas há + 1).</summary>
    public static string NextDefaultName(IReadOnlyCollection<string> existentes)
    {
        ArgumentNullException.ThrowIfNull(existentes);
        var nomes = existentes.Select(n => n.Trim()).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var n = existentes.Count + 1;
        while (nomes.Contains(Tr.F("Área {0}", n))) n++;
        return Tr.F("Área {0}", n);
    }

    /// <summary>
    /// As marcas efetivas das polilinhas de área (correção de 10/10/2026, à
    /// noite): o COPY, o MIRROR ou o ARRAY da polilinha leva o XData junto e
    /// a cópia nasce com o GUID e o nome da original. Por GUID repetido, fica
    /// com ele a de menor handle (a mais antiga); cada outra ganha GUID
    /// próprio (<see cref="CopyId"/>, o mesmo a cada leitura até ser gravado)
    /// e o nome "Área N" seguinte. Nenhuma é descartada. Na ordem de <paramref name="lidas"/>.
    /// </summary>
    public static IReadOnlyList<SiteMark> ResolveCopies(IReadOnlyList<(long Handle, SiteMark Mark)> lidas)
    {
        ArgumentNullException.ThrowIfNull(lidas);
        var saida = lidas.Select(l => l.Mark).ToArray();
        var donos = lidas.GroupBy(l => l.Mark.Id).ToDictionary(g => g.Key, g => g.Min(l => l.Handle));
        var repetidas = Enumerable.Range(0, lidas.Count).Where(i => lidas[i].Handle != donos[lidas[i].Mark.Id]).OrderBy(i => lidas[i].Handle).ToList();
        if (repetidas.Count == 0) return saida;

        var nomes = Enumerable.Range(0, lidas.Count).Where(i => !repetidas.Contains(i)).Select(i => lidas[i].Mark.Name).ToList();
        foreach (var i in repetidas)
        {
            var nome = NextDefaultName(nomes);
            nomes.Add(nome);
            saida[i] = new SiteMark(CopyId(lidas[i].Mark.Id, lidas[i].Handle), nome);
        }

        return saida;
    }

    /// <summary>O GUID da cópia de uma área: tirado do GUID da original e do handle da cópia (estável entre leituras).</summary>
    public static Guid CopyId(Guid original, long handle)
    {
        var bytes = original.ToByteArray().Concat(BitConverter.GetBytes(handle)).ToArray();
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Por que o nome não serve para a área (item 6 de 10/10/2026): vazio,
    /// comprido demais, com caractere de controle ou repetido de outra área
    /// (sem diferença de maiúscula). Null se serve.
    /// </summary>
    public static string? NameProblem(string? nome, IEnumerable<string> outras)
    {
        ArgumentNullException.ThrowIfNull(outras);
        var limpo = nome?.Trim() ?? string.Empty;
        if (limpo.Length == 0) return Tr.T("o nome da área não pode ficar vazio");
        if (limpo.Length > MaxNameLength) return Tr.F("o nome da área tem no máximo {0} caracteres", MaxNameLength);
        if (limpo.Any(char.IsControl)) return Tr.T("o nome da área não pode ter caractere de controle");
        if (outras.Any(o => string.Equals(o.Trim(), limpo, StringComparison.CurrentCultureIgnoreCase))) return Tr.F("já há uma área chamada {0}", limpo);
        return null;
    }
}

/// <summary>O que a linha do inversor mostra no lugar do botão de campo (item 4 e 19 de 10/10/2026).</summary>
public enum InverterFieldButton
{
    /// <summary>"Pôr em campo": não está no desenho.</summary>
    Place,

    /// <summary>"Mover": já está no desenho.</summary>
    Move,

    /// <summary>O texto "Alocação automática", sem clique: a rota CC põe o inversor (o automático fora de campo).</summary>
    Automatic,
}

/// <summary>
/// O estado coerente da linha do inversor (item 4 de 10/10/2026: "é marcado
/// área e também é marcado pôr em campo, erro grave"): a coluna Local e o
/// botão saem da mesma conta. Área só vale com o inversor em campo e a área
/// no desenho (o registro de uma área que o inversor não ocupa mais, porque
/// não coube, foi apagado ou a área sumiu, vale como à mão). Automático
/// vale sempre: fora de campo quem põe é a rota (só o texto); em campo, o
/// usuário pode mover (item 19: "eu quero ter a liberdade de mover o inversor").
/// </summary>
public sealed record InverterSiteView(InverterPlacementMode? Mode, Guid Site, InverterFieldButton Button, bool CanSee)
{
    public static InverterSiteView Of(InverterPlacement? local, bool emCampo, bool areaExiste)
    {
        if (local is { Mode: InverterPlacementMode.Automatic })
            return new InverterSiteView(InverterPlacementMode.Automatic, Guid.Empty, emCampo ? InverterFieldButton.Move : InverterFieldButton.Automatic, emCampo);

        if (local is { Mode: InverterPlacementMode.Area } && emCampo && areaExiste)
            return new InverterSiteView(InverterPlacementMode.Area, local.Site, InverterFieldButton.Move, true);

        return new InverterSiteView(null, Guid.Empty, emCampo ? InverterFieldButton.Move : InverterFieldButton.Place, emCampo);
    }

    /// <summary>
    /// O texto da coluna Local (item 4 da segunda rodada de 10/10/2026): o
    /// nome da área; "Auto" para o automático; "À mão" para o que está em
    /// campo fora de qualquer área e sem automático; vazio fora de campo.
    /// </summary>
    public string LocalText(string? nomeDaArea) => Mode switch
    {
        InverterPlacementMode.Area => nomeDaArea ?? string.Empty,
        InverterPlacementMode.Automatic => Tr.T("Auto"),
        _ => CanSee ? Tr.T("À mão") : string.Empty,
    };
}

/// <summary>O resultado de encher uma área: o centro de cada um (null: não coube) e a folga com que entrou.</summary>
public sealed record AreaFill(IReadOnlyList<Point3?> Centers, IReadOnlyList<double?> Gaps)
{
    public int Placed => Centers.Count(c => c is not null);

    /// <summary>A menor folga usada (null se nenhum entrou).</summary>
    public double? SmallestGap
    {
        get
        {
            var usadas = Gaps.OfType<double>().ToList();
            return usadas.Count > 0 ? usadas.Min() : null;
        }
    }
}

/// <summary>
/// Onde pôr os inversores (Renan, 10/10/2026): dentro de uma área, um ao
/// lado do outro; ou ao lado da vala, no ponto de menor cabo CC das strings
/// dele ("o sistema calcula o menor trajeto de cabos CC para aquele
/// inversor, conforme a posição das strings, e aloca o inversor ao lado da
/// vala; eu quero ter a liberdade de mover o inversor"). Só planta: a cota é
/// do terreno, de quem desenha.
/// </summary>
public static class InverterSites
{
    /// <summary>A folga entre dois inversores na área e entre o inversor e a vala (m).</summary>
    public const double Gap = 0.5;

    /// <summary>
    /// Os centros dos retângulos (largura em X, comprimento em Y, como o
    /// bloco do equipamento) dentro do polígono, em fileiras ao longo do lado
    /// comprido do menor retângulo que o contém, com <paramref name="gap"/>
    /// entre eles e 1 cm da borda (encostar na parede da sala vale). A caixa
    /// tem que caber inteira (nenhum lado do polígono a corta: dente de área
    /// côncava) e estar livre (<paramref name="livre"/>: fora dos inversores
    /// que já estão na área, das mesas e dos outros equipamentos). Null para o
    /// que não coube.
    /// </summary>
    public static IReadOnlyList<Point3?> InArea(IReadOnlyList<Point3> poligono, IReadOnlyList<(double Width, double Length)> tamanhos, double gap = Gap,
        Func<IReadOnlyList<(double X, double Y)>, bool>? livre = null)
    {
        ArgumentNullException.ThrowIfNull(poligono);
        ArgumentNullException.ThrowIfNull(tamanhos);
        var saida = new Point3?[tamanhos.Count];
        if (poligono.Count < 3 || tamanhos.Count == 0) return saida;

        var (centro, u, v, meioU, meioV) = MenorRetangulo(poligono);

        // Quanto cada retângulo (alinhado a X e Y) ocupa ao longo de u e de v.
        double Ao(Point3 eixo, double w, double l) => Math.Abs(w * eixo.X) + Math.Abs(l * eixo.Y);

        const double borda = 0.01;
        var linhaV = meioV - borda;  // onde começa a fileira de agora, descendo em v
        var posU = -meioU + borda;   // onde começa o próximo, ao longo de u
        var alturaDaFileira = 0.0;

        for (var i = 0; i < tamanhos.Count; i++)
        {
            var (w, l) = tamanhos[i];
            var eu = Ao(u, w, l);
            var ev = Ao(v, w, l);

            // Vaga por vaga: num polígono que não é retângulo, a vaga do menor
            // retângulo pode cair fora dele; tenta a seguinte até acabarem as fileiras.
            while (true)
            {
                if (posU + eu > meioU - borda + 1e-9)
                {
                    // Não cabe nesta fileira: a próxima.
                    linhaV -= alturaDaFileira + gap;
                    posU = -meioU + borda;
                    alturaDaFileira = 0;
                }

                if (linhaV - ev < -meioV + borda - 1e-9 || posU + eu > meioU - borda + 1e-9) break;

                var su = posU + eu / 2;
                var sv = linhaV - ev / 2;
                var p = new Point3(centro.X + u.X * su + v.X * sv, centro.Y + u.Y * su + v.Y * sv, 0);
                var cantos = EquipmentFootprint.Corners(p.X, p.Y, w, l);

                posU += eu + gap;
                alturaDaFileira = Math.Max(alturaDaFileira, ev);

                if (Dentro(poligono, cantos) && (livre is null || livre(cantos)))
                {
                    saida[i] = p;
                    break;
                }
            }
        }

        return saida;
    }

    /// <summary>
    /// As folgas que o Encher tenta, da desejada (<see cref="Gap"/>) à mínima
    /// (<see cref="MinGap"/>): quem não cabe com 0,50 m entra com a maior
    /// que couber.
    /// </summary>
    public static readonly IReadOnlyList<double> Gaps = [0.5, 0.4, 0.3, 0.2, 0.1];

    /// <summary>A menor folga entre inversores (e entre o inversor e o que já está na área), em metro.</summary>
    public const double MinGap = 0.1;

    /// <summary>
    /// Enche a área com os inversores (item 2 da segunda rodada de
    /// 10/10/2026, reincidência: "escolhi o 9 e o 10, cliquei em escolher
    /// área na mesma área, e o sistema NÃO coloca"). Primeiro a grade de
    /// sempre (<see cref="InArea"/>, folga de 0,50 m), fora dos
    /// <paramref name="obstaculos"/> (os inversores que já estão na área, as
    /// mesas, os outros equipamentos). Quem não coube na grade procura vaga
    /// livre fora dela: encostada (com a folga) nas caixas que já estão lá e
    /// nas bordas, de cima para baixo e da esquerda para a direita, com a
    /// folga de 0,50 m e, se não houver, com 0,40, 0,30, 0,20 e 0,10 m. A
    /// caixa fica inteira dentro do contorno e a pelo menos a folga de
    /// qualquer obstáculo e dos postos agora.
    /// </summary>
    /// <remarks>
    /// A causa do erro no Itatiba: a Área 1 tinha 3,77 × 5,15 m e o inversor
    /// 1,10 × 0,70 m. A grade com 0,50 m entre caixas põe 2 × 4 = 8; a faixa
    /// livre em cima (0,84 m) e a do lado (1,06 m) não cabiam outra caixa com
    /// 0,50 m de folga. Os 9 e 10 voltavam "0 de 2", e o aviso só ia para a
    /// linha de comando, atrás da janela que voltava.
    /// </remarks>
    public static AreaFill Fill(IReadOnlyList<Point3> poligono, IReadOnlyList<(double Width, double Length)> tamanhos, IReadOnlyList<IReadOnlyList<Point3>> obstaculos,
        Func<IReadOnlyList<(double X, double Y)>, bool>? livre = null)
    {
        ArgumentNullException.ThrowIfNull(poligono);
        ArgumentNullException.ThrowIfNull(tamanhos);
        ArgumentNullException.ThrowIfNull(obstaculos);
        var centros = new Point3?[tamanhos.Count];
        var folgas = new double?[tamanhos.Count];
        if (poligono.Count < 3 || tamanhos.Count == 0) return new AreaFill(centros, folgas);

        // Só os obstáculos perto da área contam (o desenho tem milhares de mesas).
        var (minX, minY, maxX, maxY) = Caixa(poligono);
        var margem = Gaps.Max();
        var perto = obstaculos.Where(o => o.Count >= 3)
            .Where(o => { var (a, b, c, d) = Caixa(o); return a <= maxX + margem && c >= minX - margem && b <= maxY + margem && d >= minY - margem; })
            .ToList();

        // 1) A grade de sempre, com a folga cheia.
        // A grade também guarda a folga cheia até o que já está lá (outro modelo, mesa encostada na área).
        // O <paramref name="livre"/> de quem chama (a caixa não encosta numa vala, 10/10/2026 à noite) vale nas duas etapas.
        livre ??= _ => true;
        var grade = InArea(poligono, tamanhos, Gap, cantos => livre(cantos) && !perto.Any(o => Overlaps(Crescer(cantos, Gap - 1e-6), o)));
        var postos = new List<IReadOnlyList<Point3>>();
        for (var i = 0; i < tamanhos.Count; i++)
        {
            if (grade[i] is not { } p) continue;
            centros[i] = p;
            folgas[i] = Gap;
            postos.Add(Retangulo(p.X, p.Y, tamanhos[i].Width, tamanhos[i].Length, 0));
        }

        // 2) Quem não coube: a vaga livre fora da grade, com a maior folga que couber.
        // A caixa que não achou vaga nem com a folga mínima não acha depois (os
        // obstáculos só crescem): as do mesmo tamanho nem procuram de novo.
        var semVaga = new HashSet<(double, double)>();
        for (var i = 0; i < tamanhos.Count; i++)
        {
            if (centros[i] is not null) continue;
            var (w, l) = tamanhos[i];
            if (semVaga.Contains((w, l))) continue;
            semVaga.Add((w, l));
            foreach (var folga in Gaps)
            {
                if (Vaga(poligono, w, l, folga, [.. perto, .. postos], livre) is not { } p) continue;
                centros[i] = p;
                folgas[i] = folga;
                postos.Add(Retangulo(p.X, p.Y, w, l, 0));
                semVaga.Remove((w, l));
                break;
            }
        }

        return new AreaFill(centros, folgas);
    }

    /// <summary>
    /// A primeira vaga (de cima para baixo, da esquerda para a direita) para
    /// a caixa w × l inteira dentro do polígono e a pelo menos
    /// <paramref name="folga"/> de cada obstáculo. Os candidatos: encostada
    /// nas bordas (1 cm) e nos obstáculos (com a folga), e uma varredura
    /// regular entre eles (área girada ou côncava). Null se não há.
    /// </summary>
    private static Point3? Vaga(IReadOnlyList<Point3> poligono, double w, double l, double folga, IReadOnlyList<IReadOnlyList<Point3>> obstaculos, Func<IReadOnlyList<(double X, double Y)>, bool> livre)
    {
        const double borda = 0.01;
        var (minX, minY, maxX, maxY) = Caixa(poligono);
        if (maxX - minX < w || maxY - minY < l) return null;

        var xs = new List<double> { minX + borda + w / 2, maxX - borda - w / 2 };
        var ys = new List<double> { maxY - borda - l / 2, minY + borda + l / 2 };
        foreach (var v in poligono)
        {
            xs.Add(v.X + borda + w / 2);
            xs.Add(v.X - borda - w / 2);
            ys.Add(v.Y - borda - l / 2);
            ys.Add(v.Y + borda + l / 2);
        }

        foreach (var o in obstaculos)
        {
            var (a, b, c, d) = Caixa(o);
            xs.Add(c + folga + w / 2);
            xs.Add(a - folga - w / 2);
            ys.Add(b - folga - l / 2);
            ys.Add(d + folga + l / 2);
        }

        // A varredura regular: no máximo uns 200 passos no lado comprido.
        var passo = Math.Max(Math.Min(w, l) / 4, Math.Max(maxX - minX, maxY - minY) / 200);
        for (var x = minX + borda + w / 2; x <= maxX - borda - w / 2 + 1e-9; x += passo) xs.Add(x);
        for (var y = maxY - borda - l / 2; y >= minY + borda + l / 2 - 1e-9; y -= passo) ys.Add(y);

        var colunas = xs.Where(x => x - w / 2 >= minX - 1e-9 && x + w / 2 <= maxX + 1e-9).Select(x => Math.Round(x, 6)).Distinct().Order().ToList();
        var linhas = ys.Where(y => y - l / 2 >= minY - 1e-9 && y + l / 2 <= maxY + 1e-9).Select(y => Math.Round(y, 6)).Distinct().OrderDescending().ToList();

        // A folga vale entre caixas: a caixa crescida da folga (menos um fio, encostar na folga vale) não toca nenhum obstáculo.
        var crescer = Math.Max(0, folga - 1e-6);

        // A caixa envolvente de cada obstáculo: o candidato longe dela nem chega ao Overlaps (área grande com muitas mesas).
        var caixas = obstaculos.Select(o => (Obstaculo: o, Caixa: Caixa(o))).ToList();
        foreach (var y in linhas)
            foreach (var x in colunas)
            {
                var (ax, ay, bx, by) = (x - w / 2 - crescer, y - l / 2 - crescer, x + w / 2 + crescer, y + l / 2 + crescer);
                var crescida = EquipmentFootprint.Corners(x, y, w + 2 * crescer, l + 2 * crescer);
                if (caixas.Any(c => c.Caixa.MinX <= bx && c.Caixa.MaxX >= ax && c.Caixa.MinY <= by && c.Caixa.MaxY >= ay && Overlaps(crescida, c.Obstaculo))) continue;
                var cantos = EquipmentFootprint.Corners(x, y, w, l);
                if (!Dentro(poligono, cantos) || !livre(cantos)) continue;
                return new Point3(x, y, 0);
            }

        return null;
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Caixa(IReadOnlyList<Point3> pontos) =>
        (pontos.Min(p => p.X), pontos.Min(p => p.Y), pontos.Max(p => p.X), pontos.Max(p => p.Y));

    /// <summary>O retângulo (alinhado a X e Y) crescido de <paramref name="d"/> para cada lado.</summary>
    private static IReadOnlyList<(double X, double Y)> Crescer(IReadOnlyList<(double X, double Y)> cantos, double d)
    {
        var (minX, maxX, minY, maxY) = (cantos.Min(c => c.X), cantos.Max(c => c.X), cantos.Min(c => c.Y), cantos.Max(c => c.Y));
        return [(minX - d, minY - d), (maxX + d, minY - d), (maxX + d, maxY + d), (minX - d, maxY + d)];
    }

    private static IReadOnlyList<Point3> Retangulo(double x, double y, double w, double l, double z) =>
        EquipmentFootprint.Corners(x, y, w, l).Select(c => new Point3(c.X, c.Y, z)).ToList();

    /// <summary>As medidas da área (o lado comprido e o curto do menor retângulo que a contém), para o aviso.</summary>
    public static (double Long, double Short) Measures(IReadOnlyList<Point3> poligono)
    {
        ArgumentNullException.ThrowIfNull(poligono);
        if (poligono.Count < 3) return (0, 0);
        var (_, _, _, mu, mv) = MenorRetangulo(poligono);
        return (2 * mu, 2 * mv);
    }

    /// <summary>
    /// A área de inversores onde está o ponto (o centro do bloco do inversor):
    /// a regra única de "o inversor é desta área" (item 2 da segunda rodada
    /// de 10/10/2026: "a área 1 é um objeto que recebe coisas dentro dela").
    /// Vale o CENTRO, não a pegada inteira: o Renan pôs o 9 e o 10 à mão
    /// dentro da Área 1 com 6,5 cm da caixa passando da borda, e eles são da
    /// área. Num ponto dentro de duas (uma dentro da outra), a menor. Null se
    /// em nenhuma.
    /// </summary>
    public static Guid? AreaOf(double x, double y, IEnumerable<(Guid Id, IReadOnlyList<Point3> Contour)> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        Guid? melhor = null;
        var menor = double.PositiveInfinity;
        foreach (var (id, contorno) in areas)
        {
            if (contorno.Count < 3 || !Polygons.Contains(contorno, x, y)) continue;
            var area = Math.Abs(AreaDe(contorno));
            if (area < menor || (area == menor && melhor is { } m && id.CompareTo(m) < 0))
            {
                menor = area;
                melhor = id;
            }
        }

        return melhor;
    }

    /// <summary>A área em planta do polígono (m², sem sinal).</summary>
    public static double PlanArea(IReadOnlyList<Point3> poligono) => Math.Abs(AreaDe(poligono));

    private static double AreaDe(IReadOnlyList<Point3> p)
    {
        var s = 0.0;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            s += a.X * b.Y - b.X * a.Y;
        }

        return s / 2;
    }

    /// <summary>
    /// O local de cada inversor pela GEOMETRIA, não pelo caminho (item 2 da
    /// segunda rodada): em campo com o centro numa área, é dessa área (posto
    /// pelo Escolher área, pelo Pôr em campo, pelo Mover ou pelo MOVE/COPY do
    /// AutoCAD); em campo fora de qualquer área, a área que o registro dizia
    /// cai (à mão) e o automático fica automático; fora de campo, o registro
    /// de área cai (não há caixa para estar dentro) e o automático fica. O
    /// registro de inversor que não está em <paramref name="inversores"/>
    /// (outro cadastro) fica como está. A lista nova (os de fora do cadastro
    /// primeiro, depois na ordem do cadastro).
    /// </summary>
    public static List<InverterPlacement> Reconcile(IReadOnlyList<InverterPlacement> registro, IEnumerable<Guid> inversores,
        IReadOnlyDictionary<Guid, (double X, double Y)> emCampo, IReadOnlyList<(Guid Id, IReadOnlyList<Point3> Contour)> areas)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(inversores);
        ArgumentNullException.ThrowIfNull(emCampo);
        ArgumentNullException.ThrowIfNull(areas);

        var doCadastro = inversores.Distinct().ToList();
        var noCadastro = doCadastro.ToHashSet();
        var porInversor = registro.GroupBy(r => r.Inverter).ToDictionary(g => g.Key, g => g.First());
        var saida = registro.Where(r => !noCadastro.Contains(r.Inverter)).ToList();

        foreach (var id in doCadastro)
        {
            var atual = porInversor.GetValueOrDefault(id);
            InverterPlacement? novo;
            if (emCampo.TryGetValue(id, out var c))
            {
                novo = AreaOf(c.X, c.Y, areas) is { } area
                    ? new InverterPlacement(id, InverterPlacementMode.Area, area)
                    : atual is { Mode: InverterPlacementMode.Automatic } ? atual : null;
            }
            else novo = atual is { Mode: InverterPlacementMode.Automatic } ? atual : null;

            if (novo is not null) saida.Add(novo);
        }

        return saida;
    }

    /// <summary>
    /// O que se grava no registro a partir do local pela geometria
    /// (<paramref name="certo"/>): o mesmo, menos num caso (correção de
    /// 10/10/2026, à noite). O automático que o usuário levou com o MOVE para
    /// dentro de uma área aparece como da área (a geometria manda na tabela e
    /// na rota), mas a ESCOLHA de automático fica gravada: tirado da área,
    /// ele volta a ser automático, em vez de cair para "à mão" só porque
    /// alguma gravação passou no meio. Quem foi mudado de propósito
    /// (<paramref name="mudados"/>: Escolher área, À mão) fica como o certo diz.
    /// </summary>
    public static List<InverterPlacement> ForRecord(IReadOnlyList<InverterPlacement> gravado, IReadOnlyList<InverterPlacement> certo, IReadOnlyCollection<Guid>? mudados = null)
    {
        ArgumentNullException.ThrowIfNull(gravado);
        ArgumentNullException.ThrowIfNull(certo);
        var automaticos = gravado.Where(g => g.Mode == InverterPlacementMode.Automatic).Select(g => g.Inverter).ToHashSet();
        return [.. certo.Select(c => c.Mode == InverterPlacementMode.Area && automaticos.Contains(c.Inverter) && (mudados is null || !mudados.Contains(c.Inverter))
            ? new InverterPlacement(c.Inverter, InverterPlacementMode.Automatic, Guid.Empty)
            : c)];
    }

    /// <summary>O menor contorno que contém o ponto (área dentro de área: a de dentro), ou null.</summary>
    public static IReadOnlyList<Point3>? SmallestAt(double x, double y, IEnumerable<IReadOnlyList<Point3>> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        IReadOnlyList<Point3>? melhor = null;
        var menor = double.PositiveInfinity;
        foreach (var contorno in areas)
        {
            if (contorno.Count < 3 || !Polygons.Contains(contorno, x, y)) continue;
            var area = Math.Abs(AreaDe(contorno));
            if (area < menor)
            {
                menor = area;
                melhor = contorno;
            }
        }

        return melhor;
    }

    /// <summary>Se as duas listas de local dizem o mesmo (a ordem não importa).</summary>
    public static bool SamePlacements(IReadOnlyList<InverterPlacement> a, IReadOnlyList<InverterPlacement> b) =>
        a.Count == b.Count && a.ToHashSet().SetEquals(b);

    /// <summary>Se o retângulo está inteiro dentro do polígono: os cantos dentro e nenhum lado do polígono cortando os dele.</summary>
    public static bool Dentro(IReadOnlyList<Point3> poligono, IReadOnlyList<(double X, double Y)> cantos)
    {
        if (!cantos.All(c => Polygons.Contains(poligono, c.X, c.Y))) return false;
        var caixa = cantos.Select(c => new Point3(c.X, c.Y, 0)).ToList();
        for (var i = 0; i < poligono.Count; i++)
            for (var j = 0; j < caixa.Count; j++)
                if (Polygons.SegmentsCross(poligono[i], poligono[(i + 1) % poligono.Count], caixa[j], caixa[(j + 1) % caixa.Count])) return false;
        return true;
    }

    /// <summary>Se o retângulo (cantos) e o polígono se sobrepõem em planta: um vértice (ou o centro) dentro do outro, ou lados que se cruzam.</summary>
    public static bool Overlaps(IReadOnlyList<(double X, double Y)> cantos, IReadOnlyList<Point3> poligono)
    {
        if (poligono.Count < 3) return false;
        var caixa = cantos.Select(c => new Point3(c.X, c.Y, 0)).ToList();
        if (caixa.Any(c => Polygons.Contains(poligono, c.X, c.Y)) || poligono.Any(q => Polygons.Contains(caixa, q.X, q.Y))) return true;
        if (Polygons.Contains(poligono, caixa.Average(c => c.X), caixa.Average(c => c.Y))) return true;
        for (var i = 0; i < poligono.Count; i++)
            for (var j = 0; j < caixa.Count; j++)
                if (Polygons.SegmentsCross(poligono[i], poligono[(i + 1) % poligono.Count], caixa[j], caixa[(j + 1) % caixa.Count])) return true;
        return false;
    }

    /// <summary>O menor retângulo que contém o polígono, com um lado num lado dele: centro, eixos (u o comprido) e meias medidas.</summary>
    private static (Point3 Centro, Point3 U, Point3 V, double MeioU, double MeioV) MenorRetangulo(IReadOnlyList<Point3> poligono)
    {
        (Point3, Point3, Point3, double, double)? melhor = null;
        var menorArea = double.PositiveInfinity;

        for (var i = 0; i < poligono.Count; i++)
        {
            var a = poligono[i];
            var b = poligono[(i + 1) % poligono.Count];
            var d = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (d < 1e-9) continue;

            var u = new Point3((b.X - a.X) / d, (b.Y - a.Y) / d, 0);
            var v = new Point3(-u.Y, u.X, 0);
            var su = poligono.Select(p => p.X * u.X + p.Y * u.Y).ToList();
            var sv = poligono.Select(p => p.X * v.X + p.Y * v.Y).ToList();
            var area = (su.Max() - su.Min()) * (sv.Max() - sv.Min());
            if (area >= menorArea) continue;

            menorArea = area;
            var cu = (su.Max() + su.Min()) / 2;
            var cv = (sv.Max() + sv.Min()) / 2;
            var centro = new Point3(u.X * cu + v.X * cv, u.Y * cu + v.Y * cv, 0);
            var (mu, mv) = ((su.Max() - su.Min()) / 2, (sv.Max() - sv.Min()) / 2);
            // O lado comprido vira u; v é sempre o perpendicular a ele. Até
            // 10/10/2026 o v do caso "primeiro lado é o curto" saía -v (paralelo
            // ao novo u): as fileiras andavam na mesma reta e os inversores
            // caíam um em cima do outro numa coluna no meio da área (Renan, item 2).
            melhor = mu >= mv ? (centro, u, v, mu, mv) : (centro, v, new Point3(-u.X, -u.Y, 0), mv, mu);
        }

        return melhor ?? throw new ArgumentException("o polígono não tem lado", nameof(poligono));
    }

    /// <summary>
    /// Uma string para o automático: para cada lado da fileira por onde ela
    /// pode sair, as batidas na vala do + e do − e quanto o cabo anda antes
    /// delas (a saída da mesa). O router escolhe o lado de menor soma (18.2):
    /// a conta aqui é a mesma.
    /// </summary>
    public sealed record StringAccess(IReadOnlyList<(TrenchPoint Hit, double Before)[]> Sides);

    /// <summary>
    /// O ponto da rede de valas de menor cabo CC para as strings: a soma, em
    /// cada string, do lado mais curto (saída da mesa até a vala e pela vala
    /// até o ponto, do + e do −). Candidatos: os nós da rede e as batidas
    /// (entre eles a soma não tem mínimo). Se as valas das strings não se
    /// ligam todas (redes separadas), vale o ponto que alcança mais strings e,
    /// entre esses, o de menor soma; Reached diz quantas. Null se nenhuma
    /// string chega à rede.
    /// </summary>
    public static (TrenchPoint Point, Point3 Direction, double Total, int Reached)? BestTrenchPoint(TrenchNetwork valas, IReadOnlyList<StringAccess> strings) =>
        RankTrenchPoints(valas, strings) is { Count: > 0 } r ? r[0] : null;

    /// <summary>
    /// Os candidatos de <see cref="BestTrenchPoint"/> do melhor para o pior:
    /// mais strings alcançadas antes, depois a menor soma (empate: menor X, depois menor Y).
    /// Vazia se nenhuma string chega à rede.
    /// </summary>
    public static IReadOnlyList<(TrenchPoint Point, Point3 Direction, double Total, int Reached)> RankTrenchPoints(TrenchNetwork valas, IReadOnlyList<StringAccess> strings)
    {
        ArgumentNullException.ThrowIfNull(valas);
        ArgumentNullException.ThrowIfNull(strings);
        var uteis = strings.Where(s => s.Sides.Count > 0).ToList();
        if (uteis.Count == 0) return [];

        // Uma árvore de caminhos por batida (Dijkstra uma vez, consultada em todos os candidatos).
        var arvores = new Dictionary<(int, double, double), TrenchNetwork.TrenchTree>();
        TrenchNetwork.TrenchTree Arvore(TrenchPoint p)
        {
            var chave = (p.Edge, Math.Round(p.At.X, 6), Math.Round(p.At.Y, 6));
            if (!arvores.TryGetValue(chave, out var a)) arvores[chave] = a = valas.Tree(p);
            return a;
        }

        var candidatos = valas.Nodes().ToList();
        foreach (var s in uteis)
            foreach (var lado in s.Sides)
                foreach (var (hit, _) in lado)
                    candidatos.Add((hit, valas.Direction(hit.Edge)));

        var avaliados = new List<(TrenchPoint, Point3, double, int)>();
        foreach (var (ponto, direcao) in candidatos)
        {
            var total = 0.0;
            var alcancadas = 0;
            foreach (var s in uteis)
            {
                var desta = s.Sides.Min(lado => lado.Sum(x => x.Before + Arvore(x.Hit).Length(ponto)));
                if (double.IsInfinity(desta)) continue;
                total += desta;
                alcancadas++;
            }

            if (alcancadas == 0) continue;
            avaliados.Add((ponto, direcao, total, alcancadas));
        }

        // Desempate pela posição (correção de 10/10/2026): somas iguais ao
        // micrômetro vão do ponto de menor X e, nele, de menor Y. Antes o empate
        // ficava com o primeiro candidato, e a ordem dos candidatos segue a ordem
        // das strings lidas do desenho: o mesmo desenho podia pôr o inversor em
        // lugares diferentes conforme a leitura.
        return [.. avaliados.OrderByDescending(a => a.Item4).ThenBy(a => Math.Round(a.Item3, 6))
            .ThenBy(a => Math.Round(a.Item1.At.X, 6)).ThenBy(a => Math.Round(a.Item1.At.Y, 6))];
    }

    /// <summary>
    /// O lugar do inversor automático (correção de 10/10/2026, à noite): ao
    /// lado da vala no ponto de menor cabo CC, mas NUNCA dentro de uma área
    /// de inversores (<paramref name="areas"/>: o automático não é de área
    /// nenhuma; antes, a rota podia pô-lo dentro de uma sala e a geometria o
    /// fazia "da área"). Se os dois lados do melhor ponto caem numa área, vale
    /// o ponto seguinte da lista (<see cref="RankTrenchPoints"/>) que alcança
    /// as mesmas strings. Moved diz se saiu do melhor ponto. Null se nenhuma
    /// string chega à rede (<paramref name="alcanca"/> falso) ou se todo ponto
    /// cai dentro de uma área.
    /// </summary>
    public static (Point3 Center, bool Free, double Total, int Reached, bool Moved)? AutomaticSite(TrenchNetwork valas, IReadOnlyList<StringAccess> strings,
        double largura, double comprimento, Func<IReadOnlyList<(double X, double Y)>, bool> livre, IReadOnlyList<IReadOnlyList<Point3>> areas, out bool alcanca)
    {
        ArgumentNullException.ThrowIfNull(livre);
        ArgumentNullException.ThrowIfNull(areas);
        var ranking = RankTrenchPoints(valas, strings);
        alcanca = ranking.Count > 0;
        if (ranking.Count == 0) return null;

        bool NaArea(IReadOnlyList<(double X, double Y)> cantos) => areas.Any(a => Overlaps(cantos, a));
        for (var i = 0; i < ranking.Count && ranking[i].Reached == ranking[0].Reached; i++)
        {
            var c = ranking[i];
            if (BesideTrench(c.Point.At, valas.DirectionsAt(c.Point), largura, comprimento, livre, NaArea) is { } lugar)
                return (lugar.Center, lugar.Free, c.Total, c.Reached, i > 0);
        }

        return null;
    }

    /// <summary>
    /// O centro do retângulo do inversor ao lado da vala, no ponto
    /// <paramref name="at"/>: para cada trecho que passa ali
    /// (<paramref name="direcoes"/>; num cruzamento, mais de um), dos dois
    /// lados, afastado o bastante para a caixa não ficar em cima dele, mais
    /// <paramref name="folga"/>; o primeiro lugar livre (<paramref name="livre"/>:
    /// fora das mesas, dos equipamentos e das valas). Nenhum livre: o primeiro
    /// lugar, com Free falso, para quem chama avisar.
    /// </summary>
    public static (Point3 Center, bool Free) BesideTrench(Point3 at, IReadOnlyList<Point3> direcoes, double largura, double comprimento, Func<IReadOnlyList<(double X, double Y)>, bool> livre, double folga = Gap) =>
        BesideTrench(at, direcoes, largura, comprimento, livre, _ => false, folga) ?? (at, false);

    /// <summary>
    /// O mesmo, com lugares <paramref name="proibido"/>s (dentro de uma área
    /// de inversores): nunca devolvidos, nem como o "primeiro lugar" sem
    /// folga. Null se todos os lugares em volta do ponto são proibidos.
    /// </summary>
    public static (Point3 Center, bool Free)? BesideTrench(Point3 at, IReadOnlyList<Point3> direcoes, double largura, double comprimento,
        Func<IReadOnlyList<(double X, double Y)>, bool> livre, Func<IReadOnlyList<(double X, double Y)>, bool> proibido, double folga = Gap)
    {
        ArgumentNullException.ThrowIfNull(direcoes);
        ArgumentNullException.ThrowIfNull(livre);
        ArgumentNullException.ThrowIfNull(proibido);
        Point3? primeiro = null;
        foreach (var direcao in direcoes)
        {
            var n = new Point3(-direcao.Y, direcao.X, 0);
            var afastamento = (Math.Abs(largura * n.X) + Math.Abs(comprimento * n.Y)) / 2 + folga;

            foreach (var sinal in new[] { 1.0, -1.0 })
            {
                var c = new Point3(at.X + n.X * afastamento * sinal, at.Y + n.Y * afastamento * sinal, 0);
                var cantos = EquipmentFootprint.Corners(c.X, c.Y, largura, comprimento);
                if (proibido(cantos)) continue;
                primeiro ??= c;
                if (livre(cantos)) return (c, true);
            }
        }

        return primeiro is { } p ? (p, false) : null;
    }
}
