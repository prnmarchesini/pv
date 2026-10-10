using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As tabelas do "Ver cabos" (17.8, 18.8, 23.4, 23.6) e do resumo (24):
/// sempre a partir do desenho de agora (regra 7). Antes de montar, reconta:
/// os lances gerados que não estão mais no desenho (apagados com Delete por
/// fora do plugin) são contados e a origem deles é pintada.
/// </summary>
internal static class RotaDeCabosTabelas
{
    /// <summary>A recontagem de uma rota: os lances do desenho e os sumidos.</summary>
    internal sealed record Recontagem(List<RotaDeCabosStore.LanceNoDesenho> Lances, List<CableRun> Sumidos);

    /// <summary>Reconta a rota e pinta a origem dos lances sumidos (somando à pintura que já havia).</summary>
    internal static Recontagem Recontar(Document documento, CableRoute rota, LeituraDaRota leitura)
    {
        var db = documento.Database;
        var lances = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == rota).ToList();
        var presentes = lances.Select(l => l.Lance.Id).ToHashSet();
        var sumidos = RotaDeCabosStore.LancesGerados(db).Where(l => l.Route == rota && !presentes.Contains(l.Id)).ToList();

        if (sumidos.Count > 0)
        {
            var pintar = sumidos.SelectMany(s => s.From.Kind == CableEndKind.String ? leitura.MesasDaString(s.From.Id) : leitura.Pintaveis(s.From)).ToList();
            EscritaForaDeComando.Fazer(documento, () => RotaDeCabosStore.Pintar(db, rota, pintar, somar: true));
        }

        return new Recontagem(lances, sumidos);
    }

    /// <summary>As tabelas de uma rota (o CC tem uma; a Combiner tem as strings e os alimentadores).</summary>
    internal static List<CableTable> Montar(Document documento, CableRoute rota, out Recontagem recontagem)
    {
        var db = documento.Database;
        var leitura = LeituraDaRota.Ler(db);
        recontagem = Recontar(documento, rota, leitura);

        var config = RotaDeCabosStore.Configuracao(db, rota);
        var projeto = SettingsStore.Load(db).Settings ?? ProjectSettings.Default;
        var pans = RotaDeCabosStore.ModulosPan(db);
        var titulo = Tr.F("Cabos {0}", CableRoutes.Title(rota));

        if (rota is CableRoute.DirectCurrent or CableRoute.Combiner)
        {
            var mesas = MesasDoDesenho.Ler(db);
            PanModule? Modulo(string? perfil)
            {
                var modelo = DrawingTables.Find(mesas, perfil)?.Profile.Layout.Module.Model;
                return pans.FirstOrDefault(p => modelo is not null && string.Equals(p.Model.Trim(), modelo.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? (pans.Count == 1 ? pans[0] : null);
            }

            var porString = recontagem.Lances.Where(l => l.Lance.From.Kind == CableEndKind.String)
                .GroupBy(l => l.Lance.From.Id)
                .ToDictionary(g => g.Key, g => g.ToList());
            var esperadas = recontagem.Sumidos.Where(s => s.From.Kind == CableEndKind.String).Select(s => s.From.Id).ToHashSet();

            var linhas = leitura.Strings()
                .Where(x => porString.ContainsKey(x.String.Id) || esperadas.Contains(x.String.Id))
                .Select(x =>
                {
                    porString.TryGetValue(x.String.Id, out var dela);
                    double? Comprimento(CablePolarity p) => dela?.Where(l => l.Lance.Polarity == p).Select(l => (double?)l.Comprimento).Sum();
                    return new DcStringRun(x.String.Id, x.String.Tag, x.String.Modules.Count, Comprimento(CablePolarity.Positive), Comprimento(CablePolarity.Negative), Modulo(x.Perfil));
                })
                .ToList();

            var dc = CableReport.Dc(titulo, linhas, config.Cable, null, projeto.MinTemperature, projeto.MaxTemperature);
            // As temperaturas que entraram nas contas, sempre à vista (as de partida também são de alguém escolher).
            var tabelas = new List<CableTable>
            {
                dc with { Notes = [.. dc.Notes, Tr.F("Temperaturas usadas (Configurações): mínima {0} °C, máxima {1} °C, aplicadas como estão.", projeto.MinTemperature, projeto.MaxTemperature)] },
            };

            if (rota == CableRoute.Combiner)
            {
                var serieDe = leitura.Strings().ToDictionary(x => x.String.Id, x => (x.String.Modules.Count, Modulo(x.Perfil)));
                var alimentadores = recontagem.Lances.Where(l => l.Lance.From.Kind == CableEndKind.Combiner)
                    .Select(l =>
                    {
                        var cb = l.Lance.From.Id;
                        var dela = leitura.CombinerDaString.Where(x => x.Value == cb).Select(x => x.Key).Where(serieDe.ContainsKey).ToList();
                        var (serie, modulo) = dela.Count > 0 ? serieDe[dela[0]] : (0, null);
                        return new DcFeederRun(leitura.Setup.FindCombiner(cb)?.Name ?? "?", leitura.Setup.FindInverter(l.Lance.To.Id)?.Name ?? "?", l.Comprimento, dela.Count, serie, modulo);
                    });
                tabelas.Add(CableReport.Feeders(Tr.T("Combiner → inversor"), alimentadores, config.Cable, projeto.MaxTemperature));
            }

            return tabelas;
        }

        var setup = leitura.Setup;
        var trechos = recontagem.Lances.Select(l =>
        {
            if (rota == CableRoute.AlternatingCurrent)
            {
                var inv = setup.FindInverter(l.Lance.From.Id);
                var trafo = setup.FindTransformer(l.Lance.To.Id);
                var kw = inv is null ? 0 : setup.FindModel(inv.Model)?.PowerKw ?? 0;
                var v = trafo?.InputVoltage ?? 0;
                string? falta = kw <= 0 ? Tr.T("potência do inversor não informada no modelo") : v <= 0 ? Tr.T("tensão de baixa do trafo não informada") : null;
                return new EquipmentRun(inv?.Name ?? "?", trafo?.Nickname ?? "?", l.Comprimento,
                    falta is null ? config.ThreePhase ? CableCalc.ThreePhaseCurrent(kw, v, config.PowerFactor) : CableCalc.SinglePhaseCurrent(kw, v, config.PowerFactor) : null,
                    v > 0 ? v : null, falta);
            }

            var t = setup.FindTransformer(l.Lance.From.Id);
            var (_, sub) = t is null ? (Guid.Empty, "?") : CableChain.PhysicalSubstation(setup, t.ConsumerUnit);
            var mt = t?.OutputVoltage ?? 0;
            string? faltaMt = t is null || t.PowerKva <= 0 ? Tr.T("potência do trafo não informada") : mt <= 0 ? Tr.T("tensão de média do trafo não informada") : null;
            return new EquipmentRun(t?.Nickname ?? "?", sub, l.Comprimento,
                faltaMt is null ? CableCalc.ThreePhaseCurrentKva(t!.PowerKva, mt) : null, mt > 0 ? mt : null, faltaMt);
        });

        return [CableReport.Equipment(titulo, trechos, config.Cable, config.Method, config.PowerFactor, rota != CableRoute.AlternatingCurrent || config.ThreePhase)];
    }

    /// <summary>Os lances medidos de todas as rotas, com o nome do cabo da aba (para o resumo e a lista de material).</summary>
    internal static List<CableReport.MeasuredRun> Medidos(Database db)
    {
        var config = RotaDeCabosStore.Configuracoes(db, out _);
        return RotaDeCabosStore.Lances(db)
            .Select(l => new CableReport.MeasuredRun(l.Lance.Route, l.Lance.Polarity, config[l.Lance.Route].Cable?.Name ?? Tr.T("(sem cabo escolhido)"), l.Comprimento))
            .ToList();
    }

    /// <summary>Grava as tabelas num CSV (uma depois da outra, com o título), UTF-8 com BOM para o Excel abrir com acento.</summary>
    internal static void GravarCsv(string caminho, IEnumerable<CableTable> tabelas)
    {
        var texto = string.Join("\r\n", tabelas.Select(t => t.Title + "\r\n" + t.ToCsv(Tr.Culture)));
        System.IO.File.WriteAllText(caminho, texto, new System.Text.UTF8Encoding(true));
    }
}
