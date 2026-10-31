namespace Clivus.Core;

/// <summary>
/// O enterro de uma linha de pilares (38.12, 31/10/2026, Renan: "enterro
/// nominal, para usar no desenho, tendo o enterro mínimo e máximo, vc vai
/// saber até onde pode chegar" e "no biposte, o enterro mínimo e máximo é POR
/// PILAR, o da frente e de traz"). O nominal é o do desenho da estrutura no
/// chão plano; o mínimo e o máximo, até onde o enterro de cada estaca pode ir
/// no terreno da usina.
/// </summary>
/// <param name="Min">O mínimo que a estaca fica dentro do solo.</param>
/// <param name="Nominal">O enterro do desenho (o da estrutura no chão plano).</param>
/// <param name="Max">O máximo que a estaca pode ficar dentro do solo.</param>
public sealed record EmbedmentRange(double Min, double Nominal, double Max)
{
    /// <summary>
    /// O enterro e o comprimento de uma estaca com esta altura livre (a
    /// escolha do Renan, "Estaca do catálogo"). Com o comprimento de catálogo,
    /// a estaca é a do catálogo e o enterro é o que sobra, se cabe entre o
    /// mínimo e o máximo. Se nem com o mínimo ela alcança, ela cresce, com o
    /// mínimo enterrado ("o pilar fica com 7m por exemplo, foda-se eu tenho
    /// que respeitar o enterrado, o que ficar livre pra cima da terra é
    /// consequência"); se passaria do máximo, é cortada, com o máximo. Sem
    /// catálogo (a monoposte), o nominal.
    /// </summary>
    /// <param name="free">A altura livre: do terreno ao topo da estaca.</param>
    /// <param name="catalogLength">O comprimento da estaca do catálogo, ou null.</param>
    public (double Embedment, double Length) Stake(double free, double? catalogLength)
    {
        if (catalogLength is not { } catalogo) return (Nominal, free + Nominal);

        var enterro = catalogo - free;
        if (enterro < Min) return (Min, free + Min);
        if (enterro > Max) return (Max, free + Max);
        return (enterro, catalogo);
    }

    /// <summary>
    /// O enterro de uma linha a partir do que a estrutura diz (cada um pode
    /// faltar) e do projeto: o mínimo da estrutura ou o do projeto; o nominal
    /// da estrutura ou o mínimo (o enterro de antes do 38.12); o máximo da
    /// estrutura ou o maior entre o do projeto e o nominal.
    /// </summary>
    public static EmbedmentRange Of(double? min, double? nominal, double? max, double projectMin, double projectMax)
    {
        var minimo = min ?? projectMin;
        var nom = Math.Max(nominal ?? minimo, minimo);
        var maximo = max ?? Math.Max(projectMax, nom);
        return new EmbedmentRange(minimo, Math.Min(nom, Math.Max(maximo, minimo)), Math.Max(maximo, minimo));
    }

    /// <summary>Por que o enterro digitado não serve, ou null: o mínimo, o nominal e o máximo em ordem.</summary>
    public static string? WhyInvalid(double? min, double? nominal, double? max)
    {
        if (min is { } a && nominal is { } b && b < a) return Tr.T("o enterro nominal é menor que o mínimo");
        if (nominal is { } c && max is { } d && c > d) return Tr.T("o enterro nominal é maior que o máximo");
        if (min is { } e && max is { } f && e > f) return Tr.T("o enterro mínimo é maior que o máximo");
        return null;
    }
}
