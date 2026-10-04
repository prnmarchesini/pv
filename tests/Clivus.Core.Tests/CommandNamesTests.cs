using System.Reflection;

namespace Clivus.Core.Tests;

/// <summary>Os nomes dos comandos em inglês e espanhol (10.5).</summary>
public class CommandNamesTests
{
    /// <summary>Os comandos digitáveis: as constantes CLIVUS_* do PluginInfo, sem os de teste (_AUTO).</summary>
    private static List<string> Digitaveis() =>
        typeof(PluginInfo).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.StartsWith("Comando", StringComparison.Ordinal) && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(n => n.StartsWith("CLIVUS_", StringComparison.Ordinal) && !n.EndsWith("_AUTO", StringComparison.Ordinal) && !n.Contains("_AUTO_", StringComparison.Ordinal))
            .Distinct()
            .ToList();

    [Fact]
    [Trait("Etapa", "10")]
    public void TodoComandoDigitavelTemNomeEmInglesEEspanhol()
    {
        var faltam = Digitaveis().Where(c => !CommandNames.Table.ContainsKey(c)).ToList();
        Assert.True(faltam.Count == 0, "sem nome em inglês/espanhol: " + string.Join(", ", faltam));

        var sobram = CommandNames.Table.Keys.Except(Digitaveis()).ToList();
        Assert.True(sobram.Count == 0, "na tabela, mas não é comando: " + string.Join(", ", sobram));
    }

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.Spanish)]
    public void NenhumNomeSeRepeteNemColideComOutroComando(UiLanguage idioma)
    {
        var globais = Digitaveis().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var locais = CommandNames.Table.Keys
            .Select(g => (Global: g, Local: CommandNames.LocalName(g, idioma)))
            .Where(x => x.Local is not null)
            .ToList();

        var repetidos = locais.GroupBy(x => x.Local!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(repetidos.Count == 0, "repetidos: " + string.Join(", ", repetidos));

        // O nome traduzido não pode ser o nome global de OUTRO comando.
        var colidem = locais.Where(x => globais.Contains(x.Local!)).Select(x => $"{x.Global} -> {x.Local}").ToList();
        Assert.True(colidem.Count == 0, "colidem com outro comando: " + string.Join(", ", colidem));

        Assert.All(locais, x => Assert.Matches("^CLIVUS_[A-Z0-9_]+$", x.Local!));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void EmPortuguesOuNomeIgualNaoHaNadaARegistrar()
    {
        Assert.Null(CommandNames.LocalName(PluginInfo.ComandoTrocarMesa, UiLanguage.Portuguese));
        Assert.Equal("CLIVUS_SWAP_TABLE", CommandNames.LocalName(PluginInfo.ComandoTrocarMesa, UiLanguage.English));
        Assert.Equal("CLIVUS_CAMBIAR_MESA", CommandNames.LocalName(PluginInfo.ComandoTrocarMesa, UiLanguage.Spanish));
        Assert.Null(CommandNames.LocalName("CLIVUS_EXCEL", UiLanguage.English));
        Assert.Null(CommandNames.LocalName("CLIVUS_QUE_NAO_EXISTE", UiLanguage.English));
    }
}
