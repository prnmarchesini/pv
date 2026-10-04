using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Os vãos entre pilares escritos par a par (P1-P2, P2-P3...), com a soma e
/// se ela bate com o que os pilares precisam cobrir. Passo 8.2
/// (Melhorias.docx, 01/10/2026). A conta é do <see cref="PillarSpanForm"/>.
/// </summary>
internal sealed class JanelaDeVaos : Window
{
    private static CultureInfo Cultura => Tr.Culture;

    private readonly double _cobrir;
    private readonly TextBox _pilares = new() { Width = 60, Height = 24, Margin = new Thickness(6, 0, 6, 0) };
    private readonly StackPanel _linhas = new();
    private readonly List<TextBox> _campos = [];
    private readonly TextBlock _soma = new() { FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Button _ok;

    /// <summary>Se o usuário confirmou (OK ou "Usar o vão-alvo").</summary>
    internal bool Confirmou { get; private set; }

    /// <summary>Os vãos escritos, ou null para voltar ao vão-alvo.</summary>
    internal IReadOnlyList<double>? Vaos { get; private set; }

    internal JanelaDeVaos(TableLayout mesa, TableFrame estrutura)
    {
        ArgumentNullException.ThrowIfNull(mesa);
        ArgumentNullException.ThrowIfNull(estrutura);

        _cobrir = estrutura.PillarCoverage(mesa);

        Title = Tr.T("Clivus Solar — Vãos entre pilares");
        Width = 420;
        Height = 560;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new DockPanel { Margin = new Thickness(12) };

        var explicacao = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Text = Tr.F("Os pilares precisam cobrir {0} m: a mesa de {1} m (módulos, espaçamentos e sobras) menos o balanço de {2} m em cada ponta.",
                Numero(_cobrir), Numero(mesa.Length), Numero(estrutura.PillarCantilever)),
        };
        DockPanel.SetDock(explicacao, Dock.Top);
        pilha.Children.Add(explicacao);

        var quantos = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 8) };
        quantos.Children.Add(new TextBlock { Text = Tr.T("Pilares:"), VerticalAlignment = VerticalAlignment.Center });
        quantos.Children.Add(_pilares);

        var igual = new Button
        {
            Content = Tr.T("Distribuir igual"),
            Height = 24,
            Padding = new Thickness(8, 0, 8, 0),
            ToolTip = Tr.T("Refaz a lista com vãos iguais (ao milímetro) para este número de pilares; o último leva o resto."),
        };
        igual.Click += (_, _) => DistribuirIgual();
        quantos.Children.Add(igual);
        _pilares.ToolTip = Tr.T("Quantos pilares a mesa tem. N pilares dão N-1 vãos.");

        DockPanel.SetDock(quantos, Dock.Top);
        pilha.Children.Add(quantos);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };

        var alvo = new Button
        {
            Content = Tr.T("Usar o vão-alvo"),
            Height = 26,
            Padding = new Thickness(8, 0, 8, 0),
            ToolTip = Tr.T("Apaga os vãos escritos: a tabela volta a sair do vão-alvo, com vãos iguais."),
        };
        alvo.Click += (_, _) => { Vaos = null; Confirmou = true; DialogResult = true; };

        _ok = new Button { Content = "OK", Width = 80, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsDefault = true, ToolTip = Tr.T("Guarda estes vãos na mesa. Só libera quando a soma fecha.") };
        _ok.Click += (_, _) => Confirmar();

        botoes.Children.Add(alvo);
        botoes.Children.Add(_ok);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 80, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });

        DockPanel.SetDock(botoes, Dock.Bottom);
        pilha.Children.Add(botoes);

        DockPanel.SetDock(_soma, Dock.Bottom);
        pilha.Children.Add(_soma);

        pilha.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _linhas });

        Content = pilha;

        var iniciais = estrutura.PillarSpans ?? estrutura.Pillars(mesa).Spans;
        Montar(iniciais.Select(v => Math.Round(v, 3)).ToList());
    }

    private void Montar(IReadOnlyList<double> vaos)
    {
        _linhas.Children.Clear();
        _campos.Clear();

        for (var i = 0; i < vaos.Count; i++)
        {
            var linha = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var rotulo = new TextBlock { Text = PillarSpanForm.Label(i) + " (m)", Width = 110, VerticalAlignment = VerticalAlignment.Center };
            var campo = new TextBox { Height = 24, Text = Numero(vaos[i]), ToolTip = Tr.F("Distância do eixo do pilar {0} ao eixo do pilar {1}, em metro.", i + 1, i + 2) };

            campo.TextChanged += (_, _) => Somar();

            linha.Children.Add(rotulo);
            linha.Children.Add(campo);
            _linhas.Children.Add(linha);
            _campos.Add(campo);
        }

        _pilares.Text = (vaos.Count + 1).ToString(Cultura);
        Somar();
    }

    private void DistribuirIgual()
    {
        if (!NumberInput.TryParseCount(_pilares.Text, out var pilares) || pilares < 2)
        {
            _soma.Foreground = Brushes.Firebrick;
            _soma.Text = Tr.T("O número de pilares precisa ser um inteiro de 2 para cima.");
            return;
        }

        try
        {
            Montar(PillarSpanForm.Equal(_cobrir, pilares));
        }
        catch (ArgumentOutOfRangeException)
        {
            _soma.Foreground = Brushes.Firebrick;
            _soma.Text = Tr.F("Não dá para distribuir {0} pilares em {1} m (de 2 a 1001 pilares, vão de pelo menos 1 mm).", pilares, Numero(_cobrir));
        }
    }

    private void Somar()
    {
        if (!PillarSpanForm.TryRead(_campos.Select(c => c.Text).ToList(), out var vaos, out var motivo))
        {
            _soma.Foreground = Brushes.Firebrick;
            _soma.Text = char.ToUpper(motivo[0], Cultura) + motivo[1..];
            _ok.IsEnabled = false;
            return;
        }

        var soma = vaos.Sum();
        var fecha = PillarSpanForm.Closes(soma, _cobrir);

        _soma.Foreground = fecha ? Brushes.ForestGreen : Brushes.Firebrick;
        _soma.Text = PillarSpanForm.Summary(soma, _cobrir);
        _ok.IsEnabled = fecha;
    }

    private void Confirmar()
    {
        if (!PillarSpanForm.TryRead(_campos.Select(c => c.Text).ToList(), out var vaos, out _)) return;
        if (!PillarSpanForm.Closes(vaos.Sum(), _cobrir)) return;

        Vaos = vaos;
        Confirmou = true;
        DialogResult = true;
    }

    private static string Numero(double valor) => valor.ToString("0.###", Cultura);
}
