using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A numeração (7.10): a distribuição numerada de volta dá os mesmos
/// letreiros; F1.1 na outra ponta inverte tudo; vão acima do limite e
/// azimute diferente abrem fileira; avisos quando a mesa indicada não é
/// a ponta; fileiras colineares em coordenadas UTM; entradas que não valem.
/// </summary>
public class RowNumberingTests
{
    private const double Comprimento = 20;
    private const double Fundo = 4;
    private const double Vao = 6.5;

    /// <summary>A distribuição de sempre: retângulo 100×50, linha na borda oeste, azimute sul: 8 fileiras × 5 mesas.</summary>
    private static (PlanLayout Layout, Dictionary<string, Guid> Guids, List<TableToNumber> Mesas) Distribuida()
    {
        Point3[] area = [new(-50, -50, 0), new(50, -50, 0), new(50, 50, 0), new(-50, 50, 0)];
        Point3[] linha = [new(-50, -50, 0), new(-50, 50, 0)];

        var layout = RowDistributor.Distribute(area, linha, LineSide.Right, 12, 0.5, new TableFootprint(Comprimento, Fundo), Math.PI);

        var guids = layout.Tables.ToDictionary(t => t.Label, _ => Guid.NewGuid());
        var mesas = layout.Tables.Select(t => new TableToNumber(guids[t.Label], t)).ToList();

        return (layout, guids, mesas);
    }

    /// <summary>Uma célula solta: origem, direção, comprimento e fundo padrão.</summary>
    private static TableToNumber Celula(double x, double y, double direcaoGraus, Guid? id = null)
    {
        var d = direcaoGraus * Math.PI / 180;
        var dx = Math.Cos(d);
        var dy = Math.Sin(d);
        var nx = -dy;
        var ny = dx;

        Point3[] cantos =
        [
            new(x, y, 0),
            new(x + dx * Comprimento, y + dy * Comprimento, 0),
            new(x + dx * Comprimento + nx * Fundo, y + dy * Comprimento + ny * Fundo, 0),
            new(x + nx * Fundo, y + ny * Fundo, 0),
        ];

        return new TableToNumber(id ?? Guid.NewGuid(), new PlacedTable(1, 1, cantos[0], d, Comprimento, Fundo, cantos, false));
    }

    private static string LetreiroDe(NumberingResult r, Guid id) => r.Tables.Single(t => t.Id == id).Label;

    [Fact]
    [Trait("Etapa", "7")]
    public void ADistribuicaoNumeradaDeVoltaDaOsMesmosLetreiros()
    {
        var (layout, guids, mesas) = Distribuida();
        var ultima = layout.Rows[^1];

        var r = RowNumbering.Number(mesas, guids["F1.1"], guids[ultima.Tables[0].Label], Vao);

        Assert.Equal(layout.Rows.Count, r.RowCount);
        Assert.Empty(r.Warnings);
        Assert.Equal(mesas.Count, r.Tables.Count);

        foreach (var (letreiro, guid) in guids) Assert.Equal(letreiro, LetreiroDe(r, guid));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void F11NaOutraPontaInverteFileirasENumeros()
    {
        var (layout, guids, mesas) = Distribuida();
        var fileiras = layout.Rows.Count;
        var porFileira = layout.Rows[0].Tables.Count;
        var ultimaMesa = layout.Rows[^1].Tables[^1].Label;

        var r = RowNumbering.Number(mesas, guids[ultimaMesa], guids["F1.1"], Vao);

        Assert.Empty(r.Warnings);

        foreach (var mesa in layout.Tables)
        {
            Assert.Equal($"F{fileiras + 1 - mesa.Row}.{porFileira + 1 - mesa.Number}", LetreiroDe(r, guids[mesa.Label]));
        }
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void VaoAcimaDoLimiteAbreFileiraEAbaixoNao()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(Comprimento + Vao - 0.1, 0, 0);
        var c = Celula(2 * Comprimento + Vao + Vao + 0.2, 0, 0);

        var r = RowNumbering.Number([a, b, c], a.Id, c.Id, Vao);

        Assert.Equal(2, r.RowCount);
        Assert.Equal("F1.1", LetreiroDe(r, a.Id));
        Assert.Equal("F1.2", LetreiroDe(r, b.Id));
        Assert.Equal("F2.1", LetreiroDe(r, c.Id));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("indicada"));
        Assert.Contains(r.Warnings, w => w.Contains("não têm o mesmo número de mesas") && w.Contains("F2 tem 1"));
    }

    /// <summary>
    /// Duas fileiras colineares em coordenadas UTM (o centro das duas cai
    /// na mesma distância da F1.1, a menos do ruído do ponto flutuante):
    /// a da F1.1 vem primeiro, com a F1.1 na ponta final, e a outra vem
    /// depois, sem aviso de "ficou na F2". Antes, o ruído decidia.
    /// </summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void FileirasColinearesEmCoordenadasUtmSaoOrdenadasPelaF11()
    {
        const double x0 = 714_018.37;
        const double y0 = 7_456_163.91;

        var a = Celula(x0, y0, 0);
        var b = Celula(x0 + Comprimento + 0.5, y0, 0);
        var c = Celula(x0 + 2 * Comprimento + Vao + 2, y0, 0);
        var d = Celula(x0 + 3 * Comprimento + Vao + 2.5, y0, 0);
        var acima = Celula(x0, y0 + 12, 0);

        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            // A F1.1 é a última do trecho de cima (d); o trecho a-b vem depois, como F2.
            var r = RowNumbering.Number([a, b, c, d, acima], d.Id, acima.Id, Vao);

            Assert.Equal(3, r.RowCount);
            Assert.Equal("F1.1", LetreiroDe(r, d.Id));
            Assert.Equal("F1.2", LetreiroDe(r, c.Id));
            Assert.Equal("F2.1", LetreiroDe(r, b.Id));
            Assert.Equal("F2.2", LetreiroDe(r, a.Id));
            Assert.Equal("F3.1", LetreiroDe(r, acima.Id));
            Assert.DoesNotContain(r.Warnings, w => w.Contains("indicada"));

            // E a partir do outro trecho, na ponta inicial.
            var r2 = RowNumbering.Number([a, b, c, d, acima], a.Id, acima.Id, Vao);

            Assert.Equal("F1.1", LetreiroDe(r2, a.Id));
            Assert.Equal("F2.1", LetreiroDe(r2, c.Id));
            Assert.Equal("F3.1", LetreiroDe(r2, acima.Id));
            Assert.DoesNotContain(r2.Warnings, w => w.Contains("indicada"));
        }
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ATolerânciaDeAzimuteNaoSeAcumulaEmCadeia()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(Comprimento + 0.5, 0, 0.8);
        var c = Celula(2 * (Comprimento + 0.5), 0, 1.6);
        var d = Celula(0, 12, 0);

        var r = RowNumbering.Number([a, b, c, d], a.Id, d.Id, Vao);

        // a e b no mesmo azimute (0,8° < 1°); c a 1,6° da primeira do grupo, fora.
        Assert.Equal("F1.1", LetreiroDe(r, a.Id));
        Assert.Equal("F1.2", LetreiroDe(r, b.Id));
        Assert.NotEqual("F1.3", LetreiroDe(r, c.Id));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void MesaGiradaDe180GrausEstaNaMesmaFileira()
    {
        var a = Celula(0, 0, 0);
        // A mesma célula da vizinha, mas percorrida ao contrário (origem no
        // outro canto, direção oposta): cópia girada.
        var b = Celula(2 * Comprimento + 0.5, Fundo, 180);

        var r = RowNumbering.Number([a, b], a.Id, b.Id, Vao);

        Assert.Equal(1, r.RowCount);
        Assert.Equal("F1.1", LetreiroDe(r, a.Id));
        Assert.Equal("F1.2", LetreiroDe(r, b.Id));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void AzimuteDiferenteAbreFileiraPropria()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(Comprimento + 0.5, 0, 0);
        var torta = Celula(2 * Comprimento + 1, 0, 5);
        var d = Celula(0, 12, 0);

        var r = RowNumbering.Number([a, b, torta, d], a.Id, d.Id, Vao);

        Assert.Equal(3, r.RowCount);
        Assert.Equal("F1.1", LetreiroDe(r, a.Id));
        Assert.Equal("F1.2", LetreiroDe(r, b.Id));
        Assert.StartsWith("F2.", LetreiroDe(r, torta.Id));
        Assert.Equal("F3.1", LetreiroDe(r, d.Id));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void AvisaQuandoAMesaIndicadaNaoEAPonta()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(Comprimento + 0.5, 0, 0);
        var c = Celula(2 * (Comprimento + 0.5), 0, 0);
        var d = Celula(0, 12, 0);
        var e = Celula(0, 24, 0);

        // F1.1 no meio da fileira, e a "última" na fileira do meio.
        var r = RowNumbering.Number([a, b, c, d, e], b.Id, d.Id, Vao);

        Assert.Equal(3, r.RowCount);
        Assert.Equal("F1.2", LetreiroDe(r, b.Id));
        Assert.Equal("F2.1", LetreiroDe(r, d.Id));
        Assert.Equal("F3.1", LetreiroDe(r, e.Id));
        Assert.Contains(r.Warnings, w => w.Contains("não está na ponta"));
        Assert.Contains(r.Warnings, w => w.Contains("1 fileira(s) depois dela"));

        // A F1.1 indicada numa fileira que não é a primeira no sentido dado.
        var r2 = RowNumbering.Number([a, d, e], d.Id, e.Id, Vao);

        Assert.Equal("F2.1", LetreiroDe(r2, d.Id));
        Assert.Equal("F1.1", LetreiroDe(r2, a.Id));
        Assert.Contains(r2.Warnings, w => w.Contains("ficou na F2"));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void MesmaFileiraParaAsDuasOrdenaPelaSubida()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(Comprimento + 0.5, 0, 0);
        var acima = Celula(0, 12, 0);

        var r = RowNumbering.Number([a, b, acima], a.Id, b.Id, Vao);

        Assert.Equal(2, r.RowCount);
        Assert.Equal("F1.1", LetreiroDe(r, a.Id));
        Assert.Equal("F2.1", LetreiroDe(r, acima.Id));
        Assert.Contains(r.Warnings, w => w.Contains("mesma fileira"));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void EntradaQueNaoValeERecusada()
    {
        var a = Celula(0, 0, 0);
        var b = Celula(30, 0, 0);
        var repetida = Celula(60, 0, 0, a.Id);

        Assert.Throws<ArgumentException>(() => RowNumbering.Number([], a.Id, a.Id, Vao));
        Assert.Throws<ArgumentException>(() => RowNumbering.Number([a, b], Guid.NewGuid(), b.Id, Vao));
        Assert.Throws<ArgumentException>(() => RowNumbering.Number([a, b], a.Id, Guid.NewGuid(), Vao));
        Assert.Throws<ArgumentException>(() => RowNumbering.Number([a, b, repetida], a.Id, b.Id, Vao));
        Assert.Throws<ArgumentOutOfRangeException>(() => RowNumbering.Number([a, b], a.Id, b.Id, double.NaN));
    }
}
