using System.IO.Compression;
using System.Net;
using System.Text;
using Clivus.Core;

namespace Clivus.Core.Tests;

public class TermoVisionClientTests
{
    private static readonly TermoVisionLink Link = new("https://tv.exemplo", "cv_1", "tvc_SEGREDO");

    private sealed class Servidor(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Pedido, byte[]? Corpo)> Pedidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelar)
        {
            Pedidos.Add((pedido, pedido.Content is null ? null : await pedido.Content.ReadAsByteArrayAsync(cancelar)));
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SemRede : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelar) =>
            throw new HttpRequestException("Nenhuma conexão pôde ser feita");
    }

    [Fact]
    [Trait("Etapa", "33")]
    public async Task Resumo_vai_com_o_token_e_e_lido()
    {
        var servidor = new Servidor(HttpStatusCode.OK, """
            {"versao":1,"envio":"cv_1","usina":{"id":"p","nome":"Paradise"},"area":{"id":"a","nome":"Área 1"},
             "crs":{"epsg":31982},"expira_em":"2026-11-03T00:15:24Z","criado_por":"Renan","tamanho_estimado_mb":1.5}
            """);
        using var cliente = new TermoVisionClient(Link, servidor);

        var (resumo, erro) = await cliente.SummaryAsync();

        Assert.Null(erro);
        Assert.Equal(new TermoVisionSummary("Paradise", "Área 1", 31982, new DateTimeOffset(2026, 11, 3, 0, 15, 24, TimeSpan.Zero), "Renan", 1.5), resumo);
        var (pedido, _) = Assert.Single(servidor.Pedidos);
        Assert.Equal(HttpMethod.Get, pedido.Method);
        Assert.Equal("https://tv.exemplo/api/clivus/v1/envio", pedido.RequestUri!.ToString());
        Assert.Equal("Bearer", pedido.Headers.Authorization!.Scheme);
        Assert.Equal("tvc_SEGREDO", pedido.Headers.Authorization.Parameter);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public async Task Retorno_vai_comprimido_com_o_pedido_do_clique()
    {
        var servidor = new Servidor(HttpStatusCode.OK, """{"gravado":true,"operacao":"op1","mesas":1,"sairam":0,"fora_da_area":0,"avisos":[],"url":"u"}""");
        using var cliente = new TermoVisionClient(Link, servidor);
        var json = Encoding.UTF8.GetBytes("""{"versao":1,"mesas":[]}""");

        var (resultado, erro) = await cliente.SendAsync(json, "clique-7");

        Assert.Null(erro);
        Assert.True(resultado!.Saved);
        var (pedido, corpo) = Assert.Single(servidor.Pedidos);
        Assert.Equal(HttpMethod.Post, pedido.Method);
        Assert.Equal("/api/clivus/v1/retorno", pedido.RequestUri!.AbsolutePath);
        Assert.Equal("clique-7", Assert.Single(pedido.Headers.GetValues("X-Pedido")));
        Assert.Equal("gzip", Assert.Single(pedido.Content!.Headers.ContentEncoding));
        Assert.Equal("application/json", pedido.Content.Headers.ContentType!.MediaType);

        using var gzip = new GZipStream(new MemoryStream(corpo!), CompressionMode.Decompress);
        using var leitor = new StreamReader(gzip, Encoding.UTF8);
        Assert.Equal("""{"versao":1,"mesas":[]}""", leitor.ReadToEnd());
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData(401, "{\"detail\": \"O token venceu em 02/11/2026.\"}", "O token venceu em 02/11/2026.")]
    [InlineData(401, "", "o token venceu ou deixou de valer")]
    [InlineData(404, "{\"detail\": \"Área 1 apagada\"}", "desfeita no TermoVision: Área 1 apagada")]
    [InlineData(400, "{\"detail\": \"mesa M3: cantos com 3 pontos\"}", "recusou as mesas: mesa M3: cantos com 3 pontos")]
    [InlineData(413, "", "20 MB")]
    [InlineData(500, "Internal Server Error", "respondeu 500: sem motivo.")]
    public async Task Erro_do_servidor_vira_frase_com_o_detalhe(int status, string corpo, string trecho)
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);
        using var cliente = new TermoVisionClient(Link, new Servidor((HttpStatusCode)status, corpo));

        var (resultado, erro) = await cliente.SendAsync([1], "c");

        Assert.Null(resultado);
        Assert.Contains(trecho, erro);
        Assert.DoesNotContain("SEGREDO", erro);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public async Task Sem_rede_diz_o_servidor_e_nao_o_token()
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);
        using var cliente = new TermoVisionClient(Link, new SemRede());

        var (zip, erro) = await cliente.PackageAsync();

        Assert.Null(zip);
        Assert.Contains("https://tv.exemplo", erro);
        Assert.DoesNotContain("SEGREDO", erro);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public async Task Retorno_acima_de_20_MB_nem_sai()
    {
        var servidor = new Servidor(HttpStatusCode.OK, "{}");
        using var cliente = new TermoVisionClient(Link, servidor);

        var (_, erro) = await cliente.SendAsync(new byte[TermoVisionClient.MaxReturnBytes + 1], "c");

        Assert.NotNull(erro);
        Assert.Empty(servidor.Pedidos);
    }

    /// <summary>
    /// Contra o servidor de teste do TermoVision, quando a variável
    /// <c>CLIVUS_TV_TESTE</c> traz um endereço clivus:// válido. Sem ela, passa calado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "33")]
    public async Task Servidor_de_teste_de_verdade_quando_configurado()
    {
        if (Environment.GetEnvironmentVariable("CLIVUS_TV_TESTE") is not { Length: > 0 } endereco) return;
        Assert.True(TermoVisionLink.TryParse(endereco, out var link, out var e0), e0);

        using var cliente = new TermoVisionClient(link!);
        var (resumo, e1) = await cliente.SummaryAsync();
        Assert.True(resumo is not null, e1);

        var (zip, e2) = await cliente.PackageAsync();
        Assert.True(zip is not null, e2);
        Assert.True(TermoVisionPackage.TryRead(new MemoryStream(zip!), out var pacote, out var e3), e3);
        Assert.Equal(link!.Shipment, pacote!.Shipment);
    }

    private sealed class CaiNoMeio : HttpMessageHandler
    {
        private sealed class Fluxo : MemoryStream
        {
            public override ValueTask<int> ReadAsync(Memory<byte> destino, CancellationToken cancelar = default) =>
                throw new IOException("conexão caiu");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelar) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Fluxo()) });
    }

    [Fact]
    [Trait("Etapa", "33")]
    public async Task Conexao_que_cai_no_meio_do_pacote_vira_frase()
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);
        using var cliente = new TermoVisionClient(Link, new CaiNoMeio());

        var (zip, erro) = await cliente.PackageAsync();

        Assert.Null(zip);
        Assert.Contains("Não consegui falar", erro);
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("{\"crs\": \"31982\"}")]
    [InlineData("{\"crs\": {\"epsg\": \"31982\"}}")]
    [InlineData("[]")]
    [InlineData("{\"usina\": [1]}")]
    public async Task Resumo_com_tipo_inesperado_nao_derruba(string corpo)
    {
        using var cliente = new TermoVisionClient(Link, new Servidor(HttpStatusCode.OK, corpo));
        var (resumo, erro) = await cliente.SummaryAsync();
        Assert.True(resumo is not null || erro is not null);
    }
}
