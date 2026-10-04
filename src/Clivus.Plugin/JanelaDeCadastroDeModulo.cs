using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Cadastro de um módulo novo no serviço local (passo 8.4, Melhorias.docx,
/// 01/10/2026: "botão de cadastro de módulo para ir criando biblioteca").
/// </summary>
internal sealed class JanelaDeCadastroDeModulo : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly TextBox _marca = Campo("Fabricante, como no datasheet.");
    private readonly TextBox _modelo = Campo("Modelo, como no datasheet. É por ele que o perfil de mesa acha o módulo.");
    private readonly TextBox _potencia = Campo("Potência de pico, em Wp (720, não 0,72).");
    private readonly TextBox _altura = Campo("Lado maior do módulo, em metro.");
    private readonly TextBox _largura = Campo("Lado menor do módulo, em metro.");
    private readonly TextBox _espessura = Campo("Espessura com a moldura, em metro.");
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };

    /// <summary>O módulo cadastrado, ou null se não houve cadastro.</summary>
    internal SolarModule? Cadastrado { get; private set; }

    /// <param name="partida">Medidas para começar (as que estão na janela de Mesa), ou null.</param>
    internal JanelaDeCadastroDeModulo(SolarModule? partida)
    {
        Title = "Clivus Solar — Cadastrar módulo";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(12) };

        void Linha(string rotulo, TextBox campo)
        {
            pilha.Children.Add(new TextBlock { Text = rotulo, FontSize = 12, ToolTip = campo.ToolTip });
            pilha.Children.Add(campo);
        }

        Linha("Marca", _marca);
        Linha("Modelo", _modelo);
        Linha("Potência (Wp)", _potencia);
        Linha("Altura (m)", _altura);
        Linha("Largura (m)", _largura);
        Linha("Espessura (m)", _espessura);

        pilha.Children.Add(new TextBlock
        {
            Text = $"Vai para o serviço em {FonteDeModulos.Endereco}.",
            FontSize = 11,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 0),
        });
        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cadastrar = new Button { Content = "Cadastrar", Width = 100, Height = 26, IsDefault = true, ToolTip = "Grava o módulo no serviço; ele aparece na lista de modelos da janela de Mesa." };
        cadastrar.Click += (_, _) => Cadastrar();
        botoes.Children.Add(cadastrar);
        botoes.Children.Add(new Button { Content = "Cancelar", Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        pilha.Children.Add(botoes);

        Content = pilha;

        if (partida is not null)
        {
            // Campo que não deu para ler na janela de Mesa chega como zero;
            // zero na caixa só atrapalha.
            _potencia.Text = Partida(partida.PowerWatts);
            _altura.Text = Partida(partida.Height);
            _largura.Text = Partida(partida.Width);
            _espessura.Text = Partida(partida.Thickness);
        }
    }

    private static TextBox Campo(string dica) => new() { Height = 24, Margin = new Thickness(0, 2, 0, 6), ToolTip = dica };

    private void Cadastrar()
    {
        try
        {
            if (!NumberInput.TryParseLarge(_potencia.Text, out var potencia)
                || !NumberInput.TryParseMeasure(_altura.Text, out var altura)
                || !NumberInput.TryParseMeasure(_largura.Text, out var largura)
                || !NumberInput.TryParseMeasure(_espessura.Text, out var espessura))
            {
                Dizer("Preencha potência, altura, largura e espessura com números.", erro: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(_modelo.Text))
            {
                Dizer("O modelo está em branco.", erro: true);
                return;
            }

            var modulo = new SolarModule(_marca.Text.Trim(), _modelo.Text.Trim(), potencia, altura, largura, espessura);
            var (cadastrou, mensagem) = FonteDeModulos.Cadastrar(modulo);

            if (!cadastrou)
            {
                Dizer(mensagem, erro: true);
                return;
            }

            Cadastrado = modulo;
            DialogResult = true;
        }
        catch (Exception erro)
        {
            // Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.
            RegistroDeDiagnostico.Registrar("Falha ao cadastrar módulo.", erro);
            Dizer($"Não consegui cadastrar: {erro.Message}", erro: true);
        }
    }

    private void Dizer(string recado, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = recado;
    }

    private static string Partida(double valor) =>
        double.IsFinite(valor) && valor > 0 ? valor.ToString("0.###", Brasil) : string.Empty;
}
