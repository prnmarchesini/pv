using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Inversor (etapa 14): à esquerda os modelos de inversor (MPPT,
/// entradas por MPPT, total derivado, dimensão); à direita os inversores da
/// usina.
/// </summary>
internal sealed class AbaInversor : AbaEletrica
{
    private readonly ListBox _modelos = new() { MinHeight = 110 };
    private readonly TextBox _nomeDoModelo, _mppt, _entradas, _largura, _comprimento, _altura;
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold };

    private readonly ComboBox _modeloParaCriar = new() { Height = 26, MinWidth = 160, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _quantos = new() { Text = "1", Width = 50, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ListBox _inversores = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };

    private ElectricalSetup _setup = new();
    private IReadOnlyDictionary<Guid, int> _contagem = new Dictionary<Guid, int>();

    internal AbaInversor(Document documento) : base(documento)
    {
        // ------------------------------------------------- modelos (14.1)
        var grade = Grade(140);
        _nomeDoModelo = Campo(grade, Tr.T("Nome do modelo"), Tr.T("Genérico do cliente ou cadastrado (ex. Huawei 250)."));
        _mppt = Campo(grade, Tr.T("MPPT"), Tr.T("Quantos MPPT o inversor tem."));
        _entradas = Campo(grade, Tr.T("Entradas por MPPT"), Tr.T("Quantas strings entram em cada MPPT."));

        var linha = grade.RowDefinitions.Count;
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var rotulo = new TextBlock { Text = Tr.T("Total de entradas"), Margin = new Thickness(0, 0, 8, 4) };
        Grid.SetRow(rotulo, linha);
        Grid.SetRow(_total, linha);
        Grid.SetColumn(_total, 1);
        grade.Children.Add(rotulo);
        grade.Children.Add(_total);

        _largura = Campo(grade, Tr.T("Largura (m)"), Tr.T("Medida em X do retângulo em campo."));
        _comprimento = Campo(grade, Tr.T("Comprimento (m)"), Tr.T("Medida em Y do retângulo em campo."));
        _altura = Campo(grade, Tr.T("Altura (m)"), Tr.F("Altura do retângulo 3D (a base flutua {0:0.00} m acima do terreno).", Clivus.Geo.EquipmentFootprint.FloatHeight));

        _mppt.TextChanged += (_, _) => MostrarTotal();
        _entradas.TextChanged += (_, _) => MostrarTotal();

        var botoesDoModelo = new WrapPanel();
        Botao(botoesDoModelo, Tr.T("Novo modelo"), Tr.T("Cria um modelo genérico (1 MPPT, 1 entrada) para ajustar."), NovoModelo);
        Botao(botoesDoModelo, Tr.T("Salvar modelo"), Tr.T("Grava o modelo escolhido no desenho."), SalvarModelo);
        Botao(botoesDoModelo, Tr.T("Apagar modelo"), Tr.T("Tira o modelo do cadastro (só se nenhum inversor é dele)."), ApagarModelo);

        var modelos = new StackPanel();
        modelos.Children.Add(Titulo(Tr.T("Modelos de inversor")));
        modelos.Children.Add(_modelos);
        modelos.Children.Add(botoesDoModelo);
        modelos.Children.Add(grade);

        // ---------------------------------------------- inversores (14.2)
        var criar = new WrapPanel();
        criar.Children.Add(new TextBlock { Text = Tr.T("Modelo:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        criar.Children.Add(_modeloParaCriar);
        criar.Children.Add(new TextBlock { Text = Tr.T("Quantos:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        criar.Children.Add(_quantos);
        Botao(criar, Tr.T("Criar inversores"), Tr.T("Cria os inversores do modelo escolhido (Inversor 1, 2..., continuando a numeração)."), CriarInversores);

        var inversores = new DockPanel();
        var topo = new StackPanel();
        topo.Children.Add(Titulo(Tr.T("Inversores da usina")));
        topo.Children.Add(criar);
        DockPanel.SetDock(topo, Dock.Top);
        inversores.Children.Add(topo);
        inversores.Children.Add(_inversores);

        var colunas = new Grid();
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        var esquerda = new ScrollViewer { Content = modelos, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 10, 0) };
        Grid.SetColumn(inversores, 1);
        colunas.Children.Add(esquerda);
        colunas.Children.Add(inversores);
        Children.Add(colunas);

        _modelos.SelectionChanged += (_, _) =>
        {
            try { PreencherModelo(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o modelo de inversor escolhido.", erro); }
        };
    }

    private static TextBlock Titulo(string texto) => new() { Text = texto, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };

    private InverterModel? ModeloEscolhido => (_modelos.SelectedItem as ListBoxItem)?.Tag as InverterModel;

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        _setup = setup;

        var anterior = ModeloEscolhido?.Id;
        _modelos.Items.Clear();
        foreach (var m in setup.Models)
        {
            var item = new ListBoxItem { Content = DescreverModelo(m), Tag = m };
            _modelos.Items.Add(item);
            if (m.Id == anterior) _modelos.SelectedItem = item;
        }

        if (_modelos.SelectedItem is null && _modelos.Items.Count > 0) _modelos.SelectedIndex = 0;
        PreencherModelo();

        var paraCriar = (_modeloParaCriar.SelectedItem as ComboBoxItem)?.Tag as Guid?;
        _modeloParaCriar.Items.Clear();
        foreach (var m in setup.Models)
        {
            var item = new ComboBoxItem { Content = m.Name, Tag = m.Id };
            _modeloParaCriar.Items.Add(item);
            if (m.Id == paraCriar) _modeloParaCriar.SelectedItem = item;
        }

        if (_modeloParaCriar.SelectedItem is null && _modeloParaCriar.Items.Count > 0) _modeloParaCriar.SelectedIndex = 0;

        using (var transacao = Documento.Database.TransactionManager.StartOpenCloseTransaction())
            _contagem = StringAllocation.CountByInverter(ElectricalStore.Strings(transacao, Documento.Database).Select(x => x.String));

        MontarInversores();

        if (problema is not null) Avisar(problema, erro: true);
        else if (_modelos.Items.Count == 0) Avisar(Tr.T("Nenhum modelo de inversor ainda: use Novo modelo."));
    }

    internal static string DescreverModelo(InverterModel m) =>
        Tr.F("{0} — {1} MPPT × {2} entradas = {3} entradas", m.Name, m.Mppts, m.InputsPerMppt, m.TotalInputs);

    private void PreencherModelo()
    {
        var m = ModeloEscolhido;
        var caixas = new[] { _nomeDoModelo, _mppt, _entradas, _largura, _comprimento, _altura };
        foreach (var caixa in caixas) caixa.IsEnabled = m is not null;

        if (m is null)
        {
            foreach (var caixa in caixas) caixa.Text = string.Empty;
            _total.Text = string.Empty;
            return;
        }

        _nomeDoModelo.Text = m.Name;
        _mppt.Text = m.Mppts.ToString(Tr.Culture);
        _entradas.Text = m.InputsPerMppt.ToString(Tr.Culture);
        _largura.Text = Numero(m.Size.Width);
        _comprimento.Text = Numero(m.Size.Length);
        _altura.Text = Numero(m.Size.Height);
        MostrarTotal();
    }

    /// <summary>O total derivado, ao vivo enquanto o usuário digita.</summary>
    private void MostrarTotal()
    {
        try
        {
            _total.Text = NumberInput.TryParseCount(_mppt.Text, out var a) && NumberInput.TryParseCount(_entradas.Text, out var b) && a > 0 && b > 0
                ? (a * b).ToString(Tr.Culture)
                : "—";
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao somar as entradas do modelo de inversor.", erro);
        }
    }

    private Inverter? InversorEscolhido => (_inversores.SelectedItem as ListBoxItem)?.Tag as Inverter;

    /// <summary>A lista dos inversores: nome, modelo e quantas strings tem de quantas entradas.</summary>
    private void MontarInversores()
    {
        var anterior = InversorEscolhido?.Id;
        _inversores.Items.Clear();

        foreach (var inversor in _setup.Inverters)
        {
            var linha = new DockPanel();
            var acoes = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(acoes, Dock.Right);
            linha.Children.Add(acoes);

            var este = inversor;
            Botao(acoes, "+", Tr.T("Alocar strings: a janela some; selecione só strings em campo (Shift+clique tira), Enter volta."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaAlocar, este.Id.ToString("D")), largura: 30);

            linha.Children.Add(new TextBlock { Text = DescreverInversor(_setup, inversor, _contagem.GetValueOrDefault(inversor.Id)), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });

            var item = new ListBoxItem { Content = linha, Tag = inversor };
            _inversores.Items.Add(item);
            if (inversor.Id == anterior) _inversores.SelectedItem = item;
        }
    }

    internal static string DescreverInversor(ElectricalSetup setup, Inverter inversor, int strings)
    {
        var modelo = setup.FindModel(inversor.Model);
        return modelo is null
            ? Tr.F("{0} — sem modelo — {1} string(s)", inversor.Name, strings)
            : Tr.F("{0} — {1} — {2} de {3} string(s)", inversor.Name, modelo.Name, strings, modelo.TotalInputs);
    }

    private void CriarInversores()
    {
        if ((_modeloParaCriar.SelectedItem as ComboBoxItem)?.Tag is not Guid modelo)
        {
            Avisar(Tr.T("Escolha o modelo (cadastre um à esquerda, se não há)."), erro: true);
            return;
        }

        if (!NumberInput.TryParseCount(_quantos.Text, out var quantos) || quantos < 1 || quantos > ElectricalDefaults.MaxAtOnce)
        {
            Avisar(Tr.F("Quantos: um número inteiro de 1 a {0}.", ElectricalDefaults.MaxAtOnce), erro: true);
            return;
        }

        Fazer(() =>
        {
            var novos = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddInverters(modelo, quantos));
            return novos.Count == 1 ? Tr.F("{0} criado.", novos[0].Name) : Tr.F("{0} inversores criados: {1} a {2}.", novos.Count, novos[0].Name, novos[^1].Name);
        });
    }

    private void NovoModelo()
    {
        InverterModel? novo = null;
        Fazer(() =>
        {
            novo = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddModel());
            return Tr.F("{0} criado.", novo.Name);
        });

        foreach (ListBoxItem item in _modelos.Items)
            if (item.Tag is InverterModel m && m.Id == novo?.Id) _modelos.SelectedItem = item;
    }

    private void SalvarModelo()
    {
        if (ModeloEscolhido is not { } m)
        {
            Avisar(Tr.T("Escolha um modelo na lista."), erro: true);
            return;
        }

        if (!NumberInput.TryParseCount(_mppt.Text, out var mppt) || !NumberInput.TryParseCount(_entradas.Text, out var entradas))
        {
            Avisar(Tr.T("MPPT e entradas por MPPT são números inteiros."), erro: true);
            return;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;

        var editado = m with { Name = _nomeDoModelo.Text, Mppts = mppt, InputsPerMppt = entradas, Size = tamanho };
        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditModel(editado));
            return porque is null ? Tr.F("{0} salvo.", editado.Name.Trim()) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
    }

    private void ApagarModelo()
    {
        if (ModeloEscolhido is not { } m)
        {
            Avisar(Tr.T("Escolha um modelo na lista."), erro: true);
            return;
        }

        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveModel(m.Id));
            return porque is null ? Tr.F("{0} apagado do cadastro.", m.Name) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não apaguei: {0}.", porque), erro: true);
    }
}
