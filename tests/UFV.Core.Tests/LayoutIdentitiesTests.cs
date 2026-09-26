namespace UFV.Core.Tests;

/// <summary>
/// O estado limpo/sujo da mesa (7.1): nasce limpa, suja com motivo, limpa
/// sem motivo, e o par estado/motivo tem que ser coerente para valer.
/// </summary>
public class LayoutIdentitiesTests
{
    private static TableIdentity Mesa() => new(Guid.NewGuid(), "F1.3", 700.10, 700.35, 0.35, false, null);

    [Fact]
    [Trait("Etapa", "7")]
    public void AMesaNasceLimpa()
    {
        var mesa = Mesa();

        Assert.False(mesa.Dirty);
        Assert.Null(mesa.DirtyReason);
        Assert.True(mesa.IsValid);
        Assert.Equal("F1.3: limpa", mesa.DescribeState());
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void SujarGuardaOMotivoELimparOTira()
    {
        var suja = Mesa().AsDirty("  movida ");

        Assert.True(suja.Dirty);
        Assert.Equal("movida", suja.DirtyReason);
        Assert.True(suja.IsValid);
        Assert.Equal("F1.3: SUJA (movida)", suja.DescribeState());

        var limpa = suja.AsClean();

        Assert.False(limpa.Dirty);
        Assert.Null(limpa.DirtyReason);
        Assert.True(limpa.IsValid);

        // O resto da identidade não muda.
        Assert.Equal(Mesa() with { Id = suja.Id }, limpa with { Id = suja.Id });
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void SujarSemMotivoERecusado()
    {
        Assert.Throws<ArgumentException>(() => Mesa().AsDirty(" "));
    }

    /// <summary>Suja sem motivo, ou limpa com motivo, é XData corrompido: não vale.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void EstadoEMotivoIncoerentesNaoValem()
    {
        Assert.False((Mesa() with { Dirty = true }).IsValid);
        Assert.False((Mesa() with { DirtyReason = "movida" }).IsValid);
    }

    /// <summary>Sujar não mexe na marca do motor: são dois estados diferentes.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void SujaEMarcadaSaoIndependentes()
    {
        var marcada = Mesa() with { Marked = true, Reason = "6 módulo(s) fora da faixa" };
        var suja = marcada.AsDirty("copiada");

        Assert.True(suja.Marked);
        Assert.Equal("6 módulo(s) fora da faixa", suja.Reason);
        Assert.True(suja.AsClean().Marked);
    }
}
