using System.Diagnostics;

namespace UFV.Core.Tests;

/// <summary>
/// A decisão "esta versão do Civil 3D serve?" é o coração do instalador, e
/// errar nela custa caro: ou ele recusa uma máquina boa, ou instala num CAD
/// onde o plugin não carrega e o usuário fica sem saber por quê.
///
/// O instalador aceita -SimularSerie e -SimularCivil3D justamente para essa
/// decisão poder ser exercitada aqui, sem depender do que está instalado na
/// máquina que roda o teste.
///
/// Os códigos de saída são o contrato:
///   0 compatível   2 versão errada   3 AutoCAD sem Civil 3D
///   4 nada instalado   5 CAD aberto   6 série em formato desconhecido
/// </summary>
public class InstaladorTests
{
    private const int Compativel = 0;
    private const int VersaoIncompativel = 2;
    private const int SemCivil3D = 3;
    private const int SerieIlegivel = 6;

    [Theory]
    [InlineData("R25.1")]  // AutoCAD/Civil 3D 2026: a série do PackageContents.xml
    [Trait("Etapa", "0")]
    public void SerieSuportadaEAceita(string serie)
    {
        var (codigo, saida) = Verificar(serie);

        Assert.True(codigo == Compativel, $"Esperava aceitar {serie}, mas saiu {codigo}. Saída:\n{saida}");
    }

    [Theory]
    [InlineData("R24.3")]  // 2024
    [InlineData("R25.0")]  // 2025
    [InlineData("R26.0")]  // futura, ainda não testada
    [InlineData("R20.1")]  // bem antiga
    [Trait("Etapa", "0")]
    public void SerieForaDaFaixaERecusada(string serie)
    {
        var (codigo, saida) = Verificar(serie);

        Assert.True(
            codigo == VersaoIncompativel,
            $"Esperava recusar {serie} com {VersaoIncompativel}, mas saiu {codigo}. Saída:\n{saida}");

        // Só o código não basta: série ilegível também sairia com outro número,
        // e um erro de leitura poderia se disfarçar de "versão errada".
        Assert.Contains("esta maquina tem", saida, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("25.1")]     // sem o R
    [InlineData("R")]        // só o R
    [InlineData("2026")]     // o ano, não a série
    [InlineData("XPTO")]
    [Trait("Etapa", "0")]
    public void SerieEmFormatoDesconhecidoNaoSeDisfarcaDeVersaoErrada(string serie)
    {
        // "Não entendi o que está instalado" e "está instalada a versão errada"
        // mandam o usuário fazer coisas diferentes.
        var (codigo, saida) = Verificar(serie);

        Assert.True(
            codigo == SerieIlegivel,
            $"Esperava {SerieIlegivel} para '{serie}', mas saiu {codigo}. Saída:\n{saida}");

        Assert.Contains("formato desconhecido", saida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void VersaoFuturaNaoEAceitaPorOtimismo()
    {
        // 02-arquitetura.md: o projeto trava na versão instalada, e versão nova
        // vira atualização. Carregar numa versão não testada é pior que não
        // carregar, porque o erro aparece no meio do trabalho.
        var (codigo, _) = Verificar("R26.1");

        Assert.Equal(VersaoIncompativel, codigo);
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void AutoCadPuroNaSerieCertaERecusado()
    {
        // O plugin lê a superfície TIN, que só existe no Civil 3D. Instalar
        // aqui carregaria, e quebraria no primeiro comando útil.
        var (codigo, saida) = Verificar("R25.1", temCivil3D: false);

        Assert.True(codigo == SemCivil3D, $"Esperava {SemCivil3D}, mas saiu {codigo}. Saída:\n{saida}");
        Assert.Contains("Civil 3D", saida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void AMensagemDeRecusaDizOQueTemEOQuePrecisa()
    {
        var (_, saida) = Verificar("R24.3");

        // A asserção tem que cair em texto exclusivo do bloco de recusa: o
        // "R24.3" e o "R25.1" sozinhos aparecem no cabeçalho, impresso antes
        // de qualquer decisão, e o teste passaria com a recusa apagada.
        var recusa = saida[saida.IndexOf("esta maquina tem", StringComparison.OrdinalIgnoreCase)..];

        Assert.Contains("R24.3", recusa, StringComparison.Ordinal);
        Assert.Contains("R25.1", recusa, StringComparison.Ordinal);
    }

    // ---- apoio -------------------------------------------------------------

    private static (int Codigo, string Saida) Verificar(string serie, bool temCivil3D = true)
    {
        var script = Repositorio.Caminho("tools", "instalar.ps1");
        Assert.True(File.Exists(script), $"instalar.ps1 não encontrado em {script}.");

        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(script);
        info.ArgumentList.Add("-SomenteVerificar");
        info.ArgumentList.Add("-SimularSerie");
        info.ArgumentList.Add(serie);
        info.ArgumentList.Add("-SimularCivil3D");
        info.ArgumentList.Add(temCivil3D ? "1" : "0");

        // Sem -Bundle, o instalador leria o PackageContents.xml de
        // artefatos/UFV.bundle quando ele existisse: o teste passaria a
        // conferir uma cópia velha na máquina do desenvolvedor e o arquivo do
        // repositório na integração contínua. Apontamos para uma pasta que não
        // existe, e ele cai no arquivo-fonte sempre.
        info.ArgumentList.Add("-Bundle");
        info.ArgumentList.Add(Path.Combine(Path.GetTempPath(), "ufv-bundle-inexistente"));

        using var processo = Process.Start(info);
        Assert.True(processo is not null, "Não consegui iniciar o powershell.exe.");

        // Os dois fluxos são lidos em paralelo: ler um até o fim e só depois o
        // outro trava se o segundo encher o buffer do pipe.
        var lendoSaida = processo!.StandardOutput.ReadToEndAsync();
        var lendoErro = processo.StandardError.ReadToEndAsync();

        Assert.True(processo.WaitForExit(60_000), "O instalador não terminou em 60 s.");

        return (processo.ExitCode, lendoSaida.Result + lendoErro.Result);
    }
}
