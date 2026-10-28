using Clivus.Core;

namespace Clivus.Core.Tests;

public sealed class TermoVisionInboxTests : IDisposable
{
    private const string Endereco = "clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&envio=cv_1&token=tvc_1";
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "clivus-entrada-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Endereco_deixado_e_lido_uma_vez_so()
    {
        var entrada = new TermoVisionInbox(_pasta);
        var arquivo = entrada.Deposit("  " + Endereco + "\r\n");
        Assert.EndsWith(TermoVisionInbox.Extension, arquivo);
        Assert.Empty(Directory.GetFiles(_pasta, "*.tmp"));

        var lidos = entrada.Take(DateTime.UtcNow, out var descartados);
        Assert.Equal("cv_1", Assert.Single(lidos).Shipment);
        Assert.Equal(0, descartados);
        Assert.Empty(Directory.GetFiles(_pasta));

        Assert.Empty(entrada.Take(DateTime.UtcNow, out _));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Lixo_e_arquivo_vencido_saem_sem_abrir()
    {
        var entrada = new TermoVisionInbox(_pasta);
        entrada.Deposit("não é endereço");
        var velho = entrada.Deposit(Endereco);
        File.SetLastWriteTimeUtc(velho, DateTime.UtcNow.AddDays(-8));
        entrada.Deposit(Endereco.Replace("cv_1", "cv_2"));

        var lidos = entrada.Take(DateTime.UtcNow, out var descartados);

        Assert.Equal("cv_2", Assert.Single(lidos).Shipment);
        Assert.Equal(2, descartados);
        Assert.Empty(Directory.GetFiles(_pasta));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Pasta_que_nao_existe_e_vazia() =>
        Assert.Empty(new TermoVisionInbox(_pasta).Take(DateTime.UtcNow, out _));

    [Fact]
    [Trait("Etapa", "33")]
    public void Varios_cliques_saem_na_ordem()
    {
        var entrada = new TermoVisionInbox(_pasta);
        foreach (var n in new[] { "cv_a", "cv_b", "cv_c" })
        {
            entrada.Deposit(Endereco.Replace("cv_1", n));
            Thread.Sleep(2); // o nome começa pela hora em milissegundos
        }

        Assert.Equal(["cv_a", "cv_b", "cv_c"], entrada.Take(DateTime.UtcNow, out _).Select(l => l.Shipment));
    }
}
