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
/// caixa na própria linha, que grava na hora; várias linhas escolhidas (Ctrl
/// ou Shift) vão juntas pelo "Pôr no trafo". É o mesmo vínculo do skid
/// (<see cref="Inverter.Transformer"/>); o agrupar pela seleção em campo
/// ficou num quadro fechado, "Agrupar em campo (skid)".
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

    /// <summary>A tabela: uma linha por inversor, várias escolhidas com Ctrl ou Shift.</summary>
    private readonly ListBox _inversores = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        SelectionMode = SelectionMode.Extended,
        Padding = new Thickness(0),
    };

    private readonly ContentControl _rodapeDaTabela = new();
    private readonly ComboBox _trafoEmLote = new() { Height = 26, MinWidth = 90, Margin = new Thickness(0, 0, 6, 6) };

    private readonly TextBox _nomeDoInversor = new() { Width = 150, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _modeloDoInversor = new() { Height = 26, MinWidth = 140, Margin = new Thickness(0, 0, 6, 6) };

    /// <summary>Onde fica a caixa de cor do inversor escolhido (refeita a cada escolha, com a cor dele).</summary>
    private readonly ContentControl _lugarDaCor = new() { Margin = new Thickness(0, 0, 6, 6), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _trafoDoSkid = new() { Height = 26, MinWidth = 90, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _nomeDoSkid = new() { Width = 140, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };

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
        var criar = new WrapPanel();
        criar.Children.Add(new TextBlock { Text = Tr.T("Modelo:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        criar.Children.Add(_modeloParaCriar);
        criar.Children.Add(new TextBlock { Text = Tr.T("Quantos:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        criar.Children.Add(_quantos);
        Botao(criar, Tr.T("Criar inversores"), Tr.T("Cria os inversores do modelo escolhido (Inversor 1, 2..., continuando a numeração)."), CriarInversores);

        // A atribuição automática das strings livres nos inversores, pela varredura dela.
        var atribuir = new WrapPanel();
        atribuir.Children.Add(new TextBlock { Text = Tr.T("Atribuir:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        atribuir.Children.Add(_sentidoDaAtribuicao);
        atribuir.Children.Add(new TextBlock { Text = Tr.T("na faixa:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        atribuir.Children.Add(_faixaDaAtribuicao);
        Botao(atribuir, Tr.T("Distribuição automática"), Tr.T("As strings livres, na ordem desta varredura, enchem os inversores na ordem da lista, cada um até o total de entradas. As já alocadas não mudam (e contam); inversor cheio é pulado; as que sobrarem são avisadas. Para redistribuir do zero, use antes Soltar todas da usina."), AtribuirStrings);
        Botao(atribuir, Tr.T("Soltar todas da usina"), Tr.T("Solta as strings de todos os inversores: ficam livres e continuam no desenho (nada é apagado). Depois, a Distribuição automática redistribui do zero."), SoltarTodasDaUsina);
        _sentidoDaAtribuicao.ToolTip = Tr.T("O sentido em que a varredura da atribuição avança (é desta atribuição; a numeração tem a sua).");
        _faixaDaAtribuicao.ToolTip = Tr.T("Na mesma faixa (linha ou coluna), em que sentido as strings são tomadas.");
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

        // As linhas escolhidas (Ctrl ou Shift) num trafo de uma vez.
        var lote = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        Botao(lote, Tr.T("Pôr no trafo"), Tr.T("Põe todas as linhas escolhidas na tabela no trafo ao lado (Ctrl+clique ou Shift+clique escolhe várias)."), PorAsEscolhidasNoTrafo);
        lote.Children.Add(_trafoEmLote);
        lote.Children.Add(new TextBlock { Text = Tr.T("as linhas escolhidas (Ctrl ou Shift + clique para várias)"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6), Foreground = System.Windows.SystemColors.GrayTextBrush });

        // O inversor escolhido na tabela: editar e apagar (14.5).
        var editar = new WrapPanel();
        editar.Children.Add(new TextBlock { Text = Tr.T("Escolhido:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        editar.Children.Add(_nomeDoInversor);
        editar.Children.Add(_modeloDoInversor);
        editar.Children.Add(_lugarDaCor);
        Botao(editar, Tr.T("Salvar inversor"), Tr.T("Grava o nome e o modelo do inversor escolhido."), SalvarInversor);
        Botao(editar, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda."), () =>
        {
            if (SoUmEscolhido() is { } i) AlocarEmCampo(i.Id);
        });
        Botao(editar, Tr.T("Apagar inversor"), Tr.T("Tira o inversor do cadastro: as strings dele ficam livres (continuam no desenho) e o retângulo dele sai do campo."), ApagarInversor);

        // O skid (14.7) pela seleção em campo: fechado, para quem quer.
        var skid = new WrapPanel();
        skid.Children.Add(new TextBlock { Text = Tr.T("Trafo:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        skid.Children.Add(_trafoDoSkid);
        skid.Children.Add(new TextBlock { Text = Tr.T("nome do skid:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        skid.Children.Add(_nomeDoSkid);
        Botao(skid, Tr.T("Agrupar em campo"), Tr.T("A janela some: selecione em campo só os retângulos dos inversores do skid (Shift+clique tira), Enter volta. Inversor de outro skid fica travado."), AgruparEmCampo);
        _trafoDoSkid.SelectionChanged += (_, _) =>
        {
            try { _nomeDoSkid.Text = (_trafoDoSkid.SelectedItem as ComboBoxItem)?.Tag is Guid t ? _setup.FindSkid(t)?.Name ?? string.Empty : string.Empty; }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o skid do trafo.", erro); }
        };

        var explicacao = new TextBlock
        {
            Text = Tr.T("Skid = inversores montados juntos com o trafo num mesmo lugar em campo. Escolher o trafo na coluna Trafo já põe o inversor no skid desse trafo; aqui é o mesmo, escolhendo os retângulos no desenho e dando nome ao grupo."),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 6),
            Foreground = System.Windows.SystemColors.GrayTextBrush,
        };
        var quadroDoSkid = new StackPanel();
        quadroDoSkid.Children.Add(explicacao);
        quadroDoSkid.Children.Add(skid);
        var expansor = new Expander { Header = Tr.T("Agrupar em campo (skid)"), IsExpanded = false, Content = quadroDoSkid, Margin = new Thickness(0, 0, 0, 2) };

        var rodape = new StackPanel();
        rodape.Children.Add(lote);
        rodape.Children.Add(editar);
        rodape.Children.Add(expansor);

        // A tabela: cabeçalho, linhas e total com as mesmas colunas (SharedSizeGroup).
        var tabela = new Grid();
        Grid.SetIsSharedSizeScope(tabela, true);
        tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tabela.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabela.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cabecalho = Cabecalho();
        Grid.SetRow(_inversores, 1);
        Grid.SetRow(_rodapeDaTabela, 2);
        tabela.Children.Add(cabecalho);
        tabela.Children.Add(_inversores);
        tabela.Children.Add(_rodapeDaTabela);

        var inversores = new DockPanel();
        var topo = new StackPanel();
        topo.Children.Add(Titulo(Tr.T("Inversores da usina")));
        topo.Children.Add(criar);
        topo.Children.Add(atribuir);
        DockPanel.SetDock(topo, Dock.Top);
        DockPanel.SetDock(rodape, Dock.Bottom);
        inversores.Children.Add(topo);
        inversores.Children.Add(rodape);
        inversores.Children.Add(tabela);

        _inversores.SelectionChanged += (_, e) =>
        {
            try { if (e.OriginalSource == _inversores) PreencherInversor(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o inversor escolhido.", erro); }
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

        MontarTrafos(_trafoDoSkid, setup, comSemTrafo: false);
        MontarTrafos(_trafoEmLote, setup, comSemTrafo: true);

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
        Margin = new Thickness(0, 0, 12, 0),
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

        Titulo(ColunaNome, Tr.T("Inversor"), Tr.T("O nome do inversor (a tag)."));
        Titulo(ColunaModelo, Tr.T("Modelo"), Tr.T("O modelo do inversor (à esquerda, o cadastro dos modelos)."));
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

    private Inverter? InversorEscolhido => (_inversores.SelectedItem as ListBoxItem)?.Tag as Inverter;

    /// <summary>
    /// O inversor do "Escolhido" para salvar, alocar em campo ou apagar: com
    /// várias linhas escolhidas, recusa e avisa (apagar só a primeira, sem
    /// dizer, enganaria). Null com o recado.
    /// </summary>
    private Inverter? SoUmEscolhido()
    {
        if (_inversores.SelectedItems.Count > 1)
        {
            Avisar(Tr.F("Há {0} linhas escolhidas: para isso, escolha uma só.", _inversores.SelectedItems.Count), erro: true);
            return null;
        }

        if (InversorEscolhido is null) Avisar(Tr.T("Escolha um inversor na lista."), erro: true);
        return InversorEscolhido;
    }

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
        var escolhidos = _inversores.SelectedItems.OfType<ListBoxItem>().Select(i => ((Inverter)i.Tag).Id).ToHashSet();
        var principal = InversorEscolhido?.Id;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);
        var linhas = LinhasDaTabela(Documento, _setup, _contagem);
        _inversores.Items.Clear();

        foreach (var linha in linhas)
        {
            var item = new ListBoxItem { Content = MontarLinha(linha, emCampo.Contains((EquipmentKind.Inverter, linha.Inverter.Id))), Tag = linha.Inverter, Padding = new Thickness(2, 1, 2, 1) };
            _inversores.Items.Add(item);
        }

        // A escolha volta (a principal primeiro, para o "Escolhido" ser o mesmo).
        foreach (var item in _inversores.Items.OfType<ListBoxItem>().OrderBy(i => ((Inverter)i.Tag).Id == principal ? 0 : 1))
            if (escolhidos.Contains(((Inverter)item.Tag).Id)) _inversores.SelectedItems.Add(item);

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
    }

    /// <summary>Uma linha: cor, nome, modelo, a caixa do trafo, strings, kWp, kW, CC/CA e os botões.</summary>
    private Grid MontarLinha(InverterTableRow linha, bool emCampo)
    {
        var inversor = linha.Inverter;
        var g = LinhaDaTabela();

        // A cor do inversor (a das strings dele no desenho).
        var cor = inversor.Color ?? InverterColors.Palette[0].Color;
        Por(g, new System.Windows.Shapes.Rectangle
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(cor.R, cor.G, cor.B)),
            Stroke = System.Windows.Media.Brushes.Gray,
            ToolTip = Tr.F("Cor das strings deste inversor: {0}", cor.ToHex()),
        }, ColunaCor);

        var aviso = StringAllocation.ExcessWarning(inversor, linha.Model, linha.Strings);
        var nome = Por(g, Celula(ComCampo(inversor.Name, emCampo), larguraMaxima: 220), ColunaNome);
        nome.ToolTip = aviso ?? ComCampo(inversor.Name, emCampo);

        var modelo = Por(g, Celula(linha.Model?.Name ?? Tr.T("sem modelo"), larguraMaxima: 180), ColunaModelo);
        modelo.ToolTip = linha.Model is { } m ? DescreverModelo(m) : null;

        Por(g, CaixaDoTrafo(inversor), ColunaTrafo);

        var strings = Por(g, Celula(Strings(linha.Strings, linha.Capacity, linha.Model is not null), numero: true), ColunaStrings);
        strings.ToolTip = aviso;
        if (aviso is not null)
        {
            foreach (var t in new[] { nome, strings })
            {
                t.Foreground = System.Windows.Media.Brushes.Firebrick;
                t.FontWeight = FontWeights.SemiBold;
            }
        }

        Por(g, Celula(Kwp(linha.PowerKwp), numero: true), ColunaKwp);
        Por(g, Celula(linha.PowerKw > 0 ? Kw(linha.PowerKw) : "—", numero: true), ColunaKw);
        Por(g, Celula(Razao(linha.DcAcRatio), numero: true), ColunaRazao);

        var acoes = Por(g, new StackPanel { Orientation = Orientation.Horizontal }, ColunaAcoes);
        var id = inversor.Id;
        var botoes = new[]
        {
            Botao(acoes, "+", Tr.T("Alocar strings: a janela some; selecione só strings em campo (Shift+clique tira), Enter volta."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaAlocar, id.ToString("D")), largura: 28),
            Botao(acoes, Tr.T("Selecionar"), Tr.T("Seleciona no CAD todas as strings deste inversor."),
                () => JanelaEletrica.SelecionarStrings(Documento, id)),
            Botao(acoes, Tr.T("Soltar strings"), Tr.T("Solta as strings deste inversor: elas ficam livres e continuam no desenho (nada é apagado)."),
                () => SoltarTodas(inversor)),
        };
        foreach (var b in botoes)
        {
            b.Height = 22;
            b.Margin = new Thickness(0, 1, 4, 1);
        }

        return g;
    }

    /// <summary>
    /// A caixa do trafo na linha: "sem trafo" e os trafos do cadastro (um
    /// vínculo para trafo que sumiu aparece como "?"). Escolher grava na hora.
    /// </summary>
    private ComboBox CaixaDoTrafo(Inverter inversor)
    {
        var caixa = new ComboBox { Height = 22, MinWidth = 86, Margin = new Thickness(0, 0, 12, 0), VerticalContentAlignment = VerticalAlignment.Center };
        caixa.Items.Add(new ComboBoxItem { Content = Tr.T("sem trafo"), Tag = Guid.Empty });
        foreach (var t in _setup.Transformers)
            caixa.Items.Add(new ComboBoxItem { Content = t.Nickname, Tag = t.Id, ToolTip = t.Name });

        if (inversor.Transformer != Guid.Empty && _setup.FindTransformer(inversor.Transformer) is null)
            caixa.Items.Add(new ComboBoxItem { Content = "?", Tag = inversor.Transformer, ToolTip = Tr.T("o trafo dele não está no cadastro") });

        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().First(i => (Guid)i.Tag == inversor.Transformer);
        caixa.ToolTip = _setup.FindSkid(inversor.Transformer) is { } skid
            ? Tr.F("O trafo deste inversor (skid {0}). Escolher grava na hora.", skid.Name)
            : Tr.T("O trafo deste inversor. Escolher grava na hora.");

        // Grava quando a lista fecha (ou a caixa perde o foco) com outro trafo:
        // a seta do teclado na caixa fechada não grava a cada passo (revisão
        // de 05/10/2026: quem desce com a seta trocaria o trafo sem querer).
        var id = inversor.Id;
        var atual = inversor.Transformer;
        var gravando = false;
        void Confirmar()
        {
            try
            {
                if (gravando || (caixa.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo || trafo == atual) return;
                gravando = true;

                // Depois que a escolha assenta: gravar relê e refaz a tabela (e esta caixa).
                Dispatcher.BeginInvoke(() =>
                {
                    try { PorNoTrafoPelaTela([id], trafo); }
                    catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao trocar o trafo do inversor.", erro); }
                });
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao escolher o trafo do inversor.", erro);
            }
        }

        caixa.DropDownClosed += (_, _) => Confirmar();
        caixa.LostKeyboardFocus += (_, _) => Confirmar();
        caixa.SelectionChanged += (_, e) => e.Handled = true;   // não sobe para a tabela (o "Escolhido")

        return caixa;
    }

    /// <summary>O "Pôr no trafo": as linhas escolhidas vão todas para o trafo da caixa ao lado.</summary>
    private void PorAsEscolhidasNoTrafo()
    {
        var ids = _inversores.SelectedItems.OfType<ListBoxItem>().Select(i => ((Inverter)i.Tag).Id).ToList();
        if (ids.Count == 0)
        {
            Avisar(Tr.T("Escolha uma ou mais linhas na tabela (Ctrl ou Shift + clique para várias)."), erro: true);
            return;
        }

        if ((_trafoEmLote.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo)
        {
            Avisar(Tr.T("Escolha o trafo (cadastre na aba Transformador, se não há)."), erro: true);
            return;
        }

        PorNoTrafoPelaTela(ids, trafo);
    }

    private void PorNoTrafoPelaTela(IReadOnlyCollection<Guid> inversores, Guid trafo)
    {
        string? porque = null;
        var gravou = false;
        Fazer(() =>
        {
            var (frase, problema) = PorNoTrafo(Documento.Database, inversores, trafo);
            porque = problema;
            gravou = true;
            return frase;
        });

        // Recusado ou falhou: a tabela volta ao que está gravado (a caixa da linha não fica mostrando o que não foi).
        if (porque is not null || !gravou)
        {
            try { Atualizar(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao reler a tabela de inversores.", erro); }
        }

        if (porque is not null) Avisar(Tr.F("Não mudei: {0}.", porque), erro: true);
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

    private void PreencherInversor()
    {
        var inversor = InversorEscolhido;
        _nomeDoInversor.IsEnabled = _modeloDoInversor.IsEnabled = inversor is not null;
        _nomeDoInversor.Text = inversor?.Name ?? string.Empty;
        MontarCor(inversor);

        _modeloDoInversor.Items.Clear();
        foreach (var m in _setup.Models)
        {
            var item = new ComboBoxItem { Content = m.Name, Tag = m.Id };
            _modeloDoInversor.Items.Add(item);
            if (m.Id == inversor?.Model) _modeloDoInversor.SelectedItem = item;
        }
    }

    /// <summary>
    /// A caixa de cor do inversor escolhido (a paleta dos inversores e "Mais
    /// cores..."). Escolher uma cor grava na hora e repinta as strings dele.
    /// </summary>
    private void MontarCor(Inverter? inversor)
    {
        if (inversor is null)
        {
            _lugarDaCor.Content = null;
            return;
        }

        var caixa = PaletaDeCores.Caixa(inversor.Color ?? InverterColors.Palette[0].Color, Tr.T("A cor do inversor: as strings dele ficam desta cor no desenho."), InverterColors.Palette);
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

        _lugarDaCor.Content = caixa;
    }

    /// <summary>Grava a cor nova do inversor e repinta as strings dele (só representação; o vínculo não muda).</summary>
    private void TrocarCor(Guid id, RgbColor cor)
    {
        if (_setup.FindInverter(id) is not { } inversor || inversor.Color == cor) return;

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

    private void SalvarInversor()
    {
        if (_inversores.SelectedItems.Count > 1)
        {
            SoUmEscolhido();
            return;
        }

        if (InversorEscolhido is not { } inversor || (_modeloDoInversor.SelectedItem as ComboBoxItem)?.Tag is not Guid modelo)
        {
            Avisar(Tr.T("Escolha um inversor na lista e o modelo dele."), erro: true);
            return;
        }

        string? porque = null;
        var nome = _nomeDoInversor.Text;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditInverter(inversor.Id, nome, modelo));
            if (porque is not null) return null;
            EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.Inverter, inversor.Id);
            return Tr.F("{0} salvo.", nome.Trim());
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
    }

    private void ApagarInversor()
    {
        if (SoUmEscolhido() is not { } inversor) return;

        var tirou = true;
        Fazer(() =>
        {
            // Primeiro o cadastro: se o registro não pode ser gravado, nada
            // muda. Depois o vínculo das strings no desenho (se falhar, a
            // string fica apontando para um inversor que não existe, e essa
            // não trava: pode ser alocada de novo).
            tirou = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveInverter(inversor.Id));
            if (!tirou) return null;
            var soltas = StringsDoDesenho.Soltar(Documento.Database, inversor.Id);
            EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.Inverter, inversor.Id);
            return Tr.F("{0} apagado do cadastro; {1} string(s) ficaram livres no desenho.", inversor.Name, soltas);
        });
        if (!tirou) Avisar(Tr.T("Esse inversor não está mais no cadastro."), erro: true);
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

    private void AgruparEmCampo()
    {
        if ((_trafoDoSkid.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo)
        {
            Avisar(Tr.T("Escolha o trafo do skid (cadastre na aba Transformador, se não há)."), erro: true);
            return;
        }

        // O nome vai como resposta da segunda pergunta do comando (até o Enter).
        JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaSkid, trafo.ToString("D") + "\n" + _nomeDoSkid.Text.Trim());
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
