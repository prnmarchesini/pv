using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Inversor (etapa 14): à esquerda os modelos de inversor (nome,
/// potência, os MPPTs com as entradas de cada um, o total somado, a
/// dimensão); à direita a tabela dos inversores da usina (cor, nome, modelo,
/// trafo, strings, kWp, kW, CC/CA e as ações de cada um).
/// </summary>
/// <remarks>
/// 05/10/2026 (Renan: "eu queria uma forma mais fácil de dizer 'esse
/// inversor, esse e esse é deste trafo'"): o trafo de cada inversor é uma
/// caixa na própria linha, que grava na hora. É o mesmo vínculo do skid
/// (<see cref="Inverter.Transformer"/>).
/// Mais tarde no mesmo dia (Renan, sobre o bloco "Escolhido"/"Pôr no
/// trafo"/skid embaixo da tabela: "essa parte não estou entendendo nada,
/// melhore MUITO ela"): tudo na própria linha. O nome é editável na célula,
/// o modelo e a cor são caixas que gravam ao escolher, e as ações têm nome
/// (+ Strings, Ver e o "⋯" com Soltar strings, Pôr/Mover em campo e Apagar).
/// Com duas ou mais linhas escolhidas aparece a barra delas (trafo de todas,
/// apagar todas). O agrupar pela seleção em campo virou o botão "Trafo pelo
/// desenho…" (o nome do skid sai da tela; fica o que já tem, ou "Skid T1").
/// </remarks>
internal sealed class AbaInversor : AbaEletrica
{
    private readonly ListBox _modelos = new() { MinHeight = 110 };
    private readonly TextBox _nomeDoModelo, _potencia, _mppt, _largura, _comprimento, _altura;

    /// <summary>As entradas de cada MPPT: uma caixa por MPPT, na ordem (cresce e encolhe com o número de MPPTs).</summary>
    private readonly WrapPanel _entradas = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly List<TextBox> _caixasDasEntradas = [];
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold };

    private readonly ComboBox _modeloParaCriar = new() { Height = 26, MinWidth = 160, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _quantos = new() { Text = "1", Width = 50, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>O trafo em lote: as linhas "de ... a ..." e o trafo (07/10/2026).</summary>
    private readonly TextBox _loteDe = new() { Width = 40, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _loteAte = new() { Width = 40, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _trafoDoLote = new() { Height = 26, MinWidth = 90, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };

    /// <summary>A tabela: uma linha por inversor, várias escolhidas com Ctrl ou Shift.</summary>
    private readonly ListBox _inversores = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        SelectionMode = SelectionMode.Extended,
        Padding = new Thickness(0),
    };

    private readonly ContentControl _rodapeDaTabela = new();

    /// <summary>
    /// A barra das linhas escolhidas: só aparece com duas ou mais (Ctrl ou
    /// Shift + clique), embaixo da tabela (aparecer lá não empurra as linhas
    /// que o usuário está clicando).
    /// </summary>
    private readonly Border _barraDasEscolhidas = new()
    {
        Visibility = Visibility.Collapsed,
        Margin = new Thickness(0, 4, 0, 0),
        Padding = new Thickness(6, 3, 6, 0),
        BorderThickness = new Thickness(1),
        BorderBrush = System.Windows.SystemColors.HighlightBrush,
        Background = System.Windows.SystemColors.InfoBrush,
    };

    private readonly TextBlock _quantasEscolhidas = new() { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 3) };
    private readonly ComboBox _trafoDasEscolhidas = new() { Height = 22, MinWidth = 90, Margin = new Thickness(0, 0, 6, 3) };
    private readonly Button _apagarAsEscolhidas;

    // A atribuição automática: o sentido que avança e o sentido na faixa (a varredura própria dela).
    private readonly ComboBox _sentidoDaAtribuicao = new() { Height = 26, MinWidth = 150, Margin = new Thickness(0, 0, 6, 6) };
    private readonly ComboBox _faixaDaAtribuicao = new() { Height = 26, MinWidth = 150, Margin = new Thickness(0, 0, 6, 6) };
    private bool _mostrandoVarredura;

    private ElectricalSetup _setup = new();
    private IReadOnlyDictionary<Guid, int> _contagem = new Dictionary<Guid, int>();

    internal AbaInversor(Document documento) : base(documento)
    {
        // ------------------------------------------------- modelos (14.1)
        var grade = Grade(130);
        _nomeDoModelo = Campo(grade, Tr.T("Nome do modelo"), Tr.T("Genérico do cliente ou cadastrado (ex. Huawei 250)."));
        _potencia = Campo(grade, Tr.T("Potência (kW)"), Tr.T("Potência nominal CA do inversor, em kW. Vazio = não informada (a coluna CC/CA fica em branco)."));
        _mppt = Campo(grade, Tr.T("Número de MPPTs"), Tr.T("Quantos MPPTs o inversor tem. Ao mudar, a lista de entradas cresce (repetindo o último valor) ou encolhe."));

        // A lista das entradas de cada MPPT (5 MPPTs com 4, 4, 4, 5 e 5 entradas).
        var linhaDasEntradas = grade.RowDefinitions.Count;
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var rotuloDasEntradas = new TextBlock { Text = Tr.T("Entradas de cada MPPT"), Margin = new Thickness(0, 4, 8, 4), ToolTip = Tr.T("Quantas strings entram em cada MPPT; cada um pode ter a sua quantidade.") };
        Grid.SetRow(rotuloDasEntradas, linhaDasEntradas);
        Grid.SetRow(_entradas, linhaDasEntradas);
        Grid.SetColumn(_entradas, 1);
        grade.Children.Add(rotuloDasEntradas);
        grade.Children.Add(_entradas);

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

        _mppt.TextChanged += (_, _) =>
        {
            try { AjustarEntradas(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao ajustar a lista de entradas do modelo de inversor.", erro); }
        };

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
        // A linha de criar: "Criar [1] inversor(es) do modelo [..] [Criar]", e o trafo pelo desenho.
        var criar = new WrapPanel();
        criar.Children.Add(Rotulo(Tr.T("Criar")));
        criar.Children.Add(_quantos);
        criar.Children.Add(Rotulo(Tr.T("inversor(es) do modelo")));
        criar.Children.Add(_modeloParaCriar);
        _quantos.ToolTip = Tr.F("Quantos inversores criar de uma vez (1 a {0}).", ElectricalDefaults.MaxAtOnce);
        _modeloParaCriar.ToolTip = Tr.T("O modelo dos inversores novos (os modelos são cadastrados à esquerda).");
        Botao(criar, Tr.T("Criar"), Tr.T("Cria os inversores do modelo escolhido (Inversor 1, 2..., continuando a numeração). Depois escolha o trafo de cada um na tabela."), CriarInversores);
        Button? peloDesenho = null;
        peloDesenho = Botao(criar, Tr.T("Trafo pelo desenho…"), Tr.T("Escolha um trafo e depois clique nos inversores no desenho: eles passam a ser desse trafo (só os que já estão em campo; inversor de outro trafo não muda)."), () => MenuDoTrafoPeloDesenho(peloDesenho!));
        peloDesenho.Margin = new Thickness(18, 0, 6, 6);

        // O trafo em lote (Renan, 07/10/2026: "um por um ... é bem demorado"):
        // "Trafo em lote: linhas [7] a [12] no [T2] [Pôr]".
        var lote = new WrapPanel();
        lote.Children.Add(Rotulo(Tr.T("Trafo em lote: linhas")));
        lote.Children.Add(_loteDe);
        lote.Children.Add(Rotulo(Tr.T("a")));
        lote.Children.Add(_loteAte);
        lote.Children.Add(Rotulo(Tr.T("no")));
        lote.Children.Add(_trafoDoLote);
        _loteDe.ToolTip = Tr.T("A primeira linha da tabela (a contagem começa em 1, de cima para baixo).");
        _loteAte.ToolTip = Tr.T("A última linha (vazio: só a primeira).");
        _trafoDoLote.ToolTip = Tr.T("O trafo dessas linhas (sem trafo solta).");
        Botao(lote, Tr.T("Pôr"), Tr.T("Põe as linhas de ... a ... no trafo escolhido (o inversor que era de outro trafo muda). Também vale: escolher várias linhas (Ctrl ou Shift + clique) e trocar o trafo de uma delas."), PorOLoteNoTrafo);

        // A distribuição automática das strings livres nos inversores, pela varredura dela.
        var atribuir = new WrapPanel();
        atribuir.Children.Add(Rotulo(Tr.T("Distribuir strings livres:")));
        atribuir.Children.Add(_sentidoDaAtribuicao);
        atribuir.Children.Add(Rotulo(Tr.T("e na faixa")));
        atribuir.Children.Add(_faixaDaAtribuicao);
        Botao(atribuir, Tr.T("Distribuir"), Tr.T("As strings livres, na ordem desta varredura, enchem os inversores na ordem da tabela, cada um até o total de entradas. As já alocadas não mudam (e contam); inversor cheio é pulado; as que sobrarem são avisadas. Para redistribuir do zero, use antes Soltar todas da usina."), AtribuirStrings);
        Botao(atribuir, Tr.T("Soltar todas da usina"), Tr.T("Solta as strings de todos os inversores: ficam livres e continuam no desenho (nada é apagado). Depois, Distribuir redistribui do zero."), SoltarTodasDaUsina);
        _sentidoDaAtribuicao.ToolTip = Tr.T("O sentido em que a distribuição percorre a usina (é só da distribuição; a numeração tem o seu).");
        _faixaDaAtribuicao.ToolTip = Tr.T("Dentro da mesma faixa (linha ou coluna), em que sentido as strings são tomadas.");
        foreach (var sentido in Enum.GetValues<ScanDirection>()) _sentidoDaAtribuicao.Items.Add(new ComboBoxItem { Content = ScanOrder.Describe(sentido), Tag = sentido });
        _sentidoDaAtribuicao.SelectionChanged += (_, _) =>
        {
            try { MudouAVarredura(trocouOSentido: true); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mudar o sentido da atribuição.", erro); }
        };
        _faixaDaAtribuicao.SelectionChanged += (_, _) =>
        {
            try { MudouAVarredura(trocouOSentido: false); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mudar o sentido na faixa da atribuição.", erro); }
        };

        // A ajuda de uma linha, em cima da tabela.
        var textoDaAjuda = Tr.T("Trafo: na coluna Trafo; em lote, pelas linhas de ... a ..., ou escolha várias (Ctrl ou Shift + clique) e troque o trafo de uma. Strings: + Strings ou Distribuir.");
        var ajuda = new TextBlock
        {
            Text = textoDaAjuda,
            ToolTip = textoDaAjuda,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 0, 0, 3),
        };

        // A barra das linhas escolhidas (duas ou mais).
        var barra = new WrapPanel();
        barra.Children.Add(_quantasEscolhidas);
        barra.Children.Add(Rotulo(Tr.T("Trafo"), 3));
        barra.Children.Add(_trafoDasEscolhidas);
        _trafoDasEscolhidas.ToolTip = Tr.T("O trafo para todas as linhas escolhidas (sem trafo solta).");
        foreach (var b in new[]
        {
            Botao(barra, Tr.T("Aplicar"), Tr.T("Põe todas as linhas escolhidas no trafo ao lado (o inversor que era de outro trafo muda)."), PorAsEscolhidasNoTrafo),
            _apagarAsEscolhidas = Botao(barra, Tr.T("Apagar"), Tr.T("Apaga do cadastro todos os inversores escolhidos (pede confirmação): as strings deles ficam livres e os retângulos saem do campo."), ApagarAsEscolhidas),
            Botao(barra, Tr.T("Cancelar seleção"), Tr.T("Desmarca as linhas escolhidas."), () => _inversores.UnselectAll()),
        })
        {
            b.Height = 22;
            b.Margin = new Thickness(0, 0, 6, 3);
        }

        _barraDasEscolhidas.Child = barra;

        // A tabela: ajuda, cabeçalho, linhas e total com as mesmas colunas (SharedSizeGroup).
        ScrollViewer.SetHorizontalScrollBarVisibility(_inversores, ScrollBarVisibility.Disabled);
        var tabela = new Grid();
        Grid.SetIsSharedSizeScope(tabela, true);
        tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tabela.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cabecalho = Cabecalho();
        Grid.SetRow(cabecalho, 1);
        Grid.SetRow(_inversores, 2);
        Grid.SetRow(_rodapeDaTabela, 3);
        tabela.Children.Add(ajuda);
        tabela.Children.Add(cabecalho);
        tabela.Children.Add(_inversores);
        tabela.Children.Add(_rodapeDaTabela);

        var inversores = new DockPanel();
        var topo = new StackPanel();
        topo.Children.Add(Titulo(Tr.T("Inversores da usina")));
        topo.Children.Add(criar);
        topo.Children.Add(lote);
        topo.Children.Add(atribuir);
        DockPanel.SetDock(topo, Dock.Top);
        DockPanel.SetDock(_barraDasEscolhidas, Dock.Bottom);
        inversores.Children.Add(topo);
        inversores.Children.Add(_barraDasEscolhidas);
        inversores.Children.Add(tabela);

        _inversores.SelectionChanged += (_, e) =>
        {
            try { if (e.OriginalSource == _inversores) MostrarAsEscolhidas(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar as linhas escolhidas.", erro); }
        };

        // O editor do modelo é estreito; a tabela fica com o resto da largura.
        var colunas = new Grid();
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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

    /// <summary>O rótulo ao lado de uma caixa, na mesma linha.</summary>
    private static TextBlock Rotulo(string texto, double baixo = 6) => new() { Text = texto, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, baixo) };

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

        _contagem = ContarStrings(Documento.Database);

        MontarInversores();
        MostrarVarredura(AtribuicaoAutomatica.Varredura(Documento.Database).Varredura);

        MontarTrafos(_trafoDasEscolhidas, setup, comSemTrafo: true);
        MontarTrafos(_trafoDoLote, setup, comSemTrafo: true);

        // 14.4: o excesso aparece em vermelho na linha do inversor e no rodapé.
        var excessos = _setup.Inverters
            .Select(i => StringAllocation.ExcessWarning(i, _setup.FindModel(i.Model), _contagem.GetValueOrDefault(i.Id)))
            .OfType<string>().ToList();
        if (problema is null && excessos.Count > 0) Avisar(string.Join("\n", excessos), erro: true);

        if (problema is not null) Avisar(problema, erro: true);
        else if (_modelos.Items.Count == 0) Avisar(Tr.T("Nenhum modelo de inversor ainda: use Novo modelo."));
    }

    /// <summary>Os trafos do cadastro na caixa (com "sem trafo" na frente, se pedido), mantendo a escolha.</summary>
    private static void MontarTrafos(ComboBox caixa, ElectricalSetup setup, bool comSemTrafo)
    {
        var antes = (caixa.SelectedItem as ComboBoxItem)?.Tag as Guid?;
        caixa.Items.Clear();
        if (comSemTrafo) caixa.Items.Add(new ComboBoxItem { Content = Tr.T("sem trafo"), Tag = Guid.Empty });
        foreach (var t in setup.Transformers)
            caixa.Items.Add(new ComboBoxItem { Content = t.Nickname, Tag = t.Id, ToolTip = t.Name });

        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (Guid)i.Tag == antes)
            ?? caixa.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (Guid)i.Tag != Guid.Empty)
            ?? (caixa.Items.Count > 0 ? caixa.Items[0] : null);
    }

    /// <summary>As strings de cada inversor, pelo vínculo (a mesma contagem do aviso de excesso).</summary>
    internal static IReadOnlyDictionary<Guid, int> ContarStrings(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        return StringAllocation.CountByInverter(ElectricalStore.Strings(transacao, database).Select(x => x.String));
    }

    /// <summary>
    /// "Huawei 250 — 5 MPPT × 4 entradas = 20 entradas — 250 kW", ou com a
    /// lista quando os MPPTs diferem; sem potência informada, sem o kW.
    /// </summary>
    internal static string DescreverModelo(InverterModel m)
    {
        var entradas = m.IsUniform && m.Mppts > 0
            ? Tr.F("{0} — {1} MPPT × {2} entradas = {3} entradas", m.Name, m.Mppts, m.InputsByMppt[0], m.TotalInputs)
            : Tr.F("{0} — {1} MPPT ({2}) = {3} entradas", m.Name, m.Mppts, string.Join(", ", m.InputsByMppt), m.TotalInputs);
        return m.HasPower ? Tr.F("{0} — {1} kW", entradas, Kw(m.PowerKw)) : entradas;
    }

    /// <summary>Potência para a tela: "250", "1.500", "62,5" (na cultura da tela).</summary>
    private static string Kw(double v) => v.ToString("#,0.##", Tr.Culture);

    private void PreencherModelo()
    {
        var m = ModeloEscolhido;
        var caixas = new[] { _nomeDoModelo, _potencia, _mppt, _largura, _comprimento, _altura };
        foreach (var caixa in caixas) caixa.IsEnabled = m is not null;

        if (m is null)
        {
            foreach (var caixa in caixas) caixa.Text = string.Empty;
            MontarEntradas([]);
            return;
        }

        _nomeDoModelo.Text = m.Name;
        _potencia.Text = m.HasPower ? m.PowerKw.ToString("0.######", Tr.Culture) : string.Empty;
        _largura.Text = Numero(m.Size.Width);
        _comprimento.Text = Numero(m.Size.Length);
        _altura.Text = Numero(m.Size.Height);

        // Primeiro a lista, depois o número (o TextChanged do número acha a lista já do tamanho certo).
        _entradasGuardadas = [];
        MontarEntradas(m.InputsByMppt.Select(n => n.ToString(Tr.Culture)).ToList());
        _mppt.Text = m.Mppts.ToString(Tr.Culture);
    }

    /// <summary>Uma caixa por MPPT, com o texto dado; a lista vazia apaga as caixas.</summary>
    private void MontarEntradas(IReadOnlyList<string> textos)
    {
        _entradas.Children.Clear();
        _caixasDasEntradas.Clear();
        foreach (var texto in textos) NovaEntrada(texto);
        MostrarTotal();
    }

    private void NovaEntrada(string texto)
    {
        var n = _caixasDasEntradas.Count + 1;
        var caixa = new TextBox { Text = texto, Width = 38, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = Tr.F("Entradas do MPPT {0}", n) };
        caixa.TextChanged += (_, _) => MostrarTotal();

        var par = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 4) };
        par.Children.Add(new TextBlock { Text = n.ToString(Tr.Culture) + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0), Foreground = System.Windows.SystemColors.GrayTextBrush });
        par.Children.Add(caixa);

        _caixasDasEntradas.Add(caixa);
        _entradas.Children.Add(par);
    }

    /// <summary>
    /// O número de MPPTs mudou: a lista cresce repetindo o último valor, ou
    /// encolhe tirando do fim (o que foi digitado nas caixas que ficam não muda).
    /// Número que não se lê (ou fora do limite) deixa a lista como está.
    /// </summary>
    private void AjustarEntradas()
    {
        if (!NumberInput.TryParseCount(_mppt.Text, out var quantos) || quantos < 1 || quantos > ElectricalDefaults.MaxMppts)
        {
            MostrarTotal();
            return;
        }

        if (quantos == _caixasDasEntradas.Count)
        {
            MostrarTotal();
            return;
        }

        // O que sai do fim fica guardado: digitar "10" passa por "1" e não pode
        // perder as entradas dos MPPTs 2 a 5 (revisão de 05/10/2026).
        var atuais = _caixasDasEntradas.Select(c => c.Text).ToList();
        _entradasGuardadas = [.. atuais, .. _entradasGuardadas.Skip(atuais.Count)];
        MontarEntradas(InverterModel.Resize(_entradasGuardadas, quantos, "1"));
    }

    /// <summary>As entradas de todos os MPPTs já mostrados deste modelo (as que saíram do fim continuam aqui).</summary>
    private List<string> _entradasGuardadas = [];

    /// <summary>As entradas lidas das caixas, ou null se alguma não é número inteiro.</summary>
    private List<int>? LerEntradas()
    {
        var lidas = new List<int>(_caixasDasEntradas.Count);
        foreach (var caixa in _caixasDasEntradas)
        {
            if (!NumberInput.TryParseCount(caixa.Text, out var n)) return null;
            lidas.Add(n);
        }

        return lidas;
    }

    /// <summary>O total somado, ao vivo enquanto o usuário digita.</summary>
    private void MostrarTotal()
    {
        try
        {
            _total.Text = LerEntradas() is { Count: > 0 } lidas && lidas.All(n => n > 0) ? lidas.Sum().ToString(Tr.Culture) : "—";
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao somar as entradas do modelo de inversor.", erro);
        }
    }

    // ------------------------------------------------------------ a tabela

    /// <summary>As colunas da tabela, na ordem (o cabeçalho, as linhas e o total usam as mesmas).</summary>
    private static readonly string[] Colunas = ["Cor", "Nome", "Modelo", "Trafo", "Strings", "Kwp", "Kw", "Razao", "Acoes"];

    private const int ColunaCor = 0, ColunaNome = 1, ColunaModelo = 2, ColunaTrafo = 3, ColunaStrings = 4, ColunaKwp = 5, ColunaKw = 6, ColunaRazao = 7, ColunaAcoes = 8;

    /// <summary>Uma linha da tabela: as colunas com a largura repartida (a maior de cada uma) e uma sobra no fim.</summary>
    private static Grid LinhaDaTabela()
    {
        var g = new Grid();
        foreach (var grupo in Colunas) g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Inversor" + grupo });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }

    private static T Por<T>(Grid linha, T elemento, int coluna) where T : UIElement
    {
        Grid.SetColumn(elemento, coluna);
        linha.Children.Add(elemento);
        return elemento;
    }

    private static TextBlock Celula(string texto, bool numero = false, double larguraMaxima = double.PositiveInfinity) => new()
    {
        Text = texto,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = numero ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        TextAlignment = numero ? TextAlignment.Right : TextAlignment.Left,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxWidth = larguraMaxima,
        Margin = new Thickness(0, 0, 10, 0),
    };

    /// <summary>O cabeçalho, alinhado às linhas (a borda e o recuo da lista descontados).</summary>
    private static Grid Cabecalho()
    {
        var g = LinhaDaTabela();
        g.Margin = new Thickness(4, 0, 2, 2);

        void Titulo(int coluna, string texto, string dica, bool numero = false)
        {
            var t = Por(g, Celula(texto, numero), coluna);
            t.FontWeight = FontWeights.SemiBold;
            t.ToolTip = dica;
        }

        Titulo(ColunaCor, Tr.T("Cor"), Tr.T("A cor das strings do inversor no desenho: clique no quadradinho para trocar."));
        Titulo(ColunaNome, Tr.T("Inversor"), Tr.T("O nome do inversor (a tag): clique no nome para renomear."));
        Titulo(ColunaModelo, Tr.T("Modelo"), Tr.T("O modelo do inversor: escolher outro grava na hora (o cadastro dos modelos fica à esquerda)."));
        Titulo(ColunaTrafo, Tr.T("Trafo"), Tr.T("O trafo do inversor. Escolher na linha grava na hora; é também o skid do trafo."));
        Titulo(ColunaStrings, Tr.T("Strings"), Tr.T("Strings alocadas / total de entradas do modelo."), numero: true);
        Titulo(ColunaKwp, "kWp", Tr.T("Potência CC: a soma da potência dos módulos das strings alocadas (a mesma conta do Resumo elétrico)."), numero: true);
        Titulo(ColunaKw, "kW", Tr.T("Potência nominal CA do modelo."), numero: true);
        Titulo(ColunaRazao, Tr.T("CC/CA"), Tr.T("kWp ÷ kW: só com a potência do modelo informada."), numero: true);
        return g;
    }

    /// <summary>"5/20": as strings de quantas entradas (sem modelo, só as strings).</summary>
    private static string Strings(int strings, int entradas, bool comModelo) =>
        comModelo ? Tr.F("{0}/{1}", strings, entradas) : strings.ToString(Tr.Culture);

    private static string Kwp(double? kwp) => kwp is { } v ? v.ToString("#,0.00", Tr.Culture) : "—";

    private static string Razao(double? razao) => razao is { } r ? r.ToString("0.00", Tr.Culture) : "—";

    /// <summary>Os inversores das linhas escolhidas, na ordem da tabela.</summary>
    private List<Inverter> Escolhidos() =>
        _inversores.Items.OfType<ListBoxItem>().Where(i => i.IsSelected).Select(i => (Inverter)i.Tag).ToList();

    /// <summary>
    /// As linhas da tabela como a aba mostra: as strings pela contagem do
    /// vínculo e o kWp pelo resumo elétrico (<see cref="ResumoEletricoCommands.Ler"/>,
    /// a potência dos módulos pela mesa dona). Se o resumo não pôde ser lido,
    /// o kWp fica "—" e o resto continua.
    /// </summary>
    internal static IReadOnlyList<InverterTableRow> LinhasDaTabela(Document documento, ElectricalSetup setup, IReadOnlyDictionary<Guid, int> contagem)
    {
        IReadOnlyList<InverterSummary>? resumo = null;
        try
        {
            resumo = ResumoEletricoCommands.Ler(documento).Resumo.AllInverters.ToList();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("A tabela de inversores ficou sem o kWp (o resumo elétrico não pôde ser lido).", erro);
        }

        return InverterTable.Rows(setup.Inverters, setup.Models, contagem, resumo);
    }

    /// <summary>A tabela dos inversores: uma linha por inversor e o total.</summary>
    private void MontarInversores()
    {
        var escolhidos = Escolhidos().Select(i => i.Id).ToHashSet();
        var foco = OndeEstaOFoco();
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);
        var linhas = LinhasDaTabela(Documento, _setup, _contagem);
        _inversores.Items.Clear();

        foreach (var linha in linhas)
        {
            var item = new ListBoxItem { Content = MontarLinha(linha, emCampo.Contains((EquipmentKind.Inverter, linha.Inverter.Id))), Tag = linha.Inverter, Padding = new Thickness(2, 1, 2, 1) };
            _inversores.Items.Add(item);
        }

        // A escolha volta (o que sumiu do cadastro sai dela).
        foreach (var item in _inversores.Items.OfType<ListBoxItem>())
            if (escolhidos.Contains(((Inverter)item.Tag).Id)) _inversores.SelectedItems.Add(item);
        MostrarAsEscolhidas();

        var total = InverterTable.Total(linhas);
        var rodape = LinhaDaTabela();
        rodape.Margin = new Thickness(4, 3, 2, 0);
        foreach (var t in new[]
        {
            Por(rodape, Celula(Tr.F("Total ({0})", total.Inverters)), ColunaNome),
            Por(rodape, Celula(Strings(total.Strings, total.Capacity, comModelo: true), numero: true), ColunaStrings),
            Por(rodape, Celula(Kwp(total.PowerKwp), numero: true), ColunaKwp),
            Por(rodape, Celula(total.PowerKw > 0 ? Kw(total.PowerKw) : "—", numero: true), ColunaKw),
            Por(rodape, Celula(Razao(total.DcAcRatio), numero: true), ColunaRazao),
        })
            t.FontWeight = FontWeights.SemiBold;
        _rodapeDaTabela.Content = rodape;
        if (foco is { } f) DevolverOFoco(f.Inversor, f.Coluna);
    }

    /// <summary>
    /// A caixa da tabela que tem o foco (o inversor da linha e a coluna), ou
    /// null. Gravar o nome ao sair dele refaz a tabela; sem isto, a caixa que
    /// o usuário acabou de clicar (o nome da linha de baixo, a lista do
    /// trafo) sumiria debaixo dele (revisão de 05/10/2026).
    /// </summary>
    private (Guid Inversor, int Coluna)? OndeEstaOFoco()
    {
        if (!_inversores.IsKeyboardFocusWithin || System.Windows.Input.Keyboard.FocusedElement is not DependencyObject focado) return null;
        if (ItemsControl.ContainerFromElement(_inversores, focado) is not ListBoxItem { Tag: Inverter inversor, Content: Grid linha }) return null;

        foreach (UIElement celula in linha.Children)
            if (ReferenceEquals(celula, focado) || (focado is System.Windows.Media.Visual v && celula.IsAncestorOf(v)))
                return (inversor.Id, Grid.GetColumn(celula));
        return null;
    }

    /// <summary>Põe o foco na caixa da mesma coluna da linha do inversor, depois que a tabela nova assenta.</summary>
    private void DevolverOFoco(Guid inversor, int coluna) =>
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            try
            {
                var item = _inversores.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((Inverter)i.Tag).Id == inversor);
                if (item?.Content is not Grid linha) return;
                var celula = linha.Children.OfType<UIElement>().FirstOrDefault(c => Grid.GetColumn(c) == coluna);
                if (celula is TextBox texto)
                {
                    texto.Focus();
                    texto.SelectAll();
                }
                else celula?.Focus();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao devolver o foco à tabela de inversores.", erro);
            }
        });

    /// <summary>
    /// Se o desenho ainda está aberto: a gravação adiada (sair da caixa ao
    /// fechar a janela ou o desenho) não mexe em documento que está saindo.
    /// </summary>
    private bool DesenhoAberto()
    {
        try
        {
            return !Documento.IsDisposed && Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.Cast<Document>().Contains(Documento);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir se o desenho da aba Inversor está aberto.", erro);
            return false;
        }
    }

    /// <summary>A barra das escolhidas: aparece com duas ou mais linhas, com quantas são.</summary>
    private void MostrarAsEscolhidas()
    {
        var n = _inversores.SelectedItems.Count;
        _barraDasEscolhidas.Visibility = n >= 2 ? Visibility.Visible : Visibility.Collapsed;
        _quantasEscolhidas.Text = Tr.F("{0} inversores escolhidos:", n);
        _apagarAsEscolhidas.Content = Tr.F("Apagar os {0}", n);
    }

    /// <summary>
    /// Uma linha: a cor (clique abre a paleta), o nome (editável na própria
    /// célula), o modelo e o trafo (caixas que gravam ao escolher), strings,
    /// kWp, kW, CC/CA e as ações (+ Strings, Ver e o "⋯" com o resto).
    /// </summary>
    private Grid MontarLinha(InverterTableRow linha, bool emCampo)
    {
        var inversor = linha.Inverter;
        var g = LinhaDaTabela();

        Por(g, CaixaDaCor(inversor), ColunaCor);

        var aviso = StringAllocation.ExcessWarning(inversor, linha.Model, linha.Strings);
        var nome = Por(g, CaixaDoNome(inversor, aviso, emCampo), ColunaNome);
        Por(g, CaixaDoModelo(inversor), ColunaModelo);
        Por(g, CaixaDoTrafo(inversor), ColunaTrafo);

        var strings = Por(g, Celula(Strings(linha.Strings, linha.Capacity, linha.Model is not null), numero: true), ColunaStrings);
        strings.ToolTip = aviso;
        if (aviso is not null)
        {
            nome.Foreground = strings.Foreground = System.Windows.Media.Brushes.Firebrick;
            nome.FontWeight = strings.FontWeight = FontWeights.SemiBold;
        }

        Por(g, Celula(Kwp(linha.PowerKwp), numero: true), ColunaKwp);
        Por(g, Celula(linha.PowerKw > 0 ? Kw(linha.PowerKw) : "—", numero: true), ColunaKw);
        Por(g, Celula(Razao(linha.DcAcRatio), numero: true), ColunaRazao);

        var acoes = Por(g, new StackPanel { Orientation = Orientation.Horizontal }, ColunaAcoes);
        var id = inversor.Id;
        Button? mais = null;
        var botoes = new[]
        {
            Botao(acoes, Tr.T("+ Strings"), Tr.T("Pôr strings neste inversor: a janela some; clique nas strings no desenho (Shift+clique tira) e Enter volta."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaAlocar, id.ToString("D"))),
            Botao(acoes, Tr.T("Ver"), Tr.T("Mostra no desenho as strings deste inversor (ficam selecionadas)."),
                () => JanelaEletrica.SelecionarStrings(Documento, id)),
            mais = Botao(acoes, "⋯", Tr.T("Mais: soltar as strings, pôr em campo, apagar o inversor."),
                () => MenuDaLinha(mais!, inversor, emCampo)),
        };
        foreach (var b in botoes)
        {
            b.Height = 22;
            b.Padding = new Thickness(6, 0, 6, 0);
            b.Margin = new Thickness(0, 1, 4, 1);
        }

        return g;
    }

    /// <summary>
    /// O "⋯" da linha: soltar as strings, pôr (ou mover) o retângulo em campo
    /// e apagar o inversor (com confirmação).
    /// </summary>
    private void MenuDaLinha(Button botao, Inverter inversor, bool emCampo)
    {
        var menu = new ContextMenu { PlacementTarget = botao, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        Item(menu, Tr.T("Soltar strings"), Tr.T("As strings deste inversor ficam livres; continuam no desenho (nada é apagado)."), () => SoltarTodas(inversor));
        Item(menu, emCampo ? Tr.T("Mover em campo") : Tr.T("Pôr em campo"),
            emCampo
                ? Tr.T("O retângulo do inversor já está no desenho: a janela some e você clica o novo centro dele. O vínculo não muda.")
                : Tr.T("Põe o retângulo do inversor no desenho: a janela some e você clica o centro dele."),
            () => AlocarEmCampo(inversor.Id));
        menu.Items.Add(new Separator());
        Item(menu, Tr.T("Apagar inversor…"), Tr.T("Tira o inversor do cadastro (pede confirmação): as strings dele ficam livres e o retângulo sai do campo."), () => Apagar([inversor]));
        menu.IsOpen = true;
    }

    /// <summary>Um item de menu com o clique em try/catch (exceção num evento WPF derrubaria o Civil 3D).</summary>
    private void Item(ContextMenu menu, string texto, string dica, Action acao)
    {
        var item = new MenuItem { Header = texto, ToolTip = dica };
        item.Click += (_, _) =>
        {
            try
            {
                acao();
            }
            catch (Exception falha)
            {
                RegistroDeDiagnostico.Registrar($"Falha no menu {texto} da aba Inversor.", falha);
                Avisar(Tr.F("Não consegui: {0}", falha.Message), erro: true);
            }
        };
        menu.Items.Add(item);
    }

    /// <summary>
    /// A cor na linha: o quadradinho é a caixa da paleta dos inversores (e
    /// "Mais cores..."), estreita, só com a cor à vista. Escolher grava na
    /// hora e repinta as strings dele.
    /// </summary>
    private ComboBox CaixaDaCor(Inverter inversor)
    {
        var caixa = PaletaDeCores.Caixa(inversor.Color ?? InverterColors.Palette[0].Color, Tr.T("A cor do inversor: as strings dele ficam desta cor no desenho. Clique para trocar."), InverterColors.Palette);
        caixa.MinWidth = 0;
        caixa.Width = 40;
        caixa.Height = 22;
        caixa.Margin = new Thickness(0, 0, 8, 0);
        caixa.VerticalAlignment = VerticalAlignment.Center;

        var id = inversor.Id;
        caixa.SelectionChanged += (_, e) =>
        {
            try
            {
                if (e.AddedItems.Count == 0 || (e.AddedItems[0] as ComboBoxItem)?.Tag is not RgbColor cor) return;

                // Depois que a escolha assenta (o "Mais cores..." troca o item dentro do próprio evento).
                Dispatcher.BeginInvoke(() =>
                {
                    try { TrocarCor(id, cor); }
                    catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao trocar a cor do inversor.", erro); }
                });
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao escolher a cor do inversor.", erro);
            }
        };

        return caixa;
    }

    /// <summary>
    /// O nome na linha: uma caixa sem borda que parece texto. Clicar edita;
    /// Enter ou sair da caixa grava (uma vez, nunca a cada tecla); Esc desfaz.
    /// Nome recusado (vazio, repetido...) volta ao gravado, com o porquê.
    /// </summary>
    private TextBox CaixaDoNome(Inverter inversor, string? aviso, bool emCampo)
    {
        var dica = Tr.T("Clique para renomear: Enter (ou sair da caixa) grava, Esc desfaz.");
        if (emCampo) dica = Tr.T("Em campo: o retângulo dele está no desenho.") + "\n" + dica;
        var caixa = new TextBox
        {
            Text = inversor.Name,
            Width = 112,
            Height = 22,
            MaxLength = ElectricalDefaults.MaxNameLength,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = aviso is null ? dica : aviso + "\n" + dica,
        };

        var id = inversor.Id;
        var gravado = inversor.Name;
        var enviado = false;

        void Confirmar()
        {
            if (enviado || caixa.Text.Trim() == gravado) return;
            enviado = true;
            var texto = caixa.Text;

            // Fora do evento: gravar relê e refaz a tabela (e esta caixa).
            Dispatcher.BeginInvoke(() =>
            {
                try { if (DesenhoAberto()) RenomearPelaTela(id, texto); }
                catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao renomear o inversor.", erro); }
            });
        }

        caixa.GotKeyboardFocus += (_, _) =>
        {
            caixa.BorderThickness = new Thickness(1);
            caixa.Background = System.Windows.SystemColors.WindowBrush;
        };
        caixa.LostKeyboardFocus += (_, _) =>
        {
            try
            {
                caixa.BorderThickness = new Thickness(0);
                caixa.Background = System.Windows.Media.Brushes.Transparent;
                Confirmar();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao sair do nome do inversor.", erro);
            }
        };
        caixa.PreviewKeyDown += (_, e) =>
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    e.Handled = true;
                    Confirmar();
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    e.Handled = true;
                    caixa.Text = gravado;
                    caixa.SelectAll();
                }
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha numa tecla do nome do inversor.", erro);
            }
        };

        return caixa;
    }

    /// <summary>
    /// O modelo na linha: os modelos do cadastro (o vínculo para modelo que
    /// sumiu aparece como "sem modelo"). Escolher grava ao fechar a lista, com
    /// a conferência das strings já alocadas (<see cref="ElectricalSetup.ChangeInverterModel"/>).
    /// </summary>
    private ComboBox CaixaDoModelo(Inverter inversor)
    {
        var caixa = new ComboBox { Width = 124, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var m in _setup.Models)
            caixa.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id, ToolTip = DescreverModelo(m) });

        if (_setup.FindModel(inversor.Model) is not { } atual)
        {
            caixa.Items.Add(new ComboBoxItem { Content = Tr.T("sem modelo"), Tag = inversor.Model, ToolTip = Tr.T("o modelo dele não está no cadastro") });
            caixa.ToolTip = Tr.T("O modelo deste inversor não está no cadastro: escolha um. Escolher grava na hora.");
        }
        else
        {
            caixa.ToolTip = Tr.F("{0}. Escolher outro grava na hora.", DescreverModelo(atual));
        }

        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().First(i => (Guid)i.Tag == inversor.Model);
        AoEscolher(caixa, inversor.Model, modelo => TrocarModeloPelaTela(inversor.Id, modelo), "Falha ao trocar o modelo do inversor.");
        return caixa;
    }

    /// <summary>
    /// A caixa do trafo na linha: "sem trafo" e os trafos do cadastro (um
    /// vínculo para trafo que sumiu aparece como "?"). Escolher grava na hora.
    /// </summary>
    private ComboBox CaixaDoTrafo(Inverter inversor)
    {
        var caixa = new ComboBox { Width = 80, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center };
        caixa.Items.Add(new ComboBoxItem { Content = Tr.T("sem trafo"), Tag = Guid.Empty });
        foreach (var t in _setup.Transformers)
            caixa.Items.Add(new ComboBoxItem { Content = t.Nickname, Tag = t.Id, ToolTip = t.Name });

        if (inversor.Transformer != Guid.Empty && _setup.FindTransformer(inversor.Transformer) is null)
            caixa.Items.Add(new ComboBoxItem { Content = "?", Tag = inversor.Transformer, ToolTip = Tr.T("o trafo dele não está no cadastro") });

        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().First(i => (Guid)i.Tag == inversor.Transformer);
        caixa.ToolTip = _setup.FindSkid(inversor.Transformer) is { } skid
            ? Tr.F("O trafo deste inversor (skid {0}). Escolher grava na hora; com várias linhas escolhidas, vale para todas.", skid.Name)
            : Tr.T("O trafo deste inversor. Escolher grava na hora; com várias linhas escolhidas, vale para todas.");

        // Linha que faz parte de uma escolha de várias: o trafo vai para todas (07/10/2026).
        AoEscolher(caixa, inversor.Transformer, trafo =>
        {
            var escolhidos = Escolhidos().Select(i => i.Id).ToList();
            PorNoTrafoPelaTela(escolhidos.Count >= 2 && escolhidos.Contains(inversor.Id) ? escolhidos : [inversor.Id], trafo);
        }, "Falha ao trocar o trafo do inversor.");
        return caixa;
    }

    /// <summary>
    /// Grava quando a lista fecha (ou a caixa perde o foco) com outro valor:
    /// a seta do teclado na caixa fechada não grava a cada passo (revisão de
    /// 05/10/2026: quem desce com a seta trocaria sem querer). A gravação vai
    /// depois que a escolha assenta: ela relê e refaz a tabela (e a caixa).
    /// </summary>
    private void AoEscolher(ComboBox caixa, Guid atual, Action<Guid> gravar, string falha)
    {
        var gravando = false;
        void Confirmar()
        {
            try
            {
                if (gravando || (caixa.SelectedItem as ComboBoxItem)?.Tag is not Guid escolhido || escolhido == atual) return;
                gravando = true;

                Dispatcher.BeginInvoke(() =>
                {
                    try { if (DesenhoAberto()) gravar(escolhido); }
                    catch (Exception erro) { RegistroDeDiagnostico.Registrar(falha, erro); }
                });
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar(falha, erro);
            }
        }

        caixa.DropDownClosed += (_, _) => Confirmar();
        caixa.LostKeyboardFocus += (_, _) =>
        {
            // O foco indo para a própria lista (Alt+seta abre) não é sair da caixa.
            if (!caixa.IsDropDownOpen && !caixa.IsKeyboardFocusWithin) Confirmar();
        };
        caixa.SelectionChanged += (_, e) => e.Handled = true;   // não sobe para a tabela (a escolha das linhas)
    }

    /// <summary>O "Aplicar" da barra: as linhas escolhidas vão todas para o trafo da caixa ao lado.</summary>
    private void PorAsEscolhidasNoTrafo()
    {
        var ids = Escolhidos().Select(i => i.Id).ToList();
        if (ids.Count == 0)
        {
            Avisar(Tr.T("Escolha uma ou mais linhas na tabela (Ctrl ou Shift + clique para várias)."), erro: true);
            return;
        }

        if ((_trafoDasEscolhidas.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo)
        {
            Avisar(Tr.T("Escolha o trafo (cadastre na aba Transformador, se não há)."), erro: true);
            return;
        }

        PorNoTrafoPelaTela(ids, trafo);
    }

    /// <summary>O "Pôr" do trafo em lote: as linhas de ... a ... vão para o trafo escolhido.</summary>
    private void PorOLoteNoTrafo()
    {
        var linhas = _inversores.Items.OfType<ListBoxItem>().Select(i => (Inverter)i.Tag).ToList();
        var (indices, problema) = InverterTable.Range(linhas.Count, _loteDe.Text, _loteAte.Text);
        if (problema is not null)
        {
            Avisar(Tr.F("Não mudei: {0}.", problema), erro: true);
            return;
        }

        if ((_trafoDoLote.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo)
        {
            Avisar(Tr.T("Escolha o trafo (cadastre na aba Transformador, se não há)."), erro: true);
            return;
        }

        PorNoTrafoPelaTela(indices.Select(i => linhas[i].Id).ToList(), trafo);
    }

    private void PorNoTrafoPelaTela(IReadOnlyCollection<Guid> inversores, Guid trafo) =>
        GravarNaLinha(() => PorNoTrafo(Documento.Database, inversores, trafo), p => Tr.F("Não mudei: {0}.", p));

    private void RenomearPelaTela(Guid id, string nome) =>
        GravarNaLinha(() =>
        {
            var porque = RenomearNaLinha(Documento.Database, id, nome);
            return (porque is null ? Tr.F("Inversor renomeado para {0}.", nome.Trim()) : null, porque);
        }, p => Tr.F("Não renomeei: {0}.", p));

    private void TrocarModeloPelaTela(Guid id, Guid modelo) =>
        GravarNaLinha(() =>
        {
            var porque = TrocarModeloNaLinha(Documento.Database, id, modelo);
            return (porque is null ? Tr.F("{0}: modelo trocado para {1}.", _setup.FindInverter(id)?.Name ?? "?", _setup.FindModel(modelo)?.Name ?? "?") : null, porque);
        }, p => Tr.F("Não troquei o modelo: {0}.", p));

    /// <summary>
    /// Uma gravação feita direto na linha (nome, modelo, trafo): pelo Fazer
    /// (trava, relê, recado). Recusada ou com falha, a tabela volta ao que
    /// está gravado (a caixa não fica mostrando o que não foi) e o porquê
    /// vai para o recado pela frase dada.
    /// </summary>
    private void GravarNaLinha(Func<(string? Frase, string? Problema)> operacao, Func<string, string> recusa)
    {
        string? porque = null;
        var gravou = false;
        Fazer(() =>
        {
            var (frase, problema) = operacao();
            porque = problema;
            gravou = true;
            return frase;
        });

        if (!gravou)
        {
            try { Atualizar(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao reler a tabela de inversores.", erro); }
        }

        if (porque is not null) Avisar(recusa(porque), erro: true);
    }

    /// <summary>
    /// Grava o trafo dos inversores (<see cref="ElectricalSetup.SetTransformer"/>)
    /// e diz o que mudou; o problema, se nada mudou. Quem chama trava o
    /// documento (<see cref="EscritaForaDeComando"/>): a tabela pelo Fazer, o
    /// nível 2 pelo gancho do mesmo caminho.
    /// </summary>
    internal static (string? Frase, string? Problema) PorNoTrafo(Database database, IReadOnlyCollection<Guid> inversores, Guid trafo)
    {
        TransformerAssignment? r = null;
        string? apelido = null;
        ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            r = s.SetTransformer(inversores, trafo);
            apelido = s.FindTransformer(trafo)?.Nickname;
            return true;
        });

        if (r!.Problem is { } problema) return (null, problema);

        var frase = trafo == Guid.Empty
            ? Tr.F("{0} inversor(es) ficaram sem trafo.", r.Changed)
            : Tr.F("{0} inversor(es) postos no {1}; {2} já eram dele.", r.Changed, apelido ?? "?", r.AlreadyThere);
        if (r.Missing > 0) frase += " " + Tr.F("{0} não estão mais no cadastro.", r.Missing);
        return (frase, null);
    }

    /// <summary>
    /// O nome editado na linha (<see cref="ElectricalSetup.RenameInverter"/>)
    /// e o retângulo em campo redesenhado com ele. Null se gravou, o porquê
    /// se não. Quem chama trava o documento (o Fazer da aba, ou o gancho do nível 2).
    /// </summary>
    internal static string? RenomearNaLinha(Database database, Guid inversor, string? nome)
    {
        var porque = ConfiguracaoEletricaStore.Mudar(database, s => s.RenameInverter(inversor, nome));
        if (porque is null) EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Inverter, inversor);
        return porque;
    }

    /// <summary>
    /// O modelo escolhido na linha (<see cref="ElectricalSetup.ChangeInverterModel"/>,
    /// com as strings alocadas contadas pelo vínculo no desenho) e o retângulo
    /// em campo redesenhado com a dimensão dele. Null se gravou, o porquê se não.
    /// </summary>
    internal static string? TrocarModeloNaLinha(Database database, Guid inversor, Guid modelo)
    {
        var strings = ContarStrings(database).GetValueOrDefault(inversor);
        var porque = ConfiguracaoEletricaStore.Mudar(database, s => s.ChangeInverterModel(inversor, modelo, strings));
        if (porque is null) EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Inverter, inversor);
        return porque;
    }

    /// <summary>
    /// Apaga os inversores (um, pelo "⋯" da linha, ou as linhas escolhidas):
    /// primeiro o cadastro (se o registro não pode ser gravado, nada muda),
    /// depois o vínculo das strings e os retângulos em campo (se falhar, a
    /// string fica apontando para um inversor que não existe, e essa não
    /// trava: pode ser alocada de novo). Os apagados e quantas strings ficaram livres.
    /// </summary>
    internal static (IReadOnlyList<Inverter> Apagados, int Soltas) ApagarInversores(Database database, IReadOnlyCollection<Guid> inversores)
    {
        var apagados = ConfiguracaoEletricaStore.Mudar(database, s => s.RemoveInverters(inversores));
        var soltas = 0;
        foreach (var i in apagados)
        {
            soltas += StringsDoDesenho.Soltar(database, i.Id);
            EquipamentoEmCampo.Apagar(database, EquipmentKind.Inverter, i.Id);
        }

        return (apagados, soltas);
    }

    private void ApagarAsEscolhidas() => Apagar(Escolhidos());

    /// <summary>Apaga os inversores depois da confirmação (o nome de cada um na pergunta).</summary>
    private void Apagar(IReadOnlyList<Inverter> inversores)
    {
        if (inversores.Count == 0)
        {
            Avisar(Tr.T("Escolha uma ou mais linhas na tabela (Ctrl ou Shift + clique para várias)."), erro: true);
            return;
        }

        var nomes = string.Join(", ", inversores.Take(8).Select(i => i.Name)) + (inversores.Count > 8 ? ", …" : string.Empty);
        var pergunta = inversores.Count == 1
            ? Tr.F("Apagar o {0}? As strings dele ficam livres (continuam no desenho) e o retângulo sai do campo.", nomes)
            : Tr.F("Apagar {0} inversores ({1})? As strings deles ficam livres (continuam no desenho) e os retângulos saem do campo.", inversores.Count, nomes);
        if (MessageBox.Show(Window.GetWindow(this), pergunta, Tr.T("Apagar inversor"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        var ids = inversores.Select(i => i.Id).ToList();
        var nenhum = false;
        Fazer(() =>
        {
            var (apagados, soltas) = ApagarInversores(Documento.Database, ids);
            nenhum = apagados.Count == 0;
            if (nenhum) return null;
            return apagados.Count == 1
                ? Tr.F("{0} apagado do cadastro; {1} string(s) ficaram livres no desenho.", apagados[0].Name, soltas)
                : Tr.F("{0} inversores apagados do cadastro; {1} string(s) ficaram livres no desenho.", apagados.Count, soltas);
        });
        if (nenhum) Avisar(Tr.T("Esse inversor não está mais no cadastro."), erro: true);
    }

    /// <summary>
    /// O "Trafo pelo desenho…": a lista dos trafos; escolher um esconde a
    /// janela e roda o CLIVUS_ELETRICA_SKID com ele (o nome do skid fica o
    /// que já tem, ou "Skid T1"), para clicar os retângulos dos inversores.
    /// </summary>
    private void MenuDoTrafoPeloDesenho(Button botao)
    {
        if (_setup.Transformers.Count == 0)
        {
            Avisar(Tr.T("Nenhum trafo ainda: cadastre na aba Transformador."), erro: true);
            return;
        }

        if (!EquipamentoEmCampo.EmCampo(Documento.Database).Any(e => e.Kind == EquipmentKind.Inverter))
        {
            Avisar(Tr.T("Nenhum inversor em campo: ponha os inversores no desenho antes (⋯ › Pôr em campo, na linha de cada um)."), erro: true);
            return;
        }

        var menu = new ContextMenu { PlacementTarget = botao, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var t in _setup.Transformers)
        {
            var id = t.Id;
            Item(menu, string.Equals(t.Nickname, t.Name, StringComparison.Ordinal) ? t.Nickname : Tr.F("{0} — {1}", t.Nickname, t.Name),
                Tr.F("A janela some: clique nos retângulos dos inversores do {0} (Shift+clique tira) e Enter volta.", t.Nickname),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaSkid, id.ToString("D") + "\n"));
        }

        menu.IsOpen = true;
    }

    /// <summary>Grava a cor nova do inversor e repinta as strings dele (só representação; o vínculo não muda).</summary>
    private void TrocarCor(Guid id, RgbColor cor)
    {
        if (_setup.FindInverter(id) is not { } inversor || (inversor.Color ?? InverterColors.Palette[0].Color) == cor) return;

        Fazer(() =>
        {
            if (!ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.SetInverterColor(id, cor))) return Tr.T("Esse inversor não está mais no cadastro.");
            var n = CorDasStrings.Repintar(Documento.Database, id);
            return Tr.F("{0}: cor trocada para {1}; {2} string(s) repintada(s).", inversor.Name, cor.ToHex(), n);
        });
    }

    /// <summary>Põe nas caixas a varredura gravada (sem gravar de novo).</summary>
    private void MostrarVarredura(AllocationScan varredura)
    {
        _mostrandoVarredura = true;
        try
        {
            _sentidoDaAtribuicao.SelectedItem = _sentidoDaAtribuicao.Items.OfType<ComboBoxItem>().First(i => (ScanDirection)i.Tag == varredura.Direction);
            MontarFaixa(varredura.Direction, varredura.Cross);
        }
        finally
        {
            _mostrandoVarredura = false;
        }
    }

    /// <summary>As duas opções da faixa (as perpendiculares ao sentido), com a dada escolhida (ou a primeira).</summary>
    private void MontarFaixa(ScanDirection sentido, ScanDirection faixa)
    {
        _faixaDaAtribuicao.Items.Clear();
        foreach (var f in ScanOrder.CrossOptions(sentido)) _faixaDaAtribuicao.Items.Add(new ComboBoxItem { Content = ScanOrder.Describe(f), Tag = f });
        _faixaDaAtribuicao.SelectedItem = _faixaDaAtribuicao.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (ScanDirection)i.Tag == faixa) ?? _faixaDaAtribuicao.Items[0];
    }

    /// <summary>O usuário mudou o sentido ou a faixa: grava a varredura da atribuição (a da numeração não muda).</summary>
    private void MudouAVarredura(bool trocouOSentido)
    {
        if (_mostrandoVarredura || (_sentidoDaAtribuicao.SelectedItem as ComboBoxItem)?.Tag is not ScanDirection sentido) return;

        if (trocouOSentido)
        {
            var antes = (_faixaDaAtribuicao.SelectedItem as ComboBoxItem)?.Tag as ScanDirection?;
            _mostrandoVarredura = true;
            try { MontarFaixa(sentido, antes ?? ScanOrder.DefaultCross(sentido)); }
            finally { _mostrandoVarredura = false; }
        }

        if ((_faixaDaAtribuicao.SelectedItem as ComboBoxItem)?.Tag is not ScanDirection faixa) return;

        var varredura = new AllocationScan(sentido, faixa);
        try
        {
            EscritaForaDeComando.Fazer(Documento, () => AtribuicaoAutomatica.GravarVarredura(Documento.Database, varredura));
            Avisar(Tr.F("Varredura da atribuição: {0}.", varredura.Describe()));
        }
        catch (Exception erro)
        {
            // A caixa mostraria uma varredura que não ficou gravada: volta à gravada e avisa.
            RegistroDeDiagnostico.Registrar("Falha ao gravar a varredura da atribuição.", erro);
            MostrarVarredura(AtribuicaoAutomatica.Varredura(Documento.Database).Varredura);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>"Atribuir strings": as livres enchem os inversores na ordem da varredura; o relatório no rodapé.</summary>
    private void AtribuirStrings()
    {
        // "Carregando..." com a porcentagem (05/10/2026), a janela travada enquanto isso.
        var progresso = JanelaDeProgresso.Abrir(Window.GetWindow(this), Tr.T("Distribuição automática"));
        (AutoAllocationResult r, IReadOnlyList<string> linhas) resultado;
        try
        {
            resultado = EscritaForaDeComando.Fazer(Documento, () => AtribuicaoAutomatica.Atribuir(Documento.Database, (p, t) => progresso.Avancar(p, t)));
        }
        finally
        {
            progresso.Fechar();
        }

        var (r, linhas) = resultado;
        (AoMudar ?? Atualizar)();
        Avisar(string.Join("\n", linhas), erro: r.Changed.Count == 0 || r.Leftover > 0 || r.WithoutModel.Count > 0 || r.Unplaced > 0 || r.Duplicates > 0);
    }

    private void SoltarTodasDaUsina() =>
        Fazer(() =>
        {
            var soltas = StringsDoDesenho.SoltarTodasDaUsina(Documento.Database);
            return Tr.F("{0} string(s) soltas de todos os inversores; continuam no desenho, livres.", soltas);
        });

    private void SoltarTodas(Inverter inversor) =>
        Fazer(() =>
        {
            var soltas = StringsDoDesenho.Soltar(Documento.Database, inversor.Id);
            return Tr.F("{0}: {1} string(s) soltas; continuam no desenho, livres.", inversor.Name, soltas);
        });

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

    /// <summary>A potência digitada: vazio = 0 (não informada); senão um número de 0 ao limite. Se leu.</summary>
    internal static bool LerPotencia(string? texto, out double kw)
    {
        kw = 0;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        return NumberInput.TryParseLarge(texto, out kw) && InverterModel.IsValidPower(kw);
    }

    private void SalvarModelo()
    {
        if (ModeloEscolhido is not { } m)
        {
            Avisar(Tr.T("Escolha um modelo na lista."), erro: true);
            return;
        }

        if (!NumberInput.TryParseCount(_mppt.Text, out var mppt) || LerEntradas() is not { } entradas)
        {
            Avisar(Tr.T("O número de MPPTs e as entradas de cada MPPT são números inteiros."), erro: true);
            return;
        }

        if (entradas.Count != mppt)
        {
            Avisar(Tr.F("O número de MPPTs tem que ser de 1 a {0}.", ElectricalDefaults.MaxMppts), erro: true);
            return;
        }

        if (!LerPotencia(_potencia.Text, out var potencia))
        {
            Avisar(Tr.F("Potência: um número de 0 a {0:#,0} kW (vazio = não informada).", ElectricalDefaults.MaxInverterPowerKw), erro: true);
            return;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;

        var editado = m with { Name = _nomeDoModelo.Text, InputsByMppt = entradas, Size = tamanho, PowerKw = potencia };
        string? porque = null;
        Fazer(() =>
        {
            porque = GravarModelo(Documento.Database, editado);
            return porque is null ? Tr.F("{0} salvo.", editado.Name.Trim()) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
    }

    /// <summary>
    /// Grava o modelo editado e redesenha os retângulos dos inversores dele
    /// em campo (a dimensão do modelo é a deles). Null se gravou, o porquê se
    /// não. Quem chama trava o documento (o Fazer da aba, ou o gancho do nível 2).
    /// </summary>
    internal static string? GravarModelo(Database database, InverterModel editado)
    {
        var porque = ConfiguracaoEletricaStore.Mudar(database, s => s.EditModel(editado));
        if (porque is not null) return porque;

        EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Inverter,
            ConfiguracaoEletricaStore.Ler(database).Setup.Inverters.Where(i => i.Model == editado.Id).Select(i => i.Id).ToList());
        return null;
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
