using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ConfiguracoesCommands))]

namespace Clivus.Plugin;

/// <summary>CLIVUS_CONFIGURACOES (passo 8.7): a janela de Configurações, com abas.</summary>
public static class ConfiguracoesCommands
{
    [CommandMethod(PluginInfo.ComandoConfiguracoes)]
    public static void Configuracoes()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (!ClivusExtension.TemInterface())
        {
            editor.WriteMessage(Tr.T("\nA janela de Configurações precisa da interface do Civil 3D.\n"));
            return;
        }

        try
        {
            Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de Configurações.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui abrir as Configurações: {0}\n", erro.Message));
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

        TableProfile? EditarMesa(TableProfile perfil, TableProfile? outroTipo)
        {
            // Resultado, e não só a confirmada: "Salvar perfil" com o mesmo
            // nome e depois Fechar também atualiza a mesa (05/10/2026).
            var mesa = new JanelaDeMesa(biblioteca, perfil, doDesenho: true, outroTipo: outroTipo) { Owner = janela };
            mesa.ShowDialog();
            return mesa.Resultado;
        }

        // Item 15 (10/10/2026): as divergências PAN × estrutura × mesas
        // desenhadas, com a lista da janela (o que ainda não foi gravado
        // também conta), e o "Atualizar a potência das mesas desenhadas".
        var pans = RotaDeCabosStore.ModulosPan(database);
        var simulada = FonteDoModulo.Simulada(database);
        var desenhadas = FonteDoModulo.MesasDesenhadas(database);

        IReadOnlyList<string> Divergencias(IReadOnlyList<DrawingTable> lista) =>
            new ModuleSource(lista, pans, simulada).Divergences(desenhadas);

        (int, double, string) AtualizarPotencia(List<DrawingTable> lista, DrawingTable mesa)
        {
            var feito = PotenciaCommands.AtualizarPotencia(database, lista, mesa);
            desenhadas = FonteDoModulo.MesasDesenhadas(database);
            return feito;
        }

        // O carport (35.1): o segundo tipo de estrutura, com registro próprio.
        var carports = CarportsDoDesenho.Ler(database, out var problemasDosCarports);
        foreach (var problema in problemasDosCarports) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", problema));

        CarportProfile? EditarCarport(CarportProfile perfil)
        {
            var modulos = mesas.Select(m => m.Profile.Layout.Module).Distinct().ToList();
            var carport = new JanelaDeCarport(perfil, modulos) { Owner = janela };
            return carport.ShowDialog() == true ? carport.Resultado : null;
        }

        // O azimute pela linha de apoio (28/10/2026): dois pontos com a janela
        // escondida; só o elástico aparece, nada fica no desenho.
        double? PegarAzimute(System.Windows.Window dono, double atualGraus)
        {
            using var interacao = editor.StartUserInteraction(dono);

            var primeiro = editor.GetPoint(new PromptPointOptions(Tr.T("\nLinha de apoio do azimute, primeiro ponto (na direção das fileiras): ")));
            if (primeiro.Status != PromptStatus.OK) return null;

            var segundo = editor.GetPoint(new PromptPointOptions(Tr.T("\nSegundo ponto: ")) { UseBasePoint = true, BasePoint = primeiro.Value, UseDashedLine = true });
            if (segundo.Status != PromptStatus.OK) return null;

            var ucs = editor.CurrentUserCoordinateSystem;
            var a = primeiro.Value.TransformBy(ucs);
            var b = segundo.Value.TransformBy(ucs);
            var az = FacingFromLine.Azimuth(a.X, a.Y, b.X, b.Y, atualGraus * Math.PI / 180);
            if (az is null) return null;

            var graus = az.Value * 180 / Math.PI;
            editor.WriteMessage(Tr.F("\nAzimute pela linha: {0:0.###}° ({1}).\n", graus, FacingFromLine.Describe(graus)));
            return graus;
        }

        janela = new JanelaDeConfiguracoes(mesas, parametros, aviso, estilos, EditarMesa, divergencias: Divergencias, atualizarPotencia: AtualizarPotencia,
            carports: carports, editarCarport: EditarCarport, pegarAzimute: PegarAzimute);

        if (AcadApp.ShowModalWindow(janela) != true || janela.Salvo is not { } salvo)
        {
            editor.WriteMessage(Tr.T("\nConfigurações fechadas sem salvar.\n"));
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
                editor.WriteMessage(Tr.F("\n  ATENÇÃO: não consegui gravar {0}: {1}\n", Tr.T(oQue), erro.Message));
            }
        }

        var antes = MesasDoDesenho.Ler(database);
        Gravar(Tr.N("as mesas do desenho"), () => MesasDoDesenho.Gravar(database, salvo.Mesas));
        if (salvo.Carports.Count > 0 || carports.Count > 0)
            Gravar(Tr.N("os carports do desenho"), () => CarportsDoDesenho.Gravar(database, salvo.Carports, salvo.Mesas));

        // A cor do tipo vale já nas mesas desenhadas (03/10/2026: "não tem
        // nenhuma mesa laranja ou verde").
        var repintadas = 0;
        Gravar(Tr.N("as cores das mesas desenhadas"), () => repintadas = CoresDosTipos.Repintar(database, antes, salvo.Mesas));
        Gravar(Tr.N("os parâmetros"), () => SettingsStore.Save(database, salvo.Parametros));
        Gravar(Tr.N("os estilos"), () => EstilosCommands.Gravar(editor, database, salvo.Estilos));

        var emUso = DrawingTables.ForEngine(salvo.Mesas);

        editor.WriteMessage(emUso.Count == 0
            ? Tr.F("\nCONFIGURAÇÕES gravadas no desenho: {0} mesa(s), nenhuma (vale a da janela de Mesa); {1}.\n", salvo.Mesas.Count, salvo.Parametros.Describe())
            : Tr.F("\nCONFIGURAÇÕES gravadas no desenho: {0} mesa(s), o motor usa, nesta prioridade: {1}; {2}.\n", salvo.Mesas.Count, string.Join(", ", emUso.Select(m => m.Name)), salvo.Parametros.Describe()));
        if (repintadas > 0) editor.WriteMessage(Tr.F("  {0} mesa(s) desenhada(s) com a cor nova do tipo.\n", repintadas));
        editor.WriteMessage(Tr.T("  Elas vão junto com o arquivo: salve o desenho. Para a usina seguir as outras mudanças, use Regerar área.\n"));
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
        foreach (var problema in problemas) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", problema));

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
                    editor.WriteMessage(Tr.F("\n  ATENÇÃO: o perfil \"{0}\" da biblioteca não pôde ser lido: {1}\n", nome, erro.Message));
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
