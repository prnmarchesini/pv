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
/// olha), solto antes do aviso do fim. Não instala por cima de uma versão
/// igual ou mais nova, nem de volta o que o usuário desinstalou enquanto
/// esperava.
///
/// O registro vai para o <c>/log=</c>, uma linha com a hora por
/// acontecimento; a última traz o código e a frase, como no silencioso. As
/// decisões (esperar, instalar, desistir, tentar de novo) são do
/// <see cref="InstallerWait"/>, no Core, com teste; aqui só se olha os
/// processos e se dorme.
/// </summary>
internal static class EsperaDoCivil3D
{
    internal static int Executar(Instalacao instalacao, InstallerOptions opcoes)
    {
        Mutex mutex;
        bool primeiro;

        try
        {
            mutex = new Mutex(initiallyOwned: true, PluginUpdate.WaitingMutexName, out primeiro);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            Registrar(opcoes.LogFile, $"1 {Tr.F("Não consegui instalar: {0}", erro.Message)}", acrescentar: true);
            return 1;
        }

        (int Codigo, string Frase, bool Avisar) fim;

        using (mutex)
        {
            if (!primeiro)
            {
                // Outro instalador já espera nesta sessão: ele instala; este só
                // acrescenta uma linha ao registro (sem apagar o dele) e sai.
                Registrar(opcoes.LogFile, $"{InstallerWait.ExitAlreadyWaiting} {Tr.T("Já há uma atualização do Clivus Solar esperando o Civil 3D fechar.")}", acrescentar: true);
                return InstallerWait.ExitAlreadyWaiting;
            }

            try
            {
                fim = Esperar(instalacao, opcoes);
            }
            catch (Exception erro)
            {
                fim = (1, Tr.F("Não consegui instalar: {0}", erro.Message), true);
                Registrar(opcoes.LogFile, $"{fim.Codigo} {fim.Frase}");
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        // O aviso depois de soltar o mutex: enquanto a janela está aberta, o
        // plugin já não diz que a atualização "espera o Civil 3D fechar".
        if (fim.Avisar) Avisar(fim.Frase, erro: fim.Codigo != 0);
        return fim.Codigo;
    }

    /// <summary>Espera e instala. O código, a frase e se ela vai para a janela do fim.</summary>
    private static (int Codigo, string Frase, bool Avisar) Esperar(Instalacao instalacao, InstallerOptions opcoes)
    {
        var log = opcoes.LogFile;
        var versao = Instalacao.Versao;
        var estavaInstalado = instalacao.Instalado();

        // O registro começa de novo a cada atualização.
        Registrar(log, $"Clivus Solar {versao}: esperando o Civil 3D fechar para instalar (até {InstallerWait.Limit.TotalHours:0} horas).", acrescentar: false);

        var desde = Stopwatch.StartNew();
        Stopwatch? fechado = null;
        var tentativa = 0;
        var falhas = 0;
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
                    return (InstallerWait.ExitTimedOut, frase, true);
                }
            }

            // Instalar. Com o Windows desligando, a instalação podia ficar pela
            // metade: fica para a próxima (o plugin oferece de novo).
            if (SessaoTerminando())
            {
                var frase = Tr.T("O Windows está desligando: a atualização do Clivus Solar fica para a próxima vez.");
                Registrar(log, $"{InstallerWait.ExitSessionEnding} {frase}");
                return (InstallerWait.ExitSessionEnding, frase, false);
            }

            var instalada = instalacao.VersaoInstalada();
            switch (InstallerWait.Decide(versao, instalada, estavaInstalado, instalacao.Instalado(), falhas))
            {
                case InstallDecision.AlreadyCurrent:
                {
                    var frase = Tr.F("O Clivus Solar {0} já está instalado; a atualização para a {1} não foi necessária.", instalada, versao);
                    Registrar(log, $"0 {frase}");
                    return (0, frase, false);
                }

                case InstallDecision.UninstalledMeanwhile:
                {
                    var frase = Tr.T("O Clivus Solar foi desinstalado enquanto a atualização esperava; ela não instala de volta.");
                    Registrar(log, $"0 {frase}");
                    return (0, frase, false);
                }
            }

            tentativa++;
            Registrar(log, $"Instalando (tentativa {tentativa}; instalada antes: {instalada ?? "nenhuma"}).");
            var (codigo, resultado) = Programa.Executar(instalacao, desinstalar: false, opcoes.IgnoreCivil3D, null);

            // As tentativas que contam para o máximo são as que falharam; o
            // Civil 3D que reabriu não gasta tentativa.
            switch (InstallerWait.AfterInstall(codigo, falhas + 1))
            {
                case AfterInstallStep.WaitAgain:
                    // O Civil 3D abriu de novo entre a olhada e a instalação.
                    Registrar(log, $"O Civil 3D abriu de novo ({codigo} {resultado}); volta a esperar.");
                    fechado = null;
                    continue;

                case AfterInstallStep.Retry:
                    falhas++;
                    Registrar(log, $"Falhou ({codigo} {resultado}); tenta de novo em {InstallerWait.Grace.TotalSeconds:0} s.");
                    Thread.Sleep(InstallerWait.Grace);
                    continue;
            }

            Registrar(log, $"{codigo} {resultado}");

            if (codigo == 0) return (0, resultado, true);

            // A falha de cópia (1) pode ter levado a versão anterior junto: o
            // aviso manda instalar de novo. As da regra de versão (2, 3, 4, 6)
            // param antes de mexer em qualquer arquivo.
            return (codigo, codigo == 1
                ? Tr.F("A atualização do Clivus Solar não foi instalada, e o plugin pode ter ficado incompleto: baixe o instalador na área do cliente e rode de novo. {0}", resultado)
                : Tr.F("A atualização do Clivus Solar não foi instalada. {0}", resultado), true);
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
