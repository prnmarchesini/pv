using System.Globalization;
using System.Text.RegularExpressions;

namespace UFV.Core;

/// <summary>
/// Lê o número que o projetista digitou.
///
/// A máquina está em português, então ele digita vírgula. Mas ele também cola
/// número de planilha e de PDF, que vêm com ponto. Aceitar só um dos dois faz
/// "2.384" virar 2384 ou não ser lido — e das duas, uma é pior que a outra.
///
/// O ponto é ambíguo e não há como desambiguá-lo sozinho: "1.500" é mil e
/// quinhentos ou um e meio? A resposta depende do campo, e por isso há dois
/// métodos em vez de um esperto. Numa medida de mesa, nada chega a mil metros,
/// então ponto é decimal. Numa potência, que vive na casa do milhar, ponto é
/// separador de milhar.
///
/// Mora no Core, e não junto da janela, porque é texto virando número — não
/// tem nada de CAD nem de interface. E porque é o pedaço mais sujeito a erro
/// silencioso do passo: sem teste, "1.500" no campo de potência vira 1,5 Wp e
/// a usina sai com a potência dividida por mil, sem uma linha de aviso.
/// </summary>
public static class NumberInput
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Milhar de verdade: grupos de exatamente três algarismos, com decimal
    /// opcional depois da vírgula.
    ///
    /// A conferência é feita aqui, e não com <c>NumberStyles.AllowThousands</c>,
    /// porque o .NET não confere o tamanho dos grupos: com ele, "1.2.3" vira
    /// 123 e — muito pior — "28.5" no campo de módulos vira 285. Duzentos e
    /// oitenta e cinco módulos é um número plausível, e o projetista digitou
    /// vinte e oito e meio.
    /// </summary>
    private static readonly Regex Milhar = new(
        @"^[+-]?\d{1,3}(\.\d{3})+(,\d+)?$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Lê uma medida em metro, onde o ponto é separador decimal.
    ///
    /// Vale para tudo que a mesa mede: altura de módulo, folga, sobra,
    /// tesoura. Nenhuma dessas grandezas chega a mil metros, então um ponto
    /// solto nunca é milhar.
    /// </summary>
    public static bool TryParseMeasure(string? texto, out double valor)
    {
        var limpo = texto?.Trim() ?? string.Empty;

        valor = 0;
        if (limpo.Length == 0) return false;

        // Um ponto só e nenhuma vírgula: numa medida, isso é decimal.
        if (!limpo.Contains(',') && limpo.Count(c => c == '.') == 1)
            return Ler(limpo.Replace('.', ','), out valor);

        return Ler(SemMilhar(limpo), out valor);
    }

    /// <summary>
    /// Lê uma grandeza que chega à casa do milhar, onde o ponto é separador de
    /// milhar: potência em watt-pico, contagem de módulos.
    ///
    /// Aqui "1.500" é mil e quinhentos, que é o que qualquer projetista quer
    /// dizer ao digitar isso num campo de watt.
    /// </summary>
    public static bool TryParseLarge(string? texto, out double valor) =>
        Ler(SemMilhar(texto?.Trim() ?? string.Empty), out valor);

    /// <summary>
    /// Lê uma contagem inteira, com a mesma regra de <see cref="TryParseLarge"/>.
    /// </summary>
    public static bool TryParseCount(string? texto, out int valor)
    {
        valor = 0;

        if (!TryParseLarge(texto, out var numero)) return false;
        if (numero != Math.Floor(numero)) return false;
        if (numero is < int.MinValue or > int.MaxValue) return false;

        valor = (int)numero;
        return true;
    }

    /// <summary>
    /// O texto sem os pontos de milhar, se eles forem milhar de verdade. Se
    /// não forem, o texto volta como veio e a leitura recusa — que é o certo:
    /// "1.2.3" não é número nenhum.
    /// </summary>
    private static string SemMilhar(string texto) =>
        Milhar.IsMatch(texto) ? texto.Replace(".", string.Empty) : texto;

    /// <summary>
    /// O número em português, sem tolerância a separador de milhar: quem cuida
    /// dele é <see cref="SemMilhar"/>, e com regra estrita.
    /// </summary>
    private static bool Ler(string texto, out double valor) =>
        double.TryParse(texto, NumberStyles.Float, Brasil, out valor);
}
