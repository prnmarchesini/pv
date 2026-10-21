namespace Clivus.Core;

/// <summary>O que cada traço do gráfico é: o plugin escolhe cor e camada por isto.</summary>
public enum ChartLineKind
{
    Frame,
    Grid,
    Terrain,
    Bottom,
    WallTop,
    Alert,
    Arrow,
}

/// <summary>Um traço do gráfico, em coordenadas do gráfico (metro do desenho, origem no canto de baixo à esquerda da moldura).</summary>
public sealed record ChartLine(ChartLineKind Kind, IReadOnlyList<(double X, double Y)> Points);

/// <summary>Alinhamento horizontal do texto.</summary>
public enum ChartTextAlign
{
    Left,
    Center,
    Right,
}

/// <summary>Um texto do gráfico, sempre na horizontal (regra de 07/10/2026: texto nunca virado nem em pé).</summary>
public sealed record ChartText(double X, double Y, double Height, string Text, ChartTextAlign Align);

/// <summary>O gráfico pronto para desenhar.</summary>
public sealed record ChartDrawing(IReadOnlyList<ChartLine> Lines, IReadOnlyList<ChartText> Texts, double Width, double Height, double Exaggeration);

/// <summary>O que vai no gráfico do perfil.</summary>
public sealed record ChartInput
{
    public string Title { get; init; } = string.Empty;

    /// <summary>O terreno: (distância desde a montante, cota).</summary>
    public IReadOnlyList<(double D, double Z)> Terrain { get; init; } = [];

    /// <summary>O fundo da canaleta, quando é canaleta.</summary>
    public IReadOnlyList<(double D, double Z)>? Bottom { get; init; }

    /// <summary>A altura da parede: o topo é o fundo mais isto (canaleta).</summary>
    public double WallHeight { get; init; }

    /// <summary>Os trechos de declividade da série principal (o fundo na canaleta, o terreno na linha).</summary>
    public IReadOnlyList<SlopeSegment> Segments { get; init; } = [];

    /// <summary>Os trechos ruins do fundo (abaixo da mínima, planos ou subindo): ganham o traço de alerta.</summary>
    public IReadOnlyList<SlopeSegment> Alerts { get; init; } = [];

    /// <summary>Exagero vertical; zero ou negativo = automático.</summary>
    public double VerticalExaggeration { get; init; }
}

/// <summary>
/// O gráfico do perfil (passo 27.3), pronto: o Renan pediu "faço linha e
/// surge gráfico", sem configurar vista de perfil, bandas e estilos do
/// Civil 3D. Tudo aqui é geometria pura; o plugin só desenha.
///
/// No eixo horizontal, metro de verdade (o gráfico tem o comprimento da
/// linha); no vertical, a cota com exagero. Embaixo, a banda com distância e
/// cotas; em cima, o título com os números da linha. Sobre cada trecho, a
/// declividade em % e em graus e uma seta no sentido da água.
/// </summary>
public static class ProfileChart
{
    private static readonly double[] PassosBonitos = [0.1, 0.2, 0.25, 0.5, 1, 2, 2.5, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];
    private static readonly double[] ExagerosBonitos = [1, 2, 5, 10, 20, 50];

    /// <summary>O menor passo "bonito" maior ou igual ao valor.</summary>
    public static double NiceStep(double value) => PassosBonitos.FirstOrDefault(p => p >= value - 1e-12, PassosBonitos[^1]);

    /// <summary>
    /// O exagero automático: a altura do gráfico perto de um terço do
    /// comprimento, no maior valor bonito que não passe disso (mínimo 1).
    /// </summary>
    public static double AutoExaggeration(double length, double relief)
    {
        var alvo = 0.35 * length / Math.Max(relief, 0.5);
        return ExagerosBonitos.LastOrDefault(e => e <= alvo + 1e-9, 1);
    }

    public static ChartDrawing Build(ChartInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Terrain.Count < 2) throw new ArgumentException(Tr.T("O perfil precisa de pelo menos dois pontos."));

        var cultura = Tr.Culture;
        var comprimento = input.Terrain[^1].D;
        if (!(comprimento > 0)) throw new ArgumentException(Tr.T("A linha tem comprimento zero."));

        var cotas = input.Terrain.Select(p => p.Z)
            .Concat(input.Bottom?.Select(p => p.Z) ?? [])
            .Concat(input.Bottom?.Select(p => p.Z + input.WallHeight) ?? [])
            .ToList();

        var zMin = cotas.Min();
        var zMax = cotas.Max();
        var relevo = Math.Max(zMax - zMin, 0.5);

        var exagero = input.VerticalExaggeration > 0 ? Math.Min(input.VerticalExaggeration, 100) : AutoExaggeration(comprimento, relevo);
        var passoZ = NiceStep(relevo / 6);
        var passoD = NiceStep(comprimento / 8);
        var th = Math.Clamp(comprimento / 90, 0.3, 5);

        var zBase = Math.Floor(zMin / passoZ) * passoZ - passoZ;
        var zTopo = Math.Ceiling(zMax / passoZ) * passoZ + passoZ;
        double Y(double z) => (z - zBase) * exagero;
        var altura = Y(zTopo);

        var linhas = new List<ChartLine>();
        var textos = new List<ChartText>();

        string N(double v, string formato = "0.00") => v.ToString(formato, cultura);

        // Moldura e grade.
        linhas.Add(new ChartLine(ChartLineKind.Frame, [(0, 0), (comprimento, 0), (comprimento, altura), (0, altura), (0, 0)]));

        for (var z = zBase; z <= zTopo + 1e-9; z += passoZ)
        {
            linhas.Add(new ChartLine(ChartLineKind.Grid, [(0, Y(z)), (comprimento, Y(z))]));
            textos.Add(new ChartText(-0.5 * th, Y(z) - 0.4 * th, 0.8 * th, N(z), ChartTextAlign.Right));
        }

        // A banda de baixo: distância e cotas a cada passo, mais a última.
        var linhasDaBanda = new List<(string Rotulo, Func<double, string> Valor)>
        {
            (Tr.T("Distância (m)"), d => N(d, "0.0")),
            (Tr.T("Terreno (m)"), d => N(Interpolar(input.Terrain, d))),
        };

        if (input.Bottom is { Count: > 1 } fundo)
            linhasDaBanda.Add((Tr.T("Fundo (m)"), d => N(Interpolar(fundo, d))));

        var distancias = new List<double>();
        for (var d = 0.0; d < comprimento - 0.3 * passoD; d += passoD) distancias.Add(d);
        distancias.Add(comprimento);

        foreach (var d in distancias)
            linhas.Add(new ChartLine(ChartLineKind.Grid, [(d, -(linhasDaBanda.Count + 0.5) * 1.6 * th), (d, altura)]));

        for (var i = 0; i < linhasDaBanda.Count; i++)
        {
            var y = -(i + 1) * 1.6 * th;
            textos.Add(new ChartText(-0.5 * th, y, 0.8 * th, linhasDaBanda[i].Rotulo, ChartTextAlign.Right));

            // As pontas para dentro da moldura: o primeiro valor centrado no
            // zero encostava no rótulo da linha da banda.
            for (var k = 0; k < distancias.Count; k++)
            {
                var d = distancias[k];
                var (x, alinhamento) = k == 0 ? (d + 0.2 * th, ChartTextAlign.Left)
                    : k == distancias.Count - 1 ? (d - 0.2 * th, ChartTextAlign.Right)
                    : (d, ChartTextAlign.Center);
                textos.Add(new ChartText(x, y, 0.7 * th, linhasDaBanda[i].Valor(d), alinhamento));
            }

            linhas.Add(new ChartLine(ChartLineKind.Grid, [(0, y - 0.5 * th), (comprimento, y - 0.5 * th)]));
        }

        // As séries.
        linhas.Add(new ChartLine(ChartLineKind.Terrain, input.Terrain.Select(p => (p.D, Y(p.Z))).ToList()));

        if (input.Bottom is { Count: > 1 } fundoDaSerie)
        {
            linhas.Add(new ChartLine(ChartLineKind.Bottom, fundoDaSerie.Select(p => (p.D, Y(p.Z))).ToList()));
            linhas.Add(new ChartLine(ChartLineKind.WallTop, fundoDaSerie.Select(p => (p.D, Y(p.Z + input.WallHeight))).ToList()));
        }

        // Sobre cada trecho: "% | grau" e a seta no sentido da água.
        var principal = input.Bottom is { Count: > 1 } b2 ? b2 : input.Terrain;

        foreach (var s in input.Segments)
        {
            var meio = (s.From + s.To) / 2;
            var topoDoTrecho = new[] { s.From, meio, s.To }.Max(d => Interpolar(input.Terrain, d));
            var yTexto = Y(topoDoTrecho) + 1.6 * th;

            textos.Add(new ChartText(meio, yTexto, 0.8 * th, Tr.F("{0} | {1}°", N(s.Percent, "0.0") + "%", N(s.Degrees, "0.0")), ChartTextAlign.Center));

            if (Math.Abs(s.Slope) > 1e-4)
            {
                var meiaSeta = Math.Min(0.2 * s.Length, 3 * th);
                var sentido = s.Slope > 0 ? 1 : -1;
                var yS = yTexto - 0.6 * th;
                var (x0, x1) = (meio - sentido * meiaSeta, meio + sentido * meiaSeta);
                var aba = 0.5 * th;

                linhas.Add(new ChartLine(ChartLineKind.Arrow, [(x0, yS), (x1, yS)]));
                linhas.Add(new ChartLine(ChartLineKind.Arrow, [(x1 - sentido * aba, yS + 0.35 * aba), (x1, yS), (x1 - sentido * aba, yS - 0.35 * aba)]));
            }

        }

        // Os trechos ruins ganham o traço de alerta sobre a série principal.
        foreach (var s in input.Alerts)
        {
            var trecho = principal.Where(p => p.D >= s.From - 1e-9 && p.D <= s.To + 1e-9).Select(p => (p.D, Y(p.Z))).ToList();
            if (trecho.Count < 2) trecho = [(s.From, Y(s.ZFrom)), (s.To, Y(s.ZTo))];
            linhas.Add(new ChartLine(ChartLineKind.Alert, trecho));
        }

        // O título, em duas linhas, em cima da moldura.
        var media = comprimento > 0 ? (principal[0].Z - principal[^1].Z) / comprimento : 0;
        textos.Add(new ChartText(0, altura + 2.6 * th, 1.2 * th, input.Title, ChartTextAlign.Left));
        textos.Add(new ChartText(0, altura + 1.0 * th, 0.8 * th,
            Tr.F("Comprimento {0} m · cotas {1} a {2} m · declividade média {3}% · exagero vertical {4}×",
                N(comprimento, "0.0"), N(zMin), N(zMax), N(media * 100, "0.0"), N(exagero, "0.#")),
            ChartTextAlign.Left));

        return new ChartDrawing(linhas, textos, comprimento, altura, exagero);
    }

    private static double Interpolar(IReadOnlyList<(double D, double Z)> serie, double d)
    {
        if (d <= serie[0].D) return serie[0].Z;
        if (d >= serie[^1].D) return serie[^1].Z;

        int lo = 0, hi = serie.Count - 1;
        while (hi - lo > 1)
        {
            var meio = (lo + hi) / 2;
            if (serie[meio].D <= d) lo = meio; else hi = meio;
        }

        var (a, b) = (serie[lo], serie[hi]);
        return b.D > a.D ? a.Z + (b.Z - a.Z) * (d - a.D) / (b.D - a.D) : a.Z;
    }
}
