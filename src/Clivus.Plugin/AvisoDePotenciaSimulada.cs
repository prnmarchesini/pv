using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.GanchosDaPotencia))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// A faixa das telas de cálculo (rota de cabos, configuração elétrica,
/// resumo elétrico) quando a potência do módulo foi trocada pela área (item
/// 14 de 10/10/2026): diz por que os cálculos elétricos não aparecem e dá o
/// botão "Usar a configuração da mesa em vez da potência definida pela área",
/// que desfaz a troca e traz os cálculos de volta. Sem troca, some (não
/// ocupa espaço).
/// </summary>
internal sealed class AvisoDePotenciaSimulada : Border
{
    private readonly Document _documento;
    private readonly TextBlock _texto = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.SaddleBrown };

    /// <summary>Chamado depois de desfazer a troca: quem mostra a faixa relê o resto da tela.</summary>
    internal Action? AoMudar { get; set; }

    internal AvisoDePotenciaSimulada(Document documento)
    {
        _documento = documento;

        Background = new SolidColorBrush(Color.FromRgb(255, 248, 220));
        BorderBrush = Brushes.DarkGoldenrod;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(8, 4, 8, 4);
        Margin = new Thickness(8, 8, 8, 0);

        var usar = new Button
        {
            Content = Tr.T("Usar a configuração da mesa em vez da potência definida pela área"),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Tr.T("Desfaz a potência trocada pela área: cada mesa volta à potência do módulo dela, e as tensões, correntes e quedas voltam a ser calculadas."),
        };
        usar.Click += (_, _) => Usar();

        var linha = new DockPanel();
        DockPanel.SetDock(usar, Dock.Right);
        linha.Children.Add(usar);
        linha.Children.Add(_texto);
        Child = linha;

        Atualizar();
    }

    /// <summary>Relê o desenho: mostra a faixa com o porquê, ou some.</summary>
    internal void Atualizar()
    {
        try
        {
            var simulada = FonteDoModulo.Simulada(_documento.Database);
            Visibility = simulada is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            _texto.Text = simulada?.Reason() ?? string.Empty;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ler a potência trocada pela área.", erro);
            Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    /// <summary>
    /// O clique do botão: fora de comando, com o documento travado e o vigia
    /// calado, desfaz a troca e escreve a frase na linha de comando. O nível 2
    /// chama este mesmo caminho (CLIVUS-USAR-A-MESA, só no build de teste).
    /// </summary>
    internal static string Desfazer(Document documento)
    {
        var frase = EscritaForaDeComando.Fazer(documento, () => PotenciaCommands.UsarAMesa(documento));
        documento.Editor.WriteMessage("\n" + frase + "\n");
        return frase;
    }

    private void Usar()
    {
        try
        {
            Desfazer(_documento);
            Atualizar();
            AoMudar?.Invoke();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desfazer a potência trocada pela área.", erro);
            _texto.Text = Tr.F("Não consegui desfazer: {0}", erro.Message);
        }
    }
}

#if DEBUG
/// <summary>
/// Só no build de teste: o nível 2 clica, por LISP, no botão "Usar a
/// configuração da mesa em vez da potência definida pela área", fora de um
/// comando, pelo mesmo caminho da faixa. (clivus-usar-a-mesa) devolve T.
/// </summary>
public static class GanchosDaPotencia
{
    [LispFunction("CLIVUS-USAR-A-MESA")]
    public static object? UsarAMesa(ResultBuffer argumentos)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return null;

        AvisoDePotenciaSimulada.Desfazer(documento);
        return true;
    }
}
#endif
