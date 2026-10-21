using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// O que uma linha da drenagem é (27.1, 28.2): a identidade (GUID e nome),
/// se já virou canaleta e com qual seção e assentamento, e onde fica o
/// gráfico do perfil dela. Vai no XData da polilinha 3D; a camada é só
/// aparência (regra 7 da drenagem).
/// </summary>
public sealed record DrainageLineIdentity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    /// <summary>A seção, quando a linha virou canaleta; null = linha de análise.</summary>
    public ChannelSection? Section { get; init; }

    public ChannelBottomMode Mode { get; init; } = ChannelBottomMode.FollowTerrain;

    /// <summary>O canto de baixo à esquerda da moldura do gráfico, em planta; null = ao lado da linha.</summary>
    public (double X, double Y)? Chart { get; init; }

    public bool IsChannel => Section is not null;

    /// <summary>O tipo do XData.</summary>
    public const string XDataType = "DrnLinha";

    public const int XDataVersion = 1;

    /// <summary>Quantos campos o XData tem.</summary>
    public const int FieldCount = 16;

    /// <summary>Cada campo de texto do XData vai até 255 bytes; o nome é cortado bem antes.</summary>
    private const int NomeMaximo = 80;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Os campos do XData, um valor por campo (cada campo de XData tem limite
    /// de 255 bytes, e a seção inteira num JSON só passaria disso): GUID, nome,
    /// modo, X e Y do gráfico, e a seção (vazia na linha de análise): nome,
    /// tipo, b, z, y, r, e, revestimento, n e velocidade máxima.
    /// </summary>
    public string[] ToFields()
    {
        static string N(double v) => v.ToString("R", Inv);
        static string Cortar(string s) => s.Length > NomeMaximo ? s[..NomeMaximo] : s;

        var s = Section;

        return
        [
            Id.ToString("D"),
            Cortar(Name),
            Mode.ToString(),
            Chart is { } c ? N(c.X) : string.Empty,
            Chart is { } d ? N(d.Y) : string.Empty,
            s is null ? string.Empty : "1",
            s is null ? string.Empty : Cortar(s.Name),
            s?.Kind.ToString() ?? string.Empty,
            s is null ? string.Empty : N(s.Base),
            s is null ? string.Empty : N(s.SideSlope),
            s is null ? string.Empty : N(s.Height),
            s is null ? string.Empty : N(s.Radius),
            s is null ? string.Empty : N(s.Thickness),
            s is null ? string.Empty : Cortar(s.Lining),
            s is null ? string.Empty : N(s.Manning),
            s is null ? string.Empty : N(s.MaxVelocity),
        ];
    }

    /// <summary>A identidade dos campos, ou null se não são de uma linha da drenagem.</summary>
    public static DrainageLineIdentity? FromFields(IReadOnlyList<string>? fields)
    {
        if (fields is null || fields.Count < FieldCount || !Guid.TryParse(fields[0], out var id)) return null;

        static bool Ler(string s, out double v) => double.TryParse(s, NumberStyles.Float, Inv, out v);

        var modo = Enum.TryParse<ChannelBottomMode>(fields[2], out var m) ? m : ChannelBottomMode.FollowTerrain;
        (double, double)? grafico = Ler(fields[3], out var x) && Ler(fields[4], out var y) ? (x, y) : null;

        ChannelSection? secao = null;

        if (fields[5] == "1")
        {
            if (!Enum.TryParse<ChannelKind>(fields[7], out var tipo)
                || !Ler(fields[8], out var b) || !Ler(fields[9], out var z) || !Ler(fields[10], out var h)
                || !Ler(fields[11], out var r) || !Ler(fields[12], out var e)
                || !Ler(fields[14], out var n) || !Ler(fields[15], out var vmax))
                return null;

            secao = new ChannelSection
            {
                Name = fields[6], Kind = tipo, Base = b, SideSlope = z, Height = h, Radius = r, Thickness = e,
                Lining = fields[13], Manning = n, MaxVelocity = vmax,
            };
        }

        return new DrainageLineIdentity { Id = id, Name = fields[1], Section = secao, Mode = modo, Chart = grafico };
    }

    /// <summary>
    /// O próximo nome livre ("Linha 3", "Canaleta 2"): o maior número usado
    /// mais um, para apagar a 2 não fazer a próxima repetir a 3.
    /// </summary>
    public static string NextName(string prefix, IEnumerable<string> used)
    {
        var maior = 0;

        foreach (var nome in used)
        {
            if (!nome.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(nome[(prefix.Length + 1)..], NumberStyles.Integer, Inv, out var n)) maior = Math.Max(maior, n);
        }

        return $"{prefix} {maior + 1}";
    }
}
