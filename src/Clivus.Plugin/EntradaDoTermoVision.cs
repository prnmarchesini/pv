using System.IO;
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
/// 2. fica de olho na pasta de entrada (<see cref="TermoVisionInbox"/>), onde o
///    Link deixa o endereço de cada clique em "Abrir no AutoCAD";
/// 3. na folga do AutoCAD, sem comando rodando, pega o endereço e chama o
///    comando Abrir do TermoVision com ele.
///
/// A folga fica sempre assinada (assinada na thread do AutoCAD, no carregamento)
/// e olha a pasta quando o vigia avisou ou a cada poucos segundos. Não há
/// Dispatcher no meio: a primeira versão agendava a folga pelo Dispatcher a
/// partir da thread do vigia, e no AutoCAD o agendamento nunca rodava (teste
/// do Renan, 26/10/2026: o arquivo ficou na pasta). O Link traz o AutoCAD para
/// a frente, o que gera folga na hora.
/// </summary>
internal static class EntradaDoTermoVision
{
    private const string ChaveDoProtocolo = @"Software\Classes\clivus";

    /// <summary>De quanto em quanto tempo a folga olha a pasta mesmo sem aviso do vigia.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    private static FileSystemWatcher? _vigia;
    private static volatile bool _chegou;
    private static DateTime _ultimaOlhada = DateTime.MinValue;
    private static bool _assinada;

    /// <summary>O endereço que o comando Abrir pega em vez de perguntar.</summary>
    internal static TermoVisionLink? Pendente { get; private set; }

    internal static void Instalar()
    {
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
            // O vigia avisa de outra thread: só levanta a bandeira; quem age é a folga.
            _vigia.Created += (_, _) => _chegou = true;
            _vigia.Renamed += (_, _) => _chegou = true;
            _vigia.Error += (_, _) => _chegou = true;
            _vigia.EnableRaisingEvents = true;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or ArgumentException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui vigiar a pasta de entrada do TermoVision.", erro);
        }

        // O que chegou com o AutoCAD fechado entra na primeira folga.
        _chegou = true;
        AcadApp.Idle += NaFolga;
        _assinada = true;
        RegistroDeDiagnostico.Registrar($"Entrada do TermoVision ligada em {TermoVisionInbox.DefaultFolder}.");
    }

    internal static void Desinstalar()
    {
        _vigia?.Dispose();
        _vigia = null;
        if (_assinada) AcadApp.Idle -= NaFolga;
        _assinada = false;
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

    private static void NaFolga(object? sender, EventArgs e)
    {
        // Barato na maioria das folgas: sem aviso e dentro do intervalo, nada.
        var agora = DateTime.UtcNow;
        if (!_chegou && agora - _ultimaOlhada < Intervalo) return;

        try
        {
            var documento = AcadApp.DocumentManager.MdiActiveDocument;

            // Sem desenho aberto ou com comando rodando: a bandeira fica para a próxima folga.
            if (documento is null || !string.IsNullOrEmpty(documento.CommandInProgress)) return;

            _ultimaOlhada = agora;
            _chegou = false;

            var link = Tomar();
            if (link is null) return;

            Pendente = link;
            RegistroDeDiagnostico.Registrar($"TermoVision: chegou {link}; chamando o Abrir.");

            try
            {
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
        catch (Exception erro)
        {
            // Uma folga que lança derruba as seguintes: registra e segue.
            RegistroDeDiagnostico.Registrar("Falha na folga da entrada do TermoVision.", erro);
        }
    }

    /// <summary>
    /// O último endereço da pasta de entrada (vários cliques seguidos: vale o
    /// último), ou nulo. Os lidos saem da pasta.
    /// </summary>
    internal static TermoVisionLink? Tomar()
    {
        var pasta = TermoVisionInbox.DefaultFolder;
        if (!Directory.Exists(pasta) || !Directory.EnumerateFiles(pasta, "*" + TermoVisionInbox.Extension).Any()) return null;

        try
        {
            var chegaram = new TermoVisionInbox(pasta).Take(DateTime.UtcNow, out var descartados);
            if (descartados > 0)
                RegistroDeDiagnostico.Registrar($"Pasta de entrada do TermoVision: {descartados} arquivo(s) vencido(s) ou inválido(s) descartado(s).");
            return chegaram.Count == 0 ? null : chegaram[^1];
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a pasta de entrada do TermoVision.", erro);
            return null;
        }
    }

    /// <summary>
    /// O endereço que o comando Abrir usa sem perguntar: o que a folga
    /// separou ou, sem ele, o que estiver na pasta de entrada (o botão Abrir
    /// apertado depois do clique no navegador).
    /// </summary>
    internal static TermoVisionLink? TomarPendente()
    {
        var link = Pendente ?? Tomar();
        Pendente = null;
        return link;
    }
}
