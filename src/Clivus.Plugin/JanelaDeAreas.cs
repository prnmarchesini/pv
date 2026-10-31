using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>Uma linha da lista, com os números já escritos na cultura da tela.</summary>
    internal sealed record Linha(
        LinhaDasAreas Origem, Brush Amostra, string Nome, string Hectares, string Mesas, string Modulos, string Kwp, string Pilares,
        string Tipos, string Objetos, string Hachura, bool Total, string Dica);

    /// <summary>Uma linha da contagem por tipo de mesa.</summary>
    internal sealed record LinhaDoTipo(string Tipo, string Mesas, string Modulos, string Kwp);

    private readonly Document _documento;
    private LeituraDasAreas? _leitura;

    private readonly DataGrid _lista = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
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

    private readonly Button _zoom;
    private readonly Button _hachurar;
    private readonly Button _tirar;
    private readonly Button _renomear;
    private readonly Button _hachurarTodas;
    private readonly Button _tirarTodas;

    private JanelaDeAreas(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Áreas");
        Width = 1080;
        MinWidth = 900;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        MontarColunas();

        _lista.SelectionChanged += (_, _) => Tentar(MostrarDetalhe);
        _lista.MouseDoubleClick += (_, e) => Tentar(() =>
        {
            // Só o duplo clique numa linha (no cabeçalho, nada).
            if (e.OriginalSource is DependencyObject alvo && ItemsControl.ContainerFromElement(_lista, alvo) is DataGridRow) Zoom();
        });

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => Tentar(acao);
            return b;
        }

        _zoom = Botao(Tr.T("Zoom"), Tr.T("Mostra na tela a linha escolhida: a área inteira, as mesas de fora das áreas ou, no total, todas. O duplo clique na linha faz o mesmo."), Zoom);
        _hachurar = Botao(Tr.T("Hachurar"), Tr.T("Hachura a área escolhida com a cor dela (a da amostra na lista), 70 % transparente, com o nome no meio, acima do terreno e das mesas dela. Se já tinha hachura, troca."), () => Hachurar(todas: false));
        _tirar = Botao(Tr.T("Tirar hachura"), Tr.T("Tira a hachura e o nome da área escolhida; a área e as mesas ficam."), () => Tirar(todas: false));
        _renomear = Botao(Tr.T("Renomear…"), Tr.T("Troca o nome da área (o que o Clivus mostra nas listas, na hachura e no Excel). O GUID, as mesas e o que a área segue não mudam."), Renomear);
        _hachurarTodas = Botao(Tr.T("Hachurar todas"), Tr.F("Hachura todas as áreas, cada uma com uma cor diferente, na camada {0}.", HachuraDasAreas.Camada), () => Hachurar(todas: true));
        _tirarTodas = Botao(Tr.T("Tirar todas"), Tr.T("Tira todas as hachuras de área do desenho (também as de área que não existe mais)."), () => Tirar(todas: true));

        var exportar = Botao(Tr.T("Exportar Excel…"), Tr.T("Excel com a aba Resumo (uma linha por área e o total) e uma aba por área com a contagem por tipo de mesa, os pilares e a ocupação."), Exportar);
        var atualizar = Botao(Tr.T("Atualizar"), Tr.T("Lê o desenho de novo (depois de gerar, apagar ou mexer em mesas e áreas com a janela aberta)."), Preencher);
        var fechar = new Button { Content = Tr.T("Fechar"), Height = 26, Width = 80, IsCancel = true, ToolTip = Tr.T("Fecha a janela; as hachuras ficam.") };
        fechar.Click += (_, _) => Tentar(Close);

        var daEscolhida = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var b in new[] { _zoom, _hachurar, _tirar, _renomear }) daEscolhida.Children.Add(b);
        daEscolhida.Children.Add(new Border { Width = 1, Margin = new Thickness(4, 3, 10, 3), Background = Brushes.Silver });
        daEscolhida.Children.Add(_hachurarTodas);
        daEscolhida.Children.Add(_tirarTodas);

        var finais = new StackPanel { Orientation = Orientation.Horizontal };
        finais.Children.Add(exportar);
        finais.Children.Add(atualizar);
        finais.Children.Add(fechar);

        var botoes = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        DockPanel.SetDock(finais, Dock.Right);
        botoes.Children.Add(finais);
        botoes.Children.Add(daEscolhida);

        var corpo = new StackPanel();
        corpo.Children.Add(_lista);
        corpo.Children.Add(new TextBlock
        {
            Text = Tr.T("Cada mesa conta na área onde está o centro dela (dentro de duas, na menor); \"na divisa\" é mesa com canto fora da área. O total é o desenho inteiro, como o Recontar."),
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

    private void MontarColunas()
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
        _lista.Columns.Add(new DataGridTemplateColumn { Header = string.Empty, Width = 24, CellTemplate = new DataTemplate { VisualTree = amostra } });

        void Coluna(string titulo, string campo, double largura, bool numero, string? dica = null)
        {
            var coluna = new DataGridTextColumn
            {
                Header = dica is null ? titulo : new TextBlock { Text = titulo, ToolTip = dica },
                Binding = new Binding(campo),
                Width = largura > 0 ? new DataGridLength(largura) : new DataGridLength(1, DataGridLengthUnitType.Star),
            };

            if (numero) coluna.ElementStyle = direita;
            _lista.Columns.Add(coluna);
        }

        Coluna(Tr.T("Área"), nameof(Linha.Nome), 170, false);
        Coluna(Tr.T("ha"), nameof(Linha.Hectares), 70, true, Tr.T("A área em planta, em hectares."));
        Coluna(Tr.T("Mesas"), nameof(Linha.Mesas), 62, true);
        Coluna(Tr.T("Módulos"), nameof(Linha.Modulos), 72, true);
        Coluna("kWp", nameof(Linha.Kwp), 84, true);
        Coluna(Tr.T("Pilares"), nameof(Linha.Pilares), 66, true);
        Coluna(Tr.T("Tipos de mesa"), nameof(Linha.Tipos), 0, false);
        Coluna(Tr.T("Objetos"), nameof(Linha.Objetos), 62, false, Tr.T("Se a área segue os objetos do cliente (Conforme objetos): o Regerar área a refaz por eles."));
        Coluna(Tr.T("Hachura"), nameof(Linha.Hachura), 64, false);

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

        _avisos.Text = string.Join("\n", _leitura.Avisos);
        _avisos.Visibility = _leitura.Avisos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _hachurarTodas.IsEnabled = _leitura.Areas.Count > 0;

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
            dica);
    }

    /// <summary>O resumo da linha escolhida e quais botões valem para ela.</summary>
    private void MostrarDetalhe()
    {
        var linha = Escolhida;
        var area = linha?.Area;

        _zoom.IsEnabled = linha is not null;
        _hachurar.IsEnabled = area is not null;
        _tirar.IsEnabled = area is not null && linha!.Hachurada;
        _renomear.IsEnabled = area is not null;

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

    private void Renomear()
    {
        if (_leitura is not { } leitura || Escolhida?.Area is not { } area)
        {
            Recado(Tr.T("Escolha uma área na lista."), true);
            return;
        }

        var janela = new JanelaDeNome(Tr.T("Renomear a área"), Tr.F("Novo nome da área \"{0}\":", area.Identity.DisplayName), area.Identity.DisplayName);
        if (AcadApp.ShowModalWindow(janela) != true || janela.Nome is not { } nome) return;
        if (nome == area.Identity.DisplayName) return;

        Fazer(() => AreasDoDesenho.Renomear(_documento, leitura.Areas, area, nome));
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
