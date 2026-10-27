using System.IO;
using System.Windows.Threading;
using Clivus.Core;
using Microsoft.Win32;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A porta de entrada do TermoVision (etapa 33.5). Ao carregar, o plugin:
///
/// 1. grava o protocolo <c>clivus:</c> no HKCU, apontando para o
///    <c>Clivus.Link.exe</c> da pasta do bundle (só se faltar ou apontar para
///    outro lugar);
/// 2. vigia a pasta de entrada (<see cref="TermoVisionInbox"/>), onde o Link
///    deixa o endereço de cada clique em "Exportar área para o Clivus";
/// 3. na folga do AutoCAD, sem comando rodando, pega o endereço e chama o
///    comando Abrir do TermoVision com ele.
///
/// O que chegou com o AutoCAD fechado é pego no carregamento seguinte.
/// </summary>
internal static class EntradaDoTermoVision
{
    private const string ChaveDoProtocolo = @"Software\Classes\clivus";

    private static FileSystemWatcher? _vigia;
    private static Dispatcher? _principal;
    private static bool _folgaAgendada;

    /// <summary>O endereço que o comando Abrir pega em vez de perguntar.</summary>
    internal static TermoVisionLink? Pendente { get; private set; }

    internal static void Instalar()
    {
        _principal = Dispatcher.CurrentDispatcher;

        try
        {
            RegistrarProtocolo();
        }
        catch (Exception erro) when (erro is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui gravar o protocolo clivus: no registro.", erro);
        }

        try
        {
            var pasta = TermoVisionInbox.DefaultFolder;
            Directory.CreateDirectory(pasta);
            _vigia = new FileSystemWatcher(pasta) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName };
            _vigia.Created += (_, _) => Agendar();
            _vigia.Renamed += (_, _) => Agendar();
            // Buffer estourado: os nomes se perdem, mas os arquivos estão lá.
            _vigia.Error += (_, _) => Agendar();
            _vigia.EnableRaisingEvents = true;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or ArgumentException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui vigiar a pasta de entrada do TermoVision.", erro);
        }

        // O que chegou com o AutoCAD fechado.
        Agendar();
    }

    internal static void Desinstalar()
    {
        _vigia?.Dispose();
        _vigia = null;
        if (_folgaAgendada) AcadApp.Idle -= NaFolga;
        _folgaAgendada = false;
    }

    /// <summary>
    /// <c>HKCU\Software\Classes\clivus</c> com <c>URL Protocol</c> e o comando
    /// <c>"…\Clivus.Link.exe" "%1"</c>. O endereço chega como argumento único,
    /// entre aspas; o Link não passa nada para shell.
    /// </summary>
    private static void RegistrarProtocolo()
    {
        var pasta = Path.GetDirectoryName(typeof(EntradaDoTermoVision).Assembly.Location);
        if (pasta is null) return;

        var exe = Path.Combine(pasta, "Clivus.Link.exe");
        if (!File.Exists(exe))
        {
            RegistroDeDiagnostico.Registrar($"Clivus.Link.exe não está em {pasta}: o endereço clivus:// não vai abrir o Clivus.");
            return;
        }

        var comando = $"\"{exe}\" \"%1\"";
        using (var atual = Registry.CurrentUser.OpenSubKey(ChaveDoProtocolo + @"\shell\open\command"))
            if (atual?.GetValue(null) as string == comando) return;

        using var raiz = Registry.CurrentUser.CreateSubKey(ChaveDoProtocolo);
        raiz.SetValue(null, "URL:Clivus Solar");
        raiz.SetValue("URL Protocol", "");
        using (var icone = raiz.CreateSubKey("DefaultIcon")) icone.SetValue(null, $"\"{exe}\",0");
        using (var abrir = raiz.CreateSubKey(@"shell\open\command")) abrir.SetValue(null, comando);

        RegistroDeDiagnostico.Registrar($"Protocolo clivus: gravado para {exe}.");
    }

    // O vigia avisa de outra thread: a folga é agendada na thread do AutoCAD.
    private static void Agendar()
    {
        var principal = _principal;
        if (principal is null) return;

        principal.BeginInvoke(() =>
        {
            if (_folgaAgendada) return;
            AcadApp.Idle += NaFolga;
            _folgaAgendada = true;
        });
    }

    private static void NaFolga(object? sender, EventArgs e)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;

        // Sem desenho aberto ou com comando rodando: espera a próxima folga.
        if (documento is null || !string.IsNullOrEmpty(documento.CommandInProgress)) return;

        AcadApp.Idle -= NaFolga;
        _folgaAgendada = false;

        List<TermoVisionLink> chegaram;
        try
        {
            chegaram = new TermoVisionInbox(TermoVisionInbox.DefaultFolder).Take(DateTime.UtcNow, out var descartados);
            if (descartados > 0)
                RegistroDeDiagnostico.Registrar($"Pasta de entrada do TermoVision: {descartados} arquivo(s) vencido(s) ou inválido(s) descartado(s).");
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a pasta de entrada do TermoVision.", erro);
            return;
        }

        // Vários cliques seguidos: vale o último (os outros são a mesma pessoa mudando de ideia).
        if (chegaram.Count == 0) return;
        Pendente = chegaram[^1];

        try
        {
            // O AutoCAD vem para a frente: a pessoa estava no navegador.
            if (AcadApp.MainWindow.WindowState == Autodesk.AutoCAD.Windows.Window.State.Minimized)
                AcadApp.MainWindow.WindowState = Autodesk.AutoCAD.Windows.Window.State.Normal;
            AcadApp.MainWindow.Focus();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui trazer o AutoCAD para a frente.", erro);
        }

        documento.SendStringToExecute($"\x03\x03_{PluginInfo.ComandoTermoVisionAbrir} ", true, false, false);
    }

    /// <summary>O comando pega o endereço pendente uma vez só.</summary>
    internal static TermoVisionLink? TomarPendente()
    {
        var link = Pendente;
        Pendente = null;
        return link;
    }
}
