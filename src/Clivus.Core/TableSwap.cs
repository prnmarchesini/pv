using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O lado da mesa antiga que fica preso quando ela é trocada.</summary>
public enum SwapAnchor
{
    /// <summary>A primeira mesa nova começa onde a antiga começava.</summary>
    Start,

    /// <summary>A última mesa nova termina onde a antiga terminava.</summary>
    End,
}

/// <summary>
/// O lado travado como o Renan vê na tela (10/10/2026): esquerda ou direita
/// em relação ao norte do desenho (WCS, norte = +Y), não o sentido em que a
/// fileira foi desenhada. Esquerda é a ponta de menor X; numa fileira quase
/// norte-sul, a ponta sul (menor Y).
/// </summary>
public enum RowSide
{
    /// <summary>A ponta oeste (menor X); norte-sul: a ponta sul.</summary>
    Left,

    /// <summary>A ponta leste (maior X); norte-sul: a ponta norte.</summary>
    Right,
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
    /// Abaixo deste |cosseno| da direção a fileira é tida como norte-sul
    /// (uns 1,1° do norte): aí a esquerda é a ponta sul.
    /// </summary>
    public const double NorthSouthCosine = 0.02;

    /// <summary>
    /// O lado da fileira (início ou fim, no sentido em que foi desenhada)
    /// que fica à <paramref name="side"/> olhando o desenho com o norte para
    /// cima.
    /// </summary>
    /// <param name="side">Esquerda ou direita, pelo norte do desenho.</param>
    /// <param name="directionRadians">A direção da fileira (do início para o fim), a partir do +X.</param>
    public static SwapAnchor AnchorOf(RowSide side, double directionRadians)
    {
        var c = Math.Cos(directionRadians);

        // O início fica à esquerda quando a fileira anda para o leste; quase
        // norte-sul, quando anda para o norte (o início é a ponta sul).
        var inicioAEsquerda = Math.Abs(c) >= NorthSouthCosine ? c > 0 : Math.Sin(directionRadians) > 0;

        return (side == RowSide.Left) == inicioAEsquerda ? SwapAnchor.Start : SwapAnchor.End;
    }

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
                [origem, fim, oposto, fundo], old.PartlyOutside, kind,
                Suffix: count == 1 ? old.Suffix : old.Suffix + (char)('a' + i)));
        }

        // O espaço livre: a antiga e, do lado solto, o vão até a vizinha menos
        // o espaçamento que precisa ficar. Sem vizinha, sem limite.
        var livre = roomBeyond is { } vao ? old.Length + vao - gap : double.PositiveInfinity;
        var passa = double.IsPositiveInfinity(livre) ? double.NegativeInfinity : total - livre;

        return new SwapResult(mesas, passa);
    }

    /// <summary>
    /// Reespaça uma fileira mantendo as mesas e os tipos (9.3): depois de
    /// uma troca, "regerar a fileira, aí pelo menos tudo fica blzera" (Renan,
    /// 03/10/2026), sem desfazer a troca. As mesas são postas na ordem da
    /// fileira, encostadas uma na outra com o espaçamento da configuração, a
    /// partir do início da primeira de cada trecho (ou, com
    /// <paramref name="side"/>, a partir da ponta daquele lado). Um vão maior
    /// que <paramref name="maxGap"/> (a fileira quebrada por um recorte da
    /// área) separa os trechos e fica como está.
    /// </summary>
    /// <param name="tables">As mesas da fileira, em qualquer ordem; o tipo de cada uma em <see cref="PlacedTable.Kind"/>.</param>
    /// <param name="footprints">A pegada de cada tipo.</param>
    /// <param name="gap">O espaçamento entre mesas.</param>
    /// <param name="maxGap">O vão que quebra a fileira.</param>
    /// <param name="side">
    /// A ponta de cada trecho que fica parada, pelo norte do desenho (a
    /// troca com "Refazer a fileira inteira"; Renan, 10/10/2026: "o
    /// travamento esquerda e direita tem que valer para as pontas da mesa
    /// inteira"). Null: o início de cada trecho, no sentido do desenho
    /// (Regerar fileira > Manter).
    /// </param>
    /// <returns>As mesas reespaçadas, na ordem da fileira, com o mesmo letreiro e tipo.</returns>
    public static IReadOnlyList<PlacedTable> Respace(
        IReadOnlyList<PlacedTable> tables, IReadOnlyList<TableFootprint> footprints, double gap, double maxGap, RowSide? side = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(footprints);
        if (tables.Count == 0) return [];

        var referencia = tables[0];
        var dx = Math.Cos(referencia.DirectionRadians);
        var dy = Math.Sin(referencia.DirectionRadians);
        var nx = referencia.Corners[3].X - referencia.Corners[0].X;
        var ny = referencia.Corners[3].Y - referencia.Corners[0].Y;
        var n = Math.Sqrt(nx * nx + ny * ny);
        if (n < 1e-9) throw new ArgumentException("a mesa de referência não tem fundo", nameof(tables));
        nx /= n;
        ny /= n;

        double Estacao(Point3 p) => (p.X - referencia.Origin.X) * dx + (p.Y - referencia.Origin.Y) * dy;
        double Lado(Point3 p) => (p.X - referencia.Origin.X) * nx + (p.Y - referencia.Origin.Y) * ny;

        var ordem = tables.OrderBy(t => Estacao(t.Origin)).ToList();

        foreach (var mesa in ordem)
        {
            if (mesa.Kind < 0 || mesa.Kind >= footprints.Count) throw new ArgumentException($"a mesa {mesa.Label} é de um tipo que não está na lista", nameof(tables));
        }

        // Os trechos: a fileira quebra onde o vão passa de maxGap.
        var trechos = new List<List<PlacedTable>>();
        var fimAnterior = double.NegativeInfinity;

        foreach (var mesa in ordem)
        {
            var inicio = Estacao(mesa.Origin);
            if (trechos.Count == 0 || inicio - fimAnterior > maxGap) trechos.Add([]);
            trechos[^1].Add(mesa);
            fimAnterior = Math.Max(fimAnterior, inicio + mesa.Length);   // uma mesa dentro de outra (troca sobreposta) não quebra o trecho
        }

        var ancora = side is { } lado ? AnchorOf(lado, referencia.DirectionRadians) : SwapAnchor.Start;
        var resultado = new List<PlacedTable>(ordem.Count);

        PlacedTable Posta(PlacedTable mesa, double estacao)
        {
            var pegada = footprints[mesa.Kind];

            // A faixa (o lado) é a da própria mesa: a fileira não entorta.
            var t = Lado(mesa.Origin);
            var origem = new Point3(referencia.Origin.X + dx * estacao + nx * t, referencia.Origin.Y + dy * estacao + ny * t, 0);
            var fim = new Point3(origem.X + dx * pegada.Length, origem.Y + dy * pegada.Length, 0);
            var oposto = new Point3(fim.X + nx * pegada.PlanDepth, fim.Y + ny * pegada.PlanDepth, 0);
            var fundo = new Point3(origem.X + nx * pegada.PlanDepth, origem.Y + ny * pegada.PlanDepth, 0);

            return mesa with
            {
                Origin = origem,
                DirectionRadians = referencia.DirectionRadians,
                Length = pegada.Length,
                PlanDepth = pegada.PlanDepth,
                Corners = [origem, fim, oposto, fundo],
            };
        }

        foreach (var trecho in trechos)
        {
            if (ancora == SwapAnchor.Start)
            {
                // O início do trecho fica; as mesas se encostam nele.
                var proxima = Estacao(trecho[0].Origin);

                foreach (var mesa in trecho)
                {
                    resultado.Add(Posta(mesa, proxima));
                    proxima += footprints[mesa.Kind].Length + gap;
                }
            }
            else
            {
                // O fim do trecho fica; as mesas se encostam nele, de trás
                // para a frente, e voltam à ordem da fileira.
                var termina = trecho.Max(m => Estacao(m.Origin) + m.Length);
                var postas = new List<PlacedTable>(trecho.Count);

                for (var i = trecho.Count - 1; i >= 0; i--)
                {
                    var comprimento = footprints[trecho[i].Kind].Length;
                    postas.Add(Posta(trecho[i], termina - comprimento));
                    termina -= comprimento + gap;
                }

                postas.Reverse();
                resultado.AddRange(postas);
            }
        }

        return resultado;
    }

    /// <summary>
    /// O vão livre entre a mesa e a vizinha do lado solto, medido ao longo da
    /// fileira, ou null sem vizinha desse lado. Vizinha é mesa da mesma faixa
    /// (o fundo dela cruza o da mesa, em planta) que fica toda além da ponta
    /// solta. Travado o início, o lado solto é o fim, e vice-versa.
    /// </summary>
    /// <param name="old">A mesa que sai.</param>
    /// <param name="others">Os cantos (em planta) das outras mesas do desenho.</param>
    /// <param name="anchor">O lado que fica preso.</param>
    public static double? RoomBeyond(PlacedTable old, IEnumerable<IReadOnlyList<Point3>> others, SwapAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(others);

        var dx = Math.Cos(old.DirectionRadians);
        var dy = Math.Sin(old.DirectionRadians);
        var nx = old.Corners[3].X - old.Corners[0].X;
        var ny = old.Corners[3].Y - old.Corners[0].Y;
        var n = Math.Sqrt(nx * nx + ny * ny);
        if (n < 1e-9) return null;
        nx /= n;
        ny /= n;

        const double Folga = 0.01;
        double? vao = null;

        foreach (var cantos in others)
        {
            if (cantos.Count == 0) continue;

            var s = cantos.Select(p => (p.X - old.Origin.X) * dx + (p.Y - old.Origin.Y) * dy).ToList();
            var t = cantos.Select(p => (p.X - old.Origin.X) * nx + (p.Y - old.Origin.Y) * ny).ToList();

            // Outra faixa (outra fileira): o fundo não cruza o da mesa.
            if (t.Max() <= Folga || t.Min() >= old.PlanDepth - Folga) continue;

            double? este = anchor == SwapAnchor.Start
                ? (s.Min() >= old.Length - Folga ? s.Min() - old.Length : null)
                : (s.Max() <= Folga ? -s.Max() : null);

            if (este is { } v && (vao is null || v < vao)) vao = v;
        }

        return vao;
    }
}
