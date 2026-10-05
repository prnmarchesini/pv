using Clipper2Lib;

namespace Clivus.Core;

/// <summary>
/// A mancha da sombra ao longo de um período (05/10/2026, Renan: "eu quero o
/// desenho da sombra no chão ao longo do dia, na passada que o sistema
/// calcula ... apaga as linhas do meio e deixa somente as bordas ... um
/// desenho estranho, formado por dentes"): a união, em planta, dos contornos
/// de sombra de cada passo do cálculo. Só a borda fica; os dentes são os
/// passos. A união é a da biblioteca Clipper2 (Boost), em milímetros.
/// </summary>
public static class ShadowUnion
{
    private const double Escala = 1000;

    /// <summary>
    /// As bordas da união dos polígonos (em planta). Cada anel é fechado
    /// implicitamente (o último ponto não repete o primeiro); um buraco na
    /// mancha vem como anel próprio. Vazio se não há polígono com área.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Union(IEnumerable<IReadOnlyList<(double X, double Y)>> poligonos)
    {
        ArgumentNullException.ThrowIfNull(poligonos);

        var caminhos = new Paths64();
        foreach (var p in poligonos)
        {
            if (p.Count < 3) continue;
            var caminho = new Path64(p.Count);
            foreach (var (x, y) in p) caminho.Add(new Point64(Math.Round(x * Escala), Math.Round(y * Escala)));
            caminhos.Add(caminho);
        }

        if (caminhos.Count == 0) return [];

        var uniao = Clipper.Union(caminhos, FillRule.NonZero);

        // Tira os vértices alinhados (a união de polígonos de 64 lados deixa
        // muitos) sem mexer nos dentes: tolerância de 2 mm.
        uniao = Clipper.SimplifyPaths(uniao, 2);

        return uniao
            .Where(c => c.Count >= 3 && Math.Abs(Clipper.Area(c)) > 1)
            .Select(c => (IReadOnlyList<(double X, double Y)>)c.Select(p => (p.X / Escala, p.Y / Escala)).ToList())
            .ToList();
    }

    /// <summary>A área de um anel (m²), sempre positiva.</summary>
    public static double Area(IReadOnlyList<(double X, double Y)> anel)
    {
        ArgumentNullException.ThrowIfNull(anel);

        var soma = 0.0;
        for (var i = 0; i < anel.Count; i++)
        {
            var (a, b) = (anel[i], anel[(i + 1) % anel.Count]);
            soma += a.X * b.Y - b.X * a.Y;
        }

        return Math.Abs(soma) / 2;
    }

    /// <summary>
    /// O anel com pontos a cada <paramref name="passoMaximo"/> metros, no
    /// máximo, ao longo de cada lado (o fechamento incluído): no chão, cada
    /// ponto ganha a cota do terreno e a borda acompanha o relevo, sem cortar
    /// morro nem passar no ar (regra 5).
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> Densify(IReadOnlyList<(double X, double Y)> anel, double passoMaximo)
    {
        ArgumentNullException.ThrowIfNull(anel);
        if (!(passoMaximo > 0)) throw new ArgumentOutOfRangeException(nameof(passoMaximo));

        var saida = new List<(double X, double Y)>();

        for (var i = 0; i < anel.Count; i++)
        {
            var (a, b) = (anel[i], anel[(i + 1) % anel.Count]);
            var comprimento = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            var partes = Math.Max(1, (int)Math.Ceiling(comprimento / passoMaximo));

            for (var k = 0; k < partes; k++)
            {
                var t = (double)k / partes;
                saida.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
        }

        return saida;
    }
}
