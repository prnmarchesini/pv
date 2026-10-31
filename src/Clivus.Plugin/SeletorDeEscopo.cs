using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.EscopoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A escolha do que entra: as áreas (uma, várias ou o desenho inteiro) e as
/// mesas escolhidas no desenho. Nasceu na janela das sombras (42.1, Renan,
/// 31/10/2026: "Seleção, tudo de uma area, tudo de uma area mais seleção,
/// varias areas, desenho inteiro") e serve também à exportação para o PVsyst
/// (42.4).
/// <para>
/// Desde a tela de 31/10/2026 à tarde ("uma area pintou e na outra nao [...]
/// cade o botão de escolher area ou escolher o que eu quero"), tudo à vista:
/// uma caixa por área, com quantas mesas ela leva (a área leva as que estão
/// dentro dela, <see cref="ShadowScopePlan"/>), "Desenho inteiro", e os botões
/// "Escolher no desenho…" para a área (clique dentro dela) e para as mesas.
/// Áreas e mesas somam: área marcada e mesas escolhidas é a área mais as mesas.
/// </para>
/// <para>
/// Serve também à janela das análises (31/10/2026: "Em analise eu tbm quero
/// poder escolher o que analisar, uma area ou todas, isso para TODAS
/// analises"). Na janela solta, o "Escolher no desenho…" vai por comando
/// (<see cref="EscopoCommands"/>), que entrega a escolha ao seletor que pediu.
/// </para>
/// </summary>
internal sealed class SeletorDeEscopo
{
    /// <summary>Uma área: o GUID, o nome e quantas mesas ela leva.</summary>
    private sealed record AreaDaLista(Guid Id, string Nome, int Mesas)
    {
        public override string ToString() => Mesas > 0 ? Tr.F("{0} · {1} mesa(s)", Nome, Mesas) : Tr.F("{0} · nenhuma mesa", Nome);
    }

    private readonly Document _documento;
    private readonly Action<string, bool> _recado;
    private readonly Action<Action> _tentar;

    private readonly CheckBox _tudo = new() { Content = Tr.T("Desenho inteiro"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 14, 2), ToolTip = Tr.T("Todas as mesas do desenho, dentro e fora das áreas.") };
    private readonly WrapPanel _areas = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _selecao = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _limpar;
    private HashSet<Guid> _mesasEscolhidas = [];

    /// <param name="documento">O desenho.</param>
    /// <param name="rotulo">O rótulo da linha das áreas ("Calcular", "Exportar").</param>
    /// <param name="recado">Escreve o recado na janela (o texto e se é erro).</param>
    /// <param name="tentar">Roda o manipulador protegido (exceção solta no WPF fecha o Civil 3D).</param>
    /// <param name="escolherArea">
    /// O botão da área no desenho. A janela modal pede escondendo-se; sem
    /// ele (a janela solta), o seletor pede por comando e a janela fica aberta.
    /// </param>
    /// <param name="escolherMesas">O botão das mesas no desenho (o mesmo).</param>
    /// <param name="mesas">As mesas já escolhidas (a seleção feita antes do botão); com elas, nenhuma área vem marcada.</param>
    /// <param name="desenhoInteiro">
    /// Abre no desenho inteiro (as análises, que eram sempre do desenho
    /// inteiro); sem ele, na primeira área que tem mesa (as sombras, que
    /// travavam o PC numa usina grande).
    /// </param>
    internal SeletorDeEscopo(Document documento, string rotulo, Action<string, bool> recado, Action<Action> tentar, Action? escolherArea = null, Action? escolherMesas = null, IEnumerable<Guid>? mesas = null, bool desenhoInteiro = false)
    {
        _documento = documento;
        _recado = recado;
        _tentar = tentar;
        _mesasEscolhidas = mesas?.ToHashSet() ?? [];

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 24, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 2, 6, 2), ToolTip = dica };
            b.Click += (_, _) => tentar(acao);
            return b;
        }

        var areaNoDesenho = Botao(Tr.T("Escolher área no desenho…"), Tr.T("Clique dentro de uma área (ou na linha dela) no desenho: ela fica marcada aqui."), escolherArea ?? (() => PedirAoDesenho(PluginInfo.ComandoEscolherArea)));
        var mesasNoDesenho = Botao(Tr.T("Escolher mesas no desenho…"), Tr.T("Selecione as mesas no desenho (janela, cruzamento ou clique): elas entram junto com as áreas marcadas."), escolherMesas ?? (() => PedirAoDesenho(PluginInfo.ComandoEscolherMesas)));
        _limpar = Botao(Tr.T("Limpar"), Tr.T("Tira as mesas escolhidas."), () => DefinirMesas([]));

        // Marcar uma área desmarca o desenho inteiro, e marcar o desenho
        // inteiro desmarca as áreas: as caixas nunca ficam desligadas (a
        // janela das análises abre no desenho inteiro e a área tem de poder
        // ser marcada direto).
        _tudo.Checked += (_, _) => tentar(() =>
        {
            foreach (var c in Caixas) c.IsChecked = false;
            Aplicar();
        });
        _tudo.Unchecked += (_, _) => tentar(Aplicar);

        var linhaDasAreas = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var r1 = new TextBlock { Text = rotulo, Width = 88, Margin = new Thickness(0, 4, 6, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(r1, Dock.Left);
        linhaDasAreas.Children.Add(r1);
        var dasAreas = new WrapPanel { Orientation = Orientation.Horizontal };
        dasAreas.Children.Add(_tudo);
        dasAreas.Children.Add(_areas);
        dasAreas.Children.Add(areaNoDesenho);
        linhaDasAreas.Children.Add(dasAreas);

        var linhaDasMesas = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 3) };
        linhaDasMesas.Children.Add(new TextBlock { Text = Tr.T("Mesas"), Width = 88, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        linhaDasMesas.Children.Add(_selecao);
        linhaDasMesas.Children.Add(mesasNoDesenho);
        linhaDasMesas.Children.Add(_limpar);

        var painel = new StackPanel();
        painel.Children.Add(linhaDasAreas);
        painel.Children.Add(linhaDasMesas);
        Painel = painel;

        // Abre com a primeira área que tem mesa marcada (ou com as mesas já
        // escolhidas); sem área no desenho, no desenho inteiro.
        tentar(() => LerAreas(primeira: !desenhoInteiro && _mesasEscolhidas.Count == 0));
        if (desenhoInteiro || (_areas.Children.Count == 0 && _mesasEscolhidas.Count == 0)) _tudo.IsChecked = true;

        tentar(Aplicar);
    }

    /// <summary>As duas linhas: as áreas e as mesas.</summary>
    internal UIElement Painel { get; }

    /// <summary>Se o desenho inteiro está marcado.</summary>
    internal bool DesenhoInteiro => _tudo.IsChecked == true;

    private IEnumerable<CheckBox> Caixas => _areas.Children.OfType<CheckBox>();

    /// <summary>Lê as áreas do desenho para as caixas, mantendo as marcadas; com <paramref name="primeira"/>, marca a primeira que tem mesa.</summary>
    internal void LerAreas(bool primeira = false)
    {
        var marcadas = Caixas.Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToHashSet();

        var doDesenho = AreasDoDesenho.Varrer(_documento.Database);
        var mesas = SombrasCommands.MesasPorArea(_documento.Database, doDesenho);
        var areas = EscolhaDeArea.Distintas(doDesenho)
            .Select(a => new AreaDaLista(a.Identity.Id, a.Identity.DisplayName, mesas.TryGetValue(a.Identity.Id, out var n) ? n : 0))
            .ToList();

        if (primeira && marcadas.Count == 0 && areas.FirstOrDefault(a => a.Mesas > 0) is { } comMesa) marcadas.Add(comMesa.Id);

        _areas.Children.Clear();
        foreach (var a in areas)
        {
            var caixa = new CheckBox { Content = a.ToString(), Tag = a, IsChecked = marcadas.Contains(a.Id), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 14, 2) };
            caixa.Checked += (_, _) => _tentar(() => { _tudo.IsChecked = false; });
            _areas.Children.Add(caixa);
        }

        Aplicar();
    }

    /// <summary>Marca a área (escolhida no desenho) e desmarca o desenho inteiro.</summary>
    internal void MarcarArea(Guid id)
    {
        if (!Caixas.Any(c => ((AreaDaLista)c.Tag).Id == id)) LerAreas();

        var caixa = Caixas.FirstOrDefault(c => ((AreaDaLista)c.Tag).Id == id);
        if (caixa is null)
        {
            _recado(Tr.T("Essa área não está na lista (ela tem polilinha de área do Clivus?)."), true);
            return;
        }

        _tudo.IsChecked = false;
        caixa.IsChecked = true;
        _recado(Tr.F("Área marcada: {0}.", ((AreaDaLista)caixa.Tag).ToString()), false);
    }

    /// <summary>As mesas escolhidas no desenho (vazio: nenhuma).</summary>
    internal void DefinirMesas(IEnumerable<Guid> mesas)
    {
        _mesasEscolhidas = mesas.ToHashSet();
        if (_mesasEscolhidas.Count > 0) _tudo.IsChecked = false;
        Aplicar();
    }

    /// <summary>O seletor que pediu ao desenho, por desenho: o comando entrega a escolha a ele.</summary>
    private static readonly Dictionary<Document, SeletorDeEscopo> Pedintes = [];

    /// <summary>Pede ao desenho (a área pelo clique, as mesas pela seleção) por um comando: a janela é solta e fica aberta.</summary>
    private void PedirAoDesenho(string comando)
    {
        if (!ClivusExtension.TemInterface()) return;
        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            _recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        Pedintes[_documento] = this;

        // Dois Esc antes: um comando que estivesse no meio levaria o nome do
        // nosso como resposta.
        _documento.SendStringToExecute($"\x03\x03_{comando} ", true, false, false);
    }

    /// <summary>A janela fechou: o seletor dela deixa de esperar a escolha do desenho.</summary>
    internal void Soltar()
    {
        if (Pedintes.TryGetValue(_documento, out var esperando) && esperando == this) Pedintes.Remove(_documento);
    }

    /// <summary>O seletor que pediu ao desenho (e que deixa de esperar), ou null.</summary>
    internal static SeletorDeEscopo? QuemPediu(Document documento) => Pedintes.Remove(documento, out var seletor) ? seletor : null;

    /// <summary>Entrega a escolha do desenho ao seletor, protegida como um clique da janela.</summary>
    internal void Receber(Action escolha) => _tentar(escolha);

    /// <summary>A linha das mesas escolhidas: quantas, e o Limpar só com alguma.</summary>
    private void Aplicar()
    {
        _selecao.Text = _mesasEscolhidas.Count == 0 ? Tr.T("nenhuma mesa escolhida") : Tr.F("{0} mesa(s) escolhida(s)", _mesasEscolhidas.Count);
        _limpar.Visibility = _mesasEscolhidas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>O escopo escolhido. Lança <see cref="ArgumentException"/> com o que falta.</summary>
    internal ShadowScope Escopo()
    {
        if (DesenhoInteiro) return ShadowScope.WholeDrawing;

        var areas = Caixas.Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToList();
        var mesas = _mesasEscolhidas.ToList();

        var escopo = (areas.Count, mesas.Count) switch
        {
            (0, 0) => throw new ArgumentException(Tr.T("Marque uma área (ou o desenho inteiro), ou escolha mesas no desenho.")),
            (0, _) => new ShadowScope(ShadowScopeKind.Selection, [], mesas),
            (1, 0) => new ShadowScope(ShadowScopeKind.Area, areas, []),
            (_, 0) => new ShadowScope(ShadowScopeKind.Areas, areas, []),
            _ => new ShadowScope(ShadowScopeKind.AreaAndSelection, areas, mesas),
        };

        return escopo.WhyInvalid() is { } falta ? throw new ArgumentException(falta) : escopo;
    }
}

/// <summary>
/// O "Escolher no desenho…" das janelas soltas (sombras, análises): o clique
/// na área ou a seleção das mesas, entregue ao seletor que pediu. Sem janela
/// esperando (a janela fechou, que solta o pedido, ou o comando foi
/// digitado), só diz o que escolheu.
/// </summary>
public static class EscopoCommands
{
    /// <summary>CLIVUS_ESCOLHER_AREA: clique dentro de uma área e ela fica marcada na janela que pediu.</summary>
    [CommandMethod(PluginInfo.ComandoEscolherArea)]
    public static void EscolherArea()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var seletor = SeletorDeEscopo.QuemPediu(documento);

        try
        {
            if (EscolhaDeArea.Pedir(documento.Editor, documento) is not { } area) return;

            if (seletor is null)
            {
                documento.Editor.WriteMessage(Tr.F("\nÁrea {0}: nenhuma janela esperando a escolha.\n", area.Identity.DisplayName));
                return;
            }

            seletor.Receber(() => seletor.MarcarArea(area.Identity.Id));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao escolher a área no desenho.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui escolher a área: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_ESCOLHER_MESAS: selecione mesas no desenho e elas entram na
    /// janela que pediu (com a seleção feita antes, ela vale direto).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEscolherMesas, CommandFlags.UsePickSet)]
    public static void EscolherMesas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var seletor = SeletorDeEscopo.QuemPediu(documento);

        try
        {
            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as mesas: ") });
            if (selecao.Status != PromptStatus.OK) return;

            var mesas = SelecaoCommands.MesasTocadas(documento, selecao.Value.GetObjectIds());
            editor.WriteMessage(Tr.F("\n{0} mesa(s) escolhida(s).\n", mesas.Count));

            seletor?.Receber(() => seletor.DefinirMesas(mesas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao escolher as mesas no desenho.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui escolher as mesas: {0}\n", erro.Message));
        }
    }
}
