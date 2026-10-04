using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.DeclividadeCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Seção Análises: a declividade de cada mesa, com seta e valor (Renan,
/// 29/09/2026: "quero uma flecha e a indicação em % ou graus, eu decido qual
/// unidade"). O comando pergunta a unidade, grava no desenho, apaga as setas
/// que houver e desenha de novo em todas as mesas, a partir do contorno de
/// cada uma. "Desligar" apaga as setas e grava desligada: mesa nova ou
/// recalculada deixa de nascer com seta.
/// </summary>
public static class DeclividadeCommands
{
    private const string Porcentagem = "Porcentagem";
    private const string Graus = "Graus";
    private const string Desligar = "Desligar";

    /// <summary>CLIVUS_DECLIVIDADE: [Porcentagem/Graus/Desligar].</summary>
    [CommandMethod(PluginInfo.ComandoDeclividade)]
    public static void Declividade()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var (ligada, unidade) = SetaDeDeclividade.Ler(documento.Database);

            var opcoes = new PromptKeywordOptions("\nDeclividade das mesas, em que unidade? [Porcentagem/Graus/Desligar]");
            opcoes.Keywords.Add(Porcentagem);
            opcoes.Keywords.Add(Graus);
            opcoes.Keywords.Add(Desligar);
            opcoes.Keywords.Default = ligada ? (unidade == SlopeUnit.Degrees ? Graus : Porcentagem) : Porcentagem;
            opcoes.AllowNone = true;

            var resposta = editor.GetKeywords(opcoes);
            if (resposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;

            var escolha = string.IsNullOrEmpty(resposta.StringResult) ? opcoes.Keywords.Default : resposta.StringResult;

            if (escolha == Desligar)
            {
                SetaDeDeclividade.Gravar(documento.Database, false, unidade);
                var apagadas = Apagar(documento.Database);
                editor.WriteMessage($"\nDECLIVIDADE desligada: {apagadas} entidade(s) apagada(s) da camada {LayoutLayers.SetaDeclividade}.\n");
                editor.Regen();
                return;
            }

            var nova = escolha == Graus ? SlopeUnit.Degrees : SlopeUnit.Percent;
            SetaDeDeclividade.Gravar(documento.Database, true, nova);

            var (mesas, entidades) = Regerar(documento.Database, nova);

            editor.WriteMessage(
                $"\nDECLIVIDADE em {(nova == SlopeUnit.Degrees ? "graus" : "porcentagem")}: seta e valor em {mesas} mesa(s) "
                + $"({entidades} entidade(s)), na camada {LayoutLayers.SetaDeclividade}. A seta aponta para onde a mesa desce.\n");

            editor.Regen();
            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na análise de declividade.", erro);
            editor.WriteMessage($"\nNão consegui desenhar a declividade: {erro.Message}\n");
        }
    }

    private static readonly RXClass ClasseDoTexto = RXObject.GetClass(typeof(MText));
    private static readonly RXClass ClasseDaLinha = RXObject.GetClass(typeof(Line));

    /// <summary>
    /// Apaga o que é nosso na camada das setas: nota com identidade, ou texto
    /// e linha sem XData (sobra de seta antiga). Outra coisa na camada é do
    /// usuário e fica. Quantas.
    /// </summary>
    internal static int Apagar(Database database)
    {
        var apagadas = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDoTexto && id.ObjectClass != ClasseDaLinha) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;
            if (!string.Equals(entidade.Layer, LayoutLayers.SetaDeclividade, StringComparison.OrdinalIgnoreCase)) continue;

            bool semXData;
            using (var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo))
                semXData = dados is null;

            if (!semXData && LayoutXData.LoadNote(entidade) is null) continue;

            entidade.UpgradeOpen();
            entidade.Erase();
            apagadas++;
        }

        transacao.Commit();

        return apagadas;
    }

    /// <summary>Apaga as setas e desenha de novo em todas as mesas, pelo contorno. Quantas mesas e entidades.</summary>
    internal static (int Mesas, int Entidades) Regerar(Database database, SlopeUnit unidade)
    {
        Apagar(database);

        var mesas = 0;
        var entidades = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var camada = SetaDeDeclividade.Camada(transacao, database);

        LayoutLayers.Ligar(transacao, database, camada, true);

        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);

        foreach (var (guid, mesa) in LayoutScan.Tables(transacao, database))
        {
            if (mesa.Identity is null || mesa.Contour is not { } contorno || mesa.IsDuplicated) continue;
            if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var cantos = FileiraCommands.Vertices(polilinha, transacao);
            var feitas = SetaDeDeclividade.Desenhar(transacao, espaco, camada, guid, cantos, unidade, estilo: estilo);

            if (feitas > 0)
            {
                mesas++;
                entidades += feitas;
            }
        }

        transacao.Commit();

        return (mesas, entidades);
    }
}
