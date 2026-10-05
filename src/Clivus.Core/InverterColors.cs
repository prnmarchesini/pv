namespace Clivus.Core;

/// <summary>
/// As cores automáticas dos inversores (pedido do Renan em 05/10/2026: "cada
/// inversor pode ter uma cor, e cada string recebe a cor"). Legíveis no fundo
/// escuro do CAD e no claro (do papel e da janela), e longe das cores que já
/// dizem outra coisa: lilás, violeta e roxo são da sombra; magenta e vermelho
/// são de aviso (e o vermelho é a cor da camada das strings livres). A cor é
/// só representação: o vínculo continua na string (regra elétrica 1).
/// </summary>
public static class InverterColors
{
    /// <summary>A paleta, na ordem em que os inversores novos a recebem (vizinhas bem diferentes).</summary>
    public static IReadOnlyList<(string Name, RgbColor Color)> Palette { get; } =
    [
        (Tr.N("Laranja"), new RgbColor(240, 130, 0)),
        (Tr.N("Azul"), new RgbColor(30, 130, 230)),
        (Tr.N("Verde"), new RgbColor(30, 160, 50)),
        (Tr.N("Ouro"), new RgbColor(190, 150, 0)),
        (Tr.N("Ciano"), new RgbColor(0, 165, 185)),
        (Tr.N("Marrom"), new RgbColor(170, 100, 40)),
        (Tr.N("Lima"), new RgbColor(110, 170, 0)),
        (Tr.N("Azul-aço"), new RgbColor(70, 130, 180)),
        (Tr.N("Verde-mar"), new RgbColor(40, 150, 110)),
        (Tr.N("Oliva"), new RgbColor(140, 140, 20)),
    ];

    /// <summary>
    /// A cor do próximo inversor: a da paleta menos usada entre as dadas (as
    /// dos inversores que já existem), na ordem da paleta no empate. Assim os
    /// primeiros dez saem todos diferentes, e depois a paleta recomeça.
    /// </summary>
    public static RgbColor Next(IEnumerable<RgbColor> used)
    {
        ArgumentNullException.ThrowIfNull(used);

        var usos = used.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        return Palette.Select((p, i) => (p.Color, Usos: usos.GetValueOrDefault(p.Color), i)).OrderBy(x => x.Usos).ThenBy(x => x.i).First().Color;
    }
}
