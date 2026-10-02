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

        var doDesenho = MesasDoDesenho.Ler(database, out var problemas);
        foreach (var problema in problemas) editor.WriteMessage($"\n  ATENÇÃO: {problema}.\n");

        var parametros = ConfigCommands.Inicial(documento, out var aviso);
        var estilos = EstilosCommands.Listar(database);
        var biblioteca = new TableProfileStore(MesaCommands.PastaDosPerfis);

        // As mesas salvas na biblioteca (a pasta do usuário) entram na lista
        // também, desmarcadas, as que o desenho ainda não tem (02/10/2026:
        // "não está listando as mesas salvas, e tem mesa salva").
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

        Gravar("as mesas do desenho", () => MesasDoDesenho.Gravar(database, salvo.Mesas));
        Gravar("os parâmetros", () => SettingsStore.Save(database, salvo.Parametros));
        Gravar("os estilos", () => EstilosCommands.Gravar(editor, database, salvo.Estilos));

        var emUso = DrawingTables.InUse(salvo.Mesas);

        editor.WriteMessage(
            $"\nCONFIGURAÇÕES gravadas no desenho: {salvo.Mesas.Count} mesa(s), "
            + (emUso.Count == 0 ? "nenhuma em uso (vale a da janela de Mesa)" : $"em uso: {string.Join(", ", emUso.Select(m => m.Name))}")
            + $"; {salvo.Parametros.Describe()}.\n");
        editor.WriteMessage("  Elas vão junto com o arquivo: salve o desenho. Para a usina seguir as mudanças, use Refazer.\n");
    }
}
