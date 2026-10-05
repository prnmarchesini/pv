using System.Globalization;
using System.Text;

namespace Clivus.Core;

/// <summary>
/// Uma célula do plano cartesiano de um tipo de string: a mesa da assinatura
/// (na ordem da fileira), a coluna e a fileira do módulo no cartesiano
/// (fileira 0 = a de baixo, a da ponta baixa).
/// </summary>
public readonly record struct RoutingCell(int Table, int Column, int Row)
{
    /// <summary>"0.13.1": mesa, coluna, fileira.</summary>
    public string ToText() => string.Create(CultureInfo.InvariantCulture, $"{Table}.{Column}.{Row}");

    public static RoutingCell? Parse(string texto)
    {
        var partes = texto.Split('.');
        if (partes.Length != 3) return null;

        var n = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(partes[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i])) return null;

        return new RoutingCell(n[0], n[1], n[2]);
    }
}

/// <summary>O tipo de um trecho do traçado (11.3, 11.4).</summary>
public enum RoutingKind
{
    /// <summary>Módulo a módulo, em sequência: as pontas do trecho ficam opostas.</summary>
    Conventional,

    /// <summary>Alternado: vai pulando um e volta pelos pulados; a ponta fica ao lado do começo.</summary>
    Leapfrog,
}

/// <summary>
/// Um trecho do traçado: termina na célula de índice <see cref="End"/> (e
/// começa onde o anterior terminou, ou na primeira célula).
/// </summary>
public sealed record RoutingSegment(int End, RoutingKind Kind);

/// <summary>
/// O traçado de uma string de um tipo (11.3, 11.4): as células na ordem
/// elétrica, do positivo (a primeira) ao negativo (a última), e os trechos
/// (convencional ou leapfrog). O número de módulos da string é o número de
/// células.
/// </summary>
public sealed class StringRoute : IEquatable<StringRoute>
{
    public StringRoute(IReadOnlyList<RoutingCell> cells, IReadOnlyList<RoutingSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(segments);
        Cells = cells.ToList();
        Segments = segments.ToList();
    }

    public IReadOnlyList<RoutingCell> Cells { get; }

    public IReadOnlyList<RoutingSegment> Segments { get; }

    public int ModuleCount => Cells.Count;

    /// <summary>A ponta positiva (+).</summary>
    public RoutingCell Positive => Cells[0];

    /// <summary>A ponta negativa (−).</summary>
    public RoutingCell Negative => Cells[^1];

    /// <summary>
    /// Se a forma é de traçado: duas células ou mais (o + e o − em módulos
    /// diferentes), sem módulo repetido, trechos em ordem e cobrindo até a
    /// última célula.
    /// </summary>
    public bool IsWellFormed =>
        Cells.Count >= 2
        && Cells.Distinct().Count() == Cells.Count
        && Segments.Count > 0
        && Segments[0].End >= 1
        && Segments.Zip(Segments.Skip(1), (a, b) => b.End > a.End).All(x => x)
        && Segments[^1].End == Cells.Count - 1
        && Segments.All(s => Enum.IsDefined(s.Kind));

    /// <summary>A mesma string com o positivo e o negativo trocados (espelhar, 11.5).</summary>
    public StringRoute Mirrored()
    {
        var n = Cells.Count;
        var celulas = Cells.Reverse().ToList();

        // O trecho k ia de (fim do k-1, ou 0) até End; ao contrário, vai de
        // n-1-End até n-1-começo. Os fins novos são os começos antigos.
        var trechos = new List<RoutingSegment>();
        for (var k = Segments.Count - 1; k >= 0; k--)
        {
            var comeco = k == 0 ? 0 : Segments[k - 1].End;
            trechos.Add(new RoutingSegment(n - 1 - comeco, Segments[k].Kind));
        }

        return new StringRoute(celulas, trechos);
    }

    /// <summary>O tipo do trecho que leva da célula i-1 à célula i.</summary>
    public RoutingKind KindOfStep(int i) => Segments.FirstOrDefault(s => s.End >= i)?.Kind ?? RoutingKind.Conventional;

    /// <summary>"C13,L27|0.0.0,0.1.0,...": os trechos (tipo e fim) e as células.</summary>
    public string ToText() =>
        string.Join(',', Segments.Select(s => (s.Kind == RoutingKind.Leapfrog ? "L" : "C") + s.End.ToString(CultureInfo.InvariantCulture)))
        + "|" + string.Join(',', Cells.Select(c => c.ToText()));

    public static StringRoute? Parse(string texto)
    {
        var lados = texto.Split('|');
        if (lados.Length != 2 || lados[0].Length == 0 || lados[1].Length == 0) return null;

        var trechos = new List<RoutingSegment>();
        foreach (var t in lados[0].Split(','))
        {
            if (t.Length < 2 || (t[0] != 'C' && t[0] != 'L')) return null;
            if (!int.TryParse(t.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fim)) return null;
            trechos.Add(new RoutingSegment(fim, t[0] == 'L' ? RoutingKind.Leapfrog : RoutingKind.Conventional));
        }

        var celulas = new List<RoutingCell>();
        foreach (var c in lados[1].Split(','))
        {
            if (RoutingCell.Parse(c) is not { } celula) return null;
            celulas.Add(celula);
        }

        var rota = new StringRoute(celulas, trechos);
        return rota.IsWellFormed ? rota : null;
    }

    public bool Equals(StringRoute? other) => other is not null && Cells.SequenceEqual(other.Cells) && Segments.SequenceEqual(other.Segments);

    public override bool Equals(object? obj) => Equals(obj as StringRoute);

    public override int GetHashCode() => HashCode.Combine(Cells.Count, Cells.Count > 0 ? Cells[0] : default);
}

/// <summary>
/// As contas do traçado sobre a assinatura de arranjo (11.3, 11.4): que
/// célula existe, a corrida de uma célula a outra pela fileira ou pela
/// coluna, o padrão leapfrog e a conferência do traçado de um tipo.
/// </summary>
public static class StringRouting
{
    /// <summary>Se a célula existe na assinatura.</summary>
    public static bool Exists(StringArrangement arranjo, RoutingCell c) =>
        c.Table >= 0 && c.Table < arranjo.Tables.Count
        && c.Column >= 0 && c.Column < arranjo.Tables[c.Table].Columns
        && c.Row >= 0 && c.Row < arranjo.Tables[c.Table].Rows;

    /// <summary>A coluna da célula contando as mesas anteriores (o X do cartesiano em módulos).</summary>
    public static int GlobalColumn(StringArrangement arranjo, RoutingCell c) =>
        arranjo.Tables.Take(c.Table).Sum(t => t.Columns) + c.Column;

    /// <summary>A célula da coluna global e fileira dadas, ou null se não existe (mesa 1V não tem a fileira de cima).</summary>
    public static RoutingCell? AtGlobal(StringArrangement arranjo, int colunaGlobal, int fileira)
    {
        if (colunaGlobal < 0) return null;

        for (var t = 0; t < arranjo.Tables.Count; t++)
        {
            var mesa = arranjo.Tables[t];
            if (colunaGlobal < mesa.Columns) return fileira >= 0 && fileira < mesa.Rows ? new RoutingCell(t, colunaGlobal, fileira) : null;
            colunaGlobal -= mesa.Columns;
        }

        return null;
    }

    /// <summary>
    /// A corrida de <paramref name="de"/> a <paramref name="ate"/>, as duas
    /// incluídas: pela fileira do cartesiano (atravessando mesas) ou pela
    /// coluna de uma mesa. Null e o porquê se as duas não estão na mesma
    /// fileira nem na mesma coluna, ou se a corrida passa por um buraco.
    /// </summary>
    public static IReadOnlyList<RoutingCell>? Run(StringArrangement arranjo, RoutingCell de, RoutingCell ate, out string? problema)
    {
        problema = null;

        if (!Exists(arranjo, de) || !Exists(arranjo, ate))
        {
            problema = Tr.T("o módulo não existe neste tipo");
            return null;
        }

        var corrida = new List<RoutingCell>();

        if (de.Row == ate.Row)
        {
            var g0 = GlobalColumn(arranjo, de);
            var g1 = GlobalColumn(arranjo, ate);
            var passo = Math.Sign(g1 - g0);

            for (var g = g0; ; g += passo)
            {
                if (AtGlobal(arranjo, g, de.Row) is not { } celula)
                {
                    problema = Tr.T("o trecho passa por uma mesa sem essa fileira");
                    return null;
                }

                corrida.Add(celula);
                if (g == g1) break;
            }

            return corrida;
        }

        if (de.Table == ate.Table && de.Column == ate.Column)
        {
            var passo = Math.Sign(ate.Row - de.Row);
            for (var r = de.Row; ; r += passo)
            {
                corrida.Add(de with { Row = r });
                if (r == ate.Row) break;
            }

            return corrida;
        }

        problema = Tr.T("o trecho tem que seguir uma fileira ou uma coluna");
        return null;
    }

    /// <summary>
    /// O leapfrog sobre a corrida (o primeiro já está no traçado): vai pelos
    /// de índice par (2, 4, 6...) até o fim e volta pelos ímpares; termina no
    /// segundo, ao lado do primeiro. Devolve só as células novas.
    /// </summary>
    public static IReadOnlyList<RoutingCell> Leapfrog(IReadOnlyList<RoutingCell> corrida)
    {
        ArgumentNullException.ThrowIfNull(corrida);

        var ida = Enumerable.Range(1, Math.Max(0, corrida.Count - 1)).Where(i => i % 2 == 0);
        var volta = Enumerable.Range(1, Math.Max(0, corrida.Count - 1)).Where(i => i % 2 == 1).Reverse();
        return ida.Concat(volta).Select(i => corrida[i]).ToList();
    }

    /// <summary>
    /// A fileira inteira do cartesiano numa string só (11.4: o botão leapfrog
    /// "monta o padrão alternado na fileira inteira de uma vez"; o mesmo
    /// botão em convencional liga a fileira em sequência). Começa (o +) na
    /// ponta da fileira mais perto da célula clicada.
    /// </summary>
    public static StringRoute? WholeRow(StringArrangement arranjo, RoutingCell clicada, RoutingKind tipo, out string? problema)
    {
        problema = null;
        if (!Exists(arranjo, clicada))
        {
            problema = Tr.T("o módulo não existe neste tipo");
            return null;
        }

        var colunas = arranjo.Tables.Sum(t => t.Columns);
        var g = GlobalColumn(arranjo, clicada);
        var (inicio, fim) = g <= colunas - 1 - g ? (0, colunas - 1) : (colunas - 1, 0);

        if (AtGlobal(arranjo, inicio, clicada.Row) is not { } a || AtGlobal(arranjo, fim, clicada.Row) is not { } b)
        {
            problema = Tr.T("o trecho passa por uma mesa sem essa fileira");
            return null;
        }

        if (Run(arranjo, a, b, out problema) is not { } corrida) return null;
        if (corrida.Count < 2)
        {
            problema = Tr.T("a string precisa de dois módulos ou mais");
            return null;
        }

        var celulas = tipo == RoutingKind.Leapfrog ? [corrida[0], .. Leapfrog(corrida)] : corrida.ToList();
        return new StringRoute(celulas, [new RoutingSegment(celulas.Count - 1, tipo)]);
    }

    /// <summary>
    /// Confere o traçado de um tipo: cada string bem formada, toda célula
    /// existe na assinatura, nenhum módulo em duas strings. Null se está
    /// certo, o porquê se não.
    /// </summary>
    public static string? WhyInvalid(StringArrangement arranjo, IReadOnlyList<StringRoute> strings)
    {
        ArgumentNullException.ThrowIfNull(arranjo);
        ArgumentNullException.ThrowIfNull(strings);

        var usadas = new HashSet<RoutingCell>();

        for (var i = 0; i < strings.Count; i++)
        {
            var s = strings[i];
            if (s.Cells.Count < 2) return Tr.F("a string {0} precisa de dois módulos ou mais (o + e o −)", i + 1);
            if (!s.IsWellFormed) return Tr.F("a string {0} tem módulo repetido ou trecho torto", i + 1);
            if (s.Cells.Any(c => !Exists(arranjo, c)))
                return Tr.F("a string {0} usa um módulo que não existe nas mesas do tipo", i + 1);

            foreach (var c in s.Cells)
                if (!usadas.Add(c)) return Tr.F("a string {0} usa um módulo que já está em outra string", i + 1);
        }

        return null;
    }

    /// <summary>Quantos módulos do arranjo ficam sem string.</summary>
    public static int Uncovered(StringArrangement arranjo, IReadOnlyList<StringRoute> strings) =>
        arranjo.ModuleCount - strings.Sum(s => s.Cells.Count);

    /// <summary>As strings em texto ("/" entre uma e outra), para gravar.</summary>
    public static string ToText(IReadOnlyList<StringRoute> strings) => string.Join('/', strings.Select(s => s.ToText()));

    /// <summary>O inverso de <see cref="ToText"/>; null se o texto está estragado.</summary>
    public static IReadOnlyList<StringRoute>? Parse(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return [];

        var lista = new List<StringRoute>();
        foreach (var parte in texto.Split('/'))
        {
            if (StringRoute.Parse(parte) is not { } s) return null;
            lista.Add(s);
        }

        return lista;
    }
}

/// <summary>
/// O traçado de uma string sendo montado por cliques no cartesiano (11.3,
/// 11.4): o primeiro clique marca o + e cada clique seguinte estende até a
/// célula clicada, pela fileira ou pela coluna, em convencional (módulo a
/// módulo) ou leapfrog (alternado). Concluir dá a string, com o − no último.
/// </summary>
public sealed class RouteBuilder
{
    private readonly StringArrangement _arranjo;
    private readonly HashSet<RoutingCell> _ocupadas;
    private readonly List<RoutingCell> _celulas = [];
    private readonly List<RoutingSegment> _trechos = [];

    /// <param name="arranjo">A assinatura do tipo.</param>
    /// <param name="ocupadas">As células das outras strings do tipo (não podem entrar nesta).</param>
    public RouteBuilder(StringArrangement arranjo, IEnumerable<RoutingCell> ocupadas)
    {
        ArgumentNullException.ThrowIfNull(arranjo);
        ArgumentNullException.ThrowIfNull(ocupadas);
        _arranjo = arranjo;
        _ocupadas = ocupadas.ToHashSet();
    }

    public IReadOnlyList<RoutingCell> Cells => _celulas;

    public IReadOnlyList<RoutingSegment> Segments => _trechos;

    public bool IsEmpty => _celulas.Count == 0;

    /// <summary>O clique: marca o + (primeiro) ou estende até a célula. Null se aceitou, o porquê se não.</summary>
    public string? Click(RoutingCell celula, RoutingKind tipo)
    {
        if (!StringRouting.Exists(_arranjo, celula)) return Tr.T("o módulo não existe neste tipo");
        if (_ocupadas.Contains(celula)) return Tr.T("esse módulo já está em outra string");

        if (_celulas.Count == 0)
        {
            _celulas.Add(celula);
            return null;
        }

        if (_celulas.Contains(celula)) return Tr.T("esse módulo já está nesta string");
        if (StringRouting.Run(_arranjo, _celulas[^1], celula, out var problema) is not { } corrida) return problema;

        var novas = tipo == RoutingKind.Leapfrog ? StringRouting.Leapfrog(corrida) : corrida.Skip(1).ToList();
        if (novas.Any(c => _ocupadas.Contains(c) || _celulas.Contains(c)))
            return Tr.T("o trecho passa por um módulo que já tem string");

        _celulas.AddRange(novas);
        _trechos.Add(new RoutingSegment(_celulas.Count - 1, tipo));
        return null;
    }

    /// <summary>Desfaz o último trecho (ou o +, se só há ele). Se havia o que desfazer.</summary>
    public bool Undo()
    {
        if (_trechos.Count > 0)
        {
            var anterior = _trechos.Count > 1 ? _trechos[^2].End : 0;
            _trechos.RemoveAt(_trechos.Count - 1);
            _celulas.RemoveRange(anterior + 1, _celulas.Count - anterior - 1);
            return true;
        }

        if (_celulas.Count == 0) return false;
        _celulas.Clear();
        return true;
    }

    /// <summary>
    /// O − marcado com Ctrl + clique (05/10/2026): fecha a string nesta célula.
    /// Convencional: estende até ela e conclui. Leapfrog: se a montagem já
    /// termina nela (ponto de virada clicado antes), conclui; se só há o +, o
    /// − é o vizinho dele e a fileira inteira, no sentido do −, vira o
    /// alternado que termina no −. Null e o porquê se não fecha; a montagem
    /// fica como estava.
    /// </summary>
    public StringRoute? FinishAt(RoutingCell negativo, RoutingKind tipo, out string? problema)
    {
        problema = null;

        if (_celulas.Count == 0)
        {
            problema = Tr.T("marque primeiro o + (Ctrl + clique no módulo onde a string começa)");
            return null;
        }

        if (negativo == _celulas[0])
        {
            problema = Tr.T("o − não pode ficar no mesmo módulo do +");
            return null;
        }

        if (_celulas[^1] == negativo) return Finish(out problema);

        if (tipo == RoutingKind.Leapfrog)
        {
            if (_celulas.Count > 1)
            {
                problema = Tr.T("no leapfrog o − fica no fim do trecho alternado: desfaça o trecho ou marque o − no último módulo dele");
                return null;
            }

            var mais = _celulas[0];
            var g = StringRouting.GlobalColumn(_arranjo, mais);
            var gn = StringRouting.GlobalColumn(_arranjo, negativo);
            if (negativo.Row != mais.Row || Math.Abs(gn - g) != 1)
            {
                problema = Tr.T("no leapfrog o − fica no módulo vizinho do +, na mesma fileira");
                return null;
            }

            var fim = gn > g ? _arranjo.Tables.Sum(t => t.Columns) - 1 : 0;
            if (StringRouting.AtGlobal(_arranjo, fim, mais.Row) is not { } ponta
                || StringRouting.Run(_arranjo, mais, ponta, out problema) is not { } corrida)
            {
                problema ??= Tr.T("o trecho passa por uma mesa sem essa fileira");
                return null;
            }

            var novas = StringRouting.Leapfrog(corrida);
            if (novas.Count == 0 || novas[^1] != negativo)
            {
                problema = Tr.T("no leapfrog o − fica no módulo vizinho do +, na mesma fileira");
                return null;
            }

            if (novas.Any(c => _ocupadas.Contains(c)))
            {
                problema = Tr.T("o trecho passa por um módulo que já tem string");
                return null;
            }

            _celulas.AddRange(novas);
            _trechos.Add(new RoutingSegment(_celulas.Count - 1, tipo));
            return Finish(out problema);
        }

        if (Click(negativo, tipo) is { } recusa)
        {
            problema = recusa;
            return null;
        }

        return Finish(out problema);
    }

    /// <summary>A string montada, ou null e o porquê (menos de dois módulos).</summary>
    public StringRoute? Finish(out string? problema)
    {
        problema = null;
        if (_celulas.Count < 2)
        {
            problema = Tr.T("a string precisa de dois módulos ou mais");
            return null;
        }

        return new StringRoute(_celulas, _trechos);
    }

    /// <summary>A string em texto curto para a tela: "14 módulo(s), + mesa 1 col 1 fil 1".</summary>
    public static string Describe(StringRoute s)
    {
        var texto = new StringBuilder();
        texto.Append(Tr.F("{0} módulo(s): + em {1}, − em {2}", s.ModuleCount, Celula(s.Positive), Celula(s.Negative)));
        if (s.Segments.Any(t => t.Kind == RoutingKind.Leapfrog)) texto.Append(Tr.T(" (leapfrog)"));
        return texto.ToString();
    }

    private static string Celula(RoutingCell c) => Tr.F("mesa {0} col. {1} fil. {2}", c.Table + 1, c.Column + 1, c.Row + 1);
}
