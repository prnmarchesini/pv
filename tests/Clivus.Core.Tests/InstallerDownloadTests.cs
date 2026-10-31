using System.Net;
using System.Text;

namespace Clivus.Core.Tests;

/// <summary>
/// Etapa 43: o download do instalador da atualização, com um servidor falso
/// (sem rede). Só baixa do servidor ou do site de download, inclusive depois
/// de redirecionar; nada além do tamanho publicado é gravado; o tamanho e o
/// SHA-256 conferem; conexão parada e cancelamento param o download.
/// </summary>
public class InstallerDownloadTests
{
    private const string Servidor = "https://h5rxptnbwh7bvaufisadgzos.177.153.20.214.sslip.io";
    private const string Site = "https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io";
    private const string Url = Site + "/baixar/ClivusSolar-Setup-0.1.1.exe";

    /// <summary>O SHA-256 de "abc".</summary>
    private const string ShaDeAbc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private static readonly TimeSpan Leitura = TimeSpan.FromSeconds(5);

    private static PluginRelease Versao(string url = Url, long tamanho = 3, string sha = ShaDeAbc) =>
        new(SemanticVersion.Parse("0.1.1")!, url, sha, tamanho, string.Empty, null, false);

    private static Task<InstallerDownloadResult> Baixar(Falso servidor, PluginRelease versao, Stream destino, Action<long>? andamento = null, TimeSpan? leitura = null, CancellationToken cancelar = default) =>
        InstallerDownload.RunAsync(new HttpClient(servidor), versao, [Servidor, Site], destino, andamento, leitura ?? Leitura, cancelar);

    private static HttpResponseMessage Corpo(string texto) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes(texto)) };

    private static HttpResponseMessage Para(string local) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(local, UriKind.RelativeOrAbsolute) } };

    [Fact]
    [Trait("Etapa", "43")]
    public async Task BaixaEConfere()
    {
        var servidor = new Falso(_ => Corpo("abc"));
        using var destino = new MemoryStream();
        var andou = new List<long>();

        var resultado = await Baixar(servidor, Versao(), destino, andou.Add);

        Assert.Null(resultado.Error);
        Assert.False(resultado.Mismatch);
        Assert.Equal(new Uri(Url), resultado.From);
        Assert.Equal("abc", Encoding.ASCII.GetString(destino.ToArray()));
        Assert.Equal(3, andou[^1]);
        Assert.Equal([new Uri(Url)], servidor.Pedidos);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task SegueORedirecionamentoParaOServidorOuOSite()
    {
        var servidor = new Falso(p => p.RequestUri!.AbsolutePath switch
        {
            "/baixar/ClivusSolar-Setup-0.1.1.exe" => Para(Servidor + "/arquivos/setup.exe"),
            "/arquivos/setup.exe" => Para("/arquivos/de-novo/setup.exe"),
            _ => Corpo("abc"),
        });
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.Null(resultado.Error);
        Assert.Equal(new Uri(Servidor + "/arquivos/de-novo/setup.exe"), resultado.From);
        Assert.Equal(3, servidor.Pedidos.Count);
        Assert.Equal("abc", Encoding.ASCII.GetString(destino.ToArray()));
    }

    /// <summary>O servidor comprometido que redireciona para outro lugar: o plugin nem pede lá.</summary>
    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("https://golpe.com/ClivusSolar-Setup-0.1.1.exe")]
    [InlineData("http://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io/baixar/a.exe")]
    [InlineData("https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io.golpe.com/a.exe")]
    public async Task NaoSegueORedirecionamentoParaOutroLugar(string outro)
    {
        var servidor = new Falso(p => p.RequestUri!.Host.Contains("golpe", StringComparison.Ordinal) || p.RequestUri.Scheme == "http" ? Corpo("abc") : Para(outro));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.NotNull(resultado.Error);
        Assert.False(resultado.Mismatch);
        Assert.Equal([new Uri(Url)], servidor.Pedidos);
        Assert.Equal(0, destino.Length);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task RedirecionamentoDemaisPara()
    {
        var servidor = new Falso(p => Para(Site + "/salto/" + Guid.NewGuid().ToString("N")));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.NotNull(resultado.Error);
        Assert.Equal(InstallerDownload.MaxRedirects + 1, servidor.Pedidos.Count);
        Assert.Equal(0, destino.Length);
    }

    /// <summary>Revisão: um Location que não se resolve vira erro do download, e não exceção solta.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public async Task LocationQueNaoSeResolveNaoLanca()
    {
        var servidor = new Falso(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("//[", UriKind.Relative) } });
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.NotNull(resultado.Error);
        Assert.Single(servidor.Pedidos);
    }

    /// <summary>O domínio novo do servidor está entre os endereços de download de toda versão instalada a partir daqui.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public async Task BaixaDoDominioNovoDoServidor()
    {
        const string novo = "https://clivus.marcheng.com.br/baixar/ClivusSolar-Setup-0.1.1.exe";
        var servidor = new Falso(_ => Corpo("abc"));
        using var destino = new MemoryStream();

        string?[] confiaveis = [Servidor, .. PluginInfo.EnderecosDeDownload];
        var resultado = await InstallerDownload.RunAsync(new HttpClient(servidor), Versao(url: novo), confiaveis, destino, null, Leitura, default);

        Assert.Null(resultado.Error);
        Assert.Equal([new Uri(novo)], servidor.Pedidos);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task EnderecoQueNaoPrestaNemEPedido()
    {
        var servidor = new Falso(_ => Corpo("abc"));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(url: "https://golpe.com/a.exe"), destino);

        Assert.NotNull(resultado.Error);
        Assert.Null(resultado.From);
        Assert.Empty(servidor.Pedidos);
    }

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Found)]
    public async Task RespostaQueNaoEOArquivoPara(HttpStatusCode status)
    {
        // O 302 sem Location não é redirecionamento que se siga.
        var servidor = new Falso(_ => new HttpResponseMessage(status));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), resultado.Error, StringComparison.Ordinal);
        Assert.False(resultado.Mismatch);
        Assert.Equal(0, destino.Length);
    }

    /// <summary>O tamanho declarado diferente do publicado: nem começa a gravar.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public async Task TamanhoDeclaradoDiferenteNemComeca()
    {
        var servidor = new Falso(_ => Corpo("abcd"));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.NotNull(resultado.Error);
        Assert.False(resultado.Mismatch);
        Assert.Equal(0, destino.Length);
    }

    /// <summary>Sem tamanho declarado e com mais bytes que o publicado: para de gravar no limite.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public async Task MaisQueOPublicadoNaoEGravado()
    {
        var servidor = new Falso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new SoParaFrente(new byte[1_000_000])) });
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(tamanho: 3), destino);

        Assert.NotNull(resultado.Error);
        Assert.True(resultado.Mismatch);
        Assert.True(destino.Length <= 3, $"gravou {destino.Length} bytes");
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task ShaDiferenteNaoServe()
    {
        var servidor = new Falso(_ => Corpo("abd"));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.Contains("SHA-256", resultado.Error, StringComparison.Ordinal);
        Assert.True(resultado.Mismatch);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task ConexaoParadaPara()
    {
        var servidor = new Falso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Parado()) });
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino, leitura: TimeSpan.FromMilliseconds(200));

        Assert.NotNull(resultado.Error);
        Assert.Contains("parou", resultado.Error, StringComparison.Ordinal);
        Assert.False(resultado.Mismatch);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task CanceladoPara()
    {
        var servidor = new Falso(_ => Corpo("abc"));
        using var destino = new MemoryStream();
        using var cancelar = new CancellationTokenSource();
        cancelar.Cancel();

        var resultado = await Baixar(servidor, Versao(), destino, cancelar: cancelar.Token);

        Assert.Equal(Tr.T("download cancelado"), resultado.Error);
        Assert.Equal(0, destino.Length);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public async Task SemRedeDizQueNaoBaixou()
    {
        var servidor = new Falso(_ => throw new HttpRequestException("sem rota"));
        using var destino = new MemoryStream();

        var resultado = await Baixar(servidor, Versao(), destino);

        Assert.Contains("sem rota", resultado.Error, StringComparison.Ordinal);
        Assert.False(resultado.Mismatch);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>O servidor falso: responde pelo pedido e guarda os endereços pedidos.</summary>
    private sealed class Falso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Uri> Pedidos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Pedidos.Add(request.RequestUri!);
            return Task.FromResult(responder(request));
        }
    }

    /// <summary>Um fluxo que não sabe o próprio tamanho (a resposta vem sem Content-Length).</summary>
    private sealed class SoParaFrente(byte[] dados) : MemoryStream(dados)
    {
        public override bool CanSeek => false;
    }

    /// <summary>Uma conexão que não manda mais nada.</summary>
    private sealed class Parado : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
