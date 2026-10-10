using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ResumoEletricoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ELETRICA_RESUMO (elétrica, 16.1): o resumo do sistema pela cadeia
/// de vínculo (subestações, trafos, inversores, strings, módulos e kWp), numa
/// janela solta com a tabela e as pendências. A conta é do Core
/// (<see cref="ElectricalSummary"/>); aqui só se lê o desenho e se mostra.
/// CLIVUS_ELETRICA_RESUMO_AUTO escreve o mesmo texto na linha de comando,
/// para o nível 2.
/// </summary>
public static class ResumoEletricoCommands
{
    [CommandMethod(PluginInfo.ComandoEletricaResumo)]
    public static void Resumo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                Escrever(documento.Editor, Ler(documento));
                return;
            }

            JanelaDeResumoEletrico.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir o resumo elétrico.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui fazer o resumo elétrico: {0}\n", erro.Message));
        }
    }

    /// <summary>O resumo na linha de comando, mais as linhas de conferência do nível 2 (números invariantes).</summary>
    [CommandMethod(PluginInfo.ComandoEletricaResumoAutomatico)]
    public static void ResumoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var lido = Ler(documento);
            Escrever(editor, lido);

            var r = lido.Resumo;
            var inv = CultureInfo.InvariantCulture;
            editor.WriteMessage(string.Format(inv, "RESUMO_TOTAIS ucs={0} trafos={1} inversores={2} strings={3} alocadas={4} livres={5} modulos={6} kwp={7:0.###} pendencias={8}\n",
                r.UnitCount, r.TransformerCount, r.InverterCount, r.StringCount, r.AllocatedStrings, r.FreeStrings, r.Modules, r.PowerKwp, r.Pending().Count));

            string Nome(string texto) => texto.Replace(' ', '_');

            foreach (var b in r.Substations ?? [])
                editor.WriteMessage(string.Format(inv, "RESUMO_BLOCO nome={0} ucs={1} strings={2} kwp={3:0.###}\n",
                    Nome(b.Substation.Name), string.Join(",", b.Units.Select(u => u.Unit.Code)), b.Strings, b.PowerKwp));

            foreach (var u in r.Units)
                foreach (var t in u.Transformers)
                    foreach (var i in t.Inverters) Inversor(i, t.Transformer.Nickname, u.Unit.Code);
            foreach (var t in r.TransformersWithoutUnit)
                foreach (var i in t.Inverters) Inversor(i, t.Transformer.Nickname, "-");
            foreach (var i in r.InvertersWithoutTransformer) Inversor(i, "-", "-");

            void Inversor(InverterSummary i, string trafo, string uc) =>
                editor.WriteMessage(string.Format(inv, "RESUMO_INVERSOR nome={0} trafo={1} uc={2} strings={3} capacidade={4} excesso={5} modulos={6} kwp={7:0.###}\n",
                    Nome(i.Inverter.Name), Nome(trafo), Nome(uc), i.Strings, i.Capacity, i.OverCapacity ? 1 : 0, i.Modules, i.PowerKwp));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_RESUMO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer o resumo elétrico: {0}\n", erro.Message));
        }
    }

    /// <summary>O resumo e os problemas de leitura dos cadastros.</summary>
    internal sealed record Lido(SystemSummary Resumo, IReadOnlyList<string> Problemas, SimulatedModulePower? Simulada = null);

    private static void Escrever(Editor editor, Lido lido)
    {
        var linhas = lido.Resumo.Lines();
        editor.WriteMessage(Tr.F("\nRESUMO {0}\n", linhas[0]));
        foreach (var linha in linhas.Skip(1)) editor.WriteMessage(linha + "\n");
        foreach (var problema in lido.Problemas) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problema));
        if (lido.Simulada is { } simulada) editor.WriteMessage("  " + simulada.Reason() + "\n");
    }

    /// <summary>
    /// Lê a cadeia do desenho: os cadastros (dicionário), as strings (XData)
    /// e a potência de cada módulo pela mesa dona (TableIdentity.ModulePowerWatts,
    /// achada pelo GUID do módulo). Mesa sem potência gravada usa a do perfil
    /// atual, contada no resumo.
    /// </summary>
    internal static Lido Ler(Document documento)
    {
        var database = documento.Database;
        var ucs = ElectricalStore.ConsumerUnits(database);
        var trafos = ElectricalStore.Transformers(database);
        var modelos = ElectricalStore.InverterModels(database);
        var inversores = ElectricalStore.Inverters(database);

        List<ElectricalString> strings;
        var potencia = new Dictionary<Guid, double?>();
        var semMesa = new HashSet<Guid>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            strings = ElectricalStore.Strings(transacao, database).Select(s => s.String).ToList();

            var potenciaDaMesa = new Dictionary<Guid, double?>();
            foreach (var (mesa, partes) in LayoutScan.Tables(transacao, database))
                potenciaDaMesa[mesa] = partes.Identity?.ModulePowerWatts;

            // Módulo cuja mesa dona não está no desenho: sem potência, contado à parte.
            foreach (var (modulo, lugar) in NumeracaoDesenho.Modulos(transacao, database))
            {
                if (potenciaDaMesa.TryGetValue(lugar.Mesa, out var p)) potencia[modulo] = p;
                else semMesa.Add(modulo);
            }
        }

        // A potência trocada pela área (item 14 de 10/10/2026) vale para todo
        // módulo, inclusive o de mesa que não está mais no desenho.
        var simulada = FonteDoModulo.Simulada(database);
        if (simulada is not null)
        {
            foreach (var m in potencia.Keys.ToList()) potencia[m] = simulada.Watts;
            foreach (var m in semMesa) potencia[m] = simulada.Watts;
            semMesa.Clear();
        }

        // Só um perfil SALVO dá a reserva: sem ele, a mesa de exemplo não é o
        // "perfil atual", e esses módulos ficam fora do kWp, contados.
        double? reserva = null;
        if (potencia.Values.Any(p => p is null))
        {
            try
            {
                reserva = MesaCommands.PrimeiroPerfil(new TableProfileStore(MesaCommands.PastaDosPerfis))?.Layout.Module.PowerWatts;
            }
            catch (System.Exception erro)
            {
                // Sem perfil, os módulos dessas mesas ficam fora do kWp, contados no resumo.
                RegistroDeDiagnostico.Registrar("Resumo elétrico sem a potência do perfil atual.", erro);
            }
        }

        // O bloco compartilhado com as UCs dele; desenho antigo (UC compartilhada
        // sem bloco) passa pela mesma leitura do cadastro, que põe a UC num bloco.
        var blocos = ElectricalStore.Substations(database);
        var cadastro = new ElectricalSetup(units: ucs.Items, substations: blocos.Items);

        var resumo = ElectricalSummary.Build(cadastro.Units, trafos.Items, modelos.Items, inversores.Items, strings, potencia, reserva, semMesa, cadastro.Substations);
        var problemas = new[] { ucs.Problem, blocos.Problem, trafos.Problem, modelos.Problem, inversores.Problem }.OfType<string>().ToList();
        return new Lido(resumo, problemas, simulada);
    }
}

/// <summary>A janela do resumo elétrico (16.1): a tabela pela cadeia, os totais e as pendências. Uma por desenho.</summary>
internal sealed class JanelaDeResumoEletrico : Window
{
    private static readonly Dictionary<Document, JanelaDeResumoEletrico> Abertas = [];

    private readonly Document _documento;
    private readonly ListView _tabela = new();
    private readonly TextBlock _total = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBlock _pendencias = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly AvisoDePotenciaSimulada _potenciaSimulada;
    private IReadOnlyList<string> _texto = [];

    /// <summary>Uma linha da tabela, como o WPF mostra.</summary>
    private sealed record Linha(string Item, string Detalhe, int Strings, int Modulos, string Kwp, string Observacao, bool Atencao, bool Forte);

    private JanelaDeResumoEletrico(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Resumo elétrico — Clivus Solar");
        Width = 860;
        Height = 560;
        MinWidth = 640;
        MinHeight = 360;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var grade = new GridView();
        grade.Columns.Add(Coluna(Tr.T("Item"), nameof(Linha.Item), 220));
        grade.Columns.Add(Coluna(Tr.T("Detalhe"), nameof(Linha.Detalhe), 130));
        grade.Columns.Add(Coluna(Tr.T("Strings"), nameof(Linha.Strings), 60));
        grade.Columns.Add(Coluna(Tr.T("Módulos"), nameof(Linha.Modulos), 70));
        grade.Columns.Add(Coluna("kWp", nameof(Linha.Kwp), 80));
        grade.Columns.Add(Coluna(Tr.T("Observação"), nameof(Linha.Observacao), 260));
        _tabela.View = grade;

        // Linha que precisa de atenção em vermelho; subestação e total em negrito.
        var estilo = new Style(typeof(ListViewItem));
        var atencao = new DataTrigger { Binding = new System.Windows.Data.Binding(nameof(Linha.Atencao)), Value = true };
        atencao.Setters.Add(new Setter(ForegroundProperty, Brushes.Firebrick));
        var forte = new DataTrigger { Binding = new System.Windows.Data.Binding(nameof(Linha.Forte)), Value = true };
        forte.Setters.Add(new Setter(FontWeightProperty, FontWeights.SemiBold));
        estilo.Triggers.Add(atencao);
        estilo.Triggers.Add(forte);
        _tabela.ItemContainerStyle = estilo;

        var botoes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var atualizar = new Button { Content = Tr.T("Atualizar"), Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 8, 0), ToolTip = Tr.T("Lê o desenho de novo.") };
        atualizar.Click += (_, _) => Atualizar();
        var copiar = new Button { Content = Tr.T("Copiar texto"), Height = 26, Padding = new Thickness(10, 0, 10, 0), ToolTip = Tr.T("Copia o resumo em texto para colar num relatório.") };
        copiar.Click += (_, _) => Copiar();
        botoes.Children.Add(atualizar);
        botoes.Children.Add(copiar);

        // Item 14 (10/10/2026): com a potência trocada pela área, o porquê e o
        // botão de voltar à configuração da mesa.
        _potenciaSimulada = new AvisoDePotenciaSimulada(documento) { AoMudar = Atualizar, Margin = new Thickness(0, 0, 0, 6) };

        var raiz = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(_potenciaSimulada, Dock.Top);
        raiz.Children.Add(_potenciaSimulada);
        DockPanel.SetDock(_total, Dock.Top);
        raiz.Children.Add(_total);
        DockPanel.SetDock(botoes, Dock.Bottom);
        raiz.Children.Add(botoes);
        var rolagem = new ScrollViewer { Content = _pendencias, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 130 };
        DockPanel.SetDock(rolagem, Dock.Bottom);
        raiz.Children.Add(rolagem);
        raiz.Children.Add(_tabela);
        Content = raiz;

        Atualizar();
    }

    private static GridViewColumn Coluna(string titulo, string campo, double largura) =>
        new() { Header = titulo, Width = largura, DisplayMemberBinding = new System.Windows.Data.Binding(campo) };

    private void Atualizar()
    {
        try
        {
            var lido = ResumoEletricoCommands.Ler(_documento);
            var r = lido.Resumo;
            _potenciaSimulada.Atualizar();

            _tabela.ItemsSource = r.Rows()
                .Select(l => new Linha(
                    new string(' ', l.Level * 4) + l.Name,
                    l.Detail,
                    l.Strings,
                    l.Modules,
                    l.PowerKwp.ToString("0.00", Tr.Culture),
                    l.Note,
                    l.Warning,
                    l.Kind is SummaryRowKind.Substation or SummaryRowKind.Unit or SummaryRowKind.Total))
                .ToList();

            _texto = r.Lines();
            _total.Text = _texto[0];

            var pendencias = r.Pending().Concat(lido.Problemas).ToList();
            _pendencias.Foreground = pendencias.Count == 0 ? Brushes.ForestGreen : Brushes.Firebrick;
            _pendencias.Text = pendencias.Count == 0
                ? Tr.T("Nenhuma pendência.")
                : Tr.F("{0} pendência(s):", pendencias.Count) + "\n" + string.Join("\n", pendencias.Select(p => "• " + p));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar o resumo elétrico.", erro);
            _texto = [];
            _pendencias.Foreground = Brushes.Firebrick;
            _pendencias.Text = Tr.F("Não consegui ler o desenho: {0}", erro.Message);
        }
    }

    private void Copiar()
    {
        try
        {
            if (_texto.Count == 0)
            {
                _pendencias.Text = Tr.T("Não há resumo para copiar: use Atualizar.");
                return;
            }

            Clipboard.SetText(string.Join(Environment.NewLine, _texto));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao copiar o resumo elétrico.", erro);
            _pendencias.Text = Tr.F("Não consegui copiar: {0}", erro.Message);
        }
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente (atualizada) a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Atualizar();
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeResumoEletrico(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            if (e.Document == documento) janela.Close();
        }

        void Soltar()
        {
            Abertas.Remove(documento);
            AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) => Soltar();

        try
        {
            AcadApp.ShowModelessWindow(janela);
        }
        catch
        {
            // A janela nunca apareceu: sem isto, o próximo comando só ativaria uma janela invisível.
            Soltar();
            throw;
        }
    }
}
