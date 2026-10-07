using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O que a parte elétrica pendura nas mesas sai junto com elas (Renan,
/// 07/10/2026, erro grave: "cliquei em regerar mesas da usina inteira, e o
/// sistema manteve as strings antigas. Toda vez que regerar as mesas é
/// preciso apagar tudo o que é relacionado com a mesa, strings, tags, etc").
///
/// A string guarda o GUID dos módulos, não o da mesa, e o módulo redesenhado
/// nasce com GUID novo: por isso o apagar da mesa não a via. Aqui, dentro da
/// transação de quem apagou as peças, sai toda string que toca módulo que
/// não está mais vivo (o traçado, o + e o − com o círculo, a tag da
/// numeração); a alocação ao inversor morava nela e vai junto. Depois do
/// Commit, o hatch do trafo que perdeu strings é refeito com as que sobraram.
/// </summary>
internal static class EletricaDasMesas
{
    /// <summary>O que saiu: quantas strings, e os inversores delas (para o hatch do trafo).</summary>
    internal sealed record Apagado(int Strings, IReadOnlySet<Guid> Inversores, IReadOnlySet<Guid> TrafosComHatch)
    {
        internal static readonly Apagado Nada = new(0, new HashSet<Guid>(), new HashSet<Guid>());
    }

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoBloco = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(BlockReference));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaPolilinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoMText = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoCirculo = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Circle));

    /// <summary>
    /// Apaga as strings mortas, com sinais e tags. Chamar DEPOIS de apagar
    /// as peças das mesas, na mesma transação (um U desfaz tudo); os módulos
    /// dados contam como apagados mesmo que ainda não estejam.
    /// </summary>
    internal static Apagado Apagar(Transaction transacao, Database database, IReadOnlySet<Guid> modulosApagados)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(modulosApagados);

        var vivos = new HashSet<Guid>();
        var strings = new List<(ObjectId Id, ElectricalString String)>();
        var penduradas = new List<(ObjectId Id, Guid String)>();

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased) continue;

            var classe = id.ObjectClass;
            if (classe != ClasseDoBloco && classe != ClasseDaPolilinha && classe != ClasseDoTexto && classe != ClasseDoMText && classe != ClasseDoCirculo) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

            using (var dados = e.GetXDataForApplication(PluginXData.Aplicativo))
                if (dados is null) continue;

            switch (e)
            {
                case BlockReference when LayoutXData.LoadModule(e) is { } m:
                    if (!modulosApagados.Contains(m.Id)) vivos.Add(m.Id);
                    break;
                case Polyline3d when ElectricalStore.LoadString(e) is { } s:
                    strings.Add((id, s));
                    break;
                case MText when NumeracaoDesenho.LerTag(e) is { } tag:
                    penduradas.Add((id, tag.String));
                    break;
                case DBText or Circle when PluginXData.Load(e, StringSign.Tipo, 1, StringSign.FieldCount) is { } c && StringSign.Parse(c) is { } sinal:
                    penduradas.Add((id, sinal.String));
                    break;
            }
        }

        var mortas = StringsOfErasedTables.Doomed(strings.Select(x => x.String), vivos);
        if (mortas.Count == 0) return Apagado.Nada;

        foreach (var (id, s) in strings)
            if (mortas.Contains(s.Id)) Apagar(transacao, id);

        foreach (var (id, dona) in penduradas)
            if (mortas.Contains(dona)) Apagar(transacao, id);

        var inversores = strings.Where(x => mortas.Contains(x.String.Id) && x.String.IsAllocated).Select(x => x.String.Inverter).ToHashSet();
        var trafosComHatch = inversores.Count > 0 ? AreaDoTrafo.Hatches(transacao, database).Select(h => h.Trafo).ToHashSet() : new HashSet<Guid>();

        return new Apagado(mortas.Count, inversores, trafosComHatch);
    }

    /// <summary>
    /// Depois do Commit (o cadastro é lido fora da transação de escrita): o
    /// trafo que tinha hatch e perdeu string tem o hatch refeito com as que
    /// sobraram (sem nenhuma, o hatch só sai). Quantos trafos.
    /// </summary>
    internal static int AcertarHatches(Database database, Apagado apagado)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(apagado);

        if (apagado.Inversores.Count == 0 || apagado.TrafosComHatch.Count == 0) return 0;

        var trafos = ElectricalStore.Inverters(database).Items
            .Where(i => apagado.Inversores.Contains(i.Id) && apagado.TrafosComHatch.Contains(i.Transformer))
            .Select(i => i.Transformer)
            .Distinct()
            .ToList();

        foreach (var t in trafos) AreaDoTrafo.Gerar(database, t);
        return trafos.Count;
    }

    /// <summary>
    /// Tira dos blocos da numeração as mesas que sumiram de vez (o Refazer dá
    /// GUID novo à mesa; o Recalcular mantém o dela e não chama isto).
    /// </summary>
    internal static void EsquecerMesas(Database database, IReadOnlyCollection<Guid> mesas)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (mesas.Count == 0) return;

        var (varredura, problema) = NumeracaoStore.Varredura(database);

        // Registro lido pela metade: gravar por cima perderia o resto.
        if (problema is not null)
        {
            RegistroDeDiagnostico.Registrar($"Blocos da numeração não acertados depois de apagar mesas: {problema}");
            return;
        }

        if (varredura.ForgetTables(mesas.ToHashSet()) > 0) NumeracaoStore.GravarVarredura(database, varredura);
    }

    private static void Apagar(Transaction transacao, ObjectId id)
    {
        if (id.IsErased || transacao.GetObject(id, OpenMode.ForWrite, false, true) is not Entity e) return;
        e.Erase();
    }
}
