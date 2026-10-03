using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.ConfiguracoesCommands))]

namespace UFV.Plugin;

/// <summary>UFV_CONFIGURACOES (passo 8.7): a janela de Configurações, com abas.</summary>
public static class ConfiguracoesCommands
{
    [CommandMethod(PluginInfo.ComandoConfiguracoes)]
    public static void Configuracoes()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (!UfvExtension.TemInterface())
        {
            editor.WriteMessage("\nA janela de Configurações precisa da interface do Civil 3D.\n");
            return;
        }

        try
        {
            Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de Configurações.", erro);
            editor.WriteMessage($"\nNão consegui abrir as Configurações: {erro.Message}\n");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Abrir(Document documento)
    {
        var editor = documento.Editor;
        var database = documento.Database;

        var parametros = ConfigCommands.Inicial(documento, out var aviso);
        var estilos = EstilosCommands.Listar(database);
        var biblioteca = new TableProfileStore(MesaCommands.PastaDosPerfis);
        var mesas = Lista(editor, database, biblioteca);

        JanelaDeConfiguracoes? janela = null;

        TableProfile? EditarMesa(TableProfile perfil)
        {
            var mesa = new JanelaDeMesa(biblioteca, perfil) { Owner = janela };
            return mesa.ShowDialog() == true ? mesa.Escolhida : null;
        }

        janela = new JanelaDeConfiguracoes(mesas, parametros, aviso, estilos, EditarMesa);

        if (AcadApp.ShowModalWindow(janela) != true || janela.Salvo is not { } salvo)
        {
            editor.WriteMessage("\nConfigurações fechadas sem salvar.\n");
            return;
        }

        // Cada parte grava sozinha e diz se falhou: uma falha nos estilos não
        // pode passar por "não consegui abrir" com as mesas já gravadas.
        void Gravar(string oQue, Action gravar)
        {
            try
            {
                gravar();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Falha ao gravar {oQue}.", erro);
                editor.WriteMessage($"\n  ATENÇÃO: não consegui gravar {oQue}: {erro.Message}\n");
            }
        }

        var antes = MesasDoDesenho.Ler(database);
        Gravar("as mesas do desenho", () => MesasDoDesenho.Gravar(database, salvo.Mesas));

        // A cor do tipo vale já nas mesas desenhadas (03/10/2026: "não tem
        // nenhuma mesa laranja ou verde").
        var repintadas = 0;
        Gravar("as cores das mesas desenhadas", () => repintadas = CoresDosTipos.Repintar(database, antes, salvo.Mesas));
        Gravar("os parâmetros", () => SettingsStore.Save(database, salvo.Parametros));
        Gravar("os estilos", () => EstilosCommands.Gravar(editor, database, salvo.Estilos));

        var emUso = DrawingTables.ForEngine(salvo.Mesas);

        editor.WriteMessage(
            $"\nCONFIGURAÇÕES gravadas no desenho: {salvo.Mesas.Count} mesa(s), "
            + (emUso.Count == 0 ? "nenhuma (vale a da janela de Mesa)" : $"o motor usa, nesta prioridade: {string.Join(", ", emUso.Select(m => m.Name))}")
            + $"; {salvo.Parametros.Describe()}.\n");
        if (repintadas > 0) editor.WriteMessage($"  {repintadas} mesa(s) desenhada(s) com a cor nova do tipo.\n");
        editor.WriteMessage("  Elas vão junto com o arquivo: salve o desenho. Para a usina seguir as outras mudanças, use Regerar área.\n");
    }

    /// <summary>
    /// A lista de mesas, como a janela mostra e o motor usa: as do desenho,
    /// na ordem gravada, e depois as da biblioteca (a pasta do usuário) que
    /// o desenho ainda não tem, desmarcadas (02/10/2026: "não está listando
    /// as mesas salvas, e tem mesa salva").
    /// </summary>
    internal static List<DrawingTable> Lista(Autodesk.AutoCAD.EditorInput.Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database, TableProfileStore biblioteca)
    {
        var doDesenho = MesasDoDesenho.Ler(database, out var problemas);
        foreach (var problema in problemas) editor.WriteMessage($"\n  ATENÇÃO: {problema}.\n");

        var mesas = doDesenho.ToList();

        try
        {
            foreach (var nome in biblioteca.List())
            {
                if (DrawingTables.Find(mesas, nome) is not null) continue;

                try
                {
                    mesas.Add(new DrawingTable(biblioteca.Load(nome), DrawingTables.NextColor(mesas), Use: false));
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar($"Não consegui ler o perfil \"{nome}\" da biblioteca.", erro);
                    editor.WriteMessage($"\n  ATENÇÃO: o perfil \"{nome}\" da biblioteca não pôde ser lido: {erro.Message}\n");
                }
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui listar a biblioteca de perfis.", erro);
        }

        return mesas;
    }
}
