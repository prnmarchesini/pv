using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A janela de Configurações (passo 8.7, Melhorias.docx, 01/10/2026: "um
/// botão para configurar os parâmetros da simulação, que abre um modal, e
/// nesse modal tem vários menus, tipo janelas do Chrome"). Quatro abas:
/// <list type="number">
/// <item>Estruturas: as mesas do desenho (nova, editar na janela de Mesa, remover).</item>
/// <item>Escolha das estruturas: quais entram na usina e a cor de cada uma.</item>
/// <item>Parâmetros: azimute, pitch, degraus, espaçamentos, altura livre, declividade.</item>
/// <item>Projeto: os estilos de texto, cota e chamada.</item>
/// </list>
/// Nada vai para o desenho até "Salvar no desenho".
/// </summary>
internal sealed class JanelaDeConfiguracoes : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly List<DrawingTable> _mesas;
    private readonly Func<TableProfile, TableProfile?> _editarMesa;
    private readonly JanelaDeConfiguracao _parametros;
    private readonly PainelDeEstilos _estilos;

    private readonly ListBox _lista = new() { MinHeight = 220 };
    private readonly StackPanel _escolha = new();
    private readonly TextBlock _recado = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    /// <summary>O que o usuário mandou salvar, ou null se fechou sem salvar.</summary>
    internal (IReadOnlyList<DrawingTable> Mesas, ProjectSettings Parametros, ProjectStyles Estilos)? Salvo { get; private set; }

    /// <param name="mesas">As mesas do desenho.</param>
    /// <param name="parametros">A configuração gravada no desenho.</param>
    /// <param name="aviso">Aviso da leitura da configuração, ou null.</param>
    /// <param name="estilos">Os estilos do desenho (texto, cota, chamada) e os escolhidos.</param>
    /// <param name="editarMesa">Abre a janela de Mesa com o perfil dado; o perfil confirmado, ou null.</param>
    /// <param name="abaInicial">A aba que abre na frente (0 a 3).</param>
    internal JanelaDeConfiguracoes(
        IReadOnlyList<DrawingTable> mesas,
        ProjectSettings parametros,
        string? aviso,
        (IReadOnlyList<string> Textos, IReadOnlyList<string> Cotas, IReadOnlyList<string> Chamadas, ProjectStyles Atuais) estilos,
        Func<TableProfile, TableProfile?> editarMesa,
        int abaInicial = 0)
    {
        _mesas = [.. mesas];
        _editarMesa = editarMesa;

        Title = "UFV — Configurações";
        Width = 900;
        Height = 560;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _parametros = new JanelaDeConfiguracao(parametros, aviso, soAnalises: false, embutida: true);
        _estilos = new PainelDeEstilos(estilos.Textos, estilos.Cotas, estilos.Chamadas, estilos.Atuais);

        var abas = new TabControl { Margin = new Thickness(10, 10, 10, 0) };
        abas.Items.Add(new TabItem { Header = "Estruturas", Content = AbaEstruturas(), ToolTip = "As mesas cadastradas neste desenho: módulo, arranjo, tesoura, pilares, vãos e enterro." });
        abas.Items.Add(new TabItem { Header = "Escolha das estruturas", Content = AbaEscolha(), ToolTip = "Quais mesas entram na usina e a cor de cada uma no desenho." });
        abas.Items.Add(new TabItem { Header = "Parâmetros", Content = _parametros.Formulario(), ToolTip = "Azimute, pitch, degraus, espaçamentos, altura livre e declividade." });
        abas.Items.Add(new TabItem { Header = "Projeto", Content = new ScrollViewer { Content = _estilos, Margin = new Thickness(12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, ToolTip = "Os estilos (anotativos) dos textos, cotas e chamadas do plugin." });
        abas.SelectedIndex = Math.Clamp(abaInicial, 0, 3);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
        var salvar = new Button { Content = "Salvar no desenho", Width = 150, Height = 26, IsDefault = true, ToolTip = "Grava as mesas, os parâmetros e os estilos no desenho." };
        salvar.Click += (_, _) => Salvar();
        botoes.Children.Add(salvar);
        botoes.Children.Add(new Button { Content = "Fechar", Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true, ToolTip = "Fecha sem gravar nada." });

        var raiz = new DockPanel();
        DockPanel.SetDock(botoes, Dock.Bottom);
        raiz.Children.Add(botoes);
        DockPanel.SetDock(_recado, Dock.Bottom);
        _recado.Margin = new Thickness(12, 0, 12, 0);
        raiz.Children.Add(_recado);
        raiz.Children.Add(abas);

        Content = raiz;

        // A janela dos parâmetros nunca é mostrada; fechá-la junto evita que
        // ela fique pendurada na aplicação a cada abertura.
        Closed += (_, _) => _parametros.Close();

        Atualizar();
    }

    // ------------------------------------------------------------- abas

    private UIElement AbaEstruturas()
    {
        var painel = new DockPanel { Margin = new Thickness(12) };

        var botoes = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Width = 170 };

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 28, Margin = new Thickness(0, 0, 0, 6), ToolTip = dica };
            b.Click += (_, _) => Tentar(acao);
            botoes.Children.Add(b);
            return b;
        }

        Botao("Nova mesa...", "Abre a janela de Mesa para cadastrar uma mesa neste desenho (pode partir de um perfil salvo).", Nova);
        Botao("Editar...", "Abre a mesa escolhida na janela de Mesa: módulo, arranjo, tesoura, pilares, vãos P1-P2, enterro T3.", Editar);
        Botao("Duplicar", "Copia a mesa escolhida com outro nome (para fazer a de 14 a partir da de 28).", Duplicar);
        Botao("Remover", "Tira a mesa escolhida deste desenho (as mesas já desenhadas continuam como estão).", Remover);

        botoes.Children.Add(new TextBlock
        {
            Text = "As mesas ficam gravadas no desenho. Os perfis da pasta do usuário são a biblioteca de onde trazer uma mesa.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            FontSize = 11,
            Margin = new Thickness(0, 10, 0, 0),
        });

        DockPanel.SetDock(botoes, Dock.Right);
        painel.Children.Add(botoes);

        _lista.MouseDoubleClick += (_, _) => Tentar(Editar);
        painel.Children.Add(_lista);

        return painel;
    }

    private UIElement AbaEscolha()
    {
        var painel = new StackPanel { Margin = new Thickness(12) };

        painel.Children.Add(new TextBlock
        {
            Text = "Marque as mesas que entram na usina. Com mais de uma, o motor escolhe por trecho de fileira a combinação que põe mais módulos (as compridas primeiro). Sem nenhuma marcada, vale a mesa da janela de Mesa.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        painel.Children.Add(new ScrollViewer { Content = _escolha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 480 });
        return painel;
    }

    private void Atualizar()
    {
        var escolhida = _lista.SelectedIndex;

        _lista.Items.Clear();
        _escolha.Children.Clear();

        for (var i = 0; i < _mesas.Count; i++)
        {
            var mesa = _mesas[i];
            var indice = i;

            _lista.Items.Add(new ListBoxItem
            {
                Content = $"{mesa.Name} — {mesa.Profile.Layout.ModuleCount} módulos, {mesa.Profile.Layout.Length.ToString("0.###", Brasil)} m, "
                    + $"{mesa.Profile.TiltDegrees.ToString("0.#", Brasil)}°{(mesa.Use ? "  (em uso)" : "")}",
                ToolTip = mesa.Profile.Describe(),
            });

            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

            var usar = new CheckBox
            {
                IsChecked = mesa.Use,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 26,
                ToolTip = "Marcada, a mesa entra na usina.",
            };
            usar.Checked += (_, _) => Usar(indice, true);
            usar.Unchecked += (_, _) => Usar(indice, false);

            var cor = PaletaDeCores.Caixa(mesa.Color, "A cor do contorno desta mesa no desenho, para saber qual é qual.");
            cor.Width = 140;
            cor.SelectionChanged += (_, _) => _mesas[indice] = _mesas[indice] with { Color = PaletaDeCores.Cor(cor) };

            linha.Children.Add(usar);
            linha.Children.Add(cor);
            linha.Children.Add(new TextBlock
            {
                Text = $"  {mesa.Name} — {mesa.Profile.Layout.ModuleCount} módulos, {mesa.Profile.Layout.Module.DisplayName}",
                VerticalAlignment = VerticalAlignment.Center,
            });

            _escolha.Children.Add(linha);
        }

        if (_mesas.Count == 0)
        {
            _lista.Items.Add(new ListBoxItem { Content = "(nenhuma mesa neste desenho: use Nova mesa...)", IsEnabled = false });
            _escolha.Children.Add(new TextBlock { Text = "Nenhuma mesa cadastrada neste desenho ainda.", Foreground = Brushes.Gray });
        }

        if (escolhida >= 0 && escolhida < _mesas.Count) _lista.SelectedIndex = escolhida;
    }

    /// <summary>Marca ou desmarca a mesa e atualiza a linha dela na aba Estruturas.</summary>
    private void Usar(int indice, bool usar)
    {
        _mesas[indice] = _mesas[indice] with { Use = usar };

        if (indice < _lista.Items.Count && _lista.Items[indice] is ListBoxItem item)
        {
            var mesa = _mesas[indice];
            item.Content = $"{mesa.Name} — {mesa.Profile.Layout.ModuleCount} módulos, {mesa.Profile.Layout.Length.ToString("0.###", Brasil)} m, "
                + $"{mesa.Profile.TiltDegrees.ToString("0.#", Brasil)}°{(mesa.Use ? "  (em uso)" : "")}";
        }
    }

    // ------------------------------------------------------------- ações

    private DrawingTable? Selecionada() =>
        _lista.SelectedIndex >= 0 && _lista.SelectedIndex < _mesas.Count ? _mesas[_lista.SelectedIndex] : null;

    private void Nova()
    {
        var partida = _mesas.Count > 0 ? _mesas[^1].Profile : MesaCommands.MesaDeExemplo();
        var perfil = _editarMesa(partida with { Name = NomeLivre(partida.Name) });
        if (perfil is null) return;

        _mesas.Add(new DrawingTable(perfil with { Name = NomeLivre(perfil.Name) }, DrawingTables.NextColor(_mesas), Use: true));
        Atualizar();
        _lista.SelectedIndex = _mesas.Count - 1;
    }

    private void Editar()
    {
        if (Selecionada() is not { } mesa)
        {
            _recado.Text = "Escolha uma mesa na lista.";
            return;
        }

        var perfil = _editarMesa(mesa.Profile);
        if (perfil is null) return;

        var outras = _mesas.Where(m => !ReferenceEquals(m, mesa)).ToList();
        var nome = DrawingTables.Find(outras, perfil.Name) is null ? perfil.Name : NomeLivre(perfil.Name);

        _mesas[_lista.SelectedIndex] = mesa with { Profile = perfil with { Name = nome } };
        Atualizar();
    }

    private void Duplicar()
    {
        if (Selecionada() is not { } mesa)
        {
            _recado.Text = "Escolha uma mesa na lista.";
            return;
        }

        _mesas.Add(new DrawingTable(mesa.Profile with { Name = NomeLivre(mesa.Name + " (cópia)") }, DrawingTables.NextColor(_mesas), Use: false));
        Atualizar();
    }

    private void Remover()
    {
        if (Selecionada() is not { } mesa)
        {
            _recado.Text = "Escolha uma mesa na lista.";
            return;
        }

        _mesas.RemoveAt(_lista.SelectedIndex);
        Atualizar();
    }

    private string NomeLivre(string desejado)
    {
        var nome = string.IsNullOrWhiteSpace(desejado) ? "Mesa" : desejado.Trim();
        if (DrawingTables.Find(_mesas, nome) is null) return nome;

        for (var n = 2; ; n++)
        {
            var candidato = $"{nome} {n}";
            if (DrawingTables.Find(_mesas, candidato) is null) return candidato;
        }
    }

    private void Salvar()
    {
        Tentar(() =>
        {
            if (DrawingTables.WhyInvalid(_mesas) is { } motivoDasMesas)
            {
                _recado.Text = "Estruturas: " + motivoDasMesas + ".";
                return;
            }

            var emUso = _mesas.Where(m => m.Use).ToList();

            if (emUso.Select(m => Math.Round(m.Profile.TiltDegrees, 3)).Distinct().Count() > 1)
            {
                _recado.Text = "Escolha das estruturas: as mesas em uso precisam ter a mesma inclinação.";
                return;
            }

            var parametros = _parametros.Validar(out var motivo);

            if (parametros is null)
            {
                _recado.Text = "Parâmetros: " + motivo;
                return;
            }

            Salvo = (_mesas, parametros, _estilos.Ler());
            DialogResult = true;
        });
    }

    /// <summary>Manipulador de clique do WPF: exceção solta aqui fecha o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            _recado.Text = string.Empty;
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de Configurações.", erro);
            _recado.Text = erro.Message;
        }
    }
}
