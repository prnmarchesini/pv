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

    /// <summary>
    /// CLIVUS_MONO_2P14_AUTO (39.2): a mesma mesa da GC800, com estrutura
    /// monoposte (um pilar por pórtico, nas mesmas estações e com o mesmo T3),
    /// sozinha e em uso, com a faixa da PB do biposte. O nível 2 roda a mesma
    /// usina com esta e com a GC800 e compara tudo o que não é estrutura.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoMonoposte2P14Automatico)]
    public static void MonoposteExemplo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            var mono = Monoposte2P14();
            MesasDoDesenho.Gravar(documento.Database, [new DrawingTable(mono, DrawingTables.NextColor([]), true)]);

            var atual = ConfigCommands.Inicial(documento, out _);
            SettingsStore.Save(documento.Database, atual with
            {
                Configuration = atual.Configuration with { MinLowEdge = 0.84, MaxLowEdge = 1.60 },
            });

            documento.Editor.WriteMessage(Tr.F("\nMONOPOSTE {0} no desenho, em uso; PB de 0,84 a 1,60 m.\n", mono.Name));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_MONO_2P14_AUTO.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui gravar a monoposte: {0}\n", erro.Message));
        }
    }

    /// <summary>A GC800 com estrutura monoposte: o pilar a 1,50 m da ponta baixa de uma tesoura de 3,00 m.</summary>
    internal static TableProfile Monoposte2P14()
    {
        var gc800 = BifilaReference.Profile(BifilaVariant.Gc800);

        return gc800 with
        {
            Name = "2P14 monoposte",
            Frame = new TableFrame(3.00, 1.50, 0.15, 0.07, gc800.Frame.PillarSpanTarget, gc800.Frame.PillarCantilever)
            {
                PillarSpans = gc800.Frame.PillarSpans,
                MinEmbedment = gc800.Frame.MinEmbedment,
            },
        };
    }
}
