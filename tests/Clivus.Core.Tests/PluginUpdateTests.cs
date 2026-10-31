using System.Text;

namespace Clivus.Core.Tests;

/// <summary>
/// Etapa 43: a atualização do plugin no PC do cliente. A versão publicada
/// (<c>GET /api/v1/versao</c>), a comparação de versões, de onde o instalador
/// pode ser baixado, a conferência do arquivo, quando a abertura do Civil 3D
/// consulta e a linha de comando do instalador que espera o Civil 3D fechar.
/// </summary>
public class PluginUpdateTests
{
    private const string Servidor = "https://h5rxptnbwh7bvaufisadgzos.177.153.20.214.sslip.io";
    private const string Site = "https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io";

    /// <summary>O SHA-256 de "abc" (FIPS 180-2, exemplo B.1).</summary>
    private const string ShaDeAbc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private static string Resposta(
        string versao = "0.1.1", string url = Site + "/baixar/ClivusSolar-Setup-0.1.1.exe", string sha = ShaDeAbc,
        string tamanho = "6500000", string notas = "\"Atualização automática.\"", string publicada = "\"2026-11-01T12:00:00Z\"", string obrigatoria = "false") =>
        $"{{\"versao\": \"{versao}\", \"url\": \"{url}\", \"sha256\": \"{sha}\", \"tamanho\": {tamanho}, \"notas\": {notas}, \"publicada_em\": {publicada}, \"obrigatoria\": {obrigatoria}}}";

    // ------------------------------------------------------------ as versões

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.1.1", false)]
    [InlineData("0.10.0", "0.9.9", true)]
    [InlineData("1.0.0", "0.99.99", true)]
    [InlineData("0.2.0", "0.1.12", true)]
    [InlineData("0.1.1", "0.1.0+3f2a1c9", true)]
    [InlineData("0.1.0", "0.1.0+3f2a1c9", false)]
    [InlineData("v0.1.1", "0.1.0", true)]
    [InlineData("0.2.0-beta.1", "0.1.9", true)]
    [InlineData("0.2.0-beta.1", "0.2.0", false)]
    [InlineData("0.2.0", "0.2.0-beta.1", true)]
    public void AVersaoPublicadaEMaisNovaPeloSemver(string publicada, string instalada, bool maisNova) =>
        Assert.Equal(maisNova, SemanticVersion.IsNewer(publicada, instalada));

    /// <summary>Versão fora do formato, de qualquer lado: não oferece (melhor não oferecer do que oferecer sem saber o quê).</summary>
    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("0.1", "0.1.0")]
    [InlineData("0.1.1.0", "0.1.0")]
    [InlineData("0.1.x", "0.1.0")]
    [InlineData("", "0.1.0")]
    [InlineData(null, "0.1.0")]
    [InlineData("0.1.1", "desconhecida")]
    [InlineData("0.1.1", null)]
    [InlineData("-0.1.1", "0.1.0")]
    [InlineData("0.1.1-", "0.1.0")]
    [InlineData("0.1.1-beta..1", "0.1.0")]
    [InlineData("0.1.1+", "0.1.0")]
    [InlineData("0.1.+1", "0.1.0")]
    [InlineData("1234567890.0.0", "0.1.0")]
    public void VersaoForaDoFormatoNaoEOferecida(string? publicada, string? instalada) =>
        Assert.False(SemanticVersion.IsNewer(publicada, instalada));

    /// <summary>A ordem do exemplo do semver.org (item 11).</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void APreVersaoSegueAOrdemDoSemver()
    {
        string[] ordem = ["1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"];

        for (var i = 0; i + 1 < ordem.Length; i++)
        {
            var antes = SemanticVersion.Parse(ordem[i])!;
            var depois = SemanticVersion.Parse(ordem[i + 1])!;
            Assert.True(antes.CompareTo(depois) < 0, $"{ordem[i]} devia vir antes de {ordem[i + 1]}");
            Assert.True(depois.CompareTo(antes) > 0, $"{ordem[i + 1]} devia vir depois de {ordem[i]}");
        }

        Assert.Equal(0, SemanticVersion.Parse("1.0.0+build.1")!.CompareTo(SemanticVersion.Parse("1.0.0+outro")));
        Assert.Equal("0.2.0-beta.1", SemanticVersion.Parse(" v0.2.0-beta.1+abc ")!.ToString());
    }

    // ----------------------------------------------------- a versão publicada

    [Fact]
    [Trait("Etapa", "43")]
    public void AVersaoPublicadaELidaDoContrato()
    {
        var (versao, erro) = PluginUpdate.ParseReply(200, Resposta(sha: ShaDeAbc.ToUpperInvariant()));

        Assert.Null(erro);
        Assert.NotNull(versao);
        Assert.Equal("0.1.1", versao.Version.ToString());
        Assert.Equal(Site + "/baixar/ClivusSolar-Setup-0.1.1.exe", versao.Url);
        Assert.Equal(ShaDeAbc, versao.Sha256);
        Assert.Equal(6_500_000, versao.Size);
        Assert.Equal("Atualização automática.", versao.Notes);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), versao.PublishedAt);
        Assert.Equal(DateTimeKind.Utc, versao.PublishedAt!.Value.Kind);
        Assert.False(versao.Required);

        Assert.True(PluginUpdate.ParseReply(200, Resposta(obrigatoria: "true")).Release!.Required);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void OsCamposOpcionaisPodemFaltar()
    {
        var (versao, erro) = PluginUpdate.ParseReply(200, $"{{\"versao\": \"0.1.1\", \"url\": \"{Site}/baixar/a.exe\", \"sha256\": \"{ShaDeAbc}\", \"tamanho\": 10}}");

        Assert.Null(erro);
        Assert.Equal(string.Empty, versao!.Notes);
        Assert.Null(versao.PublishedAt);
        Assert.False(versao.Required);

        Assert.Equal(string.Empty, PluginUpdate.ParseReply(200, Resposta(notas: "null", publicada: "null")).Release!.Notes);
        Assert.Null(PluginUpdate.ParseReply(200, Resposta(publicada: "\"ontem\"")).Release!.PublishedAt);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void NotasLongasSaoCortadas()
    {
        var longas = new string('x', PluginUpdate.LongestNotes + 50);
        var notas = PluginUpdate.ParseReply(200, Resposta(notas: $"\"{longas}\"")).Release!.Notes;

        Assert.Equal(PluginUpdate.LongestNotes + 1, notas.Length);
        Assert.EndsWith("…", notas, StringComparison.Ordinal);
    }

    /// <summary>404: o servidor ainda não publicou versão. Não é erro.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void SemVersaoPublicadaNaoEErro()
    {
        Assert.Equal((null, null), PluginUpdate.ParseReply(404, "{\"erro\": \"nenhuma versão publicada\"}"));
        Assert.Equal((null, null), PluginUpdate.ParseReply(404, ""));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void OErroDoServidorVemComoVeio()
    {
        Assert.Equal("manutenção", PluginUpdate.ParseReply(503, "{\"erro\": \"manutenção\"}").Error);
        Assert.Contains("500", PluginUpdate.ParseReply(500, "<html>erro</html>").Error, StringComparison.Ordinal);
        Assert.NotNull(PluginUpdate.ParseReply(200, "<html>proxy</html>").Error);
        Assert.NotNull(PluginUpdate.ParseReply(200, "").Error);
        Assert.NotNull(PluginUpdate.ParseReply(200, "[1, 2]").Error);
    }

    /// <summary>Revisão: o corte das notas não parte um caractere de dois códigos.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void OCorteDasNotasNaoParteUmEmoji()
    {
        // O emoji começa no último caractere que cabe: o corte simples levaria só a metade dele.
        var notas = new string('x', PluginUpdate.LongestNotes - 1) + "🙂" + new string('y', 50);
        Assert.True(char.IsHighSurrogate(notas[PluginUpdate.LongestNotes - 1]));

        var lidas = PluginUpdate.ParseReply(200, Resposta(notas: System.Text.Json.JsonSerializer.Serialize(notas))).Release!.Notes;

        Assert.EndsWith("…", lidas, StringComparison.Ordinal);
        var semReticencias = lidas[..^1];
        Assert.False(char.IsHighSurrogate(semReticencias[^1]), "o corte deixou meio emoji");
        Assert.Equal(new string('x', PluginUpdate.LongestNotes - 1), semReticencias);
    }

    /// <summary>
    /// Revisão: a consulta conta como a do dia quando o servidor respondeu e
    /// não há versão obrigatória pendente, pela abertura ou pelo comando.
    /// </summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void AObrigatoriaPendenteNaoContaComoAConsultaDoDia()
    {
        var comum = PluginUpdate.ParseReply(200, Resposta()).Release!;
        var obrigatoria = comum with { Required = true };

        Assert.True(PluginUpdate.RecordsCheck(respondeu: true, maisNova: null));
        Assert.True(PluginUpdate.RecordsCheck(respondeu: true, maisNova: comum));
        Assert.False(PluginUpdate.RecordsCheck(respondeu: true, maisNova: obrigatoria));
        Assert.False(PluginUpdate.RecordsCheck(respondeu: false, maisNova: null));
    }

    /// <summary>
    /// Revisão: os endereços de download incluem o domínio novo do servidor
    /// (com eles, o plugin de hoje aceita o instalador servido de lá depois
    /// da troca) e nenhum domínio que não seja do Renan.
    /// </summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void OsEnderecosDeDownloadSaoOSiteEODominioNovo()
    {
        Assert.Equal([PluginInfo.SiteDeDownload, "https://clivus.marcheng.com.br"], PluginInfo.EnderecosDeDownload);
        Assert.All(PluginInfo.EnderecosDeDownload, e => Assert.Null(Viewer3DPage.WhyServerUnsafe(e)));
        Assert.DoesNotContain(PluginInfo.EnderecosDeDownload, e => e.Contains("clivussolar.com", StringComparison.OrdinalIgnoreCase));

        string?[] confiaveis = [Servidor, .. PluginInfo.EnderecosDeDownload];
        Assert.Null(PluginUpdate.WhyUrlRejected("https://clivus.marcheng.com.br/baixar/ClivusSolar-Setup-0.1.1.exe", confiaveis));
        Assert.NotNull(PluginUpdate.WhyUrlRejected("https://marcheng.com.br/baixar/a.exe", confiaveis));
        Assert.NotNull(PluginUpdate.WhyUrlRejected("https://outro.marcheng.com.br/baixar/a.exe", confiaveis));
        Assert.NotNull(PluginUpdate.WhyUrlRejected("https://clivussolar.com/baixar/a.exe", confiaveis));
    }

    /// <summary>Versão fora do contrato não vira oferta: nada de baixar sem saber o quê.</summary>
    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("versao", "0.1")]
    [InlineData("versao", "")]
    [InlineData("url", "")]
    [InlineData("url", "/baixar/a.exe")]
    [InlineData("sha256", "abc")]
    [InlineData("sha256", "zz7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("tamanho", "0")]
    [InlineData("tamanho", "-5")]
    [InlineData("tamanho", "6.5")]
    [InlineData("tamanho", "\"6500000\"")]
    [InlineData("tamanho", "209715201")]
    public void VersaoForaDoContratoERecusada(string campo, string valor)
    {
        var corpo = campo switch
        {
            "versao" => Resposta(versao: valor),
            "url" => Resposta(url: valor),
            "sha256" => Resposta(sha: valor),
            _ => Resposta(tamanho: valor),
        };

        var (versao, erro) = PluginUpdate.ParseReply(200, corpo);

        Assert.Null(versao);
        Assert.NotNull(erro);
    }

    // --------------------------------------------------------- de onde baixa

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(Site + "/baixar/ClivusSolar-Setup-0.1.1.exe")]
    [InlineData(Servidor + "/baixar/ClivusSolar-Setup-0.1.1.exe")]
    [InlineData("https://NOXSBAMRCOBKFHFKCL3IVA7X.177.153.20.214.sslip.io/baixar/a.exe")]
    public void OInstaladorVemDoServidorOuDoSite(string url) =>
        Assert.Null(PluginUpdate.WhyUrlRejected(url, Servidor, Site));

    /// <summary>Um servidor comprometido não manda o plugin rodar um executável de qualquer lugar.</summary>
    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("https://golpe.com/ClivusSolar-Setup-0.1.1.exe")]
    [InlineData("https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io.golpe.com/baixar/a.exe")]
    [InlineData("https://177.153.20.214/baixar/a.exe")]
    [InlineData("http://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io/baixar/a.exe")]
    [InlineData("https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io:8443/baixar/a.exe")]
    [InlineData("ftp://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io/a.exe")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("/baixar/a.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void OInstaladorDeOutroLugarERecusado(string? url) =>
        Assert.NotNull(PluginUpdate.WhyUrlRejected(url, Servidor, Site));

    [Fact]
    [Trait("Etapa", "43")]
    public void ComServidorDeTesteEmLocalhostOHttpDeleVale()
    {
        Assert.Null(PluginUpdate.WhyUrlRejected("http://localhost:8000/baixar/a.exe", "http://localhost:8000", Site));
        Assert.NotNull(PluginUpdate.WhyUrlRejected("http://localhost:9000/baixar/a.exe", "http://localhost:8000", Site));

        // Servidor sem HTTPS fora de localhost não é endereço confiável.
        Assert.NotNull(PluginUpdate.WhyUrlRejected("http://clivus.exemplo.com/a.exe", "http://clivus.exemplo.com", null));
        Assert.NotNull(PluginUpdate.WhyUrlRejected(Site + "/a.exe", null, null));
    }

    // ------------------------------------------------------- o arquivo baixado

    [Fact]
    [Trait("Etapa", "43")]
    public void OArquivoBaixadoTemOTamanhoEOShaDaVersao()
    {
        var versao = PluginUpdate.ParseReply(200, Resposta(tamanho: "3")).Release!;

        using var abc = new MemoryStream(Encoding.ASCII.GetBytes("abc"));
        var sha = PluginUpdate.Sha256Hex(abc);
        Assert.Equal(ShaDeAbc, sha);

        Assert.Null(PluginUpdate.WhyDownloadRejected(3, sha, versao));
        Assert.Null(PluginUpdate.WhyDownloadRejected(3, sha.ToUpperInvariant(), versao));
        Assert.NotNull(PluginUpdate.WhyDownloadRejected(4, sha, versao));
        Assert.NotNull(PluginUpdate.WhyDownloadRejected(3, new string('0', 64), versao));
        Assert.NotNull(PluginUpdate.WhyDownloadRejected(3, null, versao));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void ONomeDoInstaladorSaiDaVersaoENuncaDoEndereco()
    {
        var versao = PluginUpdate.ParseReply(200, Resposta(url: Site + "/baixar/..%5C..%5Cwindows%5Cmalvado.exe")).Release!;

        Assert.Equal("ClivusSolar-Setup-0.1.1.exe", PluginUpdate.InstallerFileName(versao.Version));
    }

    // ----------------------------------------------------- quando consulta

    [Fact]
    [Trait("Etapa", "43")]
    public void AAberturaConsultaNoMaximoUmaVezPorDia()
    {
        var agora = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

        Assert.True(PluginUpdate.IsCheckDue(null, agora));
        Assert.False(PluginUpdate.IsCheckDue(agora, agora));
        Assert.False(PluginUpdate.IsCheckDue(agora.AddHours(-23), agora));
        Assert.True(PluginUpdate.IsCheckDue(agora.AddHours(-24), agora));
        Assert.True(PluginUpdate.IsCheckDue(agora.AddDays(-30), agora));

        // Relógio que voltou: a última "no futuro" não cala a atualização até lá.
        Assert.True(PluginUpdate.IsCheckDue(agora.AddDays(3), agora));
        Assert.False(PluginUpdate.IsCheckDue(agora.AddMinutes(1), agora));
    }

    /// <summary>A última consulta fica nas preferências, sem estragar o arquivo de quem já tem (como os modelos apagados).</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void AUltimaConsultaFicaNasPreferencias()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clivus-atualizacao-" + Guid.NewGuid().ToString("N"));
        var arquivo = Path.Combine(pasta, "preferencias.json");
        var quando = new DateTime(2026, 10, 31, 14, 5, 9, DateTimeKind.Utc);

        try
        {
            Assert.Null(UserPreferences.Load(arquivo).LastUpdateCheck);

            // O arquivo de antes, sem o campo: nunca consultou, e o resto vale.
            Directory.CreateDirectory(pasta);
            File.WriteAllText(arquivo, "{\"idioma\": \"es\", \"rastreio\": false, \"modelosApagados\": [\"GC800\"]}");
            var antigas = UserPreferences.Load(arquivo);
            Assert.Null(antigas.LastUpdateCheck);
            Assert.Equal("es", antigas.Language);

            // Grava a consulta e mantém o resto.
            (antigas with { LastUpdateCheck = quando }).Save(arquivo);
            var lidas = UserPreferences.Load(arquivo);
            Assert.Equal(quando, lidas.LastUpdateCheck);
            Assert.Equal(DateTimeKind.Utc, lidas.LastUpdateCheck!.Value.Kind);
            Assert.Equal("es", lidas.Language);
            Assert.False(lidas.ProfileTracking);
            Assert.Equal(["GC800"], lidas.HiddenModelIds);
            Assert.Contains("\"ultimaConsultaDeVersao\": \"2026-10-31T14:05:09Z\"", File.ReadAllText(arquivo), StringComparison.Ordinal);

            // Mudar outra preferência não perde a consulta.
            (lidas with { Language = "pt" }).Save(arquivo);
            Assert.Equal(quando, UserPreferences.Load(arquivo).LastUpdateCheck);

            // Sem consulta, o campo nem vai para o arquivo.
            new UserPreferences("pt").Save(arquivo);
            Assert.DoesNotContain("ultimaConsultaDeVersao", File.ReadAllText(arquivo), StringComparison.Ordinal);

            // Data estragada: nunca consultou (e o resto continua valendo).
            File.WriteAllText(arquivo, "{\"idioma\": \"en\", \"ultimaConsultaDeVersao\": \"amanhã\"}");
            Assert.Null(UserPreferences.Load(arquivo).LastUpdateCheck);
            Assert.Equal("en", UserPreferences.Load(arquivo).Language);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    /// <summary>
    /// Revisão: quem grava sozinho (a consulta do dia) sabe quando o arquivo
    /// não se leu, para não gravar o padrão por cima do idioma e dos modelos
    /// apagados do usuário.
    /// </summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void AsPreferenciasDizemQuandoNaoSeLeram()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clivus-preferencias-" + Guid.NewGuid().ToString("N"));
        var arquivo = Path.Combine(pasta, "preferencias.json");

        try
        {
            // Sem arquivo: lê (o padrão).
            Assert.True(UserPreferences.TryLoad(arquivo, out var padrao));
            Assert.Equal("auto", padrao.Language);

            Directory.CreateDirectory(pasta);
            File.WriteAllText(arquivo, "{\"idioma\": \"es\"}");
            Assert.True(UserPreferences.TryLoad(arquivo, out var lidas));
            Assert.Equal("es", lidas.Language);

            // Estragado (uma gravação pela metade): não leu, e o Load continua dando o padrão.
            File.WriteAllText(arquivo, "{\"idioma\": \"es\", \"modelosApa");
            Assert.False(UserPreferences.TryLoad(arquivo, out var estragadas));
            Assert.Equal("auto", estragadas.Language);
            Assert.Equal("auto", UserPreferences.Load(arquivo).Language);

            // Preso por outro programa: também não leu.
            File.WriteAllText(arquivo, "{\"idioma\": \"es\"}");
            using (new FileStream(arquivo, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.False(UserPreferences.TryLoad(arquivo, out _));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    // --------------------------------------------- o instalador que espera

    [Fact]
    [Trait("Etapa", "43")]
    public void OInstaladorRodaComAEsperaORegistroEOIdioma()
    {
        var registro = @"C:\Users\Fulano de Tal\AppData\Local\Clivus Solar\atualizacao.log";

        Assert.Equal(
            "/aguardar-civil3d \"/log=C:\\Users\\Fulano de Tal\\AppData\\Local\\Clivus Solar\\atualizacao.log\" /idioma=es",
            PluginUpdate.InstallerArguments(registro, UiLanguage.Spanish));

        // A linha que o plugin monta é a que o instalador lê.
        var opcoes = InstallerOptions.Parse(LinhaDeComando(PluginUpdate.InstallerArguments(registro, UiLanguage.English)));
        Assert.True(opcoes.WaitForCivil3D);
        Assert.True(opcoes.Silent);
        Assert.False(opcoes.Uninstall);
        Assert.Equal(registro, opcoes.LogFile);
        Assert.Equal("en", opcoes.Language);

        Assert.Throws<ArgumentException>(() => PluginUpdate.InstallerArguments("C:\\pasta\\", UiLanguage.Portuguese));
        Assert.Throws<ArgumentException>(() => PluginUpdate.InstallerArguments("C:\\a\"b.log", UiLanguage.Portuguese));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void SoOPluginDoInstaladorEAtualizadoPorAqui()
    {
        var appData = @"C:\Users\fulano\AppData\Roaming";

        Assert.True(PluginUpdate.IsInstallerBundle(@"C:\Users\fulano\AppData\Roaming\Autodesk\ApplicationPlugins\ClivusSolar.bundle\Contents", appData));
        Assert.True(PluginUpdate.IsInstallerBundle(@"c:\users\FULANO\appdata\roaming\autodesk\applicationplugins\clivussolar.bundle\contents\", appData));

        // A instalação da máquina (Arquivos de Programas), a pasta do build e o nome antigo ficam de fora.
        Assert.False(PluginUpdate.IsInstallerBundle(@"C:\Program Files\Autodesk\ApplicationPlugins\ClivusSolar.bundle\Contents", appData));
        Assert.False(PluginUpdate.IsInstallerBundle(@"C:\Dev\PV\src\Clivus.Plugin\bin\Debug", appData));
        Assert.False(PluginUpdate.IsInstallerBundle(@"C:\Users\fulano\AppData\Roaming\Autodesk\ApplicationPlugins\UFV.bundle\Contents", appData));
        Assert.False(PluginUpdate.IsInstallerBundle(null, appData));
        Assert.False(PluginUpdate.IsInstallerBundle(@"C:\x", null));
    }

    /// <summary>Divide a linha de comando como o Windows divide os argumentos de um programa .NET (aspas agrupam).</summary>
    private static List<string> LinhaDeComando(string linha)
    {
        var args = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;

        foreach (var c in linha)
        {
            if (c == '"')
            {
                entreAspas = !entreAspas;
                continue;
            }

            if (c == ' ' && !entreAspas)
            {
                if (atual.Length > 0) args.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            atual.Append(c);
        }

        if (atual.Length > 0) args.Add(atual.ToString());
        return args;
    }
}
