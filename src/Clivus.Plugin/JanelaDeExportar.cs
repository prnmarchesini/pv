using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O menu Exportar (Renan, 10/10/2026: "quero um menu exportar para eu
/// escolher e exportar cabos, pilares etc."): uma caixa de marcar por parte,
/// tudo num .xlsx só, uma aba por parte. O padrão é tudo marcado.
/// </summary>
internal sealed class JanelaDeExportar : Window
{
    private readonly List<(ExportSheets Aba, CheckBox Caixa)> _caixas = [];

    /// <summary>O que foi escolhido (vale com DialogResult = true).</summary>
    internal ExportSheets Abas { get; private set; }

    internal JanelaDeExportar()
    {
        Title = Tr.T("Exportar para o Excel — Clivus Solar");
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 380;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(12) };
        var recado = new TextBlock { Foreground = Brushes.Firebrick, Margin = new Thickness(0, 6, 0, 0) };
        pilha.Children.Add(new TextBlock { Text = Tr.T("Escolha o que vai para a planilha (uma aba para cada):"), Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 8) });

        void Caixa(ExportSheets aba, string texto, string dica)
        {
            var c = new CheckBox { Content = texto, ToolTip = dica, IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
            _caixas.Add((aba, c));
            pilha.Children.Add(c);
        }

        Caixa(ExportSheets.Summary, Tr.T("Resumo (mesas, módulos, kWp, pilares)"), Tr.T("As quantidades da usina."));
        Caixa(ExportSheets.Analyses, Tr.T("Análises"), Tr.T("A última quantificação de cada análise."));
        Caixa(ExportSheets.Pillars, Tr.T("Pilares"), Tr.T("Um pilar por linha: enterrado, acima do terreno e total."));
        Caixa(ExportSheets.PillarPurchase, Tr.T("Compra de pilares"), Tr.T("Os comprimentos agrupados, com a quantidade de cada um."));
        Caixa(ExportSheets.ElectricalSummary, Tr.T("Resumo elétrico"), Tr.T("Subestações, trafos, inversores, strings, módulos e kWp pela cadeia de vínculo."));
        Caixa(ExportSheets.Cables, Tr.T("Cabos (com totalização)"), Tr.T("Uma aba por rota com cabos (recontada antes), o resumo de cabos e a lista de material."));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var exportar = new Button { Content = Tr.T("Exportar..."), MinWidth = 90, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancelar = new Button { Content = Tr.T("Cancelar"), MinWidth = 80, Height = 26, IsCancel = true };
        exportar.Click += (_, _) =>
        {
            try
            {
                Abas = _caixas.Where(c => c.Caixa.IsChecked == true).Aggregate(ExportSheets.None, (a, c) => a | c.Aba);
                if (Abas == ExportSheets.None)
                {
                    recado.Text = Tr.T("Marque pelo menos uma parte.");
                    return;
                }

                DialogResult = true;
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha na janela de exportar.", erro);
            }
        };

        botoes.Children.Add(exportar);
        botoes.Children.Add(cancelar);
        pilha.Children.Add(recado);
        pilha.Children.Add(botoes);
        Content = pilha;
    }
}
