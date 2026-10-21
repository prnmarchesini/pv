namespace Clivus.Geo;

/// <summary>
/// A grade das bacias (passo 30.1): para cada célula, para qual canaleta a
/// água dela vai, por quanto caminho e de que cota a que cota.
/// </summary>
/// <param name="Owner">Por célula (linha a linha, de baixo para cima): o índice da canaleta, <see cref="Watershed.NoDestination"/> ou <see cref="Watershed.NotAnalyzed"/>.</param>
/// <param name="Reason">Por célula sem destino: por que a água não chega a canaleta nenhuma.</param>
/// <param name="Length">Por célula: o comprimento em planta do caminho até a canaleta (NaN se não se sabe).</param>
/// <param name="StartZ">Por célula: a cota do terreno no centro.</param>
/// <param name="EndZ">Por célula: a cota onde a água chega à canaleta.</param>
/// <param name="Manual">Por célula: se o dono veio de um ajuste manual (30.3).</param>
public sealed record WatershedGrid(
    double X0, double Y0, double Cell, int Columns, int Rows,
    int[] Owner, FlowStop[] Reason, double[] Length, double[] StartZ, double[] EndZ, bool[] Manual)
{
    public int Index(int i, int j) => j * Columns + i;

    public (double X, double Y) Center(int i, int j) => (X0 + (i + 0.5) * Cell, Y0 + (j + 0.5) * Cell);

    /// <summary>A célula que contém (x, y), ou −1 fora da grade.</summary>
    public int CellAt(double x, double y)
    {
        var i = (int)Math.Floor((x - X0) / Cell);
        var j = (int)Math.Floor((y - Y0) / Cell);
        return i < 0 || j < 0 || i >= Columns || j >= Rows ? -1 : Index(i, j);
    }

    /// <summary>Quantas células foram analisadas (dentro da região e do terreno).</summary>
    public int AnalyzedCount => Owner.Count(o => o != Watershed.NotAnalyzed);
}

/// <summary>O resumo de uma bacia: o que o memorial pede (Tabela 1).</summary>
/// <param name="Target">O índice da canaleta.</param>
/// <param name="Area">Área em planta, em m².</param>
/// <param name="LongestPath">O maior caminho da água até a canaleta (L), em m.</param>
/// <param name="Drop">O desnível desse caminho (ΔH), em m.</param>
/// <param name="LongestStart">De onde sai esse caminho.</param>
/// <param name="HasManual">Se alguma parte veio de ajuste manual.</param>
public sealed record BasinSummary(int Target, int Cells, double Area, double LongestPath, double Drop, (double X, double Y) LongestStart, bool HasManual)
{
    /// <summary>Declividade média do caminho, em %.</summary>
    public double SlopePercent => LongestPath > 0 ? Drop / LongestPath * 100 : 0;
}

/// <summary>
/// As bacias de contribuição de cada canaleta (passo 30.1). Pedido do Renan:
/// "qual pedaço de terra vai mandar água para qual canaleta?". De cada célula
/// da grade a água desce pelo terreno (<see cref="FlowTracer"/>) até tocar
/// uma canaleta, cair num fundo ou sair do terreno.
///
/// As células vão da mais baixa para a mais alta, e o caminho de uma célula
/// para assim que entra numa célula já resolvida: dali em diante o destino e
/// o resto do caminho já se sabem. Sem isso, cada célula do alto refaria o
/// caminho inteiro até a canaleta.
/// </summary>
public static class Watershed
{
    /// <summary>Célula fora da região ou do terreno.</summary>
    public const int NotAnalyzed = -1;

    /// <summary>Célula cuja água não chega a canaleta nenhuma.</summary>
    public const int NoDestination = -2;

    /// <summary>Mais células que isto e o cálculo é recusado: pede célula maior.</summary>
    public const int MaximumCells = 2_000_000;

    private const int Pendente = int.MinValue;

    private static readonly (int, int)[] Vizinhas = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <param name="region">Os polígonos da região analisada; vazio = o terreno todo.</param>
    /// <param name="manual">Os ajustes manuais (30.3): dentro do polígono, a água vai para a canaleta dada.</param>
    /// <param name="reuse">Se aproveita o caminho das células já resolvidas (desligar só serve para conferir).</param>
    public static WatershedGrid Compute(
        Tin tin,
        IReadOnlyList<IReadOnlyList<Point3>> region,
        FlowTargets targets,
        double cell,
        IReadOnlyList<(IReadOnlyList<Point3> Polygon, int Target)>? manual = null,
        FlowOptions? options = null,
        bool reuse = true,
        Func<double, bool>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(targets);

        if (!(cell > 0) || !double.IsFinite(cell)) throw new ArgumentOutOfRangeException(nameof(cell));

        options ??= new FlowOptions { Step = Math.Min(0.5, cell / 4) };
        manual ??= [];

        double minX, minY, maxX, maxY;
        if (region.Count > 0)
        {
            var todos = region.SelectMany(r => r).ToList();
            (minX, minY, maxX, maxY) = (todos.Min(p => p.X), todos.Min(p => p.Y), todos.Max(p => p.X), todos.Max(p => p.Y));
        }
        else
        {
            if (tin.TriangleCount == 0) throw new ArgumentException("terreno vazio");
            (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
            foreach (var t in tin.Triangles)
            {
                minX = Math.Min(minX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)));
                minY = Math.Min(minY, Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)));
                maxX = Math.Max(maxX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)));
                maxY = Math.Max(maxY, Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)));
            }
        }

        // A grade alinhada em múltiplos da célula: calcular de novo dá as mesmas células.
        var x0 = Math.Floor(minX / cell) * cell;
        var y0 = Math.Floor(minY / cell) * cell;
        var colunas = (int)Math.Ceiling((maxX - x0) / cell);
        var linhas = (int)Math.Ceiling((maxY - y0) / cell);

        if ((long)colunas * linhas > MaximumCells)
            throw new ArgumentException($"{(long)colunas * linhas} células; aumente a célula");

        var n = colunas * linhas;
        var dono = new int[n];
        var motivo = new FlowStop[n];
        var comprimento = new double[n];
        var zInicio = new double[n];
        var zFim = new double[n];
        var manualFlag = new bool[n];

        var grade = new WatershedGrid(x0, y0, cell, colunas, linhas, dono, motivo, comprimento, zInicio, zFim, manualFlag);

        // As células analisadas, da mais baixa para a mais alta.
        var fila = new List<(int Indice, double Z)>();

        for (var j = 0; j < linhas; j++)
        for (var i = 0; i < colunas; i++)
        {
            var k = grade.Index(i, j);
            var (x, y) = grade.Center(i, j);

            dono[k] = NotAnalyzed;
            comprimento[k] = double.NaN;
            zFim[k] = double.NaN;

            if (region.Count > 0 && !region.Any(r => Polygons.Contains(r, x, y))) continue;
            if (!tin.TryGetZ(x, y, out var z)) continue;

            dono[k] = Pendente;
            zInicio[k] = z;
            fila.Add((k, z));
        }

        fila.Sort((a, b) => a.Z.CompareTo(b.Z));

        for (var f = 0; f < fila.Count; f++)
        {
            if (progress is not null && f % 500 == 0 && !progress((double)f / fila.Count))
                throw new OperationCanceledException();

            var k = fila[f].Indice;
            var (x, y) = grade.Center(k % colunas, k / colunas);

            // Célula de ajuste manual não serve de atalho: o dono dela foi
            // escolhido à mão, não pela água, e não passa para quem entra nela.
            //
            // E só a célula do miolo da bacia: na divisa entre duas bacias, o
            // centro de uma célula pode ir para um lado e a borda por onde a
            // água entrou, para o outro. Ali o caminho segue até o fim.
            bool Resolvida(double px, double py)
            {
                var c = grade.CellAt(px, py);
                if (c < 0 || c == k || dono[c] == Pendente || dono[c] == NotAnalyzed || manualFlag[c]) return false;

                var (ci, cj) = (c % colunas, c / colunas);
                foreach (var (di, dj) in Vizinhas)
                {
                    var (ni, nj) = (ci + di, cj + dj);
                    if (ni < 0 || nj < 0 || ni >= colunas || nj >= linhas) continue;

                    var v = grade.Index(ni, nj);
                    if (dono[v] == Pendente || dono[v] == NotAnalyzed || manualFlag[v]) continue;
                    if (dono[v] != dono[c]) return false;
                }

                return true;
            }

            var caminho = FlowTracer.Trace(tin, x, y, targets, options, reuse ? Resolvida : null);

            switch (caminho.Stop)
            {
                case FlowStop.Target:
                    dono[k] = caminho.TargetIndex;
                    comprimento[k] = caminho.Length2D;
                    zFim[k] = caminho.Points[^1].Z;
                    break;

                case FlowStop.Joined:
                    var entrada = caminho.Points[^1];
                    var c = grade.CellAt(entrada.X, entrada.Y);
                    dono[k] = dono[c];
                    motivo[k] = motivo[c];
                    zFim[k] = zFim[c];

                    // O comprimento da célula de lá conta do centro dela; daqui
                    // a água entrou pela borda. Falta o trecho da entrada até o
                    // centro, medido na direção em que a água corre (sem ele, o L
                    // perdia meia célula a cada junção e saía pela metade).
                    var (cx, cy) = grade.Center(c % colunas, c / colunas);
                    var falta = tin.TryGetSlope(entrada.X, entrada.Y, out var declive)
                        ? (cx - entrada.X) * declive.DownhillX + (cy - entrada.Y) * declive.DownhillY
                        : 0;
                    comprimento[k] = caminho.Length2D + falta + comprimento[c];
                    break;

                default:
                    dono[k] = NoDestination;
                    motivo[k] = caminho.Stop;
                    break;
            }

            // O ajuste manual manda no dono; o caminho só vale se foi para a mesma canaleta (o refino abaixo respeita isso).
            foreach (var (poligono, alvo) in manual)
            {
                if (!Polygons.Contains(poligono, x, y)) continue;

                if (dono[k] != alvo)
                {
                    comprimento[k] = double.NaN;
                    zFim[k] = double.NaN;
                }

                dono[k] = alvo;
                manualFlag[k] = true;
                break;
            }
        }

        if (reuse) RefinarOMaisLongo(tin, targets, options, grade);

        return grade;
    }

    /// <summary>Quantas candidatas a caminho mais longo, por bacia, são refeitas sem atalho.</summary>
    private const int Candidatas = 40;

    /// <summary>
    /// O L de cada bacia sai do caminho mais longo, e o atalho das células
    /// resolvidas acumula um erro pequeno a cada junção (num talvegue comprido,
    /// alguns por cento). As células com o maior L estimado, e as mais altas,
    /// têm o caminho refeito inteiro, sem atalho: o maior deles é o L exato.
    /// </summary>
    private static void RefinarOMaisLongo(Tin tin, FlowTargets targets, FlowOptions options, WatershedGrid grade)
    {
        var porBacia = new Dictionary<int, List<int>>();

        for (var k = 0; k < grade.Owner.Length; k++)
        {
            if (grade.Owner[k] < 0 || grade.Manual[k]) continue;
            if (!porBacia.TryGetValue(grade.Owner[k], out var lista)) porBacia[grade.Owner[k]] = lista = [];
            lista.Add(k);
        }

        foreach (var (alvo, celulas) in porBacia)
        {
            var escolhidas = celulas.Where(k => double.IsFinite(grade.Length[k])).OrderByDescending(k => grade.Length[k]).Take(Candidatas)
                .Concat(celulas.OrderByDescending(k => grade.StartZ[k]).Take(Candidatas / 2))
                .Distinct();

            foreach (var k in escolhidas)
            {
                var (x, y) = grade.Center(k % grade.Columns, k / grade.Columns);
                var caminho = FlowTracer.Trace(tin, x, y, targets, options);

                if (caminho.Stop == FlowStop.Target && caminho.TargetIndex == alvo)
                {
                    grade.Length[k] = caminho.Length2D;
                    grade.EndZ[k] = caminho.Points[^1].Z;
                }
            }
        }
    }

    /// <summary>O resumo de cada bacia (só as canaletas que receberam alguma célula).</summary>
    public static IReadOnlyList<BasinSummary> Summaries(WatershedGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var area = grid.Cell * grid.Cell;
        var resumos = new Dictionary<int, (int Celulas, double Maior, double Queda, (double, double) Inicio, bool Manual)>();

        for (var k = 0; k < grid.Owner.Length; k++)
        {
            var dono = grid.Owner[k];
            if (dono < 0) continue;

            resumos.TryGetValue(dono, out var r);
            r.Celulas++;
            r.Manual |= grid.Manual[k];

            if (double.IsFinite(grid.Length[k]) && grid.Length[k] > r.Maior)
            {
                r.Maior = grid.Length[k];
                r.Queda = grid.StartZ[k] - grid.EndZ[k];
                r.Inicio = grid.Center(k % grid.Columns, k / grid.Columns);
            }

            resumos[dono] = r;
        }

        return resumos
            .OrderBy(p => p.Key)
            .Select(p => new BasinSummary(p.Key, p.Value.Celulas, p.Value.Celulas * area, p.Value.Maior, p.Value.Queda, p.Value.Inicio, p.Value.Manual))
            .ToList();
    }
}
