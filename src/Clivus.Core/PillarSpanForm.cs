using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// A conta por trás da janela de vãos do passo 8.2 (Melhorias.docx,
/// 01/10/2026): os vãos par a par, P1-P2, P2-P3..., a soma, e se ela bate
/// com o que os pilares precisam cobrir.
///
/// Mora no Core para ser testada; a janela só mostra.
/// </summary>
public static class PillarSpanForm
{
    private static CultureInfo Cultura => Tr.Culture;

    /// <summary>Um milímetro, a mesma tolerância com que a estrutura fecha.</summary>
    private const double Tolerancia = 0.001;

    /// <summary>Mais pilares que isso numa mesa é erro de digitação.</summary>
    private const int MaisPilares = 1001;

    /// <summary>O nome do vão de índice <paramref name="indice"/>: P1-P2, P2-P3...</summary>
    public static string Label(int indice) => $"P{indice + 1}-P{indice + 2}";

    /// <summary>
    /// Vãos iguais para <paramref name="pilares"/> pilares cobrirem
    /// <paramref name="cobrir"/> metros, arredondados ao milímetro (o que se
    /// digita); o último leva o resto, para a soma fechar exata.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se não houver ao menos dois pilares, ou se não houver o que cobrir.
    /// </exception>
    public static IReadOnlyList<double> Equal(double cobrir, int pilares)
    {
        if (!double.IsFinite(cobrir) || cobrir <= 0)
            throw new ArgumentOutOfRangeException(nameof(cobrir), cobrir, Tr.T("Não há comprimento para os pilares cobrirem."));

        if (pilares < 2 || pilares > MaisPilares)
            throw new ArgumentOutOfRangeException(nameof(pilares), pilares, Tr.F("A mesa precisa de 2 a {0} pilares.", MaisPilares));

        // Em milímetros inteiros: o resto da divisão vai 1 mm para cada um dos
        // últimos vãos. Arredondar o vão e jogar a diferença toda no último
        // acumulava meio milímetro por vão, e com 400 pilares o último saía
        // negativo (achado da revisão do 8.2).
        var quantos = pilares - 1;
        var total = (long)Math.Round(cobrir * 1000, MidpointRounding.AwayFromZero);
        var basico = total / quantos;
        var resto = total % quantos;

        if (basico <= 0)
            throw new ArgumentOutOfRangeException(nameof(pilares), pilares, Tr.T("Pilares demais para o comprimento: o vão ficaria menor que 1 mm."));

        var vaos = new double[quantos];

        for (var i = 0; i < quantos; i++)
            vaos[i] = (basico + (i >= quantos - resto ? 1 : 0)) / 1000.0;

        return vaos;
    }

    /// <summary>
    /// Lê os vãos digitados, um por par de pilares. O motivo da recusa
    /// nomeia o par (P2-P3), não a posição na lista.
    /// </summary>
    public static bool TryRead(IReadOnlyList<string> textos, out IReadOnlyList<double> vaos, out string motivo)
    {
        ArgumentNullException.ThrowIfNull(textos);

        vaos = [];

        if (textos.Count == 0)
        {
            motivo = Tr.T("a mesa precisa de pelo menos um vão.");
            return false;
        }

        var lidos = new double[textos.Count];

        for (var i = 0; i < textos.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(textos[i]))
            {
                motivo = Tr.F("o vão {0} está em branco.", Label(i));
                return false;
            }

            if (!NumberInput.TryParseMeasure(textos[i], out var valor) || !double.IsFinite(valor) || valor <= 0)
            {
                motivo = Tr.F("o vão {0} não é uma distância válida.", Label(i));
                return false;
            }

            lidos[i] = valor;
        }

        vaos = lidos;
        motivo = string.Empty;
        return true;
    }

    /// <summary>Se a soma fecha com o que os pilares precisam cobrir, a 1 mm.</summary>
    public static bool Closes(double soma, double cobrir) => Math.Abs(soma - cobrir) <= Tolerancia;

    /// <summary>
    /// A frase da soma: "Soma 18,702 m de 18,702 m: fecha." ou com quanto
    /// falta ou sobra.
    /// </summary>
    public static string Summary(double soma, double cobrir)
    {
        if (Closes(soma, cobrir)) return Tr.F("Soma {0} m de {1} m: fecha.", Texto(soma), Texto(cobrir));

        var diferenca = soma - cobrir;

        return diferenca > 0
            ? Tr.F("Soma {0} m de {1} m: sobra {2} m.", Texto(soma), Texto(cobrir), Texto(Math.Abs(diferenca)))
            : Tr.F("Soma {0} m de {1} m: falta {2} m.", Texto(soma), Texto(cobrir), Texto(Math.Abs(diferenca)));
    }

    private static string Texto(double valor) => valor.ToString("0.###", Cultura);
}
