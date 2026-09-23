using System.Globalization;

namespace UFV.Core;

/// <summary>
/// A estrutura que segura os módulos: a tesoura e o pilar.
///
/// Os nomes seguem o desenho cotado do Renan (23/09/2026):
/// <list type="bullet">
/// <item><description><b>T1</b> — o comprimento da tesoura;</description></item>
/// <item><description><b>T2</b> — onde o pilar encosta nela, medido da ponta
/// baixa da própria tesoura.</description></item>
/// </list>
///
/// A tesoura é mais curta que os módulos e fica <b>centrada</b> neles — foi o
/// que ele confirmou no segundo desenho, com o eixo. É dessa diferença que sai
/// a sobra que entra na conta da altura do pilar, e é por isso que T2 sozinho
/// não basta: ele não parte do mesmo lugar que o M1.
/// </summary>
/// <param name="RafterLength">T1: o comprimento da tesoura, em metro.</param>
/// <param name="PillarAlongRafter">
/// T2: a posição do pilar ao longo da tesoura, contada da ponta baixa dela.
/// </param>
/// <param name="PillarWidth">Um lado da seção do pilar, ao longo da mesa.</param>
/// <param name="PillarDepth">O outro lado, na direção da inclinação.</param>
public sealed record TableFrame(
    double RafterLength,
    double PillarAlongRafter,
    double PillarWidth,
    double PillarDepth)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Maior medida aceita, em metro. Rede para erro de escala, como nas
    /// outras classes: quem digitou 300 achando que o campo era em centímetro.
    /// </summary>
    private const double MaiorMedida = 20.0;

    /// <summary>Se a estrutura fecha. Quando não fecha, <see cref="WhyInvalid"/> diz por quê.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de a estrutura não fechar, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (!Medida(RafterLength)) return "o comprimento da tesoura não é uma medida válida";
            if (!Medida(PillarWidth)) return "a largura do pilar não é uma medida válida";
            if (!Medida(PillarDepth)) return "a profundidade do pilar não é uma medida válida";

            if (!double.IsFinite(PillarAlongRafter) || PillarAlongRafter < 0)
                return "a posição do pilar na tesoura não é uma distância válida";

            if (PillarAlongRafter > RafterLength)
            {
                // Pilar fora da tesoura é peça pendurada no ar. Passaria em
                // qualquer conferência de sinal.
                return $"o pilar está a {Texto(PillarAlongRafter)} m de uma tesoura de "
                    + $"{Texto(RafterLength)} m, ou seja, fora dela";
            }

            return null;
        }
    }

    /// <summary>A linha que descreve a estrutura para o usuário.</summary>
    public string Describe() =>
        WhyInvalid is { } motivo
            ? $"Estrutura inválida: {motivo}."
            : $"tesoura de {Texto(RafterLength)} m, pilar a {Texto(PillarAlongRafter)} m dela, "
              + $"seção {Texto(PillarWidth)} × {Texto(PillarDepth)} m";

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);

    private static bool Medida(double valor) =>
        double.IsFinite(valor) && valor > 0 && valor <= MaiorMedida;
}
