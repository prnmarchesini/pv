using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O plano cartesiano de um tipo de string (elétrica, 11.2): não a mesa
/// real, mas os módulos em X/Y, mesa ao lado de mesa com o vão de campo; a
/// fileira de baixo da mesa (a ponta baixa) embaixo. 2V mostra duas
/// fileiras, 1V uma.
/// </summary>
internal sealed class CartesianoDaString : Border
{
    /// <summary>Pixels por metro antes do Viewbox ajustar ao espaço.</summary>
    private const double Escala = 40;

    private readonly Canvas _tela = new();
    private readonly TextBlock _vazio = new()
    {
        Text = Tr.T("Escolha um tipo na lista. Tipo sem mesas: use Trocar mesas."),
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Gray,
        Margin = new Thickness(12),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    internal CartesianoDaString()
    {
        BorderBrush = Brushes.LightGray;
        BorderThickness = new Thickness(1);
        Background = Brushes.White;
        Padding = new Thickness(8);
        Child = _vazio;
    }

    /// <summary>O tipo mostrado, ou null.</summary>
    internal StringType? Tipo { get; private set; }

    internal void Mostrar(StringType? tipo)
    {
        Tipo = tipo;
        _tela.Children.Clear();

        if (tipo is null || tipo.Arrangement.IsEmpty)
        {
            Child = _vazio;
            return;
        }

        var arranjo = tipo.Arrangement;
        var desenho = tipo.SketchOrDefault;
        var (largura, altura) = desenho.Size(arranjo);
        var margemDeCima = 0.9;

        _tela.Width = largura * Escala;
        _tela.Height = (altura + margemDeCima) * Escala;

        for (var t = 0; t < arranjo.Tables.Count; t++)
        {
            var mesa = arranjo.Tables[t];
            var inicio = desenho.CellRect(arranjo, t, 0, 0);

            var rotulo = new TextBlock { Text = Tr.F("Mesa {0} ({1}x{2})", t + 1, mesa.Columns, mesa.Rows), FontSize = 0.45 * Escala, Foreground = Brushes.DimGray };
            Canvas.SetLeft(rotulo, inicio.X * Escala);
            Canvas.SetTop(rotulo, 0);
            _tela.Children.Add(rotulo);

            for (var c = 0; c < mesa.Columns; c++)
            {
                for (var r = 0; r < mesa.Rows; r++)
                {
                    var (x, y, w, h) = desenho.CellRect(arranjo, t, c, r);
                    var celula = new Rectangle
                    {
                        Width = Math.Max(1, w * Escala - 3),
                        Height = Math.Max(1, h * Escala - 3),
                        Fill = Brushes.LightSteelBlue,
                        Stroke = Brushes.SteelBlue,
                        StrokeThickness = 1.5,
                        ToolTip = Tr.F("Mesa {0}, coluna {1}, fileira {2}", t + 1, c + 1, r + 1),
                    };

                    // Y do cartesiano para cima: a fileira 0 (ponta baixa) embaixo.
                    Canvas.SetLeft(celula, x * Escala + 1.5);
                    Canvas.SetTop(celula, (margemDeCima + altura - y - h) * Escala + 1.5);
                    _tela.Children.Add(celula);
                }
            }
        }

        Child = new Viewbox { Stretch = Stretch.Uniform, Child = _tela };
    }
}
