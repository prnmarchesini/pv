using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A janela das tags (passo 8.14, no mesmo molde da janela de análises):
/// uma aba por tag (Fileiras, Mesas, Módulos, Strings), cada uma com
/// inserir e apagar; a de strings pede os módulos por string.
/// </summary>
internal sealed class JanelaDeTags : Window
{
    /// <summary>Se o usuário pediu as tags de fileira: a janela fecha e o comando as faz (pedem cliques no desenho).</summary>
    internal bool Fileiras { get; private set; }

    internal JanelaDeTags(Database database, Autodesk.AutoCAD.EditorInput.Editor editor, Action atualizarTela)
    {
        Title = "UFV — Tags";
        Width = 560;
        Height = 420;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var abas = new TabControl { Margin = new Thickness(10) };

        foreach (var tipo in new[] { TagKind.Row, TagKind.Table, TagKind.Module, TagKind.String })
        {
            var titulo = tipo switch { TagKind.Row => "Fileiras", TagKind.Table => "Mesas", TagKind.Module => "Módulos", _ => "Strings" };
            abas.Items.Add(new TabItem { Header = titulo, Content = Aba(tipo, database, editor, atualizarTela, () => { Fileiras = true; DialogResult = true; }), ToolTip = $"Tags de {Tags.Name(tipo)}." });
        }

        var fechar = new Button { Content = "Fechar", Width = 90, Height = 26, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, ToolTip = "Fecha a janela; o que foi feito já está no desenho." };

        var raiz = new DockPanel();
        DockPanel.SetDock(fechar, Dock.Bottom);
        raiz.Children.Add(fechar);
        raiz.Children.Add(abas);
        Content = raiz;
    }

    private static UIElement Aba(TagKind tipo, Database database, Autodesk.AutoCAD.EditorInput.Editor editor, Action atualizarTela, Action pedirFileiras)
    {
        var pilha = new StackPanel { Margin = new Thickness(12) };
        var recado = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var tamanho = new TextBox { Width = 70, Height = 24, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Quantos módulos cada string tem. A string não atravessa mesa: o que sobra numa mesa sai com asterisco (S2*)." };

        pilha.Children.Add(new TextBlock
        {
            Text = tipo switch
            {
                TagKind.Row => "\"F1\", \"F2\"... na ponta de cada fileira, para fora das mesas. Inserir fecha esta janela e pede no desenho: uma mesa da primeira fileira, uma da última (isso numera fileiras e mesas, F1.1...) e um clique do lado onde as tags vão.",
                TagKind.Table => "\"F1.1\", \"F1.2\"... no meio de cada mesa.",
                TagKind.Module => "O número de cada módulo dentro da mesa, em serpentina: a fileira de baixo da esquerda para a direita, a de cima voltando.",
                _ => "\"S1\", \"S2\"... no meio de cada string, na ordem das mesas e em serpentina dentro da mesa.",
            },
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 10),
        });

        if (tipo == TagKind.String)
        {
            var gravado = TagsCommands.TamanhoGravado(database);
            tamanho.Text = gravado > 0 ? gravado.ToString(CultureInfo.InvariantCulture) : "";

            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            linha.Children.Add(new TextBlock { Text = "Módulos por string", VerticalAlignment = VerticalAlignment.Center });
            linha.Children.Add(tamanho);
            pilha.Children.Add(linha);
        }

        void Fazer(string oQue, Func<string> operacao)
        {
            try
            {
                var frase = operacao();
                atualizarTela();
                editor.WriteMessage($"\nTAGS {frase}\n");
                JanelaDeAnalises.Dizer(recado, true, frase);
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Falha na janela de tags ({oQue}).", erro);
                JanelaDeAnalises.Dizer(recado, false, $"Não consegui {oQue}: {erro.Message}");
            }
        }

        var botoes = new StackPanel { Orientation = Orientation.Horizontal };
        var inserir = new Button { Content = "Inserir", Height = 28, MinWidth = 120, Margin = new Thickness(0, 0, 8, 0), ToolTip = $"Escreve as tags de {Tags.Name(tipo)}; as que já existiam saem antes." };
        var apagar = new Button { Content = "Apagar", Height = 28, MinWidth = 120, ToolTip = $"Apaga as tags de {Tags.Name(tipo)}; as outras tags ficam." };

        inserir.Click += (_, _) =>
        {
            if (tipo == TagKind.Row)
            {
                pedirFileiras();
                return;
            }

            var porString = 1;

            if (tipo == TagKind.String)
            {
                if (!int.TryParse(tamanho.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out porString) || porString < 1 || porString > 200)
                {
                    JanelaDeAnalises.Dizer(recado, false, "Os módulos por string precisam ser um inteiro de 1 a 200.");
                    return;
                }
            }

            Fazer("inserir as tags", () =>
            {
                if (tipo == TagKind.String) TagsCommands.GravarTamanho(database, porString);

                var (criadas, mesas, incompletas) = TagsCommands.Inserir(database, tipo, porString);
                return mesas == 0
                    ? "O desenho não tem mesa gerada pelo plugin."
                    : $"{criadas} tag(s) de {Tags.Name(tipo)} em {mesas} mesa(s)."
                      + (incompletas > 0 ? $" {incompletas} string(s) incompleta(s), com asterisco." : "");
            });
        };

        apagar.Click += (_, _) => Fazer("apagar as tags", () => $"{TagsCommands.Apagar(database, tipo)} tag(s) de {Tags.Name(tipo)} apagada(s).");

        botoes.Children.Add(inserir);
        botoes.Children.Add(apagar);
        pilha.Children.Add(botoes);
        pilha.Children.Add(recado);

        return pilha;
    }
}
