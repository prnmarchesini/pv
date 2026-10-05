using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.NumeracaoTestCommands))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 das etapas 15 e 16): uma cadeia elétrica de
/// mentira gravada pelo contrato, para numerar e resumir sem esperar as
/// janelas de subestação, trafo e inversor. Uma UC (C1); dois trafos (TA na
/// C1, TB sem UC); um modelo de 2 MPPT x 2 entradas (4 strings); quatro
/// inversores (1 e 2 no TA, 3 no TB, 4 sem trafo). As strings, na ordem do
/// handle (a ordem em que foram criadas, nunca a posição), vão 5 para o
/// inversor 1 (uma além da capacidade), 4 para o 2, 3 para o 3 e 2 para o 4;
/// o resto fica livre. Substitui o que havia nos cadastros.
/// </summary>
public static class NumeracaoTestCommands
{
    private static readonly int[] PorInversor = [5, 4, 3, 2];

#if DEBUG
    [CommandMethod(PluginInfo.ComandoNumeracaoExemploAutomatico)]
#endif
    public static void Exemplo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var database = documento.Database;
        var caixa = new EquipmentSize(2, 1, 2.2);

        var uc = new ConsumerUnit(Guid.NewGuid(), "C1", "Medição", ConsumerUnitMode.Shared, caixa);
        var ta = new Transformer(Guid.NewGuid(), "Trafo seco", "TA", 800, 13800, 2500, 4, 6.5, "", caixa, uc.Id);
        var tb = new Transformer(Guid.NewGuid(), "Trafo seco", "TB", 800, 13800, 2500, 4, 6.5, "", caixa, Guid.Empty);
        var modelo = new InverterModel(Guid.NewGuid(), "Teste 2x2", 2, 2, caixa);
        var inversores = new[]
        {
            new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", ta.Id),
            new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 2", ta.Id),
            new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 3", tb.Id),
            new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 4", Guid.Empty),
        };

        ElectricalStore.SaveConsumerUnits(database, [uc]);
        ElectricalStore.SaveTransformers(database, [ta, tb]);
        ElectricalStore.SaveInverterModels(database, [modelo]);
        ElectricalStore.SaveInverters(database, inversores);

        var alocadas = 0;
        var total = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var strings = ElectricalStore.Strings(transacao, database).OrderBy(s => s.Id.Handle.Value).ToList();
            total = strings.Count;
            var proxima = 0;

            for (var i = 0; i < inversores.Length; i++)
            {
                for (var k = 0; k < PorInversor[i] && proxima < strings.Count; k++, proxima++)
                {
                    var (id, s) = strings[proxima];
                    ElectricalStore.SaveString(transacao, (Entity)transacao.GetObject(id, OpenMode.ForWrite), s with { Inverter = inversores[i].Id });
                    alocadas++;
                }
            }

            for (; proxima < strings.Count; proxima++)
            {
                var (id, s) = strings[proxima];
                if (s.IsAllocated) ElectricalStore.SaveString(transacao, (Entity)transacao.GetObject(id, OpenMode.ForWrite), s with { Inverter = Guid.Empty });
            }

            transacao.Commit();
        }

        documento.Editor.WriteMessage($"\nNUMERACAO_EXEMPLO ucs=1 trafos=2 inversores={inversores.Length} alocadas={alocadas} livres={total - alocadas}\n");
    }
}
