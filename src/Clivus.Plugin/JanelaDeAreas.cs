using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela Áreas (etapa 41, Renan, 30/10/2026: "um botão, pequeno, de lista
/// de áreas, pq nesse caso real vou ter 7 áreas [...] quero ver elas, quero ter
/// a opção do sistema listar o resumo de cada área, de marcar cada área com
/// rachura, do resumo sair por área, enfim, algo completo").
/// <para>
/// Em cima, uma linha por área (a cor dela, área em ha, mesas, módulos, kWp,
/// pilares, tipos de mesa, se segue os objetos do cliente, se está
/// hachurada), a das mesas de fora das áreas e o total. Embaixo, o resumo da
/// linha escolhida em colunas: por tipo de mesa, pilares e situação, área e
/// ocupação. Os botões: zoom, hachurar e tirar a hachura (da escolhida e de
/// todas), renomear e o Excel por área.
/// </para>
/// <para>
/// Solta, como as da drenagem: dá para olhar o desenho, gerar uma área e
/// voltar (o Atualizar lê de novo). Todo clique passa por <see cref="Tentar"/>
/// (exceção solta num manipulador do WPF fecha o Civil 3D), e toda escrita é
/// de <see cref="AreasDoDesenho"/>, com o documento travado e o vigia calado.
/// Compacta (regra de 02/10/2026): a lista do tamanho das áreas, o resumo em
/// colunas, rótulo ao lado do valor.
/// </para>
/// </summary>
internal sealed class JanelaDeAreas : Window
{
    private static readonly Dictionary<Document, JanelaDeAreas> Abertas = [];

    /// <summary>Uma linha da lista, com os números já escritos na cultura da tela; <paramref name="Apagavel"/> só na linha de área.</summary>
    internal sealed record Linha(
        LinhaDasAreas Origem, Brush Amostra, string Nome, string Hectares, string Mesas, string Modulos, string Kwp, string Pilares,
        string Tipos, string Objetos, string Hachura, bool Total, string Dica, bool Apagavel);

    /// <summary>Uma linha da contagem por tipo de mesa.</summary>
    internal sealed record LinhaDoTipo(string Tipo, string Mesas, string Modulos, string Kwp);

    private readonly Document _documento;
    private LeituraDasAreas? _leitura;

    // A lista edita só o nome da área (F2 ou o clique lento, como nas pastas do
    // Windows, 31/10/2026); as outras colunas são só leitura.
    private readonly DataGrid _lista = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        CanUserSortColumns = false,
        CanUserReorderColumns = false,
        CanUserResizeRows = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        SelectionMode = DataGridSelectionMode.Single,
        SelectionUnit = DataGridSelectionUnit.FullRow,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        HorizontalGridLinesBrush = Brushes.Gainsboro,
        MaxHeight = 330,
    };

    private readonly DataGrid _tipos = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserSortColumns = false,
        CanUserReorderColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.None,
        MaxHeight = 160,
        Margin = new Thickness(6, 4, 6, 6),
    };

    private readonly Dictionary<string, TextBlock> _valores = [];
    private readonly TextBlock _titulo = new() { FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _avisos = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    // As ações de uma área ficam na linha dela (31/10/2026, Renan: "As ações
    // precisam estar na linha (apagar, renomear, regerar, etc). Dois cliques dá
    // zoom, símbolo de lixeira para apagar com aviso, [...] F2, renomear,
    // botão direito, opções de hachurar área, tirar hachura, regerar, apagar"):
    // o duplo clique, a lixeira, o F2 e o menu do botão direito. Embaixo,
    // as de todas as áreas.
    private readonly DataGridTextColumn _colunaNome;

    // O clique devagar no nome (o segundo, depois do tempo do duplo clique).
    private readonly System.Windows.Threading.DispatcherTimer _cliqueDevagar = new() { Interval = TimeSpan.FromMilliseconds(TempoDoDuploClique()) };
    private Linha? _linhaDoCliqueDevagar;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>O tempo do duplo clique do Windows, em milissegundos (500 sem ele).</summary>
    private static double TempoDoDuploClique()
    {
        try
        {
            var tempo = GetDoubleClickTime();
            return tempo is > 0 and < 5000 ? tempo : 500;
        }
        catch (Exception erro) when (erro is DllNotFoundException or EntryPointNotFoundException)
        {
            return 500;
        }
    }

    /// <summary>O primeiro ancestral do tipo pedido na árvore visual, ou null.</summary>
    private static T? Acima<T>(DependencyObject? de) where T : DependencyObject
    {
        while (de is not null and not T) de = de is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(de) : LogicalTreeHelper.GetParent(de);
        return de as T;
    }
    private readonly MenuItem _menuZoom;
    private readonly MenuItem _menuRenomear;
    private readonly MenuItem _menuHachurar;
    private readonly MenuItem _menuTirar;
    private readonly MenuItem _menuLimpar;
    private readonly MenuItem _menuRegerar;
    private readonly MenuItem _menuRecalcular;
    private readonly MenuItem _menuApagar;

    private readonly Button _hachurarTodas;
    private readonly Button _tirarTodas;
    private readonly Button _limparTodas;
    private readonly Button _regerarTodas;
    private readonly Button _apagarTodas;

    /// <summary>Os comandos que mudam o que a lista mostra: a lista se lê de novo quando um deles termina.</summary>
    private static readonly HashSet<string> ComandosQueMudamAsAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        PluginInfo.ComandoAreaApagar, PluginInfo.ComandoAreaRecalcular, PluginInfo.ComandoApagarTudo, PluginInfo.ComandoRefazer,
        PluginInfo.ComandoAreasLimpar, PluginInfo.ComandoAreasRegerar, PluginInfo.ComandoAreasApagar,
        PluginInfo.ComandoArea, PluginInfo.ComandoUsina, PluginInfo.ComandoFileira, PluginInfo.ComandoReindexar, "U", "UNDO", "REDO", "ERASE",
    };

    internal JanelaDeAreas(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Áreas");
        Width = 1080;
        MinWidth = 900;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _colunaNome = MontarColunas();

        _lista.SelectionChanged += (_, _) => Tentar(MostrarDetalhe);
        _lista.MouseDoubleClick += (_, e) => Tentar(() =>
        {
            _cliqueDevagar.Stop();

            // Só o duplo clique numa linha (no cabeçalho, nada); na caixa do
            // nome aberta, ele escolhe a palavra, sem zoom.
            if (e.OriginalSource is not DependencyObject alvo || Acima<DataGridCell>(alvo) is { IsEditing: true }) return;
            if (ItemsControl.ContainerFromElement(_lista, alvo) is DataGridRow) Zoom();
        });

        // O nome se edita na linha: F2, ou o segundo clique, devagar, no nome
        // da área já escolhida (o duplo clique é o zoom).
        // Só o F2, o menu e o clique devagar (os três chamam o BeginEdit sem
        // evento) abrem o nome: o DataGrid sozinho abriria a edição com a
        // primeira letra digitada (e a área viraria "RE" de quem achou que
        // estava na linha de comando) e já no primeiro clique da célula
        // escolhida (revisão da 0.1.6).
        _lista.BeginningEdit += (_, e) =>
        {
            if (e.Column != _colunaNome || e.Row.Item is not Linha { Apagavel: true } || e.EditingEventArgs is not null) e.Cancel = true;
        };

        // O clique devagar, como nas pastas do Windows: o segundo clique, um
        // só, no nome da área que já estava escolhida; se dentro do tempo do
        // duplo clique vier outro, é o zoom. Na caixa já aberta, o clique só
        // põe o cursor (o Tick tirava o foco dela, e o Del ia para a área).
        _lista.PreviewMouseLeftButtonDown += (_, e) => Tentar(() =>
        {
            _cliqueDevagar.Stop();
            if (e.ClickCount != 1 || e.OriginalSource is not DependencyObject alvo) return;
            if (Acima<DataGridCell>(alvo) is not { } celula || celula.Column != _colunaNome || celula.IsEditing) return;
            if (celula.DataContext is not Linha { Apagavel: true } linha || !ReferenceEquals(_lista.SelectedItem, linha)) return;

            _linhaDoCliqueDevagar = linha;
            _cliqueDevagar.Start();
        });
        _cliqueDevagar.Tick += (_, _) => Tentar(() =>
        {
            _cliqueDevagar.Stop();
            if (ReferenceEquals(_lista.SelectedItem, _linhaDoCliqueDevagar)) ComecarARenomear();
        });
        _lista.CellEditEnding += (_, e) =>
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not Linha { Origem.Area: { } area } || e.EditingElement is not TextBox caixa) return;

            var nome = caixa.Text.Trim();
            if (nome.Length == 0 || nome == area.Identity.DisplayName) return;

            // Depois que a edição fecha: ler a lista de novo no meio dela quebra o DataGrid.
            // Abaixo do DataBind: no fim da linha o próprio DataGrid esvazia a fila até
            // o DataBind (Dispatcher.Invoke), e na prioridade Normal o Renomear lia a
            // lista ali dentro; a linha ficava com erro de validação e o F2 não abria
            // mais nome nenhum (01/11/2026: "consigo renomear apenas uma linha"). O
            // Loaded ainda vem antes das teclas e cliques que esperam na fila.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => Tentar(() => Renomear(area, nome))));
        };
        _lista.PreviewKeyDown += (_, e) => Tentar(() =>
        {
            if (_lista.IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBox)
            {
                // Editando o nome: o Enter grava e fica na mesma linha (o
                // DataGrid passaria para a de baixo).
                if (e.Key == Key.Enter)
                {
                    _lista.CommitEdit(DataGridEditingUnit.Row, true);
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == Key.F2)
            {
                ComecarARenomear();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && Escolhida?.Area is not null)
            {
                ApagarArea();
                e.Handled = true;
            }
        });

        // O botão direito escolhe a linha debaixo dele e abre o menu dela.
        _lista.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject alvo && ItemsControl.ContainerFromElement(_lista, alvo) is DataGridRow linha) linha.IsSelected = true;
        };

        MenuItem Item(string texto, string dica, Action acao, string atalho = "")
        {
            var m = new MenuItem { Header = texto, ToolTip = dica, InputGestureText = atalho };
            m.Click += (_, _) => Tentar(acao);
            return m;
        }

        _menuZoom = Item(Tr.T("Zoom"), Tr.T("Mostra na tela a linha escolhida: a área inteira, as mesas de fora das áreas ou, no total, todas."), Zoom, Tr.T("dois cliques"));
        // O menu fecha e devolve o foco à lista: a edição abre depois disso,
        // senão o foco que volta fecha a caixa do nome na hora.
        _menuRenomear = Item(Tr.T("Renomear"), Tr.T("Troca o nome da área (o que o Clivus mostra nas listas, na hachura e no Excel). O GUID, as mesas e o que a área segue não mudam."),
            () => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Tentar(ComecarARenomear))), "F2");
        _menuHachurar = Item(Tr.T("Hachurar"), Tr.T("Hachura a área com a cor dela (a da amostra na lista), 70 % transparente, com o nome no meio, acima do terreno e das mesas dela. Se já tinha hachura, troca."), () => Hachurar(todas: false));
        _menuTirar = Item(Tr.T("Tirar hachura"), Tr.T("Tira a hachura e o nome da área; a área e as mesas ficam."), () => Tirar(todas: false));
        _menuLimpar = Item(Tr.T("Limpar mesas"), Tr.T("Apaga as mesas da área (pilares, módulos, estrutura, strings); a área e o alinhamento ficam, prontos para gerar de novo. U desfaz."), () => Mandar(PluginInfo.ComandoApagarTudo, Tr.T("Limpar mesas")));
        _menuRegerar = Item(Tr.T("Regerar"), Tr.T("Apaga as mesas da área e gera de novo com a configuração e o terreno de hoje (a área que segue os objetos do cliente, pelos mesmos objetos). Pode perguntar o alinhamento na linha de comando."), () => Mandar(PluginInfo.ComandoRefazer, Tr.T("Regerar")));
        _menuRecalcular = Item(Tr.T("Recalcular"), Tr.T("Recalcula, onde estão, todas as mesas da área: pilares e pontas baixas no terreno de hoje, com a configuração atual. Nada muda de lugar."), () => Mandar(PluginInfo.ComandoAreaRecalcular, Tr.T("Recalcular")));
        _menuApagar = Item(Tr.T("Apagar área…"), Tr.T("Apaga a área (a linha, a hachura e o registro) e as mesas dela, depois de perguntar. U desfaz."), ApagarArea, "Del");

        var menu = new ContextMenu();
        foreach (var x in new object[] { _menuZoom, _menuRenomear, new Separator(), _menuHachurar, _menuTirar, new Separator(), _menuLimpar, _menuRegerar, _menuRecalcular, new Separator(), _menuApagar })
            menu.Items.Add(x);
        menu.Opened += (_, _) => Tentar(MostrarDetalhe);
        _lista.ContextMenu = menu;

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => Tentar(acao);
            return b;
        }

        // Embaixo, o que vale para todas as áreas (o título da seção já diz "todas").
        _hachurarTodas = Botao(Tr.T("Hachurar"), Tr.F("Hachura todas as áreas, cada uma com uma cor diferente, na camada {0}.", HachuraDasAreas.Camada), () => Hachurar(todas: true));
        _tirarTodas = Botao(Tr.T("Tirar hachura"), Tr.T("Tira todas as hachuras de área do desenho (também as de área que não existe mais)."), () => Tirar(todas: true));
        _limparTodas = Botao(Tr.T("Limpar"), Tr.T("Apaga as mesas de todas as áreas (cada área as dela); as áreas e os alinhamentos ficam. Pergunta antes. U desfaz."), () => Todas(PluginInfo.ComandoAreasLimpar));
        _regerarTodas = Botao(Tr.T("Regerar"), Tr.T("Regera todas as áreas: a que segue os objetos do cliente, por eles; as outras, pelo alinhamento. Pergunta antes. U desfaz."), () => Todas(PluginInfo.ComandoAreasRegerar));
        _apagarTodas = Botao(Tr.T("Apagar"), Tr.T("Apaga todas as áreas, cada uma com as mesas dela. Pergunta antes. U desfaz."), () => Todas(PluginInfo.ComandoAreasApagar));

        var exportar = Botao(Tr.T("Exportar Excel…"), Tr.T("Excel com a aba Resumo (uma linha por área e o total) e uma aba por área com a contagem por tipo de mesa, os pilares e a ocupação."), Exportar);
        var atualizar = Botao(Tr.T("Atualizar"), Tr.T("Lê o desenho de novo (depois de gerar, apagar ou mexer em mesas e áreas com a janela aberta)."), Preencher);
        var fechar = new Button { Content = Tr.T("Fechar"), Height = 26, Width = 80, IsCancel = true, ToolTip = Tr.T("Fecha a janela; as hachuras ficam.") };
        fechar.Click += (_, _) => Tentar(Close);

        var todas = new WrapPanel { Orientation = Orientation.Horizontal };
        todas.Children.Add(new TextBlock { Text = Tr.T("Todas as áreas:"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        foreach (var b in new[] { _hachurarTodas, _tirarTodas }) todas.Children.Add(b);
        todas.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(4, 3, 10, 3), Background = Brushes.Silver });
        foreach (var b in new[] { _limparTodas, _regerarTodas, _apagarTodas }) todas.Children.Add(b);

        var finais = new StackPanel { Orientation = Orientation.Horizontal };
        finais.Children.Add(exportar);
        finais.Children.Add(atualizar);
        finais.Children.Add(fechar);

        var botoes = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        DockPanel.SetDock(finais, Dock.Right);
        botoes.Children.Add(finais);
        botoes.Children.Add(todas);

        var corpo = new StackPanel();
        corpo.Children.Add(_lista);
        corpo.Children.Add(new TextBlock
        {
            Text = Tr.T("Na linha da área: dois cliques dá zoom, F2 (ou o segundo clique, devagar, no nome) renomeia, a lixeira (ou Del) apaga, e o botão direito tem o resto (hachurar, limpar, regerar, recalcular). Cada mesa conta na área onde está o centro dela (dentro de duas, na menor); \"na divisa\" é mesa com canto fora da área. O total é o desenho inteiro, como o Recontar."),
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        corpo.Children.Add(_avisos);
        corpo.Children.Add(_titulo);
        corpo.Children.Add(Detalhe());
        corpo.Children.Add(botoes);
        corpo.Children.Add(_recado);

        Content = new Border { Padding = new Thickness(12), Child = corpo };

        // Lida antes de aparecer: a janela já abre do tamanho da lista.
        Tentar(Preencher);

        // Os comandos que a janela manda (e os que mudam as áreas) terminam
        // depois do clique: a lista se lê de novo no fim deles.
        _documento.CommandEnded += AoTerminarComando;
        Closed += (_, _) =>
        {
            try { _documento.CommandEnded -= AoTerminarComando; }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao soltar a janela das áreas do fim dos comandos.", erro); }
        };
    }

    private void AoTerminarComando(object? sender, CommandEventArgs e)
    {
        if (!ComandosQueMudamAsAreas.Contains(e.GlobalCommandName)) return;

        // Abaixo do DataBind, como o Renomear: nunca no meio do fim de uma edição da
        // lista; e antes dos cliques que esperaram o comando (eles veem a lista nova).
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => Tentar(Preencher)));
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta (lida de novo).</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Tentar(aberta.Preencher);
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeAreas(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document == documento) janela.Close();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao fechar a janela das áreas junto com o desenho.", erro);
            }
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) =>
        {
            try
            {
                Abertas.Remove(documento);
                AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar a janela das áreas.", erro);
            }
        };

        AcadApp.ShowModelessWindow(janela);
    }

    // ------------------------------------------------------------ montagem

    /// <summary>As colunas da lista; devolve a do nome (a única que se edita).</summary>
    private DataGridTextColumn MontarColunas()
    {
        var direita = new Style(typeof(TextBlock));
        direita.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        direita.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 4, 0)));

        var amostra = new FrameworkElementFactory(typeof(Border));
        amostra.SetValue(WidthProperty, 14.0);
        amostra.SetValue(HeightProperty, 14.0);
        amostra.SetValue(Border.BorderBrushProperty, Brushes.Gray);
        amostra.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        amostra.SetValue(Border.MarginProperty, new Thickness(2));
        amostra.SetBinding(Border.BackgroundProperty, new Binding(nameof(Linha.Amostra)));
        _lista.Columns.Add(new DataGridTemplateColumn { Header = string.Empty, Width = 24, IsReadOnly = true, CellTemplate = new DataTemplate { VisualTree = amostra } });

        DataGridTextColumn Coluna(string titulo, string campo, double largura, bool numero, string? dica = null, bool editavel = false)
        {
            var coluna = new DataGridTextColumn
            {
                Header = dica is null ? titulo : new TextBlock { Text = titulo, ToolTip = dica },
                // O nome editado vai pelo CellEditEnding, não pela ligação. A
                // ligação do nome é de ida e volta, mas a volta não grava nada
                // (SoDeIda): o DataGrid trata a coluna de ligação só de ida
                // como só leitura, e o F2, o menu e o clique devagar não
                // abriam a edição (31/10/2026, "nao funciona o renomear. o F2
                // nao funciona tbm").
                Binding = editavel
                    ? new Binding(campo) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit, Converter = SoDeIda.Instancia }
                    : new Binding(campo) { Mode = BindingMode.OneWay },
                Width = largura > 0 ? new DataGridLength(largura) : new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = !editavel,
            };

            if (numero) coluna.ElementStyle = direita;
            _lista.Columns.Add(coluna);
            return coluna;
        }

        var nome = Coluna(Tr.T("Área"), nameof(Linha.Nome), 170, false, Tr.T("F2, ou o segundo clique devagar no nome, renomeia a área."), editavel: true);
        Coluna(Tr.T("ha"), nameof(Linha.Hectares), 70, true, Tr.T("A área em planta, em hectares."));
        Coluna(Tr.T("Mesas"), nameof(Linha.Mesas), 62, true);
        Coluna(Tr.T("Módulos"), nameof(Linha.Modulos), 72, true);
        Coluna("kWp", nameof(Linha.Kwp), 84, true);
        Coluna(Tr.T("Pilares"), nameof(Linha.Pilares), 66, true);
        Coluna(Tr.T("Tipos de mesa"), nameof(Linha.Tipos), 0, false);
        Coluna(Tr.T("Objetos"), nameof(Linha.Objetos), 62, false, Tr.T("Se a área segue os objetos do cliente (Conforme objetos): o Regerar área a refaz por eles."));
        Coluna(Tr.T("Hachura"), nameof(Linha.Hachura), 64, false);

        // A lixeira na linha de cada área: apaga depois de perguntar (31/10/2026).
        var lixeira = new FrameworkElementFactory(typeof(Button));
        lixeira.SetValue(ContentProperty, "");
        lixeira.SetValue(FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        lixeira.SetValue(Button.BackgroundProperty, Brushes.Transparent);
        lixeira.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        lixeira.SetValue(Button.PaddingProperty, new Thickness(4, 1, 4, 1));
        lixeira.SetValue(Button.CursorProperty, Cursors.Hand);
        lixeira.SetValue(ToolTipProperty, Tr.T("Apagar a área (pergunta antes)"));
        lixeira.SetBinding(VisibilityProperty, new Binding(nameof(Linha.Apagavel)) { Converter = new BooleanToVisibilityConverter() });
        lixeira.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, _) => Tentar(() =>
        {
            if (s is not FrameworkElement { DataContext: Linha linha }) return;
            _lista.SelectedItem = linha;
            ApagarArea();
        })));
        _lista.Columns.Add(new DataGridTemplateColumn { Header = string.Empty, Width = 32, IsReadOnly = true, CellTemplate = new DataTemplate { VisualTree = lixeira } });

        // O total em negrito; a dica da linha diz quem é a área (criação, handle, GUID).
        var estilo = new Style(typeof(DataGridRow));
        estilo.Setters.Add(new Setter(ToolTipProperty, new Binding(nameof(Linha.Dica))));
        var negrito = new DataTrigger { Binding = new Binding(nameof(Linha.Total)), Value = true };
        negrito.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
        estilo.Triggers.Add(negrito);
        _lista.RowStyle = estilo;

        _tipos.Columns.Add(new DataGridTextColumn { Header = Tr.T("Tipo de mesa"), Binding = new Binding(nameof(LinhaDoTipo.Tipo)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _tipos.Columns.Add(new DataGridTextColumn { Header = Tr.T("Mesas"), Binding = new Binding(nameof(LinhaDoTipo.Mesas)), Width = 58, ElementStyle = direita });
        _tipos.Columns.Add(new DataGridTextColumn { Header = Tr.T("Módulos"), Binding = new Binding(nameof(LinhaDoTipo.Modulos)), Width = 68, ElementStyle = direita });
        _tipos.Columns.Add(new DataGridTextColumn { Header = "kWp", Binding = new Binding(nameof(LinhaDoTipo.Kwp)), Width = 78, ElementStyle = direita });

        return nome;
    }

    /// <summary>O resumo da linha escolhida, em três colunas.</summary>
    private Grid Detalhe()
    {
        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        void Secao(int coluna, string titulo, UIElement conteudo)
        {
            var caixa = new GroupBox { Header = titulo, Content = conteudo, Margin = new Thickness(0, 0, coluna < 2 ? 8 : 0, 0) };
            Grid.SetColumn(caixa, coluna);
            grade.Children.Add(caixa);
        }

        Secao(0, Tr.T("Por tipo de mesa"), _tipos);
        Secao(1, Tr.T("Pilares e situação"), Campos(
            ("pilares", Tr.T("Pilares"), Tr.T("Quantos pilares as mesas desta linha têm no desenho.")),
            ("metros", Tr.T("Comprimento somado"), Tr.T("A soma dos comprimentos totais (enterrado + acima) gravados em cada pilar.")),
            ("semComprimento", Tr.T("Sem comprimento"), Tr.T("Pilares com problema, sem comprimento gravado: ficam fora da soma.")),
            ("pendentes", Tr.T("Mesas pendentes"), Tr.T("Mesas marcadas como pendentes (movidas, copiadas): use Recalcular pendentes.")),
            ("naoCabem", Tr.T("Não cabem no terreno"), Tr.T("Mesas marcadas porque não cabem no terreno.")),
            ("divisa", Tr.T("Na divisa"), Tr.T("Mesas contadas nesta área com algum canto fora dela. O Regerar e o Apagar tudo de qualquer área que elas tocam as levam."))));
        Secao(2, Tr.T("Área e ocupação"), Campos(
            ("planta", Tr.T("Área em planta"), Tr.T("A área da polilinha vista de cima.")),
            ("modulos", Tr.T("Área dos módulos"), Tr.T("A face de cima de cada módulo, em verdadeira grandeza, somada.")),
            ("ocupacao", Tr.T("Ocupação"), Tr.T("A área dos módulos ÷ a área em planta.")),
            ("objetos", Tr.T("Segue os objetos"), Tr.T("Se a área segue os objetos do cliente (Conforme objetos).")),
            ("hachura", Tr.T("Hachura"), Tr.F("Se a área está hachurada (camada {0}).", HachuraDasAreas.Camada))));

        return grade;
    }

    /// <summary>Uma grade de rótulo e valor (o rótulo ao lado, regra de 02/10/2026).</summary>
    private Grid Campos(params (string Chave, string Rotulo, string Dica)[] campos)
    {
        var grade = new Grid { Margin = new Thickness(6, 4, 6, 4) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < campos.Length; i++)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var rotulo = new TextBlock { Text = campos[i].Rotulo, Foreground = Brushes.DimGray, Margin = new Thickness(0, 1, 10, 1), ToolTip = campos[i].Dica };
            var valor = new TextBlock { Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap, ToolTip = campos[i].Dica };
            Grid.SetRow(rotulo, i);
            Grid.SetRow(valor, i);
            Grid.SetColumn(valor, 1);
            grade.Children.Add(rotulo);
            grade.Children.Add(valor);
            _valores[campos[i].Chave] = valor;
        }

        return grade;
    }

    // ------------------------------------------------------------ leitura

    private LinhaDasAreas? Escolhida => (_lista.SelectedItem as Linha)?.Origem;

    /// <summary>Lê o desenho e enche a lista, mantendo a linha escolhida.</summary>
    private void Preencher()
    {
        var antes = Escolhida;

        // A troca da lista tira da árvore a célula com o foco, e o WPF o devolve
        // à janela: as setas, o F2 e o Del paravam de passar pela lista até um
        // clique nela. O foco volta à linha escolhida só se estava na lista (a
        // releitura do fim de um comando não o tira do desenho).
        var tinhaFoco = _lista.IsKeyboardFocusWithin;

        // Um nome aberto e não confirmado (sem Enter) fecha sem gravar, como no
        // Esc: trocar a lista no meio da edição prende o DataGrid, e o desenho
        // pode ter mudado embaixo dele (um U, um apagar).
        _lista.CancelEdit(DataGridEditingUnit.Row);

        try
        {
            Cursor = Cursors.Wait;
            _leitura = AreasDoDesenho.Ler(_documento);
        }
        finally
        {
            Cursor = null;
        }

        var linhas = _leitura.Linhas.Select(ParaLinha).ToList();
        _lista.ItemsSource = linhas;
        _lista.SelectedItem =
            linhas.FirstOrDefault(l => antes?.Area is { } a ? l.Origem.Area?.Handle == a.Handle : antes is not null && l.Origem.Area is null && l.Origem.Resumo.Kind == antes.Resumo.Kind)
            ?? linhas.FirstOrDefault();
        if (tinhaFoco) FocarNaEscolhida();

        _avisos.Text = string.Join("\n", _leitura.Avisos);
        _avisos.Visibility = _leitura.Avisos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var comAreas = _leitura.Areas.Count > 0;
        foreach (var b in new[] { _hachurarTodas, _limparTodas, _regerarTodas, _apagarTodas }) b.IsEnabled = comAreas;

        var semArea = Tr.T("Nenhuma área neste desenho. Use o botão Área.");
        if (_leitura.Areas.Count == 0) Recado(semArea, true);
        else if (_recado.Text == semArea) _recado.Text = string.Empty;

        MostrarDetalhe();
    }

    private static Linha ParaLinha(LinhaDasAreas l)
    {
        var r = l.Resumo;
        var c = Tr.Culture;

        Brush amostra = Brushes.Transparent;
        if (l.Cor is { } cor)
        {
            amostra = new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B));
            amostra.Freeze();
        }

        var dica = l.Area is { } a
            ? $"{a.Identity.Describe()} · handle {a.Handle} · {a.Identity.Id:D}" + (a.Registrada ? string.Empty : " · " + Tr.T("fora do registro"))
            : r.Kind == AreaRowKind.Outside ? Tr.T("Mesas com o centro fora de toda área de implantação.") : Tr.T("O desenho inteiro, como o Recontar.");

        return new Linha(
            l,
            amostra,
            r.Name,
            r.Hectares is { } ha ? ha.ToString("N2", c) : string.Empty,
            r.Census.Tables.ToString("N0", c),
            r.Census.Modules.ToString("N0", c),
            r.Census.PowerKwp.ToString("N1", c),
            r.Census.Pillars.ToString("N0", c),
            r.TypesText,
            l.Area is not null && r.FollowsObjects ? Tr.T("sim") : string.Empty,
            l.Area is not null && l.Hachurada ? Tr.T("sim") : string.Empty,
            r.Kind == AreaRowKind.Total,
            dica,
            l.Area is not null);
    }

    /// <summary>O resumo da linha escolhida e quais botões valem para ela.</summary>
    private void MostrarDetalhe()
    {
        var linha = Escolhida;
        var area = linha?.Area;

        // O menu do botão direito vale para a linha escolhida.
        _menuZoom.IsEnabled = linha is not null;
        _menuRenomear.IsEnabled = area is not null;
        _menuHachurar.IsEnabled = area is not null;
        _menuTirar.IsEnabled = area is not null && linha!.Hachurada;

        var comMesas = area is not null && linha!.Resumo.Census.Tables > 0;
        _menuLimpar.IsEnabled = comMesas;
        _menuRecalcular.IsEnabled = comMesas;
        _menuRegerar.IsEnabled = area is not null;
        _menuApagar.IsEnabled = area is not null;

        if (linha is null)
        {
            _titulo.Text = string.Empty;
            _tipos.ItemsSource = null;
            foreach (var v in _valores.Values) v.Text = string.Empty;
            return;
        }

        var r = linha.Resumo;
        var c = Tr.Culture;
        string N(double v, string formato) => v.ToString(formato, c);

        _titulo.Text = r.Name + (r.Hectares is { } ha ? " — " + N(ha, "N4") + " ha" : string.Empty) + " · "
            + Tr.F("{0:N0} mesa(s) · {1:N0} módulo(s) · {2:N1} kWp", r.Census.Tables, r.Census.Modules, r.Census.PowerKwp);

        _tipos.ItemsSource = r.Types.Select(t => new LinhaDoTipo(t.Name, N(t.Tables, "N0"), N(t.Modules, "N0"), N(t.PowerKwp, "N1"))).ToList();

        _valores["pilares"].Text = N(r.Census.Pillars, "N0");
        _valores["metros"].Text = N(r.PillarMeters, "N1") + " m";
        _valores["semComprimento"].Text = N(r.Census.PillarsWithoutLength, "N0");
        _valores["pendentes"].Text = N(r.Census.Dirty, "N0");
        _valores["naoCabem"].Text = N(r.Census.Marked, "N0");
        _valores["divisa"].Text = r.Kind == AreaRowKind.Outside ? "—" : N(r.OnBorder, "N0");

        _valores["planta"].Text = r.PlanArea is { } m2 ? N(m2 / 10_000, "N4") + " ha (" + N(m2, "N0") + " m²)" : "—";
        _valores["modulos"].Text = N(r.ModuleArea, "N0") + " m²"
            + (r.TablesWithoutFace > 0 ? " · " + Tr.F("{0} mesa(s) sem face, fora da conta", r.TablesWithoutFace) : string.Empty);
        _valores["ocupacao"].Text = r.Occupation is { } o ? N(o * 100, "N1") + " %" : "—";

        var areas = _leitura?.Linhas.Where(l => l.Area is not null).ToList() ?? [];
        _valores["objetos"].Text = area is null ? "—" : r.FollowsObjects ? Tr.T("sim") : Tr.T("não");
        _valores["hachura"].Text = area is not null
            ? (linha.Hachurada ? Tr.T("sim") : Tr.T("não"))
            : r.Kind == AreaRowKind.Total ? Tr.F("{0} de {1} área(s)", areas.Count(l => l.Hachurada), areas.Count) : "—";
    }

    // -------------------------------------------------------------- ações

    private void Zoom()
    {
        if (Escolhida is not { } linha)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        if (AreasDoDesenho.Limites(linha.Pontos) is not { } caixa)
        {
            Recado(Tr.T("Nada a mostrar nessa linha."), true);
            return;
        }

        using (_documento.LockDocument())
            LocalDosInversores.Zoom(_documento.Editor, caixa, folga: 1.15);
    }

    private void Hachurar(bool todas)
    {
        if (_leitura is not { } leitura) return;

        if (todas)
        {
            Fazer(() => AreasDoDesenho.Hachurar(_documento, leitura.Areas, null));
            return;
        }

        if (Escolhida?.Area is not { } area)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        Fazer(() => AreasDoDesenho.Hachurar(_documento, leitura.Areas, [area]));
    }

    private void Tirar(bool todas)
    {
        if (_leitura is not { } leitura) return;

        if (todas)
        {
            Fazer(() => AreasDoDesenho.TirarHachura(_documento, leitura.Areas, null));
            return;
        }

        if (Escolhida?.Area is not { } area)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        Fazer(() => AreasDoDesenho.TirarHachura(_documento, leitura.Areas, area));
    }

    /// <summary>Abre o nome da área escolhida para editar na própria linha (o F2).</summary>
    private void ComecarARenomear()
    {
        if (_lista.SelectedItem is not Linha { Apagavel: true } linha)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        // Já aberto nesta linha: nada (o Focus abaixo tiraria o cursor da caixa).
        if (Acima<DataGridCell>(_colunaNome.GetCellContent(linha)) is { IsEditing: true }) return;

        _lista.ScrollIntoView(linha, _colunaNome);
        _lista.UpdateLayout();
        _lista.Focus();
        _lista.CurrentCell = new DataGridCellInfo(linha, _colunaNome);

        if (!_lista.BeginEdit())
        {
            RegistroDeDiagnostico.Registrar($"Áreas: o BeginEdit do nome de {linha.Nome} não abriu.");
            Recado(Tr.T("Não consegui abrir o nome para editar; use o botão direito › Renomear."), true);
        }
    }

    /// <summary>O foco do teclado na célula do nome da linha escolhida (as setas, o F2 e o Del seguem dali).</summary>
    private void FocarNaEscolhida()
    {
        if (_lista.SelectedItem is not Linha linha) return;

        _lista.ScrollIntoView(linha, _colunaNome);
        _lista.UpdateLayout();
        _lista.CurrentCell = new DataGridCellInfo(linha, _colunaNome);
        Acima<DataGridCell>(_colunaNome.GetCellContent(linha))?.Focus();
    }

#if DEBUG
    /// <summary>
    /// Para o nível 2, o Renomear pelo caminho da tela: escolhe a linha, abre
    /// a edição pelo menu (o mesmo do botão direito) ou pelo F2, escreve o
    /// nome na caixa da linha e confirma, como o Enter. A frase do recado, ou
    /// o que não andou.
    /// </summary>
    internal string RenomearComoNaTela(string atual, string novo, bool pelaTecla)
    {
        if (_lista.Items.OfType<Linha>().FirstOrDefault(l => l.Origem.Area?.Identity.DisplayName == atual) is not { } linha) return "sem a linha " + atual;

        _lista.SelectedItem = linha;
        _lista.UpdateLayout();

        if (pelaTecla) ComecarARenomear();
        else _menuRenomear.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Esvaziar();

        if (_colunaNome.GetCellContent(linha) is not TextBox caixa) return "a linha nao entrou em edicao";

        caixa.Text = novo;
        if (!_lista.CommitEdit(DataGridEditingUnit.Row, true)) return "a edicao nao confirmou";

        Esvaziar();
        return _recado.Text;
    }

    /// <summary>Roda o que a janela deixou para depois (o BeginInvoke do menu e do renomear).</summary>
    private void Esvaziar() => Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
#endif

    /// <summary>Grava o nome novo da área (editado na linha).</summary>
    private void Renomear(AreaNoDesenho area, string nome)
    {
        if (_leitura is not { } leitura) return;

        Fazer(() => AreasDoDesenho.Renomear(_documento, leitura.Areas, area, nome));
    }

    /// <summary>
    /// Apagar a área escolhida: pergunta aqui, sempre (a lixeira, o Del e o
    /// menu), com o que vai junto, e o comando não pergunta de novo na linha
    /// de comando (31/10/2026: a pergunta ia para a linha de comando, ninguém
    /// a via, e "clico em apagar área e não apaga"). As mesas que vão são as
    /// DA área, as que a lista conta nela.
    /// </summary>
    private void ApagarArea()
    {
        if (_leitura is not { } leitura || Escolhida is not { Area: { } area } linha)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        var repetida = EscolhaDeArea.Repetidas(leitura.Areas, area).Count > 1;
        var mesas = repetida ? 0 : linha.Resumo.Census.Tables;

        var pergunta = mesas > 0
            ? Tr.F("A área \"{0}\" tem {1} mesa(s). Apagar a área apaga as mesas, os pilares, a estrutura e as strings dela também (U desfaz).\n\nApagar a área e as mesas?", area.Identity.DisplayName, mesas)
            : Tr.F("Apagar a área \"{0}\"? Ela não tem mesa dela (as mesas que estão dentro dela contam em outra área e ficam). U desfaz.", area.Identity.DisplayName);
        if (MessageBox.Show(this, pergunta, Tr.T("Apagar área"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        AreaAcoesCommands.ConfirmadaPelaJanela = area.Id;
        Mandar(PluginInfo.ComandoAreaApagar, Tr.T("Apagar área"));
    }

    /// <summary>
    /// O "todas as áreas" de baixo (Limpar, Regerar, Apagar): pergunta aqui,
    /// com quantas áreas e mesas, e manda o comando, que não pergunta de novo.
    /// </summary>
    private void Todas(string comando)
    {
        if (_leitura is not { } leitura || leitura.Areas.Count == 0)
        {
            Recado(Tr.T("Nenhuma área neste desenho. Use o botão Área."), true);
            return;
        }

        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        var areas = EscolhaDeArea.Distintas(leitura.Areas).Count;
        var mesas = leitura.Linhas.Where(l => l.Area is not null).Sum(l => l.Resumo.Census.Tables);

        var (pergunta, titulo) = comando switch
        {
            PluginInfo.ComandoAreasLimpar => (Tr.F("Apagar as {0} mesa(s) das {1} área(s)? As áreas e os alinhamentos ficam. U desfaz.", mesas, areas), Tr.T("Limpar todas as áreas")),
            PluginInfo.ComandoAreasRegerar => (Tr.F("Regerar as {0} área(s)? As mesas delas são apagadas e geradas de novo com a configuração e o terreno de hoje (a área que segue os objetos do cliente, por eles). U desfaz.", areas), Tr.T("Regerar todas as áreas")),
            _ => (Tr.F("Apagar as {0} área(s) e as {1} mesa(s) delas? U desfaz.", areas, mesas), Tr.T("Apagar todas as áreas")),
        };
        if (MessageBox.Show(this, pergunta, titulo, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        // Sem interface (Core Console) o SendStringToExecute fora de comando derruba o processo.
        if (!ClivusExtension.TemInterface()) return;

        AreaAcoesCommands.TodasConfirmadasPelaJanela = true;
        var cancelar = string.IsNullOrEmpty(_documento.CommandInProgress) ? string.Empty : "\x03\x03";
        _documento.SendStringToExecute($"{cancelar}_{comando} ", true, false, false);
        Recado(Tr.F("{0}: veja a linha de comando.", titulo), false);
    }

    /// <summary>
    /// Manda o comando para a linha de comando com a área escolhida
    /// (<see cref="EscolhaDeArea.PedirPelaJanela"/>). Corre como qualquer
    /// comando: U desfaz, e a lista se lê de novo quando ele termina.
    /// </summary>
    private void Mandar(string comando, string oQue)
    {
        if (Escolhida?.Area is not { } area)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        EscolhaDeArea.PedirPelaJanela(area.Id);

        // A seleção que estivesse no desenho não fica no lugar da área pedida.
        using (_documento.LockDocument()) _documento.Editor.SetImpliedSelection([]);

        // Sem interface (Core Console) o SendStringToExecute fora de comando derruba o processo.
        if (!ClivusExtension.TemInterface()) return;

        var cancelar = string.IsNullOrEmpty(_documento.CommandInProgress) ? string.Empty : "\x03\x03";
        _documento.SendStringToExecute($"{cancelar}_{comando} ", true, false, false);
        Recado(Tr.F("{0} de {1}: veja a linha de comando.", oQue, area.Identity.DisplayName), false);
    }

    private void Exportar()
    {
        var nome = Path.GetFileNameWithoutExtension(_documento.Name);
        if (string.IsNullOrWhiteSpace(nome)) nome = "usina";

        // Sempre a janela do Windows (regra de 02/10/2026).
        var caminho = DialogoDeArquivo.Salvar(
            Tr.T("Exportar o resumo por área"), Tr.T("Pasta de trabalho do Excel (*.xlsx)|*.xlsx"), nome + " - áreas.xlsx",
            _documento.IsNamedDrawing ? Path.GetDirectoryName(_documento.Name) : null);

        if (caminho is null) return;

        // Lido de novo na hora: o desenho pode ter mudado com a janela aberta.
        Fazer(() => AreasDoDesenho.Exportar(_documento, AreasDoDesenho.Ler(_documento).Resumo, caminho));
    }

    // ------------------------------------------------------------ apoio

    /// <summary>Roda a ação, mostra a frase (aqui e na linha de comando) e lê o desenho de novo.</summary>
    private void Fazer(Func<string> operacao)
    {
        try
        {
            Cursor = Cursors.Wait;
            var frase = operacao();
            _documento.Editor.WriteMessage($"\n{frase}\n");
            Recado(frase, false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
            return;
        }
        finally
        {
            Cursor = null;
        }

        // A lista depois da ação (a hachura, o nome); erro na leitura aparece no lugar da frase.
        Tentar(Preencher);
    }

    /// <summary>Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
    }

    private void Falhou(Exception erro)
    {
        RegistroDeDiagnostico.Registrar("Falha na janela das áreas.", erro);
        Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
    }

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }
}

/// <summary>
/// A ligação do nome da área na lista: mostra o valor e nunca grava de volta
/// (o nome novo vai pelo Renomear, no fim da edição). Com ela a coluna fica
/// editável, coisa que a ligação só de ida não deixa no DataGrid.
/// </summary>
internal sealed class SoDeIda : IValueConverter
{
    internal static readonly SoDeIda Instancia = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
}
