using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.GraficoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Recriar gráfico e Apagar gráfico (pedido de 21/10/2026): nas linhas e nas
/// canaletas escolhidas (ou no gráfico delas). O gráfico apagado não volta
/// quando a linha é mexida; só com Recriar, no mesmo lugar.
/// </summary>
public static class GraficoCommands
{
    /// <summary>CLIVUS_DRN_GRAFICO_RECRIAR: desenha de novo o gráfico, onde ele está (ou estava).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemGraficoRecriar, CommandFlags.UsePickSet)]
    public static void Recriar() => Fazer(apagar: false);

    /// <summary>CLIVUS_DRN_GRAFICO_APAGAR: tira o gráfico; a linha e a canaleta ficam.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemGraficoApagar, CommandFlags.UsePickSet)]
    public static void Apagar() => Fazer(apagar: true);

    private static void Fazer(bool apagar)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var mensagem = apagar
                ? Tr.T("\nEscolha as linhas, canaletas ou gráficos cujo gráfico sai (Enter = todos): ")
                : Tr.T("\nEscolha as linhas, canaletas ou gráficos cujo gráfico é refeito (Enter = todos): ");
            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = mensagem });

            // Enter sem escolher nada: todas.
            if (selecao.Status is not (PromptStatus.OK or PromptStatus.Error)) return;
            var escolhidos = selecao.Status == PromptStatus.OK ? selecao.Value.GetObjectIds() : null;

            var configuracao = DrenagemStore.Ler(documento.Database);
            var feitas = 0;

            using (var transacao = documento.Database.TransactionManager.StartTransaction())
            {
                var todas = LinhasDeDrenagem.Todas(transacao, documento.Database);
                var alvo = escolhidos is null ? todas : Linhas(transacao, todas, escolhidos);
                var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);

                foreach (var linha in alvo)
                {
                    var p = (Polyline3d)transacao.GetObject(linha.Id, OpenMode.ForWrite, false, true);
                    LinhasDeDrenagem.Gravar(transacao, p, linha.Identidade with { ChartHidden = apagar });

                    try
                    {
                        LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno.Mesh, configuracao, linha.Id, indice);
                        feitas++;
                    }
                    catch (ArgumentException erro)
                    {
                        editor.WriteMessage($"\nGRÁFICO {erro.Message}\n");
                    }
                }

                transacao.Commit();
            }

            editor.WriteMessage(apagar
                ? Tr.F("\nGRÁFICO {0} gráfico(s) apagado(s). Recriar gráfico traz de volta no mesmo lugar.\n", feitas)
                : Tr.F("\nGRÁFICO {0} gráfico(s) refeito(s).\n", feitas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recriar ou apagar o gráfico.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer no gráfico: {0}\n", erro.Message));
        }
    }

    /// <summary>As linhas dos objetos escolhidos: a própria linha, ou o gráfico, o 3D ou o rótulo dela.</summary>
    private static List<LinhaNoDesenho> Linhas(Transaction transacao, List<LinhaNoDesenho> todas, IEnumerable<ObjectId> escolhidos)
    {
        var porId = todas.ToDictionary(l => l.Id);
        var porGuid = todas.GroupBy(l => l.Identidade.Id.ToString("D")).ToDictionary(g => g.Key, g => g.First());
        var linhas = new List<LinhaNoDesenho>();

        foreach (var id in escolhidos)
        {
            if (porId.TryGetValue(id, out var linha))
            {
                linhas.Add(linha);
                continue;
            }

            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

            foreach (var tipo in IndiceDaDrenagem.Tipos)
            {
                if (PluginXData.Load(e, tipo, 1, 1) is { } campos && porGuid.TryGetValue(campos[0], out var dona))
                {
                    linhas.Add(dona);
                    break;
                }
            }
        }

        return linhas.DistinctBy(l => l.Id).ToList();
    }
}
