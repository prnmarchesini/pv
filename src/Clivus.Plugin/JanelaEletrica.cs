using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela da configuração elétrica (CLIVUS_ELETRICA, etapas 12 a 15):
/// solta, uma por desenho, abas Subestação, Transformador, Inversor e
/// Numeração (esta de <see cref="PainelDeNumeracao"/>). O que pede clique em
/// campo (alocar, posicionar, agrupar) esconde a janela e roda o comando pela
/// linha de comando; o comando devolve a janela no fim (<see cref="Voltar"/>).
/// </summary>
internal sealed class JanelaEletrica : Window
{
    private static readonly Dictionary<Document, JanelaEletrica> Abertas = [];

    private readonly List<AbaEletrica> _abas = [];

    private JanelaEletrica(Document documento)
    {
        Title = Tr.T("Configuração elétrica — Clivus Solar");
        Width = 980;
        Height = 560;
        MinWidth = 820;
        MinHeight = 440;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var abas = new TabControl { Margin = new Thickness(8) };

        void Aba(string titulo, string dica, UIElement conteudo)
        {
            abas.Items.Add(new TabItem { Header = titulo, ToolTip = dica, Content = conteudo });
            if (conteudo is not AbaEletrica a) return;
            a.AoMudar = Atualizar;
            _abas.Add(a);
        }

        Aba(Tr.T("Subestação"), Tr.T("As subestações (unidades consumidoras), os trafos de cada uma e a posição em campo."), new AbaSubestacao(documento));
        Aba(Tr.T("Transformador"), Tr.T("Os trafos: cadastro livre ou a partir de um padrão, apelido (tag) e posição em campo."), new AbaTransformador(documento));
        Aba(Tr.T("Numeração"), Tr.T("A numeração das strings (tags)."), PainelDeNumeracao.Criar(documento));

        // Trocar de aba relê o desenho (o usuário pode ter mexido no CAD).
        // O evento sobe também das listas de dentro: só o da própria TabControl conta.
        abas.SelectionChanged += (_, e) =>
        {
            try
            {
                if (e.OriginalSource == abas && abas.SelectedContent is AbaEletrica aba) aba.Atualizar();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao trocar de aba na configuração elétrica.", erro);
            }
        };

        Content = abas;
        Atualizar();
    }

    /// <summary>Relê o desenho em todas as abas.</summary>
    internal void Atualizar()
    {
        foreach (var aba in _abas) aba.Atualizar();
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            aberta.Mostrar();
            return;
        }

        var janela = new JanelaEletrica(documento);
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

    /// <summary>
    /// Depois de um comando de campo: a janela (se existe) volta e relê o
    /// desenho. Sem janela aberta (comando digitado), nada.
    /// </summary>
    internal static void Voltar(Document documento)
    {
        try
        {
            if (Abertas.TryGetValue(documento, out var janela)) janela.Mostrar();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao devolver a janela da configuração elétrica.", erro);
        }
    }

    private void Mostrar()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Atualizar();
        Activate();
    }

    /// <summary>
    /// Esconde a janela e manda o comando de campo para a linha de comando
    /// (corre no contexto do documento como qualquer comando). O argumento é
    /// a resposta à primeira pergunta do comando (o GUID do cadastro).
    /// </summary>
    internal static void Campo(Document documento, string comando, string argumento)
    {
        if (Abertas.TryGetValue(documento, out var janela)) janela.Hide();
        // Os dois ESC cancelam um comando que estivesse no meio.
        documento.SendStringToExecute($"\x03\x03_{comando} {argumento}\n", true, false, false);
    }
}

/// <summary>Uma aba da configuração elétrica: o recado no rodapé, a escrita protegida e os campos de formulário.</summary>
internal abstract class AbaEletrica : DockPanel
{
    protected readonly Document Documento;
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    protected AbaEletrica(Document documento)
    {
        Documento = documento;
        Margin = new Thickness(6);
        LastChildFill = true;
        SetDock(_recado, Dock.Bottom);
        Children.Add(_recado);
    }

    /// <summary>Relê o desenho e remonta a aba.</summary>
    internal abstract void Atualizar();

    /// <summary>Quem relê depois de uma mudança: a janela inteira (as outras abas mostram o mesmo cadastro).</summary>
    internal Action? AoMudar { get; set; }

    protected void Avisar(string texto, bool erro = false)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    protected void Limpar() => _recado.Text = string.Empty;

    /// <summary>
    /// Escreve no desenho fora de comando (trava e vigia calado), relê e mostra
    /// a frase; erro não derruba o Civil 3D, vai para o registro e o recado.
    /// </summary>
    protected void Fazer(Func<string?> operacao, bool erro = false)
    {
        try
        {
            var frase = EscritaForaDeComando.Fazer(Documento, operacao);
            (AoMudar ?? Atualizar)();
            if (frase is not null) Avisar(frase, erro);
        }
        catch (Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela da configuração elétrica.", falha);
            Avisar(Tr.F("Não consegui: {0}", falha.Message), erro: true);
        }
    }

    /// <summary>Clique de botão em try/catch: exceção num evento WPF derrubaria o Civil 3D.</summary>
    protected Button Botao(Panel onde, string texto, string dica, Action acao, double largura = 0)
    {
        var b = new Button { Content = texto, Height = 26, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 0, 8, 0), ToolTip = dica };
        if (largura > 0) b.MinWidth = largura;

        b.Click += (_, _) =>
        {
            try
            {
                acao();
            }
            catch (Exception falha)
            {
                RegistroDeDiagnostico.Registrar($"Falha no botão {texto} da configuração elétrica.", falha);
                Avisar(Tr.F("Não consegui: {0}", falha.Message), erro: true);
            }
        };

        onde.Children.Add(b);
        return b;
    }

    /// <summary>Uma linha "rótulo | caixa" na grade do formulário.</summary>
    protected static TextBox Campo(Grid grade, string rotulo, string? dica = null)
    {
        var linha = grade.RowDefinitions.Count;
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var texto = new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4), ToolTip = dica };
        var caixa = new TextBox { Height = 24, Margin = new Thickness(0, 0, 0, 4), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = dica };

        Grid.SetRow(texto, linha);
        Grid.SetRow(caixa, linha);
        Grid.SetColumn(caixa, 1);
        grade.Children.Add(texto);
        grade.Children.Add(caixa);
        return caixa;
    }

    /// <summary>A lista à esquerda e o formulário à direita (com rolagem só se faltar altura).</summary>
    protected static Grid DuasColunas(UIElement esquerda, UIElement direita)
    {
        var colunas = new Grid();
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rolagem = new ScrollViewer { Content = direita, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 0, 0, 0) };
        Grid.SetColumn(rolagem, 1);
        colunas.Children.Add(esquerda);
        colunas.Children.Add(rolagem);
        return colunas;
    }

    /// <summary>A grade do formulário: rótulo ao lado, caixa ocupando o resto.</summary>
    protected static Grid Grade(double larguraDoRotulo = 150)
    {
        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(larguraDoRotulo) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grade;
    }

    /// <summary>
    /// Número para a caixa, na cultura da tela e sem separador de milhar
    /// (13800, não 13.800): volta igual pela leitura em qualquer idioma.
    /// </summary>
    protected static string Numero(double v) => v.ToString("0.###", Tr.Culture);

    /// <summary>Lê as três medidas de um formulário; null e o recado se alguma não serve.</summary>
    protected EquipmentSize? LerTamanho(TextBox largura, TextBox comprimento, TextBox altura)
    {
        if (!NumberInput.TryParseMeasure(largura.Text, out var w) || !NumberInput.TryParseMeasure(comprimento.Text, out var l) || !NumberInput.TryParseMeasure(altura.Text, out var h))
        {
            Avisar(Tr.T("Não consigo ler a largura, o comprimento ou a altura."), erro: true);
            return null;
        }

        var tamanho = new EquipmentSize(w, l, h);
        if (!tamanho.IsValid)
        {
            Avisar(Tr.T("Largura, comprimento e altura têm que ser maiores que zero."), erro: true);
            return null;
        }

        return tamanho;
    }
}
