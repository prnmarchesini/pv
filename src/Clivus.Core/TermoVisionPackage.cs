using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>O sistema de coordenadas do pacote: UTM SIRGAS 2000, em metro.</summary>
public sealed record TermoVisionCrs(int Epsg, string Name, int Zone, string Hemisphere);

/// <summary>Uma árvore ou casa do modelo de elevação: prisma do <c>chao_m</c> ao <c>chao_m + altura_m</c>.</summary>
public sealed record TermoVisionObject(string Name, double GroundZ, double Height, IReadOnlyList<(double E, double N)> Outline)
{
    /// <summary>A área do contorno em planta (m²), sempre positiva.</summary>
    public double PlanArea => Math.Abs(TermoVisionPackage.SignedArea(Outline));

    /// <summary>O centro de massa do contorno em planta (a média dos vértices se a área for nula).</summary>
    public (double E, double N) Centroid => TermoVisionPackage.Centroid(Outline);
}

/// <summary>O módulo da Nova UFV (metro): largura ao longo da fileira, altura na rampa, espaço entre módulos.</summary>
public sealed record TermoVisionModule(double Width, double Height, double Gap);

/// <summary>
/// A grade do terreno: o nó (i, j), coluna i e linha j, fica em
/// (<see cref="E0"/> + i·<see cref="Step"/>, <see cref="N0"/> + j·<see cref="Step"/>),
/// com a cota em <c>Z[j·Columns + i]</c>. A linha 0 é a do SUL. Nulo onde não há dado.
/// </summary>
public sealed record TermoVisionGrid(string Source, double Step, double E0, double N0, int Columns, int Rows, double?[] Z)
{
    /// <summary>A cota do nó (i, j), nula sem dado ou fora da grade.</summary>
    public double? At(int i, int j) =>
        i < 0 || j < 0 || i >= Columns || j >= Rows ? null : Z[j * Columns + i];

    /// <summary>Os nós com dado, em coordenada absoluta: os vértices da superfície.</summary>
    public IEnumerable<(double E, double N, double Z)> Nodes()
    {
        for (var j = 0; j < Rows; j++)
            for (var i = 0; i < Columns; i++)
                if (Z[j * Columns + i] is { } z)
                    yield return (E0 + i * Step, N0 + j * Step, z);
    }

    /// <summary>Quantos nós têm dado.</summary>
    public int NodeCount => Z.Count(z => z.HasValue);
}

/// <summary>
/// O world file do orto (<c>.jgw</c>): o centro do pixel (col, lin) fica em
/// x = A·col + B·lin + C, y = D·col + E·lin + F (a linha cresce para baixo, E negativo).
/// </summary>
public sealed record WorldFile(double A, double D, double B, double E, double C, double F)
{
    /// <summary>Lê as seis linhas do world file, com ponto decimal.</summary>
    public static WorldFile? TryParse(string texto)
    {
        var linhas = texto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (linhas.Length < 6) return null;

        var v = new double[6];
        for (var k = 0; k < 6; k++)
            if (!double.TryParse(linhas[k], NumberStyles.Float, CultureInfo.InvariantCulture, out v[k]) || !double.IsFinite(v[k]))
                return null;

        var mundo = new WorldFile(v[0], v[1], v[2], v[3], v[4], v[5]);
        return Math.Abs(mundo.A * mundo.E - mundo.B * mundo.D) < 1e-18 ? null : mundo;
    }

    /// <summary>
    /// Onde a imagem de <paramref name="largura"/> × <paramref name="altura"/> pixels vai no desenho:
    /// o canto de baixo à esquerda e os vetores da borda de baixo (U) e da esquerda, subindo (V).
    /// </summary>
    public ((double X, double Y) Origin, (double X, double Y) U, (double X, double Y) V) Placement(int largura, int altura)
    {
        // O canto da grade de pixels é o centro do pixel menos meio pixel.
        var origem = (X: A * -0.5 + B * (altura - 0.5) + C, Y: D * -0.5 + E * (altura - 0.5) + F);
        return (origem, (A * largura, D * largura), (-B * altura, -E * altura));
    }
}

/// <summary>O orto recortado: os bytes do JPEG e onde ele fica.</summary>
public sealed record TermoVisionOrtho(byte[] Jpeg, WorldFile World, double PixelSize);

/// <summary>
/// O pacote que o TermoVision manda (<c>GET /api/clivus/v1/pacote</c>): o
/// <c>clivus.json</c> e o orto. Lido da memória, nada extraído para o disco.
/// </summary>
public sealed class TermoVisionPackage
{
    /// <summary>Limite do <c>clivus.json</c> descomprimido (1 milhão de nós cabem com folga).</summary>
    public const long MaxJsonBytes = 256L * 1024 * 1024;

    /// <summary>Limite do <c>orto.jpg</c>.</summary>
    public const long MaxOrthoBytes = 512L * 1024 * 1024;

    public required string Server { get; init; }
    public required string Shipment { get; init; }
    public required string PlantName { get; init; }
    public required string AreaName { get; init; }
    public required TermoVisionCrs Crs { get; init; }
    public GeoLocation? Location { get; init; }
    public required IReadOnlyList<(double E, double N)> Area { get; init; }
    public required TermoVisionGrid Terrain { get; init; }
    public TermoVisionOrtho? Ortho { get; init; }
    public required IReadOnlyList<TermoVisionObject> Objects { get; init; }
    public TermoVisionModule? Module { get; init; }

    /// <summary>Lê o ZIP. Falso com o motivo em português quando o pacote não serve.</summary>
    public static bool TryRead(Stream zip, out TermoVisionPackage? pacote, out string? erro)
    {
        pacote = null;
        erro = null;
        try
        {
            using var arquivo = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);

            var json = Entrada(arquivo, "clivus.json");
            if (json is null)
            {
                erro = Tr.T("O pacote do TermoVision não tem o clivus.json.");
                return false;
            }

            using var doc = JsonDocument.Parse(Ler(json, MaxJsonBytes));
            pacote = DoJson(doc.RootElement, arquivo, out erro);
            return pacote is not null;
        }
        catch (InvalidDataException)
        {
            erro = Tr.T("O pacote do TermoVision não é um ZIP válido.");
        }
        catch (JsonException)
        {
            erro = Tr.T("O clivus.json do pacote do TermoVision está corrompido.");
        }
        catch (PackageTooBigException e)
        {
            erro = Tr.F("O arquivo {0} do pacote do TermoVision passa do limite de {1} MB.", e.Name, e.Limit / (1024 * 1024));
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        {
            erro = Tr.F("O clivus.json do pacote do TermoVision tem um campo errado: {0}", e.Message);
        }

        pacote = null;
        return false;
    }

    private static TermoVisionPackage? DoJson(JsonElement raiz, ZipArchive arquivo, out string? erro)
    {
        erro = null;

        if (Numero(raiz, "versao") != 1)
        {
            erro = Tr.T("O pacote é de outra versão do TermoVision; atualize o Clivus.");
            return null;
        }

        var crs = raiz.GetProperty("crs");
        var terreno = raiz.GetProperty("terreno");
        var grade = Grade(terreno, out erro);
        if (grade is null) return null;

        var area = Pontos(raiz.GetProperty("area_utm"));
        if (area.Count < 3)
        {
            erro = Tr.T("A área do pacote do TermoVision tem menos de três vértices.");
            return null;
        }

        GeoLocation? local = null;
        if (raiz.TryGetProperty("geolocalizacao", out var geo) && geo.ValueKind == JsonValueKind.Object)
        {
            var g = new GeoLocation(Numero(geo, "lat"), Numero(geo, "lon"), GeoLocationSource.Usuario);
            if (g.IsValid && !g.LooksUnset) local = g;
        }

        var objetos = new List<TermoVisionObject>();
        if (raiz.TryGetProperty("objetos", out var lista) && lista.ValueKind == JsonValueKind.Array)
            foreach (var o in lista.EnumerateArray())
            {
                var contorno = Pontos(o.GetProperty("contorno"));
                var altura = Numero(o, "altura_m");
                var chao = Numero(o, "chao_m");
                if (contorno.Count < 3 || !(altura > 0) || !double.IsFinite(chao)) continue;
                objetos.Add(new TermoVisionObject(Texto(o, "nome"), chao, altura, contorno));
            }

        TermoVisionModule? modulo = null;
        if (raiz.TryGetProperty("projeto", out var projeto) && projeto.ValueKind == JsonValueKind.Object
            && projeto.TryGetProperty("modulo", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            var mm = new TermoVisionModule(Numero(m, "largura_m"), Numero(m, "altura_m"), Numero(m, "espaco_m"));
            if (mm.Width > 0 && mm.Height > 0 && mm.Gap >= 0) modulo = mm;
        }

        return new TermoVisionPackage
        {
            Server = Texto(raiz, "servidor"),
            Shipment = Texto(raiz, "envio"),
            PlantName = Texto(raiz.GetProperty("usina"), "nome"),
            AreaName = Texto(raiz.GetProperty("area"), "nome"),
            Crs = new TermoVisionCrs((int)Numero(crs, "epsg"), Texto(crs, "nome"), (int)Numero(crs, "zona"), Texto(crs, "hemisferio")),
            Location = local,
            Area = area,
            Terrain = grade,
            Ortho = Orto(raiz, arquivo),
            Objects = objetos,
            Module = modulo,
        };
    }

    private static TermoVisionGrid? Grade(JsonElement t, out string? erro)
    {
        erro = null;
        var passo = Numero(t, "passo_m");
        var colunas = (int)Numero(t, "colunas");
        var linhas = (int)Numero(t, "linhas");
        var e0 = Numero(t, "e0");
        var n0 = Numero(t, "n0");
        var lista = t.GetProperty("z");

        if (!(passo > 0) || colunas < 2 || linhas < 2 || !double.IsFinite(e0) || !double.IsFinite(n0)
            || (long)colunas * linhas > 50_000_000 || lista.GetArrayLength() != colunas * linhas)
        {
            erro = Tr.T("A grade do terreno do pacote do TermoVision está incompleta.");
            return null;
        }

        var z = new double?[colunas * linhas];
        var k = 0;
        foreach (var v in lista.EnumerateArray())
            z[k++] = v.ValueKind == JsonValueKind.Number && v.GetDouble() is var d && double.IsFinite(d) ? d : null;

        var grade = new TermoVisionGrid(Texto(t, "fonte"), passo, e0, n0, colunas, linhas, z);
        if (grade.NodeCount < 3)
        {
            erro = Tr.T("A grade do terreno do pacote do TermoVision não tem cota.");
            return null;
        }
        return grade;
    }

    // O orto é opcional: sem os dois arquivos, ou com o world file ruim, a área abre sem ele.
    private static TermoVisionOrtho? Orto(JsonElement raiz, ZipArchive arquivo)
    {
        if (!raiz.TryGetProperty("orto", out var o) || o.ValueKind != JsonValueKind.Object) return null;

        var jpg = Entrada(arquivo, Texto(o, "arquivo") is { Length: > 0 } a ? a : "orto.jpg");
        var jgw = Entrada(arquivo, Texto(o, "mundo") is { Length: > 0 } w ? w : "orto.jgw");
        if (jpg is null || jgw is null) return null;

        var mundo = WorldFile.TryParse(System.Text.Encoding.ASCII.GetString(Ler(jgw, 4096)));
        if (mundo is null) return null;

        var px = Numero(o, "px_m");
        return new TermoVisionOrtho(Ler(jpg, MaxOrthoBytes), mundo, px > 0 ? px : Math.Abs(mundo.A));
    }

    // Só o nome exato na raiz do ZIP: nada de caminho com pasta ou "..".
    private static ZipArchiveEntry? Entrada(ZipArchive arquivo, string nome) =>
        nome.IndexOfAny(['/', '\\']) >= 0 || nome.Contains("..") ? null
        : arquivo.Entries.FirstOrDefault(e => string.Equals(e.FullName, nome, StringComparison.OrdinalIgnoreCase));

    // Lê no máximo o limite: o tamanho declarado no ZIP pode mentir.
    private static byte[] Ler(ZipArchiveEntry entrada, long limite)
    {
        if (entrada.Length > limite) throw new PackageTooBigException(entrada.FullName, limite);

        using var origem = entrada.Open();
        using var destino = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while ((lidos = origem.Read(bloco, 0, bloco.Length)) > 0)
        {
            if (destino.Length + lidos > limite) throw new PackageTooBigException(entrada.FullName, limite);
            destino.Write(bloco, 0, lidos);
        }
        return destino.ToArray();
    }

    private static List<(double E, double N)> Pontos(JsonElement lista)
    {
        var pontos = new List<(double E, double N)>();
        foreach (var p in lista.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() < 2) continue;
            var e = p[0].GetDouble();
            var n = p[1].GetDouble();
            if (double.IsFinite(e) && double.IsFinite(n)) pontos.Add((e, n));
        }

        // Polígono fechado repetindo o primeiro vértice: o último sai.
        if (pontos.Count > 1 && pontos[0] == pontos[^1]) pontos.RemoveAt(pontos.Count - 1);
        return pontos;
    }

    private static double Numero(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;

    private static string Texto(JsonElement o, string nome) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    internal static double SignedArea(IReadOnlyList<(double E, double N)> p)
    {
        double s = 0;
        for (var k = 0; k < p.Count; k++)
        {
            var (a, b) = (p[k], p[(k + 1) % p.Count]);
            s += a.E * b.N - b.E * a.N;
        }
        return s / 2;
    }

    internal static (double E, double N) Centroid(IReadOnlyList<(double E, double N)> p)
    {
        var area = SignedArea(p);
        if (Math.Abs(area) < 1e-9) return (p.Average(q => q.E), p.Average(q => q.N));

        // Em relação ao primeiro vértice: coordenada UTM grande perde precisão no produto.
        var (e0, n0) = p[0];
        double ce = 0, cn = 0;
        for (var k = 0; k < p.Count; k++)
        {
            var (ae, an) = (p[k].E - e0, p[k].N - n0);
            var (be, bn) = (p[(k + 1) % p.Count].E - e0, p[(k + 1) % p.Count].N - n0);
            var cruz = ae * bn - be * an;
            ce += (ae + be) * cruz;
            cn += (an + bn) * cruz;
        }
        return (e0 + ce / (6 * area), n0 + cn / (6 * area));
    }

    private sealed class PackageTooBigException(string name, long limit) : Exception
    {
        public string Name { get; } = name;
        public long Limit { get; } = limit;
    }
}
