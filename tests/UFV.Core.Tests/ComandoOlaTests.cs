namespace UFV.Core.Tests;

/// <summary>
/// O nome do comando aparece em quatro lugares: a constante
/// PluginInfo.ComandoOla, o atributo [CommandMethod] que o registra, o
/// CommandParameter do botão da ribbon e o script do Core Console.
///
/// Os três primeiros já saem da constante, então o compilador os mantém
/// juntos. O script é texto solto: renomear o comando o deixaria para trás, e
/// o teste de nível 2 passaria a falhar por um motivo que não é o dele.
/// </summary>
public class ComandoOlaTests
{
    [Fact]
    [Trait("Etapa", "0")]
    public void OScriptDoCoreConsoleChamaOComandoPeloNomeCerto()
    {
        var script = Repositorio.Caminho("tests", "UFV.Integration", "ufv-ola.scr");

        Assert.True(File.Exists(script), $"Script de nível 2 não encontrado em {script}.");

        Assert.Contains(
            PluginInfo.ComandoOla,
            File.ReadAllText(script),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void ONomeDoComandoTemOPrefixoDoPlugin()
    {
        // Comando de plugin sem prefixo colide com comando de outro plugin ou
        // do próprio AutoCAD, e quem perde é o último a carregar.
        Assert.StartsWith("UFV_", PluginInfo.ComandoOla, StringComparison.Ordinal);
    }
}
