using System.Xml.Linq;

namespace UFV.Core.Tests;

/// <summary>
/// O PackageContents.xml e o que faz o Civil 3D carregar o plugin sozinho.
/// Errar um caminho ou uma versao ali nao quebra a compilacao: o plugin
/// simplesmente nao aparece, e a causa custa a ser achada. Estes testes leem o
/// arquivo direto do repositorio, sem CAD nenhum.
/// </summary>
public class BundleTests
{
    private const string VersaoEsperada = "0.1.0";

    [Fact]
    [Trait("Etapa", "0")]
    public void ModuleNameApontaParaADllDoPlugin()
    {
        var entrada = ComponentEntry();
        Assert.Equal("./Contents/UFV.Plugin.dll", (string?)entrada.Attribute("ModuleName"));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void CarregaNaAberturaSemPrecisarDeComando()
    {
        var entrada = ComponentEntry();

        // O objetivo do passo 0.4 e a aba aparecer sem NETLOAD.
        Assert.Equal("True", (string?)entrada.Attribute("LoadOnAutoCADStartup"));
        Assert.Equal("False", (string?)entrada.Attribute("LoadOnCommandInvocation"));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void VersaoDoPacoteBateComADoBuild()
    {
        // Tres lugares dizem a versao: Directory.Build.props, o atributo
        // AppVersion e o Version do ComponentEntry. Divergir e o caminho curto
        // para o instalador atualizar uma coisa e deixar outra para tras.
        var pacote = Pacote();

        Assert.Equal(VersaoEsperada, (string?)pacote.Attribute("AppVersion"));
        Assert.Equal(VersaoEsperada, (string?)ComponentEntry().Attribute("Version"));
        Assert.Equal(
            VersaoEsperada,
            XDocument.Load(Path.Combine(RaizDoRepositorio(), "Directory.Build.props"))
                .Descendants()
                .First(e => e.Name.LocalName == "Version")
                .Value);
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void TravaNaSerieDoCivil3D2026()
    {
        // R25.1 e o AutoCAD/Civil 3D 2026. 02-arquitetura.md trava o projeto na
        // versao instalada: carregar numa versao nao testada e pior que nao
        // carregar.
        var requisitos = Pacote()
            .Descendants()
            .First(e => e.Name.LocalName == "RuntimeRequirements");

        Assert.Equal("R25.1", (string?)requisitos.Attribute("SeriesMin"));
        Assert.Equal("R25.1", (string?)requisitos.Attribute("SeriesMax"));
        Assert.Equal("Win64", (string?)requisitos.Attribute("OS"));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void ProductCodeEUmGuidEntreChaves()
    {
        var codigo = (string?)Pacote().Attribute("ProductCode");

        Assert.False(string.IsNullOrWhiteSpace(codigo));
        Assert.StartsWith("{", codigo);
        Assert.EndsWith("}", codigo);
        Assert.True(Guid.TryParse(codigo, out _), $"ProductCode '{codigo}' nao e um GUID.");
    }

    // ---- apoio -------------------------------------------------------------

    private static XElement Pacote() =>
        XDocument.Load(Path.Combine(RaizDoRepositorio(), "src", "UFV.Plugin", "PackageContents.xml")).Root!;

    private static XElement ComponentEntry() =>
        Pacote().Descendants().First(e => e.Name.LocalName == "ComponentEntry");

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UFV.sln")))
            dir = dir.Parent;

        Assert.True(dir is not null, $"UFV.sln nao encontrada acima de {AppContext.BaseDirectory}.");
        return dir!.FullName;
    }
}
