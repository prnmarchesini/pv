using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// O painel dos grupos (7.9): uma paleta do AutoCAD com uma lista WPF, um
/// grupo por linha com mesas, módulos, pilares e kWp, e os botões
/// Atualizar, Selecionar, Recalcular e Apagar para o grupo escolhido. As
/// ações vão pela linha de comando (<c>SendStringToExecute</c>), para
/// correrem no contexto do documento como qualquer comando.
/// </summary>
internal static class PainelDeGrupos
{
    private static PaletteSet? _paleta;
    private static ListView? _lista;
    private static TextBlock? _rodape;
    private static DocumentCollectionEventHandler? _aoAtivar;
    private static DocumentCollectionEventHandler? _aoDestruir;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Mostrar()
    {
        if (_paleta is null)
        {
            // A lista é do documento ativo: trocar de desenho recarrega, e
            // fechar o último limpa.
            _aoAtivar = (_, _) => Atualizar();
            _aoDestruir = (_, _) => Limpar();
            AcadApp.DocumentManager.DocumentActivated += _aoAtivar;
            AcadApp.DocumentManager.DocumentToBeDestroyed += _aoDestruir;

            _paleta = new PaletteSet("Clivus Solar: grupos", new Guid("6B4D2E0A-6E7C-4B5B-9C3B-2F0A7D9E1C21"))
            {
                Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton,
                MinimumSize = new System.Drawing.Size(360, 240),
            };

            _paleta.AddVisual("Grupos", Montar());
        }

        _paleta.Visible = true;
        Atualizar();
    }

    /// <summary>Recarrega a lista, se o painel existe. Chamado pelos comandos de grupo.</summary>
    internal static void Atualizar()
    {
        if (_paleta is null || _lista is null) return;

        try
        {
            AtualizarLista();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar o painel dos grupos.", erro);
        }
    }

    /// <summary>Solta os eventos e fecha a paleta, no Terminate.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        try
        {
            if (_aoAtivar is not null) AcadApp.DocumentManager.DocumentActivated -= _aoAtivar;
            if (_aoDestruir is not null) AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
            _aoAtivar = null;
            _aoDestruir = null;

            if (_paleta is not null)
            {
                _paleta.Visible = false;
                _paleta.Dispose();
                _paleta = null;
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao fechar o painel dos grupos.", erro);
        }
    }

    private static void Limpar()
    {
        if (_lista is null || _rodape is null) return;

        _lista.ItemsSource = null;
        _rodape.Text = "Nenhum desenho aberto.";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AtualizarLista()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;

        if (documento is null || _lista is null || _rodape is null) return;

        var resumos = GrupoCommands.Resumir(documento);

        _lista.ItemsSource = resumos.Select(r => new Linha(
            r.Group.Number, r.Group.Name, r.Census.Tables, r.Census.Modules, r.Census.Pillars,
            r.Census.PowerKwp.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            r.Census.Dirty, r.MissingTables)).ToList();

        _rodape.Text = resumos.Count == 0
            ? "Nenhum grupo. Selecione mesas e clique em Criar grupo."
            : $"{resumos.Count} grupo(s). Escolha um e use os botões.";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static UIElement Montar()
    {
        var raiz = new DockPanel { Margin = new Thickness(8) };

        var botoes = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        botoes.Children.Add(Botao("Criar grupo", PluginInfo.ComandoGrupoCriar, comNome: false));
        botoes.Children.Add(Botao("Atualizar", null, comNome: false));
        botoes.Children.Add(Botao("Selecionar", PluginInfo.ComandoGrupoSelecionar, comNome: true));
        botoes.Children.Add(Botao("Recalcular", PluginInfo.ComandoGrupoRecalcular, comNome: true));
        botoes.Children.Add(Botao("Apagar", PluginInfo.ComandoGrupoApagar, comNome: true));
        DockPanel.SetDock(botoes, Dock.Top);
        raiz.Children.Add(botoes);

        _rodape = new TextBlock { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(_rodape, Dock.Bottom);
        raiz.Children.Add(_rodape);

        var grade = new GridView();
        grade.Columns.Add(Coluna("Nº", nameof(Linha.Numero), 35));
        grade.Columns.Add(Coluna("Grupo", nameof(Linha.Nome), 120));
        grade.Columns.Add(Coluna("Mesas", nameof(Linha.Mesas), 50));
        grade.Columns.Add(Coluna("Módulos", nameof(Linha.Modulos), 60));
        grade.Columns.Add(Coluna("Pilares", nameof(Linha.Pilares), 55));
        grade.Columns.Add(Coluna("kWp", nameof(Linha.Kwp), 60));
        grade.Columns.Add(Coluna("Sujas", nameof(Linha.Sujas), 45));
        grade.Columns.Add(Coluna("Sumidas", nameof(Linha.Sumidas), 55));

        _lista = new ListView { View = grade, SelectionMode = SelectionMode.Single };
        raiz.Children.Add(_lista);

        return raiz;
    }

    private static GridViewColumn Coluna(string titulo, string campo, double largura) =>
        new() { Header = titulo, Width = largura, DisplayMemberBinding = new System.Windows.Data.Binding(campo) };

    private static Button Botao(string rotulo, string? comando, bool comNome)
    {
        var botao = new Button { Content = rotulo, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };

        botao.Click += (_, _) =>
        {
            try
            {
                if (comando is null)
                {
                    AtualizarLista();
                    return;
                }

                var documento = AcadApp.DocumentManager.MdiActiveDocument;
                if (documento is null) return;

                if (comNome)
                {
                    if (_lista?.SelectedItem is not Linha linha)
                    {
                        if (_rodape is not null) _rodape.Text = "Escolha um grupo na lista primeiro.";
                        return;
                    }

                    // O nome vai cru como resposta ao prompt do comando, que
                    // aceita espaço (AllowSpaces) e vai até o Enter; entre
                    // aspas ele chegaria com as aspas.
                    documento.SendStringToExecute($"_{comando} {linha.Nome}\n", true, false, false);
                }
                else
                {
                    documento.SendStringToExecute($"_{comando}\n", true, false, false);
                }
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Falha no botão {rotulo} do painel dos grupos.", erro);
            }
        };

        return botao;
    }

    /// <summary>Uma linha da lista.</summary>
    private sealed record Linha(int Numero, string Nome, int Mesas, int Modulos, int Pilares, string Kwp, int Sujas, int Sumidas);
}
