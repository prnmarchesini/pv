using System.Globalization;
using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// A célula em planta reconstruída de uma mesa já desenhada (7.3): os quatro
/// cantos do contorno, na ordem em que o desenho os grava (borda baixa do
/// início ao fim, depois a borda alta de volta), viram a
/// <see cref="PlacedTable"/> que o pipeline precisa para recalcular só
/// aquela mesa, ONDE ELA ESTÁ AGORA (uma mesa movida é recalculada na
/// posição nova: a posição é vontade do usuário, a cota é do terreno).
///
/// O contorno desenhado NÃO é um retângulo em planta: com giro longitudinal
/// a projeção é um paralelogramo, e a borda baixa é mais curta que a mesa
/// (cos do giro). Por isso a célula é a NOMINAL, ancorada no canto da borda
/// baixa e apontada pela borda baixa, com o comprimento e o fundo em planta
/// do perfil; o giro o pipeline recalcula das cotas. A normal é o lado em
/// que está a borda alta: a subida. A orientação resolvida disso tem a
/// ponta baixa de cá e o comprimento correndo com a fileira, por
/// construção.
/// </summary>
public static class TableCells
{
    /// <summary>
    /// Monta a célula.
    /// </summary>
    /// <param name="corners">Os quatro cantos do contorno, com cota (ignorada): baixa-início, baixa-fim, alta-fim, alta-início.</param>
    /// <param name="label">O letreiro da mesa (F1.3), de onde saem fileira e número.</param>
    /// <param name="length">O comprimento da mesa do perfil atual, em metro.</param>
    /// <param name="planDepth">O fundo em planta do perfil atual (fundo × cos tilt), em metro.</param>
    /// <exception cref="ArgumentException">
    /// Cantos que não descrevem uma mesa (borda baixa sem comprimento,
    /// borda alta do mesmo lado), borda baixa que não fecha com o
    /// comprimento do perfil (trocou de mesa), ou letreiro ilegível.
    /// </exception>
    public static PlacedTable FromCorners(IReadOnlyList<Point3> corners, string label, double length, double planDepth)
    {
        ArgumentNullException.ThrowIfNull(corners);

        if (corners.Count != 4 || corners.Any(c => !c.IsFinite))
            throw new ArgumentException("O contorno da mesa precisa de quatro cantos finitos.", nameof(corners));

        if (!double.IsFinite(length) || length < RowDistributor.MenorMedida || !double.IsFinite(planDepth) || planDepth < RowDistributor.MenorMedida)
            throw new ArgumentOutOfRangeException(nameof(length), "O comprimento e o fundo em planta precisam ser medidas.");

        if (!TryParseLabel(label, out var fileira, out var numero))
            throw new ArgumentException($"O letreiro \"{label}\" não é F<fileira>.<mesa>.", nameof(label));

        var origem = new Point3(corners[0].X, corners[0].Y, 0);

        var dx = corners[1].X - origem.X;
        var dy = corners[1].Y - origem.Y;
        var bordaBaixa = Math.Sqrt(dx * dx + dy * dy);

        if (bordaBaixa < RowDistributor.MenorMedida)
            throw new ArgumentException("A borda baixa do contorno não tem comprimento em planta.", nameof(corners));

        // A borda baixa desenhada é o comprimento vezes o cosseno do giro:
        // nunca maior que o comprimento, e um giro de 45° já seria absurdo.
        if (bordaBaixa > length + 1e-3 || bordaBaixa < length * 0.7)
        {
            throw new ArgumentException(
                $"a borda baixa desenhada tem {bordaBaixa:0.00} m e o perfil atual tem {length:0.00} m de comprimento. Trocou de mesa?",
                nameof(corners));
        }

        var direcao = new Point3(dx / bordaBaixa, dy / bordaBaixa, 0);

        // A normal: perpendicular à borda baixa, do lado da borda alta.
        var ax = corners[3].X - origem.X;
        var ay = corners[3].Y - origem.Y;
        var lado = -direcao.Y * ax + direcao.X * ay;

        if (Math.Abs(lado) < RowDistributor.MenorMedida)
            throw new ArgumentException("A borda alta do contorno está em cima da borda baixa.", nameof(corners));

        var normal = lado > 0 ? new Point3(-direcao.Y, direcao.X, 0) : new Point3(direcao.Y, -direcao.X, 0);

        Point3[] cantos =
        [
            origem,
            new(origem.X + direcao.X * length, origem.Y + direcao.Y * length, 0),
            new(origem.X + direcao.X * length + normal.X * planDepth, origem.Y + direcao.Y * length + normal.Y * planDepth, 0),
            new(origem.X + normal.X * planDepth, origem.Y + normal.Y * planDepth, 0),
        ];

        return new PlacedTable(fileira, numero, origem, Math.Atan2(direcao.Y, direcao.X), length, planDepth, cantos, false);
    }

    /// <summary>Lê "F1.3" como fileira 1, mesa 3.</summary>
    public static bool TryParseLabel(string? label, out int row, out int number)
    {
        row = 0;
        number = 0;

        if (string.IsNullOrWhiteSpace(label)) return false;

        var texto = label.Trim();
        if (texto.Length < 4 || (texto[0] != 'F' && texto[0] != 'f')) return false;

        var partes = texto[1..].Split('.');
        if (partes.Length != 2) return false;

        return int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out row)
            && int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            && row > 0 && number > 0;
    }
}
