using System.Text.Json;
using System.Text.RegularExpressions;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O 3D do 42.2 (Renan, 31/10/2026): o sol com sombra calculado no navegador
/// (a cena leva o lugar), os pilares modelados (a seção e o giro do bloco), o
/// quadro do resumo (mesas, módulos e kWp) e o logo do Clivus na página local.
/// </summary>
public class Viewer3DSunAndSummaryTests
{
    private static Scene3D Cena() => new(
        "Usina",
        Viewer3DPage.SampleTerrain(314_000, 7_456_000, 314_100, 7_456_050, (_, _) => 700, maxCells: 50),
        [new Scene3DFace([new(314_010, 7_456_010, 702), new(314_012, 7_456_010, 702), new(314_012, 7_456_011, 702.5), new(314_010, 7_456_011, 702.5)], new RgbColor(30, 60, 140))],
        [new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699)],
        [new Scene3DTree(314_050, 7_456_020, 702, TreeSpec.Default)],
        []);

    private static JsonElement Ler(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>
    /// Com o lugar, a cena leva "local" (latitude, longitude e fuso); sem ele,
    /// o campo não vai e a cena é a de antes. No envio ao servidor o lugar
    /// vai a 0,1°, como a origem vai zerada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void ACenaLevaOLugarSoQuandoTem()
    {
        var sem = Cena();
        Assert.False(Ler(Viewer3DPage.Json(sem)).TryGetProperty("local", out _));
        Assert.False(Ler(Viewer3DPage.PublishBody(sem, "0.1.0", "x")).GetProperty("cena").TryGetProperty("local", out _));

        var com = sem with { Location = new Scene3DLocation(-23.01234567, -46.84567891, -3) };
        var local = Ler(Viewer3DPage.Json(com)).GetProperty("local");
        Assert.Equal(-23.0123457, local.GetProperty("latitude").GetDouble(), 9);
        Assert.Equal(-46.8456789, local.GetProperty("longitude").GetDouble(), 9);
        Assert.Equal(-3, local.GetProperty("fuso").GetDouble(), 9);

        // O corpo do envio: a 0,1° (uns 11 km), o fuso inteiro.
        var publicado = Ler(Viewer3DPage.PublishBody(com, "0.1.0", "x")).GetProperty("cena").GetProperty("local");
        Assert.Equal("{\"latitude\":-23,\"longitude\":-46.8,\"fuso\":-3}", publicado.GetRawText());
        Assert.Equal(1, Viewer3DPage.PublishedLocationDecimals);

        // O fuso de meia hora e o −0 do arredondamento.
        var meia = Ler(Viewer3DPage.PublishBody(sem with { Location = new Scene3DLocation(0.04, -0.04, 5.5) }, "0.1.0", "x")).GetProperty("cena").GetProperty("local");
        Assert.Equal("{\"latitude\":0,\"longitude\":0,\"fuso\":5.5}", meia.GetRawText());

        // Lugar que o SolarCalculator recusaria não vai: o servidor responderia 400.
        foreach (var torto in new[] { new Scene3DLocation(double.NaN, -46, -3), new Scene3DLocation(-91, -46, -3), new Scene3DLocation(-23, 181, -3), new Scene3DLocation(-23, -46, 15) })
        {
            Assert.False(torto.IsValid);
            Assert.False(Ler(Viewer3DPage.Json(sem with { Location = torto })).TryGetProperty("local", out _));
        }

        // A cena leva o mesmo lugar com que o C# calcula o sol (o limite é o dele).
        Assert.True(new Scene3DLocation(-90, 180, 14).IsValid);
        _ = SolarCalculator.Compute(-90, 180, new DateTime(2026, 6, 21, 12, 0, 0), 14);
    }

    /// <summary>
    /// O pilar com a seção leva largura, profundidade e giro (sete números); sem
    /// ela, os quatro de antes, e o visualizador desenha a padrão.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OPilarLevaASecaoEOGiroQuandoTem()
    {
        var cena = Cena() with
        {
            Pillars =
            [
                new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699),
                new Scene3DPillar(314_013, 7_456_010.5, 701.6, 699.2, new Scene3DPillarSection(0.15, 0.095, 0.5235987756)),
                new Scene3DPillar(314_015, 7_456_010.5, 701.7, 699.4, new Scene3DPillarSection(0, 0.1, 0)),
                new Scene3DPillar(314_017, 7_456_010.5, 701.7, 699.4, new Scene3DPillarSection(0.1, 0.1, double.NaN)),
            ],
        };

        var pilares = Ler(Viewer3DPage.Json(cena)).GetProperty("pilares");

        Assert.Equal(4, pilares[0].GetArrayLength());
        Assert.Equal("[-37,-14.5,1.6,-0.8,0.15,0.095,0.5236]", pilares[1].GetRawText());

        // Seção que não serve (largura zero, giro NaN): fica a padrão.
        Assert.Equal(4, pilares[2].GetArrayLength());
        Assert.Equal(4, pilares[3].GetArrayLength());

        Assert.False(new Scene3DPillarSection(10.001, 0.1, 0).IsValid);
        Assert.True(new Scene3DPillarSection(Scene3DPillarSection.MaxSize, 0.1, -7).IsValid);

        // Abaixo de 1 mm o arredondamento ao milímetro daria 0, que o servidor
        // recusa (a publicação inteira): fica a padrão. 1 mm vai como 0.001.
        var fina = cena with { Pillars = [new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699, new Scene3DPillarSection(0.0004, 0.1, 0)), new Scene3DPillar(314_013, 7_456_010.5, 701.5, 699, new Scene3DPillarSection(Scene3DPillarSection.MinSize, 0.1, 0))] };
        var finas = Ler(Viewer3DPage.Json(fina)).GetProperty("pilares");
        Assert.Equal(4, finas[0].GetArrayLength());
        Assert.Equal(0.001, finas[1][4].GetDouble(), 9);

        // O corpo do envio leva a mesma seção.
        var corpo = Ler(Viewer3DPage.PublishBody(cena, "0.1.0", "x")).GetProperty("cena").GetProperty("pilares");
        Assert.Equal(pilares.GetRawText(), corpo.GetRawText());
    }

    private static AreaTableInput Mesa(double cx, double cy, string tipo, int modulos)
    {
        var identidade = new TableIdentity(Guid.NewGuid(), "F1.1", 100, 100, 0.35, false, null, false, null, 600, ProfileName: tipo);
        IReadOnlyList<Point3> contorno = [new(cx - 5, cy - 2, 100), new(cx + 5, cy - 2, 100), new(cx + 5, cy + 2, 100), new(cx - 5, cy + 2, 100)];
        return new AreaTableInput(new CountedTable(identidade, 1, modulos, [3.0, 3.0], 0), contorno, 2.5);
    }

    private static AreaInput Area(string nome, double x0, double y0, double x1, double y1) =>
        new(Guid.NewGuid(), nome, [new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0)]);

    /// <summary>
    /// O resumo é a conta da janela Áreas: o total, por área (com a linha das
    /// mesas fora das áreas, marcada) e por tipo de mesa; sem área no
    /// desenho, só o total e os tipos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OResumoLevaOTotalAsAreasEOsTipos()
    {
        var mesas = new List<AreaTableInput> { Mesa(20, 20, "GC800", 28), Mesa(20, 40, "GC800", 28), Mesa(60, 20, "Mesa 14", 14), Mesa(500, 500, "GC800", 28) };
        var resumo = Scene3DSummary.From(AreaSummary.Build([Area("Norte", 0, 0, 100, 100)], mesas, 550));

        Assert.Equal(new Scene3DSummaryRow(Tr.T("Total"), 4, 98, 58.8), resumo.Total with { PowerKwp = Math.Round(resumo.Total.PowerKwp, 9) });
        Assert.Equal(["Norte", Tr.T("Fora das áreas")], resumo.Areas.Select(a => a.Name));
        Assert.Equal([false, true], resumo.Areas.Select(a => a.Outside));
        Assert.Equal((3, 70), (resumo.Areas[0].Tables, resumo.Areas[0].Modules));
        Assert.Equal(["GC800", "Mesa 14"], resumo.Types.Select(t => t.Name));
        Assert.Equal((3, 84), (resumo.Types[0].Tables, resumo.Types[0].Modules));

        var json = Ler(Viewer3DPage.Json(Cena() with { Summary = resumo })).GetProperty("resumo");
        Assert.Equal("{\"mesas\":4,\"modulos\":98,\"kwp\":58.8}", json.GetProperty("total").GetRawText());
        Assert.Equal("{\"nome\":\"Norte\",\"mesas\":3,\"modulos\":70,\"kwp\":42}", json.GetProperty("areas")[0].GetRawText());
        Assert.True(json.GetProperty("areas")[1].GetProperty("fora").GetBoolean());
        Assert.False(json.GetProperty("areas")[0].TryGetProperty("fora", out _));
        Assert.Equal("{\"nome\":\"Mesa 14\",\"mesas\":1,\"modulos\":14,\"kwp\":8.4}", json.GetProperty("tipos")[1].GetRawText());

        // Sem área no desenho: as mesas todas seriam "fora das áreas", o que
        // repete o total. Vai só o total e os tipos.
        var semArea = Scene3DSummary.From(AreaSummary.Build([], mesas, 550));
        Assert.Empty(semArea.Areas);
        Assert.Equal(4, semArea.Total.Tables);
        Assert.Equal("[]", Ler(Viewer3DPage.Json(Cena() with { Summary = semArea })).GetProperty("resumo").GetProperty("areas").GetRawText());

        // Linha que não serve fica de fora; sem um total que serve, nada de resumo.
        var torto = resumo with { Types = [.. resumo.Types, new Scene3DSummaryRow("x", -1, 0, 0), new Scene3DSummaryRow("y", 1, 1, double.NaN), new Scene3DSummaryRow("z", 1, 1, 2e9)] };
        Assert.True(new Scene3DSummaryRow("w", 1_000_000_000, 1_000_000_000, Scene3DSummaryRow.MaxValue).IsValid);
        Assert.Equal(2, Ler(Viewer3DPage.Json(Cena() with { Summary = torto })).GetProperty("resumo").GetProperty("tipos").GetArrayLength());
        Assert.False(Ler(Viewer3DPage.Json(Cena() with { Summary = resumo with { Total = resumo.Total with { PowerKwp = -1 } } })).TryGetProperty("resumo", out _));
        Assert.False(Ler(Viewer3DPage.Json(Cena())).TryGetProperty("resumo", out _));
    }

    /// <summary>
    /// A página local tem o sol (os controles, o código e a sombra), os pilares
    /// em caixas por instância, o quadro do resumo e o logo do Clivus, e
    /// continua um arquivo só, com os três scripts de antes e sem a
    /// propaganda da página pública.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void APaginaLocalTemOSolOsPilaresOResumoEOLogo()
    {
        var cena = Cena() with { Location = new Scene3DLocation(-23, -46.8, -3) };
        var html = Viewer3DPage.Html(cena);

        foreach (var id in new[] { "solPainel", "solSombras", "solData", "solHora", "solHoraTexto", "solTocar", "solTexto", "resumoQuadro", "itemResumo", "vResumo", "marca" })
            Assert.Contains($"id=\"{id}\"", html, StringComparison.Ordinal);

        for (var i = 0; i < 4; i++) Assert.Contains($"data-marco=\"{i}\"", html, StringComparison.Ordinal);

        // O sol e o resumo vêm escondidos: só o JS mostra, com o lugar e com o resumo na cena.
        Assert.Matches("<div id=\"solPainel\" hidden", html);
        Assert.Matches("<div id=\"resumoQuadro\" class=\"caixa\" hidden", html);
        Assert.Contains("<label id=\"itemResumo\" hidden>", html, StringComparison.Ordinal);

        // O código: o sol do NOAA, a luz com sombra e os pilares por instância.
        Assert.Contains("raiz.ClivusSol = api", html, StringComparison.Ordinal);
        Assert.Contains("raiz.ClivusResumo = api", html, StringComparison.Ordinal);
        Assert.Contains("ClivusSol.montar(", html, StringComparison.Ordinal);
        Assert.Contains("ClivusResumo.montar(D", html, StringComparison.Ordinal);
        Assert.Contains("const L = o.D && o.D.local;", html, StringComparison.Ordinal);
        Assert.Contains("luz.castShadow", html, StringComparison.Ordinal);
        Assert.Contains("render.shadowMap.enabled = true", html, StringComparison.Ordinal);
        Assert.Contains("THREE.PCFSoftShadowMap", html, StringComparison.Ordinal);
        Assert.Contains("new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1)", html, StringComparison.Ordinal);
        Assert.Contains("\"local\":{\"latitude\":-23,", html, StringComparison.Ordinal);

        // Os textos vêm do plugin, no idioma dele.
        Assert.Contains("data-abaixo=\"Sol abaixo do horizonte\"", html, StringComparison.Ordinal);
        Assert.Contains("data-posicao=\"Altura do sol {0}&#176;, azimute {1}&#176;\"", html, StringComparison.Ordinal);

        // O logo do Clivus, embutido; a propaganda é só da página pública.
        Assert.Contains("<div id=\"marca\" aria-hidden=\"true\"><svg xmlns=\"http://www.w3.org/2000/svg\"", html, StringComparison.Ordinal);
        Assert.Contains("fill=\"#F4A51C\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("propaganda", html, StringComparison.OrdinalIgnoreCase);

        // Continua um arquivo só: os três scripts, nada buscado na internet, nenhuma marca sobrando.
        Assert.Equal(3, html.Split("</script>").Length - 1);
        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", html.Replace("${", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>Um nome de área ou de tipo com "{{SOL}}" no resumo não vira o lugar onde o script entra.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void UmNomeNoResumoNaoViraLugarDeScript()
    {
        var resumo = new Scene3DSummary(new Scene3DSummaryRow("Total", 1, 28, 15.4), [new Scene3DSummaryRow("{{SOL}}</script>", 1, 28, 15.4)], [new Scene3DSummaryRow("{{TITULO}}", 1, 28, 15.4)]);
        var html = Viewer3DPage.Html(Cena() with { Title = "Usina", Summary = resumo });

        Assert.Single(Regex.Matches(html, "raiz.ClivusSol = api"));
        Assert.Equal(3, html.Split("</script>").Length - 1);
        Assert.Contains("\"nome\":\"{\\u007bSOL}}\\u003c/script>\"", html, StringComparison.Ordinal);
        Assert.Contains("\"nome\":\"{\\u007bTITULO}}\"", html, StringComparison.Ordinal);

        // E o JSON da página continua o mesmo texto.
        var json = Regex.Match(html, "const D = (\\{.*?\\});\\r?\\n", RegexOptions.Singleline).Groups[1].Value.Replace("<\\/", "</", StringComparison.Ordinal);
        Assert.Equal("{{SOL}}</script>", Ler(json).GetProperty("resumo").GetProperty("areas")[0].GetProperty("nome").GetString());
    }

    /// <summary>
    /// A tabela que confere o sol do navegador (tests/dados/sol-referencia.json)
    /// é o <see cref="SolarCalculator"/> de hoje, instante por instante. O
    /// tools/conferir-sol.js (o rodar-testes.ps1 roda quando há node) confere
    /// o sol.js contra ela: as duas pontas juntas pegam o C# e o JS que se
    /// separam. Mudou o C# de propósito: a tabela nova sai no diretório do
    /// teste, para copiar, e o conferir-sol.js roda de novo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OSolDoCSharpBateComATabelaDoNavegador()
    {
        using var tabela = JsonDocument.Parse(File.ReadAllText(Repositorio.Caminho("tests", "dados", "sol-referencia.json")));
        var linhas = tabela.RootElement.GetProperty("instantes").EnumerateArray().Select(l => l.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToList();
        Assert.True(linhas.Count >= 40, $"a tabela tem só {linhas.Count} instantes");

        var nova = new List<string>();
        var diferentes = 0;

        foreach (var l in linhas)
        {
            var quando = new DateTime((int)l[3], (int)l[4], (int)l[5]).AddMinutes(l[6]);
            var p = SolarCalculator.Compute(l[0], l[1], quando, l[2]);
            double[] calculado = [p.AzimuthDegrees, p.ElevationDegrees, p.DeclinationDegrees, p.EquationOfTimeMinutes];

            if (calculado.Where((v, i) => Math.Abs(v - l[7 + i]) > 1e-9).Any()) diferentes++;
            nova.Add("    [" + string.Join(", ", l.Take(7).Concat(calculado).Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]");
        }

        if (diferentes > 0)
        {
            var arquivo = Path.Combine(AppContext.BaseDirectory, "sol-referencia.json");
            File.WriteAllText(arquivo, "{\n  \"descricao\": " + tabela.RootElement.GetProperty("descricao").GetRawText() + ",\n  \"instantes\": [\n" + string.Join(",\n", nova) + "\n  ]\n}\n");
            Assert.Fail($"{diferentes} instante(s) da tabela não são o SolarCalculator de hoje. Se o C# mudou de propósito, a tabela nova está em {arquivo}: copie para tests/dados e rode node tools/conferir-sol.js.");
        }
    }

    /// <summary>
    /// O sol do navegador é o porte do <see cref="SolarCalculator"/>: cada
    /// constante do NOAA (os números com casa decimal e os inteiros de três
    /// algarismos ou mais) que o C# usa aparece no sol.js. Pega a constante
    /// trocada de um lado só, mesmo sem o node para rodar o conferir-sol.js.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OSolDoNavegadorUsaAsConstantesDoNoaa()
    {
        var csharp = File.ReadAllText(Repositorio.Caminho("src", "Clivus.Core", "SolarPosition.cs"));
        var js = File.ReadAllText(Repositorio.Caminho("src", "Clivus.Core", "Visualizador3D", "sol.js"));

        var conta = csharp[csharp.IndexOf("public static SunPosition Compute", StringComparison.Ordinal)..];
        var constantes = Regex.Matches(conta, @"(?<![\w.])(\d+\.\d+|\d{3,}|\d+e-\d+)(?![\w.])")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        Assert.True(constantes.Count > 40, $"poucas constantes achadas no C#: {constantes.Count}");

        var faltam = constantes.Where(c => !Regex.IsMatch(js, @"(?<![\w.])" + Regex.Escape(c) + @"(?![\w.])")).ToList();
        Assert.True(faltam.Count == 0, "constantes do C# que não estão no sol.js: " + string.Join(", ", faltam));
    }
}
