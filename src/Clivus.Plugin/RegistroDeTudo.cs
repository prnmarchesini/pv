using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// O registro de tudo (Renan, 30/10/2026: "quero também que você gere logs
/// de tudo, porque apareceu umas msg de erro, e aí, tendo log, você poderá
/// analisar"). Só no Civil 3D com interface; o Core Console dos testes não
/// escreve nada aqui. Três fontes, todas na pasta do plugin
/// (%LOCALAPPDATA%\Clivus Solar):
///
/// - a linha de comando inteira, do jeito que o usuário viu (as mensagens do
///   plugin, os avisos e os erros do próprio AutoCAD): é o log do AutoCAD
///   (LOGFILEMODE), ligado aqui com a pasta linha-de-comando, se o usuário
///   não tinha ligado o dele. Arquivo com mais de 30 dias sai;
/// - cada comando no clivus.log: quando começou, em que desenho, quanto
///   durou e como acabou (terminou, cancelado, falhou), e os desenhos
///   abertos e fechados. Um comando que começou e não terminou é onde o
///   AutoCAD travou ou caiu;
/// - o erro que escapa de tudo (de um botão de janela, de uma tarefa), no
///   clivus.log com a pilha.
/// </summary>
internal static class RegistroDeTudo
{
    /// <summary>A pasta do log da linha de comando, dentro da do plugin.</summary>
    internal static string PastaDaLinhaDeComando => Path.Combine(RegistroDeDiagnostico.Pasta, "linha-de-comando");

    /// <summary>Quantos dias o log da linha de comando fica.</summary>
    private const int DiasGuardados = 30;

    private static readonly Dictionary<Document, Stack<(string Nome, Stopwatch Relogio)>> EmAndamento = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;
    private static bool _ligado;

    internal static void Instalar()
    {
        if (_ligado) return;
        _ligado = true;

        RegistroDeDiagnostico.Registrar(
            $"Civil 3D {AcadApp.Version} ({Environment.ProcessPath}), Windows {Environment.OSVersion.Version}, cultura {System.Globalization.CultureInfo.CurrentCulture.Name}.");

        AppDomain.CurrentDomain.UnhandledException += AoEscaparErro;
        TaskScheduler.UnobservedTaskException += AoEscaparTarefa;
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += AoEscaparDaJanela;

        var documentos = AcadApp.DocumentManager;
        _aoCriar = (_, e) => Escutar(e.Document, Path.GetFileName(e.Document?.Name) is { Length: > 0 } nome ? $"desenho aberto: {nome}" : null);
        _aoDestruir = (_, e) => Soltar(e.Document);
        documentos.DocumentCreated += _aoCriar;
        documentos.DocumentToBeDestroyed += _aoDestruir;

        foreach (Document documento in documentos) Escutar(documento, $"desenho já aberto: {Path.GetFileName(documento.Name)}");

        // A variável do AutoCAD na primeira folga, com o AutoCAD de pé.
        AcadApp.Idle += LigarALinhaDeComandoNaFolga;
    }

    internal static void Desinstalar()
    {
        if (!_ligado) return;
        _ligado = false;

        try
        {
            AppDomain.CurrentDomain.UnhandledException -= AoEscaparErro;
            TaskScheduler.UnobservedTaskException -= AoEscaparTarefa;
            AcadApp.Idle -= LigarALinhaDeComandoNaFolga;

            foreach (var documento in EmAndamento.Keys.ToList()) Soltar(documento);

            if (_aoCriar is not null) AcadApp.DocumentManager.DocumentCreated -= _aoCriar;
            if (_aoDestruir is not null) AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desligar o registro de tudo.", erro);
        }

        _aoCriar = null;
        _aoDestruir = null;
    }

    private static void Escutar(Document? documento, string? frase)
    {
        if (documento is null || EmAndamento.ContainsKey(documento)) return;

        try
        {
            EmAndamento[documento] = new Stack<(string, Stopwatch)>();
            documento.CommandWillStart += AoComecar;
            documento.CommandEnded += AoTerminar;
            documento.CommandCancelled += AoCancelar;
            documento.CommandFailed += AoFalhar;

            if (frase is not null) RegistroDeDiagnostico.Registrar(frase);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui registrar os comandos de um desenho.", erro);
        }
    }

    private static void Soltar(Document? documento)
    {
        if (documento is null || !EmAndamento.Remove(documento, out var pilha)) return;

        try
        {
            documento.CommandWillStart -= AoComecar;
            documento.CommandEnded -= AoTerminar;
            documento.CommandCancelled -= AoCancelar;
            documento.CommandFailed -= AoFalhar;

            var abertos = pilha.Count > 0 ? $" (com {string.Join(", ", pilha.Select(c => c.Nome))} sem terminar)" : string.Empty;
            RegistroDeDiagnostico.Registrar($"desenho fechado: {Path.GetFileName(documento.Name)}{abertos}");
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao soltar o registro dos comandos de um desenho.", erro);
        }
    }

    private static void AoComecar(object? sender, CommandEventArgs e)
    {
        if (sender is not Document documento || !EmAndamento.TryGetValue(documento, out var pilha)) return;

        pilha.Push((e.GlobalCommandName, Stopwatch.StartNew()));
        RegistroDeDiagnostico.Registrar($"comando {e.GlobalCommandName} começou ({Path.GetFileName(documento.Name)})");
    }

    private static void AoTerminar(object? sender, CommandEventArgs e) => Fechar(sender, e, "terminou");

    private static void AoCancelar(object? sender, CommandEventArgs e) => Fechar(sender, e, "foi cancelado");

    private static void AoFalhar(object? sender, CommandEventArgs e) => Fechar(sender, e, "FALHOU");

    private static void Fechar(object? sender, CommandEventArgs e, string como)
    {
        if (sender is not Document documento || !EmAndamento.TryGetValue(documento, out var pilha)) return;

        // O do topo é o que acaba (o transparente acaba antes do que ele interrompeu).
        var segundos = pilha.Count > 0 ? pilha.Pop().Relogio.Elapsed.TotalSeconds : double.NaN;
        var tempo = double.IsNaN(segundos) ? string.Empty : string.Format(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), " em {0:0.0} s", segundos);
        RegistroDeDiagnostico.Registrar($"comando {e.GlobalCommandName} {como}{tempo}");
    }

    private static void AoEscaparErro(object sender, UnhandledExceptionEventArgs e) =>
        RegistroDeDiagnostico.Registrar(
            e.IsTerminating ? "ERRO que escapou de tudo; o AutoCAD vai fechar." : "ERRO que escapou de tudo.",
            e.ExceptionObject as Exception);

    private static void AoEscaparTarefa(object? sender, UnobservedTaskExceptionEventArgs e) =>
        RegistroDeDiagnostico.Registrar("ERRO numa tarefa em segundo plano.", e.Exception);

    private static void AoEscaparDaJanela(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) =>
        RegistroDeDiagnostico.Registrar("ERRO que escapou de uma janela.", e.Exception);

    /// <summary>
    /// Liga o log da linha de comando do AutoCAD na pasta do plugin, se o
    /// usuário não tinha ligado o dele (aí fica o dele, onde ele quis), e
    /// tira os arquivos com mais de 30 dias.
    /// </summary>
    private static void LigarALinhaDeComandoNaFolga(object? sender, EventArgs e)
    {
        AcadApp.Idle -= LigarALinhaDeComandoNaFolga;

        try
        {
            var pasta = PastaDaLinhaDeComando;
            Directory.CreateDirectory(pasta);

            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(arquivo) < DateTime.Now.AddDays(-DiasGuardados)) File.Delete(arquivo);
                }
                catch (Exception)
                {
                    // Arquivo aberto pelo AutoCAD (o desta sessão): fica.
                }
            }

            if (Convert.ToInt32(AcadApp.GetSystemVariable("LOGFILEMODE"), System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                AcadApp.SetSystemVariable("LOGFILEPATH", pasta.EndsWith('\\') ? pasta : pasta + "\\");
                AcadApp.SetSystemVariable("LOGFILEMODE", 1);
            }

            RegistroDeDiagnostico.Registrar($"linha de comando registrada em {AcadApp.GetSystemVariable("LOGFILEPATH")}");
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ligar o log da linha de comando do AutoCAD.", erro);
        }
    }
}
