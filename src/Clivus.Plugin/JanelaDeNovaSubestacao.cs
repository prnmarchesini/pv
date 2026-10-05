using System.Windows;
using System.Windows.Controls;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>A pergunta do Adicionar subestação (12.2): uma compartilhada ou várias unitárias.</summary>
internal sealed class JanelaDeNovaSubestacao : Window
{
    private readonly RadioButton _compartilhada = new() { Content = Tr.T("Uma subestação compartilhada (C1, C2...): recebe um ou mais trafos"), IsChecked = true, Margin = new Thickness(0, 0, 0, 6) };
    private readonly RadioButton _unitarias = new() { Content = Tr.T("Várias subestações unitárias (U1, U2...): cada bloquinho com o seu trafo"), Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBox _quantas = new() { Text = "2", Width = 60, Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _aviso = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    /// <summary>Quantas unitárias; null = uma compartilhada.</summary>
    internal int? Unitarias { get; private set; }

    internal JanelaDeNovaSubestacao()
    {
        Title = Tr.T("Adicionar subestação");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var quantas = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 8) };
        quantas.Children.Add(new TextBlock { Text = Tr.T("Quantas:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        quantas.Children.Add(_quantas);

        var ok = new Button { Content = "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) =>
        {
            try { Confirmar(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha na pergunta da subestação nova.", erro); }
        };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 90, IsCancel = true });

        var painel = new StackPanel { Margin = new Thickness(14) };
        painel.Children.Add(_compartilhada);
        painel.Children.Add(_unitarias);
        painel.Children.Add(quantas);
        painel.Children.Add(_aviso);
        painel.Children.Add(botoes);
        Content = painel;

        _quantas.GotFocus += (_, _) => _unitarias.IsChecked = true;
    }

    private void Confirmar()
    {
        if (_unitarias.IsChecked == true)
        {
            if (!NumberInput.TryParseCount(_quantas.Text, out var n) || n < 1 || n > ElectricalDefaults.MaxAtOnce)
            {
                _aviso.Text = Tr.F("Quantas: um número inteiro de 1 a {0}.", ElectricalDefaults.MaxAtOnce);
                return;
            }

            Unitarias = n;
        }

        DialogResult = true;
    }
}
