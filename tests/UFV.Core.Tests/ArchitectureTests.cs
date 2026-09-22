using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UFV.Core.Tests;

/// <summary>
/// Barreira de arquitetura de 02-arquitetura.md: so UFV.Plugin sabe que o
/// AutoCAD existe. Todo o resto e C# puro.
///
/// A checagem cobre tres lugares, porque cada um sozinho tem furo:
///
/// 1. Os .csproj, que sao a fonte de verdade da intencao. Pegam a referencia
///    adicionada por engano antes de alguem usar um tipo dela.
/// 2. Os .props e .targets da raiz, que valem para todos os projetos de uma vez.
///    E justamente ali que mora Civil3DPath, e e ali que alguem vai "resolver"
///    um erro de build sem perceber que derrubou a barreira.
/// 3. As assemblies compiladas, que pegam o que de fato entrou no binario,
///    inclusive por caminho indireto.
///
/// A lista de projetos cobertos nao e fixa: e todo .csproj do repositorio menos
/// UFV.Plugin. Projeto novo ja nasce dentro da barreira.
/// </summary>
public class ArchitectureTests
{
    /// <summary>O unico projeto autorizado a referenciar CAD.</summary>
    private const string ProjetoDoPlugin = "UFV.Plugin";

    /// <summary>
    /// As assemblies gerenciadas do AutoCAD/Civil 3D terminam em Mgd
    /// (AcCoreMgd, AcDbMgd, AcMgd, AcTcMgd, AeccDbMgd, AcMPolygonMGD), fora as
    /// da interface (AdWindows, AcWindows) e as do namespace Autodesk.
    /// O padrao evita o falso-positivo de casar com qualquer nome que comece
    /// por "Ac", como Accord ou AccessControl.
    /// </summary>
    private static readonly Regex NomeDeCad = new(
        @"^(Ac|Aecc)[A-Za-z0-9.]*Mgd$|^(AdWindows|AcWindows|AcCui|AcTcMgd)$|^Autodesk\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static TheoryData<string> ProjetosSemCad
    {
        get
        {
            var dados = new TheoryData<string>();
            foreach (var projeto in TodosOsProjetos().Keys.Where(n => n != ProjetoDoPlugin))
                dados.Add(projeto);
            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(ProjetosSemCad))]
    [Trait("Etapa", "0")]
    public void CsprojNaoDeclaraCad(string nomeDoProjeto)
    {
        var (referencias, caminhos) = ProcurarCad(XDocument.Load(TodosOsProjetos()[nomeDoProjeto]));

        Assert.True(
            referencias.Length == 0 && caminhos.Length == 0,
            $"{nomeDoProjeto}.csproj declara CAD: referencias [{string.Join(", ", referencias)}], "
            + $"caminhos [{string.Join(", ", caminhos)}]. "
            + $"So {ProjetoDoPlugin} pode referenciar AutoCAD ou Civil 3D (ver 02-arquitetura.md).");
    }

    [Theory]
    [MemberData(nameof(ArquivosDeBuildCompartilhados))]
    [Trait("Etapa", "0")]
    public void ArquivoDeBuildCompartilhadoNaoDeclaraCad(string caminhoRelativo)
    {
        // Civil3DPath vive aqui e e so um texto; o que nao pode e uma
        // Reference ou PackageReference de CAD, que valeria para todo projeto.
        var documento = XDocument.Load(Path.Combine(Repositorio.Raiz, caminhoRelativo));

        var referencias = documento
            .Descendants()
            .Where(e => e.Name.LocalName is "Reference" or "PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(n => NomeDeCad.IsMatch(n))
            .ToArray();

        Assert.True(
            referencias.Length == 0,
            $"{caminhoRelativo} declara referencia de CAD [{string.Join(", ", referencias)}]. "
            + $"Isso vale para todos os projetos e derruba a barreira. Declare dentro de {ProjetoDoPlugin}.csproj.");
    }

    public static TheoryData<string> ArquivosDeBuildCompartilhados
    {
        get
        {
            var raiz = Repositorio.Raiz;
            var dados = new TheoryData<string>();
            var achou = false;

            foreach (var padrao in new[] { "*.props", "*.targets" })
                foreach (var arquivo in Directory.EnumerateFiles(raiz, padrao, SearchOption.TopDirectoryOnly))
                {
                    dados.Add(Path.GetFileName(arquivo));
                    achou = true;
                }

            // Sem nenhum .props na raiz o Theory ficaria sem dados e o xUnit
            // reclamaria; um marcador mantem o teste declarado e verde.
            if (!achou) dados.Add("Directory.Build.props");

            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(ProjetosSemCad))]
    [Trait("Etapa", "0")]
    public void AssemblyNaoCarregaCad(string nomeDoProjeto)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, nomeDoProjeto + ".dll");

        // Nem todo projeto do repositorio chega ao bin deste projeto de teste
        // (o Plugin, por exemplo, e excluido; o Cli nao e referenciado).
        // Quem nao esta aqui ja foi conferido pelo .csproj.
        if (!File.Exists(caminho)) return;

        var proibidas = Assembly.LoadFrom(caminho)
            .GetReferencedAssemblies()
            .Select(r => r.Name ?? string.Empty)
            .Where(n => NomeDeCad.IsMatch(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            proibidas.Length == 0,
            $"A assembly {nomeDoProjeto} carrega referencia de CAD: {string.Join(", ", proibidas)}.");
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void GrafoDeDependenciasEOEsperado()
    {
        // UFV.Plugin -> UFV.Core -> UFV.Geo
        // UFV.Cli    -> UFV.Core -> UFV.Geo
        Assert.Equal(["UFV.Geo"], ProjetosReferenciadosPor("UFV.Core"));
        Assert.Empty(ProjetosReferenciadosPor("UFV.Geo"));
        Assert.Equal(["UFV.Core"], ProjetosReferenciadosPor("UFV.Cli"));
        Assert.Equal(["UFV.Core"], ProjetosReferenciadosPor(ProjetoDoPlugin));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void NinguemReferenciaOPlugin()
    {
        var quemReferencia = TodosOsProjetos().Keys
            .Where(p => p != ProjetoDoPlugin)
            .Where(p => ProjetosReferenciadosPor(p).Contains(ProjetoDoPlugin))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            quemReferencia.Length == 0,
            $"Nada pode apontar para {ProjetoDoPlugin}, mas [{string.Join(", ", quemReferencia)}] aponta.");
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void PluginReferenciaAsDllsDoCivil3D()
    {
        var referencias = XDocument.Load(TodosOsProjetos()[ProjetoDoPlugin])
            .Descendants()
            .Where(e => e.Name.LocalName == "Reference")
            .ToArray();

        var nomes = referencias
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // AdWindows entra no passo 0.4, por causa da ribbon.
        Assert.Equal(["AcCoreMgd", "AcDbMgd", "AcMgd", "AdWindows", "AeccDbMgd"], nomes);

        // Copy Local = false: o AutoCAD ja tem essas assemblies carregadas, e
        // uma segunda copia na pasta de saida quebra o carregamento do plugin.
        foreach (var referencia in referencias)
        {
            var nome = (string?)referencia.Attribute("Include");
            var privado = referencia.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Private")?.Value;

            Assert.True(
                string.Equals(privado, "false", StringComparison.OrdinalIgnoreCase),
                $"A referencia {nome} precisa de <Private>false</Private>, mas esta '{privado ?? "ausente"}'.");
        }
    }

    // ---- apoio -------------------------------------------------------------

    private static (string[] Referencias, string[] Caminhos) ProcurarCad(XDocument projeto)
    {
        var referencias = projeto
            .Descendants()
            .Where(e => e.Name.LocalName is "Reference" or "PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(n => NomeDeCad.IsMatch(n))
            .ToArray();

        var caminhos = projeto
            .Descendants()
            .Where(e => e.Name.LocalName is "HintPath" or "Civil3DPath")
            .Select(e => e.Value)
            .Where(v => v.Contains("Autodesk", StringComparison.OrdinalIgnoreCase)
                     || v.Contains("AutoCAD", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return (referencias, caminhos);
    }

    /// <summary>
    /// Todo .csproj do repositorio, por nome. A varredura pula bin e obj, onde
    /// o NuGet deixa copias de projeto que nao sao codigo nosso.
    /// </summary>
    private static Dictionary<string, string> TodosOsProjetos()
    {
        var raiz = Repositorio.Raiz;

        return Directory
            .EnumerateFiles(raiz, "*.csproj", SearchOption.AllDirectories)
            .Where(c => !c.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !c.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToDictionary(c => Path.GetFileNameWithoutExtension(c)!, c => c, StringComparer.Ordinal);
    }

    private static string[] ProjetosReferenciadosPor(string nomeDoProjeto)
    {
        var projetos = TodosOsProjetos();

        Assert.True(projetos.ContainsKey(nomeDoProjeto), $"{nomeDoProjeto}.csproj nao encontrado no repositorio.");

        return XDocument.Load(projetos[nomeDoProjeto])
            .Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include") ?? string.Empty)!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }
}
