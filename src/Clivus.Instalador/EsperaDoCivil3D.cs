using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Clivus.Core;

namespace Clivus.Instalador;

/// <summary>
/// <c>/aguardar-civil3d</c> (etapa 43): o instalador que a atualização do
/// plugin baixou e rodou. Espera o Civil 3D fechar (e o resto de
/// <see cref="InstallerWait.BlockingProcesses"/>), por até
/// <see cref="InstallerWait.Limit"/>; instala como no silencioso e avisa numa
/// janela simples do Windows (a mesma do Clivus.Link). Um só por sessão (o
/// mutex de <see cref="PluginUpdate.WaitingMutexName"/>, que o plugin também
/// olha). Não instala por cima de uma versão igual ou mais nova.
///
/// O registro vai para o <c>/log=</c>: uma linha por acontecimento, e a
/// última é a de sempre, <c>{código} {frase}</c>. As decisões (esperar,
/// instalar, desistir, tentar de novo) são do <see cref="InstallerWait"/>, no
/// Core, com teste; aqui só se olha os processos e se dorme.
/// </summary>
internal static class EsperaDoCivil3D
{
    internal static int Executar(Instalacao instalacao, InstallerOptions opcoes)
    {
        using var mutex = new Mutex(initiallyOwned: true, PluginUpdate.WaitingMutexName, out var primeiro);

        if (!primeiro)
        {
            // Outro instalador já espera nesta sessão: ele instala; este só
            // acrescenta uma linha ao registro (sem apagar o dele) e sai.
            Registrar(opcoes.LogFile, $"{InstallerWait.ExitAlreadyWaiting} {Tr.T("Já há uma atualização do Clivus Solar esperando o Civil 3D fechar.")}", acrescentar: true);
            return InstallerWait.ExitAlreadyWaiting;
        }

        try
        {
            return Esperar(instalacao, opcoes);
        }
        catch (Exception erro)
        {
            var frase = Tr.F("Não consegui instalar: {0}", erro.Message);
            Registrar(opcoes.LogFile, $"1 {frase}");
            Avisar(frase, erro: true);
            return 1;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static int Esperar(Instalacao instalacao, InstallerOptions opcoes)
    {
        var log = opcoes.LogFile;
        var versao = Instalacao.Versao;

        // O registro começa de novo a cada atualização.
        Registrar(log, $"Clivus Solar {versao}: esperando o Civil 3D fechar para instalar (até {InstallerWait.Limit.TotalHours:0} horas).", acrescentar: false);

        var desde = Stopwatch.StartNew();
        Stopwatch? fechado = null;
        var tentativa = 0;
        var antes = string.Empty;

        while (true)
        {
            var (abertos, quais) = Instalacao.ProcessosQueSeguramOPlugin();

            if (abertos > 0)
            {
                fechado = null;
                if (quais != antes) Registrar(log, $"Aberto: {quais}.");
            }
            else if (fechado is null)
            {
                fechado = Stopwatch.StartNew();
                Registrar(log, $"Tudo fechado; instala em {InstallerWait.Grace.TotalSeconds:0} s se continuar assim.");
            }

            antes = quais;

            switch (InstallerWait.Next(abertos, desde.Elapsed, fechado?.Elapsed ?? TimeSpan.Zero))
            {
                case WaitStep.Wait:
                    Thread.Sleep(InstallerWait.Poll);
                    continue;

                case WaitStep.GiveUp:
                {
                    var frase = Tr.F("A atualização do Clivus Solar {0} não foi instalada: o Civil 3D ficou aberto por mais de {1:0} horas. Abra o Civil 3D e use o botão Atualizar de novo.", versao, InstallerWait.Limit.TotalHours);
                    Registrar(log, $"{InstallerWait.ExitTimedOut} {frase}");
                    Avisar(frase, erro: true);
                    return InstallerWait.ExitTimedOut;
                }
            }

            // Instalar. Com o Windows desligando, a instalação podia ficar pela
            // metade: fica para a próxima (o plugin oferece de novo).
            if (SessaoTerminando())
            {
                Registrar(log, $"{InstallerWait.ExitSessionEnding} {Tr.T("O Windows está desligando: a atualização do Clivus Solar fica para a próxima vez.")}");
                return InstallerWait.ExitSessionEnding;
            }

            var instalada = instalacao.VersaoInstalada();
            if (!InstallerWait.ShouldInstallOver(versao, instalada))
            {
                Registrar(log, $"0 {Tr.F("O Clivus Solar {0} já está instalado; a atualização para a {1} não foi preciso.", instalada, versao)}");
                return 0;
            }

            tentativa++;
            Registrar(log, $"Instalando (tentativa {tentativa}; instalada antes: {instalada ?? "nenhuma"}).");
            var (codigo, resultado) = Programa.Executar(instalacao, desinstalar: false, opcoes.IgnoreCivil3D, null);

            switch (InstallerWait.AfterInstall(codigo, tentativa))
            {
                case AfterInstallStep.WaitAgain:
                    // O Civil 3D abriu de novo entre a olhada e a instalação.
                    Registrar(log, $"O Civil 3D abriu de novo ({codigo} {resultado}); volta a esperar.");
                    fechado = null;
                    continue;

                case AfterInstallStep.Retry:
                    Registrar(log, $"Falhou ({codigo} {resultado}); tenta de novo em {InstallerWait.Grace.TotalSeconds:0} s.");
                    Thread.Sleep(InstallerWait.Grace);
                    continue;

                default:
                    Registrar(log, $"{codigo} {resultado}");
                    Avisar(codigo == 0 ? resultado : Tr.F("A atualização do Clivus Solar não foi instalada. {0}", resultado), erro: codigo != 0);
                    return codigo;
            }
        }
    }

    /// <summary>Uma linha no registro (com a hora); falha ao gravar não para a atualização.</summary>
    private static void Registrar(string? arquivo, string linha, bool acrescentar = true)
    {
        if (string.IsNullOrWhiteSpace(arquivo)) return;

        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(arquivo)) is { Length: > 0 } pasta) Directory.CreateDirectory(pasta);

            var texto = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {linha}{Environment.NewLine}";
            if (acrescentar) File.AppendAllText(arquivo, texto);
            else File.WriteAllText(arquivo, texto);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Sem onde escrever, segue: o aviso do fim ainda aparece.
        }
    }

    /// <summary>A janela simples do Windows, por cima de tudo (o Civil 3D acabou de fechar).</summary>
    private static void Avisar(string texto, bool erro) =>
        MessageBoxW(IntPtr.Zero, texto, "Clivus Solar", (erro ? MbIconError : MbIconInformation) | MbSetForeground | MbTopMost);

    /// <summary>Se o Windows está desligando ou saindo da conta (SM_SHUTTINGDOWN).</summary>
    private static bool SessaoTerminando()
    {
        try
        {
            return GetSystemMetrics(SmShuttingDown) != 0;
        }
        catch (Exception erro) when (erro is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private const uint MbIconError = 0x10;
    private const uint MbIconInformation = 0x40;
    private const uint MbSetForeground = 0x10000;
    private const uint MbTopMost = 0x40000;
    private const int SmShuttingDown = 0x2000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr janela, string texto, string titulo, uint tipo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int indice);
}
