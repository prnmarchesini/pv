using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>O projeto do TermoVision que este desenho criou: o GUID dele, o arquivo de onde saiu, a usina e o link público.</summary>
internal sealed record TermoVisionProjetoDoDesenho(Guid Guid, string Arquivo, string Usina, string Nome, string Link, string Expira);

/// <summary>
/// O "Exportar para o TermoVision" sem envio (contrato v2, 02/11/2026,
/// Renan: "se eu fizer pelo termovision, o cliente vai ver outro produto e
/// ficar curioso"): o desenho que não veio do TermoVision vira uma usina
/// pública de lá, com as mesas sobre o terreno e o satélite. As mesas, o
/// terreno em grade e o sistema de coordenadas vão ao servidor do Clivus,
/// com a licença; ele repassa ao TermoVision com a chave dele (que nunca vem
/// ao PC) e devolve o link. O GUID do desenho fica no dicionário do plugin:
/// mandar de novo atualiza a mesma usina e devolve o mesmo link.
/// </summary>
internal static class TermoVisionProjeto
{
    private const string CampoGuid = "GUID";
    private const string CampoArquivo = "ARQUIVO";
    private const string CampoUsina = "USINA";
    private const string CampoNome = "NOME";
    private const string CampoLink = "LINK";
    private const string CampoExpira = "EXPIRA";

    // Dez minutos: a grade de uma usina grande passa de alguns MB e a internet do canteiro é lenta.
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>O projeto guardado no desenho, ou null.</summary>
    internal static TermoVisionProjetoDoDesenho? Ler(Database database)
    {
        using var buffer = PluginDictionary.Load(database, PluginInfo.ChaveTermoVisionProjeto);
        if (buffer is null) return null;

        var valores = buffer.AsArray();
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var k = 0; k + 1 < valores.Length; k += 2)
            if (valores[k].Value is string nome && valores[k + 1].Value is string valor) campos[nome] = valor;

        if (!campos.TryGetValue(CampoGuid, out var texto) || !Guid.TryParse(texto, out var guid)) return null;
        return new TermoVisionProjetoDoDesenho(guid, campos.GetValueOrDefault(CampoArquivo, ""), campos.GetValueOrDefault(CampoUsina, ""),
            campos.GetValueOrDefault(CampoNome, ""), campos.GetValueOrDefault(CampoLink, ""), campos.GetValueOrDefault(CampoExpira, ""));
    }

    private static void Gravar(Database database, TermoVisionProjetoDoDesenho projeto)
    {
        var buffer = new ResultBuffer();
        void Campo(string nome, string valor)
        {
            buffer.Add(new TypedValue((int)DxfCode.Text, nome));
            buffer.Add(new TypedValue((int)DxfCode.Text, valor));
        }

        Campo(CampoGuid, projeto.Guid.ToString("D"));
        Campo(CampoArquivo, projeto.Arquivo);
        Campo(CampoUsina, projeto.Usina);
        Campo(CampoNome, projeto.Nome);
        Campo(CampoLink, projeto.Link);
        Campo(CampoExpira, projeto.Expira);
        PluginDictionary.Save(database, PluginInfo.ChaveTermoVisionProjeto, buffer);
    }

    /// <summary>O resultado de montar e mandar o projeto.</summary>
    internal sealed record Feito(int Mesas, int Recusadas, string Grade, string Sistema, Guid Guid, TermoVisionProjectResult? Resultado, string? Erro);

    /// <summary>
    /// Monta o projeto e, sem <paramref name="gravarEm"/>, manda; com ele,
    /// grava o corpo nesse arquivo, sem rede (o teste de nível 2).
    /// </summary>
    /// <param name="documento">O desenho (travado por quem chama: o GUID e o link são gravados nele).</param>
    /// <param name="interativo">Se pergunta (o "Salvar como": atualizar a mesma usina ou criar outra).</param>
    /// <param name="gravarEm">O arquivo do corpo, para o teste; null manda.</param>
    internal static Feito Criar(Document documento, bool interativo, string? gravarEm = null)
    {
        var editor = documento.Editor;
        var database = documento.Database;
        Feito Falha(string erro) => new(0, 0, "", "", Guid.Empty, null, erro);

        var terreno = TerrainCache.Get(documento) ?? FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return Falha(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));

        var lugar = SombrasCommands.Lugar(documento);
        if (lugar is not { IsValid: true })
            return Falha(Tr.T("O desenho não tem localização (latitude e longitude). Corrija em Terreno > Resumo > Localização."));

        var mesas = TermoVisionCommands.LerMesas(editor, database, out var recusadas, out var faltaCarport);
        if (faltaCarport > 0)
            return Falha(Tr.F("Nada foi mandado: {0} vão(s) de carport não puderam ser montados (o terreno do desenho não está processado). Use o botão Terreno e exporte de novo.", faltaCarport));
        if (mesas.Count == 0) return Falha(Tr.T("Não há mesa no desenho para mandar ao TermoVision."));

        // O desenho tem de estar em UTM, na zona da localização: senão a usina iria parar em outro lugar do mapa.
        var (codigo, _) = GeoStore.SistemaDoDesenho(database);
        var crs = TermoVisionProjectCrs.Of(lugar.Latitude, lugar.Longitude, drawingName: codigo);
        var centro = TerrenoResumoCommands.Centro(terreno);
        if (!crs.LooksLikeUtm(centro.X, centro.Y, lugar.Latitude, lugar.Longitude))
            return Falha(Tr.F("As coordenadas do desenho não parecem UTM na zona {0}{1} da localização ({2:0.0000}°, {3:0.0000}°): no TermoVision a usina iria parar em outro lugar. Confira o sistema de coordenadas do desenho e a localização em Terreno > Resumo.",
                crs.Zone, crs.South ? "S" : "N", lugar.Latitude, lugar.Longitude));

        // O terreno em grade, sobre as mesas com folga: 2 m até 2 km de lado.
        var cantos = mesas.SelectMany(m => m.Corners).ToList();
        var (x0, y0) = (cantos.Min(c => c.E) - TermoVisionProject.TerrainMargin, cantos.Min(c => c.N) - TermoVisionProject.TerrainMargin);
        var (x1, y1) = (cantos.Max(c => c.E) + TermoVisionProject.TerrainMargin, cantos.Max(c => c.N) + TermoVisionProject.TerrainMargin);
        var tin = terreno.Mesh;
        var grade = Viewer3DPage.SampleTerrain(x0, y0, x1, y1, (x, y) => tin.TryGetZ(x, y, out var z) ? z : null,
            TermoVisionProject.GridCells(Math.Max(x1 - x0, y1 - y0)));

        // O GUID: o do desenho; o "Salvar como" (outro arquivo) pergunta se é a mesma usina.
        var arquivo = database.Filename ?? "";
        var antes = Ler(database);
        var guid = antes?.Guid ?? Guid.NewGuid();
        if (antes is not null && interativo && !string.IsNullOrEmpty(antes.Arquivo)
            && !string.Equals(Path.GetFullPath(antes.Arquivo), Path.GetFullPath(string.IsNullOrEmpty(arquivo) ? antes.Arquivo : arquivo), StringComparison.OrdinalIgnoreCase)
            && !TermoVisionCommands.Confirmar(editor, Tr.F("Este desenho já criou a usina \"{0}\" no TermoVision a partir de {1}. Atualizar a mesma usina (Não: criar uma nova)?",
                antes.Nome, Path.GetFileName(antes.Arquivo))))
            guid = Guid.NewGuid();

        var nome = NomeDaUsina.Atual(documento);
        var corpo = TermoVisionProject.Build(ClivusCommands.VersaoDoPlugin(), TermoVisionCommands.NomeDoDesenho(documento), nome, guid, crs, grade, mesas, out var invalidas);
        recusadas.AddRange(invalidas);

        var descricaoDaGrade = FormattableString.Invariant($"{grade.Columns}x{grade.Rows} de {grade.Step:0.##} m");
        var sistema = crs.Epsg is { } epsg ? FormattableString.Invariant($"{crs.Zone}{(crs.South ? "S" : "N")} EPSG {epsg}") : FormattableString.Invariant($"{crs.Zone}{(crs.South ? "S" : "N")}");
        var enviadas = mesas.Count - invalidas.Count;

        if (gravarEm is not null)
        {
            File.WriteAllBytes(gravarEm, corpo);
            return new Feito(enviadas, recusadas.Count, descricaoDaGrade, sistema, guid, null, null);
        }

        editor.WriteMessage(Tr.F("\nTermoVision: mandando {0} mesa(s) e o terreno ({1})...\n", enviadas, descricaoDaGrade));
        var (resultado, erro) = Task.Run(() => Mandar(corpo)).GetAwaiter().GetResult();
        if (resultado is null) return new Feito(enviadas, recusadas.Count, descricaoDaGrade, sistema, guid, null, erro);

        Gravar(database, new TermoVisionProjetoDoDesenho(guid, arquivo, resultado.PlantCode ?? "", resultado.PlantName ?? nome, resultado.Url ?? "",
            resultado.ExpiresAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? ""));
        return new Feito(enviadas, recusadas.Count, descricaoDaGrade, sistema, guid, resultado, null);
    }

    /// <summary>Manda o corpo ao servidor do Clivus, com a licença. O resultado, ou o erro em português.</summary>
    private static async Task<(TermoVisionProjectResult? Resultado, string? Erro)> Mandar(byte[] corpo)
    {
        if (Publicador3D.Endereco is not { } endereco) return (null, Tr.T("o servidor do Clivus não está configurado"));
        if (Viewer3DPage.WhyServerUnsafe(endereco) is { } inseguro) return (null, inseguro);

        try
        {
            using var comprimido = new MemoryStream();
            using (var gzip = new GZipStream(comprimido, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(corpo);

            using var conteudo = new ByteArrayContent(comprimido.ToArray());
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            conteudo.Headers.ContentEncoding.Add("gzip");

            using var pedido = new HttpRequestMessage(HttpMethod.Post, endereco + "/api/v1/termovision/projetos") { Content = conteudo };
            var autorizacao = Publicador3D.Autorizacao();
            if (autorizacao is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue(autorizacao.Scheme, autorizacao.Parameter);

            // Um id por clique: o reenvio do mesmo clique não grava duas vezes lá.
            pedido.Headers.Add("X-Pedido", Guid.NewGuid().ToString("N"));
            pedido.Headers.AcceptLanguage.ParseAdd(Tr.AcceptLanguage);

            using var resposta = await Cliente.SendAsync(pedido).ConfigureAwait(false);
            var texto = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (resposta.IsSuccessStatusCode && TermoVisionProjectResult.TryParse(texto) is { } resultado) return (resultado, null);
            return (null, Tr.F("o servidor recusou ({0}): {1}", (int)resposta.StatusCode, Erro(texto) ?? texto[..Math.Min(texto.Length, 300)]));
        }
        catch (HttpRequestException erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao mandar o projeto ao TermoVision.", erro);
            return (null, Tr.F("não consegui falar com o servidor do Clivus em {0} ({1})", endereco, erro.Message));
        }
        catch (TaskCanceledException)
        {
            return (null, Tr.F("o servidor do Clivus em {0} não respondeu a tempo", endereco));
        }
    }

    // O erro do servidor do Clivus vem em {"erro": "..."}.
    private static string? Erro(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("erro", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : TermoVisionResult.Detail(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
