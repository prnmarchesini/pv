namespace UFV.Core.Tests;

/// <summary>Passo 8.14: as tags (fileiras, mesas, módulos, strings).</summary>
public class TagsTests
{
    private static TaggedTable Mesa(string letreiro, int colunas, int fileiras)
    {
        var modulos = new List<(int, int)>();
        for (var r = 0; r < fileiras; r++)
            for (var c = 0; c < colunas; c++)
                modulos.Add((c, r));

        return new TaggedTable(letreiro, Guid.NewGuid(), modulos);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData("F1.2", true, 1, 2)]
    [InlineData(" F12.30 ", true, 12, 30)]
    [InlineData("A", false, 0, 0)]
    [InlineData("F1", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void LeOLetreiro(string? letreiro, bool ok, int fileira, int numero)
    {
        Assert.Equal(ok, Tags.TryParseLabel(letreiro, out var f, out var n));
        Assert.Equal(fileira, f);
        Assert.Equal(numero, n);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AOrdemEPelosNumerosNaoPeloTexto()
    {
        var ordem = Tags.InOrder([Mesa("F2.1", 1, 1), Mesa("F1.10", 1, 1), Mesa("X", 1, 1), Mesa("F1.2", 1, 1)]);

        Assert.Equal(["F1.2", "F1.10", "F2.1", "X"], ordem.Select(m => m.Label));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ASerpentinaVoltaNaFileiraDeCima()
    {
        var ordem = Tags.Serpentine([(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)]);

        Assert.Equal([(0, 0), (1, 0), (2, 0), (2, 1), (1, 1), (0, 1)], ordem);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void UmaFileiraPorNumero()
    {
        var fileiras = Tags.Rows([Mesa("F2.2", 1, 1), Mesa("F1.1", 1, 1), Mesa("F2.1", 1, 1), Mesa("F1.2", 1, 1)]);

        Assert.Equal(["F1", "F2"], fileiras.Select(f => f.Label));
        Assert.Equal("F1.1", fileiras[0].First.Label);
        Assert.Equal("F2.1", fileiras[1].First.Label);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void StringsDe14NumaMesa2VDe28()
    {
        var a = Mesa("F1.1", 14, 2);
        var b = Mesa("F1.2", 14, 2);

        var strings = Tags.Strings([b, a], 14);

        Assert.Equal(4, strings.Count);
        Assert.All(strings, s => Assert.True(s.Complete));
        Assert.Equal(["S1", "S2", "S3", "S4"], strings.Select(s => s.Label));

        // A primeira string é a fileira de baixo da F1.1, da esquerda para a direita.
        Assert.All(strings[0].Modules, m => Assert.Equal(a.Id, m.Table));
        Assert.All(strings[0].Modules, m => Assert.Equal(0, m.Row));
        Assert.Equal(0, strings[0].Modules[0].Column);

        // A segunda volta pela de cima.
        Assert.Equal(13, strings[1].Modules[0].Column);
        Assert.Equal(1, strings[1].Modules[0].Row);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OQueSobraNaMesaViraStringIncompleta()
    {
        var strings = Tags.Strings([Mesa("F1.1", 14, 2), Mesa("F1.2", 14, 2)], 20);

        // 28 por mesa em strings de 20: 20 + 8 em cada uma; a string não atravessa mesa.
        Assert.Equal([20, 8, 20, 8], strings.Select(s => s.Modules.Count));
        Assert.Equal(["S1", "S2*", "S3", "S4*"], strings.Select(s => s.Label));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void TamanhoDeStringImpossivelERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tags.Strings([Mesa("F1.1", 1, 1)], 0));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CadaTagTemCamadaPropria()
    {
        var camadas = Enum.GetValues<TagKind>().Select(Tags.LayerName).ToList();

        Assert.Equal(camadas.Count, camadas.Distinct().Count());
        Assert.All(camadas, c => Assert.StartsWith("MARCHENG_UFV_TAG_", c));
    }
}
