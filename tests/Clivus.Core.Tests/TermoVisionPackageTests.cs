using System.IO.Compression;
using System.Text;
using Clivus.Core;

namespace Clivus.Core.Tests;

public class TermoVisionPackageTests
{
    // Grade 3 × 2 de passo 2 m, linha 0 ao sul; o nó (1, 1) sem dado.
    private const string Json = """
        {"versao":1,"servidor":"http://127.0.0.1:8731","envio":"cv_t1",
         "usina":{"id":"u","nome":"Paradise"},"area":{"id":"a","nome":"Área 1"},
         "crs":{"epsg":31982,"nome":"SIRGAS 2000 / UTM zone 22S","zona":22,"hemisferio":"S"},
         "geolocalizacao":{"lat":-21.1,"lon":-48.9},
         "area_utm":[[1000,2000],[1004,2000],[1004,2002],[1000,2002],[1000,2000]],
         "terreno":{"fonte":"dtm","passo_m":2.0,"e0":1000.0,"n0":2000.0,"colunas":3,"linhas":2,
                    "z":[10.0,11.0,12.0, 20.0,null,22.0]},
         "orto":{"arquivo":"orto.jpg","mundo":"orto.jgw","px_m":0.05},
         "objetos":[{"nome":"Orto 3","altura_m":9.0,"chao_m":612.3,"contorno":[[0,0],[2,0],[2,2],[0,2]]},
                    {"nome":"sem altura","altura_m":0,"chao_m":1,"contorno":[[0,0],[1,0],[1,1]]}],
         "projeto":{"modulo":{"largura_m":1.134,"altura_m":2.278,"espaco_m":0.02}}}
        """;

    private const string Mundo = "0.050000\n0.0\n0.0\n-0.050000\n711876.0250\n7664394.9750\n";

    private static MemoryStream Zip(params (string Nome, byte[] Bytes)[] arquivos)
    {
        var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nome, bytes) in arquivos)
            {
                using var s = zip.CreateEntry(nome).Open();
                s.Write(bytes);
            }
        memoria.Position = 0;
        return memoria;
    }

    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    private static TermoVisionPackage Ler(MemoryStream zip)
    {
        Assert.True(TermoVisionPackage.TryRead(zip, out var p, out var erro), erro);
        return p!;
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Grade_tem_linha_zero_ao_sul_e_null_fora()
    {
        var p = Ler(Zip(("clivus.json", B(Json))));

        Assert.Equal(5, p.Terrain.NodeCount);
        Assert.Equal(12.0, p.Terrain.At(2, 0));
        Assert.Equal(20.0, p.Terrain.At(0, 1));
        Assert.Null(p.Terrain.At(1, 1));
        Assert.Null(p.Terrain.At(3, 0));

        var nos = p.Terrain.Nodes().ToList();
        Assert.Equal((1000.0, 2000.0, 10.0), nos[0]);
        Assert.Equal((1004.0, 2000.0, 12.0), nos[2]);
        Assert.Equal((1000.0, 2002.0, 20.0), nos[3]);
        Assert.Equal((1004.0, 2002.0, 22.0), nos[4]);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Cabecalho_area_objetos_e_modulo_sao_lidos()
    {
        var p = Ler(Zip(("clivus.json", B(Json))));

        Assert.Equal("cv_t1", p.Shipment);
        Assert.Equal("Paradise", p.PlantName);
        Assert.Equal("Área 1", p.AreaName);
        Assert.Equal(31982, p.Crs.Epsg);
        Assert.Equal(22, p.Crs.Zone);
        Assert.Equal(-21.1, p.Location!.Latitude);
        Assert.Equal(4, p.Area.Count); // o vértice repetido no fim sai

        var o = Assert.Single(p.Objects); // o sem altura fica fora
        Assert.Equal("Orto 3", o.Name);
        Assert.Equal(612.3, o.GroundZ);
        Assert.Equal(4.0, o.PlanArea, 9);
        Assert.Equal((1.0, 1.0), o.Centroid);

        Assert.Equal(new TermoVisionModule(1.134, 2.278, 0.02), p.Module);
        Assert.Null(p.Ortho); // o json fala do orto mas os arquivos não vieram
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Orto_fica_no_lugar_pelo_world_file()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var p = Ler(Zip(("clivus.json", B(Json)), ("orto.jpg", jpeg), ("orto.jgw", B(Mundo))));

        Assert.NotNull(p.Ortho);
        Assert.Equal(jpeg, p.Ortho!.Jpeg);
        Assert.Equal(0.05, p.Ortho.PixelSize);

        // 100 × 40 pixels: o canto de cima à esquerda é o centro do primeiro pixel menos meio pixel.
        var (origem, u, v) = p.Ortho.World.Placement(100, 40);
        Assert.Equal(711876.0, origem.X, 6);
        Assert.Equal(7664395.0 - 2.0, origem.Y, 6);
        Assert.Equal(5.0, u.X, 9);
        Assert.Equal(0.0, u.Y, 9);
        Assert.Equal(0.0, v.X, 9);
        Assert.Equal(2.0, v.Y, 9);
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("1\n0\n0\n-1\n5", false)]
    [InlineData("0\n0\n0\n0\n5\n6", false)]
    [InlineData("abc\n0\n0\n-1\n5\n6", false)]
    [InlineData("1\r\n0\r\n0\r\n-1\r\n5\r\n6\r\n", true)]
    public void World_file_ruim_e_recusado(string texto, bool serve) =>
        Assert.Equal(serve, WorldFile.TryParse(texto) is not null);

    [Fact]
    [Trait("Etapa", "33")]
    public void Pacote_sem_clivus_json_ou_corrompido_diz_o_motivo()
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);

        Assert.False(TermoVisionPackage.TryRead(Zip(("terreno.xml", B("<x/>"))), out _, out var e1));
        Assert.Contains("clivus.json", e1);

        Assert.False(TermoVisionPackage.TryRead(Zip(("clivus.json", B("{nao é json"))), out _, out var e2));
        Assert.Contains("corrompido", e2);

        Assert.False(TermoVisionPackage.TryRead(new MemoryStream(B("não é zip")), out _, out var e3));
        Assert.Contains("ZIP", e3);

        Assert.False(TermoVisionPackage.TryRead(Zip(("clivus.json", B(Json.Replace("\"versao\":1", "\"versao\":2")))), out _, out var e4));
        Assert.Contains("outra versão", e4);

        Assert.False(TermoVisionPackage.TryRead(Zip(("clivus.json", B(Json.Replace("\"colunas\":3", "\"colunas\":4")))), out _, out var e5));
        Assert.Contains("incompleta", e5);

        Assert.False(TermoVisionPackage.TryRead(Zip(("clivus.json", B(Json.Replace("\"crs\":{", "\"crx\":{")))), out _, out var e6));
        Assert.Contains("campo errado", e6);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Orto_com_caminho_de_pasta_e_ignorado()
    {
        var json = Json.Replace("\"arquivo\":\"orto.jpg\"", "\"arquivo\":\"../fora.jpg\"");
        var p = Ler(Zip(("clivus.json", B(json)), ("../fora.jpg", [1, 2]), ("orto.jgw", B(Mundo))));
        Assert.Null(p.Ortho);
    }
}

public class TermoVisionRealPackageTests
{
    private static string Acervo(string nome)
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "tests", "acervo"))) pasta = pasta.Parent;
        Assert.NotNull(pasta);
        return Path.Combine(pasta!.FullName, "tests", "acervo", nome);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Pacote_da_Paradise_do_servidor_de_teste_e_lido()
    {
        using var zip = File.OpenRead(Acervo("etapa-33/paradise_tela.zip"));
        Assert.True(TermoVisionPackage.TryRead(zip, out var p, out var erro), erro);

        Assert.Equal("cv_1b152a3a5ce8b700", p!.Shipment);
        Assert.Equal(31982, p.Crs.Epsg);
        Assert.Equal(115, p.Terrain.Columns);
        Assert.Equal(95, p.Terrain.Rows);
        Assert.Equal(711876.0, p.Terrain.E0);
        Assert.Equal(7664301.0, p.Terrain.N0);
        Assert.Equal(4, p.Area.Count);
        Assert.InRange(p.Location!.Latitude, -21.2, -21.0);

        // A grade cobre a área com folga: todo vértice da área tem cota em volta.
        foreach (var (e, n) in p.Area)
        {
            var i = (int)Math.Round((e - p.Terrain.E0) / p.Terrain.Step);
            var j = (int)Math.Round((n - p.Terrain.N0) / p.Terrain.Step);
            Assert.NotNull(p.Terrain.At(i, j));
        }

        Assert.NotNull(p.Ortho);
        Assert.Equal(0.05, p.Ortho!.PixelSize);
        Assert.Equal(0xFF, p.Ortho.Jpeg[0]);
        Assert.Equal(0xD8, p.Ortho.Jpeg[1]);
        Assert.Equal(2, p.Objects.Count);
        Assert.All(p.Objects, o => Assert.True(o.Height >= 1));
    }
}
