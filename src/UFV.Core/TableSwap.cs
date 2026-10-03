using UFV.Geo;

namespace UFV.Core;

/// <summary>O lado da mesa antiga que fica preso quando ela é trocada.</summary>
public enum SwapAnchor
{
    /// <summary>A primeira mesa nova começa onde a antiga começava.</summary>
    Start,

    /// <summary>A última mesa nova termina onde a antiga terminava.</summary>
    End,
}

/// <summary>
/// As mesas novas no lugar da antiga, e quanto elas passam do espaço que
/// havia.
/// </summary>
/// <param name="Tables">As mesas novas, na ordem da fileira (da estação menor para a maior).</param>
/// <param name="Overflow">
/// Quanto as mesas novas passam do espaço livre, em metro (a antiga mais o
/// que sobra até a vizinha do lado solto, menos o espaçamento). Positivo:
/// encostam ou entram na vizinha. Negativo: sobra espaço.
/// </param>
public sealed record SwapResult(IReadOnlyList<PlacedTable> Tables, double Overflow)
{
    /// <summary>Se as mesas novas passam do espaço livre (com um milímetro de folga para a conta).</summary>
    public bool Overflows => Overflow > 1e-3;
}

/// <summary>
/// Trocar uma mesa por outra(s), em planta (passo 9.1). Renan, 03/10/2026:
/// "clicar em uma mesa e ter a opção de trocar por outra, por exemplo,
/// trocar uma de 28 por uma de 14, e escolher quantas colocar no lugar ...
/// eu só falo o lado que quero travar e o sistema refaz". E sobre o
/// espaçamento: "pode dar merda no espaçamento, mas é problema meu" — a
/// conta diz quanto passa e não move as vizinhas; Regerar fileira acerta.
///
/// Só planta: a cota vem depois, do motor, como em qualquer mesa.
/// </summary>
public static class TableSwap
{
    /// <summary>
    /// As <paramref name="count"/> mesas de pegada <paramref name="footprint"/>
    /// no lugar de <paramref name="old"/>, encostadas no lado travado, com
    /// <paramref name="gap"/> entre elas.
    /// </summary>
    /// <param name="old">A mesa que sai.</param>
    /// <param name="footprint">A pegada do tipo novo.</param>
    /// <param name="kind">O índice do tipo novo, gravado em cada mesa nova.</param>
    /// <param name="count">Quantas mesas novas, de 1 a 10.</param>
    /// <param name="gap">O espaçamento entre mesas da configuração.</param>
    /// <param name="anchor">O lado que fica preso.</param>
    /// <param name="roomBeyond">
    /// O vão livre entre a antiga e a vizinha do lado solto (o espaçamento
    /// incluído), ou null sem vizinha desse lado (fim da fileira).
    /// </param>
    public static SwapResult Plan(
        PlacedTable old, TableFootprint footprint, int kind, int count, double gap, SwapAnchor anchor, double? roomBeyond)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(footprint);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 10);
        if (!(footprint.Length > 0) || !(footprint.PlanDepth > 0)) throw new ArgumentException("a mesa nova precisa de comprimento e fundo positivos", nameof(footprint));
        if (!(gap >= 0)) throw new ArgumentOutOfRangeException(nameof(gap), gap, "o espaçamento entre mesas não pode ser negativo");
        if (old.Corners.Count < 4) throw new ArgumentException("a mesa antiga precisa dos quatro cantos", nameof(old));

        var dx = Math.Cos(old.DirectionRadians);
        var dy = Math.Sin(old.DirectionRadians);

        // O fundo sai dos cantos da antiga: o lado das mesas não muda.
        var nx = old.Corners[3].X - old.Corners[0].X;
        var ny = old.Corners[3].Y - old.Corners[0].Y;
        var n = Math.Sqrt(nx * nx + ny * ny);
        if (n < 1e-9) throw new ArgumentException("a mesa antiga não tem fundo", nameof(old));
        nx /= n;
        ny /= n;

        var total = count * footprint.Length + (count - 1) * gap;

        // Estação (ao longo da fileira, da origem da antiga) onde começa a primeira nova.
        var inicio = anchor == SwapAnchor.Start ? 0 : old.Length - total;

        var mesas = new List<PlacedTable>(count);

        for (var i = 0; i < count; i++)
        {
            var s = inicio + i * (footprint.Length + gap);
            var origem = new Point3(old.Origin.X + dx * s, old.Origin.Y + dy * s, 0);
            var fim = new Point3(origem.X + dx * footprint.Length, origem.Y + dy * footprint.Length, 0);
            var oposto = new Point3(fim.X + nx * footprint.PlanDepth, fim.Y + ny * footprint.PlanDepth, 0);
            var fundo = new Point3(origem.X + nx * footprint.PlanDepth, origem.Y + ny * footprint.PlanDepth, 0);

            mesas.Add(new PlacedTable(
                old.Row, old.Number, origem, old.DirectionRadians, footprint.Length, footprint.PlanDepth,
                [origem, fim, oposto, fundo], old.PartlyOutside, kind));
        }

        // O espaço livre: a antiga e, do lado solto, o vão até a vizinha menos
        // o espaçamento que precisa ficar. Sem vizinha, sem limite.
        var livre = roomBeyond is { } vao ? old.Length + vao - gap : double.PositiveInfinity;
        var passa = double.IsPositiveInfinity(livre) ? double.NegativeInfinity : total - livre;

        return new SwapResult(mesas, passa);
    }
}
