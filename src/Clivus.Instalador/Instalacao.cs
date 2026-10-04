using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Clivus.Core;
using Microsoft.Win32;

namespace Clivus.Instalador;

/// <summary>
/// O motor do instalador, sem janela (a janela e o modo silencioso usam o
/// mesmo). Instala para o usuário:
/// - o bundle em %APPDATA%\Autodesk\ApplicationPlugins\ClivusSolar.bundle
///   (o Civil 3D carrega sozinho ao abrir);
/// - o desinstalador em %LOCALAPPDATA%\Clivus Solar\desinstalar.exe;
/// - a entrada em "Adicionar ou remover programas" (HKCU, sem administrador).
/// Tira o bundle do nome antigo (UFV.bundle). Desinstalar tira o bundle e a
/// entrada; a licença, os perfis de mesa e o registro de diagnóstico do
/// usuário ficam.
/// </summary>
internal sealed class Instalacao
{
    private const string ChaveDeDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    /// <summary>A pasta dos plugins do AutoCAD do usuário.</summary>
    internal string PastaDosPlugins { get; }

    /// <summary>A pasta de dados do Clivus Solar do usuário.</summary>
    internal string PastaDeDados { get; }

    /// <summary>O nome da entrada em "Adicionar ou remover programas".</summary>
    internal string NomeDaEntrada { get; }

    internal string PastaDoBundle => Path.Combine(PastaDosPlugins, "ClivusSolar.bundle");

    internal string Desinstalador => Path.Combine(PastaDeDados, "desinstalar.exe");

    /// <param name="pastaDeTeste">
    /// Para os testes: tudo vai para dentro desta pasta (plugins e dados) e a
    /// entrada do registro ganha "_Teste", sem tocar a instalação de verdade.
    /// </param>
    internal Instalacao(string? pastaDeTeste = null)
    {
        if (pastaDeTeste is null)
        {
            PastaDosPlugins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins");
            PastaDeDados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario);
            NomeDaEntrada = "ClivusSolar";
        }
        else
        {
            PastaDosPlugins = Path.Combine(pastaDeTeste, "ApplicationPlugins");
            PastaDeDados = Path.Combine(pastaDeTeste, "dados");
            NomeDaEntrada = "ClivusSolar_Teste";
        }
    }

    /// <summary>A versão do plugin que vai dentro deste instalador.</summary>
    internal static string Versao =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    /// <summary>Os AutoCAD instalados nesta máquina (registro, visão de 64 bits).</summary>
    internal static List<AutoCadInstall> AutoCads()
    {
        var achados = new List<AutoCadInstall>();

        using var raiz = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD");
        if (raiz is null) return achados;

        foreach (var serie in raiz.GetSubKeyNames())
        {
            using var chaveDaSerie = raiz.OpenSubKey(serie);
            if (chaveDaSerie is null) continue;

            foreach (var produto in chaveDaSerie.GetSubKeyNames())
            {
                using var chave = chaveDaSerie.OpenSubKey(produto);
                if (chave?.GetValue("AcadLocation") is not string caminho || !Directory.Exists(caminho)) continue;

                achados.Add(new AutoCadInstall(serie, caminho, File.Exists(Path.Combine(caminho, "C3D", "AeccDbMgd.dll"))));
            }
        }

        return achados.DistinctBy(a => (a.Series, a.Path)).ToList();
    }

    /// <summary>O pacote embutido (bundle.zip), ou null num build sem ele.</summary>
    private static Stream? Pacote() => Assembly.GetExecutingAssembly().GetManifestResourceStream("bundle.zip");

    /// <summary>A série suportada, lida do PackageContents.xml de dentro do pacote.</summary>
    internal static (string Min, string Max) SerieSuportada()
    {
        using var pacote = Pacote() ?? throw new InvalidOperationException("este instalador foi gerado sem o plugin dentro");
        using var zip = new ZipArchive(pacote, ZipArchiveMode.Read);
        var entrada = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("PackageContents.xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("o pacote não tem PackageContents.xml");

        using var leitor = entrada.Open();
        var requisitos = XDocument.Load(leitor).Descendants("RuntimeRequirements").First();
        return ((string)requisitos.Attribute("SeriesMin")!, (string)requisitos.Attribute("SeriesMax")!);
    }

    /// <summary>Se o AutoCAD (ou o Core Console) está aberto e segura os arquivos.</summary>
    internal static bool AutoCadAberto() =>
        Process.GetProcessesByName("acad").Length + Process.GetProcessesByName("accoreconsole").Length > 0;

    /// <summary>Instala. A frase do resultado.</summary>
    internal string Instalar(Action<string>? progresso = null)
    {
        progresso?.Invoke("Tirando versões anteriores...");

        // O bundle do nome antigo e a versão anterior deste.
        foreach (var velho in new[] { Path.Combine(PastaDosPlugins, "UFV.bundle"), PastaDoBundle })
            if (Directory.Exists(velho)) Directory.Delete(velho, recursive: true);

        progresso?.Invoke("Copiando o Clivus Solar...");

        Directory.CreateDirectory(PastaDosPlugins);
        using (var pacote = Pacote() ?? throw new InvalidOperationException("este instalador foi gerado sem o plugin dentro"))
        using (var zip = new ZipArchive(pacote, ZipArchiveMode.Read))
        {
            // O zip tem a pasta ClivusSolar.bundle na raiz; cada entrada é
            // conferida para não escapar da pasta de destino.
            var destino = Path.GetFullPath(PastaDosPlugins) + Path.DirectorySeparatorChar;

            foreach (var entrada in zip.Entries)
            {
                var caminho = Path.GetFullPath(Path.Combine(PastaDosPlugins, entrada.FullName));
                if (!caminho.StartsWith(destino, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"entrada fora da pasta no pacote: {entrada.FullName}");

                if (entrada.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(caminho);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
                entrada.ExtractToFile(caminho, overwrite: true);
            }
        }

        progresso?.Invoke("Registrando em Adicionar ou remover programas...");

        Directory.CreateDirectory(PastaDeDados);
        var esteExe = Environment.ProcessPath ?? throw new InvalidOperationException("não sei onde está o instalador");
        if (!string.Equals(Path.GetFullPath(esteExe), Path.GetFullPath(Desinstalador), StringComparison.OrdinalIgnoreCase))
            File.Copy(esteExe, Desinstalador, overwrite: true);

        var tamanhoKb = new DirectoryInfo(PastaDoBundle).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;

        using (var chave = Registry.CurrentUser.CreateSubKey(ChaveDeDesinstalar + NomeDaEntrada))
        {
            chave.SetValue("DisplayName", "Clivus Solar");
            chave.SetValue("DisplayVersion", Versao);
            chave.SetValue("Publisher", "Clivus Solar");
            chave.SetValue("DisplayIcon", Path.Combine(PastaDoBundle, "Contents", "Resources", "clivus.ico"));
            chave.SetValue("InstallLocation", PastaDoBundle);
            chave.SetValue("UninstallString", $"\"{Desinstalador}\" /desinstalar");
            chave.SetValue("QuietUninstallString", $"\"{Desinstalador}\" /desinstalar /silencioso");
            chave.SetValue("EstimatedSize", (int)tamanhoKb, RegistryValueKind.DWord);
            chave.SetValue("NoModify", 1, RegistryValueKind.DWord);
            chave.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        return $"Clivus Solar {Versao} instalado. Abra o Civil 3D: a aba Clivus Solar aparece sozinha.";
    }

    /// <summary>Desinstala: o bundle e a entrada saem; os dados do usuário ficam. A frase do resultado.</summary>
    internal string Desinstalar()
    {
        if (Directory.Exists(PastaDoBundle)) Directory.Delete(PastaDoBundle, recursive: true);
        Registry.CurrentUser.DeleteSubKeyTree(ChaveDeDesinstalar + NomeDaEntrada, throwOnMissingSubKey: false);

        // O desinstalador não apaga a si mesmo enquanto roda: um cmd apaga
        // depois que ele sai.
        if (File.Exists(Desinstalador)
            && Environment.ProcessPath is { } esteExe
            && string.Equals(Path.GetFullPath(esteExe), Path.GetFullPath(Desinstalador), StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & del /q \"{Desinstalador}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        else if (File.Exists(Desinstalador))
        {
            File.Delete(Desinstalador);
        }

        return "Clivus Solar desinstalado. Sua licença e seus perfis de mesa ficaram na pasta do usuário, para uma próxima instalação.";
    }

    /// <summary>Se está instalado (a entrada existe).</summary>
    internal bool Instalado()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(ChaveDeDesinstalar + NomeDaEntrada);
        return chave is not null;
    }
}
