using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// A alocação automática dos inversores conforme as strings (Renan,
/// 10/10/2026): no Gerar da rota CC, cada inversor marcado como automático
/// que ainda não está em campo vai para o lado da vala, no ponto da rede de
/// menor cabo CC das strings dele (a mesma conta do router: saída da mesa
/// pelo lado mais curto e o caminho pela vala). Depois o usuário move à mão,
/// se o ponto cair num lugar ruim, e o Gerar refaz a rota da posição nova;
/// o "Recolocar automáticos" volta todos ao ponto de menor cabo.
/// </summary>
internal static class PosicaoAutomatica
{
    /// <summary>Põe os automáticos (todos, com <paramref name="todos"/>; senão só os que não estão em campo). As frases do que fez.</summary>
    internal static List<string> Colocar(Editor editor, Database db, ProcessedTerrain terreno, TrenchNetwork rede, RouteSettings config, LeituraDaRota leitura, bool todos)
    {
        var frases = new List<string>();
        var locais = LocalDosInversores.Ler(db, out var problema);
        if (problema is not null)
        {
            frases.Add(Tr.F("ATENÇÃO: o local dos inversores não se lê ({0}); nenhum foi posto automaticamente.", problema));
            return frases;
        }

        var automaticos = locais.Where(l => l.Mode == InverterPlacementMode.Automatic)
            .Select(l => leitura.Setup.FindInverter(l.Inverter))
            .OfType<Inverter>()
            .Where(i => todos || leitura.Ponto(EquipmentKind.Inverter, i.Id) is null)
            .ToList();
        if (automaticos.Count == 0) return frases;

        // As strings do CC de cada inversor (as de combiner vão pela aba Combiner).
        var porInversor = leitura.StringsCc().GroupBy(s => s.Destination.Id).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var inversor in automaticos)
        {
            if (!porInversor.TryGetValue(inversor.Id, out var strings) || strings.Count == 0)
            {
                frases.Add(Tr.F("{0} é automático mas não tem string: não foi posto.", inversor.Name));
                continue;
            }

            var acessos = strings.Select(s => Acesso(s, rede, config)).ToList();
            if (InverterSites.BestTrenchPoint(rede, acessos) is not { } melhor)
            {
                frases.Add(Tr.F("{0}: nenhuma string dele chega a uma vala (alcance {1:0.#} m); não foi posto.", inversor.Name, config.Reach));
                continue;
            }

            if (leitura.Setup.FindEquipment(EquipmentKind.Inverter, inversor.Id) is not { } equipamento) continue;

            var centro = InverterSites.BesideTrench(melhor.Point.At, melhor.Direction, equipamento.Size.Width, equipamento.Size.Length,
                cantos => !leitura.Ocupado(cantos, inversor.Id));

            if (ConfiguracaoEletricaCommands.NoTerreno(editor, db, terreno, equipamento, centro.X, centro.Y))
                frases.Add(Tr.F("{0} posto ao lado da vala, no ponto de menor cabo CC das {1} string(s) dele ({2:0.0} m em planta). Mova à mão se o lugar não servir e Gere de novo.",
                    inversor.Name, strings.Count, melhor.Total));
            else
                frases.Add(Tr.F("{0}: o ponto de menor cabo está fora do terreno; não foi posto.", inversor.Name));
        }

        return frases;
    }

    /// <summary>
    /// Por onde a string chega à rede em cada lado da fileira (ou só pelo
    /// forçado): as batidas do + e do − e o caminho em planta até elas.
    /// </summary>
    private static InverterSites.StringAccess Acesso(StringRouteInput s, TrenchNetwork rede, RouteSettings config)
    {
        var lados = new List<(TrenchPoint Hit, double Before)[]>();
        foreach (var lado in s.ForcedEnd is { } forcado ? new[] { forcado } : new[] { RowEnd.Start, RowEnd.End })
        {
            if (CableRouter.StringExit(s.Positive, lado, rede, config, out _) is not { } mais
                || CableRouter.StringExit(s.Negative, lado, rede, config, out _) is not { } menos)
                continue;

            lados.Add([(mais.Hit, Antes(s.Positive.Point, mais)), (menos.Hit, Antes(s.Negative.Point, menos))]);
        }

        return new InverterSites.StringAccess(lados);
    }

    /// <summary>O caminho em planta da ponta da string até a batida na vala.</summary>
    private static double Antes(Point3 ponta, (IReadOnlyList<Point3> Exit, TrenchPoint Hit) saida) =>
        TrenchNetwork.PlanLength([ponta, .. saida.Exit, saida.Hit.At]);
}
