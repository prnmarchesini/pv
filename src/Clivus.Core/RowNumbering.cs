using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma mesa a numerar: o GUID e a célula em planta reconstruída do contorno (<see cref="TableCells"/>).</summary>
public sealed record TableToNumber(Guid Id, PlacedTable Cell);

/// <summary>Uma mesa numerada: fileira e número novos.</summary>
public sealed record NumberedTable(Guid Id, int Row, int Number)
{
    /// <summary>O letreiro: F1.1, F1.2, F2.1…</summary>
    public string Label => $"F{Row}.{Number}";
}

/// <summary>O resultado da numeração.</summary>
/// <param name="Tables">Toda mesa de entrada, com a fileira e o número novos.</param>
/// <param name="RowCount">Quantas fileiras saíram.</param>
/// <param name="Warnings">O que o usuário deve saber (a F1.1 indicada não era a ponta, etc.), em português.</param>
public sealed record NumberingResult(IReadOnlyList<NumberedTable> Tables, int RowCount, IReadOnlyList<string> Warnings);

/// <summary>
/// A numeração (7.10): fileira = mesas contínuas na mesma reta e no mesmo
/// azimute. Vão acima do limite ou azimute diferente abre fileira nova. O
/// usuário indica a mesa que será a F1.1 e uma mesa da última fileira; as
/// fileiras são ordenadas da primeira para a última, e dentro de cada
/// fileira as mesas correm a partir da ponta em que está a F1.1. Fileira sem
/// mesa não existe, então não há número pulado.
///
/// Puro: recebe células, devolve números. Quem regrava letreiro é o plugin.
/// </summary>
public static class RowNumbering
{
    /// <summary>Duas mesas estão no mesmo azimute se as direções diferem menos que isto (1°).</summary>
    public const double SameAzimuthRadians = Math.PI / 180;

    /// <summary>
    /// Numera.
    /// </summary>
    /// <param name="tables">As mesas, GUIDs sem repetição.</param>
    /// <param name="firstTable">O GUID da mesa que será a F1.1.</param>
    /// <param name="lastRowTable">O GUID de uma mesa da última fileira (diz o sentido em que as fileiras crescem).</param>
    /// <param name="maxGap">O vão, em metro, acima do qual duas mesas na mesma reta são de fileiras diferentes.</param>
    /// <exception cref="ArgumentException">Sem mesa, GUID repetido, ou a F1.1/última não está na lista.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Vão que não é medida.</exception>
    public static NumberingResult Number(IReadOnlyList<TableToNumber> tables, Guid firstTable, Guid lastRowTable, double maxGap)
    {
        ArgumentNullException.ThrowIfNull(tables);

        if (tables.Count == 0) throw new ArgumentException("Não há mesa para numerar.", nameof(tables));
        if (tables.Select(t => t.Id).Distinct().Count() != tables.Count)
            throw new ArgumentException("Há GUID de mesa repetido.", nameof(tables));
        if (!double.IsFinite(maxGap) || maxGap < 0)
            throw new ArgumentOutOfRangeException(nameof(maxGap), "O vão máximo precisa ser uma medida.");

        var primeira = tables.FirstOrDefault(t => t.Id == firstTable)
            ?? throw new ArgumentException("A mesa indicada para F1.1 não está na lista.", nameof(firstTable));

        if (tables.All(t => t.Id != lastRowTable))
            throw new ArgumentException("A mesa indicada como da última fileira não está na lista.", nameof(lastRowTable));

        var avisos = new List<string>();

        // A tolerância de "mesma reta": meio fundo de mesa. Serve também para
        // dizer que duas fileiras estão à mesma distância da F1.1.
        var tolerancia = 0.5 * tables.Min(t => t.Cell.PlanDepth);

        // 1. A direção de referência é a da F1.1; toda direção é dobrada para
        //    o mesmo lado (mesa girada de 180° é a mesma reta).
        var d0 = Direcao(primeira.Cell.DirectionRadians);
        var medidas = tables.Select(t => new Medida(t, d0)).ToList();

        // 2. Por azimute: cada mesa é comparada com a PRIMEIRA do grupo, para
        //    a tolerância não se acumular em cadeia.
        var fileiras = new List<Fileira>();

        foreach (var grupoDeAzimute in Agrupar(medidas.OrderBy(m => m.DesvioDeAzimute).ToList(), (primeiro, _, m) => m.DesvioDeAzimute - primeiro.DesvioDeAzimute <= SameAzimuthRadians))
        {
            // 3. Pela reta: a distância perpendicular do centro à referência.
            var dc = grupoDeAzimute[0].Direcao;
            var nc = new Point3(-dc.Y, dc.X, 0);

            foreach (var m in grupoDeAzimute) m.Medir(dc, nc);

            foreach (var reta in Agrupar(grupoDeAzimute.OrderBy(m => m.Afastamento).ToList(), (_, anterior, m) => m.Afastamento - anterior.Afastamento <= tolerancia))
            {
                // 4. Pela continuidade ao longo da reta.
                foreach (var trecho in Agrupar(reta.OrderBy(m => m.Inicio).ToList(), (_, anterior, m) => m.Inicio - anterior.Fim <= maxGap + 1e-3))
                {
                    fileiras.Add(new Fileira(trecho));
                }
            }
        }

        var fileiraDaPrimeira = fileiras.Single(f => f.Mesas.Any(m => m.Mesa.Id == firstTable));
        var fileiraDaUltima = fileiras.Single(f => f.Mesas.Any(m => m.Mesa.Id == lastRowTable));

        // 5. O sentido dentro da fileira: a F1.1 fica na ponta em que está.
        var naFileira = fileiraDaPrimeira.Mesas;
        var posicao = naFileira.FindIndex(m => m.Mesa.Id == firstTable);
        var inverter = naFileira.Count > 1 && posicao == naFileira.Count - 1;

        if (naFileira.Count > 1 && posicao > 0 && posicao < naFileira.Count - 1)
            avisos.Add(Tr.F("a mesa indicada para F1.1 não está na ponta da fileira: ficou F1.{0}", posicao + 1));

        // 6. A ordem das fileiras: o eixo é a perpendicular às fileiras da
        //    F1.1, apontada para a última; se a última está na mesma reta,
        //    no sentido da subida da mesa.
        var eixo = new Point3(-d0.Y, d0.X, 0);
        var sentido = eixo.X * (fileiraDaUltima.Centro.X - fileiraDaPrimeira.Centro.X) + eixo.Y * (fileiraDaUltima.Centro.Y - fileiraDaPrimeira.Centro.Y);

        if (Math.Abs(sentido) < tolerancia)
        {
            var c = primeira.Cell.Corners;
            sentido = eixo.X * (c[3].X - c[0].X) + eixo.Y * (c[3].Y - c[0].Y);

            if (fileiraDaPrimeira == fileiraDaUltima && fileiras.Count > 1)
                avisos.Add(Tr.T("a mesa da F1.1 e a da última fileira estão na mesma fileira; as fileiras foram ordenadas no sentido da subida da mesa"));
        }

        if (sentido < 0) eixo = new Point3(-eixo.X, -eixo.Y, 0);

        // Fileiras à mesma distância (dentro da tolerância) são a mesma
        // "faixa": entre elas, primeiro a da F1.1, depois as mais próximas
        // dela ao longo da reta. Sem isto, duas fileiras colineares seriam
        // ordenadas pelo ruído do ponto flutuante das coordenadas UTM.
        var faixa = 0;
        var chaveAnterior = double.NegativeInfinity;
        var faixas = new Dictionary<Fileira, int>();

        foreach (var f in fileiras.OrderBy(f => f.Centro.X * eixo.X + f.Centro.Y * eixo.Y))
        {
            var chave = f.Centro.X * eixo.X + f.Centro.Y * eixo.Y;
            if (chave - chaveAnterior > tolerancia) faixa++;
            chaveAnterior = chave;
            faixas[f] = faixa;
        }

        var referencia = fileiraDaPrimeira.Mesas[0].Inicio;

        var ordenadas = fileiras
            .OrderBy(f => faixas[f])
            .ThenBy(f => Math.Abs(f.Mesas[0].Inicio - referencia))
            .ToList();

        var numeradas = new List<NumberedTable>();

        for (var f = 0; f < ordenadas.Count; f++)
        {
            var mesas = inverter ? Enumerable.Reverse(ordenadas[f].Mesas).ToList() : ordenadas[f].Mesas;

            for (var n = 0; n < mesas.Count; n++) numeradas.Add(new NumberedTable(mesas[n].Mesa.Id, f + 1, n + 1));
        }

        var fileiraDaPrimeiraNumero = ordenadas.IndexOf(fileiraDaPrimeira) + 1;
        var fileiraDaUltimaNumero = ordenadas.IndexOf(fileiraDaUltima) + 1;

        if (fileiraDaPrimeiraNumero != 1)
            avisos.Add(Tr.F("a mesa indicada para F1.1 ficou na F{0}: há {1} fileira(s) antes dela, no sentido contrário", fileiraDaPrimeiraNumero, fileiraDaPrimeiraNumero - 1));

        if (fileiraDaUltimaNumero != ordenadas.Count)
            avisos.Add(Tr.F("a mesa indicada como da última fileira ficou na F{0}: há {1} fileira(s) depois dela", fileiraDaUltimaNumero, ordenadas.Count - fileiraDaUltimaNumero));

        // Fileiras com número de mesas diferente da maioria: o sintoma visível
        // de mesa girada, de vão ou de reta mal agrupada.
        var maioria = ordenadas.GroupBy(f => f.Mesas.Count).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
        var diferentes = ordenadas.Select((f, i) => (Numero: i + 1, Mesas: f.Mesas.Count)).Where(f => f.Mesas != maioria).ToList();

        if (diferentes.Count > 0)
        {
            avisos.Add(
                Tr.F("as fileiras não têm o mesmo número de mesas: a maioria tem {0}, e {1}",
                    maioria, string.Join(", ", diferentes.Select(f => Tr.F("F{0} tem {1}", f.Numero, f.Mesas)))));
        }

        return new NumberingResult(numeradas, ordenadas.Count, avisos);
    }

    private static Point3 Direcao(double radianos) => new(Math.Cos(radianos), Math.Sin(radianos), 0);

    /// <summary>
    /// Agrupa uma lista já ordenada: o item entra no grupo se
    /// <paramref name="junto"/>(primeiro do grupo, anterior, item).
    /// </summary>
    private static IEnumerable<List<Medida>> Agrupar(List<Medida> ordenadas, Func<Medida, Medida, Medida, bool> junto)
    {
        var grupo = new List<Medida>();

        foreach (var m in ordenadas)
        {
            if (grupo.Count > 0 && !junto(grupo[0], grupo[^1], m))
            {
                yield return grupo;
                grupo = [];
            }

            grupo.Add(m);
        }

        if (grupo.Count > 0) yield return grupo;
    }

    /// <summary>Uma mesa com as medidas que a numeração usa.</summary>
    private sealed class Medida
    {
        public Medida(TableToNumber mesa, Point3 referencia)
        {
            Mesa = mesa;

            var c = mesa.Cell.Corners;
            Centro = new Point3(c.Average(p => p.X), c.Average(p => p.Y), 0);

            var d = Direcao(mesa.Cell.DirectionRadians);
            if (d.X * referencia.X + d.Y * referencia.Y < 0) d = new Point3(-d.X, -d.Y, 0);
            Direcao = d;

            // O desvio de azimute em relação à referência, em (-π/2, π/2].
            DesvioDeAzimute = Math.Atan2(referencia.X * d.Y - referencia.Y * d.X, referencia.X * d.X + referencia.Y * d.Y);
        }

        public TableToNumber Mesa { get; }
        public Point3 Centro { get; }
        public Point3 Direcao { get; }
        public double DesvioDeAzimute { get; }

        /// <summary>A distância perpendicular do centro à reta de referência do grupo.</summary>
        public double Afastamento { get; private set; }

        /// <summary>Onde a mesa começa e acaba ao longo da reta (a projeção dos cantos).</summary>
        public double Inicio { get; private set; }
        public double Fim { get; private set; }

        public void Medir(Point3 dc, Point3 nc)
        {
            Afastamento = Centro.X * nc.X + Centro.Y * nc.Y;

            var projecoes = Mesa.Cell.Corners.Select(p => p.X * dc.X + p.Y * dc.Y).ToList();
            Inicio = projecoes.Min();
            Fim = projecoes.Max();
        }
    }

    private sealed class Fileira
    {
        public Fileira(List<Medida> mesas)
        {
            Mesas = mesas;
            Centro = new Point3(mesas.Average(m => m.Centro.X), mesas.Average(m => m.Centro.Y), 0);
        }

        /// <summary>As mesas, do início ao fim da reta.</summary>
        public List<Medida> Mesas { get; }
        public Point3 Centro { get; }
    }
}
