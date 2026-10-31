using System.Globalization;
using System.Reflection;
using System.Text;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma face de módulo na cena 3D: os quatro cantos e a cor (a do desenho).</summary>
public sealed record Scene3DFace(IReadOnlyList<Point3> Corners, RgbColor Color);

/// <summary>
/// Um pilar na cena 3D: centro em planta, topo e pé (cotas) e, quando se
/// sabe, a seção (42.2). Sem a seção, o visualizador usa a padrão.
/// </summary>
public sealed record Scene3DPillar(double X, double Y, double Top, double Bottom, Scene3DPillarSection? Section = null);

/// <summary>
/// A seção do pilar no 3D (42.2, Renan: "quero que no 3d do navegador, os
/// pilares também sejam modelados"): largura ao longo da mesa, profundidade
/// na inclinação (metros) e o giro em radianos, de X para Y — a escala X e Y
/// e a rotação do bloco do pilar.
/// </summary>
public sealed record Scene3DPillarSection(double Width, double Depth, double Rotation)
{
    /// <summary>A maior largura ou profundidade aceita, em metros (o servidor recusa acima).</summary>
    public const double MaxSize = 10;

    /// <summary>Se a seção serve: medidas positivas até <see cref="MaxSize"/> e giro finito.</summary>
    public bool IsValid =>
        double.IsFinite(Width) && Width is > 0 and <= MaxSize
        && double.IsFinite(Depth) && Depth is > 0 and <= MaxSize
        && double.IsFinite(Rotation);
}

/// <summary>Uma árvore na cena 3D: o pé e as medidas.</summary>
public sealed record Scene3DTree(double X, double Y, double Ground, TreeSpec Spec);

/// <summary>
/// O terreno reduzido a uma grade regular (a superfície inteira pesaria
/// demais no navegador): <see cref="Columns"/> × <see cref="Rows"/> pontos a
/// partir de (<see cref="X0"/>, <see cref="Y0"/>), de <see cref="Step"/> em
/// <see cref="Step"/> metros; null onde não há terreno. A ordem é linha a
/// linha (y), coluna a coluna (x).
/// </summary>
public sealed record Scene3DTerrain(double X0, double Y0, double Step, int Columns, int Rows, IReadOnlyList<double?> Z);

/// <summary>A cena publicada no servidor 3D: o id, o link da página e quando ela expira.</summary>
public sealed record PublishedScene(string Id, string Url, DateTime? ExpiresAt);

/// <summary>
/// Onde a usina está, para o sol do 3D (42.2): latitude e longitude em graus
/// (positivas ao norte e a leste) e o fuso do relógio em horas (−3 em
/// Brasília), o mesmo das sombras do desenho. O navegador calcula o sol com
/// ele (o <see cref="SolarCalculator"/> portado para JavaScript).
/// </summary>
public sealed record Scene3DLocation(double Latitude, double Longitude, double UtcOffsetHours)
{
    /// <summary>Se o sol sai dele: os limites do <see cref="SolarCalculator"/>, que lança exceção fora deles.</summary>
    public bool IsValid =>
        double.IsFinite(Latitude) && Latitude is >= -90 and <= 90
        && double.IsFinite(Longitude) && Longitude is >= -180 and <= 180
        && double.IsFinite(UtcOffsetHours) && UtcOffsetHours is >= -14 and <= 14;
}

/// <summary>Uma linha do resumo do 3D (42.2): mesas, módulos e potência DC; <paramref name="Outside"/> nas mesas fora das áreas.</summary>
public sealed record Scene3DSummaryRow(string Name, int Tables, int Modules, double PowerKwp, bool Outside = false)
{
    /// <summary>O teto de mesas, módulos e kWp numa linha (o servidor recusa acima).</summary>
    public const double MaxValue = 1e9;

    /// <summary>Se a linha serve: contagens e potência sem sinal, finitas e até <see cref="MaxValue"/> (o servidor recusa fora disso).</summary>
    public bool IsValid =>
        Tables is >= 0 and <= (int)MaxValue && Modules is >= 0 and <= (int)MaxValue
        && double.IsFinite(PowerKwp) && PowerKwp is >= 0 and <= MaxValue;
}

/// <summary>
/// O resumo da usina no 3D (42.2, Renan: "Nas camadas, falta a opção de
/// resumo, onde liga e desliga o mostrar de uma tabela com a quantidade de
/// módulos e potência dc"): o total e, quando há, as linhas por área e por
/// tipo de mesa.
/// </summary>
public sealed record Scene3DSummary(Scene3DSummaryRow Total, IReadOnlyList<Scene3DSummaryRow> Areas, IReadOnlyList<Scene3DSummaryRow> Types)
{
    /// <summary>
    /// O resumo pela conta da janela Áreas e do Recontar (<see cref="AreaSummary"/>):
    /// o total; por área só quando o desenho tem área (com a linha das mesas
    /// fora delas); por tipo, os tipos que têm mesa.
    /// </summary>
    public static Scene3DSummary From(AreaSummary resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);

        static Scene3DSummaryRow Linha(AreaSummaryRow r) =>
            new(r.Name, r.Census.Tables, r.Census.Modules, r.Census.PowerKwp, r.Kind == AreaRowKind.Outside);

        var areas = resumo.Areas.Count == 0
            ? []
            : resumo.Areas.Select(Linha).Concat(resumo.Outside is { } fora ? [Linha(fora)] : []).ToList();

        var tipos = resumo.Total.Types
            .Where(t => t.Tables > 0)
            .Select(t => new Scene3DSummaryRow(t.Name, t.Tables, t.Modules, t.PowerKwp))
            .ToList();

        return new Scene3DSummary(Linha(resumo.Total), areas, tipos);
    }
}

/// <summary>
/// A cena da página 3D. Sem <paramref name="Location"/>, a página fica sem o
/// sol (a luz de sempre); sem <paramref name="Summary"/>, sem o quadro do resumo.
/// </summary>
public sealed record Scene3D(
    string Title,
    Scene3DTerrain? Terrain,
    IReadOnlyList<Scene3DFace> Faces,
    IReadOnlyList<Scene3DPillar> Pillars,
    IReadOnlyList<Scene3DTree> Trees,
    IReadOnlyList<IReadOnlyList<Point3>> Shadows,
    IReadOnlyList<Scene3DFace>? Channels = null,
    Scene3DLocation? Location = null,
    Scene3DSummary? Summary = null);

/// <summary>
/// A página 3D no navegador (9.9). Renan, 03/10/2026: "o PVcase tinha uma
/// função dessa, eu clicava, abria uma página web com um modelo 3D que eu
/// podia navegar, tinha o terreno, mesas". Um arquivo HTML só, com a
/// three.js (MIT) embutida: abre sem internet e pode ser mandado por e-mail.
/// As coordenadas vão relativas ao centro da cena (UTM em float de 32 bits
/// perderia o centímetro).
/// </summary>
public static class Viewer3DPage
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// A grade do terreno: no máximo <paramref name="maxCells"/> células no
    /// lado maior da caixa, e nunca menos de 1 m entre pontos.
    /// </summary>
    public static Scene3DTerrain SampleTerrain(double minX, double minY, double maxX, double maxY, Func<double, double, double?> z, int maxCells = 200)
    {
        ArgumentNullException.ThrowIfNull(z);
        if (!(maxX > minX) || !(maxY > minY)) throw new ArgumentException("a caixa do terreno não tem área");
        if (maxCells < 2) throw new ArgumentOutOfRangeException(nameof(maxCells));

        var passo = Math.Max(Math.Max(maxX - minX, maxY - minY) / maxCells, 1.0);
        var colunas = (int)Math.Ceiling((maxX - minX) / passo) + 1;
        var linhas = (int)Math.Ceiling((maxY - minY) / passo) + 1;
        var cotas = new double?[colunas * linhas];

        for (var j = 0; j < linhas; j++)
            for (var i = 0; i < colunas; i++)
                cotas[j * colunas + i] = z(minX + i * passo, minY + j * passo);

        return new Scene3DTerrain(minX, minY, passo, colunas, linhas, cotas);
    }

    /// <summary>
    /// A cena sem o que nasceu sem chão: módulo cujo centro não tem terreno
    /// embaixo e que está fora da faixa de cotas do terreno (no desenho, plano
    /// na cota 0 e marcado, regra 5) fica fora; pilar idem. No 3D seriam uma
    /// mesa solta centenas de metros abaixo da usina, e o enquadramento ficaria
    /// pequeno (CANAL.md, 04/10/2026). Módulo na beira, sem terreno embaixo mas
    /// na altura das vizinhas, fica. A cota não é inventada.
    /// </summary>
    public static (Scene3D Cena, int ModulosFora, int PilaresFora) OnTerrain(Scene3D scene, Func<double, double, double?> ground)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(ground);

        var cotas = scene.Terrain?.Z.Where(v => v is not null).Select(v => v!.Value).ToList() ?? [];
        if (cotas.Count == 0) return (scene, 0, 0);

        // A faixa de cotas do terreno, com folga para a mesa alta e o pilar enterrado.
        double baixo = cotas.Min() - MargemAbaixo, alto = cotas.Max() + MargemAcima;
        bool NaFaixa(double z) => z >= baixo && z <= alto;

        var faces = scene.Faces
            .Where(f => ground(f.Corners.Average(p => p.X), f.Corners.Average(p => p.Y)) is not null || f.Corners.All(p => NaFaixa(p.Z)))
            .ToList();
        var pilares = scene.Pillars
            .Where(p => ground(p.X, p.Y) is not null || (NaFaixa(p.Top) && NaFaixa(p.Bottom)))
            .ToList();

        return (scene with { Faces = faces, Pillars = pilares }, scene.Faces.Count - faces.Count, scene.Pillars.Count - pilares.Count);
    }

    /// <summary>Folga abaixo da cota mais baixa do terreno (pilar enterrado), em metros.</summary>
    public const double MargemAbaixo = 5;

    /// <summary>Folga acima da cota mais alta do terreno (mesa alta), em metros.</summary>
    public const double MargemAcima = 15;

    /// <summary>
    /// As casas decimais da latitude e da longitude no envio ao servidor
    /// (42.2): 0,1° são uns 11 km, o sol muda menos de 0,05° e o lugar exato da
    /// usina fica no computador, como a origem (plano/seguranca.md).
    /// </summary>
    public const int PublishedLocationDecimals = 1;

    /// <summary>A origem da cena: o centro da caixa de tudo que ela tem.</summary>
    public static Point3 Origin(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var pontos = new List<Point3>();

        if (scene.Terrain is { } t)
        {
            pontos.Add(new Point3(t.X0, t.Y0, 0));
            pontos.Add(new Point3(t.X0 + (t.Columns - 1) * t.Step, t.Y0 + (t.Rows - 1) * t.Step, 0));
        }

        pontos.AddRange(scene.Faces.SelectMany(f => f.Corners));
        pontos.AddRange((scene.Channels ?? []).SelectMany(f => f.Corners));
        pontos.AddRange(scene.Trees.Select(a => new Point3(a.X, a.Y, a.Ground)));

        if (pontos.Count == 0) return new Point3(0, 0, 0);

        var cotas = scene.Faces.SelectMany(f => f.Corners).Select(p => p.Z)
            .Concat(scene.Terrain?.Z.Where(v => v is not null).Select(v => v!.Value) ?? [])
            .ToList();

        return new Point3(
            (pontos.Min(p => p.X) + pontos.Max(p => p.X)) / 2,
            (pontos.Min(p => p.Y) + pontos.Max(p => p.Y)) / 2,
            cotas.Count > 0 ? cotas.Min() : 0);
    }

    /// <summary>
    /// A cena em JSON, relativa à origem, com milímetro. Sem
    /// <paramref name="comOrigem"/>, a origem vai [0,0,0]: a coordenada real da
    /// usina não sai do computador (plano/seguranca.md; o visualizador do
    /// servidor não usa o campo, CANAL.md de 04/10/2026). Pelo mesmo motivo o
    /// lugar do sol (42.2) vai, então, arredondado a <see cref="PublishedLocationDecimals"/> casa.
    /// </summary>
    public static string Json(Scene3D scene, bool comOrigem = true)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var o = Origin(scene);
        var s = new StringBuilder();

        string N(double v) => Math.Round(v, 3).ToString("0.###", Inv);
        string X(double x) => N(x - o.X);
        string Y(double y) => N(y - o.Y);
        string Z(double z) => N(z - o.Z);

        s.Append("{\"titulo\":").Append(Texto(scene.Title));
        if (comOrigem) s.Append(",\"origem\":[").Append(N(o.X)).Append(',').Append(N(o.Y)).Append(',').Append(N(o.Z)).Append(']');
        else s.Append(",\"origem\":[0,0,0]");

        // O lugar, para o navegador calcular o sol (42.2). Na página local vai
        // com o centímetro; no envio ao servidor, a 0,1° (uns 11 km), que muda
        // o sol em menos de 0,05°. Lugar que o SolarCalculator recusaria não vai:
        // a página fica com a luz de sempre e o servidor não devolve 400.
        if (scene.Location is { IsValid: true } lugar)
        {
            // O "+ 0.0" tira o −0 (−0,04 arredondado).
            string G(double v) => comOrigem
                ? (Math.Round(v, 7) + 0.0).ToString("0.#######", Inv)
                : (Math.Round(v, PublishedLocationDecimals) + 0.0).ToString("0.#", Inv);

            s.Append(",\"local\":{\"latitude\":").Append(G(lugar.Latitude))
                .Append(",\"longitude\":").Append(G(lugar.Longitude))
                .Append(",\"fuso\":").Append(N(lugar.UtcOffsetHours)).Append('}');
        }

        if (scene.Terrain is { } t)
        {
            s.Append(",\"terreno\":{\"x0\":").Append(X(t.X0)).Append(",\"y0\":").Append(Y(t.Y0))
                .Append(",\"passo\":").Append(N(t.Step)).Append(",\"colunas\":").Append(t.Columns).Append(",\"linhas\":").Append(t.Rows)
                .Append(",\"z\":[").Append(string.Join(',', t.Z.Select(v => v is { } z ? Z(z) : "null"))).Append("]}");
        }

        s.Append(",\"faces\":[");
        s.Append(string.Join(',', scene.Faces.Select(f =>
            "[" + string.Join(',', f.Corners.Select(p => X(p.X) + "," + Y(p.Y) + "," + Z(p.Z))) + "," + ((f.Color.R << 16) | (f.Color.G << 8) | f.Color.B).ToString(Inv) + "]")));
        s.Append(']');

        // O pilar com a seção (42.2) leva largura, profundidade e giro; o giro
        // com 4 casas (0,006°). Seção que não serve fica de fora: o
        // visualizador desenha a padrão.
        s.Append(",\"pilares\":[");
        s.Append(string.Join(',', scene.Pillars.Select(p => "[" + X(p.X) + "," + Y(p.Y) + "," + Z(p.Top) + "," + Z(p.Bottom)
            + (p.Section is { IsValid: true } secao
                ? "," + N(secao.Width) + "," + N(secao.Depth) + "," + (Math.Round(secao.Rotation, 4) + 0.0).ToString("0.####", Inv)
                : string.Empty)
            + "]")));
        s.Append(']');

        s.Append(",\"arvores\":[");
        s.Append(string.Join(',', scene.Trees.Select(a =>
            "[" + X(a.X) + "," + Y(a.Y) + "," + Z(a.Ground) + "," + N(a.Spec.TrunkHeight) + "," + N(a.Spec.TrunkWidth) + "," + N(a.Spec.CrownHeight) + "," + N(a.Spec.CrownWidth) + "]")));
        s.Append(']');

        s.Append(",\"sombras\":[");
        s.Append(string.Join(',', scene.Shadows.Select(c => "[" + string.Join(',', c.Select(p => X(p.X) + "," + Y(p.Y) + "," + Z(p.Z))) + "]")));
        s.Append(']');

        // As canaletas da drenagem (29.3): triângulos, três cantos e a cor.
        s.Append(",\"canaletas\":[");
        s.Append(string.Join(',', (scene.Channels ?? []).Select(f =>
            "[" + string.Join(',', f.Corners.Select(p => X(p.X) + "," + Y(p.Y) + "," + Z(p.Z))) + "," + ((f.Color.R << 16) | (f.Color.G << 8) | f.Color.B).ToString(Inv) + "]")));
        s.Append(']');

        // O resumo (42.2): o total e as linhas por área e por tipo. Linha que
        // não serve fica de fora; sem o total, o resumo inteiro.
        if (scene.Summary is { Total.IsValid: true } resumo)
        {
            string Linha(Scene3DSummaryRow l, bool comNome) =>
                "{" + (comNome ? "\"nome\":" + Texto(l.Name) + "," : string.Empty)
                + (l.Outside ? "\"fora\":true," : string.Empty)
                + "\"mesas\":" + l.Tables.ToString(Inv) + ",\"modulos\":" + l.Modules.ToString(Inv) + ",\"kwp\":" + N(l.PowerKwp) + "}";

            string Lista(IReadOnlyList<Scene3DSummaryRow>? linhas) =>
                "[" + string.Join(',', (linhas ?? []).Where(l => l.IsValid).Select(l => Linha(l, true))) + "]";

            s.Append(",\"resumo\":{\"total\":").Append(Linha(resumo.Total, false))
                .Append(",\"areas\":").Append(Lista(resumo.Areas))
                .Append(",\"tipos\":").Append(Lista(resumo.Types)).Append('}');
        }

        s.Append('}');

        return s.ToString();
    }

    /// <summary>
    /// O corpo do envio ao servidor 3D (plano/contrato-servidor-3d.md): versão
    /// do contrato, versão do plugin, nome do desenho e a cena, sem a origem
    /// absoluta.
    /// </summary>
    public static string PublishBody(Scene3D scene, string pluginVersion, string drawing)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return "{\"versao\":1,\"plugin\":" + Texto(pluginVersion ?? string.Empty)
            + ",\"desenho\":" + Texto(drawing ?? string.Empty)
            + ",\"cena\":" + Json(scene, comOrigem: false) + "}";
    }

    /// <summary>
    /// A resposta do servidor: o link da página 3D, ou o erro em português
    /// que o servidor mandou (ou um nosso, quando a resposta não é do contrato).
    /// </summary>
    public static (PublishedScene? Publicada, string? Erro) ParseResponse(int status, string? corpo)
    {
        System.Text.Json.JsonElement raiz = default;
        var temJson = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(corpo))
            {
                raiz = System.Text.Json.JsonDocument.Parse(corpo).RootElement;
                temJson = raiz.ValueKind == System.Text.Json.JsonValueKind.Object;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            temJson = false;
        }

        if (status is >= 200 and < 300)
        {
            if (temJson
                && raiz.TryGetProperty("id", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String
                && raiz.TryGetProperty("url", out var url) && url.ValueKind == System.Text.Json.JsonValueKind.String
                && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var endereco)
                && (endereco.Scheme == Uri.UriSchemeHttps || endereco.Scheme == Uri.UriSchemeHttp))
            {
                DateTime? expira = raiz.TryGetProperty("expira_em", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String
                    && DateTime.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)
                    ? quando
                    : null;

                return (new PublishedScene(id.GetString()!, endereco.ToString(), expira), null);
            }

            return (null, Tr.T("o servidor respondeu sem o link da página 3D"));
        }

        if (temJson && raiz.TryGetProperty("erro", out var erro) && erro.ValueKind == System.Text.Json.JsonValueKind.String)
            return (null, erro.GetString());

        return (null, status switch
        {
            401 or 403 => Tr.T("a chave do servidor não foi aceita (confira CLIVUS_SERVIDOR_CHAVE)"),
            413 => Tr.T("a usina é grande demais para o servidor"),
            _ => Tr.F("o servidor respondeu {0}", status),
        });
    }

    /// <summary>
    /// Se o endereço do servidor serve (plano/seguranca.md): HTTPS; http só em
    /// localhost (o servidor falso dos testes). Null se serve, o porquê se não.
    /// </summary>
    public static string? WhyServerUnsafe(string? endereco)
    {
        if (!Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) return Tr.T("o endereço do servidor não é um endereço web");
        if (uri.Scheme == Uri.UriSchemeHttps) return null;
        if (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) return null;
        return Tr.T("o servidor precisa ser HTTPS (http só em localhost, para teste)");
    }

    /// <summary>
    /// Se o link devolvido pode ser aberto no navegador: do mesmo host e
    /// esquema do servidor configurado. Um servidor comprometido não consegue
    /// mandar o usuário para outro site (plano/seguranca.md).
    /// </summary>
    public static bool IsLinkFromServer(string link, string servidor) =>
        Uri.TryCreate(link, UriKind.Absolute, out var l)
        && Uri.TryCreate(servidor, UriKind.Absolute, out var s)
        && string.Equals(l.Scheme, s.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(l.Host, s.Host, StringComparison.OrdinalIgnoreCase)
        && l.Port == s.Port
        && WhyServerUnsafe(servidor) is null;

    /// <summary>
    /// A página inteira: a three.js, os controles de órbita, o sol e o resumo
    /// (42.2), o logo, a cena e o visualizador.
    /// </summary>
    public static string Html(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var three = Recurso("Clivus.Core.three.min.js");
        var orbita = Recurso("Clivus.Core.OrbitControls.js");
        var sol = Recurso("Clivus.Core.sol.js");
        var resumo = Recurso("Clivus.Core.resumo.js");
        var logo = Recurso("Clivus.Core.clivus-logo.svg");

        // Os scripts entram antes da cena e o título por último: um nome de
        // desenho (ou de área, no resumo) com "{{ORBITA}}" não pode virar o
        // lugar onde o script entra. Na cena, "{{" vira "{{" (o mesmo
        // texto para o JSON), e nenhuma marca sobra dentro dela.
        static string H(string texto) => System.Net.WebUtility.HtmlEncode(texto);

        return Modelo
            .Replace("{{LANG}}", Tr.Culture.Name, StringComparison.Ordinal)
            .Replace("{{TXT_TERRENO}}", H(Tr.T("Terreno")), StringComparison.Ordinal)
            .Replace("{{TXT_MESAS}}", H(Tr.T("Mesas")), StringComparison.Ordinal)
            .Replace("{{TXT_PILARES}}", H(Tr.T("Pilares")), StringComparison.Ordinal)
            .Replace("{{TXT_ARVORES}}", H(Tr.T("Árvores")), StringComparison.Ordinal)
            .Replace("{{TXT_SOMBRAS}}", H(Tr.T("Sombras")), StringComparison.Ordinal)
            .Replace("{{TXT_CANALETAS}}", H(Tr.T("Canaletas, carport e estrutura do biposte")), StringComparison.Ordinal)
            .Replace("{{TXT_EXAGERO}}", H(Tr.T("Exagero vertical")), StringComparison.Ordinal)
            .Replace("{{TXT_ENQUADRAR}}", H(Tr.T("Enquadrar")), StringComparison.Ordinal)
            .Replace("{{TXT_DICA}}", H(Tr.T("Botão esquerdo gira, o direito arrasta, a roda dá zoom.")), StringComparison.Ordinal)
            .Replace("{{TXT_RESUMO}}", Texto(Tr.T("{0} módulos, {1} pilares, {2} árvore(s)")), StringComparison.Ordinal)
            .Replace("{{TXT_RESUMO_SOMBRAS}}", Texto(Tr.T(", {0} contorno(s) de sombra")), StringComparison.Ordinal)
            .Replace("{{TXT_QUADRO}}", H(Tr.T("Resumo")), StringComparison.Ordinal)
            .Replace("{{TXT_MODULOS}}", H(Tr.T("Módulos")), StringComparison.Ordinal)
            .Replace("{{TXT_TOTAL}}", H(Tr.T("Total")), StringComparison.Ordinal)
            .Replace("{{TXT_FORA}}", H(Tr.T("Fora das áreas")), StringComparison.Ordinal)
            .Replace("{{TXT_AREA}}", H(Tr.T("Área")), StringComparison.Ordinal)
            .Replace("{{TXT_TIPO}}", H(Tr.T("Tipo de mesa")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL}}", H(Tr.T("Sol")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_SOMBRAS}}", H(Tr.T("Sombras do sol")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_DATA}}", H(Tr.T("Data")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_HORA}}", H(Tr.T("Hora (relógio da usina)")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_ANIMAR}}", H(Tr.T("Animar o dia")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_PAUSAR}}", H(Tr.T("Pausar")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_POSICAO}}", H(Tr.T("Altura do sol {0}°, azimute {1}°")), StringComparison.Ordinal)
            .Replace("{{TXT_SOL_ABAIXO}}", H(Tr.T("Sol abaixo do horizonte")), StringComparison.Ordinal)
            .Replace("{{TXT_EQUINOCIO_MARCO}}", H(Tr.T("Equinócio de março")), StringComparison.Ordinal)
            .Replace("{{TXT_SOLSTICIO_JUNHO}}", H(Tr.T("Solstício de junho")), StringComparison.Ordinal)
            .Replace("{{TXT_EQUINOCIO_SETEMBRO}}", H(Tr.T("Equinócio de setembro")), StringComparison.Ordinal)
            .Replace("{{TXT_SOLSTICIO_DEZEMBRO}}", H(Tr.T("Solstício de dezembro")), StringComparison.Ordinal)
            .Replace("{{LOGO}}", logo, StringComparison.Ordinal)
            .Replace("{{THREE}}", three, StringComparison.Ordinal)
            .Replace("{{ORBITA}}", orbita, StringComparison.Ordinal)
            .Replace("{{SOL}}", sol, StringComparison.Ordinal)
            .Replace("{{RESUMO_JS}}", resumo, StringComparison.Ordinal)
            .Replace("{{CENA}}", Json(scene).Replace("</", "<\\/", StringComparison.Ordinal).Replace("{{", "{\\u007b", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("{{TITULO}}", System.Net.WebUtility.HtmlEncode(scene.Title), StringComparison.Ordinal);
    }

    private static string Recurso(string nome)
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"o recurso {nome} não está na DLL");
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    private static string Texto(string t)
    {
        var s = new StringBuilder("\"");

        foreach (var c in t)
        {
            switch (c)
            {
                case '"': s.Append("\\\""); break;
                case '\\': s.Append("\\\\"); break;
                case '<': s.Append("\\u003c"); break;
                default:
                    if (c < 0x20) s.Append("\\u").Append(((int)c).ToString("x4", Inv));
                    else s.Append(c);
                    break;
            }
        }

        return s.Append('"').ToString();
    }

    /// <summary>
    /// O visualizador. X a leste e Y ao norte no desenho viram X e −Z na
    /// three.js (que tem Y para cima). Botão esquerdo gira, direito arrasta,
    /// roda dá zoom; cada camada liga e desliga; o exagero vertical ajuda a
    /// ver o relevo. Desde o 42.2: o sol com sombra (com o lugar na cena), os
    /// pilares em caixas, o quadro do resumo e o logo do Clivus no canto.
    /// </summary>
    private const string Modelo = """
<!doctype html>
<html lang="{{LANG}}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{TITULO}} — 3D</title>
<style>
  :root { --painel: rgba(255,255,255,.92); --texto: #1d2733; --borda: #c9d2dc; --suave: #5b6b7c; }
  html, body { margin: 0; height: 100%; overflow: hidden; background: #cfe3f3; font: 13px/1.4 "Segoe UI", system-ui, sans-serif; color: var(--texto); }
  [hidden] { display: none !important; }
  .caixa { position: absolute; background: var(--painel); border: 1px solid var(--borda); border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,.12); }
  #painel { top: 12px; left: 12px; padding: 10px 12px; max-width: 300px; max-height: calc(100% - 50px); overflow-y: auto; }
  #painel h1 { font-size: 14px; margin: 0 0 6px; }
  #painel label { display: inline-flex; align-items: center; gap: 4px; margin: 2px 10px 2px 0; cursor: pointer; }
  #painel .dica { color: var(--suave); margin-top: 6px; font-size: 12px; }
  #painel select, #painel button, #painel input[type=date] { font: inherit; }
  #solPainel { margin-top: 8px; padding-top: 6px; border-top: 1px solid var(--borda); }
  #solPainel .linha { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; margin: 3px 0; }
  #solHora { width: 100%; margin: 4px 0 2px; }
  #solHoraTexto { font-variant-numeric: tabular-nums; }
  #solPainel .marcos button { font-size: 12px; padding: 1px 6px; }
  #resumoQuadro { top: 12px; right: 12px; padding: 6px 10px 8px; max-height: calc(100% - 90px); overflow-y: auto; font-size: 12px; }
  #resumoQuadro caption { text-align: left; font-weight: 600; font-size: 13px; padding-bottom: 2px; }
  #resumoQuadro th, #resumoQuadro td { padding: 1px 0 1px 12px; text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
  #resumoQuadro th:first-child, #resumoQuadro td:first-child { padding-left: 0; text-align: left; }
  #resumoQuadro th { color: var(--suave); font-weight: 400; }
  #resumoQuadro tr.total td { font-weight: 600; }
  #resumoQuadro tr.secao td { padding-top: 6px; color: var(--suave); font-weight: 600; }
  #resumoQuadro tr.fora td:first-child { font-style: italic; }
  #marca { position: absolute; right: 12px; bottom: 12px; width: 92px; opacity: .92; pointer-events: none; }
  #marca svg { display: block; width: 100%; height: auto; }
</style>
</head>
<body>
<div id="painel" class="caixa">
  <h1>{{TITULO}}</h1>
  <div>
    <label><input type="checkbox" id="vTerreno" checked> {{TXT_TERRENO}}</label>
    <label><input type="checkbox" id="vMesas" checked> {{TXT_MESAS}}</label>
    <label><input type="checkbox" id="vPilares" checked> {{TXT_PILARES}}</label>
    <label><input type="checkbox" id="vArvores" checked> {{TXT_ARVORES}}</label>
    <label><input type="checkbox" id="vSombras" checked> {{TXT_SOMBRAS}}</label>
    <label><input type="checkbox" id="vCanaletas" checked> {{TXT_CANALETAS}}</label>
    <label id="itemResumo" hidden><input type="checkbox" id="vResumo" checked> {{TXT_QUADRO}}</label>
  </div>
  <div>{{TXT_EXAGERO}} <select id="exagero"><option value="1">1×</option><option value="2">2×</option><option value="3">3×</option><option value="5">5×</option></select>
    <button id="enquadrar">{{TXT_ENQUADRAR}}</button></div>
  <div id="solPainel" hidden data-animar="{{TXT_SOL_ANIMAR}}" data-pausar="{{TXT_SOL_PAUSAR}}" data-posicao="{{TXT_SOL_POSICAO}}" data-abaixo="{{TXT_SOL_ABAIXO}}">
    <div class="linha"><b>{{TXT_SOL}}</b> <label><input type="checkbox" id="solSombras" checked> {{TXT_SOL_SOMBRAS}}</label></div>
    <div class="linha"><input type="date" id="solData" title="{{TXT_SOL_DATA}}" aria-label="{{TXT_SOL_DATA}}">
      <button type="button" id="solTocar">&#9654;</button> <span id="solHoraTexto"></span></div>
    <input type="range" id="solHora" min="0" max="1439" step="5" title="{{TXT_SOL_HORA}}" aria-label="{{TXT_SOL_HORA}}">
    <div class="linha marcos">
      <button type="button" data-marco="0" title="{{TXT_EQUINOCIO_MARCO}}"></button>
      <button type="button" data-marco="1" title="{{TXT_SOLSTICIO_JUNHO}}"></button>
      <button type="button" data-marco="2" title="{{TXT_EQUINOCIO_SETEMBRO}}"></button>
      <button type="button" data-marco="3" title="{{TXT_SOLSTICIO_DEZEMBRO}}"></button>
    </div>
    <div class="dica" id="solTexto"></div>
  </div>
  <div class="dica" id="resumo"></div>
  <div class="dica">{{TXT_DICA}}</div>
</div>
<div id="resumoQuadro" class="caixa" hidden data-titulo="{{TXT_QUADRO}}" data-mesas="{{TXT_MESAS}}" data-modulos="{{TXT_MODULOS}}" data-total="{{TXT_TOTAL}}" data-fora="{{TXT_FORA}}" data-area="{{TXT_AREA}}" data-tipo="{{TXT_TIPO}}"></div>
<div id="marca" aria-hidden="true">{{LOGO}}</div>
<script>{{THREE}}</script>
<script>{{ORBITA}}</script>
<script>
{{SOL}}
{{RESUMO_JS}}
const D = {{CENA}};
const cena = new THREE.Scene();
cena.background = new THREE.Color(0xcfe3f3);
const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.5, 50000);
const render = new THREE.WebGLRenderer({ antialias: true });
render.setPixelRatio(devicePixelRatio);
render.setSize(innerWidth, innerHeight);
document.body.appendChild(render.domElement);
const ceu = new THREE.HemisphereLight(0xffffff, 0x8a7a60, 0.75);
cena.add(ceu);
const sol = new THREE.DirectionalLight(0xffffff, 0.65);
sol.position.set(-300, 600, -200);
cena.add(sol);

// Desenho (x leste, y norte, z cima) -> three (x, z, -y).
const mundo = new THREE.Group();
cena.add(mundo);
const P = (x, y, z) => [x, z, -y];
const camadas = {};
function camada(nome) { const g = new THREE.Group(); camadas[nome] = g; mundo.add(g); return g; }

// Se o triângulo a, b, c (já na three) tem a normal para cima. A sombra do
// sol afasta da superfície, ao longo da normal, o ponto que ela consulta no
// mapa: com a normal para baixo, a face faria sombra nela mesma.
const paraCima = (a, b, c) => (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]) >= 0;

// Terreno: grade com cor pela cota, os triângulos com a normal para cima.
if (D.terreno) {
  const t = D.terreno, pos = [], cor = [], idx = [];
  let zmin = Infinity, zmax = -Infinity; for (const v of t.z) if (v !== null) { if (v < zmin) zmin = v; if (v > zmax) zmax = v; }
  const amp = Math.max(zmax - zmin, 1);
  const baixo = new THREE.Color(0x7fa86a), alto = new THREE.Color(0xb59a6a);
  const indice = new Int32Array(t.colunas * t.linhas).fill(-1);
  for (let j = 0; j < t.linhas; j++) for (let i = 0; i < t.colunas; i++) {
    const z = t.z[j * t.colunas + i]; if (z === null) continue;
    indice[j * t.colunas + i] = pos.length / 3;
    pos.push(...P(t.x0 + i * t.passo, t.y0 + j * t.passo, z));
    const c = baixo.clone().lerp(alto, (z - zmin) / amp); cor.push(c.r, c.g, c.b);
  }
  for (let j = 0; j + 1 < t.linhas; j++) for (let i = 0; i + 1 < t.colunas; i++) {
    const a = indice[j * t.colunas + i], b = indice[j * t.colunas + i + 1], c = indice[(j + 1) * t.colunas + i + 1], d = indice[(j + 1) * t.colunas + i];
    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
    idx.push(a, b, c, a, c, d);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(cor, 3));
  g.setIndex(idx); g.computeVertexNormals();
  camada('terreno').add(new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide })));
}

// Módulos: dois triângulos por face, com a cor do desenho e a normal para cima.
{
  const pos = [], cor = [];
  for (const f of D.faces) {
    const v = [0, 1, 2, 3].map(k => P(f[3 * k], f[3 * k + 1], f[3 * k + 2]));
    const c = new THREE.Color(f[12]);
    for (const k of paraCima(v[0], v[1], v[2]) ? [0, 1, 2, 0, 2, 3] : [0, 2, 1, 0, 3, 2]) { pos.push(...v[k]); cor.push(c.r, c.g, c.b); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(cor, 3));
  g.computeVertexNormals();
  const malha = new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -1 }));
  const bordas = new THREE.LineSegments(new THREE.EdgesGeometry(g), new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.35 }));
  const m = camada('mesas'); m.add(malha); m.add(bordas);
}

// Pilares (42.2, "quero que no 3d do navegador, os pilares também sejam
// modelados"): uma caixa por pilar, na seção e no giro do bloco do pilar
// (largura, profundidade e rotação, de X para Y), do pé no terreno ao topo;
// a cena sem a seção, 0,10 × 0,10. Uma malha só, por instância, no aço da
// estrutura.
{
  const lista = D.pilares || [], m = camada('pilares');
  if (lista.length) {
    const caixas = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshLambertMaterial({ color: 0xa8b0b8 }), lista.length);
    const matriz = new THREE.Matrix4(), giro = new THREE.Quaternion(), eixo = new THREE.Vector3(0, 1, 0), ponto = new THREE.Vector3(), escala = new THREE.Vector3();
    const medida = v => (typeof v === 'number' && v > 0 && v <= 10 ? v : 0.1);
    lista.forEach((p, i) => {
      const [x, y, topo, pe] = p;
      ponto.set(...P(x, y, (topo + pe) / 2));
      giro.setFromAxisAngle(eixo, Number.isFinite(p[6]) ? p[6] : 0);
      escala.set(medida(p[4]), Math.max(Math.abs(topo - pe), 0.01), medida(p[5]));
      caixas.setMatrixAt(i, matriz.compose(ponto, giro, escala));
    });
    // A three r128 não conhece a caixa das instâncias (só a da caixa 1 × 1 × 1
    // na origem): sem isto, os pilares somem quando a origem sai da tela.
    caixas.frustumCulled = false;
    m.add(caixas);
  }
}

// Árvores: tronco e copa.
{
  const a = camada('arvores');
  const tronco = new THREE.MeshLambertMaterial({ color: 0x785028 }), copa = new THREE.MeshLambertMaterial({ color: 0x288c3c });
  for (const t of D.arvores) {
    const [x, y, z, ht, lt, hc, lc] = t;
    const m1 = new THREE.Mesh(new THREE.CylinderGeometry(lt / 2, lt / 2, ht, 16), tronco);
    m1.position.set(...P(x, y, z + ht / 2)); a.add(m1);
    const m2 = new THREE.Mesh(new THREE.CylinderGeometry(lc / 2, lc / 2, hc, 24), copa);
    m2.position.set(...P(x, y, z + ht + hc / 2)); a.add(m2);
  }
}

// Sombras desenhadas no CAD.
{
  // Por cima de tudo: a grade do terreno da página é mais grossa que a do
  // CAD e cobriria parte do contorno.
  const s = camada('sombras'), mat = new THREE.LineBasicMaterial({ color: 0x222222, depthTest: false, transparent: true });
  for (const c of D.sombras) {
    const pts = []; for (let k = 0; k < c.length; k += 3) pts.push(new THREE.Vector3(...P(c[k], c[k + 1], c[k + 2] + 0.05)));
    const laco = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(pts), mat);
    laco.renderOrder = 10;
    s.add(laco);
  }
}

// Canaletas da drenagem: a parede de concreto em triângulos. A face quase
// deitada vai com a normal para cima: a de cima precisa dela para a sombra,
// e a de baixo não pega sol de qualquer jeito.
{
  const pos = [], cor = [];
  for (const f of (D.canaletas || [])) {
    const c = new THREE.Color(f[9]);
    const v = [0, 1, 2].map(k => P(f[3 * k], f[3 * k + 1], f[3 * k + 2]));
    const ux = v[1][0] - v[0][0], uy = v[1][1] - v[0][1], uz = v[1][2] - v[0][2], wx = v[2][0] - v[0][0], wy = v[2][1] - v[0][1], wz = v[2][2] - v[0][2];
    const nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
    for (const k of ny < -0.3 * Math.hypot(nx, ny, nz) ? [0, 2, 1] : [0, 1, 2]) { pos.push(...v[k]); cor.push(c.r, c.g, c.b); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(cor, 3));
  g.computeVertexNormals();
  camada('canaletas').add(new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -2 })));
}

const controles = new THREE.OrbitControls(camera, render.domElement);
controles.enableDamping = true;

// O sol (42.2): com o lugar na cena, a luz vem de onde o sol está e faz
// sombra; sem ele, a luz de sempre.
const sombraDoSol = ClivusSol.montar({ THREE, render, cena, mundo, luz: sol, ceu, D, camadas, usina: ['mesas', 'pilares', 'arvores', 'canaletas'], forcaSol: 0.75, forcaCeu: 0.6 });

// O resumo (42.2): o quadro com as mesas, os módulos e a potência DC.
ClivusResumo.montar(D, true);

// Enquadra a usina (mesas, árvores, canaletas e carport); sem elas, o terreno inteiro.
function enquadrar() {
  const caixa = new THREE.Box3();
  for (const nome of ['mesas', 'arvores', 'canaletas']) if (camadas[nome] && camadas[nome].visible) caixa.expandByObject(camadas[nome]);
  if (caixa.isEmpty()) caixa.setFromObject(mundo);
  if (caixa.isEmpty()) return;
  const centro = caixa.getCenter(new THREE.Vector3()), tam = caixa.getSize(new THREE.Vector3()).length();
  camera.position.copy(centro).add(new THREE.Vector3(tam * 0.45, tam * 0.45, tam * 0.55));
  camera.near = tam / 2000; camera.far = tam * 20; camera.updateProjectionMatrix();
  controles.target.copy(centro); controles.update();
}

for (const [id, nome] of [['vTerreno', 'terreno'], ['vMesas', 'mesas'], ['vPilares', 'pilares'], ['vArvores', 'arvores'], ['vSombras', 'sombras'], ['vCanaletas', 'canaletas']]) {
  document.getElementById(id).onchange = e => { if (camadas[nome]) camadas[nome].visible = e.target.checked; if (sombraDoSol) sombraDoSol.refazer(); };
}
document.getElementById('exagero').onchange = e => { mundo.scale.set(1, Number(e.target.value), 1); enquadrar(); if (sombraDoSol) sombraDoSol.refazer(); };
document.getElementById('enquadrar').onclick = enquadrar;
const preencher = (modelo, ...valores) => modelo.replace(/\{(\d)\}/g, (_, i) => valores[Number(i)]);
document.getElementById('resumo').textContent =
  preencher({{TXT_RESUMO}}, D.faces.length, D.pilares.length, D.arvores.length) + (D.sombras.length ? preencher({{TXT_RESUMO_SOMBRAS}}, D.sombras.length) : '') + '.';

addEventListener('resize', () => { camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); render.setSize(innerWidth, innerHeight); });
enquadrar();
(function quadro() { requestAnimationFrame(quadro); if (sombraDoSol) sombraDoSol.quadro(); controles.update(); render.render(cena, camera); })();
</script>
</body>
</html>
""";
}
