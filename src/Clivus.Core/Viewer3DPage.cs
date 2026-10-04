using System.Globalization;
using System.Reflection;
using System.Text;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma face de módulo na cena 3D: os quatro cantos e a cor (a do desenho).</summary>
public sealed record Scene3DFace(IReadOnlyList<Point3> Corners, RgbColor Color);

/// <summary>Um pilar na cena 3D: centro em planta, topo e pé (cotas).</summary>
public sealed record Scene3DPillar(double X, double Y, double Top, double Bottom);

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

/// <summary>A cena da página 3D.</summary>
public sealed record Scene3D(
    string Title,
    Scene3DTerrain? Terrain,
    IReadOnlyList<Scene3DFace> Faces,
    IReadOnlyList<Scene3DPillar> Pillars,
    IReadOnlyList<Scene3DTree> Trees,
    IReadOnlyList<IReadOnlyList<Point3>> Shadows);

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

    /// <summary>A cena em JSON, relativa à origem, com milímetro.</summary>
    public static string Json(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var o = Origin(scene);
        var s = new StringBuilder();

        string N(double v) => Math.Round(v, 3).ToString("0.###", Inv);
        string X(double x) => N(x - o.X);
        string Y(double y) => N(y - o.Y);
        string Z(double z) => N(z - o.Z);

        s.Append("{\"titulo\":").Append(Texto(scene.Title));
        s.Append(",\"origem\":[").Append(N(o.X)).Append(',').Append(N(o.Y)).Append(',').Append(N(o.Z)).Append(']');

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

        s.Append(",\"pilares\":[");
        s.Append(string.Join(',', scene.Pillars.Select(p => "[" + X(p.X) + "," + Y(p.Y) + "," + Z(p.Top) + "," + Z(p.Bottom) + "]")));
        s.Append(']');

        s.Append(",\"arvores\":[");
        s.Append(string.Join(',', scene.Trees.Select(a =>
            "[" + X(a.X) + "," + Y(a.Y) + "," + Z(a.Ground) + "," + N(a.Spec.TrunkHeight) + "," + N(a.Spec.TrunkWidth) + "," + N(a.Spec.CrownHeight) + "," + N(a.Spec.CrownWidth) + "]")));
        s.Append(']');

        s.Append(",\"sombras\":[");
        s.Append(string.Join(',', scene.Shadows.Select(c => "[" + string.Join(',', c.Select(p => X(p.X) + "," + Y(p.Y) + "," + Z(p.Z))) + "]")));
        s.Append("]}");

        return s.ToString();
    }

    /// <summary>
    /// O corpo do envio ao servidor 3D (plano/contrato-servidor-3d.md): versão
    /// do contrato, versão do plugin, nome do desenho e a cena.
    /// </summary>
    public static string PublishBody(Scene3D scene, string pluginVersion, string drawing)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return "{\"versao\":1,\"plugin\":" + Texto(pluginVersion ?? string.Empty)
            + ",\"desenho\":" + Texto(drawing ?? string.Empty)
            + ",\"cena\":" + Json(scene) + "}";
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

            return (null, "o servidor respondeu sem o link da página 3D");
        }

        if (temJson && raiz.TryGetProperty("erro", out var erro) && erro.ValueKind == System.Text.Json.JsonValueKind.String)
            return (null, erro.GetString());

        return (null, status switch
        {
            401 or 403 => "a chave do servidor não foi aceita (confira CLIVUS_SERVIDOR_CHAVE)",
            413 => "a usina é grande demais para o servidor",
            _ => $"o servidor respondeu {status}",
        });
    }

    /// <summary>
    /// Se o endereço do servidor serve (plano/seguranca.md): HTTPS; http só em
    /// localhost (o servidor falso dos testes). Null se serve, o porquê se não.
    /// </summary>
    public static string? WhyServerUnsafe(string? endereco)
    {
        if (!Uri.TryCreate(endereco, UriKind.Absolute, out var uri)) return "o endereço do servidor não é um endereço web";
        if (uri.Scheme == Uri.UriSchemeHttps) return null;
        if (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) return null;
        return "o servidor precisa ser HTTPS (http só em localhost, para teste)";
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

    /// <summary>A página inteira: a three.js, os controles de órbita, a cena e o visualizador.</summary>
    public static string Html(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var three = Recurso("Clivus.Core.three.min.js");
        var orbita = Recurso("Clivus.Core.OrbitControls.js");

        // O título por último: um nome de desenho com "{{ORBITA}}" não pode
        // virar o lugar onde o script entra.
        return Modelo
            .Replace("{{THREE}}", three, StringComparison.Ordinal)
            .Replace("{{ORBITA}}", orbita, StringComparison.Ordinal)
            .Replace("{{CENA}}", Json(scene).Replace("</", "<\\/", StringComparison.Ordinal), StringComparison.Ordinal)
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
    /// ver o relevo.
    /// </summary>
    private const string Modelo = """
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{TITULO}} — 3D</title>
<style>
  :root { --painel: rgba(255,255,255,.92); --texto: #1d2733; --borda: #c9d2dc; }
  html, body { margin: 0; height: 100%; overflow: hidden; background: #cfe3f3; font: 13px/1.4 "Segoe UI", system-ui, sans-serif; color: var(--texto); }
  #painel { position: absolute; top: 12px; left: 12px; background: var(--painel); border: 1px solid var(--borda); border-radius: 8px; padding: 10px 12px; max-width: 300px; box-shadow: 0 2px 8px rgba(0,0,0,.12); }
  #painel h1 { font-size: 14px; margin: 0 0 6px; }
  #painel label { display: inline-flex; align-items: center; gap: 4px; margin: 2px 10px 2px 0; cursor: pointer; }
  #painel .dica { color: #5b6b7c; margin-top: 6px; font-size: 12px; }
  #painel select { font: inherit; }
</style>
</head>
<body>
<div id="painel">
  <h1>{{TITULO}}</h1>
  <div>
    <label><input type="checkbox" id="vTerreno" checked> Terreno</label>
    <label><input type="checkbox" id="vMesas" checked> Mesas</label>
    <label><input type="checkbox" id="vPilares" checked> Pilares</label>
    <label><input type="checkbox" id="vArvores" checked> Árvores</label>
    <label><input type="checkbox" id="vSombras" checked> Sombras</label>
  </div>
  <div>Exagero vertical <select id="exagero"><option value="1">1×</option><option value="2">2×</option><option value="3">3×</option><option value="5">5×</option></select>
    <button id="enquadrar">Enquadrar</button></div>
  <div class="dica" id="resumo"></div>
  <div class="dica">Botão esquerdo gira, o direito arrasta, a roda dá zoom.</div>
</div>
<script>{{THREE}}</script>
<script>{{ORBITA}}</script>
<script>
const D = {{CENA}};
const cena = new THREE.Scene();
cena.background = new THREE.Color(0xcfe3f3);
const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.5, 50000);
const render = new THREE.WebGLRenderer({ antialias: true });
render.setPixelRatio(devicePixelRatio);
render.setSize(innerWidth, innerHeight);
document.body.appendChild(render.domElement);
cena.add(new THREE.HemisphereLight(0xffffff, 0x8a7a60, 0.75));
const sol = new THREE.DirectionalLight(0xffffff, 0.65);
sol.position.set(-300, 600, -200);
cena.add(sol);

// Desenho (x leste, y norte, z cima) -> three (x, z, -y).
const mundo = new THREE.Group();
cena.add(mundo);
const P = (x, y, z) => [x, z, -y];
const camadas = {};
function camada(nome) { const g = new THREE.Group(); camadas[nome] = g; mundo.add(g); return g; }

// Terreno: grade com cor pela cota.
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
    idx.push(a, c, b, a, d, c);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(cor, 3));
  g.setIndex(idx); g.computeVertexNormals();
  camada('terreno').add(new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide })));
}

// Módulos: dois triângulos por face, com a cor do desenho.
{
  const pos = [], cor = [];
  for (const f of D.faces) {
    const v = [0, 1, 2, 3].map(k => P(f[3 * k], f[3 * k + 1], f[3 * k + 2]));
    const c = new THREE.Color(f[12]);
    for (const k of [0, 1, 2, 0, 2, 3]) { pos.push(...v[k]); cor.push(c.r, c.g, c.b); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(cor, 3));
  g.computeVertexNormals();
  const malha = new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -1 }));
  const bordas = new THREE.LineSegments(new THREE.EdgesGeometry(g), new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.35 }));
  const m = camada('mesas'); m.add(malha); m.add(bordas);
}

// Pilares: um traço do pé ao topo.
{
  const pos = [];
  for (const p of D.pilares) { pos.push(...P(p[0], p[1], p[3]), ...P(p[0], p[1], p[2])); }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  camada('pilares').add(new THREE.LineSegments(g, new THREE.LineBasicMaterial({ color: 0x55606b })));
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

const controles = new THREE.OrbitControls(camera, render.domElement);
controles.enableDamping = true;

// Enquadra a usina (mesas e árvores); sem elas, o terreno inteiro.
function enquadrar() {
  const caixa = new THREE.Box3();
  for (const nome of ['mesas', 'arvores']) if (camadas[nome] && camadas[nome].visible) caixa.expandByObject(camadas[nome]);
  if (caixa.isEmpty()) caixa.setFromObject(mundo);
  if (caixa.isEmpty()) return;
  const centro = caixa.getCenter(new THREE.Vector3()), tam = caixa.getSize(new THREE.Vector3()).length();
  camera.position.copy(centro).add(new THREE.Vector3(tam * 0.45, tam * 0.45, tam * 0.55));
  camera.near = tam / 2000; camera.far = tam * 20; camera.updateProjectionMatrix();
  controles.target.copy(centro); controles.update();
}

for (const [id, nome] of [['vTerreno', 'terreno'], ['vMesas', 'mesas'], ['vPilares', 'pilares'], ['vArvores', 'arvores'], ['vSombras', 'sombras']]) {
  document.getElementById(id).onchange = e => { if (camadas[nome]) camadas[nome].visible = e.target.checked; };
}
document.getElementById('exagero').onchange = e => { mundo.scale.set(1, Number(e.target.value), 1); enquadrar(); };
document.getElementById('enquadrar').onclick = enquadrar;
document.getElementById('resumo').textContent =
  `${D.faces.length} módulos, ${D.pilares.length} pilares, ${D.arvores.length} árvore(s)` + (D.sombras.length ? `, ${D.sombras.length} contorno(s) de sombra` : '') + '.';

addEventListener('resize', () => { camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); render.setSize(innerWidth, innerHeight); });
enquadrar();
(function quadro() { requestAnimationFrame(quadro); controles.update(); render.render(cena, camera); })();
</script>
</body>
</html>
""";
}
