using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela das análises de sombra (42.1, Renan, 31/10/2026: "quero que vc
/// coloque um botão ver analises, e ai abre uma janela com TOPICOs, onde eu
/// clico e ele me guia para o local da sombra, porque do jeito que ta um
/// super texto é inutil NINGUEM vai ver").
/// <para>
/// Os tópicos são área › mesa › módulo, do pior para o melhor
/// (<see cref="ShadowTopics.Build"/>), lidos do registro dos motivos (o
/// último cálculo de cada mesa). O clique num tópico leva ao lugar: zoom na
/// área, na mesa ou no módulo, e a mesa ou o módulo fica selecionado.
/// </para>
/// <para>
/// Solta, como a de Áreas: dá para olhar o desenho e voltar; o Atualizar lê
/// de novo (depois de gerar as sombras de outra área). Todo clique passa por
/// <see cref="Tentar"/>. Compacta (regra de 02/10/2026).
/// </para>
/// </summary>
internal sealed class JanelaDeAnalisesDeSombra : Window
{
    private static readonly Dictionary<Document, JanelaDeAnalisesDeSombra> Abertas = [];

    private readonly Document _documento;
    private readonly TreeView _arvore = new() { MinHeight = 260, MaxHeight = 520 };
    private readonly TextBlock _titulo = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    /// <summary>Os comandos que mudam o que a lista mostra: ela se lê de novo quando um deles termina.</summary>
    private static readonly HashSet<string> ComandosQueMudamAsSombras = new(StringComparer.OrdinalIgnoreCase)
    {
        PluginInfo.ComandoSombrasAutomatico, PluginInfo.ComandoSombrasEscopoAutomatico, PluginInfo.ComandoSombrasApagar,
        PluginInfo.ComandoSombrasApagarAutomatico, PluginInfo.ComandoApagar, "U", "UNDO", "REDO",
    };

    private JanelaDeAnalisesDeSombra(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Análises de sombra");
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _arvore.SelectedItemChanged += (_, _) => Tentar(MostrarEscolhido);

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => Tentar(acao);
            return b;
        }

        var abrirTudo = Botao(Tr.T("Abrir tudo"), Tr.T("Abre todos os tópicos, até os módulos."), () => Abrir(true));
        var fecharTudo = Botao(Tr.T("Fechar tudo"), Tr.T("Fecha os tópicos, deixando só as áreas."), () => Abrir(false));
        var atualizar = Botao(Tr.T("Atualizar"), Tr.T("Lê o desenho de novo (depois de gerar ou apagar as sombras com a janela aberta)."), Preencher);
        var fechar = new Button { Content = Tr.T("Fechar"), Height = 26, Width = 80, IsCancel = true, ToolTip = Tr.T("Fecha a janela; as sombras ficam.") };
        fechar.Click += (_, _) => Tentar(Close);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var b in new UIElement[] { abrirTudo, fecharTudo, atualizar, fechar }) botoes.Children.Add(b);

        var corpo = new StackPanel();
        corpo.Children.Add(_titulo);
        corpo.Children.Add(_arvore);
        corpo.Children.Add(new TextBlock
        {
            Text = Tr.T("Clique num tópico para ir até ele no desenho. A fração é a maior parte da face na sombra no período do último cálculo da mesa; a causa e a hora são as desse pior momento."),
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });
        corpo.Children.Add(_recado);
        corpo.Children.Add(botoes);

        Content = new Border { Padding = new Thickness(12), Child = corpo };

        Tentar(Preencher);

        // Os comandos que mudam as sombras terminam depois; a janela de
        // sombras gera e apaga fora de comando e avisa.
        _documento.CommandEnded += AoTerminarComando;
        SombrasCommands.Mudaram += AoMudarem;
        Closed += (_, _) =>
        {
            try
            {
                _documento.CommandEnded -= AoTerminarComando;
                SombrasCommands.Mudaram -= AoMudarem;
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar a janela das análises de sombra.", erro);
            }
        };
    }

    private void AoTerminarComando(object? sender, CommandEventArgs e)
    {
        if (!ComandosQueMudamAsSombras.Contains(e.GlobalCommandName)) return;
        Dispatcher.BeginInvoke(new Action(() => Tentar(Preencher)));
    }

    private void AoMudarem(Document documento)
    {
        if (documento != _documento) return;
        Dispatcher.BeginInvoke(new Action(() => Tentar(Preencher)));
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

        var janela = new JanelaDeAnalisesDeSombra(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document == documento) janela.Close();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao fechar a janela das análises de sombra junto com o desenho.", erro);
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
                RegistroDeDiagnostico.Registrar("Falha ao soltar a janela das análises de sombra.", erro);
            }
        };

        AcadApp.ShowModelessWindow(janela);
    }

    /// <summary>Lê os tópicos do desenho e monta a árvore: as áreas abertas, as mesas fechadas.</summary>
    private void Preencher()
    {
        IReadOnlyList<ShadowTopic> topicos;
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            topicos = SombrasCommands.Analises(_documento);
        }
        finally
        {
            Cursor = null;
        }

        _arvore.Items.Clear();
        foreach (var area in topicos) _arvore.Items.Add(No(area, aberto: true));

        var modulos = topicos.Sum(a => a.Children.Sum(m => m.Children.Count));
        var mesas = topicos.Sum(a => a.Children.Count);
        _titulo.Text = topicos.Count == 0
            ? Tr.T("Nenhum módulo com sombra guardado: gere as sombras (Sombras > Gerar sombras).")
            : Tr.F("{0} módulo(s) com sombra em {1} mesa(s), {2} área(s); o pior primeiro.", modulos, mesas, topicos.Count);
        _recado.Text = string.Empty;
    }

    private static TreeViewItem No(ShadowTopic topico, bool aberto)
    {
        var item = new TreeViewItem
        {
            Header = new TextBlock { Text = topico.Text, Foreground = topico.Worst >= 0.5 ? Brushes.DarkMagenta : Brushes.Black },
            Tag = topico,
            IsExpanded = aberto,
        };

        foreach (var filho in topico.Children) item.Items.Add(No(filho, aberto: false));
        return item;
    }

    private void Abrir(bool aberto)
    {
        void Mudar(ItemCollection itens, int nivel)
        {
            foreach (var i in itens.OfType<TreeViewItem>())
            {
                i.IsExpanded = aberto || nivel == 0;
                Mudar(i.Items, nivel + 1);
            }
        }

        Mudar(_arvore.Items, 0);
    }

    /// <summary>O tópico escolhido no desenho: zoom nele (e a seleção, na mesa e no módulo).</summary>
    private void MostrarEscolhido()
    {
        if ((_arvore.SelectedItem as TreeViewItem)?.Tag is not ShadowTopic topico) return;

        if (topico.Handle is not { } handle)
        {
            Recado(Tr.T("Este tópico não tem um lugar no desenho (fora das áreas): abra-o e escolha uma mesa."), true);
            return;
        }

        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        if (!SombrasCommands.Mostrar(_documento, handle))
        {
            Recado(Tr.T("Isso não está mais no desenho (apagado depois do cálculo): use Atualizar ou gere as sombras de novo."), true);
            return;
        }

        _recado.Text = string.Empty;
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
            RegistroDeDiagnostico.Registrar("Falha na janela das análises de sombra.", erro);
            Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
        }
    }

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }
}
