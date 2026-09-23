using System.Globalization;

namespace UFV.Core;

/// <summary>De onde veio a latitude e a longitude do terreno.</summary>
public enum GeoLocationSource
{
    /// <summary>Do sistema de coordenadas geográficas definido no desenho.</summary>
    Desenho,

    /// <summary>Digitada pelo usuário, porque o desenho não tinha.</summary>
    Usuario,
}

/// <summary>
/// Onde no mundo fica o terreno.
///
/// É pré-requisito para a posição do sol, e portanto para o azimute correto e
/// para qualquer conta de sombreamento adiante. Por isso o plugin pergunta
/// quando o desenho não sabe responder: seguir sem isso adiaria a descoberta
/// para a etapa em que o erro já estaria embutido no layout.
/// </summary>
/// <param name="Latitude">Graus, negativo no hemisfério sul.</param>
/// <param name="Longitude">Graus, negativo a oeste de Greenwich.</param>
/// <param name="Source">De onde o valor veio.</param>
public sealed record GeoLocation(double Latitude, double Longitude, GeoLocationSource Source)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Se o par está dentro dos limites do planeta e não é NaN.
    ///
    /// Zero grau é um ponto legítimo — no golfo da Guiné —, mas latitude e
    /// longitude exatamente zero juntas são, na prática, desenho sem
    /// geolocalização com valores por preencher. Isso é tratado por
    /// <see cref="LooksUnset"/>, e não aqui: o limite geográfico e a suspeita
    /// são coisas diferentes.
    /// </summary>
    public bool IsValid =>
        double.IsFinite(Latitude) && double.IsFinite(Longitude)
        && Latitude is >= -90 and <= 90
        && Longitude is >= -180 and <= 180;

    /// <summary>
    /// Verdadeiro quando o par é (0, 0) — que é o que um desenho sem
    /// geolocalização costuma devolver, e não um terreno no meio do Atlântico.
    /// </summary>
    public bool LooksUnset => Math.Abs(Latitude) < 1e-9 && Math.Abs(Longitude) < 1e-9;

    /// <summary>
    /// A latitude e a longitude como o usuário lê, com o hemisfério por
    /// extenso: um sinal trocado é o erro mais fácil de cometer e o mais
    /// difícil de notar num número solto.
    /// </summary>
    public string Describe()
    {
        var latitude = $"{Math.Abs(Latitude).ToString("N6", Brasil)}° {(Latitude < 0 ? "S" : "N")}";
        var longitude = $"{Math.Abs(Longitude).ToString("N6", Brasil)}° {(Longitude < 0 ? "O" : "L")}";

        return $"{latitude}, {longitude}";
    }
}
