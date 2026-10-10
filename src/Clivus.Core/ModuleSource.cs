using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// A potência de módulo trocada pela área (Melhorias de 10/10/2026, item 14:
/// "Trocar potência do módulo"): uma simulação, que vale para a usina inteira
/// no kWp e nos resumos, sem mexer no tamanho de nada. Com ela, os cálculos
/// elétricos (tensões, correntes, quedas) ficam desligados, porque o PAN da
/// estrutura não é mais o módulo da conta: sobra só a soma das potências.
/// Gravada no desenho num registro versionado (<see cref="StorageKey"/>,
/// versão <see cref="Version"/>, um campo).
/// </summary>
/// <param name="Watts">A potência simulada de cada módulo, em Wp.</param>
public sealed record SimulatedModulePower(double Watts)
{
    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public const string StorageKey = "POTENCIA_SIMULADA";

    /// <summary>A versão do registro.</summary>
    public const int Version = 1;

    /// <summary>Campos por item.</summary>
    public const int FieldCount = 1;

    /// <summary>Por que a potência não serve (null se serve): a mesma faixa do módulo do cadastro, 1 a 2000 Wp.</summary>
    public string? WhyInvalid() =>
        double.IsFinite(Watts) && Watts >= 1 && Watts <= 2000
            ? null
            : Tr.T("a potência do módulo vai de 1 a 2000 Wp");

    public IReadOnlyList<string> ToFields() => [Watts.ToString("R", CultureInfo.InvariantCulture)];

    public static SimulatedModulePower? Parse(IReadOnlyList<string> campos)
    {
        if (campos.Count < FieldCount) return null;
        if (!double.TryParse(campos[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) return null;
        var s = new SimulatedModulePower(w);
        return s.WhyInvalid() is null ? s : null;
    }

    /// <summary>A frase do porquê os cálculos elétricos não aparecem.</summary>
    public string Reason() =>
        Tr.F("A potência do módulo foi trocada pela área para {0:0.#} Wp (simulação, para a usina inteira): o kWp e os resumos usam essa potência, e os cálculos elétricos (tensões, correntes e quedas) ficam desligados, porque o módulo da estrutura não é mais o da conta.", Watts);
}

/// <summary>Uma mesa desenhada, para a atualização da potência: GUID, de que estrutura é e a potência gravada.</summary>
public sealed record DrawnTablePower(Guid Table, string? ProfileName, double? Watts);

/// <summary>
/// A fonte única do módulo (Melhorias de 10/10/2026, item 15: "uma fonte
/// única, na estrutura"): os dados elétricos vêm da estrutura (perfil da
/// mesa do desenho, <see cref="TableProfile.ModuleElectrical"/>); o PAN
/// gravado na rota por versões anteriores fica como reserva, para desenho
/// antigo não perder dado; e a potência trocada pela área (item 14), quando
/// existe, desliga os dados elétricos e manda na potência.
/// <para>
/// Quem precisa dos dados elétricos de uma string (rota CC, Combiner, resumo)
/// chama <see cref="ElectricalFor"/> com o nome da estrutura da mesa dela.
/// </para>
/// </summary>
public sealed class ModuleSource
{
    /// <summary>Diferença de potência que conta como divergência, em W (o PAN grava com uma casa).</summary>
    public const double Tolerance = 0.5;

    private readonly IReadOnlyList<DrawingTable> _estruturas;
    private readonly IReadOnlyList<PanModule> _reserva;

    /// <param name="structures">As mesas (estruturas) cadastradas no desenho.</param>
    /// <param name="legacyPans">Os módulos lidos de PAN pela rota, antes do item 15 (reserva).</param>
    /// <param name="simulated">A potência trocada pela área, ou null.</param>
    public ModuleSource(IReadOnlyList<DrawingTable> structures, IReadOnlyList<PanModule> legacyPans, SimulatedModulePower? simulated)
    {
        _estruturas = structures ?? throw new ArgumentNullException(nameof(structures));
        _reserva = legacyPans ?? throw new ArgumentNullException(nameof(legacyPans));
        Simulated = simulated;
    }

    /// <summary>A potência trocada pela área, ou null.</summary>
    public SimulatedModulePower? Simulated { get; }

    /// <summary>Se os cálculos elétricos valem (não valem com a potência trocada pela área).</summary>
    public bool ElectricalEnabled => Simulated is null;

    /// <summary>Os PAN antigos, gravados na rota (reserva).</summary>
    public IReadOnlyList<PanModule> LegacyPans => _reserva;

    /// <summary>
    /// Os dados elétricos do módulo das mesas desta estrutura, ou null (sem
    /// dados, ou com a potência trocada pela área). Nesta ordem: a potência
    /// trocada pela área desliga tudo; a estrutura com os dados manda; mesa
    /// sem estrutura conhecida (de antes do 8.6) usa os dados da estrutura do
    /// desenho, se só uma os tem; e, por último, o PAN antigo da rota (o do
    /// mesmo modelo, ou o único).
    /// </summary>
    public PanModule? ElectricalFor(string? profileName)
    {
        if (Simulated is not null) return null;

        var estrutura = DrawingTables.Find(_estruturas, profileName);
        if (estrutura?.Profile.ModuleElectrical is { } daEstrutura) return daEstrutura;

        if (estrutura is null)
        {
            var distintos = _estruturas.Select(e => e.Profile.ModuleElectrical).OfType<PanModule>().Distinct().ToList();
            if (distintos.Count == 1) return distintos[0];
        }

        return Reserva(estrutura?.Profile);
    }

    /// <summary>
    /// As linhas que dizem de onde vem o módulo de cada estrutura, para a aba
    /// CC da rota (item 15: a rota mostra o módulo vindo da estrutura).
    /// </summary>
    public IReadOnlyList<string> Describe()
    {
        var linhas = new List<string>();

        if (Simulated is { } s)
            linhas.Add(Tr.F("Potência do módulo trocada pela área: {0:0.#} Wp. Os cálculos elétricos estão desligados.", s.Watts));

        foreach (var e in _estruturas)
        {
            if (e.Profile.ModuleElectrical is { } m)
                linhas.Add(Tr.F("Estrutura \"{0}\": {1}", e.Name, m.Describe()));
            else if (Reserva(e.Profile) is { } r)
                linhas.Add(Tr.F("Estrutura \"{0}\": sem dados elétricos na estrutura; vale o PAN antigo gravado na rota: {1}", e.Name, r.Describe()));
            else
                linhas.Add(Tr.F("Estrutura \"{0}\": sem dados elétricos. Carregue o .PAN na estrutura ou digite os valores lá (Estruturas...); sem eles, tensões, correntes e quedas ficam em branco.", e.Name));
        }

        if (_estruturas.Count == 0)
        {
            if (_reserva.Count == 0) linhas.Add(Tr.T("Nenhuma estrutura no desenho: tensões, correntes e quedas ficam em branco."));
            foreach (var r in _reserva) linhas.Add(Tr.F("PAN antigo gravado na rota: {0}", r.Describe()));
        }

        return linhas;
    }

    /// <summary>A potência que conta para o kWp de um módulo: a trocada pela área, ou a gravada na mesa.</summary>
    public double? PowerFor(double? tableWatts) => Simulated?.Watts ?? tableWatts;

    /// <summary>
    /// A potência para onde o "Atualizar a potência" leva as mesas desenhadas
    /// desta estrutura: a do PAN, quando ela o tem; senão a do módulo dela.
    /// </summary>
    public static double TargetPower(TableProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.ModuleElectrical?.Pmax ?? profile.Layout.Module.PowerWatts;
    }

    /// <summary>A divergência entre o PAN da estrutura e a potência do módulo dela, ou null.</summary>
    public static string? ProfileDivergence(TableProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.ModuleElectrical is { } e && Math.Abs(e.Pmax - profile.Layout.Module.PowerWatts) > Tolerance
            ? Tr.F("Estrutura \"{0}\": o PAN ({1}) é de {2:0.#} Wp e o módulo da estrutura é de {3:0.#} Wp.", profile.Name.Trim(), e.Model.Trim(), e.Pmax, profile.Layout.Module.PowerWatts)
            : null;
    }

    /// <summary>
    /// As divergências que o usuário precisa ver (item 15: "PAN de 700 Wp e a
    /// estrutura está com módulo de 720"): o PAN da estrutura contra a
    /// potência dela; o PAN antigo da rota contra a estrutura que o usa; e as
    /// mesas desenhadas com potência diferente da que a estrutura diz.
    /// </summary>
    /// <param name="drawn">As mesas desenhadas (null: não confere as desenhadas).</param>
    public IReadOnlyList<string> Divergences(IReadOnlyList<DrawnTablePower>? drawn = null)
    {
        var avisos = new List<string>();

        foreach (var estrutura in _estruturas)
        {
            var perfil = estrutura.Profile;

            if (ProfileDivergence(perfil) is { } doPan) avisos.Add(doPan);
            else if (perfil.ModuleElectrical is null && Reserva(perfil) is { } antigo && Math.Abs(antigo.Pmax - perfil.Layout.Module.PowerWatts) > Tolerance)
                avisos.Add(Tr.F("Estrutura \"{0}\": o PAN gravado na rota ({1}) é de {2:0.#} Wp e o módulo da estrutura é de {3:0.#} Wp. Carregue o PAN na estrutura (Configurações > Estruturas > Editar).",
                    estrutura.Name, antigo.Model.Trim(), antigo.Pmax, perfil.Layout.Module.PowerWatts));

            if (drawn is null) continue;

            var alvo = TargetPower(perfil);
            var diferentes = Dela(drawn, estrutura)
                .Where(m => m.Watts is { } w && Math.Abs(w - alvo) > Tolerance)
                .GroupBy(m => Math.Round(m.Watts!.Value, 1))
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var g in diferentes)
                avisos.Add(Tr.F("Estrutura \"{0}\": {1} mesa(s) desenhada(s) com módulo de {2:0.#} Wp, e a estrutura diz {3:0.#} Wp. Use \"Atualizar a potência das mesas desenhadas\".",
                    estrutura.Name, g.Count(), g.Key, alvo));
        }

        return avisos;
    }

    /// <summary>
    /// As mesas desenhadas desta estrutura que mudam de potência no "Atualizar
    /// a potência", com a potência nova: as que têm o nome da estrutura; e,
    /// num desenho com uma estrutura só, também as sem nome (de antes do 8.6).
    /// Nada além da potência muda.
    /// </summary>
    public static IReadOnlyList<(Guid Table, double Watts)> PowerUpdate(IReadOnlyList<DrawingTable> structures, DrawingTable structure, IReadOnlyList<DrawnTablePower> drawn)
    {
        ArgumentNullException.ThrowIfNull(structures);
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(drawn);

        var alvo = TargetPower(structure.Profile);
        var semNome = structures.Count <= 1;

        return drawn
            .Where(m => Mesmo(m.ProfileName, structure.Name) || (semNome && string.IsNullOrWhiteSpace(m.ProfileName)))
            .Where(m => m.Watts is not { } w || Math.Abs(w - alvo) > 1e-9)
            .Select(m => (m.Table, alvo))
            .ToList();
    }

    private IEnumerable<DrawnTablePower> Dela(IReadOnlyList<DrawnTablePower> drawn, DrawingTable estrutura) =>
        drawn.Where(m => Mesmo(m.ProfileName, estrutura.Name) || (_estruturas.Count == 1 && string.IsNullOrWhiteSpace(m.ProfileName)));

    private static bool Mesmo(string? a, string b) =>
        a is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase);

    /// <summary>O PAN antigo da rota para esta estrutura: o do mesmo modelo, ou o único.</summary>
    private PanModule? Reserva(TableProfile? perfil)
    {
        var modelo = perfil?.Layout.Module.Model;
        return _reserva.FirstOrDefault(p => modelo is not null && string.Equals(p.Model.Trim(), modelo.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? (_reserva.Count == 1 ? _reserva[0] : null);
    }
}
