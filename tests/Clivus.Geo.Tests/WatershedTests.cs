using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>Passo 30.1: as bacias de contribuição de cada canaleta.</summary>
public class WatershedTests
{
    private static FlowTargets Canaletas(params double[] xs) =>
        new(xs.Select(x => (IReadOnlyList<Point3>)[new Point3(x, -10, 0), new Point3(x, 210, 0)]).ToList());

    [Fact]
    [Trait("Etapa", "30")]
    public void NaRampaComCanaletaNoMeioAMetadeDeCimaVaiParaElaEADeBaixoSai()
    {
        var grade = Watershed.Compute(TerrenoDeDrenagem.Rampa(), [], Canaletas(150), 2);
        var resumo = Assert.Single(Watershed.Summaries(grade));

        // Centros em x = 1, 3, ..., 149: 75 colunas × 100 linhas × 4 m².
        Assert.Equal(0, resumo.Target);
        Assert.Equal(30_000, resumo.Area, 6);

        // O maior caminho: de x = 1 até a canaleta, 5 % de queda.
        Assert.Equal(149, resumo.LongestPath, 6);
        Assert.Equal(0.05 * 149, resumo.Drop, 6);
        Assert.Equal(5, resumo.SlopePercent, 6);

        // A leste da canaleta a água sai do terreno: sem destino, com o motivo.
        var semDestino = grade.Owner.Count(o => o == Watershed.NoDestination);
        Assert.Equal(25 * 100, semDestino);
        Assert.All(Enumerable.Range(0, grade.Owner.Length).Where(k => grade.Owner[k] == Watershed.NoDestination), k => Assert.Equal(FlowStop.OutsideTerrain, grade.Reason[k]));
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void NoTelhadoACumeeiraDivideAsBacias()
    {
        var grade = Watershed.Compute(TerrenoDeDrenagem.Telhado(), [], Canaletas(50, 150), 2);
        var resumos = Watershed.Summaries(grade);

        Assert.Equal(2, resumos.Count);
        Assert.Equal(25 * 100 * 4, resumos[0].Area, 6);
        Assert.Equal(25 * 100 * 4, resumos[1].Area, 6);

        // Tudo somado: bacias + sem destino = analisado.
        Assert.Equal(grade.AnalyzedCount, resumos.Sum(r => r.Cells) + grade.Owner.Count(o => o == Watershed.NoDestination));
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void AproveitarOCaminhoNaoMudaAResposta()
    {
        var tin = TerrenoDeDrenagem.Vale();
        var alvos = new FlowTargets([[new Point3(60, 150, 0), new Point3(140, 150, 0)], [new Point3(100, 60, 0), new Point3(130, 30, 0)]]);

        var rapido = Watershed.Compute(tin, [], alvos, 4);
        var lento = Watershed.Compute(tin, [], alvos, 4, reuse: false);

        var diferentes = Enumerable.Range(0, rapido.Owner.Length).Count(k => rapido.Owner[k] != lento.Owner[k]);
        Assert.True(diferentes <= rapido.AnalyzedCount / 100, $"{diferentes} célula(s) com dono diferente");

        var (a, b) = (Watershed.Summaries(rapido), Watershed.Summaries(lento));
        Assert.Equal(b.Count, a.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(b[i].Area, a[i].Area, b[i].Area * 0.02);
            Assert.Equal(b[i].LongestPath, a[i].LongestPath, b[i].LongestPath * 0.03);
        }
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void ARegiaoLimitaAsCelulas()
    {
        IReadOnlyList<Point3> quadrado = [new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), new(0, 100, 0)];
        var grade = Watershed.Compute(TerrenoDeDrenagem.Rampa(), [quadrado], Canaletas(150), 2);

        Assert.Equal(50 * 50, grade.AnalyzedCount);
        var resumo = Assert.Single(Watershed.Summaries(grade));
        Assert.Equal(10_000, resumo.Area, 6);

        // A água da região anda até a canaleta fora dela: o caminho conta inteiro.
        Assert.Equal(149, resumo.LongestPath, 6);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void FundoDeBaciaFechadaFicaSemDestinoComMotivo()
    {
        var grade = Watershed.Compute(TerrenoDeDrenagem.Funil(), [], new FlowTargets([]), 5);

        Assert.Empty(Watershed.Summaries(grade));
        Assert.True(Enumerable.Range(0, grade.Owner.Length).Count(k => grade.Owner[k] == Watershed.NoDestination && grade.Reason[k] == FlowStop.Sink) > grade.AnalyzedCount / 2);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void AjusteManualMandaNoDono()
    {
        // Na rampa, a faixa leste (sem destino) é mandada à mão para a canaleta 0.
        IReadOnlyList<Point3> faixa = [new(160, 0, 0), new(200, 0, 0), new(200, 200, 0), new(160, 200, 0)];
        var grade = Watershed.Compute(TerrenoDeDrenagem.Rampa(), [], Canaletas(150), 2, manual: [(faixa, 0)]);

        var resumo = Assert.Single(Watershed.Summaries(grade));
        Assert.True(resumo.HasManual);
        Assert.Equal(30_000 + 20 * 100 * 4, resumo.Area, 6);

        // O caminho das células manuais não foi para a canaleta: não entra no L.
        Assert.Equal(149, resumo.LongestPath, 6);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void CelulaZeroERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Watershed.Compute(TerrenoDeDrenagem.Rampa(), [], Canaletas(150), 0));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void CadaCelulaSabeOndeEntraNaCanaleta()
    {
        // Rampa para o leste, canaleta norte-sul em x = 150: a água de cada
        // célula entra na canaleta na altura (y) dela.
        var grade = Watershed.Compute(TerrenoDeDrenagem.Rampa(), [], Canaletas(150), 4);

        var dela = Enumerable.Range(0, grade.Owner.Length).Where(k => grade.Owner[k] == 0).ToList();
        Assert.NotEmpty(dela);
        Assert.All(dela, k =>
        {
            Assert.Equal(150, grade.HitX[k], 6);
            Assert.Equal(grade.Center(k % grade.Columns, k / grade.Columns).Y, grade.HitY[k], 6);
        });

        Assert.All(Enumerable.Range(0, grade.Owner.Length).Where(k => grade.Owner[k] < 0), k => Assert.True(double.IsNaN(grade.HitX[k])));
    }
}
