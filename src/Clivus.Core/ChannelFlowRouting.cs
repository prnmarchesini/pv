namespace Clivus.Core;

/// <summary>Por onde a água sai da canaleta: uma ponta, ou uma barriga onde ela transborda.</summary>
public enum ChannelOutletKind
{
    /// <summary>Uma das pontas da canaleta.</summary>
    End,

    /// <summary>Uma barriga do fundo mais funda que a parede: a água junta ali e passa por cima da parede.</summary>
    Overflow,
}

/// <summary>Uma saída da água da canaleta.</summary>
/// <param name="Station">A estaca, da montante, em m.</param>
/// <param name="Area">A área cuja água sai por ali, em m².</param>
/// <param name="Depth">Na barriga: quanto o fundo fica abaixo da borda mais baixa dela, em m; na ponta, 0.</param>
public sealed record ChannelOutlet(double Station, double Area, ChannelOutletKind Kind, double Depth);

/// <summary>
/// A água dentro da canaleta (pedido do Renan de 21/10/2026: "preciso do
/// fluxo real, na condição da canaleta"). As bacias dizem em que estaca a
/// água de cada pedaço do terreno entra na canaleta; aqui ela anda pelo fundo
/// como ele está, morro abaixo:
/// <list type="bullet">
/// <item>numa barriga, a água dos dois lados desce para o ponto mais baixo;</item>
/// <item>uma barriga rasa (até a altura da parede) enche e a água segue por cima
/// dela, para o lado da borda mais baixa;</item>
/// <item>uma barriga mais funda que a parede não enche sem transbordar: a água
/// junta ali e sai por cima da parede (saída do tipo <see cref="ChannelOutletKind.Overflow"/>);</item>
/// <item>chegando numa ponta, sai.</item>
/// </list>
/// A área em cada estaca é a de toda a água que passa por ela; a vazão vem
/// dela pelo método racional, como a da bacia.
/// </summary>
public sealed class ChannelFlowRouting
{
    private readonly double[] _area;
    private readonly int[] _sentido;

    /// <summary>O passo entre as estacas da conta, em m.</summary>
    public double Step { get; }

    public double Length { get; }

    /// <summary>Por onde a água sai, com a área de cada saída (só as que recebem água).</summary>
    public IReadOnlyList<ChannelOutlet> Outlets { get; }

    /// <summary>A estaca com mais água e a área que passa por ela.</summary>
    public (double Station, double Area) Peak { get; }

    private ChannelFlowRouting(double step, double length, double[] area, int[] sentido, IReadOnlyList<ChannelOutlet> saidas)
    {
        Step = step;
        Length = length;
        _area = area;
        _sentido = sentido;
        Outlets = saidas;

        var k = 0;
        for (var i = 1; i < area.Length; i++)
            if (area[i] > area[k] + 1e-9) k = i;
        Peak = (Estaca(k), area[k]);
    }

    private double Estaca(int i) => Math.Min(i * Step, Length);

    private int No(double station) => Math.Clamp((int)Math.Round(station / Step), 0, _area.Length - 1);

    /// <summary>A área cuja água passa pela estaca, em m².</summary>
    public double AreaAt(double station) => _area[No(station)];

    /// <summary>Para onde a água corre na estaca: +1 para o fim da canaleta, −1 para o começo, 0 se ela sai ali.</summary>
    public int DirectionAt(double station) => _sentido[No(station)];

    /// <summary>A saída para onde vai a água que passa pela estaca.</summary>
    public ChannelOutlet? OutletFrom(double station)
    {
        var i = No(station);
        var s = _sentido[i];
        while (s != 0 && i + s >= 0 && i + s < _sentido.Length)
        {
            i += s;
            if (_sentido[i] != s) break;
        }

        var estaca = Estaca(i);
        return Outlets.MinBy(o => Math.Abs(o.Station - estaca));
    }

    /// <param name="bottom">O fundo da canaleta: (estaca desde a montante, cota), crescente.</param>
    /// <param name="wallHeight">A altura útil da seção: barriga mais funda que isso transborda.</param>
    /// <param name="accumulation">A área acumulada por estaca, como em <see cref="BasinEntry.Accumulation"/>.</param>
    public static ChannelFlowRouting Route(IReadOnlyList<(double Distance, double Bottom)> bottom, double wallHeight, IReadOnlyList<double[]> accumulation, double step = 1)
    {
        ArgumentNullException.ThrowIfNull(bottom);
        ArgumentNullException.ThrowIfNull(accumulation);
        if (bottom.Count < 2) throw new ArgumentException(Tr.T("A canaleta precisa de pelo menos dois pontos de fundo."));
        if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step));

        var comprimento = bottom[^1].Distance;
        var n = Math.Max(1, (int)Math.Ceiling(comprimento / step - 1e-9));

        // O fundo em cada estaca.
        var b = new double[n + 1];
        var k = 0;
        for (var i = 0; i <= n; i++)
        {
            var e = Math.Min(i * step, comprimento);
            while (k < bottom.Count - 2 && bottom[k + 1].Distance < e) k++;
            var (d0, z0) = bottom[k];
            var (d1, z1) = bottom[k + 1];
            b[i] = d1 > d0 ? z0 + (z1 - z0) * Math.Clamp((e - d0) / (d1 - d0), 0, 1) : z0;
        }

        // A área que entra em cada estaca.
        var chega = new double[n + 1];
        var antes = 0.0;
        foreach (var par in accumulation)
        {
            chega[Math.Clamp((int)Math.Round(par[0] / step), 0, n)] += par[1] - antes;
            antes = par[1];
        }

        // As saídas: as pontas e, uma a uma, as barrigas mais fundas que a parede.
        var saidas = new SortedSet<int> { 0, n };
        double[] maxEsq, maxDir;
        var profundidade = new Dictionary<int, double>();

        while (true)
        {
            (maxEsq, maxDir) = Bordas(b, saidas);

            var nova = false;
            var i = 1;
            while (i < n)
            {
                if (saidas.Contains(i) || Nivel(b, maxEsq, maxDir, i) - b[i] <= 1e-6)
                {
                    i++;
                    continue;
                }

                // Uma barriga: do i até onde o nível volta ao fundo.
                var nivel = Nivel(b, maxEsq, maxDir, i);
                var fundo = i;
                while (i < n && !saidas.Contains(i) && Nivel(b, maxEsq, maxDir, i) - b[i] > 1e-6)
                {
                    if (b[i] < b[fundo]) fundo = i;
                    i++;
                }

                if (nivel - b[fundo] > wallHeight + 1e-9)
                {
                    saidas.Add(fundo);
                    profundidade[fundo] = nivel - b[fundo];
                    nova = true;
                }

            }

            if (!nova) break;
        }

        // O destino de cada estaca: a saída do lado da borda mais baixa.
        var destino = new int[n + 1];
        var lista = saidas.ToList();
        for (var i = 0; i <= n; i++)
        {
            if (saidas.Contains(i))
            {
                destino[i] = i;
                continue;
            }

            var esq = lista.Last(s => s < i);
            var dir = lista.First(s => s > i);

            if (maxEsq[i] < maxDir[i] - 1e-9) destino[i] = esq;
            else if (maxDir[i] < maxEsq[i] - 1e-9) destino[i] = dir;
            else destino[i] = b[i - 1] < b[i + 1] - 1e-9 ? esq : dir;
        }

        // Ponta mais alta que a vizinha não é saída: a água dela desce pela canaleta.
        if (b[1] < b[0] - 1e-9 && !saidas.Contains(1)) destino[0] = destino[1];
        else if (b[1] < b[0] - 1e-9) destino[0] = 1;
        if (b[n - 1] < b[n] - 1e-9 && !saidas.Contains(n - 1)) destino[n] = destino[n - 1];
        else if (b[n - 1] < b[n] - 1e-9) destino[n] = n - 1;

        // A água de cada estaca passa por todas entre ela e o destino.
        var delta = new double[n + 2];
        for (var j = 0; j <= n; j++)
        {
            if (chega[j] == 0) continue;
            var (a, z) = (Math.Min(j, destino[j]), Math.Max(j, destino[j]));
            delta[a] += chega[j];
            delta[z + 1] -= chega[j];
        }

        var area = new double[n + 1];
        var soma = 0.0;
        for (var i = 0; i <= n; i++) area[i] = soma += delta[i];

        var sentido = new int[n + 1];
        for (var i = 0; i <= n; i++) sentido[i] = Math.Sign(destino[i] - i);

        var resultado = lista
            .Select(s => (s, Area: Enumerable.Range(0, n + 1).Where(j => destino[j] == s).Sum(j => chega[j])))
            .Where(x => x.Area > 1e-9)
            .Select(x => new ChannelOutlet(Math.Min(x.s * step, comprimento), x.Area,
                profundidade.ContainsKey(x.s) ? ChannelOutletKind.Overflow : ChannelOutletKind.End,
                profundidade.GetValueOrDefault(x.s)))
            .ToList();

        return new ChannelFlowRouting(step, comprimento, area, sentido, resultado);
    }

    /// <summary>A cota mais alta do fundo entre cada estaca e a saída mais perto de cada lado.</summary>
    private static (double[] Esq, double[] Dir) Bordas(double[] b, SortedSet<int> saidas)
    {
        var n = b.Length - 1;
        var esq = new double[n + 1];
        var dir = new double[n + 1];

        for (var i = 0; i <= n; i++)
            esq[i] = saidas.Contains(i) || i == 0 ? b[i] : Math.Max(esq[i - 1], b[i]);
        for (var i = n; i >= 0; i--)
            dir[i] = saidas.Contains(i) || i == n ? b[i] : Math.Max(dir[i + 1], b[i]);

        return (esq, dir);
    }

    /// <summary>Até onde a água sobe na estaca antes de escorrer para uma saída.</summary>
    private static double Nivel(double[] b, double[] esq, double[] dir, int i) => Math.Max(b[i], Math.Min(esq[i], dir[i]));
}
