using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A fonte única do módulo no desenho (Melhorias de 10/10/2026, itens 14 e
/// 15). Quem precisa dos dados elétricos do módulo de uma string chama
/// <see cref="Ler"/> uma vez e pergunta <see cref="ModuleSource.ElectricalFor"/>
/// pelo nome da estrutura da mesa dela; quem soma kWp usa
/// <see cref="ModuleSource.PowerFor"/> (ou <see cref="Simulada"/>). A regra
/// mora no Core (<see cref="ModuleSource"/>); aqui só se lê e grava o desenho.
/// </summary>
internal static class FonteDoModulo
{
    private static readonly string OQueSimulada = Tr.N("da potência do módulo trocada pela área");

    /// <summary>
    /// A fonte do módulo deste desenho: as estruturas (com o PAN de cada
    /// uma), os PAN antigos da rota (reserva) e a potência trocada pela área.
    /// </summary>
    internal static ModuleSource Ler(Database db) =>
        new(MesasDoDesenho.Ler(db), RotaDeCabosStore.ModulosPan(db), Simulada(db));

    /// <summary>A potência trocada pela área (item 14), ou null.</summary>
    internal static SimulatedModulePower? Simulada(Database db) =>
        PluginRecords.Load(db, SimulatedModulePower.StorageKey, SimulatedModulePower.Version, SimulatedModulePower.FieldCount, SimulatedModulePower.Parse, OQueSimulada)
            .Items.FirstOrDefault();

    /// <summary>Grava a potência trocada pela área (null: desfaz, e os cálculos elétricos voltam).</summary>
    internal static void GravarSimulada(Database db, SimulatedModulePower? simulada)
    {
        if (simulada?.WhyInvalid() is { } porque) throw new InvalidOperationException(porque);

        PluginRecords.Save<SimulatedModulePower>(db, SimulatedModulePower.StorageKey, SimulatedModulePower.Version, SimulatedModulePower.FieldCount,
            simulada is null ? [] : [simulada], s => s.ToFields());
    }

    /// <summary>As mesas desenhadas: GUID, de que estrutura são e a potência do módulo gravada.</summary>
    internal static List<DrawnTablePower> MesasDesenhadas(Database db)
    {
        using var transacao = db.TransactionManager.StartOpenCloseTransaction();
        return LayoutScan.Tables(transacao, db).Values
            .Where(p => p.Identity is not null)
            .Select(p => new DrawnTablePower(p.Identity!.Id, p.Identity.ProfileName, p.Identity.ModulePowerWatts))
            .ToList();
    }

    /// <summary>
    /// "Atualizar a potência das mesas desenhadas" (item 15): as mesas desta
    /// estrutura passam a ter a potência do PAN dela (ou a do módulo, sem
    /// PAN). Só o número da potência no XData do contorno muda: geometria,
    /// posição, pilares, módulos e letreiro ficam. Devolve quantas mudaram.
    /// </summary>
    internal static int AtualizarPotencia(Database db, IReadOnlyList<DrawingTable> estruturas, DrawingTable estrutura)
    {
        using var transacao = db.TransactionManager.StartTransaction();
        var mesas = LayoutScan.Tables(transacao, db);
        var desenhadas = mesas.Values
            .Where(p => p.Identity is not null)
            .Select(p => new DrawnTablePower(p.Identity!.Id, p.Identity.ProfileName, p.Identity.ModulePowerWatts))
            .ToList();

        var plano = ModuleSource.PowerUpdate(estruturas, estrutura, desenhadas);

        foreach (var (guid, watts) in plano)
        {
            var partes = mesas[guid];
            foreach (var id in partes.Contours)
            {
                if (transacao.GetObject(id, OpenMode.ForWrite) is not Entity contorno || LayoutXData.LoadTable(contorno) is not { } identidade) continue;
                LayoutXData.SaveTable(transacao, contorno, identidade with { ModulePowerWatts = watts });
            }
        }

        transacao.Commit();
        return plano.Count;
    }

    /// <summary>As divergências entre PAN, estrutura e mesas desenhadas, para mostrar onde o usuário vê.</summary>
    internal static IReadOnlyList<string> Divergencias(Database db) =>
        Ler(db).Divergences(MesasDesenhadas(db));
}
