using System.Globalization;

namespace UFV.Core.Invariants;

/// <summary>
/// Regra sagrada 1: o pilar nunca flutua.
///
/// "Todo pilar tem parte abaixo da superfície (embutimento &gt; 0) e parte acima
/// (altura livre &gt; 0). Verificador: <c>embutimento &gt; 0 &amp;&amp; alturaLivre &gt; 0</c>
/// para todo pilar."
///
/// São dois defeitos opostos, e os dois passam por qualquer conferência de
/// sinal isolada. Altura livre zero ou negativa é mesa pousada no chão — ou
/// enterrada nele —, com os módulos raspando o terreno. Embutimento zero é
/// pilar apoiado na superfície, que nem pilar é: vira na primeira ventania.
///
/// O verificador existe porque este passo é o primeiro em que os dois números
/// passam a ser calculáveis. Antes dele a regra era uma frase; a partir dele é
/// uma conta que pode dar errado.
/// </summary>
public static class FloatingPillar
{
    /// <summary>
    /// Um milímetro, a tolerância de regra da arquitetura.
    ///
    /// A regra escrita diz "maior que zero", mas zero não existe em ponto
    /// flutuante depois de um seno. E um pilar com meio milímetro acima do
    /// chão é, para qualquer efeito de obra, uma mesa pousada — aprová-lo
    /// seria cumprir a letra da regra contra o que ela quer dizer.
    /// </summary>
    public const double Tolerancia = 0.001;

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O que há de errado com este pilar, em português, ou null se ele está de
    /// pé como deve.
    /// </summary>
    /// <param name="freeHeight">P3: o que fica acima do terreno.</param>
    /// <param name="embedment">P2: o que fica enterrado.</param>
    public static string? Check(double freeHeight, double embedment)
    {
        if (!double.IsFinite(freeHeight) || !double.IsFinite(embedment))
            return "o pilar tem medida inválida";

        if (freeHeight < Tolerancia)
        {
            return freeHeight < 0
                ? $"o pilar está enterrado até a mesa: sobram {Texto(freeHeight)} m acima do terreno"
                : $"o pilar não tem altura livre: {Texto(freeHeight)} m acima do terreno, "
                  + $"e o mínimo é {Texto(Tolerancia)} m";
        }

        if (embedment < Tolerancia)
        {
            return embedment < 0
                ? $"o pilar não chega ao terreno: faltam {Texto(-embedment)} m para ele encostar"
                : $"o pilar não está enterrado: {Texto(embedment)} m abaixo do terreno, "
                  + $"e o mínimo é {Texto(Tolerancia)} m";
        }

        return null;
    }

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);
}
