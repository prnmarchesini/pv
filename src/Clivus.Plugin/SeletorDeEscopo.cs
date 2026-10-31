using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A escolha do que entra: as mesas escolhidas no desenho, uma área, uma área
/// e mais as mesas escolhidas, várias áreas ou o desenho inteiro. Nasceu na
/// janela das sombras (42.1, Renan, 31/10/2026: "Seleção, tudo de uma area,
/// tudo de uma area mais seleção, varias areas, desenho inteiro") e serve
/// também à exportação para o PVsyst (42.4: "tenho que ter a liberdade de
/// exportar uma area, varias, todas, e pode selecionar as mesas que eu quero
/// se for o caso").
/// <para>
/// A área leva as que estão dentro dela (<see cref="ShadowScopePlan"/>), e a
/// lista diz com quantas mesas cada uma vai; abre na primeira que tem mesa
/// (42.3: a "Area 1" do Aurora abria escolhida e não tinha mesa).
/// </para>
/// </summary>
internal sealed class SeletorDeEscopo
{
    /// <summary>Uma área na lista: o GUID, o nome e quantas mesas ela leva (com as áreas dentro dela).</summary>
    private sealed record AreaDaLista(Guid Id, string Nome, int Mesas)
    {
        public override string ToString() => Mesas > 0 ? Tr.F("{0} · {1} mesa(s)", Nome, Mesas) : Tr.F("{0} · nenhuma mesa", Nome);
    }

    private readonly Document _documento;
    private readonly Action<string, bool> _recado;

    private readonly ComboBox _escopo = new() { Width = 150 };
    private readonly ComboBox _area = new() { Width = 220, Margin = new Thickness(6, 0, 0, 0), ToolTip = Tr.T("A área: as mesas com o centro nela (e nas áreas dentro dela) e o chão dela.") };
    private readonly WrapPanel _areas = new() { Margin = new Thickness(76, 2, 0, 2) };
    private readonly Button _pegar;
    private readonly TextBlock _selecao = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, Margin = new Thickness(6, 0, 0, 0) };
    private HashSet<Guid> _mesasEscolhidas = [];

    /// <param name="documento">O desenho.</param>
    /// <param name="rotulo">O rótulo da linha ("Calcular", "Exportar").</param>
    /// <param name="dica">A dica da lista dos tipos.</param>
    /// <param name="recado">Escreve o recado na janela (o texto e se é erro).</param>
    /// <param name="tentar">Roda o manipulador protegido (exceção solta no WPF fecha o Civil 3D).</param>
    /// <param name="mesas">As mesas já escolhidas (a seleção feita antes do botão); com elas, abre em Seleção.</param>
    internal SeletorDeEscopo(Document documento, string rotulo, string dica, Action<string, bool> recado, Action<Action> tentar, IEnumerable<Guid>? mesas = null)
    {
        _documento = documento;
        _recado = recado;
        _escopo.ToolTip = dica;

        _pegar = new Button { Content = Tr.T("Pegar seleção"), Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(6, 0, 0, 0), ToolTip = Tr.T("Usa as mesas selecionadas agora no desenho (clique nelas com a janela aberta e depois aqui).") };
        _pegar.Click += (_, _) => tentar(PegarSelecao);

        foreach (var k in ShadowScope.Kinds) _escopo.Items.Add(ShadowScope.Name(k));
        _mesasEscolhidas = mesas?.ToHashSet() ?? [];
        _escopo.SelectedIndex = ShadowScope.Kinds.ToList().IndexOf(_mesasEscolhidas.Count > 0 ? ShadowScopeKind.Selection : ShadowScopeKind.Area);
        _escopo.SelectionChanged += (_, _) => tentar(Aplicar);
        _area.DropDownOpened += (_, _) => tentar(LerAreas);

        var linha = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        linha.Children.Add(new TextBlock { Text = rotulo, Width = 70, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        linha.Children.Add(_escopo);
        linha.Children.Add(_area);
        linha.Children.Add(_pegar);
        linha.Children.Add(_selecao);
        Linha = linha;

        // Abre em "Uma área" (ou na seleção feita antes); sem área no desenho, no desenho inteiro.
        tentar(LerAreas);
        if (_area.Items.Count == 0 && _mesasEscolhidas.Count == 0)
            _escopo.SelectedIndex = ShadowScope.Kinds.ToList().IndexOf(ShadowScopeKind.Drawing);

        tentar(Aplicar);
    }

    /// <summary>A linha do rótulo, do tipo, da área e da seleção.</summary>
    internal UIElement Linha { get; }

    /// <summary>As caixas das várias áreas (logo abaixo da linha).</summary>
    internal UIElement Caixas => _areas;

    /// <summary>O tipo escolhido.</summary>
    internal ShadowScopeKind Tipo => _escopo.SelectedIndex >= 0 ? ShadowScope.Kinds[_escopo.SelectedIndex] : ShadowScopeKind.Area;

    /// <summary>Lê as áreas do desenho para a lista e as caixas, mantendo as escolhidas.</summary>
    internal void LerAreas()
    {
        var antes = (_area.SelectedItem as AreaDaLista)?.Id;
        var marcadas = _areas.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToHashSet();

        var doDesenho = AreasDoDesenho.Varrer(_documento.Database);
        var mesas = SombrasCommands.MesasPorArea(_documento.Database, doDesenho);
        var areas = EscolhaDeArea.Distintas(doDesenho)
            .Select(a => new AreaDaLista(a.Identity.Id, a.Identity.DisplayName, mesas.TryGetValue(a.Identity.Id, out var n) ? n : 0))
            .ToList();

        _area.ItemsSource = areas;
        _area.SelectedItem = areas.FirstOrDefault(a => a.Id == antes) ?? areas.FirstOrDefault(a => a.Mesas > 0) ?? areas.FirstOrDefault();

        _areas.Children.Clear();
        foreach (var a in areas)
            _areas.Children.Add(new CheckBox { Content = a.ToString(), Tag = a, IsChecked = marcadas.Contains(a.Id), Margin = new Thickness(0, 2, 14, 2) });

        if (areas.Count == 0 && Tipo is not (ShadowScopeKind.Selection or ShadowScopeKind.Drawing))
            _recado(Tr.T("Nenhuma área neste desenho: use Seleção ou Desenho inteiro (ou crie a área com o botão Área)."), true);
    }

    /// <summary>O tipo mostra só o que vale nele.</summary>
    internal void Aplicar()
    {
        var tipo = Tipo;

        _area.Visibility = tipo is ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection ? Visibility.Visible : Visibility.Collapsed;
        _areas.Visibility = tipo == ShadowScopeKind.Areas ? Visibility.Visible : Visibility.Collapsed;

        var selecao = tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection;
        _pegar.Visibility = selecao ? Visibility.Visible : Visibility.Collapsed;
        _selecao.Visibility = selecao ? Visibility.Visible : Visibility.Collapsed;
        MostrarSelecao();
    }

    /// <summary>As mesas selecionadas agora no desenho.</summary>
    internal void PegarSelecao()
    {
        var implicita = _documento.Editor.SelectImplied();
        var ids = implicita.Status == PromptStatus.OK ? implicita.Value.GetObjectIds() : [];
        _mesasEscolhidas = SelecaoCommands.MesasTocadas(_documento, ids);
        MostrarSelecao();

        if (_mesasEscolhidas.Count == 0) _recado(Tr.T("Nenhuma mesa selecionada no desenho: clique nas mesas (com a janela aberta) e use Pegar seleção."), true);
        else _recado(string.Empty, false);
    }

    private void MostrarSelecao() =>
        _selecao.Text = _mesasEscolhidas.Count == 0 ? Tr.T("nenhuma mesa escolhida") : Tr.F("{0} mesa(s) escolhida(s)", _mesasEscolhidas.Count);

    /// <summary>O escopo escolhido; sem seleção guardada, a do desenho agora. Lança <see cref="ArgumentException"/> com o que falta.</summary>
    internal ShadowScope Escopo()
    {
        var tipo = Tipo;

        if (tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection && _mesasEscolhidas.Count == 0) PegarSelecao();

        IReadOnlyList<Guid> areas = tipo switch
        {
            ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection => _area.SelectedItem is AreaDaLista a ? [a.Id] : [],
            ShadowScopeKind.Areas => _areas.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToList(),
            _ => [],
        };

        var comSelecao = tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection;
        var escopo = new ShadowScope(tipo, areas, comSelecao ? [.. _mesasEscolhidas] : []);
        return escopo.WhyInvalid() is { } falta ? throw new ArgumentException(falta) : escopo;
    }
}
