using Clipper2Lib;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// A união de muitos polígonos, juntada aos poucos (05/10/2026): numa usina
/// de milhares de mesas, um ano de passos dá milhões de contornos; guardar
/// todos para unir no fim estoura a memória. Os polígonos esperam numa fila
/// e, quando ela enche, são unidos com o que já foi juntado. No fim, a mesma
/// borda que <see cref="ShadowUnion.Union"/> daria.
/// </summary>
public sealed class ShadowAccumulator
{
    private const double Escala = 1000;

    private readonly int _maximoNaFila;
    private Paths64 _juntado = [];
    private readonly Paths64 _fila = [];
    private int _pontosNaFila;

    /// <param name="maxPending">Quantos polígonos esperam antes de uma união parcial.</param>
    public ShadowAccumulator(int maxPending = 2000)
    {
        _maximoNaFila = Math.Max(1, maxPending);
    }

    /// <summary>Quantos polígonos com área já entraram.</summary>
    public int Count { get; private set; }

    /// <summary>Junta um polígono (em planta). Menos de 3 pontos é ignorado.</summary>
    public void Add(IReadOnlyList<(double X, double Y)> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Count < 3) return;

        var caminho = new Path64(polygon.Count);
        foreach (var (x, y) in polygon) caminho.Add(new Point64(Math.Round(x * Escala), Math.Round(y * Escala)));

        // Todos no mesmo sentido: um horário e um anti-horário sobrepostos
        // se anulariam na regra NonZero e abririam um buraco falso.
        var area = Clipper.Area(caminho);
        if (Math.Abs(area) <= 1) return;
        if (area < 0) caminho.Reverse();

        _fila.Add(caminho);
        _pontosNaFila += caminho.Count;
        Count++;

        if (_fila.Count >= _maximoNaFila || _pontosNaFila >= 400_000) Juntar();
    }

    /// <summary>Junta um polígono 3D pela planta.</summary>
    public void Add(IReadOnlyList<Point3> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        Add(polygon.Select(p => (p.X, p.Y)).ToList());
    }

    /// <summary>As bordas da união (o mesmo formato de <see cref="ShadowUnion.Union"/>).</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Rings()
    {
        Juntar();
        if (_juntado.Count == 0) return [];

        // Tira os vértices alinhados sem mexer nos dentes: 2 mm, só no fim.
        return Clipper.SimplifyPaths(_juntado, 2)
            .Where(c => c.Count >= 3 && Math.Abs(Clipper.Area(c)) > 1)
            .Select(c => (IReadOnlyList<(double X, double Y)>)c.Select(p => (p.X / Escala, p.Y / Escala)).ToList())
            .ToList();
    }

    private void Juntar()
    {
        if (_fila.Count == 0) return;

        // O já juntado guarda o sentido da união (buracos ao contrário):
        // entra como está, para os buracos continuarem buracos.
        var tudo = new Paths64(_juntado.Count + _fila.Count);
        tudo.AddRange(_juntado);
        tudo.AddRange(_fila);
        _juntado = Clipper.Union(tudo, FillRule.NonZero);

        _fila.Clear();
        _pontosNaFila = 0;
    }
}

/// <summary>
/// A sombra de TODOS os objetos do desenho, desenhável (05/10/2026, Renan:
/// "ao gerar sombras, quero que gere as sombras das mesas também, o motor de
/// sombras tem que gerar as sombras de todos objetos do desenho"): a cada
/// passo do cálculo, as árvores (cilindros) e as mesas (os contornos) fazem
/// sombra no chão e nas outras mesas; tudo é unido, passo a passo, numa
/// mancha no chão e numa mancha por mesa. O mesmo conjunto que o
/// <see cref="ShadingModel"/> usa para marcar os módulos (o relevo não tem
/// contorno: ele só tira o sol do módulo).
/// </summary>
public sealed class ShadowFootprint
{
    private readonly IReadOnlyList<ShadowCylinder> _cilindros;
    private readonly IReadOnlyList<IReadOnlyList<Point3>> _mesas;
    private readonly IReadOnlyList<IReadOnlyList<Point3>> _densas;
    private readonly Func<double, double, double?> _chao;
    private readonly bool _mesasFazemSombra;
    private readonly (double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ)[] _caixas;
    private readonly double _maisBaixa;

    private readonly double _celula;
    private readonly Dictionary<long, List<int>> _grade = [];
    private readonly int[] _carimbo;
    private int _rodada;

    private readonly ShadowAccumulator _noChao = new(20_000);
    private readonly Dictionary<int, ShadowAccumulator> _naMesa = [];

    /// <param name="cylinders">Os cilindros dos objetos (árvores).</param>
    /// <param name="tables">Os contornos das mesas (planos e convexos, em ordem): recebem sombra e, com <paramref name="tablesCastShadow"/>, fazem.</param>
    /// <param name="ground">A cota do terreno em (x, y), ou null fora dele.</param>
    /// <param name="tablesCastShadow">Se as mesas fazem sombra (no chão e nas outras).</param>
    /// <param name="edgeStep">Os lados da mesa ganham um ponto a cada tantos metros antes de irem ao chão (a sombra acompanha o relevo).</param>
    public ShadowFootprint(
        IReadOnlyList<ShadowCylinder> cylinders, IReadOnlyList<IReadOnlyList<Point3>> tables, Func<double, double, double?> ground,
        bool tablesCastShadow = true, double edgeStep = 5)
    {
        ArgumentNullException.ThrowIfNull(cylinders);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(ground);
        if (!(edgeStep > 0)) throw new ArgumentOutOfRangeException(nameof(edgeStep));

        _cilindros = cylinders;
        _mesas = tables;
        _chao = ground;
        _mesasFazemSombra = tablesCastShadow;
        _carimbo = new int[tables.Count];

        _caixas = tables.Select(t => t.Count == 0
            ? (0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
            : (t.Min(p => p.X), t.Max(p => p.X), t.Min(p => p.Y), t.Max(p => p.Y), t.Min(p => p.Z), t.Max(p => p.Z))).ToArray();
        _maisBaixa = tables.Count == 0 ? 0 : _caixas.Min(c => c.MinZ);

        _densas = tables.Select(t => Densificar(t, edgeStep)).ToList();

        // A grade das mesas, em planta: a célula perto do tamanho de uma mesa.
        _celula = tables.Count == 0 ? 10 : Math.Clamp(_caixas.Average(c => Math.Max(c.MaxX - c.MinX, c.MaxY - c.MinY)), 5, 50);

        for (var k = 0; k < tables.Count; k++)
        {
            if (tables[k].Count < 3) continue;
            var c = _caixas[k];

            for (var ix = Indice(c.MinX); ix <= Indice(c.MaxX); ix++)
            {
                for (var iy = Indice(c.MinY); iy <= Indice(c.MaxY); iy++)
                {
                    var chave = Chave(ix, iy);
                    if (!_grade.TryGetValue(chave, out var lista)) _grade[chave] = lista = [];
                    lista.Add(k);
                }
            }
        }
    }

    /// <summary>Quantos passos com sol entraram.</summary>
    public int Steps { get; private set; }

    /// <summary>Junta a sombra de todos os objetos com o sol nesta posição (abaixo do mínimo, nada).</summary>
    public void Add(SunPosition sun)
    {
        ArgumentNullException.ThrowIfNull(sun);
        if (sun.ElevationDegrees < Shading.MinimumElevationDegrees) return;

        var s = sun.Direction;
        Steps++;

        foreach (var c in _cilindros)
        {
            var contorno = Shading.ShadowOutline(c, s, _chao);
            if (contorno.Count >= 3) _noChao.Add(contorno);

            // A caixa da sombra até a mesa mais baixa: só as mesas dentro dela.
            var t = Math.Max((c.Top - _maisBaixa) / s.Z, 0);
            var (px, py) = (c.X - s.X * t, c.Y - s.Y * t);
            var caixa = (Math.Min(c.X, px) - c.Radius, Math.Max(c.X, px) + c.Radius, Math.Min(c.Y, py) - c.Radius, Math.Max(c.Y, py) + c.Radius);

            foreach (var m in MesasNa(caixa))
            {
                if (_caixas[m].MinZ >= c.Top) continue;
                var naMesa = Shading.ShadowOnPlane(c, s, _mesas[m]);
                if (naMesa.Count >= 3) Mancha(m).Add(naMesa);
            }
        }

        if (!_mesasFazemSombra) return;

        for (var i = 0; i < _mesas.Count; i++)
        {
            if (_mesas[i].Count < 3) continue;

            var noChao = Shading.PolygonShadowOnGround(_densas[i], s, _chao);
            if (noChao.Count >= 3) _noChao.Add(noChao);

            // A caixa da mesa e da sombra dela até a mesa mais baixa.
            var c = _caixas[i];
            var t = Math.Max((c.MaxZ - _maisBaixa) / s.Z, 0);
            var (dx, dy) = (-s.X * t, -s.Y * t);
            var caixa = (Math.Min(c.MinX, c.MinX + dx), Math.Max(c.MaxX, c.MaxX + dx), Math.Min(c.MinY, c.MinY + dy), Math.Max(c.MaxY, c.MaxY + dy));

            foreach (var m in MesasNa(caixa))
            {
                // A mesa não faz sombra nela mesma, nem numa que está toda acima dela.
                if (m == i || _caixas[m].MinZ >= c.MaxZ) continue;
                var naMesa = Shading.PolygonShadowOnPlane(_mesas[i], s, _mesas[m]);
                if (naMesa.Count >= 3) Mancha(m).Add(naMesa);
            }
        }
    }

    /// <summary>As bordas da mancha no chão (em planta; a cota vem do terreno ao desenhar).</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Ground() => _noChao.Rings();

    /// <summary>As mesas que pegaram alguma sombra (índices na lista do construtor).</summary>
    public IEnumerable<int> ShadedTables => _naMesa.Keys.OrderBy(k => k);

    /// <summary>As bordas da mancha sobre a mesa (em planta; a cota vem do plano dela).</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> OnTable(int table) =>
        _naMesa.TryGetValue(table, out var a) ? a.Rings() : [];

    private ShadowAccumulator Mancha(int mesa) =>
        _naMesa.TryGetValue(mesa, out var a) ? a : _naMesa[mesa] = new ShadowAccumulator(256);

    /// <summary>As mesas cuja caixa toca a caixa dada (cada uma uma vez).</summary>
    private List<int> MesasNa((double MinX, double MaxX, double MinY, double MaxY) caixa)
    {
        var achadas = new List<int>();
        if (_mesas.Count == 0) return achadas;

        _rodada++;
        if (_rodada == int.MaxValue)
        {
            Array.Clear(_carimbo);
            _rodada = 1;
        }

        bool Toca(int k) =>
            _caixas[k].MaxX >= caixa.MinX && _caixas[k].MinX <= caixa.MaxX && _caixas[k].MaxY >= caixa.MinY && _caixas[k].MinY <= caixa.MaxY;

        var (x0, x1, y0, y1) = (Indice(caixa.MinX), Indice(caixa.MaxX), Indice(caixa.MinY), Indice(caixa.MaxY));

        // Caixa enorme (sol baixo): mais barato olhar mesa a mesa.
        if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > _mesas.Count)
        {
            for (var k = 0; k < _mesas.Count; k++)
                if (_mesas[k].Count >= 3 && Toca(k)) achadas.Add(k);
            return achadas;
        }

        for (var ix = x0; ix <= x1; ix++)
        {
            for (var iy = y0; iy <= y1; iy++)
            {
                if (!_grade.TryGetValue(Chave(ix, iy), out var lista)) continue;

                foreach (var k in lista)
                {
                    if (_carimbo[k] == _rodada) continue;
                    _carimbo[k] = _rodada;
                    if (Toca(k)) achadas.Add(k);
                }
            }
        }

        return achadas;
    }

    /// <summary>O contorno com um ponto a cada <paramref name="passo"/> metros (em 3D), no máximo.</summary>
    private static IReadOnlyList<Point3> Densificar(IReadOnlyList<Point3> contorno, double passo)
    {
        var saida = new List<Point3>();

        for (var i = 0; i < contorno.Count; i++)
        {
            var (a, b) = (contorno[i], contorno[(i + 1) % contorno.Count]);
            var comprimento = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            var partes = Math.Max(1, (int)Math.Ceiling(comprimento / passo));

            for (var k = 0; k < partes; k++)
            {
                var t = (double)k / partes;
                saida.Add(new Point3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t));
            }
        }

        return saida;
    }

    private int Indice(double v) => (int)Math.Floor(v / _celula);

    private static long Chave(int ix, int iy) => ((long)ix << 32) ^ (uint)iy;
}
