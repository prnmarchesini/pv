namespace Clivus.Core.Tests;

/// <summary>O relatório da validação (7.7): limpo quando nada há, e uma linha por tipo de achado, com o que fazer.</summary>
public class LayoutValidationTests
{
    private static LayoutValidation Limpa() => new([], [], [], [], 0, 0, [], 0, 0, [], null);

    [Fact]
    [Trait("Etapa", "7")]
    public void SemAchadosELimpa()
    {
        var v = Limpa();

        Assert.True(v.IsClean);
        Assert.Equal(0, v.Count);
        Assert.Single(v.Lines());
        Assert.Contains("nada a apontar", v.Lines()[0]);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void CadaAchadoViraUmaLinhaComOQueFazer()
    {
        var v = new LayoutValidation(
            ["Área da usina"], ["Alinhamento 1", "Alinhamento 2"], ["F1.1", "F2.3"], ["F3.1"], 1, 2, ["F1.5"], 3, 1, ["F1.2"],
            "a superfície mudou desde o processamento");

        Assert.False(v.IsClean);
        Assert.Equal(1 + 2 + 2 + 1 + 1 + 2 + 1 + 3 + 1 + 1 + 1, v.Count);

        var linhas = v.Lines();
        Assert.Equal(11, linhas.Count);
        Assert.Contains(linhas, l => l.Contains("1 área(s)") && l.Contains("Área da usina") && l.Contains("Reindexar"));
        Assert.Contains(linhas, l => l.Contains("2 alinhamento(s)") && l.Contains("Alinhamento 2"));
        Assert.Contains(linhas, l => l.Contains("2 mesa(s) marcada(s) pendente(s)") && l.Contains("F2.3") && l.Contains("Recalcular pendentes"));
        Assert.Contains(linhas, l => l.Contains("1 mesa(s) fora de onde") && l.Contains("F3.1") && l.Contains("plugin descarregado"));
        Assert.Contains(linhas, l => l.Contains("1 identidade(s) de área"));
        Assert.Contains(linhas, l => l.Contains("2 identidade(s) de alinhamento"));
        Assert.Contains(linhas, l => l.Contains("F1.5") && l.Contains("mais de um contorno"));
        Assert.Contains(linhas, l => l.Contains("3 peça(s) com identidade repetida"));
        Assert.Contains(linhas, l => l.Contains("1 mesa(s) só com peças"));
        Assert.Contains(linhas, l => l.Contains("F1.2") && l.Contains("Recontar"));
        Assert.Contains(linhas, l => l.Contains("terreno: a superfície mudou"));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ListaLongaEResumida()
    {
        var sujas = Enumerable.Range(1, 12).Select(i => $"F1.{i}").ToList();
        var v = Limpa() with { DirtyTables = sujas };

        var linha = Assert.Single(v.Lines());
        Assert.Contains("F1.5", linha);
        Assert.DoesNotContain("F1.6", linha);
        Assert.Contains("e mais 7", linha);
    }

    /// <summary>A identidade da mesa guarda onde ela foi desenhada; âncora não finita não vale.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void AAncoraDaMesaValeSoFinita()
    {
        var mesa = new TableIdentity(Guid.NewGuid(), "F1.1", 700, 700, 0.35, false, null, Anchor: new Clivus.Geo.Point3(1, 2, 3));

        Assert.True(mesa.IsValid);
        Assert.False((mesa with { Anchor = new Clivus.Geo.Point3(double.NaN, 2, 3) }).IsValid);
    }
}
