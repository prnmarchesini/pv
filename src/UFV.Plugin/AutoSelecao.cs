using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// A caixa flutuante do kWp da seleção (7.8): semitransparente, no canto
/// de cima à esquerda da janela do Civil 3D, aparece quando a seleção tem
/// mesa nossa e some quando não tem, e durante comandos. Só com interface.
///
/// Escuta <c>Document.ImpliedSelectionChanged</c> de cada documento (é o
/// evento de "seleção prévia mudou"), esconde no começo de todo comando e
/// reavalia no fim (a seleção pode sobreviver a um zoom ou a um grip). A
/// conta é a de <see cref="SelecaoCommands.Resumir"/>, a mesma do comando,
/// e só é refeita quando o conjunto de MESAS tocadas muda: ids diferentes
/// da mesma mesa dão o mesmo número, sem varrer o desenho de novo.
///
/// A janela pertence à janela do Civil 3D (some quando ele minimiza, não
/// fica por cima de outros programas) e é transparente ao clique: clicar
/// onde ela está vai para o desenho.
/// </summary>
internal static class AutoSelecao
{
    private static readonly Dictionary<Document, EscutaDaSelecao> Escutas = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;
    private static CaixaDeSelecao? _caixa;
    private static int _falhasSeguidas;

    /// <summary>Depois de tantas falhas seguidas, a caixa se desliga sozinha, para não inundar o diagnóstico.</summary>
    private const int FalhasAteDesligar = 5;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (_aoCriar is not null) return;

        var documentos = AcadApp.DocumentManager;

        _aoCriar = (_, e) => Escutar(e.Document);
        _aoDestruir = (_, e) => Soltar(e.Document);

        documentos.DocumentCreated += _aoCriar;
        documentos.DocumentToBeDestroyed += _aoDestruir;

        foreach (Document documento in documentos) Escutar(documento);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        foreach (var escuta in Escutas.Values.ToList()) escuta.Soltar();
        Escutas.Clear();

        try
        {
            if (_aoCriar is not null)
            {
                AcadApp.DocumentManager.DocumentCreated -= _aoCriar;
                AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
            }

            _caixa?.Close();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao fechar a caixa da seleção.", erro);
        }

        _aoCriar = null;
        _aoDestruir = null;
        _caixa = null;
    }

    private static void Escutar(Document? documento)
    {
        if (documento is null || Escutas.ContainsKey(documento)) return;

        try
        {
            Escutas[documento] = new EscutaDaSelecao(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui escutar a seleção num desenho.", erro);
        }
    }

    private static void Soltar(Document? documento)
    {
        if (documento is null || !Escutas.Remove(documento, out var escuta)) return;

        escuta.Soltar();
        Esconder();
    }

    /// <summary>A escuta de um documento: seleção mudou, comando começou, comando acabou.</summary>
    private sealed class EscutaDaSelecao
    {
        private readonly Document _documento;
        private HashSet<Guid> _mesas = [];
        private string? _texto;

        internal EscutaDaSelecao(Document documento)
        {
            _documento = documento;
            _documento.ImpliedSelectionChanged += AoMudarSelecao;
            _documento.CommandWillStart += AoComecarComando;
            _documento.CommandEnded += AoTerminarComando;
            _documento.CommandCancelled += AoTerminarComando;
            _documento.CommandFailed += AoTerminarComando;
        }

        internal void Soltar()
        {
            try
            {
                _documento.ImpliedSelectionChanged -= AoMudarSelecao;
                _documento.CommandWillStart -= AoComecarComando;
                _documento.CommandEnded -= AoTerminarComando;
                _documento.CommandCancelled -= AoTerminarComando;
                _documento.CommandFailed -= AoTerminarComando;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar a escuta da seleção.", erro);
            }
        }

        private void AoMudarSelecao(object? sender, EventArgs e) => Atualizar();

        private void AoComecarComando(object? sender, CommandEventArgs e) => Esconder();

        private void AoTerminarComando(object? sender, CommandEventArgs e) => Atualizar();

        private void Atualizar()
        {
            if (_aoCriar is null) return;

            try
            {
                if (!string.IsNullOrEmpty(_documento.CommandInProgress))
                {
                    Esconder();
                    return;
                }

                var selecao = _documento.Editor.SelectImplied();

                if (selecao.Status != PromptStatus.OK || selecao.Value.Count == 0)
                {
                    _mesas = [];
                    _texto = null;
                    Esconder();
                    return;
                }

                var ids = selecao.Value.GetObjectIds();
                var mesas = SelecaoCommands.MesasTocadas(_documento, ids);

                if (mesas.Count == 0)
                {
                    _mesas = [];
                    _texto = null;
                    Esconder();
                    return;
                }

                if (!mesas.SetEquals(_mesas) || _texto is null)
                {
                    _mesas = mesas;
                    _texto = SelecaoCommands.Resumir(_documento, mesas).Describe();
                }

                Mostrar(_texto);
                _falhasSeguidas = 0;
            }
            catch (System.Exception erro)
            {
                // Exceção num evento derruba o Civil 3D: registrada, e a caixa
                // some. Repetindo, a escuta se desliga.
                RegistroDeDiagnostico.Registrar("Falha na caixa da seleção.", erro);
                Esconder();

                if (++_falhasSeguidas >= FalhasAteDesligar)
                {
                    RegistroDeDiagnostico.Registrar($"Caixa da seleção desligada depois de {FalhasAteDesligar} falhas seguidas.");
                    Desinstalar();
                }
            }
        }
    }

    private static void Mostrar(string texto)
    {
        _caixa ??= new CaixaDeSelecao();
        _caixa.Texto = texto;

        Posicionar(_caixa);

        if (!_caixa.IsVisible) _caixa.Show();
    }

    private static void Esconder()
    {
        if (_caixa is { IsVisible: true }) _caixa.Hide();
    }

    /// <summary>No canto de cima à esquerda da área de desenho, longe do ViewCube.</summary>
    private static void Posicionar(Window caixa)
    {
        try
        {
            var janela = AcadApp.MainWindow;
            var posicao = janela.DeviceIndependentLocation;

            caixa.Left = posicao.X + 40;
            caixa.Top = posicao.Y + 220;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui posicionar a caixa da seleção.", erro);
            caixa.Left = 100;
            caixa.Top = 200;
        }
    }
}

/// <summary>
/// A caixa: um texto sobre fundo escuro semitransparente, sem borda, filha
/// da janela do Civil 3D, transparente ao clique e sem roubar o foco.
/// </summary>
internal sealed class CaixaDeSelecao : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;

    private readonly TextBlock _texto;

    internal CaixaDeSelecao(double tamanhoDaLetra = 15, double largura = 360)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        Width = largura;
        SizeToContent = SizeToContent.Height;

        _texto = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = tamanhoDaLetra,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(14, 10, 14, 10),
        };

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 60, 140)),
            CornerRadius = new CornerRadius(8),
            Child = _texto,
        };

        // A dona é a janela do Civil 3D: a caixa fica acima dela, minimiza
        // com ela, e nunca por cima de outro programa.
        try
        {
            new WindowInteropHelper(this).Owner = AcadApp.MainWindow.Handle;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui dar dona à caixa da seleção.", erro);
        }
    }

    internal string Texto
    {
        set => _texto.Text = "Seleção: " + value;
    }

    /// <summary>O texto como vem, sem o "Seleção:" (o placar do grupo).</summary>
    internal string TextoLivre
    {
        set => _texto.Text = value;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            // Transparente ao clique e sem ativar: o clique atravessa para o
            // desenho e a linha de comando não perde o foco.
            var handle = new WindowInteropHelper(this).Handle;
            var estilo = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, estilo | WsExTransparent | WsExNoActivate | WsExToolWindow);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui tornar a caixa da seleção transparente ao clique.", erro);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
