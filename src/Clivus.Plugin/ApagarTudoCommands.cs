using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ApagarTudoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Apagar tudo de uma área (pedido do Renan em 29/09/2026, pelo botão
/// direito sobre a área, no submenu Clivus Solar): o que o plugin desenhou dentro
/// dela some (mesas, pilares, módulos, faces, notas), e a área e o
/// alinhamento ficam, prontos para desenhar de novo. É o Refazer sem o
/// redesenho. U desfaz.
///
/// Os grupos acompanham: grupo que perde todas as mesas é apagado com a
/// marca; o que perde só algumas fica com as outras e ganha marca nova.
/// </summary>
public static class ApagarTudoCommands
{
    /// <summary>CLIVUS_APAGAR_TUDO: a área (da seleção, ou escolhida) e apaga o que há dentro.</summary>
    [CommandMethod(PluginInfo.ComandoApagarTudo, CommandFlags.UsePickSet)]
    public static void ApagarTudo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var area = RefazerCommands.AreaDaSelecao(editor, documento) ?? FileiraCommands.EscolherArea(editor, documento);
            if (area is null) return;

            Executar(editor, documento, area.Value);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar tudo da área.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_APAGAR_TUDO_AUTO: a primeira área registrada. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoApagarTudoAutomatico)]
    public static void ApagarTudoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var areas = AreaStore.Load(documento.Database);

            if (areas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nAPAGAR Nenhuma área registrada neste desenho.\n"));
                return;
            }

            var area = FileiraCommands.LerArea(documento.Database, areas[0].Handle);

            if (area is null)
            {
                editor.WriteMessage(Tr.T("\nAPAGAR A área registrada não está mais no desenho.\n"));
                return;
            }

            Executar(editor, documento, area.Value);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar tudo da área automaticamente.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar: {0}\n", erro.Message));
        }
    }

    private static void Executar(Editor editor, Document documento, (IReadOnlyList<Point3> Vertices, string Nome) area)
    {
        var database = documento.Database;

        var apagadas = LayoutEraser.ApagarDentro(database, area.Vertices);
        var orfas = LayoutEraser.ApagarOrfasDentro(database, area.Vertices);

        if (apagadas.Tables.Count == 0 && orfas == 0)
        {
            editor.WriteMessage(Tr.F("\nAPAGAR Nada do plugin dentro de {0}.\n", area.Nome));
            return;
        }

        // Apagadas de propósito: não ficam no registro de removidas para
        // recontar (o vigia cala nos nossos comandos; isto tira as que já
        // estavam lá de antes).
        if (apagadas.Tables.Count > 0) RemovalStore.Remove(database, apagadas.Tables);

        var (gruposApagados, gruposEncolhidos) = AcertarGrupos(documento, apagadas.Tables);

        editor.WriteMessage(
            Tr.F(
                "\nAPAGAR {0} mesa(s) apagada(s) dentro de {1} ({2} entidade(s){3}). A área e o alinhamento ficam. U desfaz.\n",
                apagadas.Tables.Count,
                area.Nome,
                apagadas.Entities,
                orfas > 0 ? ", " + Tr.F("mais {0} nota(s) órfã(s) de desenho antigo", orfas) : string.Empty));

        if (apagadas.Eletrica.Strings > 0)
            editor.WriteMessage(Tr.F("  {0} string(s) das mesas apagadas foram junto, com o + e o − e as tags.\n", apagadas.Eletrica.Strings));

        if (gruposApagados.Count > 0)
            editor.WriteMessage(Tr.F("  Grupo(s) sem mesa, apagado(s): {0}.\n", string.Join(", ", gruposApagados)));

        if (gruposEncolhidos.Count > 0)
            editor.WriteMessage(Tr.F("  Grupo(s) que perderam mesas e continuam com as outras: {0}.\n", string.Join(", ", gruposEncolhidos)));
    }

    /// <summary>
    /// Tira as mesas apagadas dos grupos. Grupo vazio sai do registro com a
    /// marca; grupo que encolheu ganha marca nova em volta do que sobrou.
    /// </summary>
    private static (List<string> Apagados, List<string> Encolhidos) AcertarGrupos(Document documento, IReadOnlyList<Guid> mesas)
    {
        var apagados = new List<string>();
        var encolhidos = new List<string>();

        if (mesas.Count == 0) return (apagados, encolhidos);

        var database = documento.Database;
        var fora = mesas.ToHashSet();
        var grupos = GroupStore.Load(database);
        var ficam = new List<TableGroup>();
        var redesenhar = new List<TableGroup>();

        foreach (var grupo in grupos)
        {
            if (!grupo.Tables.Any(fora.Contains))
            {
                ficam.Add(grupo);
                continue;
            }

            var restantes = grupo.Tables.Where(t => !fora.Contains(t)).ToList();

            GroupDrawer.Apagar(database, grupo.Id);

            if (restantes.Count == 0)
            {
                apagados.Add(grupo.Name);
                continue;
            }

            var menor = grupo with { Tables = restantes };
            ficam.Add(menor);
            redesenhar.Add(menor);
            encolhidos.Add(grupo.Name);
        }

        if (apagados.Count == 0 && encolhidos.Count == 0) return (apagados, encolhidos);

        GroupStore.Save(database, ficam);

        foreach (var grupo in redesenhar)
            GroupDrawer.Desenhar(database, grupo, GrupoCommands.CantosDasMesas(documento, grupo));

        GrupoCommands.AtualizarPainel();

        return (apagados, encolhidos);
    }
}
