using System.Globalization;

namespace UFV.Core;

/// <summary>
/// Um módulo fotovoltaico, com as medidas do datasheet.
///
/// Tudo em metro, como o resto do plugin. O módulo é a origem de quase todo
/// número que vem depois — comprimento da mesa, posição dos pilares, altura
/// livre —, e um milímetro errado aqui vira meio metro no fim de uma mesa de 28
/// módulos. Ninguém vai desconfiar do módulo: vão desconfiar da estrutura.
/// </summary>
/// <param name="Brand">Marca, como aparece no datasheet.</param>
/// <param name="Model">
/// Modelo. É ele que identifica o módulo na biblioteca, e não a potência: a
/// mesma marca vende 720 W em mais de um tamanho.
/// </param>
/// <param name="PowerWatts">Potência nominal, em watt-pico.</param>
/// <param name="Height">
/// A medida maior, ao longo da inclinação da mesa. No datasheet do Risen
/// RSM132-8-720BHDG são 2384 mm.
/// </param>
/// <param name="Width">
/// A medida menor, ao longo do comprimento da mesa. É ela que se repete a cada
/// módulo e que manda no comprimento total. 1303 mm no mesmo datasheet.
/// </param>
/// <param name="Thickness">Espessura do módulo com o quadro. 33 mm no mesmo.</param>
public sealed record SolarModule(
    string Brand,
    string Model,
    double PowerWatts,
    double Height,
    double Width,
    double Thickness)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Maior medida aceita, em metro.
    ///
    /// Não existe módulo de três metros, e quem digitar 2384 em vez de 2,384
    /// precisa descobrir isso agora, e não quando a mesa sair com dois
    /// quilômetros de comprimento.
    /// </summary>
    private const double MaiorMedida = 3.0;

    /// <summary>
    /// Menor medida aceita, em metro.
    ///
    /// O piso existe pelo mesmo motivo do teto, na direção contrária: quem
    /// digitar 2,384 num campo que espera milímetro, ou dividir por mil duas
    /// vezes, sai com um módulo de dois milímetros que passa em toda validação
    /// de sinal e produz uma mesa de cinco centímetros.
    /// </summary>
    private const double MenorMedida = 0.005;

    /// <summary>
    /// Maior potência aceita, em watt-pico.
    ///
    /// Os módulos de hoje estão na casa dos 700 W e a curva não dobra de um
    /// ano para o outro. O limite não é físico: é para pegar quem digitou a
    /// potência da string, ou do inversor, no campo do módulo.
    /// </summary>
    private const double MaiorPotencia = 2000;

    /// <summary>
    /// Menor potência aceita, em watt-pico.
    ///
    /// Pega quem digitou em quilowatt: 0,72 no lugar de 720.
    /// </summary>
    private const double MenorPotencia = 1;

    /// <summary>Se as medidas e os nomes fazem sentido.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Model)
        && double.IsFinite(PowerWatts)
        && PowerWatts >= MenorPotencia && PowerWatts <= MaiorPotencia
        && Medida(Height) && Medida(Width) && Medida(Thickness)
        && Thickness < Width;

    /// <summary>
    /// Um aviso, em português, se altura e largura parecem trocadas — ou null
    /// se estão na ordem esperada.
    ///
    /// O módulo é mais alto que largo. Trocar os dois campos é o erro de
    /// digitação mais fácil de cometer e o mais difícil de enxergar: os dois
    /// números são plausíveis, a validação passa, e a mesa sai com pouco mais
    /// da metade do comprimento certo.
    /// </summary>
    public string? LooksSwapped =>
        IsValid && Height < Width
            ? $"o módulo {DisplayName} está mais largo ({Width.ToString("0.###", Brasil)} m) "
              + $"que alto ({Height.ToString("0.###", Brasil)} m); confira se os dois campos "
              + "não foram trocados"
            : null;

    /// <summary>Marca e modelo juntos, do jeito que o usuário reconhece.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Brand) ? Model.Trim() : $"{Brand.Trim()} {Model.Trim()}";

    /// <summary>A linha que descreve o módulo para o usuário.</summary>
    public string Describe() =>
        $"{DisplayName} — {PowerWatts.ToString("0.#", Brasil)} Wp, "
        + $"{Height.ToString("0.###", Brasil)} × {Width.ToString("0.###", Brasil)} × "
        + $"{Thickness.ToString("0.###", Brasil)} m";

    private static bool Medida(double valor) =>
        double.IsFinite(valor) && valor >= MenorMedida && valor <= MaiorMedida;
}
