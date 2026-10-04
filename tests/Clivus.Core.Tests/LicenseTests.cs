using System.Security.Cryptography;
using System.Text;

namespace Clivus.Core.Tests;

/// <summary>A licença do Clivus Solar (plano/contrato-ativacao.md).</summary>
public class LicenseTests
{
    private static readonly DateTime Agora = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private const string Maquina = "ab12cd34";

    /// <summary>Faz o papel do servidor: assina o payload com a chave privada (ECDSA P-256, r‖s).</summary>
    private static (string Licenca, string Publica) Emitir(string payload, ECDsa? chave = null)
    {
        var c = chave ?? ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var assinatura = c.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return (License.ToBase64Url(bytes) + "." + License.ToBase64Url(assinatura), Convert.ToBase64String(c.ExportSubjectPublicKeyInfo()));
    }

    private static string Payload(string maquina = Maquina, string revalidar = "2026-11-03T12:00:00Z", string expira = "2026-11-18T12:00:00Z", int v = 1) =>
        $"{{\"v\":{v},\"licenca\":\"lic_1\",\"conta\":\"a@b.com\",\"plano\":\"gratuito\",\"maquina\":\"{maquina}\",\"emitida_em\":\"2026-10-04T12:00:00Z\",\"revalidar_em\":\"{revalidar}\",\"expira_em\":\"{expira}\"}}";

    private static Dictionary<string, string> Chaves(string publica) => new() { ["k1"] = publica };

    /// <summary>Troca de chave pelo kid: a licença diz qual chave a assinou; com kid desconhecido, não vale.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void OKidEscolheAChave()
    {
        using var velha = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var nova = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicaVelha = Convert.ToBase64String(velha.ExportSubjectPublicKeyInfo());
        var publicaNova = Convert.ToBase64String(nova.ExportSubjectPublicKeyInfo());
        var chaves = new Dictionary<string, string> { ["2026a"] = publicaVelha, ["2026b"] = publicaNova };

        var comKid = Payload().Replace("{\"v\":1,", "{\"v\":1,\"kid\":\"2026b\",", StringComparison.Ordinal);
        Assert.Equal(LicenseState.Valid, License.Check(Emitir(comKid, nova).Licenca, chaves, Maquina, Agora).State);

        // Assinada pela velha mas dizendo ser da nova: não vale.
        Assert.Equal(LicenseState.Invalid, License.Check(Emitir(comKid, velha).Licenca, chaves, Maquina, Agora).State);

        // kid que o plugin não conhece.
        var outroKid = Payload().Replace("{\"v\":1,", "{\"v\":1,\"kid\":\"2030\",", StringComparison.Ordinal);
        Assert.Equal(LicenseState.Invalid, License.Check(Emitir(outroKid, nova).Licenca, chaves, Maquina, Agora).State);

        // Sem kid: vale com qualquer uma das chaves.
        Assert.Equal(LicenseState.Valid, License.Check(Emitir(Payload(), velha).Licenca, chaves, Maquina, Agora).State);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void LicencaBoaVale()
    {
        var (licenca, publica) = Emitir(Payload());
        var (estado, conteudo, porque) = License.Check(licenca, Chaves(publica), Maquina, Agora);

        Assert.Equal(LicenseState.Valid, estado);
        Assert.Null(porque);
        Assert.Equal("gratuito", conteudo!.Plan);
        Assert.Equal("a@b.com", conteudo.Account);
        Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc), conteudo.ExpiresAt);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void PrazosRevalidarEExpirar()
    {
        var (licenca, publica) = Emitir(Payload());

        Assert.Equal(LicenseState.Revalidate, License.Check(licenca, Chaves(publica), Maquina, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)).State);
        Assert.Equal(LicenseState.Expired, License.Check(licenca, Chaves(publica), Maquina, new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc)).State);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OutraMaquinaOutraChaveOuAlteradaNaoVale()
    {
        var (licenca, publica) = Emitir(Payload());

        Assert.Equal(LicenseState.Invalid, License.Check(licenca, Chaves(publica), "outra", Agora).State);

        // Assinada por outra chave (alguém fazendo a própria licença).
        var (_, outraPublica) = Emitir(Payload());
        Assert.Equal(LicenseState.Invalid, License.Check(licenca, Chaves(outraPublica), Maquina, Agora).State);

        // Payload trocado (expira em 2099) com a assinatura antiga.
        var adulterada = License.ToBase64Url(Encoding.UTF8.GetBytes(Payload(expira: "2099-01-01T00:00:00Z"))) + "." + licenca.Split('.')[1];
        Assert.Equal(LicenseState.Invalid, License.Check(adulterada, Chaves(publica), Maquina, Agora).State);

        Assert.Equal(LicenseState.Invalid, License.Check("lixo", Chaves(publica), Maquina, Agora).State);
        Assert.Equal(LicenseState.Invalid, License.Check("", Chaves(publica), Maquina, Agora).State);
        Assert.Equal(LicenseState.Invalid, License.Check(Emitir(Payload(v: 2)).Licenca, Chaves(publica), Maquina, Agora).State);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void MaquinaECodigo()
    {
        var a = License.MachineId("1D0A2B3C-4D5E-6F70-8192-A3B4C5D6E7F8");
        Assert.Equal(64, a.Length);
        Assert.Equal(a, License.MachineId(" 1d0a2b3c-4d5e-6f70-8192-a3b4c5d6e7f8 "));
        Assert.NotEqual(a, License.MachineId("outra"));

        Assert.Equal("CLV-7K3P-9QX2-M4TD", License.NormalizeCode(" clv-7k3p-9qx2-m4td "));
        Assert.Null(License.NormalizeCode("CLV;DROP"));
        Assert.Null(License.NormalizeCode("  "));
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void RespostasDoServidor()
    {
        Assert.Equal(("abc.def", (string?)null), License.ParseResponse(200, "{\"licenca\":\"abc.def\"}"));
        Assert.Equal("este código já está ativo em 2 máquinas", License.ParseResponse(409, "{\"erro\":\"este código já está ativo em 2 máquinas\"}").Error);
        Assert.Equal("código não encontrado", License.ParseResponse(404, "").Error);
        Assert.Equal("o servidor respondeu sem a licença", License.ParseResponse(200, "{}").Error);
        Assert.Equal("o servidor de licenças respondeu 500", License.ParseResponse(500, "oops").Error);

        Assert.Contains("\"codigo\":\"CLV-1\"", License.ActivateBody("CLV-1", "m", "PC", "0.1.0"), StringComparison.Ordinal);
    }
}
