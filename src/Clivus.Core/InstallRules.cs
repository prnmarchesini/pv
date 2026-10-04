using System.Globalization;

namespace Clivus.Core;

/// <summary>Uma instalação de AutoCAD achada no registro: a série (R25.1) e se tem Civil 3D.</summary>
public sealed record AutoCadInstall(string Series, string Path, bool HasCivil3D);

/// <summary>O que o instalador concluiu sobre a máquina.</summary>
public enum InstallCheck
{
    /// <summary>Há Civil 3D na série suportada.</summary>
    Ok,

    /// <summary>Nenhum AutoCAD.</summary>
    NoAutoCad,

    /// <summary>AutoCAD de outra série.</summary>
    WrongSeries,

    /// <summary>A série serve, mas é AutoCAD sem Civil 3D.</summary>
    NoCivil3D,

    /// <summary>Série num formato que não se sabe ler.</summary>
    UnknownSeries,
}

/// <summary>
/// A regra do instalador (a mesma de tools/instalar.ps1, passo 0.6): o
/// Civil 3D da série entre SeriesMin e SeriesMax do PackageContents.xml.
/// Os códigos de saída são os do instalar.ps1.
/// </summary>
public static class InstallRules
{
    /// <summary>"R25.1" vira 25.1; null fora do formato.</summary>
    public static decimal? ParseSeries(string? series)
    {
        if (string.IsNullOrWhiteSpace(series)) return null;

        var m = System.Text.RegularExpressions.Regex.Match(series.Trim(), @"^[Rr](\d+)(?:\.(\d+))?$");
        if (!m.Success) return null;

        return decimal.Parse(m.Groups[1].Value + "." + (m.Groups[2].Success ? m.Groups[2].Value : "0"), CultureInfo.InvariantCulture);
    }

    /// <summary>O que vale para estas instalações, o caminho escolhido (quando Ok) e a frase para o usuário.</summary>
    public static (InstallCheck Result, AutoCadInstall? Chosen, string Message) Check(IReadOnlyList<AutoCadInstall> installs, string seriesMin, string seriesMax)
    {
        ArgumentNullException.ThrowIfNull(installs);

        var min = ParseSeries(seriesMin) ?? throw new ArgumentException("SeriesMin fora do formato", nameof(seriesMin));
        var max = ParseSeries(seriesMax) ?? throw new ArgumentException("SeriesMax fora do formato", nameof(seriesMax));
        var suportada = seriesMin == seriesMax ? seriesMin : Tr.F("{0} a {1}", seriesMin, seriesMax);

        if (installs.Count == 0)
            return (InstallCheck.NoAutoCad, null, Tr.F("Nenhum AutoCAD encontrado neste computador. O Clivus Solar precisa do Civil 3D 2026 (série {0}).", suportada));

        var legiveis = installs.Where(i => ParseSeries(i.Series) is not null).ToList();
        if (legiveis.Count == 0)
            return (InstallCheck.UnknownSeries, null, Tr.F("Não consegui ler a versão do AutoCAD instalado ({0}).", string.Join(", ", installs.Select(i => i.Series))));

        var naSerie = legiveis.Where(i => ParseSeries(i.Series) is { } v && v >= min && v <= max).ToList();
        if (naSerie.Count == 0)
            return (InstallCheck.WrongSeries, null,
                Tr.F("Este computador tem {0}; o Clivus Solar é para o Civil 3D 2026 (série {1}).", string.Join(", ", legiveis.Select(i => i.Series).Distinct()), suportada));

        var comCivil = naSerie.FirstOrDefault(i => i.HasCivil3D);
        if (comCivil is null)
            return (InstallCheck.NoCivil3D, null, Tr.T("O AutoCAD 2026 está instalado, mas sem o Civil 3D. O Clivus Solar lê a superfície do terreno, que só existe no Civil 3D."));

        return (InstallCheck.Ok, comCivil, Tr.F("Civil 3D 2026 encontrado ({0}).", comCivil.Path));
    }

    /// <summary>O código de saída do instalador (o mesmo do instalar.ps1).</summary>
    public static int ExitCode(InstallCheck result) => result switch
    {
        InstallCheck.Ok => 0,
        InstallCheck.WrongSeries => 2,
        InstallCheck.NoCivil3D => 3,
        InstallCheck.NoAutoCad => 4,
        _ => 6,
    };
}
