namespace Clivus.Core;

/// <summary>Uma canaleta do desenho, com tudo o que a memória precisa dela.</summary>
/// <param name="Profile">O perfil do fundo (declividades, comprimento).</param>
/// <param name="Basin">A bacia dela (30), ou null se as bacias não foram calculadas com ela.</param>
/// <param name="RoadCrossings">Quantas vezes ela cruza uma via (camadas das vias dos parâmetros).</param>
public sealed record ChannelInput(Guid Id, string Name, ChannelSection Section, ChannelProfileResult Profile, BasinEntry? Basin, int RoadCrossings = 0);

/// <summary>O cálculo de uma canaleta: o que vai em cada tabela do memorial.</summary>
public sealed record ChannelCalculation
{
    public required ChannelInput Input { get; init; }

    // Tabela 1: escoamento superficial da bacia.
    public double KerbyTc { get; init; } = double.NaN;
    public double KirpichTc { get; init; } = double.NaN;
    public double Tc { get; init; } = double.NaN;
    public double SurfaceVelocity { get; init; } = double.NaN;

    // Tabela 5: vazão.
    public double Intensity { get; init; } = double.NaN;
    public double FlowLitersPerSecond { get; init; }
    public double Flow => FlowLitersPerSecond / 1000;

    // Tabela 8: hidráulica.
    public double MinSlope { get; init; }
    public double MaxSlope { get; init; }

    /// <summary>A declividade usada na lâmina: a mínima do perfil, ou a mínima da norma se o perfil não desce.</summary>
    public double SlopeForDepth { get; init; }

    public double MaxDepth { get; init; } = double.NaN;
    public double MaxVelocity { get; init; } = double.NaN;
    public double MinVelocity { get; init; } = double.NaN;
    public double Freeboard { get; init; }

    // Tabela 9: dimensões.
    public double SuggestedHeight { get; init; } = double.NaN;

    // Tabela 11 e transposição.
    public (double L1, double L2, double Length) Dissipator { get; init; }
    public string? DissipatorModel { get; init; }
    public string? CrossingModel { get; init; }

    public ChannelQuantities Quantities { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>O que está fora da regra (regra 4: nunca calado). O sistema não aprova nada (regra 5).</summary>
    public IReadOnlyList<string> Alerts { get; init; } = [];
}

/// <summary>A memória de cálculo inteira (31.3): uma linha por canaleta e as tabelas.</summary>
public sealed record DrainageMemoryResult(HydrologyParameters Parameters, IReadOnlyList<ChannelCalculation> Channels)
{
    public IEnumerable<string> Alerts => Channels.SelectMany(c => c.Alerts.Select(a => $"{c.Input.Name}: {a}"));
}

/// <summary>
/// A memória de cálculo da drenagem (passo 31), como no memorial de
/// Valparaiso: bacia (Tabela 1), chuva (3), vazão (5), canaletas (8),
/// dimensões (9), dissipadores (11), transposições e quantitativos. Calcula e
/// avisa; não escolhe seção nem aprova (regra 5 da drenagem).
/// </summary>
public static class DrainageMemory
{
    /// <summary>As durações da Tabela 3, em minutos: 5, 10, 15, 30 min, 1, 2, 4, 8, 12 e 24 h.</summary>
    public static readonly IReadOnlyList<double> Durations = [5, 10, 15, 30, 60, 120, 240, 480, 720, 1440];

    /// <summary>Os períodos de retorno da Tabela 3, em anos.</summary>
    public static readonly IReadOnlyList<double> ReturnPeriods = [15, 25, 50, 100];

    public static DrainageMemoryResult Build(HydrologyParameters parameters, IReadOnlyList<ChannelInput> channels)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(channels);

        if (parameters.Problem() is { } problema) throw new ArgumentException(problema);

        return new DrainageMemoryResult(parameters, channels.Select(c => Calculate(parameters, c)).ToList());
    }

    public static ChannelCalculation Calculate(HydrologyParameters p, ChannelInput c)
    {
        var alertas = new List<string>();
        var secao = c.Section;

        // Tabela 1 e 5: a bacia dá o tempo de concentração, a chuva e a vazão.
        double kerby = double.NaN, kirpich = double.NaN, tc = double.NaN, v = double.NaN, i = double.NaN, q = 0;

        if (c.Basin is not { } bacia)
        {
            alertas.Add(Tr.T("sem bacia calculada: a vazão fica zero. Calcule as bacias (Memória › Bacias)."));
        }
        else
        {
            kerby = Rainfall.Kerby(bacia.Length, bacia.SlopePercent, p.KerbyN);
            kirpich = Rainfall.Kirpich(bacia.Length, bacia.Drop);
            tc = Rainfall.AdoptedTc(kirpich, kerby, p.MinimumTc);
            if (!double.IsFinite(kerby) && !double.IsFinite(kirpich))
                alertas.Add(Tr.F("a bacia não tem caminho com desnível (L {0:0.0} m, ΔH {1:0.00} m): tc no mínimo de {2:0.#} min.", bacia.Length, bacia.Drop, p.MinimumTc));

            v = Rainfall.SurfaceVelocity(bacia.Length, tc);
            if (v > p.CoverMaxVelocity + 1e-9)
                alertas.Add(Tr.F("escoamento superficial de {0:0.00} m/s passa da máxima da cobertura ({1}: {2:0.00} m/s): risco de erosão.", v, p.Cover, p.CoverMaxVelocity));

            i = p.Intensity(tc);
            q = Rainfall.RationalFlowLitersPerSecond(p.RunoffC, i, bacia.Area);
            if (bacia.Area > Rainfall.RationalMaxAreaSquareMeters)
                alertas.Add(Tr.F("bacia de {0:0.0} ha: o método racional vale até 50 ha.", bacia.Area / 10_000));
        }

        var vazao = q / 1000;

        // Tabela 8: lâmina na mínima, velocidade na máxima.
        var perfil = c.Profile;
        var imin = perfil.MinSlope;
        var imax = perfil.MaxSlope;
        var iLamina = imin;

        if (imin <= 0)
        {
            iLamina = p.MinimumSlope;
            alertas.Add(Tr.F("o fundo tem trecho plano ou subindo: a lâmina foi calculada com a declividade mínima ({0:0.0}%).", p.MinimumSlope * 100));
        }
        else if (imin < p.MinimumSlope - 1e-9)
        {
            alertas.Add(Tr.F("declividade mínima do fundo de {0:0.00}% abaixo da norma ({1:0.0}%, NBR 10844).", imin * 100, p.MinimumSlope * 100));
        }

        if (imax <= 0) imax = iLamina;

        double hmax = 0, vmax = 0, vmin = 0;
        if (vazao > 0)
        {
            hmax = Hydraulics.NormalDepth(secao, vazao, iLamina);
            var hNaMaxima = Hydraulics.NormalDepth(secao, vazao, imax);
            vmax = double.IsNaN(hNaMaxima) ? double.NaN : Hydraulics.Velocity(secao, hNaMaxima, imax);
            vmin = double.IsNaN(hmax) ? double.NaN : Hydraulics.Velocity(secao, hmax, iLamina);
        }

        var f = Hydraulics.Freeboard(vazao);
        var sugerida = vazao > 0 ? Hydraulics.SuggestedHeight(secao, vazao, iLamina, p.HeightStep) : double.NaN;

        if (vazao > 0)
        {
            if (double.IsNaN(hmax))
                alertas.Add(Tr.F("a meia-cana não leva {0:0.###} m³/s nem cheia: aumente o raio.", vazao));
            else if (hmax + f > secao.UsefulHeight + 1e-9)
                alertas.Add(Tr.F("lâmina {0:0.000} m + bordo livre {1:0.00} m passa da altura da seção ({2:0.00} m); sugerida {3:0.00} m.", hmax, f, secao.UsefulHeight, sugerida));

            if (vmax > secao.MaxVelocity + 1e-9)
                alertas.Add(Tr.F("velocidade máxima {0:0.00} m/s passa da do revestimento ({1}: {2:0.00} m/s).", vmax, secao.Lining, secao.MaxVelocity));

            if (vmin < p.MinimumVelocity - 1e-9)
                alertas.Add(Tr.F("velocidade {0:0.00} m/s abaixo da mínima de autolimpeza ({1:0.0} m/s): acúmulo de terra.", vmin, p.MinimumVelocity));
        }

        // Tabela 11 e transposição pela abertura da seção.
        var abertura = secao.TopWidth;
        var dissipador = EnergyDissipator.Size(abertura);
        var modeloDes = EnergyDissipator.Model(abertura);
        if (modeloDes is null) alertas.Add(Tr.F("abertura de {0:0.00} m passa do maior dissipador do álbum do DNIT (DES 160-480).", abertura));

        string? modeloTss = null;
        if (c.RoadCrossings > 0)
        {
            modeloTss = ChannelCrossing.Model(abertura, p.CrossingMargin);
            if (modeloTss is null) alertas.Add(Tr.F("abertura de {0:0.00} m passa da maior transposição do álbum do DNIT (TSS 200).", abertura));
        }

        return new ChannelCalculation
        {
            Input = c,
            KerbyTc = kerby,
            KirpichTc = kirpich,
            Tc = tc,
            SurfaceVelocity = v,
            Intensity = i,
            FlowLitersPerSecond = q,
            MinSlope = imin,
            MaxSlope = perfil.MaxSlope,
            SlopeForDepth = iLamina,
            MaxDepth = hmax,
            MaxVelocity = vmax,
            MinVelocity = vmin,
            Freeboard = f,
            SuggestedHeight = sugerida,
            Dissipator = dissipador,
            DissipatorModel = modeloDes,
            CrossingModel = modeloTss,
            Quantities = ChannelQuantities.Of(perfil, secao),
            Alerts = alertas,
        };
    }

    /// <summary>As tabelas do memorial, prontas para a tela e o Excel.</summary>
    public static IReadOnlyList<CableTable> Tables(DrainageMemoryResult memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        var p = memoria.Parameters;
        var cs = memoria.Channels;
        static object? N(double v, int casas) => double.IsFinite(v) ? Math.Round(v, casas) : null;

        var tabelas = new List<CableTable>();

        tabelas.Add(new CableTable(Tr.T("Parâmetros"),
            [Tr.T("Parâmetro"), Tr.T("Valor"), Tr.T("Unidade")],
            [
                [Tr.T("Equação IDF (K·T^a/(t+b)^c)"), p.Source, ""],
                ["K", p.K, ""], ["a", p.A, ""], ["b", p.B, ""], ["c", p.C, ""],
                [Tr.T("Período de retorno"), p.ReturnPeriod, Tr.T("anos")],
                [Tr.T("Tempo de concentração mínimo"), p.MinimumTc, "min"],
                [Tr.T("Rugosidade de Kerby"), p.KerbyN, ""],
                [Tr.T("Coeficiente de escoamento C"), p.RunoffC, p.RunoffSurface],
                [Tr.T("Cobertura do terreno (vel. máx.)"), p.CoverMaxVelocity, "m/s · " + p.Cover],
                [Tr.T("Velocidade mínima de autolimpeza"), p.MinimumVelocity, "m/s"],
                [Tr.T("Declividade mínima"), p.MinimumSlope, "m/m"],
                [Tr.T("Passo da altura sugerida"), p.HeightStep, "m"],
            ],
            [], [Tr.T("Kerby na forma do memorial: tc = 37·(N·L/S)^0,47, L em km, S em %. Kirpich: tc = 57·(L³/ΔH)^0,385. tc adotado = o menor dos dois, nunca abaixo do mínimo.")]));

        tabelas.Add(new CableTable(Tr.T("Bacias"),
            [Tr.T("Canaleta"), "A (m²)", "L (m)", "ΔH (m)", "S (%)", "tc Kerby (min)", "tc Kirpich (min)", Tr.T("tc adotado (min)"), "V (m/s)"],
            cs.Select(c => (IReadOnlyList<object?>)[c.Input.Name, N(c.Input.Basin?.Area ?? double.NaN, 0), N(c.Input.Basin?.Length ?? double.NaN, 2), N(c.Input.Basin?.Drop ?? double.NaN, 2),
                N(c.Input.Basin?.SlopePercent ?? double.NaN, 2), N(c.KerbyTc, 2), N(c.KirpichTc, 2), N(c.Tc, 2), N(c.SurfaceVelocity, 2)]).ToList(),
            [], [Tr.F("Velocidade máxima da cobertura ({0}): {1:0.00} m/s.", p.Cover, p.CoverMaxVelocity)]));

        var chuva = ReturnPeriods.Select(t => (IReadOnlyList<object?>)new List<object?> { t }.Concat(Durations.Select(d => (object?)Math.Round(Rainfall.Intensity(p.K, p.A, p.B, p.C, t, d), 2))).ToList()).ToList();
        tabelas.Add(new CableTable(Tr.T("Chuva"),
            [Tr.T("T (anos)"), "5 min", "10 min", "15 min", "30 min", "1 h", "2 h", "4 h", "8 h", "12 h", "24 h"],
            chuva, [], [Tr.F("Intensidade pluviométrica em mm/h ({0}).", p.Source)]));

        tabelas.Add(new CableTable(Tr.T("Vazão"),
            [Tr.T("Canaleta"), "A (m²)", "C", "I (mm/h)", "Q (l/s)", "Q (m³/s)"],
            cs.Select(c => (IReadOnlyList<object?>)[c.Input.Name, N(c.Input.Basin?.Area ?? double.NaN, 0), p.RunoffC, N(c.Intensity, 2), N(c.FlowLitersPerSecond, 2), N(c.Flow, 4)]).ToList(),
            [Tr.T("Total"), N(cs.Sum(c => c.Input.Basin?.Area ?? 0), 0), null, null, N(cs.Sum(c => c.FlowLitersPerSecond), 2), N(cs.Sum(c => c.Flow), 4)],
            [Tr.T("Método racional: Q = C·I·A/3600 (l/s), válido até 50 ha.")]));

        tabelas.Add(new CableTable(Tr.T("Canaletas"),
            [Tr.T("Canaleta"), Tr.T("Seção"), "b / r (m)", "z", "L (m)", "Imin (m/m)", "Imax (m/m)", "η", "Q (m³/s)", "Vmax (m/s)", "Vmin (m/s)", "hmax (m)", "f (m)"],
            cs.Select(c => (IReadOnlyList<object?>)[c.Input.Name, c.Input.Section.Name,
                c.Input.Section.Kind == ChannelKind.HalfPipe ? c.Input.Section.Radius : c.Input.Section.Base,
                c.Input.Section.Kind == ChannelKind.HalfPipe ? null : c.Input.Section.SideSlope,
                N(c.Input.Profile.Length, 2), N(c.MinSlope, 4), N(c.MaxSlope, 4), c.Input.Section.Manning, N(c.Flow, 4),
                N(c.MaxVelocity, 2), N(c.MinVelocity, 2), N(c.MaxDepth, 3), c.Freeboard]).ToList(),
            [], [Tr.T("Manning: v = Rh^(2/3)·I^(1/2)/η; Q = v·A. hmax na declividade mínima, Vmax na máxima. Bordo livre f pela Tabela 36 do DNIT.")]));

        tabelas.Add(new CableTable(Tr.T("Dimensões"),
            [Tr.T("Canaleta"), Tr.T("Tipo"), "b (m)", "z", Tr.T("y / r adotado (m)"), Tr.T("y / r sugerido (m)"), "x (m)", "B (m)", "e (m)"],
            cs.Select(c =>
            {
                var s = c.Input.Section;
                var meia = s.Kind == ChannelKind.HalfPipe;
                return (IReadOnlyList<object?>)[c.Input.Name, meia ? Tr.T("meia-cana") : Tr.T("trapezoidal"), meia ? null : s.Base, meia ? null : s.SideSlope,
                    s.UsefulHeight, N(c.SuggestedHeight, 2), meia ? null : Math.Round(s.SideOpening, 3), Math.Round(s.TopWidth, 3), s.Thickness];
            }).ToList(),
            [], [Tr.F("Altura sugerida = lâmina + bordo livre, arredondada para cima de {0:0.00} em {0:0.00} m. A adotada é a da seção da canaleta: quem decide é o projetista.", p.HeightStep)]));

        tabelas.Add(new CableTable(Tr.T("Dissipadores"),
            [Tr.T("Canaleta"), "L1 (m)", "L2 (m)", Tr.T("Comprimento (m)"), Tr.T("Modelo DNIT")],
            cs.Select(c => (IReadOnlyList<object?>)[c.Input.Name, Math.Round(c.Dissipator.L1, 3), Math.Round(c.Dissipator.L2, 3), Math.Round(c.Dissipator.Length, 3), c.DissipatorModel ?? "—"]).ToList(),
            [], [Tr.T("DNIT IPR 736, desenho 1.19 (DES): L1 = abertura da canaleta, L2 = 3·L1, comprimento = 2·L1.")]));

        tabelas.Add(new CableTable(Tr.T("Transposições"),
            [Tr.T("Canaleta"), Tr.T("Cruzamentos com via"), "B (m)", Tr.T("Modelo DNIT")],
            cs.Where(c => c.Input.RoadCrossings > 0).Select(c => (IReadOnlyList<object?>)[c.Input.Name, c.Input.RoadCrossings, Math.Round(c.Input.Section.TopWidth, 3), c.CrossingModel ?? "—"]).ToList(),
            [], [string.IsNullOrWhiteSpace(p.RoadLayers)
                ? Tr.T("Nenhuma camada de via informada nos parâmetros: transposição não procurada.")
                : Tr.F("DNIT IPR 736, desenho 1.9 (TSS): laje maior que a abertura mais {0:0.00} m. Vias nas camadas: {1}.", p.CrossingMargin, p.RoadLayers)]));

        tabelas.Add(new CableTable(Tr.T("Quantitativos"),
            [Tr.T("Canaleta"), Tr.T("Comprimento em planta (m)"), Tr.T("Comprimento no fundo (m)"), Tr.T("Concreto (m³)"), Tr.T("Escavação (m³)"), Tr.T("Aprofundamento (m³)")],
            cs.Select(c => (IReadOnlyList<object?>)[c.Input.Name, N(c.Quantities.Length2D, 2), N(c.Quantities.Length3D, 2), N(c.Quantities.Concrete, 3), N(c.Quantities.Excavation, 3), N(c.Quantities.ExtraExcavation, 3)]).ToList(),
            [Tr.T("Total"), N(cs.Sum(c => c.Quantities.Length2D), 2), N(cs.Sum(c => c.Quantities.Length3D), 2), N(cs.Sum(c => c.Quantities.Concrete), 3), N(cs.Sum(c => c.Quantities.Excavation), 3), N(cs.Sum(c => c.Quantities.ExtraExcavation), 3)],
            [Tr.T("Seção vertical: volume = área da seção × comprimento em planta.")]));

        var alertas = memoria.Alerts.ToList();
        tabelas.Add(new CableTable(Tr.T("Alertas"),
            [Tr.T("Alerta")],
            alertas.Select(a => (IReadOnlyList<object?>)[a]).ToList(),
            [], [alertas.Count == 0 ? Tr.T("Nada fora da regra.") : Tr.T("O sistema calcula e avisa; quem aprova a canaleta é o projetista.")]));

        return tabelas;
    }
}
