using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Memória › Bacias (30): a região, o tamanho da célula, calcular, o ajuste
/// manual e a lista canaleta × bacia (30.4), com zoom ao clicar na linha.
/// Solta, como as outras janelas da drenagem.
/// </summary>
internal sealed class JanelaDeBacias : Window
{
    private static readonly Dictionary<Document, JanelaDeBacias> Abertas = [];

    /// <summary>Uma linha da lista.</summary>
    internal sealed record Linha(string Canaleta, string Area, string Hectares, string L, string Dh, string S, string Ajuste, Point3d? Onde);

    private readonly Document _documento;
    private readonly ComboBox _regiao = new() { Width = 220, ToolTip = Tr.T("Dentro das áreas de implantação, no terreno todo ou dentro de uma polilinha fechada escolhida no desenho.") };
    private readonly TextBox _celula = new() { Width = 50, ToolTip = Tr.T("O lado da célula da grade, em metros. Menor dá borda mais fina e demora mais.") };
    private readonly DataGrid _lista = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        SelectionMode = DataGridSelectionMode.Single,
        MaxHeight = 260,
        Margin = new Thickness(0, 8, 0, 0),
    };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private JanelaDeBacias(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Bacias de contribuição");
        Width = 720;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _regiao.Items.Add(Tr.T("Áreas de implantação"));
        _regiao.Items.Add(Tr.T("Terreno todo"));
        _regiao.Items.Add(Tr.T("Polilinha fechada (escolher)…"));

        var bacias = DrenagemStore.Ler(documento.Database).Basins;
        _regiao.SelectedIndex = bacias.Region switch { "Tudo" => 1, "Polilinha" => 2, _ => 0 };
        _celula.Text = bacias.CellSize.ToString("0.##", CultureInfo.CurrentCulture);

        StackPanel LinhaDeCampos(params UIElement[] itens)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            foreach (var i in itens) linha.Children.Add(i);
            return linha;
        }

        TextBlock R(string texto, double largura = 0) => new()
        {
            Text = texto,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Width = largura > 0 ? largura : double.NaN,
        };

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => acao();
            return b;
        }

        foreach (var (titulo, campo) in new[]
        {
            (Tr.T("Canaleta"), nameof(Linha.Canaleta)), (Tr.T("Área (m²)"), nameof(Linha.Area)), (Tr.T("Área (ha)"), nameof(Linha.Hectares)),
            (Tr.T("L (m)"), nameof(Linha.L)), (Tr.T("ΔH (m)"), nameof(Linha.Dh)), (Tr.T("S (%)"), nameof(Linha.S)), (Tr.T("Ajuste"), nameof(Linha.Ajuste)),
        })
        {
            _lista.Columns.Add(new DataGridTextColumn { Header = titulo, Binding = new Binding(campo) });
        }

        _lista.SelectionChanged += (_, _) =>
        {
            if (_lista.SelectedItem is Linha { Onde: { } p })
                LocalDosInversores.Zoom(_documento.Editor, new Autodesk.AutoCAD.DatabaseServices.Extents3d(new Point3d(p.X - 40, p.Y - 40, p.Z), new Point3d(p.X + 40, p.Y + 40, p.Z)));
        };

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(LinhaDeCampos(R(Tr.T("Região"), 70), _regiao, R(Tr.T("   Célula")), _celula, R(Tr.T("m"))));
        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("De cada célula a água desce pelo terreno até a primeira canaleta: cada bacia vira um hatch com a cor dela, a borda assentada no terreno e o rótulo com área, L e ΔH. O que não chega a canaleta nenhuma fica em vermelho, com o motivo. Ajuste manual: desenhe uma polilinha fechada e diga para qual canaleta a área dela vai."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 0),
        });
        pilha.Children.Add(_lista);
        pilha.Children.Add(_recado);

        var botoes = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var calcular = Botao(Tr.T("Calcular bacias"), Tr.T("Apaga as bacias anteriores, calcula e desenha de novo com as canaletas e os ajustes do desenho."), Calcular);
        calcular.IsDefault = true;
        botoes.Children.Add(calcular);
        botoes.Children.Add(Botao(Tr.T("Ajuste manual…"), Tr.T("Escolha uma polilinha fechada e a canaleta para onde a água dela vai."), () => Mandar(PluginInfo.ComandoDrenagemBaciaManual)));
        botoes.Children.Add(Botao(Tr.T("Apagar bacias"), Tr.T("Apaga o hatch, a borda e o rótulo das bacias (os ajustes manuais ficam)."), () => Fazer(() => Tr.F("BACIAS {0} entidade(s) apagada(s); os ajustes manuais ficam.", BaciasDeDrenagem.Apagar(_documento.Database)))));
        var fechar = Botao(Tr.T("Fechar"), Tr.T("Fecha a janela; as bacias desenhadas ficam."), Close);
        fechar.IsCancel = true;
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        Content = pilha;

        Preencher();
    }

    /// <summary>A lista a partir do resultado gravado.</summary>
    private void Preencher()
    {
        var relatorio = DrenagemStore.LerBacias(_documento.Database);
        var linhas = new List<Linha>();

        if (relatorio is not null)
        {
            var onde = OndeDosRotulos();
            string N(double v, string f) => v.ToString(f, CultureInfo.CurrentCulture);

            foreach (var b in relatorio.Basins)
            {
                linhas.Add(new Linha(b.ChannelName, N(b.Area, "N0"), N(b.Area / 10_000, "0.###"), N(b.Length, "0.0"), N(b.Drop, "0.00"), N(b.SlopePercent, "0.00"),
                    b.Manual ? Tr.T("manual") : Tr.T("pelo terreno"), onde.GetValueOrDefault(b.Channel.ToString("D"))));
            }

            if (relatorio.NoDestinationArea > 0)
                linhas.Add(new Linha(Tr.T("Sem destino"), N(relatorio.NoDestinationArea, "N0"), N(relatorio.NoDestinationArea / 10_000, "0.###"), "", "", "", "", onde.GetValueOrDefault("-")));

            _recado.Foreground = Brushes.Gray;
            _recado.Text = Tr.F("Calculado em {0:dd/MM/yyyy HH:mm}, células de {1:0.##} m, {2:N0} m² analisados.", relatorio.When, relatorio.CellSize, relatorio.AnalyzedArea);
        }

        _lista.ItemsSource = linhas;
    }

    /// <summary>Onde está o rótulo de cada bacia no desenho (para o zoom).</summary>
    private Dictionary<string, Point3d?> OndeDosRotulos()
    {
        var onde = new Dictionary<string, Point3d?>();
        var database = _documento.Database;

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var espaco = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)transacao.GetObject(Autodesk.AutoCAD.DatabaseServices.SymbolUtilityServices.GetBlockModelSpaceId(database), Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

        foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in espaco)
        {
            if (transacao.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) is not Autodesk.AutoCAD.DatabaseServices.DBText t) continue;
            if (t.Layer != LayoutLayers.DrenagemBacia && t.Layer != LayoutLayers.DrenagemAlerta) continue;
            if (PluginXData.Load(t, "DrnBacia", 1, 1) is { } campos) onde.TryAdd(campos[0], t.AlignmentPoint);
        }

        return onde;
    }

    private void Calcular()
    {
        if (!NumberInput.TryParseMeasure(_celula.Text, out var celula) || !(celula > 0))
        {
            Recado(Tr.F("Célula: \"{0}\" não é um número maior que zero.", _celula.Text), true);
            return;
        }

        var regiao = _regiao.SelectedIndex switch { 1 => "Tudo", 2 => "Polilinha", _ => "Areas" };

        try
        {
            EscritaForaDeComando.Fazer(_documento, () => DrenagemStore.Mudar(_documento.Database, d => d with { Basins = d.Basins with { CellSize = celula, Region = regiao == "Polilinha" ? d.Basins.Region : regiao } }));
        }
        catch (Exception erro)
        {
            Falhou(erro);
            return;
        }

        // A polilinha da região se escolhe no desenho: vai como comando.
        if (regiao == "Polilinha")
        {
            Mandar(PluginInfo.ComandoDrenagemBaciasCalcular + " _Polilinha");
            Recado(Tr.T("Escolha no desenho a polilinha fechada da região. Depois, Atualize a lista abrindo esta janela de novo."), false);
            return;
        }

        JanelaDeProgresso? andamento = null;
        Fazer(() =>
        {
            var terreno = TerrenoDoDesenho.Garantir(_documento) ?? throw new ArgumentException(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));
            andamento = JanelaDeProgresso.Abrir(this, Tr.T("Bacias de contribuição"), podeCancelar: true);

            try
            {
                return BaciasCommands.CalcularComARegiaoGravada(_documento, terreno.Mesh, p =>
                {
                    andamento.Avancar(p * 100, Tr.T("Descendo a água de cada célula…"));
                    return !andamento.Cancelado;
                });
            }
            catch (OperationCanceledException)
            {
                return Tr.T("BACIAS Cancelado; as bacias anteriores ficaram.");
            }
            finally
            {
                andamento.Fechar();
            }
        });

        Preencher();
    }

    private void Mandar(string comando)
    {
        try
        {
            _documento.SendStringToExecute($"\x03\x03_{comando} ", true, false, false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta (com a lista em dia).</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Preencher();
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeBacias(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            if (e.Document == documento) janela.Close();
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) =>
        {
            Abertas.Remove(documento);
            AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
        };

        AcadApp.ShowModelessWindow(janela);
    }

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    private void Falhou(Exception erro)
    {
        // Clique de WPF: exceção solta aqui derrubaria o Civil 3D.
        RegistroDeDiagnostico.Registrar("Falha na janela das bacias.", erro);
        Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
    }

    /// <summary>Roda com o documento travado e o vigia calado (janela solta) e mostra o relato.</summary>
    private void Fazer(Func<string> operacao)
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;

            string frase;
            using (DrenagemVigia.Calar())
            {
                frase = EscritaForaDeComando.Fazer(_documento, () =>
                {
                    var f = operacao();
                    _documento.Editor.Regen();
                    return f;
                });
            }

            _documento.Editor.WriteMessage($"\n{frase}\n");
            Recado(frase, false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
        finally
        {
            Cursor = null;
        }
    }
}
