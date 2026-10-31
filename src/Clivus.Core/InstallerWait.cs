namespace Clivus.Core;

/// <summary>O que o instalador que espera o Civil 3D faz agora.</summary>
public enum WaitStep
{
    /// <summary>Espera mais um pouco (o Civil 3D está aberto, ou fechou há pouco).</summary>
    Wait,

    /// <summary>Instala: tudo fechado há tempo bastante.</summary>
    Install,

    /// <summary>Desiste: o Civil 3D ficou aberto além do limite.</summary>
    GiveUp,
}

/// <summary>O que fazer depois de uma tentativa de instalar.</summary>
public enum AfterInstallStep
{
    /// <summary>Acabou (instalou, ou a regra de versão recusou com a frase dela).</summary>
    Done,

    /// <summary>Falhou (arquivo preso, antivírus): tenta de novo daqui a pouco.</summary>
    Retry,

    /// <summary>O Civil 3D abriu de novo entre a olhada e a instalação: volta a esperar.</summary>
    WaitAgain,
}

/// <summary>
/// A espera do instalador rodado pela atualização (<c>/aguardar-civil3d</c>,
/// etapa 43): ele olha os processos de tempos em tempos e instala quando
/// nenhum que segura o plugin está aberto há alguns segundos. As decisões
/// ficam aqui, sem processo, para os testes; olhar os processos e dormir é
/// do instalador.
/// </summary>
public static class InstallerWait
{
    /// <summary>
    /// Os processos que seguram os arquivos do bundle: o Civil 3D (que é o
    /// acad.exe), o Core Console e o Clivus.Link (o atalho do TermoVision,
    /// que mora na pasta do bundle e pode estar com um aviso aberto).
    /// </summary>
    public static IReadOnlyList<string> BlockingProcesses { get; } = ["acad", "accoreconsole", "Clivus.Link"];

    /// <summary>
    /// Até quando espera: um dia de trabalho. Quem clica em "Atualizar agora"
    /// de manhã e fecha o Civil 3D à noite ainda recebe a versão nova.
    /// </summary>
    public static readonly TimeSpan Limit = TimeSpan.FromHours(12);

    /// <summary>De quanto em quanto tempo olha os processos.</summary>
    public static readonly TimeSpan Poll = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Quanto tempo tudo precisa ficar fechado antes de instalar: o Civil 3D
    /// que reinicia, uma segunda janela dele abrindo, o Windows soltando os
    /// arquivos.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    /// <summary>Quantas vezes tenta instalar quando a instalação falha.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Saída: já há um instalador esperando o Civil 3D nesta sessão (ele instala; este sai).</summary>
    public const int ExitAlreadyWaiting = 7;

    /// <summary>Saída: o Civil 3D ficou aberto além de <see cref="Limit"/>.</summary>
    public const int ExitTimedOut = 8;

    /// <summary>Saída: o Windows está desligando ou saindo da conta; instalar agora poderia ficar pela metade.</summary>
    public const int ExitSessionEnding = 9;

    /// <summary>O próximo passo.</summary>
    /// <param name="abertos">Quantos processos de <see cref="BlockingProcesses"/> estão abertos.</param>
    /// <param name="esperado">Quanto tempo já esperou.</param>
    /// <param name="fechadoHa">Há quanto tempo está tudo fechado (zero se há algum aberto).</param>
    public static WaitStep Next(int abertos, TimeSpan esperado, TimeSpan fechadoHa)
    {
        if (abertos > 0) return esperado >= Limit ? WaitStep.GiveUp : WaitStep.Wait;

        // Fechou: instala depois da folga, mesmo que o limite tenha passado nela.
        return fechadoHa >= Grace ? WaitStep.Install : WaitStep.Wait;
    }

    /// <summary>Depois da tentativa número <paramref name="tentativa"/> (1, 2, ...) com o código de saída dela.</summary>
    public static AfterInstallStep AfterInstall(int codigo, int tentativa) => codigo switch
    {
        0 => AfterInstallStep.Done,
        5 => AfterInstallStep.WaitAgain,
        1 when tentativa < MaxAttempts => AfterInstallStep.Retry,
        _ => AfterInstallStep.Done,
    };

    /// <summary>
    /// Se instala por cima do que está instalado: não, se o instalado já é
    /// esta versão ou uma mais nova (outra atualização chegou antes, ou o
    /// usuário instalou à mão). Sem saber uma das duas, instala.
    /// </summary>
    public static bool ShouldInstallOver(string? estaVersao, string? instalada) =>
        SemanticVersion.Parse(estaVersao) is not { } nova
        || SemanticVersion.Parse(instalada) is not { } atual
        || atual.CompareTo(nova) < 0;
}
