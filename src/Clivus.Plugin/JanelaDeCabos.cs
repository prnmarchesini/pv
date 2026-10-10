using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A biblioteca de cabos (roteamento, 22.1): a lista à esquerda, o cabo
/// escolhido à direita para editar. Grava no arquivo do usuário
/// (<see cref="CableLibrary"/>). Os valores da lista de partida são de
/// catálogo e da norma, para o projetista revisar: a janela diz isso.
/// </summary>
internal sealed class JanelaDeCabos : Window
{
    private readonly string _caminho;
    private readonly List<Cable> _cabos;

    /// <summary>A biblioteca do arquivo não se leu inteira: gravar por cima perderia o que não se leu.</summary>
    private readonly string? _ilegivel;
    private readonly ListBox _lista = new() { MinWidth = 300 };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    private readonly TextBox _nome = new();
    private readonly ComboBox _tipo = new();
    private readonly TextBox _secao = new();
    private readonly TextBox _isolacao = new();
    private readonly TextBox _r20 = new();
    private readonly TextBox _rop = new();
    private readonly TextBox _x = new();
    private readonly TextBox _formacao = new();
    private readonly TextBox _condutor = new();
    private readonly TextBox _material = new();
    private readonly TextBox _capacidade = new();

    internal JanelaDeCabos(string caminho)
    {
        _caminho = caminho;
        _cabos = [.. CableLibrary.Load(caminho, out var problema)];
        _ilegivel = problema;

        Title = Tr.T("Biblioteca de cabos — Clivus Solar");
        Width = 980;
        Height = 620;
        MinWidth = 760;
        MinHeight = 440;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        foreach (var t in Enum.GetValues<CableType>()) _tipo.Items.Add(new ComboBoxItem { Content = NomeDoTipo(t), Tag = t });

        var aviso = new TextBlock
        {
            Text = Tr.T("Valores de partida: resistências de catálogo (corrigidas para 90 °C na operação) e corrente admissível da NBR 5410 e de catálogo. Revise e corrija antes de usar no memorial."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkGoldenrod,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Linha(string rotulo, Control campo, string? dica = null)
        {
            var n = grade.RowDefinitions.Count;
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var texto = new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4), ToolTip = dica };
            campo.Margin = new Thickness(0, 0, 0, 4);
            campo.ToolTip = dica;
            Grid.SetRow(texto, n);
            Grid.SetRow(campo, n);
            Grid.SetColumn(campo, 1);
            grade.Children.Add(texto);
            grade.Children.Add(campo);
        }

        Linha(Tr.T("Nome"), _nome);
        Linha(Tr.T("Tipo"), _tipo, Tr.T("CC para as strings, CA para inversor → trafo, MT para trafo → subestação."));
        Linha(Tr.T("Seção (mm²)"), _secao);
        Linha(Tr.T("Tensão de isolamento"), _isolacao, Tr.T("Ex.: 1,8 kV CC; 0,6/1 kV; 8,7/15 kV."));
        Linha(Tr.T("Resistência a 20 °C (ohm/km)"), _r20);
        Linha(Tr.T("Resistência na operação (ohm/km)"), _rop, Tr.T("É a que entra na queda de tensão."));
        Linha(Tr.T("Reatância indutiva (ohm/km)"), _x);
        Linha(Tr.T("Formação"), _formacao, Tr.T("Ex.: 1x6, 3x1x95."));
        Linha(Tr.T("Condutor"), _condutor, Tr.T("Cobre ou alumínio."));
        Linha(Tr.T("Isolação"), _material, Tr.T("EPR, XLPE, PVC."));
        Linha(Tr.T("Corrente admissível por método (A)"), _capacidade, Tr.T("Método=ampères, separados por ponto e vírgula. Ex.: B1=66; D=58"));

        var botoes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, ToolTip = dica, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 6, 6) };
            b.Click += (_, _) =>
            {
                try
                {
                    acao();
                }
                catch (Exception erro)
                {
                    RegistroDeDiagnostico.Registrar($"Falha no botão {texto} da biblioteca de cabos.", erro);
                    Avisar(Tr.F("Não consegui: {0}", erro.Message), true);
                }
            };
            botoes.Children.Add(b);
        }

        Botao(Tr.T("Salvar"), Tr.T("Grava o cabo editado (ou o novo) na biblioteca."), Salvar);
        Botao(Tr.T("Novo (cópia)"), Tr.T("Um cabo novo a partir do escolhido."), Novo);
        Botao(Tr.T("Apagar"), Tr.T("Tira o cabo escolhido da biblioteca (os desenhos que já usam guardam a cópia deles)."), Apagar);
        Botao(Tr.T("Restaurar a lista de partida"), Tr.T("Volta a biblioteca à lista de partida (perde as edições)."), Restaurar);

        var direita = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        direita.Children.Add(grade);
        direita.Children.Add(botoes);

        var colunas = new Grid();
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        var rolagem = new ScrollViewer { Content = direita, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(rolagem, 1);
        colunas.Children.Add(_lista);
        colunas.Children.Add(rolagem);

        var raiz = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(aviso, Dock.Top);
        DockPanel.SetDock(_recado, Dock.Bottom);
        raiz.Children.Add(aviso);
        raiz.Children.Add(_recado);
        raiz.Children.Add(colunas);
        Content = raiz;

        _lista.SelectionChanged += (_, _) =>
        {
            try
            {
                if (_lista.SelectedItem is ListBoxItem { Tag: Cable c }) Preencher(c);
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao escolher cabo na biblioteca.", erro);
            }
        };

        Listar(null);
        if (problema is not null) Avisar(Tr.F("{0}. A biblioteca não será gravada por cima: corrija ou apague o arquivo {1} (ou use Restaurar a lista de partida).", problema, caminho), true);
    }

    private static string NomeDoTipo(CableType t) => t switch
    {
        CableType.Dc => Tr.T("CC"),
        CableType.Ac => Tr.T("CA"),
        _ => Tr.T("MT"),
    };

    private void Listar(Guid? escolher)
    {
        _lista.Items.Clear();
        foreach (var c in _cabos.OrderBy(c => c.Type).ThenBy(c => c.SectionMm2).ThenBy(c => c.Name, StringComparer.CurrentCulture))
        {
            var item = new ListBoxItem { Content = NomeDoTipo(c.Type) + " · " + c.Name, Tag = c };
            _lista.Items.Add(item);
            if (c.Id == escolher) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
    }

    private void Preencher(Cable c)
    {
        string N(double v) => v.ToString("0.####", Tr.Culture);
        _nome.Text = c.Name;
        _tipo.SelectedItem = _tipo.Items.Cast<ComboBoxItem>().First(i => (CableType)i.Tag! == c.Type);
        _secao.Text = N(c.SectionMm2);
        _isolacao.Text = c.Insulation;
        _r20.Text = N(c.Resistance20);
        _rop.Text = N(c.ResistanceOperating);
        _x.Text = N(c.Reactance);
        _formacao.Text = c.Formation;
        _condutor.Text = c.Conductor;
        _material.Text = c.InsulationMaterial;
        _capacidade.Text = string.Join("; ", c.Ampacity.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + N(k.Value)));
    }

    /// <summary>O cabo do formulário, ou null e o recado do que não se leu.</summary>
    private Cable? Ler(Guid id)
    {
        if (!NumberInput.TryParseMeasure(_secao.Text, out var s) || !NumberInput.TryParseMeasure(_r20.Text, out var r20)
            || !NumberInput.TryParseMeasure(_rop.Text, out var rop) || !NumberInput.TryParseMeasure(_x.Text, out var x))
        {
            Avisar(Tr.T("Não consigo ler a seção, as resistências ou a reatância."), true);
            return null;
        }

        if (Cable.ParseAmpacity(_capacidade.Text, Tr.Culture) is not { } mapa)
        {
            Avisar(Tr.T("Não consigo ler a corrente admissível: use Método=ampères separados por ponto e vírgula (ex.: B1=66; D=58)."), true);
            return null;
        }

        var tipo = _tipo.SelectedItem is ComboBoxItem { Tag: CableType t } ? t : CableType.Dc;
        var cabo = new Cable(id, _nome.Text.Trim(), tipo, s, _isolacao.Text.Trim(), r20, rop, x, _formacao.Text.Trim(), _condutor.Text.Trim(), _material.Text.Trim(), mapa);
        if (!cabo.IsValid)
        {
            Avisar(Tr.T("Nome em branco, ou número zero ou negativo."), true);
            return null;
        }

        return cabo;
    }

    private void Salvar()
    {
        var id = _lista.SelectedItem is ListBoxItem { Tag: Cable atual } ? atual.Id : Guid.NewGuid();
        if (Ler(id) is not { } cabo) return;
        if (_cabos.Any(c => c.Id != id && string.Equals(c.Name, cabo.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            Avisar(Tr.F("Já há um cabo chamado {0}.", cabo.Name), true);
            return;
        }

        _cabos.RemoveAll(c => c.Id == id);
        _cabos.Add(cabo);
        Gravar(cabo.Id, Tr.F("Cabo {0} salvo.", cabo.Name));
    }

    private void Novo()
    {
        if (Ler(Guid.NewGuid()) is not { } base_) return;
        var nome = base_.Name;
        for (var n = 2; _cabos.Any(c => string.Equals(c.Name, nome, StringComparison.CurrentCultureIgnoreCase)); n++) nome = Tr.F("{0} ({1})", base_.Name, n);
        var novo = base_ with { Name = nome };
        _cabos.Add(novo);
        Gravar(novo.Id, Tr.F("Cabo {0} criado: edite e salve.", novo.Name));
    }

    private void Apagar()
    {
        if (_lista.SelectedItem is not ListBoxItem { Tag: Cable c }) return;
        _cabos.RemoveAll(x => x.Id == c.Id);
        Gravar(null, Tr.F("Cabo {0} tirado da biblioteca.", c.Name));
    }

    private void Restaurar()
    {
        _cabos.Clear();
        _cabos.AddRange(CableLibrary.Default());
        Gravar(null, Tr.T("Biblioteca de volta à lista de partida."), restaurando: true);
    }

    private void Gravar(Guid? escolher, string frase, bool restaurando = false)
    {
        if (_ilegivel is not null && !restaurando)
        {
            Avisar(Tr.F("Não gravei: a biblioteca do arquivo está ilegível ({0}).", _ilegivel), true);
            return;
        }

        CableLibrary.Save(_caminho, _cabos);
        Listar(escolher);
        Avisar(frase, false);
    }

    private void Avisar(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }
}
