using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AtualizarCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ATUALIZAR (etapa 43): consulta agora a versão publicada no servidor
/// e, com versão nova, abre a janela da atualização. A consulta da abertura
/// do Civil 3D é a mesma, sozinha e uma vez por dia (<see cref="Atualizacao"/>).
/// </summary>
public static class AtualizarCommands
{
    [CommandMethod(PluginInfo.ComandoAtualizar)]
    public static void Atualizar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            editor.WriteMessage(Tr.T("\nProcurando uma versão nova do Clivus Solar...\n"));

            var consulta = Atualizacao.Consultar(doComando: true);
            if (consulta.Respondeu) Atualizacao.GravarConsulta();

            if (consulta.Erro is { } erro)
            {
                editor.WriteMessage(Tr.F("Não consegui consultar a versão publicada: {0}.\n", erro));
                return;
            }

            if (Atualizacao.MaisNova(consulta) is not { } nova)
            {
                editor.WriteMessage(consulta.Versao is null
                    ? Tr.F("O servidor ainda não tem versão publicada. Esta é a {0}.\n", Atualizacao.Instalada)
                    : Tr.F("O Clivus Solar está em dia: versão {0}.\n", Atualizacao.Instalada));
                return;
            }

            Atualizacao.Avisar(documento, nova);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao procurar a atualização.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui procurar a atualização: {0}\n", erro.Message));
        }
    }
}

/// <summary>
/// A janela da atualização: a versão nova, o que mudou (as notas da versão
/// publicada) e os botões Atualizar agora e Depois. Atualizar agora baixa o
/// instalador com a barra de andamento, confere e o deixa esperando o Civil 3D
/// fechar. Pequena: sem logo, as notas rolam só se forem longas.
/// </summary>
internal sealed class JanelaDeAtualizacao : Window
{
    private static readonly Brush Petroleo = new SolidColorBrush(Color.FromRgb(0x0F, 0x25, 0x33));
    private static readonly Brush Ambar = new SolidColorBrush(Color.FromRgb(0xF4, 0xA5, 0x1C));

    private readonly PluginRelease _versao;
    private readonly ProgressBar _barra = new() { Height = 14, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _andamento = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _atualizar;
    private readonly Button _depois;
    private CancellationTokenSource? _baixando;
    private bool _fechada;
    private int _ultimoPorcento = -1;

    internal JanelaDeAtualizacao(PluginRelease versao)
    {
        _versao = versao;

        Title = Tr.T("Clivus Solar — Atualização");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(16) };

        pilha.Children.Add(Linha(Tr.F("Há uma versão nova do Clivus Solar: {0}", versao.Version), forte: true, tamanho: 15));

        var detalhe = Tr.F("Esta é a {0}.", Atualizacao.Instalada);
        if (versao.PublishedAt is { } quando) detalhe += " " + Tr.F("Publicada em {0:dd/MM/yyyy}.", quando.ToLocalTime());
        detalhe += " " + Tr.F("Instalador de {0:0.0} MB.", versao.Size / 1024.0 / 1024.0);
        pilha.Children.Add(Linha(detalhe, cor: Brushes.DimGray));

        if (versao.Required)
            pilha.Children.Add(Linha(Tr.T("Esta atualização é obrigatória: o servidor pede que todos instalem."), forte: true, cor: Brushes.Firebrick));

        if (versao.Notes.Length > 0)
        {
            pilha.Children.Add(Linha(Tr.T("O que mudou"), forte: true, margemDeCima: 10));
            pilha.Children.Add(new Border
            {
                BorderBrush = Brushes.Gainsboro,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Child = new ScrollViewer
                {
                    MaxHeight = 220,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock { Text = versao.Notes, TextWrapping = TextWrapping.Wrap, Foreground = Petroleo },
                },
            });
        }

        pilha.Children.Add(_barra);
        pilha.Children.Add(_andamento);
        pilha.Children.Add(_recado);

        _atualizar = new Button
        {
            Content = Tr.T("Atualizar agora"),
            Padding = new Thickness(14, 0, 14, 0),
            Height = 28,
            Background = Ambar,
            Foreground = Petroleo,
            FontWeight = FontWeights.SemiBold,
            IsDefault = true,
            ToolTip = Tr.T("Baixa o instalador, confere o tamanho e o SHA-256 e o deixa esperando: feche o Civil 3D e ele instala sozinho."),
        };
        _atualizar.Click += (_, _) => Atualizar();

        _depois = new Button
        {
            Content = Tr.T("Depois"),
            Width = 90,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            // O IsCancel fecha a janela modal (e o Esc também), como na Ativação.
            IsCancel = true,
            ToolTip = versao.Required
                ? Tr.F("Fecha sem atualizar. Por ser obrigatória, o aviso volta na próxima vez que o Civil 3D abrir; o botão Atualizar ({0}) procura na hora.", PluginInfo.ComandoAtualizar)
                : Tr.F("Fecha sem atualizar. O aviso volta amanhã; o botão Atualizar ({0}) procura na hora.", PluginInfo.ComandoAtualizar),
        };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        botoes.Children.Add(_atualizar);
        botoes.Children.Add(_depois);
        pilha.Children.Add(botoes);

        // O que impede atualizar daqui, dito antes do clique.
        if (Atualizacao.JaEsperando)
        {
            Recado(Tr.T("A atualização já foi baixada e espera o Civil 3D fechar para instalar."), Brushes.ForestGreen);
            _atualizar.IsEnabled = false;
        }
        else if (!Atualizacao.InstaladoPeloInstalador)
        {
            Recado(Tr.F("Este Clivus Solar foi carregado de {0}, fora da pasta do instalador: a atualização daqui só troca o que o instalador pôs. Baixe a versão nova na área do cliente.", Atualizacao.PastaDoPlugin ?? "?"), Brushes.Firebrick);
            _atualizar.IsEnabled = false;
        }

        // Fechar no meio do download cancela (e o instalador não roda).
        Closing += (_, _) =>
        {
            _fechada = true;
            _baixando?.Cancel();
        };

        Content = pilha;
    }

    private static TextBlock Linha(string texto, bool forte = false, double tamanho = 13, Brush? cor = null, double margemDeCima = 0) => new()
    {
        Text = texto,
        TextWrapping = TextWrapping.Wrap,
        FontSize = tamanho,
        FontWeight = forte ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = cor ?? Petroleo,
        Margin = new Thickness(0, margemDeCima, 0, 4),
    };

    private void Recado(string texto, Brush cor)
    {
        _recado.Text = texto;
        _recado.Foreground = cor;
        _recado.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Atualizar agora: o download vai para outra linha, e o andamento e o fim
    /// voltam pela fila da janela (Dispatcher), sem depender de quem chamou.
    /// </summary>
    private void Atualizar()
    {
        // Clique de WPF: exceção solta derrubaria o Civil 3D.
        try
        {
            _atualizar.IsEnabled = false;
            _depois.Content = Tr.T("Cancelar");
            _barra.Visibility = Visibility.Visible;
            _andamento.Visibility = Visibility.Visible;
            _andamento.Text = Tr.T("Baixando o instalador...");
            _recado.Visibility = Visibility.Collapsed;

            _baixando = new CancellationTokenSource();
            var cancelar = _baixando.Token;
            var versao = _versao;

            Task.Run(() => Atualizacao.Baixar(versao, bytes => Dispatcher.BeginInvoke(() => Andou(bytes)), cancelar))
                .ContinueWith(t => Dispatcher.BeginInvoke(() => Baixou(t)), TaskScheduler.Default);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao começar a atualização.", erro);
            Falhou(erro.Message);
        }
    }

    private void Andou(long bytes)
    {
        if (_fechada) return;

        var porcento = (int)Math.Clamp(100.0 * bytes / Math.Max(1, _versao.Size), 0, 100);
        if (porcento == _ultimoPorcento) return;

        _ultimoPorcento = porcento;
        _barra.Value = porcento;
        _andamento.Text = Tr.F("Baixando: {0:0.0} de {1:0.0} MB", bytes / 1024.0 / 1024.0, _versao.Size / 1024.0 / 1024.0);
    }

    private void Baixou(Task<(string? Arquivo, string? Erro)> tarefa)
    {
        try
        {
            _baixando = null;
            if (_fechada) return;

            if (tarefa.IsFaulted || tarefa.IsCanceled)
            {
                RegistroDeDiagnostico.Registrar("Falha no download da atualização.", tarefa.Exception);
                Falhou(tarefa.Exception?.GetBaseException().Message ?? Tr.T("download cancelado"));
                return;
            }

            var (arquivo, erro) = tarefa.Result;
            if (arquivo is null)
            {
                Falhou(erro ?? Tr.T("download cancelado"));
                return;
            }

            _barra.Value = 100;
            _andamento.Text = Tr.T("Baixado e conferido (tamanho e SHA-256).");

            if (Atualizacao.Instalar(arquivo, _versao) is { } naoRodou)
            {
                Falhou(naoRodou);
                return;
            }

            var frase = Tr.T("Feche o Civil 3D: a atualização instala sozinha quando ele fechar.");
            Recado(frase, Brushes.ForestGreen);
            _atualizar.Visibility = Visibility.Collapsed;
            _depois.Content = Tr.T("Fechar");

            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(Tr.F("\nClivus Solar {0} baixado e conferido. {1}\n", _versao.Version, frase));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao terminar a atualização.", erro);
            Falhou(erro.Message);
        }
    }

    private void Falhou(string porQue)
    {
        Recado(Tr.F("Não atualizei: {0}.", porQue), Brushes.Firebrick);
        _barra.Visibility = Visibility.Collapsed;
        _andamento.Visibility = Visibility.Collapsed;
        _atualizar.IsEnabled = true;
        _depois.Content = Tr.T("Depois");
    }
}
