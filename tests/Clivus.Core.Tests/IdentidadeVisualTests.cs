namespace Clivus.Core.Tests;

/// <summary>
/// A identidade visual (04/10/2026): todo botão e menu da ribbon tem ícone
/// próprio, gerado por tools/build_icons.py nos dois temas e nos quatro
/// tamanhos, com o SVG-fonte guardado; e o descritor do bundle aponta o ícone
/// do pacote.
/// </summary>
public class IdentidadeVisualTests
{
    private static readonly string Raiz = AcharRaiz();

    private static string AcharRaiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "ClivusSolar.sln"))) pasta = pasta.Parent;
        return pasta?.FullName ?? throw new InvalidOperationException("não achei a raiz do repositório");
    }

    private static IEnumerable<string> Icones() =>
        RibbonLayout.AllButtons.Select(b => b.Icon)
            .Concat(RibbonLayout.Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Items).OfType<RibbonMenuSpec>().Select(m => m.Icon));

    [Fact]
    [Trait("Etapa", "9")]
    public void CadaBotaoTemIconeProprio()
    {
        var icones = RibbonLayout.AllButtons.Select(b => b.Icon).ToList();
        Assert.Equal(icones.Count, icones.Distinct().Count());
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void TodoIconeExisteNosDoisTemasENosQuatroTamanhos()
    {
        var pasta = Path.Combine(Raiz, "src", "Clivus.Plugin", "Resources", "Icons");

        foreach (var icone in Icones().Distinct())
        {
            foreach (var tema in new[] { "Light", "Dark" })
                foreach (var sufixo in new[] { "_16", "_32", "_16@2x", "_32@2x" })
                    Assert.True(File.Exists(Path.Combine(pasta, tema, $"{icone}{sufixo}.png")), $"falta {tema}/{icone}{sufixo}.png");

            // O "sobre" é o símbolo do Clivus, que mora em Branding.
            if (icone != "sobre")
                Assert.True(File.Exists(Path.Combine(pasta, "src", $"{icone}.svg")), $"falta o SVG-fonte de {icone}");
        }
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OPacoteTemOIconeDoClivus()
    {
        var descritor = File.ReadAllText(Path.Combine(Raiz, "src", "Clivus.Plugin", "PackageContents.xml"));

        Assert.Contains("Icon=\"./Contents/Resources/clivus.ico\"", descritor, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Raiz, "src", "Clivus.Plugin", "Resources", "Branding", "clivus.ico")));
    }
}
