using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// Abre a janela de escolha da superfície.
///
/// Existe como tipo separado pelo mesmo motivo de <see cref="RibbonUfv"/>: é
/// aqui que se menciona WPF, e num host sem interface — o Core Console —
/// mencionar WPF cedo demais derruba o carregamento inteiro.
///
/// Ter a janela num tipo à parte não basta sozinho. O método que a nomeia
/// precisa de <see cref="MethodImplOptions.NoInlining"/>: se o JIT trouxer o
/// corpo para dentro de quem chama, o tipo da janela passa a ser resolvido
/// junto com o chamador, ou seja ANTES da verificação de "tem interface?"
/// chegar a rodar. Uma guarda em tempo de execução não protege contra uma
/// decisão tomada em tempo de compilação.
/// </summary>
internal static class EscolhaDeTerreno
{
    /// <summary>
    /// Pergunta ao usuário qual superfície é o terreno, ou devolve null se ele
    /// desistir.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static SurfaceEntry? Perguntar(IReadOnlyList<SurfaceEntry> superficies)
    {
        var janela = new JanelaDeTerreno(superficies);

        AcadApp.ShowModalWindow(
            AcadApp.MainWindow.Handle,
            janela,
            persistSizeAndPosition: false);

        return janela.Escolhida;
    }
}
