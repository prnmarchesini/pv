using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.FlechasCommands))]

namespace Clivus.Plugin;

/// <summary>Os comandos das flechas (26.2): gerar e apagar, com a configuração gravada pela janela.</summary>
public static class FlechasCommands
{
    private const string Areas = "Areas";
    private const string Janela = "Janela";
    private const string Tudo = "Tudo";

    /// <summary>CLIVUS_DRN_FLECHAS_GERAR: [Areas/Janela/Tudo]; Janela pede os dois cantos.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemFlechasGerar)]
    public static void Gerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var opcoes = new PromptKeywordOptions(Tr.T("\nFlechas onde?"));
            opcoes.Keywords.Add(Areas);
            opcoes.Keywords.Add(Janela);
            opcoes.Keywords.Add(Tudo);
            opcoes.Keywords.Default = Areas;
            opcoes.AllowNone = true;

            var resposta = editor.GetKeywords(opcoes);
            if (resposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;

            var escolha = string.IsNullOrEmpty(resposta.StringResult) ? Areas : resposta.StringResult;
            var configuracao = DrenagemStore.Ler(documento.Database).Arrows;

            IReadOnlyList<IReadOnlyList<Point3>> regioes;

            if (escolha == Janela)
            {
                var primeiro = editor.GetPoint(Tr.T("\nPrimeiro canto: "));
                if (primeiro.Status != PromptStatus.OK) return;

                var segundo = editor.GetCorner(Tr.T("\nCanto oposto: "), primeiro.Value);
                if (segundo.Status != PromptStatus.OK) return;

                regioes = [Retangulo(primeiro.Value, segundo.Value)];
            }
            else
            {
                regioes = FlechasDeDrenagem.Regioes(documento.Database, escolha == Tudo ? OndeFlechas.Tudo : OndeFlechas.Areas);
            }

            editor.WriteMessage($"\n{FlechasDeDrenagem.Gerar(documento.Database, terreno.Mesh, configuracao, regioes)}\n");
            editor.Regen();
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nFLECHAS {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar as flechas da drenagem.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar as flechas: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_DRN_FLECHAS_APAGAR: as flechas e a legenda.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemFlechasApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage(Tr.F("\nFLECHAS {0} entidade(s) apagada(s).\n", FlechasDeDrenagem.Apagar(documento.Database)));
            documento.Editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar as flechas da drenagem.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui apagar as flechas: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// O botão Gerar da janela (Áreas ou Tudo), fora de comando: grava a
    /// configuração e gera. Chamado com o documento travado
    /// (<see cref="EscritaForaDeComando"/>); o nível 2 chama o mesmo.
    /// </summary>
    internal static string GerarPelaJanela(Document documento, OndeFlechas onde, ArrowSettings configuracao)
    {
        var terreno = TerrenoDoDesenho.Garantir(documento);

        if (terreno is null && TerrainCommands.Reprocessar(documento.Editor, documento))
            terreno = TerrainCache.Get(documento);

        if (terreno is null) throw new ArgumentException(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));

        DrenagemStore.Mudar(documento.Database, d => d with { Arrows = configuracao });

        return FlechasDeDrenagem.Gerar(documento.Database, terreno.Mesh, configuracao, FlechasDeDrenagem.Regioes(documento.Database, onde));
    }

    private static IReadOnlyList<Point3> Retangulo(Point3d a, Point3d b)
    {
        var (x0, x1) = (Math.Min(a.X, b.X), Math.Max(a.X, b.X));
        var (y0, y1) = (Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
        return [new Point3(x0, y0, 0), new Point3(x1, y0, 0), new Point3(x1, y1, 0), new Point3(x0, y1, 0)];
    }
}
