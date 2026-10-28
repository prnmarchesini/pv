namespace Clivus.Core;

/// <summary>Uma fila de carport montada: os pórticos, os vãos e os avisos.</summary>
/// <param name="Portals">Os pórticos, do começo ao fim (vãos + 1).</param>
/// <param name="Bays">Os vãos, do começo ao fim.</param>
/// <param name="Warnings">O que o projetista precisa saber, mas não impede a fila.</param>
public sealed record CarportRowResult(
    IReadOnlyList<CarportPortal> Portals,
    IReadOnlyList<CarportBay> Bays,
    IReadOnlyList<string> Warnings)
{
    /// <summary>O comprimento da fila em planta, do primeiro ao último pórtico.</summary>
    public double Length => Bays.Count == 0 ? 0 : Math.Sqrt(
        Math.Pow(Portals[^1].Center.X - Portals[0].Center.X, 2) + Math.Pow(Portals[^1].Center.Y - Portals[0].Center.Y, 2));
}

/// <summary>
/// A fila contínua do carport (etapa 34.3, <c>plano/carport</c>): o eixo é a
/// linha do meio da cobertura, do primeiro clique para o segundo; os
/// pórticos ficam nas divisas dos vãos (N vãos, N + 1 pórticos) e cada um
/// assenta no chão das suas bases. Entre dois pórticos com desnível, o vão
/// acompanha (<see cref="CarportGeometry.Bay"/>); desnível acima do limite vira
/// aviso, não recusa: é o projetista quem decide se o estacionamento aceita.
/// </summary>
public static class CarportRow
{
    /// <summary>O desnível ao longo da fila, entre dois pórticos vizinhos, acima do qual a fila avisa (5 %).</summary>
    public const double MaiorDesnivelPadrao = 0.05;

    /// <summary>O maior número de vãos numa fila: rede para clique errado (um vão de 5,67 m dá mais de 1 km).</summary>
    public const int MaiorNumeroDeVaos = 200;

    /// <summary>Monta a fila.</summary>
    /// <param name="perfil">O carport.</param>
    /// <param name="deX">Leste do começo do eixo (o primeiro pórtico).</param>
    /// <param name="deY">Norte do começo do eixo.</param>
    /// <param name="ateX">Leste do fim do eixo, que dá o sentido e, sem <paramref name="vaos"/>, o comprimento.</param>
    /// <param name="ateY">Norte do fim do eixo.</param>
    /// <param name="caiADireita">A água cai para a direita de quem anda do começo para o fim.</param>
    /// <param name="vaos">Quantos vãos; null enche o eixo com quantos couberem.</param>
    /// <param name="chao">A cota do chão em (x, y), ou null onde não há terreno.</param>
    /// <param name="motivo">Por que a fila não sai, ou null.</param>
    /// <param name="maiorDesnivel">O desnível ao longo da fila que vira aviso (fração: 0,05 = 5 %).</param>
    public static CarportRowResult? Build(
        CarportProfile perfil, double deX, double deY, double ateX, double ateY, bool caiADireita, int? vaos,
        Func<double, double, double?> chao, out string? motivo, double maiorDesnivel = MaiorDesnivelPadrao)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(chao);

        if (perfil.WhyInvalid is { } invalido)
        {
            motivo = invalido;
            return null;
        }

        var dx = ateX - deX;
        var dy = ateY - deY;
        var eixo = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(eixo) || eixo < 1e-6)
        {
            motivo = Tr.T("o eixo da fila do carport não tem comprimento");
            return null;
        }

        var vao = perfil.Array.BayWidth;
        // Folga de 1 mm: o eixo clicado com a medida exata de 3 vãos dá 3 vãos.
        var quantos = vaos ?? (int)Math.Floor((eixo + 1e-3) / vao);
        if (quantos < 1)
        {
            motivo = vaos is null
                ? Tr.F("o eixo ({0:0.##} m) é mais curto que um vão do carport ({1:0.##} m)", eixo, vao)
                : Tr.T("a fila do carport precisa de pelo menos um vão");
            return null;
        }

        if (quantos > MaiorNumeroDeVaos)
        {
            motivo = Tr.F("a fila do carport passaria de {0} vãos; confira o eixo", MaiorNumeroDeVaos);
            return null;
        }

        var ax = dx / eixo;
        var ay = dy / eixo;
        var porticos = new List<CarportPortal>(quantos + 1);
        var avisos = new List<string>();
        for (var k = 0; k <= quantos; k++)
        {
            var p = CarportGeometry.Portal(perfil, deX + ax * vao * k, deY + ay * vao * k, ax, ay, caiADireita, chao, out var porque);
            if (p is null)
            {
                motivo = Tr.F("pórtico {0} de {1}: {2}", k + 1, quantos + 1, porque);
                return null;
            }

            porticos.Add(p);

            // O pilar do lado de chão mais baixo cresce o desnível entre as bases.
            var entreBases = Math.Abs(p.HighBase.Z - p.LowBase.Z);
            if (entreBases > CarportGeometry.MaiorDesnivelEntreBases)
            {
                var maisComprido = p.Bars.Where(b => b.Part == CarportPart.Pillar).Max(b => b.Length);
                avisos.Add(Tr.F("no pórtico {0} o chão cai {1:0.##} m entre as bases: um pilar sai com {2:0.##} m",
                    k + 1, entreBases, maisComprido));
            }
        }

        var vaosMontados = new List<CarportBay>(quantos);
        for (var i = 0; i < quantos; i++)
        {
            vaosMontados.Add(CarportGeometry.Bay(perfil, i, porticos[i], porticos[i + 1]));

            var desnivel = Math.Abs(porticos[i + 1].LowEdgeZ - porticos[i].LowEdgeZ) / vao;
            // Tolerância de ponto flutuante: 5 % exatos não avisam.
            if (desnivel > maiorDesnivel + 1e-9)
            {
                avisos.Add(Tr.F("entre os pórticos {0} e {1} a cobertura sobe ou desce {2:0.#} % ao longo da fila (aviso acima de {3:0.#} %)",
                    i + 1, i + 2, desnivel * 100, maiorDesnivel * 100));
            }
        }

        motivo = null;
        return new CarportRowResult(porticos, vaosMontados, avisos);
    }
}
