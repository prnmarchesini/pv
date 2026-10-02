using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A regra de uma análise (passos 8.9 a 8.11): "inferior a X, pintar de
/// (escolher cor); superior a X, pintar de (escolher cor)", e se pinta também
/// as peças ou só os textos.
/// </summary>
internal sealed class JanelaDeAnalise : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IndependentKind _tipo;
    private readonly CheckBox _usarAbaixo = new() { Content = "Abaixo de", VerticalAlignment = VerticalAlignment.Center, Width = 90 };
    private readonly TextBox _abaixo = new() { Width = 70, Height = 24, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _corAbaixo;
    private readonly CheckBox _usarAcima = new() { Content = "Acima de", VerticalAlignment = VerticalAlignment.Center, Width = 90 };
    private readonly TextBox _acima = new() { Width = 70, Height = 24, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _corAcima;
    private readonly CheckBox _pecas = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly ComboBox _unidade = new() { Width = 140, Height = 24, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _recado = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    internal ThresholdRule? Regra { get; private set; }

    internal SlopeUnit Unidade { get; private set; }

    internal JanelaDeAnalise(IndependentKind tipo, ThresholdRule atual, SlopeUnit unidade)
    {
        _tipo = tipo;
        Unidade = unidade;

        Title = "UFV — Analisar " + IndependentAnalysis.Name(tipo);
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _corAbaixo = PaletaDeCores.Caixa(atual.BelowColor, "Cor de quem fica abaixo do limite.");
        _corAcima = PaletaDeCores.Caixa(atual.AboveColor, "Cor de quem fica acima do limite.");

        var unidadeDoValor = tipo == IndependentKind.Slope ? "" : " (m)";
        _abaixo.ToolTip = $"Limite de baixo{unidadeDoValor}. Igual ao limite conta como dentro.";
        _acima.ToolTip = $"Limite de cima{unidadeDoValor}. Igual ao limite conta como dentro.";
        _usarAbaixo.ToolTip = "Desmarcado: nada é pintado por estar baixo.";
        _usarAcima.ToolTip = "Desmarcado: nada é pintado por estar alto.";

        _usarAbaixo.IsChecked = atual.Below is not null;
        _abaixo.Text = atual.Below is { } b ? b.ToString("0.###", Brasil) : "";
        _usarAcima.IsChecked = atual.Above is not null;
        _acima.Text = atual.Above is { } a ? a.ToString("0.###", Brasil) : "";

        _pecas.Content = tipo switch
        {
            IndependentKind.Slope => "Pintar também o contorno da mesa",
            IndependentKind.PillarAbove or IndependentKind.PillarBuried or IndependentKind.PillarLength => "Pintar também os pilares",
            _ => "Pintar também os módulos (não só os textos)",
        };
        _pecas.ToolTip = "Desmarcado, só os textos da análise mudam de cor.";
        _pecas.IsChecked = atual.PaintPieces;

        var pilha = new StackPanel { Margin = new Thickness(12) };

        if (tipo == IndependentKind.Slope)
        {
            _unidade.Items.Add(new ComboBoxItem { Content = "Porcentagem (%)", Tag = SlopeUnit.Percent });
            _unidade.Items.Add(new ComboBoxItem { Content = "Graus (°)", Tag = SlopeUnit.Degrees });
            _unidade.SelectedIndex = unidade == SlopeUnit.Degrees ? 1 : 0;
            _unidade.ToolTip = "A unidade dos textos e dos limites. Trocar a unidade pede inserir os textos de novo.";
            pilha.Children.Add(new TextBlock { Text = "Unidade" });
            pilha.Children.Add(_unidade);
        }

        pilha.Children.Add(Linha(_usarAbaixo, _abaixo, _corAbaixo));
        pilha.Children.Add(Linha(_usarAcima, _acima, _corAcima));
        pilha.Children.Add(_pecas);
        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Analisar", Width = 90, Height = 26, IsDefault = true, ToolTip = "Grava a regra no desenho e pinta." };
        ok.Click += (_, _) => Confirmar();
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = "Cancelar", Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        pilha.Children.Add(botoes);

        Content = pilha;
    }

    private static StackPanel Linha(CheckBox usar, TextBox valor, ComboBox cor)
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        linha.Children.Add(usar);
        linha.Children.Add(valor);
        linha.Children.Add(new TextBlock { Text = "pintar de", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        linha.Children.Add(cor);
        return linha;
    }

    private void Confirmar()
    {
        try
        {
            double? abaixo = null, acima = null;

            if (_usarAbaixo.IsChecked == true)
            {
                if (!NumberInput.TryParseMeasure(_abaixo.Text, out var v)) { _recado.Text = "Não consigo ler o limite de baixo."; return; }
                abaixo = v;
            }

            if (_usarAcima.IsChecked == true)
            {
                if (!NumberInput.TryParseMeasure(_acima.Text, out var v)) { _recado.Text = "Não consigo ler o limite de cima."; return; }
                acima = v;
            }

            var regra = new ThresholdRule(abaixo, PaletaDeCores.Cor(_corAbaixo), acima, PaletaDeCores.Cor(_corAcima), _pecas.IsChecked == true);

            if (regra.WhyInvalid is { } motivo)
            {
                _recado.Text = char.ToUpper(motivo[0], Brasil) + motivo[1..] + ".";
                return;
            }

            if (_tipo == IndependentKind.Slope && (_unidade.SelectedItem as ComboBoxItem)?.Tag is SlopeUnit u) Unidade = u;

            Regra = regra;
            DialogResult = true;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela da regra de análise.", erro);
            _recado.Text = erro.Message;
        }
    }
}
