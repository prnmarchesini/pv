using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Uma mancha do mapa de bacias: a bacia de uma canaleta, ou uma área sem
/// destino (com o motivo). Os anéis são a união das células, em planta; o
/// rótulo vai num ponto de dentro.
/// </summary>
/// <param name="Owner">O índice da canaleta, ou <see cref="Watershed.NoDestination"/>.</param>
/// <param name="Reason">Por que a água não chega a canaleta nenhuma (só na área sem destino).</param>
/// <param name="MaxZ">A cota mais alta das células: o hatch fica nela, nunca enterrado.</param>
public sealed record BasinShape(int Owner, FlowStop? Reason, IReadOnlyList<IReadOnlyList<(double X, double Y)>> Rings, double Area, (double X, double Y) LabelAt, double MaxZ);

/// <summary>O mapa das bacias (30.2): as manchas para o hatch, a partir da grade.</summary>
public static class BasinMap
{
    /// <summary>As manchas: uma por canaleta com bacia e uma por motivo de sem destino.</summary>
    public static IReadOnlyList<BasinShape> Shapes(WatershedGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var grupos = new Dictionary<(int Dono, FlowStop? Motivo), List<int>>();

        for (var k = 0; k < grid.Owner.Length; k++)
        {
            var dono = grid.Owner[k];
            if (dono == Watershed.NotAnalyzed) continue;

            var chave = dono == Watershed.NoDestination ? (dono, (FlowStop?)grid.Reason[k]) : (dono, null);
            if (!grupos.TryGetValue(chave, out var lista)) grupos[chave] = lista = [];
            lista.Add(k);
        }

        var manchas = new List<BasinShape>();
        var meia = grid.Cell / 2;

        foreach (var ((dono, motivo), celulas) in grupos.OrderBy(g => g.Key.Dono < 0 ? int.MaxValue : g.Key.Dono).ThenBy(g => g.Key.Motivo))
        {
            var quadrados = celulas.Select(k =>
            {
                var (x, y) = grid.Center(k % grid.Columns, k / grid.Columns);
                return (IReadOnlyList<(double X, double Y)>)[(x - meia, y - meia), (x + meia, y - meia), (x + meia, y + meia), (x - meia, y + meia)];
            });

            var aneis = ShadowUnion.Union(quadrados);

            // O rótulo na célula mais perto do centro da mancha (sempre dentro dela).
            var centros = celulas.Select(k => grid.Center(k % grid.Columns, k / grid.Columns)).ToList();
            var (mx, my) = (centros.Average(c => c.X), centros.Average(c => c.Y));
            var rotulo = centros.MinBy(c => (c.X - mx) * (c.X - mx) + (c.Y - my) * (c.Y - my));

            manchas.Add(new BasinShape(dono, motivo, aneis, celulas.Count * grid.Cell * grid.Cell, rotulo, celulas.Max(k => grid.StartZ[k])));
        }

        return manchas;
    }

    /// <summary>O motivo, em palavras, de a água não chegar a canaleta nenhuma.</summary>
    public static string Why(FlowStop? reason) => reason switch
    {
        FlowStop.Sink => Tr.T("empoça (fundo sem saída)"),
        FlowStop.OutsideTerrain => Tr.T("sai do terreno sem passar por canaleta"),
        FlowStop.LengthLimit => Tr.T("caminho longo demais sem chegar a canaleta"),
        _ => Tr.T("sem destino"),
    };
}
