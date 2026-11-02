using System.Text;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>
/// Uma mesa como vai ao TermoVision. <paramref name="Corners"/> é a face de cima
/// (o plano dos módulos), em UTM: 1 e 2 na borda BAIXA, do começo para o fim da
/// fileira; 3 e 4 na borda ALTA, do fim para o começo. É a ordem do contorno da
/// mesa no Clivus ((0,0) → (L,0) → (L,M2) → (0,M2)).
/// </summary>
/// <param name="Columns">Módulos ao longo da fileira.</param>
/// <param name="Rows">Módulos na rampa (1V = 1, 2V = 2).</param>
public sealed record TermoVisionTable(
    Guid Id, string Name, IReadOnlyList<(double E, double N, double Z)> Corners, int Columns, int Rows, TermoVisionModule Module)
{
    /// <summary>
    /// O tipo da estrutura no contrato (28/10/2026, aprovado pelo Renan:
    /// "mesa é mesa e carport é carport"): <see cref="TipoFixa"/> ou <see cref="TipoCarport"/>.
    /// </summary>
    public string Kind { get; init; } = TipoFixa;

    /// <summary>A estrutura em triângulos (UTM e cota absoluta), para o 3D de lá; null quando não vai.</summary>
    public IReadOnlyList<Scene3DFace>? Structure { get; init; }

    /// <summary>
    /// A estrutura da mesa fixa (contrato v2, 02/11/2026): <see cref="SubtipoMonoposte"/>
    /// ou <see cref="SubtipoBiposte"/>; null no carport. Só vai no projeto sem envio.
    /// </summary>
    public string? Subtype { get; init; }

    public const string SubtipoMonoposte = "monoposte";
    public const string SubtipoBiposte = "biposte";

    public const string TipoFixa = "fixa";
    public const string TipoCarport = "carport";
}

/// <summary>O corpo do <c>POST /api/clivus/v1/retorno</c>.</summary>
public static class TermoVisionReturn
{
    /// <summary>
    /// O JSON do retorno. Mesa que não serve (cantos que não são quatro ou não
    /// finitos, contagem zero, módulo sem medida) fica fora e vai para
    /// <paramref name="recusadas"/> com o motivo, para a tela contar.
    /// </summary>
    public static byte[] Build(string plugin, string desenho, IEnumerable<TermoVisionTable> mesas, out List<string> recusadas)
    {
        recusadas = [];

        using var memoria = new MemoryStream();
        using (var w = new Utf8JsonWriter(memoria))
        {
            w.WriteStartObject();
            w.WriteNumber("versao", 1);
            w.WriteString("plugin", plugin);
            w.WriteString("desenho", desenho);
            w.WriteStartArray("mesas");

            WriteTables(w, mesas, recusadas, subtype: false);

            w.WriteEndArray();
            w.WriteEndObject();
        }
        return memoria.ToArray();
    }

    /// <summary>
    /// As mesas no formato do contrato (o retorno e o projeto sem envio usam o
    /// mesmo); a que não serve vai para <paramref name="recusadas"/>. Com
    /// <paramref name="subtype"/>, a estrutura da mesa fixa leva o tipo dela
    /// (monoposte ou biposte), como o projeto sem envio pede.
    /// </summary>
    internal static void WriteTables(Utf8JsonWriter w, IEnumerable<TermoVisionTable> mesas, List<string> recusadas, bool subtype)
    {
        foreach (var m in mesas)
        {
            if (WhyInvalid(m) is { } porque)
            {
                recusadas.Add(Tr.F("{0}: {1}", string.IsNullOrEmpty(m.Name) ? m.Id.ToString() : m.Name, porque));
                continue;
            }

            w.WriteStartObject();
            w.WriteString("guid", m.Id.ToString());
            w.WriteString("nome", m.Name);
            w.WriteStartArray("cantos");
            foreach (var (e, n, z) in m.Corners)
            {
                w.WriteStartArray();
                w.WriteNumberValue(Math.Round(e, 4));
                w.WriteNumberValue(Math.Round(n, 4));
                w.WriteNumberValue(Math.Round(z, 4));
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteNumber("colunas", m.Columns);
            w.WriteNumber("fiadas", m.Rows);
            w.WriteStartObject("modulo");
            w.WriteNumber("largura_m", m.Module.Width);
            w.WriteNumber("altura_m", m.Module.Height);
            w.WriteNumber("espaco_m", m.Module.Gap);
            w.WriteEndObject();
            w.WriteString("tipo", m.Kind);

            var comTipo = subtype && m.Subtype is { Length: > 0 };
            if (m.Structure is { Count: > 0 } || comTipo)
            {
                w.WriteStartObject("estrutura");
                if (comTipo) w.WriteString("tipo", m.Subtype);
                if (m.Structure is { Count: > 0 } estrutura)
                {
                    w.WriteStartArray("triangulos");
                    foreach (var f in estrutura.Where(f => f.Corners.Count == 3))
                    {
                        w.WriteStartArray();
                        foreach (var p in f.Corners)
                        {
                            w.WriteNumberValue(Math.Round(p.X, 3));
                            w.WriteNumberValue(Math.Round(p.Y, 3));
                            w.WriteNumberValue(Math.Round(p.Z, 3));
                        }
                        w.WriteNumberValue((f.Color.R << 16) | (f.Color.G << 8) | f.Color.B);
                        w.WriteEndArray();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
    }

    /// <summary>
    /// Os quatro vértices do contorno da mesa na ordem do contrato: a borda
    /// baixa primeiro. O contorno do Clivus já nasce assim ((0,0) → (L,0) →
    /// (L,M2) → (0,M2)), mas uma mesa reconstruída pode vir com a borda alta no
    /// começo; aí a lista gira dois vértices (o sentido em volta se mantém).
    /// Mesa plana fica como veio. Nulo se não são quatro vértices.
    /// </summary>
    public static IReadOnlyList<(double E, double N, double Z)>? LowEdgeFirst(IReadOnlyList<(double E, double N, double Z)> v)
    {
        if (v.Count != 4) return null;
        var primeira = (v[0].Z + v[1].Z) / 2;
        var segunda = (v[2].Z + v[3].Z) / 2;
        return primeira > segunda + 1e-6 ? [v[2], v[3], v[0], v[1]] : [v[0], v[1], v[2], v[3]];
    }

    /// <summary>Por que a mesa não pode ir (nulo quando pode).</summary>
    public static string? WhyInvalid(TermoVisionTable m)
    {
        if (m.Corners.Count != 4) return Tr.T("a mesa não tem quatro cantos.");
        if (m.Corners.Any(c => !double.IsFinite(c.E) || !double.IsFinite(c.N) || !double.IsFinite(c.Z)))
            return Tr.T("um canto da mesa não tem coordenada.");
        if (m.Columns < 1 || m.Rows < 1) return Tr.T("a mesa não tem módulo.");
        if (!(m.Module.Width > 0) || !(m.Module.Height > 0) || !(m.Module.Gap >= 0)
            || !double.IsFinite(m.Module.Width) || !double.IsFinite(m.Module.Height) || !double.IsFinite(m.Module.Gap))
            return Tr.T("o módulo da mesa não tem medida.");
        return null;
    }
}

/// <summary>A resposta 200 do retorno.</summary>
public sealed record TermoVisionResult(
    bool Saved, string Operation, int Tables, int Removed, int Kept, int Born, int OutsideArea, IReadOnlyList<string> Warnings, string? Url)
{
    /// <summary>Lê a resposta; nulo quando não é o JSON do contrato.</summary>
    public static TermoVisionResult? TryParse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            var avisos = new List<string>();
            if (r.TryGetProperty("avisos", out var a) && a.ValueKind == JsonValueKind.Array)
                foreach (var x in a.EnumerateArray())
                    avisos.Add(x.ValueKind == JsonValueKind.String ? x.GetString()! : x.GetRawText());

            return new TermoVisionResult(
                r.TryGetProperty("gravado", out var g) && g.ValueKind == JsonValueKind.True,
                Texto(r, "operacao") ?? "",
                Inteiro(r, "mesas"), Inteiro(r, "sairam"), Inteiro(r, "mantidas"), Inteiro(r, "nasceram"),
                Inteiro(r, "fora_da_area"), avisos, Texto(r, "url"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// O texto de erro que o servidor manda (<c>{"detail": "..."}</c>), ou nulo.
    /// O FastAPI manda <c>detail</c> como lista quando a validação falha.
    /// </summary>
    public static string? Detail(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("detail", out var d)) return null;
            if (d.ValueKind == JsonValueKind.String) return d.GetString();
            if (d.ValueKind != JsonValueKind.Array) return d.GetRawText();

            var partes = new StringBuilder();
            foreach (var item in d.EnumerateArray())
            {
                if (partes.Length > 0) partes.Append("; ");
                partes.Append(item.ValueKind == JsonValueKind.Object && item.TryGetProperty("msg", out var msg) ? msg.ToString() : item.ToString());
            }
            return partes.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Número ou texto: o id da operação pode vir de um jeito ou de outro.
    private static string? Texto(JsonElement r, string nome) =>
        !r.TryGetProperty(nome, out var v) ? null
        : v.ValueKind == JsonValueKind.String ? v.GetString()
        : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;

    private static int Inteiro(JsonElement r, string nome) =>
        r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
