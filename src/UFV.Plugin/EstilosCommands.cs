using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.EstilosCommands))]

namespace UFV.Plugin;

/// <summary>UFV_ESTILOS (passo 8.13): os estilos de texto, cota e chamada do projeto.</summary>
public static class EstilosCommands
{
    [CommandMethod(PluginInfo.ComandoEstilos)]
    public static void Estilos()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (!UfvExtension.TemInterface())
            {
                editor.WriteMessage($"\nA janela dos estilos precisa da interface; use {PluginInfo.ComandoEstilosAutomatico}.\n");
                return;
            }

            var (textos, cotas, chamadas, atuais) = Listar(documento.Database);
            var escolhidos = Perguntar(textos, cotas, chamadas, atuais);
            if (escolhidos is null) return;

            Gravar(editor, documento.Database, escolhidos);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nos estilos do projeto.", erro);
            editor.WriteMessage($"\nNão consegui gravar os estilos: {erro.Message}\n");
        }
    }

    /// <summary>UFV_ESTILOS_AUTO: texto, cota e chamada pela linha de comando ("-" é o corrente).</summary>
    [CommandMethod(PluginInfo.ComandoEstilosAutomatico)]
    public static void EstilosAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            string? Perguntar(string rotulo)
            {
                var r = editor.GetString(new PromptStringOptions($"\n{rotulo} (- para o corrente): ") { AllowSpaces = true });
                return r.Status != PromptStatus.OK || r.StringResult.Trim() is "" or "-" ? null : r.StringResult.Trim();
            }

            var estilos = new ProjectStyles(Perguntar("Estilo de texto"), Perguntar("Estilo de cota"), Perguntar("Estilo de chamada"));
            Gravar(editor, documento.Database, estilos);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no UFV_ESTILOS_AUTO.", erro);
            editor.WriteMessage($"\nNão consegui gravar os estilos: {erro.Message}\n");
        }
    }

    internal static (List<string> Textos, List<string> Cotas, List<string> Chamadas, ProjectStyles Atuais) Listar(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var resultado = (
            EstiloDoProjeto.EstilosDeTexto(transacao, database),
            EstiloDoProjeto.EstilosDeCota(transacao, database),
            EstiloDoProjeto.EstilosDeChamada(transacao, database),
            EstiloDoProjeto.Efetivo(transacao, database));

        transacao.Commit();
        return resultado;
    }

    internal static void Gravar(Editor editor, Database database, ProjectStyles estilos)
    {
        var (textos, cotas, chamadas, _) = Listar(database);

        // Grava o nome como está no desenho (o pedido pode vir sem acento
        // de maiúscula, ou com travessão no lugar do hífen).
        var achados = new ProjectStyles(
            ProjectStyles.Match(estilos.TextStyle, textos) ?? estilos.TextStyle,
            ProjectStyles.Match(estilos.DimensionStyle, cotas) ?? estilos.DimensionStyle,
            ProjectStyles.Match(estilos.LeaderStyle, chamadas) ?? estilos.LeaderStyle);

        EstiloDoProjeto.Gravar(database, achados);

        editor.WriteMessage(
            $"\nESTILOS texto: {achados.TextStyle ?? "corrente"}; cota: {achados.DimensionStyle ?? "corrente"}; "
            + $"chamada: {achados.LeaderStyle ?? "corrente"}.\n");

        foreach (var (pedido, lista, tipo) in new[] { (achados.TextStyle, textos, "texto"), (achados.DimensionStyle, cotas, "cota"), (achados.LeaderStyle, chamadas, "chamada") })
        {
            if (pedido is not null && ProjectStyles.Match(pedido, lista) is null)
                editor.WriteMessage($"  ATENÇÃO: o estilo de {tipo} \"{pedido}\" não existe neste desenho; até existir, vale o corrente.\n");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ProjectStyles? Perguntar(List<string> textos, List<string> cotas, List<string> chamadas, ProjectStyles atuais)
    {
        var janela = new JanelaDeEstilos(textos, cotas, chamadas, atuais);
        return AcadApp.ShowModalWindow(janela) == true ? janela.Escolhidos : null;
    }
}
