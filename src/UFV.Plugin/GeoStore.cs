using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Lê e grava a localização geográfica do terreno.
///
/// O desenho pode já saber onde fica — se tiver sistema de coordenadas
/// geográficas definido, o AutoCAD guarda um ponto de referência em latitude e
/// longitude. Quando não tiver, o valor é perguntado ao usuário e gravado no
/// dicionário do plugin, para ninguém precisar responder duas vezes.
/// </summary>
internal static class GeoStore
{
    private const string Chave = "GEO";

    private const string CampoVersaoDoFormato = "FORMATO";
    private const string CampoLatitude = "LAT";
    private const string CampoLongitude = "LON";
    private const string CampoOrigem = "ORIGEM";

    private const int VersaoDoFormato = 1;

    /// <summary>
    /// A localização do terreno: a gravada pelo plugin, se houver; senão a do
    /// próprio desenho; senão null, e aí é caso de perguntar ao usuário.
    /// </summary>
    /// <param name="pontoDoTerreno">
    /// Um ponto de dentro do terreno, em coordenadas do desenho. É ele que é
    /// convertido — ver <see cref="FromDrawing"/>.
    /// </param>
    internal static GeoLocation? Read(Database database, Point3d pontoDoTerreno)
    {
        ArgumentNullException.ThrowIfNull(database);

        // O que o usuário confirmou vem antes do que o desenho diz: se ele
        // digitou, foi porque o desenho não sabia ou estava errado.
        var gravada = Load(database);
        if (gravada is not null) return gravada;

        return FromDrawing(database, pontoDoTerreno);
    }

    /// <summary>
    /// A localização de um ponto do terreno, segundo a geolocalização do
    /// desenho, ou null se ele não tiver nenhuma.
    /// </summary>
    /// <remarks>
    /// Converte o ponto, e não lê o ReferencePoint da geolocalização. A
    /// diferença apareceu num desenho real: o ponto de referência é um ponto
    /// de calibração arbitrário, que naquele arquivo estava a mil quilômetros
    /// do terreno — o plugin dizia 14° S quando a usina está a 23° S. Nove
    /// graus de latitude erram a posição do sol o bastante para estragar o
    /// azimute das mesas e toda a conta de sombreamento, e o número sai
    /// plausível.
    ///
    /// Quem converte é o próprio AutoCAD, que sabe o sistema de coordenadas
    /// declarado no desenho.
    /// </remarks>
    internal static GeoLocation? FromDrawing(Database database, Point3d pontoDoTerreno)
    {
        try
        {
            var id = database.GeoDataObject;
            if (id.IsNull) return null;

            using var transacao = database.TransactionManager.StartOpenCloseTransaction();

            if (transacao.GetObject(id, OpenMode.ForRead) is not GeoLocationData geo) return null;

            // Longitude em X, latitude em Y, altitude em Z — em graus.
            var geografico = geo.TransformToLonLatAlt(pontoDoTerreno);
            var lugar = new GeoLocation(geografico.Y, geografico.X, GeoLocationSource.Desenho);

            transacao.Commit();

            // Um desenho com o objeto de geolocalização presente mas zerado é
            // o mesmo que desenho sem geolocalização: melhor perguntar do que
            // colocar a usina no golfo da Guiné.
            return lugar.IsValid && !lugar.LooksUnset ? lugar : null;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a geolocalização do desenho.", erro);
            return null;
        }
    }

    /// <summary>Grava a localização no dicionário do plugin.</summary>
    internal static void Save(Database database, GeoLocation lugar)
    {
        ArgumentNullException.ThrowIfNull(lugar);

        var buffer = new ResultBuffer();

        void Campo(string nome, string valor)
        {
            buffer.Add(new TypedValue((int)DxfCode.Text, nome));
            buffer.Add(new TypedValue((int)DxfCode.Text, valor));
        }

        Campo(CampoVersaoDoFormato, VersaoDoFormato.ToString(CultureInfo.InvariantCulture));
        Campo(CampoLatitude, lugar.Latitude.ToString("R", CultureInfo.InvariantCulture));
        Campo(CampoLongitude, lugar.Longitude.ToString("R", CultureInfo.InvariantCulture));
        Campo(CampoOrigem, lugar.Source.ToString());

        PluginDictionary.Save(database, Chave, buffer);
    }

    /// <summary>
    /// A localização gravada pelo plugin, ou null. Diferente de
    /// <see cref="Read"/>, não cai para a do desenho: serve para mostrar ao
    /// usuário exatamente o que está guardado.
    /// </summary>
    internal static GeoLocation? Gravada(Database database) => Load(database);

    /// <summary>A localização gravada pelo plugin, ou null.</summary>
    private static GeoLocation? Load(Database database)
    {
        using var buffer = PluginDictionary.Load(database, Chave);
        if (buffer is null) return null;

        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var valores = buffer.AsArray();

        for (var i = 0; i + 1 < valores.Length; i += 2)
        {
            if (valores[i].Value is not string campo) continue;
            campos[campo] = valores[i + 1].Value as string ?? string.Empty;
        }

        if (!campos.TryGetValue(CampoVersaoDoFormato, out var formato)
            || !int.TryParse(formato, NumberStyles.Integer, CultureInfo.InvariantCulture, out var versao)
            || versao > VersaoDoFormato)
        {
            return null;
        }

        if (!Real(campos, CampoLatitude, out var latitude)) return null;
        if (!Real(campos, CampoLongitude, out var longitude)) return null;

        var origem = campos.TryGetValue(CampoOrigem, out var texto)
            && Enum.TryParse<GeoLocationSource>(texto, out var lida)
                ? lida
                : GeoLocationSource.Usuario;

        var lugar = new GeoLocation(latitude, longitude, origem);
        return lugar.IsValid ? lugar : null;
    }

    private static bool Real(IReadOnlyDictionary<string, string> campos, string campo, out double valor)
    {
        valor = 0;
        return campos.TryGetValue(campo, out var texto)
            && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }
}
