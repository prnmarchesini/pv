using System.Xml.Linq;
using System.Xml.XPath;

namespace Clivus.Core.Tests;

/// <summary>
/// O PackageContents.xml e o que faz o Civil 3D carregar o plugin sozinho.
/// Errar um caminho ou uma versao ali nao quebra a compilacao: o plugin
/// simplesmente nao aparece, e a causa custa a ser achada. Estes testes leem o
/// arquivo direto do repositorio, sem CAD nenhum.
/// </summary>
public class BundleTests
{
    [Fact]
    [Trait("Etapa", "0")]
    public void ModuleNameApontaParaADllDoPlugin()
    {
        var entrada = ComponentEntry();
        Assert.Equal("./Contents/Clivus.Plugin.dll", (string?)entrada.Attribute("ModuleName"));
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
        // Tres lugares diziam a versao: Directory.Build.props, o atributo
        // AppVersion e o Version do ComponentEntry (e este teste, o quarto).
        // Divergir e o caminho curto para o instalador atualizar uma coisa e
        // deixar outra para tras. Desde a etapa 43 a versao mora so no
        // Directory.Build.props: o PackageContents.xml do repositorio traz
        // $(Version) nos dois atributos, e o build grava na saida a copia com o
        // numero (alvo CarimbarPackageContents), que e a que vai para o bundle.
        var versao = VersaoDoBuild();
        Assert.Matches(@"^\d+\.\d+\.\d+$", versao);
        Assert.Equal(versao, SemanticVersion.Parse(versao)?.ToString());

        // Nenhum numero escrito a mao no descritor do repositorio.
        Assert.Equal("$(Version)", (string?)Pacote().Attribute("AppVersion"));
        Assert.Equal("$(Version)", (string?)ComponentEntry().Attribute("Version"));

        // O alvo do build troca exatamente os dois atributos pela versao: as
        // consultas dele, rodadas aqui no descritor, acham um atributo cada,
        // com o $(Version), e juntas cobrem os dois.
        var alvo = XDocument.Load(Repositorio.Caminho("src", "Clivus.Plugin", "Clivus.Plugin.csproj"))
            .Descendants()
            .Single(e => e.Name.LocalName == "Target" && (string?)e.Attribute("Name") == "CarimbarPackageContents");

        Assert.Equal("Build", (string?)alvo.Attribute("AfterTargets"));

        var trocas = alvo.Elements().Where(e => e.Name.LocalName == "XmlPoke").ToList();
        Assert.Equal(2, trocas.Count);

        var descritor = XDocument.Load(Repositorio.Caminho("src", "Clivus.Plugin", "PackageContents.xml"));
        var trocados = new List<XAttribute>();

        foreach (var troca in trocas)
        {
            Assert.Equal("$(Version)", (string?)troca.Attribute("Value"));
            Assert.Equal("$(OutDir)PackageContents.xml", (string?)troca.Attribute("XmlInputPath"));

            var achados = ((IEnumerable<object>)descritor.XPathEvaluate((string)troca.Attribute("Query")!)).OfType<XAttribute>().ToList();
            var achado = Assert.Single(achados);
            Assert.Equal("$(Version)", achado.Value);
            trocados.Add(achado);
        }

        Assert.Contains(trocados, a => a.Parent == descritor.Root && a.Name.LocalName == "AppVersion");
        Assert.Contains(trocados, a => a.Parent?.Name.LocalName == "ComponentEntry" && a.Name.LocalName == "Version");
    }

    /// <summary>A versao do Directory.Build.props, a unica fonte (etapa 43).</summary>
    private static string VersaoDoBuild() =>
        XDocument.Load(Repositorio.Caminho("Directory.Build.props"))
            .Descendants()
            .First(e => e.Name.LocalName == "Version")
            .Value;

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
        XDocument.Load(Repositorio.Caminho("src", "Clivus.Plugin", "PackageContents.xml")).Root!;

    private static XElement ComponentEntry() =>
        Pacote().Descendants().First(e => e.Name.LocalName == "ComponentEntry");
}
