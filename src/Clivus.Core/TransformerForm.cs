using System.Globalization;

namespace Clivus.Core;

/// <summary>Os textos das caixas do formulário do trafo, como estão na tela.</summary>
public sealed record TransformerFormTexts(
    string Name,
    string Nickname,
    string InputVoltage,
    string OutputVoltage,
    string PowerKva,
    string KFactor,
    string ImpedancePercent,
    string Notes,
    string Width,
    string Length,
    string Height);

/// <summary>
/// O formulário do trafo (13.1): o que a janela mostra e como o texto
/// digitado vira o cadastro. Mora no Core para a janela e o nível 2
/// (CLIVUS_ELETRICA_AUTO Formulario) passarem pelo MESMO caminho de
/// conversão (reprovação de 05/10/2026: "não mostra a potência depois de
/// salvo").
/// <para>
/// Tensão e potência vivem na casa do milhar: ponto é milhar ("13.800" é treze
/// mil e oitocentos, <see cref="NumberInput.TryParseLarge"/>). Aceita a
/// unidade escrita depois do número ("13,8 kV", "2,5 MVA", "6,5 %"). Caixa
/// vazia num campo elétrico é 0 (o "não informado" do trafo em branco); medida
/// vazia ou zero é recusada.
/// </para>
/// </summary>
public static class TransformerForm
{
    /// <summary>Os textos que a janela mostra para o trafo, na cultura da tela e sem milhar (volta igual pela leitura).</summary>
    public static TransformerFormTexts Texts(Transformer t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new TransformerFormTexts(
            t.Name, t.Nickname,
            Grande(t.InputVoltage), Grande(t.OutputVoltage), Grande(t.PowerKva),
            Medida(t.KFactor), Medida(t.ImpedancePercent), t.Notes,
            Medida(t.Size.Width), Medida(t.Size.Length), Medida(t.Size.Height));
    }

    /// <summary>
    /// O trafo com os campos do formulário (o GUID e a UC vêm do original), ou
    /// null e o porquê dizendo qual campo não deu para ler. Não confere apelido
    /// repetido nem a UC: isso é do cadastro (<see cref="ElectricalSetup.SaveTransformer"/>).
    /// </summary>
    public static Transformer? Read(Transformer original, TransformerFormTexts texts, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(texts);

        problem = null;

        if (!Tensao(texts.InputVoltage, out var entrada)) problem = Nao(Tr.T("a tensão de entrada (V)"), texts.InputVoltage);
        else if (!Tensao(texts.OutputVoltage, out var saida)) problem = Nao(Tr.T("a tensão de saída (V)"), texts.OutputVoltage);
        else if (!Potencia(texts.PowerKva, out var kva)) problem = Nao(Tr.T("a potência (kVA)"), texts.PowerKva);
        else if (!Medida(texts.KFactor, string.Empty, out var k)) problem = Nao(Tr.T("o fator K"), texts.KFactor);
        else if (!Medida(texts.ImpedancePercent, "%", out var z)) problem = Nao(Tr.T("a impedância (%)"), texts.ImpedancePercent);
        else if (!Tamanho(texts.Width, out var w)) problem = Nao(Tr.T("a largura (m)"), texts.Width);
        else if (!Tamanho(texts.Length, out var l)) problem = Nao(Tr.T("o comprimento (m)"), texts.Length);
        else if (!Tamanho(texts.Height, out var h)) problem = Nao(Tr.T("a altura (m)"), texts.Height);
        else
        {
            return original with
            {
                Name = texts.Name ?? string.Empty,
                Nickname = texts.Nickname ?? string.Empty,
                InputVoltage = entrada,
                OutputVoltage = saida,
                PowerKva = kva,
                KFactor = k,
                ImpedancePercent = z,
                Notes = texts.Notes ?? string.Empty,
                Size = new EquipmentSize(w, l, h),
            };
        }

        return null;
    }

    private static string Nao(string campo, string? texto) =>
        Tr.F("não consigo ler {0}: \"{1}\"", campo, texto?.Trim() ?? string.Empty);

    /// <summary>
    /// Tensão e potência na caixa: duas casas, que é o que a leitura de
    /// milhar aceita como decimal em inglês ("2500.75"; com três casas seria
    /// milhar).
    /// </summary>
    private static string Grande(double v) => v.ToString("0.##", Tr.Culture);

    /// <summary>Fator K, impedância e medidas: quatro casas (5,875 % volta 5,875, não 5,88).</summary>
    private static string Medida(double v) => v.ToString("0.####", Tr.Culture);

    private static bool Tensao(string? texto, out double volts)
    {
        var (numero, fator) = Unidade(texto, ("kV", 1000), ("V", 1));
        return Ler(numero, fator, out volts);
    }

    private static bool Potencia(string? texto, out double kva)
    {
        var (numero, fator) = Unidade(texto, ("MVA", 1000), ("kVA", 1));
        return Ler(numero, fator, out kva);
    }

    /// <summary>
    /// Na unidade base (V, kVA) o ponto é milhar ("13.800"); em kV e MVA o
    /// número é pequeno e o ponto é decimal ("0.380 kV" são 380 V, "1.250 MVA"
    /// são 1250 kVA; lido como milhar, seria mil vezes mais, calado).
    /// </summary>
    private static bool Ler(string numero, double fator, out double valor)
    {
        valor = 0;
        if (numero.Length == 0) return fator == 1;

        var leu = fator == 1 ? NumberInput.TryParseLarge(numero, out var v) : NumberInput.TryParseMeasure(numero, out v);
        if (!leu || !double.IsFinite(v)) return false;

        valor = v * fator;
        return true;
    }

    private static bool Medida(string? texto, string unidade, out double valor)
    {
        var (numero, _) = unidade.Length == 0 ? (texto?.Trim() ?? string.Empty, 1.0) : Unidade(texto, (unidade, 1));
        valor = 0;
        if (numero.Length == 0) return true;
        return NumberInput.TryParseMeasure(numero, out valor) && double.IsFinite(valor);
    }

    private static bool Tamanho(string? texto, out double valor) =>
        NumberInput.TryParseMeasure(texto, out valor) && double.IsFinite(valor) && valor > 0;

    /// <summary>O número sem a unidade do fim (a primeira que casar, sem olhar maiúscula) e o fator dela.</summary>
    private static (string Numero, double Fator) Unidade(string? texto, params (string Sufixo, double Fator)[] unidades)
    {
        var limpo = texto?.Trim() ?? string.Empty;

        foreach (var (sufixo, fator) in unidades)
        {
            if (limpo.Length > sufixo.Length && limpo.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase)
                && !char.IsLetter(limpo[limpo.Length - sufixo.Length - 1]))
                return (limpo[..^sufixo.Length].TrimEnd(), fator);
        }

        return (limpo, 1);
    }
}
