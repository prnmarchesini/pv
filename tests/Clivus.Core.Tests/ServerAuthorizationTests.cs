namespace Clivus.Core.Tests;

/// <summary>
/// Etapa 43 (o primeiro cliente): o plugin se identifica no servidor do Clivus
/// (Publicar 3D e cadastro de módulos) pela chave CLIVUS_SERVIDOR_CHAVE (o PC
/// do Renan, a conta Develop) ou, sem ela, pela licença válida
/// (<c>Authorization: Licenca {payload}.{assinatura}</c>); sem nenhuma, nada.
/// </summary>
public class ServerAuthorizationTests
{
    /// <summary>Uma licença na forma do contrato (o conteúdo não importa aqui: quem confere é o servidor).</summary>
    private const string Licenca = "eyJ2IjoxLCJraWQiOiIyMDI2YSJ9.c2lnbmF0dXJlLWRlLTY0LWJ5dGVz_-0";

    private const string Producao = "https://h5rxptnbwh7bvaufisadgzos.177.153.20.214.sslip.io";

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(LicenseState.Valid)]
    [InlineData(LicenseState.Revalidate)]
    [InlineData(LicenseState.Expired)]
    [InlineData(LicenseState.Invalid)]
    public void ComAChaveVaiOBearerMesmoComLicenca(LicenseState estado)
    {
        var escolhida = ServerAuthorization.Choose("  chave-develop  ", Licenca, estado);

        Assert.NotNull(escolhida);
        Assert.Equal("Bearer", escolhida.Scheme);
        Assert.Equal("chave-develop", escolhida.Parameter);
        Assert.False(escolhida.IsLicense);
    }

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemAChaveELicencaQueValeVaiALicencaComoEstaNoArquivo(string? chave)
    {
        foreach (var estado in new[] { LicenseState.Valid, LicenseState.Revalidate })
        {
            var escolhida = ServerAuthorization.Choose(chave, Licenca + "\r\n", estado);

            Assert.NotNull(escolhida);
            Assert.Equal("Licenca", escolhida.Scheme);
            Assert.Equal(Licenca, escolhida.Parameter);
            Assert.True(escolhida.IsLicense);
        }
    }

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(LicenseState.Expired)]
    [InlineData(LicenseState.Invalid)]
    public void LicencaQueNaoValeNaoVai(LicenseState estado) =>
        Assert.Null(ServerAuthorization.Choose(null, Licenca, estado));

    [Fact]
    [Trait("Etapa", "43")]
    public void SemChaveNemLicencaNaoVaiNada()
    {
        Assert.Null(ServerAuthorization.Choose(null, null, LicenseState.Valid));
        Assert.Null(ServerAuthorization.Choose(null, "   ", LicenseState.Valid));
        Assert.Null(ServerAuthorization.Choose("", null, LicenseState.Invalid));
    }

    /// <summary>Texto que estragaria o cabeçalho (quebra de linha no meio, espaço, três partes) não vai.</summary>
    [Theory]
    [Trait("Etapa", "43")]
    [InlineData("eyJ2IjoxfQ")]
    [InlineData("eyJ2IjoxfQ.")]
    [InlineData(".c2ln")]
    [InlineData("eyJ2IjoxfQ.c2ln.b3V0cm8")]
    [InlineData("eyJ2Ijox\nfQ.c2ln")]
    [InlineData("eyJ2Ijox fQ.c2ln")]
    [InlineData("eyJ2IjoxfQ==.c2ln")]
    public void TextoForaDaFormaDaLicencaNaoVai(string texto)
    {
        Assert.False(ServerAuthorization.IsLicenseText(texto));
        Assert.Null(ServerAuthorization.Choose(null, texto, LicenseState.Valid));
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void LicencaGrandeDemaisNaoVai()
    {
        var grande = new string('a', ServerAuthorization.LongestLicense) + ".b";
        Assert.Null(ServerAuthorization.Choose(null, grande, LicenseState.Valid));
    }

    /// <summary>A licença de verdade (a do servidor, conferida pelo License.Check) passa na forma.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void ALicencaDoServidorTemAForma()
    {
        using var chave = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"licenca\":\"lic_1\",\"conta\":\"a@b.com\",\"plano\":\"gratuito\",\"maquina\":\"m\",\"emitida_em\":\"2026-10-04T12:00:00Z\",\"revalidar_em\":\"2026-11-03T12:00:00Z\",\"expira_em\":\"2026-11-18T12:00:00Z\"}");
        var assinatura = chave.SignData(payload, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var licenca = License.ToBase64Url(payload) + "." + License.ToBase64Url(assinatura);
        var publica = Convert.ToBase64String(chave.ExportSubjectPublicKeyInfo());

        var (estado, _, _) = License.Check(licenca, new Dictionary<string, string> { ["k"] = publica }, "m", new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(LicenseState.Valid, estado);

        Assert.Equal(licenca, ServerAuthorization.Choose(null, licenca, estado)?.Parameter);
    }

    // ------------------------------------------------- a recusa do servidor

    [Fact]
    [Trait("Etapa", "43")]
    public void ARecusaSemMotivoDizOQueConferir()
    {
        var bearer = ServerAuthorization.Choose("chave", null, LicenseState.Invalid);
        var licenca = ServerAuthorization.Choose(null, Licenca, LicenseState.Valid);

        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", ServerAuthorization.RefusedMessage(bearer), StringComparison.Ordinal);
        Assert.Contains("licença", ServerAuthorization.RefusedMessage(licenca), StringComparison.Ordinal);
        Assert.Contains("Ativar", ServerAuthorization.RefusedMessage(licenca), StringComparison.Ordinal);
        Assert.Contains("Ativar", ServerAuthorization.RefusedMessage(null), StringComparison.Ordinal);
        Assert.DoesNotContain("CLIVUS_SERVIDOR_CHAVE", ServerAuthorization.RefusedMessage(licenca), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Etapa", "43")]
    [InlineData(401)]
    [InlineData(403)]
    public void NoPublicar3DORecusadoSemMotivoSegueAIdentificacao(int status)
    {
        var licenca = ServerAuthorization.Choose(null, Licenca, LicenseState.Valid);

        Assert.Equal(ServerAuthorization.RefusedMessage(licenca), Viewer3DPage.ParseResponse(status, "", licenca).Erro);
        Assert.Equal(ServerAuthorization.RefusedMessage(null), Viewer3DPage.ParseResponse(status, "<html>proxy</html>", null).Erro);

        // O de antes (sem dizer a identificação) continua falando da chave.
        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", Viewer3DPage.ParseResponse(status, "").Erro, StringComparison.Ordinal);
    }

    /// <summary>Contrato da etapa 43: o 401 e o 403 vêm com {"erro": "..."}, e o plugin mostra como veio.</summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void NoPublicar3DOMotivoDoServidorVaiComoVeio()
    {
        var licenca = ServerAuthorization.Choose(null, Licenca, LicenseState.Valid);

        Assert.Equal("licença revogada", Viewer3DPage.ParseResponse(403, "{\"erro\": \"licença revogada\"}", licenca).Erro);
        Assert.Equal("conta vencida", Viewer3DPage.ParseResponse(401, "{\"erro\": \"conta vencida\"}", null).Erro);

        // O que não é recusa não muda.
        Assert.Equal(Viewer3DPage.ParseResponse(413, "").Erro, Viewer3DPage.ParseResponse(413, "", licenca).Erro);

        var publicada = Viewer3DPage.ParseResponse(201, "{\"id\":\"k3f9x2\",\"url\":\"https://x/3d/k3f9x2\"}", licenca).Publicada;
        Assert.Equal("k3f9x2", publicada?.Id);
    }

    // ------------------------------------------------------------ os módulos

    [Fact]
    [Trait("Etapa", "43")]
    public void ComALicencaOCadastroDeModuloVai()
    {
        var servidor = ModuleServer.Resolve(null, Producao);

        Assert.Null(servidor.WhyCannotSend(ServerAuthorization.Choose(null, Licenca, LicenseState.Valid)));
        Assert.Null(servidor.WhyCannotSend(ServerAuthorization.Choose("chave", null, LicenseState.Invalid)));

        var semNada = servidor.WhyCannotSend(null);
        Assert.Contains("Ativar", semNada, StringComparison.Ordinal);
        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", semNada, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "43")]
    public void ALicencaSoVaiPorHttpsEOServicoLocalNaoPedeNada()
    {
        var licenca = ServerAuthorization.Choose(null, Licenca, LicenseState.Valid);

        Assert.Contains("HTTPS", ModuleServer.Resolve(null, "http://clivus.exemplo.com").WhyCannotSend(licenca), StringComparison.Ordinal);
        Assert.Null(ModuleServer.Resolve(null, "http://localhost:8000").WhyCannotSend(licenca));
        Assert.Null(ModuleServer.Resolve("http://localhost:8765", Producao).WhyCannotSend(null));
    }
}
