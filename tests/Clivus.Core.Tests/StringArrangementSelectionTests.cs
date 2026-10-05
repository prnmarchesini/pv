using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Escolha de mesas em campo e o plano cartesiano (elétrica, 11.2).</summary>
public class StringArrangementSelectionTests
{
    [Fact]
    [Trait("Etapa", "11")]
    public void UmaMesa2VDeVinteEOitoViraUmaMesaDe14x2()
    {
        var mesa = MesasDeString.Mesa("F1.1", 0, 0, 14, 2);

        var ordem = StringFieldTables.Order([mesa], out var problema)!;
        var (arranjo, desenho) = StringFieldTables.Describe(ordem);

        Assert.Null(problema);
        Assert.Equal("14x2", arranjo.ToText());
        Assert.Equal(28, arranjo.ModuleCount);
        Assert.Equal(1.1, desenho.CellWidth, 6);
        Assert.Equal(2.3, desenho.CellHeight, 6);
        Assert.Empty(desenho.Gaps);
        Assert.Equal(15.4, desenho.Size(arranjo).Width, 6);
        Assert.Equal(4.6, desenho.Size(arranjo).Height, 6);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void UmaMesa1VTemUmaFileiraSo()
    {
        var ordem = StringFieldTables.Order([MesasDeString.Mesa("F2.4", 0, 0, 20, 1)], out _)!;
        Assert.Equal("20x1", StringFieldTables.Describe(ordem).Arrangement.ToText());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void DuasMesasDe14AparecemAsDuasComOVaoDeCampo()
    {
        var fileira = MesasDeString.Fileira(3, 0, 0.8, (7, 2), (7, 2));

        // A ordem da seleção não importa: a fileira manda.
        var ordem = StringFieldTables.Order([fileira[1], fileira[0]], out var problema)!;
        var (arranjo, desenho) = StringFieldTables.Describe(ordem);

        Assert.Null(problema);
        Assert.Equal(["F3.1", "F3.2"], ordem.Select(o => o.Table.Label));
        Assert.Equal("7x2;7x2", arranjo.ToText());
        Assert.Equal(0.8, desenho.Gaps.Single(), 6);

        // A primeira célula da segunda mesa começa depois das 7 da primeira e do vão.
        Assert.Equal(7 * 1.1 + 0.8, desenho.CellRect(arranjo, 1, 0, 0).X, 6);
        Assert.Equal(2.3, desenho.CellRect(arranjo, 1, 3, 1).Y, 6);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void AOrdemSegueAFileiraMesmoComAMesaDeMaiorNumeroAntes()
    {
        // F5.2 está à esquerda de F5.1: a referência é a F5.1, e a fileira corre no sentido dela.
        var direita = MesasDeString.Mesa("F5.1", 20, 0, 10, 2);
        var esquerda = MesasDeString.Mesa("F5.2", 0, 0, 14, 2);

        var ordem = StringFieldTables.Order([direita, esquerda], out _)!;

        Assert.Equal(["F5.2", "F5.1"], ordem.Select(o => o.Table.Label));
        Assert.Equal("14x2;10x2", StringFieldTables.Describe(ordem).Arrangement.ToText());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MesaViradaTemColunasEFileirasAoContrario()
    {
        var normal = MesasDeString.Mesa("F1.1", 0, 0, 7, 2);
        // Virada 180°: o canto da borda baixa fica do outro lado, em cima.
        var virada = MesasDeString.Mesa("F1.2", 7 * 1.1 + 0.5 + 7 * 1.1, 2 * 2.3, 7, 2, angulo: Math.PI);

        var ordem = StringFieldTables.Order([normal, virada], out var problema)!;

        Assert.Null(problema);
        Assert.False(ordem[0].Reversed);
        Assert.True(ordem[1].Reversed);
        Assert.Equal((6, 1), ordem[1].ToSketch(0, 0));
        Assert.Equal((0, 0), ordem[1].FromSketch(6, 1));
        Assert.Equal(0.5, StringFieldTables.Describe(ordem).Sketch.Gaps.Single(), 6);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void Mesa1VAoLadoDe2VComAsBordasBaixasAlinhadasEDaMesmaFileira()
    {
        var fileira = MesasDeString.Fileira(4, 0, 0.5, (14, 2), (14, 1));

        var ordem = StringFieldTables.Order(fileira, out var problema);

        Assert.Null(problema);
        Assert.Equal("14x2;14x1", StringFieldTables.Describe(ordem!).Arrangement.ToText());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MesasDeFileirasDiferentesSaoRecusadas()
    {
        var a = MesasDeString.Mesa("F1.1", 0, 0, 14, 2);
        var b = MesasDeString.Mesa("F2.1", 0, 10, 14, 2);
        var torta = MesasDeString.Mesa("F1.2", 20, 0, 14, 2, angulo: 0.3);

        Assert.Null(StringFieldTables.Order([a, b], out var p1));
        Assert.Contains("F2.1", p1);
        Assert.Null(StringFieldTables.Order([a, torta], out var p2));
        Assert.Contains("F1.2", p2);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void SelecaoVaziaEMesaFuradaSaoRecusadas()
    {
        Assert.Null(StringFieldTables.Order([], out var vazio));
        Assert.NotNull(vazio);

        var inteira = MesasDeString.Mesa("F1.1", 0, 0, 14, 2);
        var furada = inteira with { Modules = inteira.Modules.Skip(1).ToList() };

        Assert.Null(StringFieldTables.Order([furada], out var porque));
        Assert.Contains("F1.1", porque);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ODesenhoDoCartesianoVaiEVoltaDoTexto()
    {
        var desenho = new ArrangementSketch(1.134, 2.278, [0.5, 12.25]);

        Assert.Equal("1.134;2.278;0.5;12.25", desenho.ToText());
        Assert.Equal(desenho, ArrangementSketch.Parse(desenho.ToText()));
        Assert.Null(ArrangementSketch.Parse("1.1"));
        Assert.Null(ArrangementSketch.Parse("1.1;0"));
        Assert.Null(ArrangementSketch.Parse("1.1;2;-1"));
        Assert.Null(ArrangementSketch.Parse("a;b"));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OTipoComMesasVaiEVoltaDoDesenhoNoFormato2EOFormato1AindaELido()
    {
        var biblioteca = new StringLibrary([]);
        var ordem = StringFieldTables.Order(MesasDeString.Fileira(1, 0, 0.6, (7, 2), (7, 2)), out _)!;
        var (arranjo, desenho) = StringFieldTables.Describe(ordem);
        var tipo = biblioteca.Add(StringArrangement.Empty);
        Assert.Null(biblioteca.SetArrangement(tipo.Id, arranjo, desenho));
        biblioteca.Add(StringArrangement.Empty);

        var texto = StringTypeRecords.Write(biblioteca.Types);
        var lido = StringTypeRecords.Read(texto, "de tipos de string");

        Assert.Null(lido.Problem);
        Assert.Equal(biblioteca.Types, lido.Items);
        Assert.Equal(0.6, lido.Items[0].Sketch!.Gaps.Single(), 6);

        // O que o 11.1 gravou (formato 1, três campos) continua sendo lido.
        var antigo = RecordTable.Write(1, 3, [biblioteca.Find(tipo.Id)!], t => t.ToFields().Take(3).ToList());
        var lidoAntigo = StringTypeRecords.Read(antigo, "de tipos de string");
        Assert.Null(lidoAntigo.Problem);
        Assert.Equal("7x2;7x2", lidoAntigo.Items.Single().Arrangement.ToText());
        Assert.Null(lidoAntigo.Items.Single().Sketch);
        Assert.Equal(0.5, lidoAntigo.Items.Single().SketchOrDefault.Gaps.Single(), 6);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void TrocarAsMesasDeUmTipoQueSumiuOuSemMesasERecusado()
    {
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(StringArrangement.Empty);
        var desenho = new ArrangementSketch(1, 2, []);

        Assert.NotNull(biblioteca.SetArrangement(Guid.NewGuid(), new([new ArrangementTable(14, 2)]), desenho));
        Assert.NotNull(biblioteca.SetArrangement(tipo.Id, StringArrangement.Empty, desenho));
        Assert.Null(biblioteca.SetArrangement(tipo.Id, new([new ArrangementTable(14, 2)]), desenho));
        Assert.Equal("14x2", biblioteca.Find(tipo.Id)!.Arrangement.ToText());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ACelulaDoModuloSaiDaMesa()
    {
        var mesa = MesasDeString.Mesa("F1.1", 0, 0, 3, 2);
        Assert.Equal(3, mesa.Columns);
        Assert.Equal(2, mesa.Rows);
        Assert.NotNull(mesa.ModuleAt(2, 1));
        Assert.Null(mesa.ModuleAt(3, 0));
        Assert.Equal(new Point3(1, 0, 0), mesa.Direction);
    }
}
