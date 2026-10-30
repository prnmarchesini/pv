using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Cadastro de um módulo novo no serviço local (passo 8.4, Melhorias.docx,
/// 01/10/2026: "botão de cadastro de módulo para ir criando biblioteca").
///
/// Desde 30/10/2026 (38.6) o cadastro lê o .PAN: marca, modelo, potência e
/// medidas saem dele, e o arquivo vai junto para o serviço, com a parte
/// elétrica (Renan: "esse arquivo pan já subir no servidor, esse módulo já
/// ficaria salvo no servidor [...] é usuário subindo pan para a gente").
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
    private readonly TextBlock _origemDoPan = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };

    /// <summary>O .PAN lido (a parte elétrica, o texto e o nome do arquivo), que vai junto no cadastro; null sem PAN.</summary>
    private (PanModule Eletrica, string Texto, string Nome)? _pan;

    /// <summary>O módulo cadastrado, ou null se não houve cadastro.</summary>
    internal SolarModule? Cadastrado { get; private set; }

    /// <param name="partida">Medidas para começar (as que estão na janela de Mesa), ou null.</param>
    /// <param name="pan">O .PAN que a janela de Mesa já leu (38.6), ou null.</param>
    internal JanelaDeCadastroDeModulo(SolarModule? partida, (PanModule Eletrica, string Texto, string Nome)? pan = null)
    {
        Title = Tr.T("Clivus Solar — Cadastrar módulo");
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(12) };

        var lerPan = new Button
        {
            Content = Tr.T("Ler .PAN..."),
            Height = 26,
            Margin = new Thickness(0, 0, 0, 4),
            ToolTip = Tr.T("Lê o arquivo PAN do PVsyst: marca, modelo, potência e medidas saem dele, e o arquivo vai junto para o serviço, com a parte elétrica."),
        };
        lerPan.Click += (_, _) => LerPan();
        pilha.Children.Add(lerPan);
        pilha.Children.Add(_origemDoPan);

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
            _marca.Text = partida.Brand;
            _modelo.Text = partida.Model;
            _potencia.Text = Partida(partida.PowerWatts, Brasil);
            _altura.Text = Partida(partida.Height, Tr.Culture);
            _largura.Text = Partida(partida.Width, Tr.Culture);
            _espessura.Text = Partida(partida.Thickness, Tr.Culture);
        }

        if (pan is { } doPan) UsarPan(doPan.Eletrica, doPan.Texto, doPan.Nome);
        else MostrarOrigem();
    }

    private static TextBox Campo(string dica) => new() { Height = 24, Margin = new Thickness(0, 2, 0, 6), ToolTip = dica };

    /// <summary>"Ler .PAN...": os campos saem do arquivo, e ele fica para ir junto no cadastro.</summary>
    private void LerPan()
    {
        try
        {
            var caminho = DialogoDeArquivo.Abrir(Tr.T("Arquivo PAN do módulo"), Tr.T("Módulo do PVsyst (*.pan)|*.pan|Todos os arquivos (*.*)|*.*"), null);
            if (caminho is null) return;

            var texto = PanModule.ReadText(caminho);
            if (PanModule.Parse(texto, out var faltam) is not { } eletrica)
            {
                Dizer(Tr.F("O PAN não serve: falta ou não se lê {0}.", string.Join(", ", faltam)), erro: true);
                return;
            }

            UsarPan(eletrica, texto, Path.GetFileName(caminho));
        }
        catch (Exception erro)
        {
            // Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.
            RegistroDeDiagnostico.Registrar("Falha ao ler o PAN no cadastro de módulo.", erro);
            Dizer(Tr.F("Não consegui ler o PAN: {0}", erro.Message), erro: true);
        }
    }

    private void UsarPan(PanModule eletrica, string texto, string nome)
    {
        _pan = (eletrica, texto, nome);

        _marca.Text = eletrica.Manufacturer.Trim();
        _modelo.Text = eletrica.Model.Trim();
        _potencia.Text = Partida(eletrica.Pmax, Brasil);

        if (PanModule.DimensionsOf(texto) is { } medidas)
        {
            _altura.Text = Partida(medidas.Height, Tr.Culture);
            _largura.Text = Partida(medidas.Width, Tr.Culture);
            _espessura.Text = Partida(medidas.Thickness, Tr.Culture);
            Dizer(string.Empty, erro: false);
        }
        else
        {
            Dizer(Tr.T("O PAN não traz as medidas do módulo (Height, Width, Depth): preencha altura, largura e espessura."), erro: true);
        }

        MostrarOrigem();
    }

    private void MostrarOrigem() =>
        _origemDoPan.Text = _pan is { } pan
            ? Tr.F("Do PAN {0}: {1}. O arquivo vai junto para o serviço.", pan.Nome, pan.Eletrica.Describe())
            : Tr.T("Sem PAN: só as medidas vão para o serviço.");

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

            // O PAN só vai junto se ainda é o mesmo módulo (o modelo não foi trocado à mão).
            var pan = _pan is { } lido && string.Equals(lido.Eletrica.Model.Trim(), modulo.Model, StringComparison.OrdinalIgnoreCase) ? lido : ((PanModule, string, string)?)null;
            var (cadastrou, mensagem) = FonteDeModulos.Cadastrar(modulo, pan?.Item1, pan?.Item2, pan?.Item3);

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
