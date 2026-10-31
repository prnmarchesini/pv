namespace Clivus.Core;

/// <summary>
/// A linha de comando do ClivusSolar-Setup.exe. Sem argumentos, a janela.
/// <c>/desinstalar</c>; <c>/silencioso</c> (sem janela, o resultado no
/// <c>/log=arquivo</c> e no código de saída); <c>/teste=pasta</c> (tudo dentro
/// da pasta, sem tocar a instalação de verdade); <c>/ignorar-civil3d</c> (só
/// com /teste); <c>/idioma=pt|en|es</c> (por cima do idioma do Windows).
/// Desde a etapa 43, <c>/aguardar-civil3d</c>: espera o Civil 3D fechar e
/// instala como no silencioso, avisando no fim (é como a atualização do
/// plugin roda o instalador baixado). Só vale para instalar.
/// </summary>
public sealed record InstallerOptions(
    bool Uninstall, bool Silent, bool WaitForCivil3D, string? TestFolder, bool IgnoreCivil3D, string? Language, string? LogFile)
{
    /// <summary>Lê os argumentos, sem distinguir maiúsculas; o que não conhece fica de fora.</summary>
    public static InstallerOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? Valor(string nome) => args.FirstOrDefault(a => a.StartsWith(nome + "=", StringComparison.OrdinalIgnoreCase))?[(nome.Length + 1)..].Trim('"');
        bool Tem(string nome) => args.Any(a => string.Equals(a, nome, StringComparison.OrdinalIgnoreCase));

        var teste = Valor("/teste");
        var desinstalar = Tem("/desinstalar");
        var aguardar = !desinstalar && Tem(PluginUpdate.WaitForCivil3DOption);

        return new InstallerOptions(
            Uninstall: desinstalar,
            Silent: Tem("/silencioso") || aguardar,
            WaitForCivil3D: aguardar,
            TestFolder: teste,
            IgnoreCivil3D: teste is not null && Tem("/ignorar-civil3d"),
            Language: Valor("/idioma"),
            LogFile: Valor("/log"));
    }
}
