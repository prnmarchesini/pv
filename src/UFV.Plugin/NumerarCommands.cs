using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.NumerarCommands))]

namespace UFV.Plugin;

/// <summary>
/// A numeração (7.10): UFV_NUMERAR lê as células de toda mesa do desenho,
/// pede a mesa que será a F1.1 e uma mesa da última fileira, e regrava os
/// letreiros (o XData do contorno e o aviso "NÃO CABE", que traz o
/// letreiro). Pilar e módulo têm número dentro da mesa, e a mesa é a
/// mesma: seguem como estão. A conta é do <see cref="RowNumbering"/>.
/// </summary>
public static class NumerarCommands
{
    /// <summary>UFV_NUMERAR: gera a numeração de todas as mesas do desenho.</summary>
    [CommandMethod(PluginInfo.ComandoNumerar)]
    public static void Numerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            NumerarPorCliques(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao numerar.", erro);
            editor.WriteMessage($"\nNão consegui numerar: {erro.Message}\n");
        }
    }

    /// <summary>
    /// Pede a primeira mesa (F1.1) e uma da última fileira e numera tudo.
    /// Se numerou. É também o começo do Inserir das tags de fileira (o Renan
    /// escolhe a primeira e a última fileira ali).
    /// </summary>
    internal static bool NumerarPorCliques(Editor editor, Document documento)
    {
        {
            var settings = ConfigCommands.Inicial(documento, out var avisoDaConfig);
            if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

            var celulas = Celulas(editor, documento);
            if (celulas is null) return false;

            editor.WriteMessage($"\nNUMERAR {celulas.Count} mesa(s) no desenho.\n");

            var primeira = MesaEscolhida(editor, documento, celulas, "Uma mesa da PRIMEIRA fileira (será a F1.1)");
            if (primeira is null) return false;

            var ultima = MesaEscolhida(editor, documento, celulas, "Uma mesa da ÚLTIMA fileira");
            if (ultima is null) return false;

            var resultado = RowNumbering.Number(
                celulas.Select(c => new TableToNumber(c.Key, c.Value.Celula)).ToList(),
                primeira.Value, ultima.Value, settings.Configuration.MaxGapBeforeBreak);

            var trocados = Regravar(documento, celulas, resultado);

            editor.WriteMessage(
                $"\nNUMERAR {resultado.RowCount} fileira(s), {resultado.Tables.Count} mesa(s), {trocados} letreiro(s) trocado(s).\n");

            foreach (var aviso in resultado.Warnings) editor.WriteMessage($"  ATENÇÃO: {aviso}.\n");

            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
            return true;
        }
    }

    /// <summary>A mesa como está no desenho e a célula reconstruída do contorno.</summary>
    internal sealed record MesaLida(TableParts Partes, PlacedTable Celula);

    /// <summary>
    /// Toda mesa do desenho com a célula. Nulo, com mensagem, se alguma mesa
    /// não dá para ler: numerar metade daria letreiro em dobro.
    /// </summary>
    private static Dictionary<Guid, MesaLida>? Celulas(Editor editor, Document documento)
    {
        var lidas = new Dictionary<Guid, MesaLida>();

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);

        if (mesas.Count == 0)
        {
            editor.WriteMessage("\nNUMERAR Não há mesa do plugin neste desenho.\n");
            return null;
        }

        foreach (var (guid, partes) in mesas)
        {
            if (partes.Identity is null || partes.Contour is not { } contorno)
            {
                // Peças cujo contorno foi apagado à mão (órfãs do 7.7): não
                // há letreiro para regravar. Segue sem elas.
                editor.WriteMessage($"\n  ATENÇÃO: {partes.All.Count()} peça(s) sem contorno de mesa (GUID {guid:D}) ficam fora da numeração; o Validar lista as órfãs.\n");
                continue;
            }

            if (partes.IsDuplicated)
            {
                editor.WriteMessage(
                    $"\nNUMERAR {partes.Identity.Label} tem {partes.Contours.Count} contornos com a mesma identidade (mesa copiada e colada). "
                    + "Apague a cópia, ou use o Refazer da área.\n");
                return null;
            }

            var cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao);

            try
            {
                // Como está desenhada: numerar não depende do perfil atual.
                lidas[guid] = new MesaLida(partes, TableCells.FromDrawnCorners(cantos, partes.Identity.Label));
            }
            catch (ArgumentException erro)
            {
                editor.WriteMessage($"\nNUMERAR {partes.Identity.Label}: {erro.Message} Use o Refazer da área.\n");
                return null;
            }
        }

        if (lidas.Count == 0)
        {
            editor.WriteMessage("\nNUMERAR Nenhuma mesa com contorno para numerar.\n");
            return null;
        }

        return lidas;
    }

    /// <summary>
    /// Pede uma mesa: clique numa peça, ou a opção Letreiro e o letreiro
    /// atual (F3.2). Nulo se o usuário desistiu.
    /// </summary>
    private static Guid? MesaEscolhida(Editor editor, Document documento, Dictionary<Guid, MesaLida> celulas, string qual)
    {
        while (true)
        {
            var opcoes = new PromptEntityOptions($"\n{qual}: clique numa peça ou [Letreiro]", "Letreiro");
            opcoes.SetRejectMessage("\nIsso não é uma peça de mesa do plugin.");
            opcoes.AddAllowedClass(typeof(Entity), false);
            opcoes.AllowNone = false;

            var resposta = editor.GetEntity(opcoes);

            if (resposta.Status == PromptStatus.Keyword)
            {
                var letreiro = editor.GetString(new PromptStringOptions($"\n{qual}: letreiro atual (como F1.1): ") { AllowSpaces = false });
                if (letreiro.Status != PromptStatus.OK) return null;

                var achadas = celulas.Where(c => string.Equals(c.Value.Partes.Identity!.Label, letreiro.StringResult.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

                if (achadas.Count == 1) return achadas[0].Key;

                editor.WriteMessage(achadas.Count == 0
                    ? $"\nNUMERAR Não há mesa com o letreiro \"{letreiro.StringResult}\".\n"
                    : $"\nNUMERAR Há {achadas.Count} mesas com o letreiro \"{letreiro.StringResult}\"; clique na que você quer.\n");
                continue;
            }

            if (resposta.Status != PromptStatus.OK) return null;

            Guid? guid;

            using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
                guid = LayoutScan.TableOf((Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead));

            if (guid is { } g && celulas.ContainsKey(g)) return g;

            editor.WriteMessage("\nNUMERAR Isso não é uma peça de mesa do plugin.\n");
        }
    }

    /// <summary>Regrava o letreiro nas mesas que mudaram: o XData do contorno e o aviso "NÃO CABE". Quantas mudaram.</summary>
    private static int Regravar(Document documento, Dictionary<Guid, MesaLida> celulas, NumberingResult resultado)
    {
        var trocados = 0;

        using var transacao = documento.Database.TransactionManager.StartTransaction();

        foreach (var numerada in resultado.Tables)
        {
            var mesa = celulas[numerada.Id];
            var identidade = mesa.Partes.Identity!;

            if (identidade.Label == numerada.Label) continue;

            foreach (var id in mesa.Partes.Contours)
            {
                var contorno = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
                LayoutXData.SaveTable(transacao, contorno, identidade with { Label = numerada.Label });
            }

            foreach (var id in mesa.Partes.Notes)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not MText texto) continue;

                // Tags do 8.14 seguem o letreiro novo: a da mesa vira o
                // letreiro, a da fileira o "F" do número novo.
                if (LayoutXData.LoadTag(texto) is { Kind: TagKind.Table or TagKind.Row } tag)
                {
                    var novo = tag.Kind == TagKind.Table ? numerada.Label : $"F{numerada.Row}";
                    texto.UpgradeOpen();
                    texto.Contents = novo;
                    LayoutXData.SaveTag(transacao, texto, tag with { Text = novo });
                    continue;
                }

                if (!texto.Contents.StartsWith(identidade.Label + " ", StringComparison.Ordinal)) continue;

                texto.UpgradeOpen();
                texto.Contents = numerada.Label + texto.Contents[identidade.Label.Length..];
            }

            trocados++;
        }

        transacao.Commit();

        return trocados;
    }
}
