namespace UFV.Core;

/// <summary>
/// A posição do sol num instante (9.5): azimute (do norte, sentido horário,
/// em graus) e elevação acima do horizonte (graus, com a refração
/// atmosférica do NOAA). Também o vetor unitário que aponta para o sol, em
/// (leste, norte, cima) — o desenho georreferenciado tem X a leste e Y ao
/// norte (UTM; a convergência de meridianos, de menos de 1° no Brasil, é
/// desprezada).
/// </summary>
public sealed record SunPosition(double AzimuthDegrees, double ElevationDegrees, double DeclinationDegrees, double EquationOfTimeMinutes)
{
    /// <summary>Se o sol está acima do horizonte.</summary>
    public bool IsUp => ElevationDegrees > 0;

    /// <summary>O vetor unitário que aponta para o sol: (leste, norte, cima).</summary>
    public (double X, double Y, double Z) Direction
    {
        get
        {
            var az = AzimuthDegrees * Math.PI / 180;
            var el = ElevationDegrees * Math.PI / 180;
            return (Math.Sin(az) * Math.Cos(el), Math.Cos(az) * Math.Cos(el), Math.Sin(el));
        }
    }
}

/// <summary>
/// O algoritmo do NOAA (Solar Calculator, as fórmulas da planilha "NOAA
/// Solar Calculations", de Jean Meeus, "Astronomical Algorithms"). Erro de
/// centésimos de grau entre 1800 e 2100, muito abaixo do que a sombra de uma
/// árvore precisa.
/// </summary>
public static class SolarCalculator
{
    /// <summary>A posição do sol.</summary>
    /// <param name="latitude">Graus, positivo ao norte.</param>
    /// <param name="longitude">Graus, positivo a leste (o Brasil é negativo).</param>
    /// <param name="localTime">A hora do relógio local (o Kind é ignorado).</param>
    /// <param name="utcOffsetHours">O fuso: −3 em Brasília.</param>
    public static SunPosition Compute(double latitude, double longitude, DateTime localTime, double utcOffsetHours)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (!double.IsFinite(utcOffsetHours) || utcOffsetHours is < -14 or > 14) throw new ArgumentOutOfRangeException(nameof(utcOffsetHours));

        static double Rad(double g) => g * Math.PI / 180;
        static double Grau(double r) => r * 180 / Math.PI;

        var utc = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified).AddHours(-utcOffsetHours);

        // Dia juliano (UTC) e século juliano desde J2000.
        var jd = DiaJuliano(utc);
        var t = (jd - 2451545.0) / 36525.0;

        var l0 = Mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
        var m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

        var c = Math.Sin(Rad(m)) * (1.914602 - t * (0.004817 + 0.000014 * t))
            + Math.Sin(Rad(2 * m)) * (0.019993 - 0.000101 * t)
            + Math.Sin(Rad(3 * m)) * 0.000289;

        var longitudeVerdadeira = l0 + c;
        var omega = 125.04 - 1934.136 * t;
        var longitudeAparente = longitudeVerdadeira - 0.00569 - 0.00478 * Math.Sin(Rad(omega));

        var obliquidadeMedia = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        var obliquidade = obliquidadeMedia + 0.00256 * Math.Cos(Rad(omega));

        var declinacao = Grau(Math.Asin(Math.Sin(Rad(obliquidade)) * Math.Sin(Rad(longitudeAparente))));

        var y = Math.Pow(Math.Tan(Rad(obliquidade / 2)), 2);
        var equacaoDoTempo = 4 * Grau(
            y * Math.Sin(2 * Rad(l0))
            - 2 * e * Math.Sin(Rad(m))
            + 4 * e * y * Math.Sin(Rad(m)) * Math.Cos(2 * Rad(l0))
            - 0.5 * y * y * Math.Sin(4 * Rad(l0))
            - 1.25 * e * e * Math.Sin(2 * Rad(m)));

        var minutosDoDia = localTime.TimeOfDay.TotalMinutes;
        var tempoSolarVerdadeiro = Mod(minutosDoDia + equacaoDoTempo + 4 * longitude - 60 * utcOffsetHours, 1440);
        var anguloHorario = tempoSolarVerdadeiro / 4 < 0 ? tempoSolarVerdadeiro / 4 + 180 : tempoSolarVerdadeiro / 4 - 180;

        var cosZenite = Math.Sin(Rad(latitude)) * Math.Sin(Rad(declinacao))
            + Math.Cos(Rad(latitude)) * Math.Cos(Rad(declinacao)) * Math.Cos(Rad(anguloHorario));
        var zenite = Grau(Math.Acos(Math.Clamp(cosZenite, -1, 1)));
        var elevacao = 90 - zenite;

        // Azimute, do norte, horário.
        double azimute;
        var denominador = Math.Cos(Rad(latitude)) * Math.Sin(Rad(zenite));

        if (Math.Abs(denominador) < 1e-12)
        {
            azimute = latitude > declinacao ? 180 : 0;
        }
        else
        {
            var cosAz = Math.Clamp((Math.Sin(Rad(latitude)) * Math.Cos(Rad(zenite)) - Math.Sin(Rad(declinacao))) / denominador, -1, 1);
            var a = Grau(Math.Acos(cosAz));
            azimute = anguloHorario > 0 ? Mod(a + 180, 360) : Mod(540 - a, 360);
        }

        return new SunPosition(azimute, elevacao + Refracao(elevacao), declinacao, equacaoDoTempo);
    }

    /// <summary>A refração atmosférica aproximada do NOAA, em graus.</summary>
    private static double Refracao(double elevacao)
    {
        if (elevacao > 85) return 0;

        var tanE = Math.Tan(elevacao * Math.PI / 180);
        double segundos;

        if (elevacao > 5) segundos = 58.1 / tanE - 0.07 / Math.Pow(tanE, 3) + 0.000086 / Math.Pow(tanE, 5);
        else if (elevacao > -0.575) segundos = 1735 + elevacao * (-518.2 + elevacao * (103.4 + elevacao * (-12.79 + elevacao * 0.711)));
        else segundos = -20.772 / tanE;

        return segundos / 3600;
    }

    private static double DiaJuliano(DateTime utc)
    {
        var ano = utc.Year;
        var mes = utc.Month;
        var dia = utc.Day + utc.TimeOfDay.TotalDays;

        if (mes <= 2)
        {
            ano--;
            mes += 12;
        }

        var a = ano / 100;
        var b = 2 - a + a / 4;

        return Math.Floor(365.25 * (ano + 4716)) + Math.Floor(30.6001 * (mes + 1)) + dia + b - 1524.5;
    }

    private static double Mod(double v, double m) => ((v % m) + m) % m;
}
