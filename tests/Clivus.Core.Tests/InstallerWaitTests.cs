namespace Clivus.Core.Tests;

/// <summary>
/// Etapa 43: o instalador que a atualização roda com <c>/aguardar-civil3d</c>.
/// Ele espera o Civil 3D fechar (por até um dia de trabalho), instala como no
/// silencioso e avisa. Aqui, o que não depende de processo: a linha de
/// comando, quando esperar, instalar ou desistir, quando tentar de novo e se
/// instala por cima do que está instalado.
/// </summary>
public class InstallerWaitTests
{
    private static readonly TimeSpan Pouco = TimeSpan.FromSeconds(1);

    // ------------------------------------------------------- a linha de comando

    /// <summary>As opções de antes continuam lidas como antes (o rodar-testes.ps1 e o desinstalador dependem delas).</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void AsOpcoesDeAntesNaoMudam()
    {
        var janela = InstallerOptions.Parse([]);
        Assert.Equal(new InstallerOptions(false, false, false, null, false, null, null), janela);

        var teste = InstallerOptions.Parse(["/silencioso", @"/teste=C:\t\clivus", @"/log=C:\t\clivus\instalar.log"]);
        Assert.True(teste.Silent);
        Assert.False(teste.Uninstall);
        Assert.False(teste.WaitForCivil3D);
        Assert.Equal(@"C:\t\clivus", teste.TestFolder);
        Assert.Equal(@"C:\t\clivus\instalar.log", teste.LogFile);

        var desinstalar = InstallerOptions.Parse(["/DESINSTALAR", "/Silencioso", "/teste=x", "/ignorar-civil3d", "/idioma=es", "/log=\"C:\\a b\\d.log\""]);
        Assert.True(desinstalar.Uninstall);
        Assert.True(desinstalar.Silent);
        Assert.True(desinstalar.IgnoreCivil3D);
        Assert.Equal("es", desinstalar.Language);
        Assert.Equal(@"C:\a b\d.log", desinstalar.LogFile);

        // Ignorar o Civil 3D só vale no teste.
        Assert.False(InstallerOptions.Parse(["/silencioso", "/ignorar-civil3d"]).IgnoreCivil3D);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void EsperarOCivil3DEInstalarSemJanela()
    {
        var opcoes = InstallerOptions.Parse(["/AGUARDAR-CIVIL3D", "/log=C:\\x\\atualizacao.log"]);

        Assert.True(opcoes.WaitForCivil3D);
        Assert.True(opcoes.Silent);
        Assert.False(opcoes.Uninstall);

        // Esperar é só para instalar.
        var desinstalar = InstallerOptions.Parse(["/aguardar-civil3d", "/desinstalar"]);
        Assert.False(desinstalar.WaitForCivil3D);
        Assert.True(desinstalar.Uninstall);
        Assert.False(desinstalar.Silent);
    }

    // ------------------------------------------------------------- a espera

    [Fact]
    [Trait("Etapa", "43")]
    public void ComOCivil3DAbertoEspera()
    {
        Assert.Equal(WaitStep.Wait, InstallerWait.Next(1, TimeSpan.Zero, TimeSpan.Zero));
        Assert.Equal(WaitStep.Wait, InstallerWait.Next(3, TimeSpan.FromHours(11.9), TimeSpan.Zero));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void FechadoHaPoucoAindaEspera()
    {
        // O Civil 3D que reinicia, ou abre uma segunda janela: só instala depois da folga.
        Assert.Equal(WaitStep.Wait, InstallerWait.Next(0, TimeSpan.FromMinutes(5), TimeSpan.Zero));
        Assert.Equal(WaitStep.Wait, InstallerWait.Next(0, TimeSpan.FromMinutes(5), InstallerWait.Grace - Pouco));
        Assert.Equal(WaitStep.Install, InstallerWait.Next(0, TimeSpan.FromMinutes(5), InstallerWait.Grace));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void AbertoAlemDoLimiteDesiste()
    {
        Assert.Equal(WaitStep.GiveUp, InstallerWait.Next(1, InstallerWait.Limit, TimeSpan.Zero));

        // Fechou no fim do limite: termina a folga e instala, em vez de desistir.
        Assert.Equal(WaitStep.Wait, InstallerWait.Next(0, InstallerWait.Limit + Pouco, Pouco));
        Assert.Equal(WaitStep.Install, InstallerWait.Next(0, InstallerWait.Limit + InstallerWait.Grace, InstallerWait.Grace));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void OLimiteCobreUmDiaDeTrabalhoEOsProcessosSaoOsQueSeguramOBundle()
    {
        Assert.InRange(InstallerWait.Limit, TimeSpan.FromHours(8), TimeSpan.FromHours(24));
        Assert.True(InstallerWait.Grace > InstallerWait.Poll);
        Assert.Contains("acad", InstallerWait.BlockingProcesses);
        Assert.Contains("accoreconsole", InstallerWait.BlockingProcesses);
        Assert.Contains("Clivus.Link", InstallerWait.BlockingProcesses);

        // Códigos novos, sem pisar nos de antes (0 a 6).
        int[] novos = [InstallerWait.ExitAlreadyWaiting, InstallerWait.ExitTimedOut, InstallerWait.ExitSessionEnding];
        Assert.Equal(3, novos.Distinct().Count());
        Assert.All(novos, c => Assert.True(c > 6));
    }

    // ---------------------------------------------------- depois de instalar

    [Fact]
    [Trait("Etapa", "43")]
    public void DepoisDeInstalar()
    {
        Assert.Equal(AfterInstallStep.Done, InstallerWait.AfterInstall(0, 1));

        // O Civil 3D abriu de novo entre a olhada e a instalação (código 5): volta a esperar.
        Assert.Equal(AfterInstallStep.WaitAgain, InstallerWait.AfterInstall(5, 1));
        Assert.Equal(AfterInstallStep.WaitAgain, InstallerWait.AfterInstall(5, InstallerWait.MaxAttempts));

        // Falha (arquivo preso, antivírus): tenta de novo, até o máximo.
        Assert.Equal(AfterInstallStep.Retry, InstallerWait.AfterInstall(1, 1));
        Assert.Equal(AfterInstallStep.Retry, InstallerWait.AfterInstall(1, InstallerWait.MaxAttempts - 1));
        Assert.Equal(AfterInstallStep.Done, InstallerWait.AfterInstall(1, InstallerWait.MaxAttempts));

        // A regra de versão recusou (2, 3, 4, 6): a frase dela diz o porquê; tentar de novo não muda nada.
        foreach (var codigo in new[] { 2, 3, 4, 6 })
            Assert.Equal(AfterInstallStep.Done, InstallerWait.AfterInstall(codigo, 1));
    }

    /// <summary>
    /// Revisão: a tentativa que falhou pode ter deixado só o começo da versão
    /// nova (o descritor vem primeiro no pacote); a seguinte instala de novo,
    /// em vez de ler "já está instalada" e sair com sucesso e o bundle pela metade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void DepoisDeUmaFalhaInstalaDeNovoMesmoComOMesmoNumero()
    {
        Assert.Equal(InstallDecision.AlreadyCurrent, InstallerWait.Decide("0.1.1", "0.1.1", true, true, falhasAntes: 0));
        Assert.Equal(InstallDecision.Install, InstallerWait.Decide("0.1.1", "0.1.1", true, true, falhasAntes: 1));
        Assert.Equal(InstallDecision.Install, InstallerWait.Decide("0.1.1", "0.1.1", true, false, falhasAntes: 2));
    }

    /// <summary>Revisão: o que o usuário desinstalou enquanto a atualização esperava não volta.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void NaoInstalaDeVoltaOQueOUsuarioDesinstalou()
    {
        Assert.Equal(InstallDecision.UninstalledMeanwhile, InstallerWait.Decide("0.1.1", null, estavaInstalado: true, estaInstalado: false, falhasAntes: 0));

        // Não estava instalado quando a espera começou (instalação nova): instala.
        Assert.Equal(InstallDecision.Install, InstallerWait.Decide("0.1.1", null, estavaInstalado: false, estaInstalado: false, falhasAntes: 0));
        Assert.Equal(InstallDecision.Install, InstallerWait.Decide("0.1.1", "0.1.0", estavaInstalado: true, estaInstalado: true, falhasAntes: 0));
        Assert.Equal(InstallDecision.AlreadyCurrent, InstallerWait.Decide("0.1.1", "0.2.0", estavaInstalado: true, estaInstalado: true, falhasAntes: 0));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void OsDoisMutexTemNomesDiferentesNaSessao()
    {
        Assert.StartsWith(@"Local\", PluginUpdate.WaitingMutexName, StringComparison.Ordinal);
        Assert.StartsWith(@"Local\", InstallerWait.InstallingMutexName, StringComparison.Ordinal);
        Assert.NotEqual(PluginUpdate.WaitingMutexName, InstallerWait.InstallingMutexName);
        Assert.True(InstallerWait.InstallingWait > TimeSpan.Zero);
    }

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.1.1", null, true)]
    [InlineData("0.1.1", "$(Version)", true)]
    [InlineData("0.1.1", "0.1.1", false)]
    [InlineData("0.1.1", "0.1.2", false)]
    [InlineData("0.1.1", "0.2.0-beta.1", false)]
    [InlineData("0.0.0", "0.1.0", false)]
    public void NaoInstalaPorCimaDeUmaVersaoIgualOuMaisNova(string estaVersao, string? instalada, bool instala) =>
        Assert.Equal(instala, InstallerWait.ShouldInstallOver(estaVersao, instalada));
}
