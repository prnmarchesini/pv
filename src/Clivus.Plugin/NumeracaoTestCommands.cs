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

    /// <summary>
    /// Depois do exemplo: dois blocos, os dois da direita para a esquerda. O
    /// Bloco 1 com as mesas das strings dos inversores 1 e 2; o Bloco 2 com as
    /// mesas das strings dos inversores 3 e 4 que não ficaram no 1. O resto
    /// fica fora de bloco. Assim o Bloco 2 tem, com certeza, as duas fileiras
    /// de uma mesa no mesmo inversor (o 4), e mudar o sentido dele muda tag.
    /// </summary>
#if DEBUG
    [CommandMethod(PluginInfo.ComandoNumeracaoExemploBlocosAutomatico)]
#endif
    public static void ExemploBlocos()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var database = documento.Database;
        var inversores = ElectricalStore.Inverters(database).Items;
        var mesasDe = new Dictionary<Guid, List<Guid>>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var modulos = NumeracaoDesenho.Modulos(transacao, database);
            foreach (var (_, s) in ElectricalStore.Strings(transacao, database).OrderBy(x => x.Id.Handle.Value))
            {
                if (!s.IsAllocated || !modulos.TryGetValue(s.Modules[0], out var lugar)) continue;
                if (!mesasDe.TryGetValue(s.Inverter, out var lista)) mesasDe[s.Inverter] = lista = [];
                lista.Add(lugar.Mesa);
            }
        }

        IEnumerable<Guid> Mesas(params int[] quais) =>
            quais.Where(i => i < inversores.Count).SelectMany(i => mesasDe.GetValueOrDefault(inversores[i].Id) ?? []);

        var um = Mesas(0, 1).Distinct().ToList();
        var dois = Mesas(2, 3).Distinct().Except(um).ToList();

        NumeracaoStore.MudarVarredura(database, v =>
        {
            foreach (var b in v.Blocks.ToList()) v.Remove(b.Id);
            var b1 = v.AddBlock();
            var b2 = v.AddBlock();
            v.SetTables(b1.Id, um);
            v.SetTables(b2.Id, dois);
            v.SetDirection(b1.Id, ScanDirection.RightToLeft);
            v.SetDirection(b2.Id, ScanDirection.RightToLeft);
        });

        documento.Editor.WriteMessage($"\nNUMERACAO_EXEMPLO_BLOCOS bloco1={um.Count} bloco2={dois.Count}\n");
    }

    /// <summary>
    /// CLIVUS_NUMERACAO_JANELA_AUTO &lt;bloco&gt;: o MESMO caminho do botão
    /// Mostrar da linha do bloco (<see cref="PainelDeNumeracao.MostrarMesas"/>),
    /// no contexto da aplicação (Session), como o clique de uma janela solta,
    /// fora de comando do documento. O nível 2 lê depois a seleção implícita
    /// com (ssget "_I").
    /// </summary>
#if DEBUG
    [CommandMethod(PluginInfo.ComandoNumeracaoJanelaAutomatico, CommandFlags.Session)]
#endif
    public static void MostrarPelaJanela()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            NumberingBlock? bloco;
            using (documento.LockDocument()) bloco = NumeracaoCommands.PerguntarBloco(documento.Editor, documento.Database);
            if (bloco is null) return;

            documento.Editor.WriteMessage($"\nNUMERACAO mostradas pela janela {PainelDeNumeracao.MostrarMesas(documento, bloco.Id)} de {bloco.Name}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_NUMERACAO_JANELA_AUTO.", erro);
            documento.Editor.WriteMessage($"\nNUMERACAO falhou: {erro.Message}\n");
        }
    }
}
