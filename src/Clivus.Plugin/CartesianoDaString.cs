using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O plano cartesiano de um tipo de string (elétrica, 11.2 a 11.4): não a
/// mesa real, mas os módulos em X/Y, mesa ao lado de mesa com o vão de
/// campo; a fileira de baixo da mesa (a ponta baixa) embaixo. 2V mostra duas
/// fileiras, 1V uma. Por cima, o traçado: cada string com uma cor, a ordem
/// de cada módulo, o + e o − nas pontas; a string em montagem tracejada.
/// Clicar num módulo avisa a janela (<see cref="CelulaClicada"/>).
/// </summary>
internal sealed class CartesianoDaString : Border
{
    /// <summary>Pixels por metro antes do Viewbox ajustar ao espaço.</summary>
    private const double Escala = 40;

    private const double MargemDeCima = 0.9;

    private static readonly Color[] Cores =
    [
        Color.FromRgb(220, 60, 60), Color.FromRgb(40, 140, 60), Color.FromRgb(40, 90, 200), Color.FromRgb(200, 120, 0),
        Color.FromRgb(140, 60, 180), Color.FromRgb(0, 150, 150), Color.FromRgb(180, 60, 120), Color.FromRgb(110, 110, 30),
    ];

    private readonly Canvas _tela = new();

    // A moldura é uma só: criar uma nova a cada redesenho punha a mesma tela
    // em duas molduras ("o elemento já é o filho lógico de outro elemento",
    // 05/10/2026), e o erro num clique derrubava o Civil 3D.
    private readonly Viewbox _moldura = new() { Stretch = Stretch.Uniform };
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

    /// <summary>O clique num módulo do cartesiano.</summary>
    /// <summary>O clique num módulo; o segundo valor diz se o Ctrl estava apertado (+ e −).</summary>
    internal event Action<RoutingCell, bool>? CelulaClicada;

    /// <summary>O tipo mostrado, ou null.</summary>
    internal StringType? Tipo { get; private set; }

    /// <summary>A cor da string de índice i (a mesma na lista e no desenho do cartesiano).</summary>
    internal static Color CorDaString(int i) => Cores[i % Cores.Length];

    internal void Mostrar(StringType? tipo, IReadOnlyList<RoutingCell>? emMontagem = null)
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

        _tela.Width = largura * Escala;
        _tela.Height = (altura + MargemDeCima) * Escala;

        var donoDaCelula = new Dictionary<RoutingCell, int>();
        for (var i = 0; i < tipo.Routes.Count; i++)
            foreach (var c in tipo.Routes[i].Cells) donoDaCelula[c] = i;

        var montagem = emMontagem ?? [];

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
                    var celula = new RoutingCell(t, c, r);
                    var (x, y, w, h) = desenho.CellRect(arranjo, t, c, r);

                    Brush fundo = Brushes.LightSteelBlue;
                    if (montagem.Contains(celula)) fundo = Brushes.Gold;
                    else if (donoDaCelula.TryGetValue(celula, out var dono)) fundo = new SolidColorBrush(Clarear(CorDaString(dono)));

                    var retangulo = new Rectangle
                    {
                        Width = Math.Max(1, w * Escala - 3),
                        Height = Math.Max(1, h * Escala - 3),
                        Fill = fundo,
                        Stroke = Brushes.SteelBlue,
                        StrokeThickness = 1.5,
                        Cursor = Cursors.Hand,
                        ToolTip = Tr.F("Mesa {0}, coluna {1}, fileira {2}", t + 1, c + 1, r + 1),
                    };

                    retangulo.MouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        CelulaClicada?.Invoke(celula, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                    };

                    // Y do cartesiano para cima: a fileira 0 (ponta baixa) embaixo.
                    Canvas.SetLeft(retangulo, x * Escala + 1.5);
                    Canvas.SetTop(retangulo, Topo(altura, y, h) + 1.5);
                    _tela.Children.Add(retangulo);
                }
            }
        }

        for (var i = 0; i < tipo.Routes.Count; i++)
            Tracar(tipo, altura, tipo.Routes[i].Cells, CorDaString(i), tracejado: false);

        if (montagem.Count > 0) Tracar(tipo, altura, montagem, Colors.DarkOrange, tracejado: true);

        if (_moldura.Child != _tela) _moldura.Child = _tela;
        if (Child != _moldura) Child = _moldura;
    }

    /// <summary>A linha pelo centro dos módulos, o número de ordem em cada um e o + e o − nas pontas.</summary>
    private void Tracar(StringType tipo, double altura, IReadOnlyList<RoutingCell> celulas, Color cor, bool tracejado)
    {
        var pincel = new SolidColorBrush(cor);
        var linha = new Polyline { Stroke = pincel, StrokeThickness = 3, IsHitTestVisible = false };
        if (tracejado) linha.StrokeDashArray = [2, 1];

        for (var k = 0; k < celulas.Count; k++)
        {
            var (cx, cy) = Centro(tipo, altura, celulas[k]);
            linha.Points.Add(new Point(cx, cy));

            var numero = new TextBlock { Text = (k + 1).ToString(Tr.Culture), FontSize = 0.3 * Escala, Foreground = Brushes.Black, IsHitTestVisible = false };
            Canvas.SetLeft(numero, cx + 3);
            Canvas.SetTop(numero, cy + 2);
            _tela.Children.Add(numero);
        }

        _tela.Children.Add(linha);

        Sinal(tipo, altura, celulas[0], "+", pincel);
        if (celulas.Count > 1 && !tracejado) Sinal(tipo, altura, celulas[^1], "−", pincel);
    }

    private void Sinal(StringType tipo, double altura, RoutingCell celula, string sinal, Brush pincel)
    {
        var (cx, cy) = Centro(tipo, altura, celula);
        var texto = new TextBlock { Text = sinal, FontSize = 0.7 * Escala, FontWeight = FontWeights.Bold, Foreground = pincel, IsHitTestVisible = false };
        Canvas.SetLeft(texto, cx - 0.45 * Escala);
        Canvas.SetTop(texto, cy - 0.95 * Escala);
        _tela.Children.Add(texto);
    }

    private static (double X, double Y) Centro(StringType tipo, double altura, RoutingCell c)
    {
        var (x, y, w, h) = tipo.SketchOrDefault.CellRect(tipo.Arrangement, c.Table, c.Column, c.Row);
        return ((x + w / 2) * Escala, Topo(altura, y, h) + h / 2 * Escala);
    }

    private static double Topo(double altura, double y, double h) => (MargemDeCima + altura - y - h) * Escala;

    private static Color Clarear(Color c) => Color.FromRgb((byte)((c.R + 255 * 2) / 3), (byte)((c.G + 255 * 2) / 3), (byte)((c.B + 255 * 2) / 3));
}
