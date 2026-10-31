using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O que entra no resumo de uma mesa (42.9), lido do desenho.</summary>
/// <param name="Table">O GUID da mesa.</param>
/// <param name="Label">O letreiro.</param>
/// <param name="TypeName">O tipo de mesa (o nome do perfil), se ela tem um.</param>
/// <param name="Structure">A estrutura: monoposte, biposte, carport.</param>
/// <param name="Module">O módulo do tipo, se ele é conhecido.</param>
/// <param name="ModulePowerWatts">A potência gravada na mesa (a da simulação, se houve), ou null.</param>
/// <param name="Faces">As faces dos módulos desenhados.</param>
/// <param name="DesignTiltRadians">O tilt do projeto (o gravado na mesa).</param>
/// <param name="Pillars">Os pilares da mesa.</param>
/// <param name="Dirty">Se a mesa está suja.</param>
/// <param name="DirtyReason">Por quê.</param>
/// <param name="Marked">Se a mesa está marcada (não coube).</param>
/// <param name="MarkReason">Por quê.</param>
/// <param name="Latitude">A latitude do desenho (o hemisfério, para o azimute do PVsyst), ou null.</param>
/// <param name="TerrainZ">A cota do terreno em (x, y), ou null fora dele; null sem terreno.</param>
public sealed record TableSummaryInput(
    Guid Table,
    string Label,
    string? TypeName,
    string Structure,
    SolarModule? Module,
    double? ModulePowerWatts,
    IReadOnlyList<PvModuleFace> Faces,
    double DesignTiltRadians,
    IReadOnlyList<PillarIdentity> Pillars,
    bool Dirty,
    string? DirtyReason,
    bool Marked,
    string? MarkReason,
    double? Latitude,
    Func<double, double, double?>? TerrainZ);

/// <summary>Um intervalo de medidas (o menor e o maior).</summary>
public readonly record struct Span(double Min, double Max);

/// <summary>
/// O resumo de uma mesa (42.9, Renan, 31/10/2026: "quero ter resumo e aí
/// quero que me mostre a inclinação da mesa em relação ao azimute (o tilt), a
/// inclinação do terreno e para qual lado, quantos módulos, potência, tipo de
/// estrutura, enfim, crie um resumo completo e legal"). Os ângulos vêm da
/// mesa como está desenhada (o plano das faces dos módulos), não do perfil.
/// </summary>
public sealed record TableSummary(
    string Label,
    string? TypeName,
    string Structure,
    int Modules,
    int Columns,
    int Rows,
    string? ModuleName,
    double? ModulePowerWatts,
    double? PowerKwp,
    double? Length,
    double? Depth,
    double? TiltDegrees,
    double DesignTiltDegrees,
    double? FacingDegrees,
    double? PvsystAzimuthDegrees,
    bool? SouthernHemisphere,
    double? AxisSlopePercent,
    double? AxisDownhillDegrees,
    double? TerrainSlopePercent,
    double? TerrainDownhillDegrees,
    double? TerrainAlongPercent,
    double? TerrainAlongDownhillDegrees,
    double? TerrainAcrossRisePercent,
    int Pillars,
    int FrontPillars,
    int RearPillars,
    Span? FreeHeight,
    Span? PillarLength,
    Span? Embedment,
    Span? LowEdgeClearance,
    Span? HighEdgeClearance,
    int PillarProblems,
    bool Dirty,
    string? DirtyReason,
    bool Marked,
    string? MarkReason)
{
    /// <summary>As seções do resumo, cada uma com as linhas "rótulo: valor", para a janela e a linha de comando.</summary>
    public IReadOnlyList<(string Title, IReadOnlyList<(string Label, string Value)> Lines)> Sections()
    {
        static string Faixa(Span? s, string unidade) => s is { } v
            ? Math.Abs(v.Max - v.Min) < 0.0005 ? Tr.F("{0:0.00} {1}", v.Min, unidade) : Tr.F("{0:0.00} a {1:0.00} {2}", v.Min, v.Max, unidade)
            : "—";

        static string Lado(double? graus) => graus is { } g ? Tr.F("{0} ({1:0}°)", TableSummaries.Cardinal(g), g) : "—";

        var mesa = new List<(string, string)>
        {
            (Tr.T("Tipo"), TypeName ?? Tr.T("sem tipo no desenho")),
            (Tr.T("Estrutura"), Structure),
            (Tr.T("Arranjo"), Tr.F("{0} coluna(s) × {1} fileira(s) de módulos", Columns, Rows)),
            (Tr.T("Tamanho"), Length is { } l && Depth is { } d ? Tr.F("{0:0.000} × {1:0.000} m (comprimento × largura)", l, d) : "—"),
            (Tr.T("Estado"), Marked ? Tr.F("não coube: {0}", MarkReason ?? "?") : Dirty ? Tr.F("suja: {0}", DirtyReason ?? "?") : Tr.T("em dia")),
        };

        var modulos = new List<(string, string)>
        {
            (Tr.T("Módulo"), ModuleName is { Length: > 0 } nome ? nome : "—"),
            (Tr.T("Quantidade"), Tr.F("{0} módulo(s)", Modules)),
            (Tr.T("Potência"), ModulePowerWatts is { } w && PowerKwp is { } k ? Tr.F("{0:0.###} kWp ({1} × {2:0.#} W)", k, Modules, w) : "—"),
        };

        var orientacao = new List<(string, string)>
        {
            (Tr.T("Tilt dos módulos"), TiltDegrees is { } t ? Tr.F("{0:0.0}° (o do projeto: {1:0.0}°)", t, DesignTiltDegrees) : "—"),
            (Tr.T("Voltada para"), Lado(FacingDegrees)),
            (Tr.T("Azimute no PVsyst"), PvsystAzimuthDegrees is { } a
                ? Tr.F("{0:0.0}° ({1})", a, SouthernHemisphere == true ? Tr.T("hemisfério sul: 0° é o norte, + é oeste") : Tr.T("hemisfério norte: 0° é o sul, + é oeste"))
                : "—"),
            (Tr.T("Ao longo da mesa"), AxisSlopePercent is { } p
                ? Tr.F("{0:0.0} % ({1:0.0}°), desce para o {2}", Math.Abs(p), Math.Atan(Math.Abs(p) / 100) * 180 / Math.PI, AxisDownhillDegrees is { } g ? TableSummaries.Cardinal(g) : "—")
                : "—"),
        };

        var terreno = new List<(string, string)>
        {
            (Tr.T("Inclinação"), TerrainSlopePercent is { } s
                ? Tr.F("{0:0.0} % ({1:0.0}°), desce para o {2}", s, Math.Atan(s / 100) * 180 / Math.PI, Lado(TerrainDownhillDegrees))
                : Tr.T("sem terreno sob a mesa")),
            (Tr.T("Ao longo da mesa"), TerrainAlongPercent is { } ao
                ? Tr.F("{0:0.0} %, desce para o {1}", ao, TerrainAlongDownhillDegrees is { } g2 ? TableSummaries.Cardinal(g2) : "—")
                : "—"),
            (Tr.T("Na largura"), TerrainAcrossRisePercent is { } la
                ? la >= 0 ? Tr.F("sobe {0:0.0} % para a ponta alta dos módulos", la) : Tr.F("desce {0:0.0} % para a ponta alta dos módulos", -la)
                : "—"),
        };

        var pilares = new List<(string, string)>
        {
            (Tr.T("Quantidade"), FrontPillars + RearPillars > 0
                ? Tr.F("{0} ({1} na frente, {2} atrás)", Pillars, FrontPillars, RearPillars)
                : Pillars.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            (Tr.T("Livre (acima do chão)"), Faixa(FreeHeight, "m")),
            (Tr.T("Enterro"), Faixa(Embedment, "m")),
            (Tr.T("Comprimento"), Faixa(PillarLength, "m")),
            (Tr.T("Ponta baixa do módulo ao chão"), Faixa(LowEdgeClearance, "m")),
            (Tr.T("Ponta alta do módulo ao chão"), Faixa(HighEdgeClearance, "m")),
        };
        if (PillarProblems > 0) pilares.Add((Tr.T("Com problema"), Tr.F("{0} pilar(es)", PillarProblems)));

        // A orientação primeiro: é o que o Renan queria ver ("a inclinação de uma mesa, o tilt").
        return
        [
            (Tr.T("Orientação"), orientacao),
            (Tr.T("Terreno sob a mesa"), terreno),
            (Tr.T("Mesa"), mesa),
            (Tr.T("Módulos"), modulos),
            (Tr.T("Pilares"), pilares),
        ];
    }

    /// <summary>O resumo em texto, uma seção por bloco, para copiar e para a linha de comando.</summary>
    public string Describe()
    {
        var linhas = new List<string> { Tr.F("Resumo da mesa {0}", Label) };

        foreach (var (titulo, itens) in Sections())
        {
            linhas.Add(string.Empty);
            linhas.Add(titulo);
            linhas.AddRange(itens.Select(i => $"  {i.Label}: {i.Value}"));
        }

        return string.Join(Environment.NewLine, linhas);
    }
}

/// <summary>
/// As contas do resumo da mesa: o plano dos módulos (tilt, para onde olha,
/// o azimute do PVsyst, o giro ao longo da mesa), o terreno sob a mesa (o
/// plano que melhor passa pelos pontos dele), os pilares e os módulos.
/// </summary>
public static class TableSummaries
{
    /// <summary>O resumo.</summary>
    public static TableSummary Build(TableSummaryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var faces = input.Faces ?? [];
        var fiadas = faces.Count > 0 ? PvFrames.FromFaces(input.Table, input.Label, faces, PvFrames.DefaultThickness) : null;
        var primeira = fiadas is { Problem: null, Frames.Count: > 0 } ? fiadas.Frames[0] : null;
        var inteira = fiadas is { Problem: null, OffGrid: false, Frames.Count: 1 } ? primeira : null;

        var colunas = faces.Select(f => f.Column).Distinct().Count();
        var fileiras = faces.Select(f => f.Row).Distinct().Count();

        double? tilt = null, voltada = null, azimutePvsyst = null, giro = null, giroDesce = null;
        double? terrenoInclinacao = null, terrenoDesce = null, terrenoAoLongo = null, terrenoAoLongoDesce = null, terrenoNaLargura = null;

        if (primeira is not null)
        {
            var c = primeira.Corners;
            var u = Menos(c[1], c[0]);
            var v = Menos(c[3], c[0]);
            var n = Cruzado(u, v);
            if (n.Z < 0) n = (-n.X, -n.Y, -n.Z);

            var tamanhoN = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
            tilt = Math.Acos(Math.Clamp(n.Z / tamanhoN, -1, 1)) * 180 / Math.PI;

            // Para onde os módulos olham: a normal em planta (nada, se a mesa está deitada).
            if (tilt > 0.01)
            {
                voltada = Bussola(n.X, n.Y);
                if (input.Latitude is { } lat) azimutePvsyst = PvsystAzimuth(voltada.Value, lat < 0);
            }

            // O giro ao longo da mesa: a subida do comprimento, e para que lado ele desce.
            var planta = Math.Sqrt(u.X * u.X + u.Y * u.Y);
            if (planta > 1e-9)
            {
                giro = u.Z / planta * 100;
                giroDesce = u.Z > 0 ? Bussola(-u.X, -u.Y) : Bussola(u.X, u.Y);
            }

            // O terreno sob a mesa: o plano dos pontos numa grade dentro de cada fiada.
            if (input.TerrainZ is { } cota && Plano(fiadas!.Frames, cota) is { } plano)
            {
                var (a, b) = plano;
                terrenoInclinacao = Math.Sqrt(a * a + b * b) * 100;
                if (terrenoInclinacao > 0.01) terrenoDesce = Bussola(-a, -b);

                if (planta > 1e-9)
                {
                    var (ux, uy) = (u.X / planta, u.Y / planta);
                    var subidaAoLongo = a * ux + b * uy;
                    terrenoAoLongo = Math.Abs(subidaAoLongo) * 100;
                    terrenoAoLongoDesce = subidaAoLongo > 0 ? Bussola(-ux, -uy) : Bussola(ux, uy);
                }

                var largura = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (largura > 1e-9) terrenoNaLargura = (a * v.X / largura + b * v.Y / largura) * 100;
            }
        }

        var potencia = input.ModulePowerWatts ?? input.Module?.PowerWatts;
        var pilares = input.Pillars ?? [];

        static Span? Faixa(IEnumerable<double?> valores)
        {
            var lista = valores.Where(x => x is { } y && double.IsFinite(y)).Select(x => x!.Value).ToList();
            return lista.Count == 0 ? null : new Span(lista.Min(), lista.Max());
        }

        return new TableSummary(
            input.Label,
            input.TypeName,
            input.Structure,
            faces.Count,
            colunas,
            fileiras,
            input.Module is { } m ? $"{m.Brand} {m.Model}".Trim() : null,
            potencia,
            potencia is { } w ? faces.Count * w / 1000 : null,
            inteira is null ? null : Distancia(inteira.Corners[0], inteira.Corners[1]),
            inteira is null ? null : Distancia(inteira.Corners[0], inteira.Corners[3]),
            tilt,
            input.DesignTiltRadians * 180 / Math.PI,
            voltada,
            azimutePvsyst,
            input.Latitude is { } la ? la < 0 : null,
            giro,
            giroDesce,
            terrenoInclinacao,
            terrenoDesce,
            terrenoAoLongo,
            terrenoAoLongoDesce,
            terrenoNaLargura,
            pilares.Count,
            pilares.Count(p => p.Line == PillarLine.Front),
            pilares.Count(p => p.Line == PillarLine.Rear),
            Faixa(pilares.Select(p => p.FreeHeight)),
            Faixa(pilares.Select(p => p.Length)),
            Faixa(pilares.Select(p => (double?)p.Embedment)),
            Faixa(pilares.Select(p => p.LowEdgeClearance)),
            Faixa(pilares.Select(p => p.HighEdgeClearance)),
            pilares.Count(p => p.Problem is { Length: > 0 }),
            input.Dirty,
            input.DirtyReason,
            input.Marked,
            input.MarkReason);
    }

    /// <summary>
    /// O azimute do PVsyst para quem olha no rumo <paramref name="bearing"/>
    /// (bússola, 0 = norte, horário): no hemisfério norte 0° é o sul, no sul 0°
    /// é o norte; nos dois, + para o oeste e − para o leste (help do PVsyst,
    /// "Plane azimuth"). Entre −180 e 180.
    /// </summary>
    public static double PvsystAzimuth(double bearing, bool southernHemisphere)
    {
        var a = southernHemisphere ? -bearing : bearing - 180;
        a %= 360;
        if (a <= -180) a += 360;
        if (a > 180) a -= 360;
        return a == 0 ? 0 : a;
    }

    /// <summary>O nome do rumo (bússola, 0 = norte, horário) em oito lados: "norte", "nordeste"...</summary>
    public static string Cardinal(double bearing)
    {
        var b = ((bearing % 360) + 360) % 360;
        return ((int)Math.Round(b / 45) % 8) switch
        {
            0 => Tr.T("norte"),
            1 => Tr.T("nordeste"),
            2 => Tr.T("leste"),
            3 => Tr.T("sudeste"),
            4 => Tr.T("sul"),
            5 => Tr.T("sudoeste"),
            6 => Tr.T("oeste"),
            _ => Tr.T("noroeste"),
        };
    }

    /// <summary>O rumo de bússola (0 = norte, horário, 0 a 360) de uma direção em planta (x = leste, y = norte).</summary>
    private static double Bussola(double x, double y)
    {
        var graus = Math.Atan2(x, y) * 180 / Math.PI;
        return graus < 0 ? graus + 360 : graus;
    }

    /// <summary>
    /// O plano z = a·x + b·y + c que melhor passa pelo terreno sob as fiadas
    /// (mínimos quadrados, numa grade de 9 × 5 pontos em cada uma), ou null
    /// com menos de três pontos no terreno ou todos alinhados.
    /// </summary>
    private static (double A, double B)? Plano(IReadOnlyList<PvFrame> fiadas, Func<double, double, double?> cota)
    {
        var pontos = new List<(double X, double Y, double Z)>();

        foreach (var f in fiadas)
        {
            var c = f.Corners;
            for (var i = 0; i <= 8; i++)
                for (var j = 0; j <= 4; j++)
                {
                    var s = i / 8.0;
                    var t = j / 4.0;
                    var x = c[0].X + s * (c[1].X - c[0].X) + t * (c[3].X - c[0].X);
                    var y = c[0].Y + s * (c[1].Y - c[0].Y) + t * (c[3].Y - c[0].Y);
                    if (cota(x, y) is { } z && double.IsFinite(z)) pontos.Add((x, y, z));
                }
        }

        if (pontos.Count < 3) return null;

        // Centrado: em UTM a conta perderia casas.
        var (mx, my, mz) = (pontos.Average(p => p.X), pontos.Average(p => p.Y), pontos.Average(p => p.Z));
        double sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0;

        foreach (var (x0, y0, z0) in pontos)
        {
            var (x, y, z) = (x0 - mx, y0 - my, z0 - mz);
            sxx += x * x; sxy += x * y; syy += y * y; sxz += x * z; syz += y * z;
        }

        var det = sxx * syy - sxy * sxy;
        if (Math.Abs(det) < 1e-9) return null;

        return ((sxz * syy - syz * sxy) / det, (syz * sxx - sxz * sxy) / det);
    }

    private static (double X, double Y, double Z) Menos(Point3 a, Point3 b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Cruzado((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
