using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RenomearCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_RENOMEAR (7.5): blocos nossos que chegaram de outro desenho com
/// sufixo do AutoCAD ("CLIVUS_PILAR$0$") voltam ao padrão do plugin.
/// Se o bloco padrão existe, as referências passam para ele e a definição
/// com sufixo é apagada; se não existe, a definição é renomeada. Só pelo
/// comando, nunca sozinho: renomear bloco do usuário sem ele pedir é
/// proibido (CLAUDE.md).
/// </summary>
public static class RenomearCommands
{
    private static readonly Regex ComSufixo = new(
        "^(?<padrao>" + Regex.Escape(PluginInfo.PrefixoDeDados) + @"_[^$]+)\$\d+\$$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [CommandMethod(PluginInfo.ComandoRenomear)]
    public static void Renomear()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var (renomeados, trocados, apagados) = Normalizar(documento.Database);

            editor.WriteMessage(
                renomeados + apagados == 0
                    ? Tr.T("\nRENOMEAR Nenhum bloco do plugin com sufixo de outro desenho.\n")
                    : Tr.F("\nRENOMEAR {0} definição(ões) renomeada(s), {1} referência(s) trocada(s) para o bloco padrão, {2} definição(ões) apagada(s).\n", renomeados, trocados, apagados));

            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao renomear os blocos.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui renomear os blocos: {0}\n", erro.Message));
        }
    }

    internal static (int Renomeados, int Trocados, int Apagados) Normalizar(Database database)
    {
        var renomeados = 0;
        var trocados = 0;
        var apagados = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var comSufixo = new List<(ObjectId Id, string Nome, string Padrao)>();

        foreach (ObjectId id in tabela)
        {
            var definicao = (BlockTableRecord)transacao.GetObject(id, OpenMode.ForRead);
            var m = ComSufixo.Match(definicao.Name);

            if (m.Success) comSufixo.Add((id, definicao.Name, m.Groups["padrao"].Value));
        }

        foreach (var (id, _, padrao) in comSufixo)
        {
            var definicao = (BlockTableRecord)transacao.GetObject(id, OpenMode.ForWrite);

            // Has devolve verdadeiro para registro apagado (PURGE, ou este
            // próprio comando): conferir.
            if (!tabela.Has(padrao) || tabela[padrao].IsErased)
            {
                definicao.Name = padrao;
                renomeados++;
                continue;
            }

            var alvo = tabela[padrao];

            foreach (ObjectId refId in definicao.GetBlockReferenceIds(true, true))
            {
                if (refId.IsErased) continue;

                var referencia = (BlockReference)transacao.GetObject(refId, OpenMode.ForWrite);
                referencia.BlockTableRecord = alvo;
                trocados++;
            }

            definicao.Erase();
            apagados++;
        }

        transacao.Commit();

        return (renomeados, trocados, apagados);
    }
}
