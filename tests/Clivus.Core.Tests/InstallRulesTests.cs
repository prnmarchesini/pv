namespace Clivus.Core.Tests;

/// <summary>A regra do instalador: Civil 3D 2026 (R25.1), com as mensagens para cada caso.</summary>
public class InstallRulesTests
{
    private static AutoCadInstall A(string serie, bool civil = true) => new(serie, $@"C:\AutoCAD {serie}", civil);

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData("R25.1", 25.1)]
    [InlineData("r24", 24.0)]
    [InlineData("R25.10", 25.10)]
    public void LeASerie(string serie, double esperado) => Assert.Equal((decimal)esperado, InstallRules.ParseSeries(serie));

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData("25.1")]
    [InlineData("R")]
    [InlineData("")]
    [InlineData(null)]
    public void SerieForaDoFormato(string? serie) => Assert.Null(InstallRules.ParseSeries(serie));

    [Fact]
    [Trait("Etapa", "9")]
    public void CadaCasoComSuaMensagemESeuCodigo()
    {
        var (ok, escolhida, frase) = InstallRules.Check([A("R24.3"), A("R25.1", civil: false), A("R25.1")], "R25.1", "R25.1");
        Assert.Equal(InstallCheck.Ok, ok);
        Assert.True(escolhida!.HasCivil3D);
        Assert.Contains("Civil 3D 2026 encontrado", frase, StringComparison.Ordinal);

        Assert.Equal(InstallCheck.NoAutoCad, InstallRules.Check([], "R25.1", "R25.1").Result);
        Assert.Equal(InstallCheck.WrongSeries, InstallRules.Check([A("R24.3")], "R25.1", "R25.1").Result);
        Assert.Equal(InstallCheck.NoCivil3D, InstallRules.Check([A("R25.1", civil: false)], "R25.1", "R25.1").Result);
        Assert.Equal(InstallCheck.UnknownSeries, InstallRules.Check([A("ACAD-XX")], "R25.1", "R25.1").Result);

        Assert.Equal([0, 2, 3, 4, 6], new[] { InstallCheck.Ok, InstallCheck.WrongSeries, InstallCheck.NoCivil3D, InstallCheck.NoAutoCad, InstallCheck.UnknownSeries }.Select(InstallRules.ExitCode));
    }
}
