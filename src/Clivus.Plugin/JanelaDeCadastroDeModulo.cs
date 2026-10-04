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

    private readonly TextBox _marca = Campo(Tr.T("Fabricante, como no datasheet."));
    private readonly TextBox _modelo = Campo(Tr.T("Modelo, como no datasheet. É por ele que o perfil de mesa acha o módulo."));
    private readonly TextBox _potencia = Campo(Tr.T("Potência de pico, em Wp (720, não 0,72)."));
    private readonly TextBox _altura = Campo(Tr.T("Lado maior do módulo, em metro."));
    private readonly TextBox _largura = Campo(Tr.T("Lado menor do módulo, em metro."));
    private readonly TextBox _espessura = Campo(Tr.T("Espessura com a moldura, em metro."));
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };

    /// <summary>O módulo cadastrado, ou null se não houve cadastro.</summary>
    internal SolarModule? Cadastrado { get; private set; }

    /// <param name="partida">Medidas para começar (as que estão na janela de Mesa), ou null.</param>
    internal JanelaDeCadastroDeModulo(SolarModule? partida)
    {
        Title = Tr.T("Clivus Solar — Cadastrar módulo");
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

        Linha(Tr.T("Marca"), _marca);
        Linha(Tr.T("Modelo"), _modelo);
        Linha(Tr.T("Potência (Wp)"), _potencia);
        Linha(Tr.T("Altura (m)"), _altura);
        Linha(Tr.T("Largura (m)"), _largura);
        Linha(Tr.T("Espessura (m)"), _espessura);

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.F("Vai para o serviço em {0}.", FonteDeModulos.Endereco),
            FontSize = 11,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 0),
        });
        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cadastrar = new Button { Content = Tr.T("Cadastrar"), Width = 100, Height = 26, IsDefault = true, ToolTip = Tr.T("Grava o módulo no serviço; ele aparece na lista de modelos da janela de Mesa.") };
        cadastrar.Click += (_, _) => Cadastrar();
        botoes.Children.Add(cadastrar);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        pilha.Children.Add(botoes);

        Content = pilha;

        if (partida is not null)
        {
            // Campo que não deu para ler na janela de Mesa chega como zero;
            // zero na caixa só atrapalha.
            // A potência fica na cultura brasileira: o ponto, na leitura dela
            // (NumberInput.TryParseLarge), é milhar, e "545.5" não seria lido.
            _potencia.Text = Partida(partida.PowerWatts, Brasil);
            _altura.Text = Partida(partida.Height, Tr.Culture);
            _largura.Text = Partida(partida.Width, Tr.Culture);
            _espessura.Text = Partida(partida.Thickness, Tr.Culture);
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
                Dizer(Tr.T("Preencha potência, altura, largura e espessura com números."), erro: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(_modelo.Text))
            {
                Dizer(Tr.T("O modelo está em branco."), erro: true);
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
            Dizer(Tr.F("Não consegui cadastrar: {0}", erro.Message), erro: true);
        }
    }

    private void Dizer(string recado, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = recado;
    }

    private static string Partida(double valor, CultureInfo cultura) =>
        double.IsFinite(valor) && valor > 0 ? valor.ToString("0.###", cultura) : string.Empty;
}
