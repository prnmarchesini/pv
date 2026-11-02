using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O resumo das sombras de uma mesa (42.14, Renan, 02/11/2026: "um botão no
/// modal sombras que eu seleciono a mesa e ele me da o resumo das sombras, me
/// uma lista estruturadas dos dias que bate sombra em cada modulo e quantos %
/// em cada hora, pense em uma forma top de apresentar"):
/// <list type="bullet">
/// <item><description>em cima, a mesa vista de cima: um quadrado por módulo,
/// da cor das horas de sombra dele (do branco ao magenta), com as horas
/// escritas; o clique escolhe o módulo e "Mesa inteira" mostra a média;</description></item>
/// <item><description>no meio, o calendário do período: uma coluna por dia e
/// uma linha por hora, da cor da porcentagem da face na sombra (cinza onde o
/// sol não conta), com os meses embaixo e as horas ao lado; o mouse diz o
/// dia, a hora e a porcentagem;</description></item>
/// <item><description>embaixo, os dias com sombra, um por linha, com cada hora
/// e a porcentagem dela;</description></item>
/// <item><description>o Copiar leva para o Excel uma linha por módulo, dia e hora com sombra.</description></item>
/// </list>
/// </summary>
internal sealed class JanelaDoCalendarioDeSombras : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Color Cinza = Color.FromRgb(232, 232, 232);

    private readonly ShadeCalendar _calendario;
    private readonly List<(Border Celula, int Modulo)> _celulas = [];
    private readonly Image _imagem = new() { Stretch = Stretch.Fill, Height = 220, Cursor = Cursors.Cross };
    private readonly Canvas _meses = new() { Height = 16 };
    private readonly TextBlock _titulo = new() { FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sobOMouse = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 0), MinHeight = 16 };
    private readonly StackPanel _dias = new();
    private readonly double[] _horasDoModulo;
    private int _modulo = ShadeCalendar.WholeTable;

    internal JanelaDoCalendarioDeSombras(ShadeCalendar calendario, string mesa, string periodo)
    {
        _calendario = calendario;
        _horasDoModulo = Enumerable.Range(0, calendario.Modules.Count).Select(calendario.Hours).ToArray();

        Title = Tr.F("Clivus Solar — Sombras da mesa {0}", mesa);
        Width = 1060;
        Height = 760;
        MinWidth = 700;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        RenderOptions.SetBitmapScalingMode(_imagem, BitmapScalingMode.NearestNeighbor);

        var raiz = new DockPanel { Margin = new Thickness(12) };

        // ---- o cabeçalho: o período e a mesa em números
        var comSombra = Enumerable.Range(0, calendario.Modules.Count).Count(m => _horasDoModulo[m] > 0);
        var (piorF, piorQuando) = calendario.Worst(ShadeCalendar.WholeTable);
        var piorModulo = Enumerable.Range(0, calendario.Modules.Count).Select(m => (m, calendario.Worst(m))).OrderByDescending(x => x.Item2.Fraction).FirstOrDefault();
        var cabeca = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        cabeca.Children.Add(new TextBlock { Text = Tr.F("Mesa {0}", mesa), FontWeight = FontWeights.Bold, FontSize = 16 });
        cabeca.Children.Add(new TextBlock { Text = periodo, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        cabeca.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Text = comSombra == 0
                ? Tr.F("Nenhum dos {0} módulos pega sombra no período.", calendario.Modules.Count)
                : Tr.F("{0} de {1} módulos pegam sombra · {2:0.#} h de sombra cheia na mesa (média dos módulos) em {3} dia(s) · pior: {4}, {5:0}% em {6:dd/MM HH:mm}",
                    comSombra, calendario.Modules.Count, calendario.Hours(ShadeCalendar.WholeTable), calendario.DaysWithShade(ShadeCalendar.WholeTable),
                    calendario.Modules[piorModulo.m].Label, piorModulo.Item2.Fraction * 100, piorModulo.Item2.When ?? piorQuando ?? DateTime.MinValue),
        });
        DockPanel.SetDock(cabeca, Dock.Top);
        raiz.Children.Add(cabeca);

        // ---- os botões, embaixo
        var recado = new TextBlock { Foreground = Brushes.ForestGreen, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var copiar = new Button { Content = Tr.T("Copiar para o Excel"), Height = 26, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = Tr.T("Copia uma linha por módulo, dia e hora com sombra (módulo, data, hora, %, causa); cole no Excel.") };
        copiar.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(calendario.ToTsv(Tr.T("Módulo\tData\tHora\t% na sombra\tCausa"), ShadowTopics.CauseName));
                recado.Foreground = Brushes.ForestGreen;
                recado.Text = Tr.T("Copiado: cole no Excel.");
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao copiar o calendário de sombras.", erro);
                recado.Foreground = Brushes.Firebrick;
                recado.Text = Tr.F("Não consegui copiar: {0}", erro.Message);
            }
        };
        var fechar = new Button { Content = Tr.T("Fechar"), Width = 80, Height = 26, IsCancel = true };
        fechar.Click += (_, _) => Close();
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        botoes.Children.Add(recado);
        botoes.Children.Add(copiar);
        botoes.Children.Add(fechar);
        DockPanel.SetDock(botoes, Dock.Bottom);
        raiz.Children.Add(botoes);

        // ---- a mesa vista de cima
        var mapa = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var linhaDoMapa = new DockPanel();
        var inteira = new Button { Content = Tr.T("Mesa inteira"), Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top, ToolTip = Tr.T("O calendário da mesa: a média dos módulos em cada hora.") };
        inteira.Click += (_, _) => Escolher(ShadeCalendar.WholeTable);
        DockPanel.SetDock(inteira, Dock.Left);
        linhaDoMapa.Children.Add(inteira);
        linhaDoMapa.Children.Add(new ScrollViewer { Content = Mesa(), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        mapa.Children.Add(linhaDoMapa);
        mapa.Children.Add(new TextBlock { Text = Tr.T("Cada quadrado é um módulo, como a mesa é vista de cima: a cor e o número são as horas de sombra cheia no período. Clique para ver o calendário dele."), Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        DockPanel.SetDock(mapa, Dock.Top);
        raiz.Children.Add(mapa);

        // ---- o calendário
        var calendarioDoAno = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        calendarioDoAno.Children.Add(_titulo);
        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var horas = Horas();
        Grid.SetColumn(horas, 0);
        grade.Children.Add(horas);
        var moldura = new Border { BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Child = _imagem };
        Grid.SetColumn(moldura, 1);
        grade.Children.Add(moldura);
        Grid.SetColumn(_meses, 1);
        Grid.SetRow(_meses, 1);
        grade.Children.Add(_meses);
        calendarioDoAno.Children.Add(grade);
        calendarioDoAno.Children.Add(Legenda());
        calendarioDoAno.Children.Add(_sobOMouse);
        DockPanel.SetDock(calendarioDoAno, Dock.Top);
        raiz.Children.Add(calendarioDoAno);

        _imagem.MouseMove += (_, e) => SobOMouse(e.GetPosition(_imagem));
        _imagem.MouseLeave += (_, _) => _sobOMouse.Text = string.Empty;
        _meses.SizeChanged += (_, _) => Meses();

        // ---- os dias com sombra, com as horas
        var lista = new DockPanel();
        var tituloDaLista = new TextBlock { Text = Tr.T("Os dias com sombra e a porcentagem da face na sombra em cada hora"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(tituloDaLista, Dock.Top);
        lista.Children.Add(tituloDaLista);
        lista.Children.Add(new ScrollViewer { Content = _dias, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1) });
        raiz.Children.Add(lista);

        Content = raiz;

        // Abre no pior módulo: é o que se quer ver primeiro; sem sombra, na mesa inteira.
        Escolher(comSombra > 0 ? piorModulo.m : ShadeCalendar.WholeTable);
    }

    /// <summary>A cor da fração na sombra: o branco sem sombra, o rosa claro com pouca, o magenta na metade e o roxo com a face inteira.</summary>
    internal static Color CorDaFracao(double f)
    {
        if (f <= 0) return Colors.White;

        static Color Entre(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

        var claro = Color.FromRgb(255, 214, 255);
        var magenta = Color.FromRgb(255, 0, 255);
        var roxo = Color.FromRgb(120, 0, 140);
        return f <= 0.5 ? Entre(claro, magenta, Math.Clamp(f / 0.5, 0, 1)) : Entre(magenta, roxo, Math.Clamp((f - 0.5) / 0.5, 0, 1));
    }

    /// <summary>A mesa vista de cima: uma célula por módulo, a fileira de cima em cima.</summary>
    private Grid Mesa()
    {
        var grade = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        if (_calendario.Modules.Count == 0) return grade;

        var colunas = _calendario.Modules.Max(m => m.Column) + 1;
        var fileiras = _calendario.Modules.Max(m => m.Row) + 1;
        for (var c = 0; c < colunas; c++) grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var r = 0; r < fileiras; r++) grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var maximo = _horasDoModulo.DefaultIfEmpty(0).Max();
        for (var i = 0; i < _calendario.Modules.Count; i++)
        {
            var m = _calendario.Modules[i];
            var horas = _horasDoModulo[i];
            var (pior, quando) = _calendario.Worst(i);
            var cor = maximo > 0 ? CorDaFracao(horas / maximo) : Colors.White;
            var celula = new Border
            {
                Width = 46,
                Height = 34,
                Margin = new Thickness(1),
                Background = new SolidColorBrush(cor),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = horas > 0
                    ? Tr.F("{0}: {1:0.#} h de sombra cheia em {2} dia(s); pior {3:0}% em {4:dd/MM HH:mm}", m.Label, horas, _calendario.DaysWithShade(i), pior * 100, quando ?? DateTime.MinValue)
                    : Tr.F("{0}: sem sombra no período", m.Label),
                Child = new TextBlock
                {
                    Text = horas > 0 ? horas.ToString(horas < 10 ? "0.#" : "0", Brasil) : "·",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11,
                    Foreground = maximo > 0 && horas / maximo > 0.55 ? Brushes.White : Brushes.Black,
                },
            };
            var modulo = i;
            celula.MouseLeftButtonUp += (_, _) => Escolher(modulo);
            Grid.SetColumn(celula, m.Column);
            Grid.SetRow(celula, fileiras - 1 - m.Row);
            grade.Children.Add(celula);
            _celulas.Add((celula, i));
        }

        return grade;
    }

    /// <summary>As horas ao lado do calendário: uma a cada duas horas cheias.</summary>
    private Grid Horas()
    {
        var grade = new Grid { Height = _imagem.Height };
        var n = _calendario.Times.Count;
        for (var h = 0; h < n; h++)
        {
            var t = _calendario.Times[h];
            if (t.Minute != 0 || t.Hour % 2 != 0) continue;
            grade.Children.Add(new TextBlock
            {
                Text = t.ToString("HH:mm", Brasil),
                FontSize = 10,
                Foreground = Brushes.DimGray,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, Math.Max(0, (h + 0.5) / n * _imagem.Height - 7), 4, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
        }

        return grade;
    }

    /// <summary>Os meses embaixo do calendário, no primeiro dia de cada um que aparece.</summary>
    private void Meses()
    {
        _meses.Children.Clear();
        var largura = _meses.ActualWidth;
        var n = _calendario.Days.Count;
        if (largura <= 0 || n == 0) return;

        var ultimo = -1;
        for (var d = 0; d < n; d++)
        {
            var dia = _calendario.Days[d];
            if (dia.Month == ultimo) continue;
            ultimo = dia.Month;
            var rotulo = new TextBlock { Text = Brasil.DateTimeFormat.GetAbbreviatedMonthName(dia.Month).TrimEnd('.'), FontSize = 10, Foreground = Brushes.DimGray };
            if (Tr.Culture.TwoLetterISOLanguageName != "pt") rotulo.Text = Tr.Culture.DateTimeFormat.GetAbbreviatedMonthName(dia.Month).TrimEnd('.');
            Canvas.SetLeft(rotulo, d / (double)n * largura);
            _meses.Children.Add(rotulo);
        }
    }

    private static FrameworkElement Legenda()
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(44, 2, 0, 0) };
        void Item(Color cor, string texto)
        {
            linha.Children.Add(new Border { Width = 14, Height = 10, Background = new SolidColorBrush(cor), BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
            linha.Children.Add(new TextBlock { Text = texto, FontSize = 10, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 12, 0) });
        }

        Item(Colors.White, Tr.T("sem sombra"));
        Item(CorDaFracao(0.1), "10%");
        Item(CorDaFracao(0.5), "50%");
        Item(CorDaFracao(1), "100%");
        Item(Cinza, Tr.T("não conta (sol abaixo da altura ou fora do horário)"));
        return linha;
    }

    /// <summary>Escolhe o módulo (ou a mesa inteira) e refaz o calendário e a lista.</summary>
    private void Escolher(int modulo)
    {
        _modulo = modulo;

        foreach (var (celula, i) in _celulas)
        {
            celula.BorderBrush = i == modulo ? Brushes.Black : Brushes.Gray;
            celula.BorderThickness = new Thickness(i == modulo ? 3 : 1);
        }

        var nome = modulo == ShadeCalendar.WholeTable ? Tr.T("Mesa inteira (média dos módulos)") : _calendario.Modules[modulo].Label;
        var (pior, quando) = _calendario.Worst(modulo);
        var horas = _calendario.Hours(modulo);
        _titulo.Text = horas > 0
            ? Tr.F("{0}: {1:0.#} h de sombra cheia em {2} dia(s); pior {3:0}% em {4:dd/MM/yyyy HH:mm}", nome, horas, _calendario.DaysWithShade(modulo), pior * 100, quando ?? DateTime.MinValue)
            : Tr.F("{0}: sem sombra no período", nome);

        Desenhar();
        Listar();
    }

    /// <summary>O calendário do módulo escolhido: um pixel por dia e por hora.</summary>
    private void Desenhar()
    {
        var largura = Math.Max(1, _calendario.Days.Count);
        var altura = Math.Max(1, _calendario.Times.Count);
        var mapa = new WriteableBitmap(largura, altura, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[largura * altura * 4];

        for (var d = 0; d < _calendario.Days.Count; d++)
        {
            for (var h = 0; h < _calendario.Times.Count; h++)
            {
                var cor = _calendario.At(_modulo, d, h) is { } f ? CorDaFracao(f) : Cinza;
                var k = (h * largura + d) * 4;
                pixels[k] = cor.B;
                pixels[k + 1] = cor.G;
                pixels[k + 2] = cor.R;
                pixels[k + 3] = 255;
            }
        }

        mapa.WritePixels(new Int32Rect(0, 0, largura, altura), pixels, largura * 4, 0);
        _imagem.Source = mapa;
    }

    /// <summary>O que está sob o mouse no calendário: o dia, a hora e a porcentagem.</summary>
    private void SobOMouse(Point p)
    {
        if (_imagem.ActualWidth <= 0 || _imagem.ActualHeight <= 0 || _calendario.Days.Count == 0 || _calendario.Times.Count == 0) return;

        var d = Math.Clamp((int)(p.X / _imagem.ActualWidth * _calendario.Days.Count), 0, _calendario.Days.Count - 1);
        var h = Math.Clamp((int)(p.Y / _imagem.ActualHeight * _calendario.Times.Count), 0, _calendario.Times.Count - 1);
        var quando = _calendario.Days[d].ToDateTime(_calendario.Times[h]);
        var i = _calendario.InstantAt(d, h);

        _sobOMouse.Text = i < 0
            ? Tr.F("{0:dd/MM/yyyy HH:mm}: não conta (sol abaixo da altura ou fora do horário)", quando)
            : _calendario.Fraction(_modulo, i) is var f and > 0
                ? _modulo == ShadeCalendar.WholeTable
                    ? Tr.F("{0:dd/MM/yyyy HH:mm}: {1:0}% da mesa na sombra (média dos módulos)", quando, f * 100)
                    : Tr.F("{0:dd/MM/yyyy HH:mm}: {1:0}% da face na sombra, por {2}", quando, f * 100, ShadowTopics.CauseName(_calendario.Modules[_modulo].Causes[i]))
                : Tr.F("{0:dd/MM/yyyy HH:mm}: sem sombra", quando);
    }

    /// <summary>Os dias com sombra do módulo escolhido: a data e, em cada hora com sombra, a porcentagem.</summary>
    private void Listar()
    {
        _dias.Children.Clear();
        var dias = _calendario.ShadedDays(_modulo);
        if (dias.Count == 0)
        {
            _dias.Children.Add(new TextBlock { Text = Tr.T("Nenhum dia com sombra no período."), Foreground = Brushes.Gray, Margin = new Thickness(6) });
            return;
        }

        foreach (var d in dias)
        {
            var dia = _calendario.Days[d];
            var linha = new DockPanel { Margin = new Thickness(4, 2, 4, 2) };
            var data = new TextBlock
            {
                Text = dia.ToString("dd/MM/yyyy", Brasil) + " " + Tr.Culture.DateTimeFormat.GetAbbreviatedDayName(dia.DayOfWeek),
                Width = 112,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            };
            DockPanel.SetDock(data, Dock.Left);
            linha.Children.Add(data);

            var horas = new WrapPanel();
            for (var h = 0; h < _calendario.Times.Count; h++)
            {
                if (_calendario.At(_modulo, d, h) is not { } f || f <= 0) continue;
                horas.Children.Add(new Border
                {
                    Background = new SolidColorBrush(CorDaFracao(f)),
                    BorderBrush = Brushes.Silver,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = string.Format(Brasil, "{0:HH:mm} {1:0}%", _calendario.Times[h], f * 100),
                        FontSize = 11,
                        Foreground = f > 0.55 ? Brushes.White : Brushes.Black,
                    },
                });
            }

            linha.Children.Add(horas);
            _dias.Children.Add(linha);
        }
    }
}
