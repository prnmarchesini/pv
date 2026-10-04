namespace Clivus.Core.Tests;

/// <summary>A contagem do desenho (7.6): totais, potência mesa a mesa, pilares, duplicadas e o relatório.</summary>
public class LayoutCensusTests
{
    private static TableIdentity Mesa(string label, bool suja = false, bool marcada = false, double? potencia = 720) =>
        new(Guid.NewGuid(), label, 700, 700, 0.35, marcada, marcada ? "motivo" : null, suja, suja ? "movida" : null, potencia);

    [Fact]
    [Trait("Etapa", "7")]
    public void ContaMesasModulosPilaresEPotencia()
    {
        var mesas = new List<CountedTable>
        {
            new(Mesa("F1.1"), 1, 28, [2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7], 0),
            new(Mesa("F1.2", suja: true), 1, 28, [2.0, 2.0, 2.0, 2.0, 2.0, 2.0], 1),
            new(Mesa("F1.3", marcada: true), 1, 28, [], 7),
            new(null, 0, 5, [1.5], 0),
        };

        var censo = LayoutCensus.Count(mesas, 500);

        Assert.Equal(3, censo.Tables);
        Assert.Equal(0, censo.Duplicated);
        Assert.Equal(1, censo.Orphans);
        Assert.Equal(5, censo.OrphanModules);
        Assert.Equal(1, censo.Dirty);
        Assert.Equal(1, censo.Marked);
        Assert.Equal(89, censo.Modules);
        Assert.Equal(22, censo.Pillars);
        Assert.Equal(8, censo.PillarsWithoutLength);
        Assert.Equal(14, censo.PillarLengths.Count);

        // 84 módulos a 720 W (das mesas) e 5 órfãos a 500 W (reserva).
        Assert.Equal((84 * 720 + 5 * 500) / 1000.0, censo.PowerKwp, 9);
        Assert.Equal(0, censo.TablesWithoutPower);

        var linhas = censo.Lines();
        Assert.Equal(3, linhas.Count);
        Assert.Contains("3 mesa(s), 89 módulo(s), 63 kWp", linhas[0]);
        Assert.Contains("22 pilar(es), 8 sem comprimento; comprimento de 1,50 a 2,70 m", linhas[1]);
        Assert.Contains("1 suja(s), 1 que não cabe(m) no terreno, 1 com peças órfãs (sem contorno; 5 módulo(s) órfão(s)", linhas[2]);
    }

    /// <summary>Cada mesa entra com a SUA potência; a sem potência gravada usa a reserva e o relatório avisa.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void APotenciaEDeCadaMesaEAReservaEAvisada()
    {
        var mesas = new List<CountedTable>
        {
            new(Mesa("F1.1", potencia: 720), 1, 10, [2], 0),
            new(Mesa("F1.2", potencia: 600), 1, 10, [2], 0),
            new(Mesa("F1.3", potencia: null), 1, 10, [2], 0),
        };

        var censo = LayoutCensus.Count(mesas, 550);

        Assert.Equal((7200 + 6000 + 5500) / 1000.0, censo.PowerKwp, 9);
        Assert.Equal(1, censo.TablesWithoutPower);
        Assert.Contains(censo.Lines(), l => l.Contains("1 mesa(s) sem potência gravada") && l.Contains("550 W"));
    }

    /// <summary>Mesa duplicada (dois contornos, um GUID) conta pelos contornos e é avisada.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void MesaDuplicadaContaPelosContornosEAvisa()
    {
        var mesas = new List<CountedTable>
        {
            new(Mesa("F1.1"), 2, 56, [2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2], 0),
            new(Mesa("F1.2"), 1, 28, [2, 2, 2, 2, 2, 2, 2], 0),
        };

        var censo = LayoutCensus.Count(mesas, 720);

        Assert.Equal(3, censo.Tables);
        Assert.Equal(1, censo.Duplicated);
        Assert.Contains(censo.Lines(), l => l.Contains("1 mesa(s) com mais de um contorno"));
    }

    /// <summary>Comprimento que não é número conta como pilar sem comprimento, não some.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void ComprimentoNaoFinitoContaComoSemComprimento()
    {
        var censo = LayoutCensus.Count([new(Mesa("F1.1"), 1, 28, [2, double.NaN, 3], 1)], 720);

        Assert.Equal(4, censo.Pillars);
        Assert.Equal(2, censo.PillarsWithoutLength);
        Assert.Equal(2, censo.PillarLengths.Count);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void DesenhoVazioContaZeroEDuasLinhas()
    {
        var censo = LayoutCensus.Count([], 720);

        Assert.Equal(0, censo.Tables);
        Assert.Equal(0, censo.PowerKwp);
        Assert.Equal(2, censo.Lines().Count);
        Assert.Contains("0 pilar(es)", censo.Lines()[1]);
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void PotenciaInvalidaERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutCensus.Count([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutCensus.Count([], double.NaN));
        Assert.False((Mesa("F1.1", potencia: -5)).IsValid);
    }
}
