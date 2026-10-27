using System.Text.Json;
using Clivus.Core;

namespace Clivus.Core.Tests;

public class TermoVisionReturnTests
{
    private static readonly TermoVisionModule Modulo = new(1.134, 2.278, 0.02);

    private static TermoVisionTable Mesa(string nome, int colunas = 13, params (double, double, double)[] cantos) =>
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"), nome,
            cantos.Length > 0 ? cantos : [(711900.123456, 7664350, 600.5), (711914.9, 7664350, 600.5), (711914.9, 7664346, 602.1), (711900.123456, 7664346, 602.1)],
            colunas, 2, Modulo);

    [Fact]
    [Trait("Etapa", "33")]
    public void Corpo_segue_o_contrato()
    {
        var corpo = TermoVisionReturn.Build("1.4.0", "Paradise.dwg", [Mesa("M12")], out var recusadas);
        Assert.Empty(recusadas);

        using var doc = JsonDocument.Parse(corpo);
        var r = doc.RootElement;
        Assert.Equal(1, r.GetProperty("versao").GetInt32());
        Assert.Equal("1.4.0", r.GetProperty("plugin").GetString());
        Assert.Equal("Paradise.dwg", r.GetProperty("desenho").GetString());

        var m = Assert.Single(r.GetProperty("mesas").EnumerateArray().ToList());
        Assert.Equal("11111111-2222-3333-4444-555555555555", m.GetProperty("guid").GetString());
        Assert.Equal("M12", m.GetProperty("nome").GetString());
        Assert.Equal(13, m.GetProperty("colunas").GetInt32());
        Assert.Equal(2, m.GetProperty("fiadas").GetInt32());
        Assert.Equal(1.134, m.GetProperty("modulo").GetProperty("largura_m").GetDouble());
        Assert.Equal(2.278, m.GetProperty("modulo").GetProperty("altura_m").GetDouble());
        Assert.Equal(0.02, m.GetProperty("modulo").GetProperty("espaco_m").GetDouble());

        var cantos = m.GetProperty("cantos").EnumerateArray().Select(c => c.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToList();
        Assert.Equal(4, cantos.Count);
        Assert.Equal([711900.1235, 7664350, 600.5], cantos[0]); // arredondado a 0,1 mm
        Assert.Equal(602.1, cantos[2][2]);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Mesa_que_nao_serve_fica_fora_com_o_motivo()
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);
        var mesas = new[]
        {
            Mesa("boa"),
            Mesa("três cantos", 13, (0, 0, 0), (1, 0, 0), (1, 1, 0)),
            Mesa("NaN", 13, (0, 0, 0), (1, 0, double.NaN), (1, 1, 0), (0, 1, 0)),
            Mesa("vazia", 0),
            Mesa("sem módulo") with { Module = new TermoVisionModule(0, 2, 0) },
        };

        var corpo = TermoVisionReturn.Build("x", "d.dwg", mesas, out var recusadas);

        using var doc = JsonDocument.Parse(corpo);
        Assert.Single(doc.RootElement.GetProperty("mesas").EnumerateArray());
        Assert.Equal(4, recusadas.Count);
        Assert.Contains("três cantos: a mesa não tem quatro cantos.", recusadas);
        Assert.Contains("NaN: um canto da mesa não tem coordenada.", recusadas);
        Assert.Contains("vazia: a mesa não tem módulo.", recusadas);
        Assert.Contains("sem módulo: o módulo da mesa não tem medida.", recusadas);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Sem_mesa_vai_lista_vazia()
    {
        var corpo = TermoVisionReturn.Build("x", "d.dwg", [], out var recusadas);
        using var doc = JsonDocument.Parse(corpo);
        Assert.Empty(doc.RootElement.GetProperty("mesas").EnumerateArray());
        Assert.Empty(recusadas);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Resposta_200_e_lida()
    {
        var r = TermoVisionResult.TryParse("""
            {"gravado":true,"operacao":42,"mesas":2,"sairam":5,"mantidas":0,"nasceram":2,"fora_da_area":1,
             "avisos":["M2: meio fora da área"],"url":"https://app.termovision.tech/mapa"}
            """);

        Assert.NotNull(r);
        Assert.True(r!.Saved);
        Assert.Equal("42", r.Operation);
        Assert.Equal(2, r.Tables);
        Assert.Equal(5, r.Removed);
        Assert.Equal(2, r.Born);
        Assert.Equal(1, r.OutsideArea);
        Assert.Equal(["M2: meio fora da área"], r.Warnings);
        Assert.Equal("https://app.termovision.tech/mapa", r.Url);

        Assert.Null(TermoVisionResult.TryParse("<html>"));
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("{\"detail\": \"token vencido em 02/11\"}", "token vencido em 02/11")]
    [InlineData("{\"detail\": [{\"msg\": \"campo cantos\"}, {\"msg\": \"campo fiadas\"}]}", "campo cantos; campo fiadas")]
    [InlineData("{\"outro\": 1}", null)]
    [InlineData("Internal Server Error", null)]
    public void Detalhe_do_erro_e_lido(string json, string? esperado) =>
        Assert.Equal(esperado, TermoVisionResult.Detail(json));

    [Fact]
    [Trait("Etapa", "33")]
    public void Modulo_infinito_fica_fora_sem_derrubar()
    {
        var mesa = Mesa("inf") with { Module = new TermoVisionModule(double.PositiveInfinity, 2, 0) };
        TermoVisionReturn.Build("x", "d.dwg", [mesa], out var recusadas);
        Assert.Single(recusadas);
    }
}
