using System.Text.Json;

namespace Clivus.Core;

/// <summary>A bacia de uma canaleta, como entra na memória (Tabela 1 do memorial).</summary>
/// <param name="Channel">O GUID da canaleta.</param>
/// <param name="ChannelName">O nome da canaleta quando as bacias foram calculadas.</param>
/// <param name="Area">Área de contribuição, em m².</param>
/// <param name="Length">O maior caminho da água até a canaleta (L), em m.</param>
/// <param name="Drop">O desnível desse caminho (ΔH), em m.</param>
/// <param name="Manual">Se alguma parte veio de ajuste manual.</param>
public sealed record BasinEntry(Guid Channel, string ChannelName, double Area, double Length, double Drop, bool Manual)
{
    /// <summary>Declividade média do caminho (S), em %.</summary>
    public double SlopePercent => Length > 0 ? Drop / Length * 100 : 0;
}

/// <summary>
/// O resultado do cálculo das bacias (30.4), gravado no desenho para a
/// memória: as bacias por canaleta, a área sem destino e a analisada.
/// </summary>
public sealed record BasinReport
{
    public IReadOnlyList<BasinEntry> Basins { get; init; } = [];

    /// <summary>Área cuja água não chega a canaleta nenhuma, em m².</summary>
    public double NoDestinationArea { get; init; }

    /// <summary>Área analisada (dentro da região e do terreno), em m².</summary>
    public double AnalyzedArea { get; init; }

    public double CellSize { get; init; }

    public DateTime When { get; init; }

    public const string StorageKey = "DRENAGEM_BACIAS";

    public string ToJson() => JsonSerializer.Serialize(this);

    public static BasinReport? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<BasinReport>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A bacia da canaleta, ou null se ela não tem (canaleta feita depois do cálculo, ou que não recebe água).</summary>
    public BasinEntry? Of(Guid channel) => Basins.FirstOrDefault(b => b.Channel == channel);
}
