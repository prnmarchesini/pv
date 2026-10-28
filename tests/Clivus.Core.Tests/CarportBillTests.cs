namespace Clivus.Core.Tests;

/// <summary>A lista de material do carport (34.4): sai das mesmas peças do desenho.</summary>
public class CarportBillTests
{
    private static CarportProfile Padrao => CarportProfile.Reference;

    private static CarportRowResult Fila(int vaos, Func<double, double, double?>? chao = null) =>
        CarportRow.Build(Padrao, 0, 0, 1, 0, true, vaos, chao ?? ((_, _) => 0), out _)!;

    private static CarportBillResult Lista(params CarportRowResult[] filas) =>
        CarportBill.Build(filas.Select((f, i) => new CarportBillRow($"Fila {i + 1}", Padrao, f)).ToList());

    private static int Quantos(CarportBillResult l, string item) => l.Lines.Where(x => x.Item == item).Sum(x => x.Quantity);

    [Fact]
    [Trait("Etapa", "34")]
    public void TresVaosEmChaoPlano()
    {
        var l = Lista(Fila(3));

        Assert.Equal(1, l.Rows);
        Assert.Equal(3, l.Bays);
        Assert.Equal(4, l.Portals);
        Assert.Equal(45, l.Modules);
        Assert.Equal(27.0, l.Kwp, 9);

        Assert.Equal(8, Quantos(l, "Pilar"));
        Assert.Equal(4, Quantos(l, "Viga"));
        Assert.Equal(8, Quantos(l, "Mão francesa"));
        Assert.Equal(18, Quantos(l, "Terça"));              // 6 linhas × 3 vãos: emenda no pórtico
        Assert.Equal(8, Quantos(l, "Chapa de base"));
        Assert.Equal(8, Quantos(l, "Bloco de base"));
        Assert.Equal(45, Quantos(l, "Módulo"));

        // Em chão plano há dois comprimentos de pilar (o do lado alto e o do baixo), 4 de cada.
        var pilares = l.Lines.Where(x => x.Item == "Pilar").ToList();
        Assert.Equal(2, pilares.Count);
        Assert.All(pilares, p => Assert.Equal(4, p.Quantity));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void GramposPelaRegraDeSempre()
    {
        var l = Lista(Fila(3));

        // 6 linhas de terça, 15 módulos na fila: 14 intermediários e 2 de ponta por linha.
        Assert.Equal(6 * 14, Quantos(l, "Grampo intermediário de módulo"));
        Assert.Equal(6 * 2, Quantos(l, "Grampo de ponta de módulo"));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void ASomaBateComAsPecasDesenhadas()
    {
        var fila = Fila(4, chao: (x, y) => 0.03 * x - 0.05 * y);
        var l = Lista(fila);

        double Desenhado(CarportPart parte) =>
            fila.Portals.SelectMany(p => p.Bars).Concat(fila.Bays.SelectMany(b => b.Purlins)).Where(b => b.Part == parte).Sum(b => b.Length);

        // O corte arredondado para cima ao centímetro: a lista nunca fica abaixo do
        // desenhado e passa dele menos de um centímetro por peça.
        foreach (var (parte, nome, pecas) in new[] { (CarportPart.Pillar, "Pilar", 10), (CarportPart.Purlin, "Terça", 24), (CarportPart.Beam, "Viga", 5) })
        {
            var lista = l.Lines.Where(x => x.Item == nome).Sum(x => x.TotalLength!.Value);
            Assert.InRange(lista - Desenhado(parte), -1e-6, pecas * 0.01);
        }
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void NoDesnivelCadaPilarAparecePeloComprimento()
    {
        // Só ao longo da fila, o pórtico inteiro sobe e os pilares não mudam: continuam dois comprimentos.
        Assert.Equal(2, Lista(Fila(2, chao: (x, _) => 0.04 * x)).Lines.Count(x => x.Item == "Pilar"));

        // Com o desnível entre os dois pilares mudando de um pórtico para o outro, cada um tem o seu.
        var l = Lista(Fila(2, chao: (x, y) => 0.02 * x * y));

        Assert.Equal(6, l.Pillars.Count);
        Assert.True(l.Lines.Count(x => x.Item == "Pilar") > 2, "terreno torcido dá mais de dois comprimentos");
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, l.Pillars.Select(p => p.Portal));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void DuasFilasSomam()
    {
        var l = Lista(Fila(2), Fila(3));

        Assert.Equal(2, l.Rows);
        Assert.Equal(5, l.Bays);
        Assert.Equal(7, l.Portals);
        Assert.Equal(75, Quantos(l, "Módulo"));
        Assert.Equal(6 * 2 * 2, Quantos(l, "Grampo de ponta de módulo"));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OExcelTemAsTresAbas()
    {
        var xlsx = CarportBill.ToXlsx(Lista(Fila(3)));

        Assert.True(xlsx.HasSheet("Carport — resumo"));
        Assert.True(xlsx.HasSheet("Carport — peças"));
        Assert.True(xlsx.HasSheet("Carport — pilares"));
        Assert.True(xlsx.ToBytes().Length > 1000);
    }
}
