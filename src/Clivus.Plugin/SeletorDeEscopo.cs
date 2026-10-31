using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

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
    /// <param name="escolherArea">O botão da área no desenho (a janela sabe se pede por comando ou escondendo-se).</param>
    /// <param name="escolherMesas">O botão das mesas no desenho.</param>
    /// <param name="mesas">As mesas já escolhidas (a seleção feita antes do botão); com elas, nenhuma área vem marcada.</param>
    internal SeletorDeEscopo(Document documento, string rotulo, Action<string, bool> recado, Action<Action> tentar, Action escolherArea, Action escolherMesas, IEnumerable<Guid>? mesas = null)
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

        var areaNoDesenho = Botao(Tr.T("Escolher área no desenho…"), Tr.T("Clique dentro de uma área (ou na linha dela) no desenho: ela fica marcada aqui."), escolherArea);
        var mesasNoDesenho = Botao(Tr.T("Escolher mesas no desenho…"), Tr.T("Selecione as mesas no desenho (janela, cruzamento ou clique): elas entram junto com as áreas marcadas."), escolherMesas);
        _limpar = Botao(Tr.T("Limpar"), Tr.T("Tira as mesas escolhidas."), () => DefinirMesas([]));

        _tudo.Checked += (_, _) => tentar(Aplicar);
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
        tentar(() => LerAreas(primeira: _mesasEscolhidas.Count == 0));
        if (_areas.Children.Count == 0 && _mesasEscolhidas.Count == 0) _tudo.IsChecked = true;

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

    /// <summary>O desenho inteiro tira a escolha das áreas e das mesas (fica tudo).</summary>
    private void Aplicar()
    {
        var tudo = DesenhoInteiro;
        foreach (var c in Caixas) c.IsEnabled = !tudo;

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
