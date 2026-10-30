using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.MesasDoDesenhoCommands))]

namespace Clivus.Plugin;

/// <summary>Comandos das mesas do desenho (8.5) que não precisam de janela.</summary>
public static class MesasDoDesenhoCommands
{
    /// <summary>
    /// CLIVUS_MESAS_EXEMPLO_AUTO: a mesa de exemplo (28 módulos, 2V) e a mesma
    /// com 14 módulos, gravadas no desenho e em uso. Para o nível 2 da usina
    /// mista.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoMesasExemploAutomatico)]
    public static void Exemplo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            var longa = MesaCommands.MesaDeExemplo() with { Name = "Mesa 2V28" };
            var curta = longa with { Name = "Mesa 2V14", Layout = longa.Layout with { ModuleCount = 14 } };

            var mesas = new List<DrawingTable>();
            mesas.Add(new DrawingTable(longa, DrawingTables.NextColor(mesas), true));
            mesas.Add(new DrawingTable(curta, DrawingTables.NextColor(mesas), true));

            MesasDoDesenho.Gravar(documento.Database, mesas);
            documento.Editor.WriteMessage(Tr.F("\nMESAS {0} mesa(s) no desenho, em uso: {1}.\n", mesas.Count, string.Join(", ", mesas.Select(m => m.Name))));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_MESAS_EXEMPLO_AUTO.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui gravar as mesas: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_BIFILA_EXEMPLO_AUTO (38.2): a bifila Arctech 2P14 GC800, sozinha
    /// e em uso, e a faixa da PB do projeto que ela pede (de 0,84 a 1,60 m: a
    /// GC800 quer 0,80 livre no fundo do módulo, 0,832 na face). Para o nível 2.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoBifilaExemploAutomatico)]
    public static void BifilaExemplo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            var bifila = BifilaReference.Profile(BifilaVariant.Gc800);
            MesasDoDesenho.Gravar(documento.Database, [new DrawingTable(bifila, DrawingTables.NextColor([]), true)]);

            var atual = ConfigCommands.Inicial(documento, out _);
            SettingsStore.Save(documento.Database, atual with
            {
                Configuration = atual.Configuration with { MinLowEdge = 0.84, MaxLowEdge = 1.60 },
            });

            documento.Editor.WriteMessage(Tr.F("\nBIFILA {0} no desenho, em uso; PB de 0,84 a 1,60 m.\n", bifila.Name));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_BIFILA_EXEMPLO_AUTO.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui gravar o biposte: {0}\n", erro.Message));
        }
    }
}
