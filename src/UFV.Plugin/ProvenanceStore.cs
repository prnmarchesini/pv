using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Grava e lê o carimbo de proveniência dentro do desenho.
///
/// O carimbo vive num dicionário nomeado do próprio DWG, sob
/// <see cref="PluginInfo.PrefixoDeDados"/>. É o "dicionário nomeado central"
/// de 02-arquitetura.md: um lugar só, com nome próprio onde mais ninguém
/// escreve, que sobrevive ao fechar e reabrir o arquivo.
///
/// A superfície é referida pelo handle, e não pelo nome nem pela layer. Handle
/// nasce com a entidade e vive com ela; nome e layer o usuário muda quando
/// quiser, e a ferramenta perderia o rastro (02-arquitetura.md: "layer nunca é
/// fonte de verdade").
/// </summary>
internal static class ProvenanceStore
{
    private const string ChaveDoTerreno = "TERRENO";

    // Os nomes ficam gravados no arquivo do usuário: mudar qualquer um destes
    // torna ilegível o carimbo dos desenhos já processados.
    private const string CampoVersaoDoFormato = "FORMATO";
    private const string CampoHandle = "HANDLE";
    private const string CampoNome = "NOME";
    private const string CampoRevisao = "REVISAO";
    private const string CampoPontos = "PONTOS";
    private const string CampoTriangulos = "TRIANGULOS";
    private const string CampoCotaMinima = "COTAMIN";
    private const string CampoCotaMaxima = "COTAMAX";
    private const string CampoCaixaMinX = "CAIXAMINX";
    private const string CampoCaixaMinY = "CAIXAMINY";
    private const string CampoCaixaMaxX = "CAIXAMAXX";
    private const string CampoCaixaMaxY = "CAIXAMAXY";
    private const string CampoProcessadoEm = "PROCESSADOEM";
    private const string CampoVersaoDoPlugin = "VERSAO";

    /// <summary>
    /// Versão do formato do carimbo. Um carimbo de versão diferente — mais
    /// nova ou mais velha — é ignorado em vez de lido pela metade: melhor
    /// dizer "não há carimbo" e pedir para reprocessar do que entender errado
    /// o que está escrito.
    ///
    /// A 2 acrescentou a caixa envolvente em planta. Um carimbo da 1 não a
    /// tem, e lê-lo com zeros faria toda superfície parecer "movida".
    /// </summary>
    private const int VersaoDoFormato = 2;

    /// <summary>Grava o carimbo, substituindo o anterior.</summary>
    internal static void Save(Database database, ProvenanceStamp carimbo)
    {
        ArgumentNullException.ThrowIfNull(carimbo);

        PluginDictionary.Save(database, ChaveDoTerreno, Escrever(carimbo));
    }

    /// <summary>
    /// O carimbo gravado, ou null se este desenho nunca teve terreno
    /// processado — ou se o que está lá não dá para ler.
    /// </summary>
    internal static ProvenanceStamp? Load(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);

        // Carimbo ilegível é tratado como ausente, aqui e no dicionário: a
        // alternativa seria impedir o usuário de trabalhar por causa de um
        // dado auxiliar.
        using var dados = PluginDictionary.Load(database, ChaveDoTerreno);
        return Ler(dados);
    }

    private static ResultBuffer Escrever(ProvenanceStamp carimbo)
    {
        var s = carimbo.Surface;

        // Pares nome/valor: cada campo é um texto de rótulo seguido do valor.
        // Assim um campo novo no futuro não desloca os anteriores, e um campo
        // que desapareça não faz o resto ser lido fora de ordem.
        var buffer = new ResultBuffer();

        Acrescentar(buffer, CampoVersaoDoFormato, VersaoDoFormato.ToString(CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoHandle, s.Handle);
        Acrescentar(buffer, CampoNome, s.Name);
        Acrescentar(buffer, CampoRevisao, s.RevisionNumber.ToString(CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoPontos, s.PointCount.ToString(CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoTriangulos, s.TriangleCount.ToString(CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCotaMinima, s.MinZ.ToString("R", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCotaMaxima, s.MaxZ.ToString("R", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCaixaMinX, s.MinX.ToString("R", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCaixaMinY, s.MinY.ToString("R", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCaixaMaxX, s.MaxX.ToString("R", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoCaixaMaxY, s.MaxY.ToString("R", CultureInfo.InvariantCulture));

        // Formato redondo e invariante: quem lê pode estar noutra máquina, com
        // outra cultura, e a data precisa significar o mesmo instante.
        Acrescentar(buffer, CampoProcessadoEm, carimbo.ProcessedAt.ToString("O", CultureInfo.InvariantCulture));
        Acrescentar(buffer, CampoVersaoDoPlugin, carimbo.PluginVersion);

        return buffer;
    }

    private static void Acrescentar(ResultBuffer buffer, string campo, string valor)
    {
        buffer.Add(new TypedValue((int)DxfCode.Text, campo));
        buffer.Add(new TypedValue((int)DxfCode.Text, valor ?? string.Empty));
    }

    private static ProvenanceStamp? Ler(ResultBuffer? buffer)
    {
        if (buffer is null) return null;

        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var valores = buffer.AsArray();

        for (var i = 0; i + 1 < valores.Length; i += 2)
        {
            if (valores[i].Value is not string campo) continue;
            campos[campo] = valores[i + 1].Value as string ?? string.Empty;
        }

        if (!Inteiro(campos, CampoVersaoDoFormato, out var formato) || formato != VersaoDoFormato)
        {
            // Versão diferente da que sabemos ler. Tratar como ausente faz o
            // usuário reprocessar, que é barato; adivinhar o que falta faria
            // o carimbo mentir.
            return null;
        }

        if (!campos.TryGetValue(CampoHandle, out var handle) || string.IsNullOrWhiteSpace(handle))
            return null;

        if (!Inteiro(campos, CampoRevisao, out var revisao)) return null;
        if (!Inteiro(campos, CampoPontos, out var pontos)) return null;
        if (!Inteiro(campos, CampoTriangulos, out var triangulos)) return null;
        if (!Real(campos, CampoCotaMinima, out var minZ)) return null;
        if (!Real(campos, CampoCotaMaxima, out var maxZ)) return null;
        if (!Real(campos, CampoCaixaMinX, out var minX)) return null;
        if (!Real(campos, CampoCaixaMinY, out var minY)) return null;
        if (!Real(campos, CampoCaixaMaxX, out var maxX)) return null;
        if (!Real(campos, CampoCaixaMaxY, out var maxY)) return null;

        if (!campos.TryGetValue(CampoProcessadoEm, out var quando)
            || !DateTime.TryParse(
                quando,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var processadoEm))
        {
            return null;
        }

        campos.TryGetValue(CampoNome, out var nome);
        campos.TryGetValue(CampoVersaoDoPlugin, out var versao);

        return new ProvenanceStamp(
            new SurfaceFingerprint(
                handle, nome ?? string.Empty, revisao, pontos, triangulos,
                minZ, maxZ, minX, minY, maxX, maxY),
            processadoEm,
            versao ?? PluginInfo.VersaoDesconhecida);
    }

    private static bool Inteiro(IReadOnlyDictionary<string, string> campos, string campo, out int valor)
    {
        valor = 0;
        return campos.TryGetValue(campo, out var texto)
            && int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out valor);
    }

    private static bool Real(IReadOnlyDictionary<string, string> campos, string campo, out double valor)
    {
        valor = 0;
        return campos.TryGetValue(campo, out var texto)
            && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }
}
