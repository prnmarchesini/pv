using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O estado sujo da mesa no desenho (7.1): grava no XData do contorno e
/// pinta de vermelho o contorno, os pilares e os módulos. A face superior
/// não é pintada (é o que o PVsyst recebe; a camada dela tem só faces
/// limpas).
///
/// A pintura é a cor na instância, por cima da cor de análise que a peça
/// tinha. Não há "despintar": quem limpa uma mesa é o recálculo (7.3/7.4),
/// que a redesenha inteira com as cores certas.
/// </summary>
internal static class TableState
{
    /// <summary>O vermelho da sujeira.</summary>
    internal static readonly RgbColor CorDaSuja = RgbColor.Red;

    /// <summary>
    /// Suja a mesa: XData e pintura. Devolve quantas peças pintou, ou null
    /// se a mesa não tem contorno (não há onde gravar o estado).
    /// </summary>
    internal static int? MarkDirty(Transaction transacao, TableParts mesa, string motivo)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(mesa);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        if (mesa.Identity is null || mesa.Contour is not { } contorno) return null;

        var entidade = (Entity)transacao.GetObject(contorno, OpenMode.ForWrite);
        LayoutXData.SaveTable(transacao, entidade, mesa.Identity.AsDirty(motivo));

        var pintadas = 0;

        foreach (var id in mesa.Paintable)
        {
            var peca = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
            peca.Color = Color.FromRgb(CorDaSuja.R, CorDaSuja.G, CorDaSuja.B);
            pintadas++;
        }

        return pintadas;
    }
}
