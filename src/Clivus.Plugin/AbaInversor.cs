using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Inversor (etapa 14): a tabela dos inversores da usina (cor, nome,
/// modelo, trafo, strings, kWp, kW, CC/CA, local e as ações de cada um) com
/// as seções de lote em cima. Os modelos de inversor (nome, potência, os
/// MPPTs com as entradas de cada um, o total somado, a dimensão) ficavam à
/// esquerda; desde 10/10/2026 (item 7) ficam no modal "Modelos de inversor…".
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
/// (+ Strings, Ver e o "⋯" com Soltar strings, Pôr/Mover em campo e Apagar;
/// o "⋯" saiu em 10/10/2026, veja abaixo).
/// Com duas ou mais linhas escolhidas aparece a barra delas (trafo de todas,
/// apagar todas). O agrupar pela seleção em campo virou o botão "Trafo pelo
/// desenho…" (o nome do skid sai da tela; fica o que já tem, ou "Skid T1").
/// 10/10/2026 (Renan: "tem o botão três pontinhos, mas tem espaço para mais
/// colunas"): o "⋯" saiu; Soltar, Pôr em campo/Mover e Apagar ficam à vista na
/// linha. Soltar apaga as tags das strings soltas; "Apagar todos" no quadro
/// Inversores da usina; a ordem da lista (a do cadastro: a da tabela, a do
/// Distribuir e o número {I} da tag) muda arrastando a linha pela alça "⠿" ou
/// pelo "Ordenar" (nome, trafo, trafo e nome).
/// </remarks>
internal sealed class AbaInversor : AbaEletrica
{
    private readonly ListBox _modelos = new() { MinHeight = 110 };
    private readonly TextBox _nomeDoModelo, _potencia, _mppt, _largura, _comprimento, _altura;

    /// <summary>As entradas de cada MPPT: uma caixa por MPPT, na ordem (cresce e encolhe com o número de MPPTs).</summary>
    private readonly WrapPanel _entradas = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly List<TextBox> _caixasDasEntradas = [];
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold };

    /// <summary>O cadastro dos modelos (lista, botões e formulário), mostrado no modal "Modelos de inversor…".</summary>
    private readonly StackPanel _painelDosModelos;

    /// <summary>"Limites: 480 de 480 strings úteis" (item 5 de 10/10/2026).</summary>
    private readonly TextBlock _somaDosLimites = new() { Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };

    /// <summary>Quantas strings a usina tem (cada GUID uma vez), lido a cada Atualizar.</summary>
    private int _stringsUteis;

    private readonly ComboBox _modeloParaCriar = new() { Height = 26, MinWidth = 160, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _quantos = new() { Text = "1", Width = 50, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };

    // As seções do topo (07/10/2026, Renan: "lote de inversores é uma seção,
    // lote de limite de strings é outra seção"): cada uma com o seu "do ... ao ...".
    private readonly ComboBox _trafoDo = Intervalo();
    private readonly ComboBox _trafoAo = Intervalo();
    private readonly ComboBox _limiteDo = Intervalo();
    private readonly ComboBox _limiteAo = Intervalo();
    private readonly TextBox _limiteDoLote = new() { Width = 44, Height = 24, Margin = new Thickness(0, 0, 6, 4), VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Right };

    /// <summary>A tabela: uma linha por inversor, várias escolhidas com Ctrl ou Shift.</summary>
    private readonly ListBox _inversores = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        SelectionMode = SelectionMode.Extended,
        Padding = new Thickness(0),
    };

    private readonly ContentControl _rodapeDaTabela = new();

    private readonly ComboBox _trafoDasEscolhidas = new() { Height = 24, MinWidth = 90, Margin = new Thickness(0, 0, 6, 4) };
    private readonly Button _apagarAsEscolhidas;
    private readonly Button _apagarTodos;

    /// <summary>O "Ordenar:" do quadro Inversores da usina: por nome, por trafo, por trafo e nome.</summary>
    private readonly ComboBox _ordem = new() { Height = 24, MinWidth = 120, Margin = new Thickness(0, 0, 6, 4) };

    /// <summary>A marca de onde a linha arrastada vai cair, uma por linha (pelo inversor).</summary>
    private readonly Dictionary<Guid, Border> _marcas = [];

    /// <summary>O formato do arrasto de uma linha da tabela (o GUID do inversor).</summary>
    private const string FormatoDoArrasto = "ClivusInversor";

    // A atribuição automática: o sentido que avança e o sentido na faixa (a varredura própria dela).
    private readonly ComboBox _sentidoDaAtribuicao = new() { Height = 26, MinWidth = 150, Margin = new Thickness(0, 0, 6, 6) };
    private readonly ComboBox _faixaDaAtribuicao = new() { Height = 26, MinWidth = 150, Margin = new Thickness(0, 0, 6, 6) };

    // As opções da pré-tag no Distribuir (item 5 da segunda rodada de 10/10/2026), tudo marcado por padrão.
    private readonly CheckBox _preTagNome = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) };
    private readonly CheckBox _preTagMoldura = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) };
    private readonly CheckBox _preTagFundo = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) };
    private bool _mostrandoAPreTag;
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

        // O cadastro dos modelos vive num modal (item 7 de 10/10/2026: "a lateral
        // é comida com a parte de cadastro de inversor, transforme isso em um
        // modal"); a aba fica toda para os inversores da usina.
        var modelos = new StackPanel();
        modelos.Children.Add(_modelos);
        modelos.Children.Add(botoesDoModelo);
        modelos.Children.Add(grade);
        _painelDosModelos = modelos;

        // ---------------------------------------------- inversores (14.2)
        // Seção Inversores: criar e apagar os escolhidos na tabela.
        var criar = new WrapPanel();
        criar.Children.Add(Rotulo(Tr.T("Criar"), 4));
        criar.Children.Add(_quantos);
        criar.Children.Add(Rotulo(Tr.T("inversor(es) do modelo"), 4));
        criar.Children.Add(_modeloParaCriar);
        _quantos.ToolTip = Tr.F("Quantos inversores criar de uma vez (1 a {0}).", ElectricalDefaults.MaxAtOnce);
        _modeloParaCriar.ToolTip = Tr.T("O modelo dos inversores novos (os modelos são cadastrados em Modelos de inversor…).");
        Botao(criar, Tr.T("Criar"), Tr.T("Cria os inversores do modelo escolhido (Inversor 1, 2..., continuando a numeração). Depois escolha o trafo de cada um na tabela."), CriarInversores);
        Botao(criar, Tr.T("Modelos de inversor…"), Tr.T("Abre o cadastro dos modelos de inversor (nome, potência, MPPTs e entradas, medidas): novo, salvar e apagar modelo."), AbrirModelos);
        _apagarAsEscolhidas = Botao(criar, Tr.T("Apagar os escolhidos"), Tr.T("Apaga do cadastro os inversores escolhidos na tabela (clique, Ctrl ou Shift + clique; pede confirmação): as strings deles ficam livres e os retângulos saem do campo."), ApagarAsEscolhidas);
        _apagarAsEscolhidas.Margin = new Thickness(18, 0, 6, 4);
        _apagarTodos = Botao(criar, Tr.T("Apagar todos"), Tr.T("Apaga do cadastro todos os inversores da usina (pede confirmação): as strings ficam livres no desenho, as tags delas saem e os retângulos saem do campo."), ApagarTodos);

        // A ordem da lista (10/10/2026): "Ordenar: [por Nome | por Trafo | por Trafo e Nome] [Ordenar]".
        var ordenar = Rotulo(Tr.T("Ordenar:"), 4);
        ordenar.Margin = new Thickness(18, 0, 6, 4);
        criar.Children.Add(ordenar);
        criar.Children.Add(_ordem);
        _ordem.Items.Add(new ComboBoxItem { Content = Tr.T("por Nome"), Tag = InverterOrder.Name });
        _ordem.Items.Add(new ComboBoxItem { Content = Tr.T("por Trafo"), Tag = InverterOrder.Transformer });
        _ordem.Items.Add(new ComboBoxItem { Content = Tr.T("por Trafo e Nome"), Tag = InverterOrder.TransformerThenName });
        _ordem.SelectedIndex = 2;
        _ordem.ToolTip = Tr.T("Nome: Inversor 2 antes de Inversor 10. Trafo: T1, T2, ..., T10 (sem trafo por último), cada trafo com a ordem que já tinha. Trafo e Nome: os do T1 pelo nome, depois os do T2...");
        Botao(criar, Tr.T("Ordenar"), Tr.T("Grava a ordem escolhida na lista de inversores: é a ordem da tabela, a do Distribuir e a do número do inversor na tag (gere as tags de novo). Para pôr um inversor num lugar, arraste a linha pela alça ⠿."), OrdenarPelaTela);

        // Seção Trafo: "Do [..] ao [..] [Todos]  no trafo [..] [Aplicar]  [Trafo pelo desenho…]".
        var trafo = new WrapPanel();
        trafo.Children.Add(Rotulo(Tr.T("Do"), 4));
        trafo.Children.Add(_trafoDo);
        trafo.Children.Add(Rotulo(Tr.T("ao"), 4));
        trafo.Children.Add(_trafoAo);
        Botao(trafo, Tr.T("Todos"), Tr.T("Do primeiro ao último inversor da tabela."), () => Todos(_trafoDo, _trafoAo));
        trafo.Children.Add(Rotulo(Tr.T("no trafo"), 4));
        trafo.Children.Add(_trafoDasEscolhidas);
        _trafoDasEscolhidas.ToolTip = Tr.T("O trafo desses inversores (sem trafo solta).");
        Botao(trafo, Tr.T("Aplicar"), Tr.T("Põe os inversores do ... ao ... no trafo escolhido (o que era de outro trafo muda)."), PorOIntervaloNoTrafo);
        Button? peloDesenho = null;
        peloDesenho = Botao(trafo, Tr.T("Trafo pelo desenho…"), Tr.T("Escolha um trafo e depois clique nos inversores no desenho: eles passam a ser desse trafo (só os que já estão em campo; inversor de outro trafo não muda)."), () => MenuDoTrafoPeloDesenho(peloDesenho!));
        peloDesenho.Margin = new Thickness(18, 0, 6, 4);

        // Seção Strings: o limite por inversor e a distribuição das livres.
        var limite = new WrapPanel();
        limite.Children.Add(Rotulo(Tr.T("Limite: do"), 4));
        limite.Children.Add(_limiteDo);
        limite.Children.Add(Rotulo(Tr.T("ao"), 4));
        limite.Children.Add(_limiteAo);
        Botao(limite, Tr.T("Todos"), Tr.T("Do primeiro ao último inversor da tabela."), () => Todos(_limiteDo, _limiteAo));
        var repartir = Botao(limite, Tr.T("Repartir pelo kW"), Tr.T("Simula o Distribuir (a mesma varredura e a ordem da tabela) e dá a cada um desses inversores o limite que deixa a potência das strings dele (kWp) na proporção do kW dele, sem passar das entradas. As strings já presas a inversores fora do trecho não entram na conta. Depois: Soltar todas da usina e Distribuir."), RepartirPeloKw);
        repartir.FontWeight = FontWeights.SemiBold;
        limite.Children.Add(Rotulo(Tr.T("ou"), 4));
        limite.Children.Add(_limiteDoLote);
        _limiteDoLote.ToolTip = Tr.T("Quantas strings o Distribuir põe em cada um desses inversores (vazio: todas as entradas do modelo).");
        limite.Children.Add(Rotulo(Tr.T("strings cada"), 4));
        Botao(limite, Tr.T("Aplicar"), Tr.T("Grava esse limite nos inversores do ... ao ... (vazio tira o limite)."), PorOLimiteNoIntervalo);

        var atribuir = new WrapPanel();
        atribuir.Children.Add(Rotulo(Tr.T("Distribuir as livres:"), 4));
        atribuir.Children.Add(_sentidoDaAtribuicao);
        atribuir.Children.Add(Rotulo(Tr.T("e na faixa"), 4));
        atribuir.Children.Add(_faixaDaAtribuicao);
        Botao(atribuir, Tr.T("Distribuir"), Tr.T("As strings livres, na ordem desta varredura, enchem os inversores na ordem da tabela, cada um até o limite dele (coluna Limite; vazia: o total de entradas). As já alocadas não mudam (e contam); inversor cheio é pulado; as que sobrarem são avisadas. Para redistribuir do zero, use antes Soltar todas da usina."), AtribuirStrings);
        Botao(atribuir, Tr.T("Soltar todas da usina"), Tr.T("Solta as strings de todos os inversores: ficam livres e continuam no desenho; as tags de numeração delas são apagadas. Depois, Distribuir redistribui do zero."), SoltarTodasDaUsina);
        // Item 5 da segunda rodada: "Inserir nome do inversor? Sim / Não. Moldura () Fundo ()".
        atribuir.Children.Add(Rotulo(Tr.T("Pré-tag:"), 4));
        _preTagNome.Content = Tr.T("Inserir nome do inversor");
        _preTagNome.ToolTip = Tr.T("No Distribuir, cada string ganha por cima o nome curto do inversor (I1, I2...) até a Numeração pôr a tag de verdade. Desmarcado, não desenha pré-tag (e apaga as que havia no próximo Distribuir).");
        _preTagMoldura.Content = Tr.T("Moldura");
        _preTagMoldura.ToolTip = Tr.T("O quadro em volta da pré-tag, como o das tags da Numeração.");
        _preTagFundo.Content = Tr.T("Fundo");
        _preTagFundo.ToolTip = Tr.T("O fundo na cor da tela atrás da pré-tag (esconde o módulo atrás do texto), como o das tags da Numeração.");
        foreach (var caixa in new[] { _preTagNome, _preTagMoldura, _preTagFundo })
        {
            atribuir.Children.Add(caixa);
            caixa.Checked += (_, _) => MudouAPreTag();
            caixa.Unchecked += (_, _) => MudouAPreTag();
        }

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

        // Seção Local (10/10/2026): onde os escolhidos vão em campo.
        var local = new WrapPanel();
        local.Children.Add(Rotulo(Tr.T("Os escolhidos na tabela:"), 4));
        Botao(local, Tr.T("Escolher área…"), Tr.T("A janela some; clique no retângulo (polilinha fechada) que você desenhou em campo para a sala ou o skid: os inversores escolhidos vão para dentro dele, um ao lado do outro, na cota do terreno."), EscolherArea);
        Botao(local, Tr.T("Automático pelas strings"), Tr.T("O Gerar da rota CC põe cada um ao lado da vala, no ponto de menor cabo CC das strings dele. Depois você pode mover à mão e Gerar de novo: a rota sai da posição nova."),
            () => MudarOLocal(InverterPlacementMode.Automatic));
        Botao(local, Tr.T("À mão"), Tr.T("Volta ao Pôr em campo de sempre (a posição de agora fica)."), () => MudarOLocal(null));
        var botaoAreas = Botao(local, Tr.T("Áreas…"), Tr.T("A lista das áreas de inversores do desenho: o nome de cada uma (para renomear), quantos inversores estão nela e Renomear. Um inversor é da área quando o centro dele está dentro do retângulo, não importa como foi posto."), AbrirAreas);
        botaoAreas.Margin = new Thickness(18, 0, 6, 4);

        // A soma dos limites contra as strings úteis (item 5): "Limites: 480 de 480 strings úteis".
        _somaDosLimites.ToolTip = Tr.T("A soma dos limites de todos os inversores (sem limite: todas as entradas do modelo) contra as strings da usina. Se não bate, sobra string sem inversor ou sobra vaga.");

        foreach (var painel in new[] { criar, trafo, limite, atribuir, local })
            foreach (var b in painel.Children.OfType<Button>())
            {
                b.Height = 24;
                if (b.Margin.Bottom > 4) b.Margin = new Thickness(b.Margin.Left, 0, b.Margin.Right, 4);
            }

        // A ajuda de uma linha, em cima da tabela.
        var textoDaAjuda = Tr.T("Cada linha grava na hora. Com várias linhas escolhidas (Ctrl ou Shift + clique), trocar o trafo ou o limite de uma muda todas. Arraste pela alça ⠿ para mudar a ordem.");
        var ajuda = new TextBlock
        {
            Text = textoDaAjuda,
            ToolTip = textoDaAjuda,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 0, 0, 3),
        };

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
        topo.Children.Add(Secao(Tr.T("Inversores da usina"), criar));
        topo.Children.Add(Secao(Tr.T("Trafo dos inversores"), trafo));
        // Item 5 (10/10/2026): o limite e a distribuição são duas seções, lado a lado quando cabem.
        var strings = new WrapPanel();
        var maximo = Secao(Tr.T("Quantidade máxima de strings por inversor"), limite, _somaDosLimites);
        maximo.Margin = new Thickness(0, 0, 6, 6);
        strings.Children.Add(maximo);
        strings.Children.Add(Secao(Tr.T("Distribuição das strings nos inversores"), atribuir));
        topo.Children.Add(strings);
        topo.Children.Add(Secao(Tr.T("Local dos inversores"), local));
        DockPanel.SetDock(topo, Dock.Top);
        inversores.Children.Add(topo);
        inversores.Children.Add(tabela);

        _inversores.SelectionChanged += (_, e) =>
        {
            try { if (e.OriginalSource == _inversores) MostrarAsEscolhidas(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar as linhas escolhidas.", erro); }
        };

        // Arrastar e soltar a linha (10/10/2026). Os eventos "Preview" descem
        // antes das caixas da linha: a caixa do nome não pega o arrasto como texto.
        _inversores.AllowDrop = true;
        _inversores.PreviewDragOver += (_, e) =>
        {
            try { ArrastandoPorCima(e); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao arrastar a linha de inversor.", erro); }
        };
        _inversores.PreviewDragLeave += (_, e) =>
        {
            try
            {
                // Passar de uma célula para outra também dispara: só esconde quando saiu da lista.
                var p = e.GetPosition(_inversores);
                var fora = p.X < 0 || p.Y < 0 || p.X >= _inversores.ActualWidth || p.Y >= _inversores.ActualHeight;
                if (fora && e.Data.GetDataPresent(FormatoDoArrasto)) EsconderAsMarcas();
            }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao sair do arrasto da linha de inversor.", erro); }
        };
        _inversores.PreviewDrop += (_, e) =>
        {
            try { Soltou(e); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao soltar a linha de inversor.", erro); }
        };

        Children.Add(inversores);

        _modelos.SelectionChanged += (_, _) =>
        {
            try { PreencherModelo(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o modelo de inversor escolhido.", erro); }
        };
    }

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

        (_contagem, _stringsUteis) = ContarTudo(Documento.Database);

        MontarInversores();
        MostrarSomaDosLimites();
        MostrarVarredura(AtribuicaoAutomatica.Varredura(Documento.Database).Varredura);
        MostrarAPreTag(PreTagDasStrings.Opcoes(Documento.Database, out _));

        MontarTrafos(_trafoDasEscolhidas, setup, comSemTrafo: true);

        // 14.4: o excesso aparece em vermelho na linha do inversor e no rodapé.
        var excessos = _setup.Inverters
            .Select(i => StringAllocation.ExcessWarning(i, _setup.FindModel(i.Model), _contagem.GetValueOrDefault(i.Id)))
            .OfType<string>().ToList();
        if (problema is null && excessos.Count > 0) Avisar(string.Join("\n", excessos), erro: true);

        if (problema is not null) Avisar(problema, erro: true);
        else if (_modelos.Items.Count == 0) Avisar(Tr.T("Nenhum modelo de inversor ainda: abra Modelos de inversor… e use Novo modelo."));
    }

    /// <summary>As strings de cada inversor (a contagem do vínculo) e quantas strings a usina tem (cada GUID uma vez), numa leitura.</summary>
    internal static (IReadOnlyDictionary<Guid, int> PorInversor, int Total) ContarTudo(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var strings = ElectricalStore.Strings(transacao, database).Select(x => x.String).ToList();
        return (StringAllocation.CountByInverter(strings), strings.Select(s => s.Id).Distinct().Count());
    }

    /// <summary>As caixas da pré-tag como estão gravadas (sem gravar de volta).</summary>
    private void MostrarAPreTag(PreTagOptions opcoes)
    {
        _mostrandoAPreTag = true;
        try
        {
            _preTagNome.IsChecked = opcoes.Insert;
            _preTagMoldura.IsChecked = opcoes.Border;
            _preTagFundo.IsChecked = opcoes.Background;
            _preTagMoldura.IsEnabled = _preTagFundo.IsEnabled = opcoes.Insert;
        }
        finally
        {
            _mostrandoAPreTag = false;
        }
    }

    /// <summary>Marcou ou desmarcou uma caixa da pré-tag: grava na hora (vale no próximo Distribuir).</summary>
    private void MudouAPreTag()
    {
        if (_mostrandoAPreTag || !DesenhoAberto()) return;
        try
        {
            var opcoes = new PreTagOptions(_preTagNome.IsChecked == true, _preTagMoldura.IsChecked == true, _preTagFundo.IsChecked == true);
            _preTagMoldura.IsEnabled = _preTagFundo.IsEnabled = opcoes.Insert;
            var frase = EscritaForaDeComando.Fazer(Documento, () => GravarAPreTag(Documento.Database, opcoes));
            Avisar(frase);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gravar as opções da pré-tag.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>Grava as opções da pré-tag (o caminho da tela, também do nível 2) e diz o que vale. Quem chama trava o documento.</summary>
    internal static string GravarAPreTag(Database database, PreTagOptions opcoes)
    {
        PreTagDasStrings.GravarOpcoes(database, opcoes);
        if (!opcoes.Insert) return Tr.T("Pré-tag desligada: o próximo Distribuir não escreve o nome do inversor nas strings.");
        return (opcoes.Border, opcoes.Background) switch
        {
            (true, true) => Tr.T("Pré-tag com o nome do inversor, moldura e fundo, no próximo Distribuir."),
            (true, false) => Tr.T("Pré-tag com o nome do inversor e moldura, sem fundo, no próximo Distribuir."),
            (false, true) => Tr.T("Pré-tag com o nome do inversor e fundo, sem moldura, no próximo Distribuir."),
            _ => Tr.T("Pré-tag com o nome do inversor, sem moldura nem fundo, no próximo Distribuir."),
        };
    }

    /// <summary>A soma dos limites contra as strings úteis, em vermelho se não bate (item 5).</summary>
    private void MostrarSomaDosLimites()
    {
        var soma = BalancedLimits.Sum(_setup.Inverters, _setup.Models);
        var (texto, bate) = BalancedLimits.Describe(soma, _stringsUteis);
        _somaDosLimites.Text = texto;
        _somaDosLimites.Foreground = bate ? System.Windows.SystemColors.ControlTextBrush : System.Windows.Media.Brushes.Firebrick;
        _somaDosLimites.FontWeight = bate ? FontWeights.Normal : FontWeights.SemiBold;
    }

    /// <summary>
    /// "Modelos de inversor…" (item 7 de 10/10/2026): o cadastro dos modelos
    /// num modal sobre a janela, com o recado dele embaixo. Fechar devolve o
    /// painel para a aba (ele é o mesmo, só muda de dono).
    /// </summary>
    private void AbrirModelos()
    {
        var recado = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        var rolagem = new ScrollViewer { Content = _painelDosModelos, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var corpo = new DockPanel { Margin = new Thickness(10), LastChildFill = true };
        var fechar = new Button { Content = Tr.T("Fechar"), Height = 24, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true };
        DockPanel.SetDock(fechar, Dock.Bottom);
        DockPanel.SetDock(recado, Dock.Bottom);
        corpo.Children.Add(fechar);
        corpo.Children.Add(recado);
        corpo.Children.Add(rolagem);

        var janela = new Window
        {
            Title = Tr.T("Modelos de inversor"),
            Content = corpo,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 720,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
        };
        fechar.Click += (_, _) => janela.Close();

        EcoDoRecado = (texto, erro) =>
        {
            recado.Text = texto;
            recado.Foreground = erro ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.ForestGreen;
        };

        try
        {
            janela.ShowDialog();
        }
        finally
        {
            EcoDoRecado = null;
            rolagem.Content = null;   // o painel fica livre para a próxima vez
        }
    }

    /// <summary>
    /// As áreas de inversores do desenho para a lista do "Áreas…": o nome e
    /// quantos inversores estão nela (pela geometria), em ordem de nome.
    /// </summary>
    internal static List<(Guid Id, string Name, int Inverters)> ListaDeAreas(Database database)
    {
        var porArea = LocalDosInversores.Ler(database, out _).Where(l => l.Mode == InverterPlacementMode.Area).GroupBy(l => l.Site).ToDictionary(g => g.Key, g => g.Count());
        return LocalDosInversores.Areas(database).Values
            .Select(a => (a.Marca.Id, a.Marca.Name, porArea.GetValueOrDefault(a.Marca.Id)))
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>O Renomear da lista de áreas e da coluna Local (o caminho da tela, também do nível 2). Quem chama trava o documento.</summary>
    internal static (string? Frase, string? Problema) RenomearArea(Database database, Guid area, string? nome) =>
        LocalDosInversores.RenomearArea(database, area, nome);

    /// <summary>
    /// "Áreas…" (item 3 da segunda rodada de 10/10/2026: "o sistema deu o nome
    /// de área 1 mas não sei como mudar"): a lista das áreas de inversores,
    /// cada uma com o nome editável, quantos inversores tem e Renomear. Enter
    /// no nome também renomeia.
    /// </summary>
    private void AbrirAreas()
    {
        var lista = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        lista.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        lista.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        lista.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var recado = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

        void Recado(string texto, bool erro)
        {
            recado.Text = texto;
            recado.Foreground = erro ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.ForestGreen;
        }

        void Montar()
        {
            lista.Children.Clear();
            lista.RowDefinitions.Clear();
            var areas = ListaDeAreas(Documento.Database);
            if (areas.Count == 0)
            {
                lista.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                lista.Children.Add(new TextBlock { Text = Tr.T("Nenhuma área de inversores no desenho: escolha os inversores na tabela e use Escolher área…"), TextWrapping = TextWrapping.Wrap });
                return;
            }

            foreach (var (id, nomeAtual, quantos) in areas)
            {
                var linha = lista.RowDefinitions.Count;
                lista.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var nome = new TextBox { Text = nomeAtual, MaxLength = SiteMark.MaxNameLength, Height = 22, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2), ToolTip = Tr.T("O nome da área: digite o novo e clique em Renomear (ou Enter).") };
                var conta = new TextBlock { Text = Tr.F("{0} inversor(es)", quantos), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                var renomear = new Button { Content = Tr.T("Renomear"), Height = 22, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 2, 0, 2), ToolTip = Tr.T("Grava o nome novo na área (vale para todos os inversores dela).") };

                void Renomear()
                {
                    try
                    {
                        if (!DesenhoAberto()) return;
                        var (frase, problema) = EscritaForaDeComando.Fazer(Documento, () => RenomearArea(Documento.Database, id, nome.Text));
                        if (problema is not null)
                        {
                            Recado(Tr.F("Não renomeei a área: {0}.", problema), true);
                            return;
                        }

                        (AoMudar ?? Atualizar)();
                        Montar();
                        if (frase is not null) Recado(frase, false);
                    }
                    catch (Exception erro)
                    {
                        RegistroDeDiagnostico.Registrar("Falha ao renomear a área pela lista de áreas.", erro);
                        Recado(Tr.F("Não consegui: {0}", erro.Message), true);
                    }
                }

                renomear.Click += (_, _) => Renomear();
                nome.KeyDown += (_, e) =>
                {
                    if (e.Key != System.Windows.Input.Key.Enter) return;
                    e.Handled = true;
                    Renomear();
                };

                Grid.SetRow(nome, linha);
                Grid.SetRow(conta, linha);
                Grid.SetColumn(conta, 1);
                Grid.SetRow(renomear, linha);
                Grid.SetColumn(renomear, 2);
                lista.Children.Add(nome);
                lista.Children.Add(conta);
                lista.Children.Add(renomear);
            }
        }

        var fechar = new Button { Content = Tr.T("Fechar"), Height = 24, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true };
        var corpo = new DockPanel { Margin = new Thickness(10), LastChildFill = true };
        DockPanel.SetDock(fechar, Dock.Bottom);
        DockPanel.SetDock(recado, Dock.Bottom);
        corpo.Children.Add(fechar);
        corpo.Children.Add(recado);
        corpo.Children.Add(new ScrollViewer { Content = lista, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var janela = new Window
        {
            Title = Tr.T("Áreas de inversores"),
            Content = corpo,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 520,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
        };
        fechar.Click += (_, _) => janela.Close();

        Montar();
        janela.ShowDialog();
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
    private static readonly string[] Colunas = ["Alca", "Cor", "Nome", "Modelo", "Trafo", "Meta", "Strings", "Kwp", "Kw", "Razao", "Local", "Acoes"];

    private const int ColunaAlca = 0, ColunaCor = 1, ColunaNome = 2, ColunaModelo = 3, ColunaTrafo = 4, ColunaMeta = 5, ColunaStrings = 6, ColunaKwp = 7, ColunaKw = 8, ColunaRazao = 9, ColunaLocal = 10, ColunaAcoes = 11;

    /// <summary>O local de cada inversor que tem um (área ou automático), lido a cada montagem da tabela.</summary>
    private Dictionary<Guid, InverterPlacement> _locais = [];

    /// <summary>As áreas de inversores do desenho (o nome de cada uma), lidas a cada montagem da tabela.</summary>
    private Dictionary<Guid, SiteMark> _areas = [];

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
        Titulo(ColunaModelo, Tr.T("Modelo"), Tr.T("O modelo do inversor: escolher outro grava na hora (o cadastro dos modelos fica em Modelos de inversor…)."));
        Titulo(ColunaTrafo, Tr.T("Trafo"), Tr.T("O trafo do inversor. Escolher na linha grava na hora; é também o skid do trafo."));
        Titulo(ColunaMeta, Tr.T("Limite"), Tr.T("Limite de strings: quantas o Distribuir põe neste inversor (vazio: todas as entradas do modelo). Enter grava; com várias linhas escolhidas, vale para todas."), numero: true);
        Titulo(ColunaStrings, Tr.T("Strings"), Tr.T("Strings alocadas / total de entradas do modelo."), numero: true);
        Titulo(ColunaKwp, "kWp", Tr.T("Potência CC: a soma da potência dos módulos das strings alocadas (a mesma conta do Resumo elétrico)."), numero: true);
        Titulo(ColunaKw, "kW", Tr.T("Potência nominal CA do modelo."), numero: true);
        Titulo(ColunaRazao, Tr.T("CC/CA"), Tr.T("kWp ÷ kW: só com a potência do modelo informada."), numero: true);
        Titulo(ColunaLocal, Tr.T("Local"), Tr.T("Onde o inversor está em campo: o nome da área (dentro do retângulo escolhido; clique para renomear a área), Auto (a rota CC põe ao lado da vala, no ponto de menor cabo) ou vazio (à mão, Pôr em campo)."));
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

    /// <summary>
    /// O local de cada inversor como a coluna Local mostra (o caminho da tela,
    /// também do nível 2), pela geometria (item 2 da segunda rodada de
    /// 10/10/2026: o inversor com o centro dentro de uma área é dela, não
    /// importa como chegou lá; fora, deixa de ser). Só lê: o registro não é
    /// gravado aqui (correção de 10/10/2026, à noite: gravar fora de comando
    /// abria um grupo de UNDO próprio, e o U depois de um MOVE com a janela
    /// aberta desfazia só o registro, nunca o MOVE). A área de cada um, as
    /// áreas e quem está em campo.
    /// </summary>
    internal static (Dictionary<Guid, InverterPlacement> Locais, Dictionary<Guid, SiteMark> Areas, HashSet<(EquipmentKind Kind, Guid Id)> EmCampo, string? Problema) LerOsLocais(Document documento)
    {
        var db = documento.Database;
        var c = LocalDosInversores.Conferir(db);
        var locais = c.Certo.GroupBy(l => l.Inverter).ToDictionary(g => g.Key, g => g.First());
        var areas = c.Areas.ToDictionary(a => a.Key, a => a.Value.Marca);
        return (locais, areas, EquipamentoEmCampo.EmCampo(db), c.Problema);
    }

    /// <summary>
    /// Antes de mostrar a tabela (ao abrir a aba, ao trocar de aba, ao voltar
    /// à janela): todo inversor em campo com a base fora do terreno + 0,80
    /// volta para lá (<see cref="LocalDosInversores.Reassentar"/>). O vigia
    /// (<see cref="ArvoreVigia"/>) já faz isso no fim do MOVE, no mesmo grupo
    /// de UNDO; aqui fica o que ele não viu (desenho antigo, MOVE sem terreno
    /// carregado). Grava só se há o que mudar. Sem terreno carregado, não
    /// mexe e diz por quê (só se há inversor em campo). A frase (e se é aviso), ou null.
    /// </summary>
    internal static (string Texto, bool Erro)? AssentarNoTerreno(Document documento)
    {
        try
        {
            var emCampo = EquipamentoEmCampo.EmCampo(documento.Database).Any(e => e.Kind == EquipmentKind.Inverter);
            if (!emCampo) return null;
            if (TerrainCache.Get(documento) is not { } terreno)
                return (Tr.T("ATENÇÃO: o terreno não está carregado: a cota dos inversores em campo não foi conferida (base = terreno + 0,80 m). Use o botão Terreno."), true);

            var frases = EscritaForaDeComando.Fazer(documento, () => LocalDosInversores.Reassentar(documento.Database, terreno));
            return frases.Count == 0 ? null : (string.Join(" ", frases), false);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui pôr os inversores no terreno + 0,80 ao ler a aba.", erro);
            return null;
        }
    }

    /// <summary>A linha de um inversor: o estado (botão, Ver em campo) e o texto da coluna Local.</summary>
    internal static (InverterSiteView Estado, string Local) EstadoDoLocal(Guid inversor, IReadOnlyDictionary<Guid, InverterPlacement> locais, IReadOnlyDictionary<Guid, SiteMark> areas, bool emCampo)
    {
        var local = locais.GetValueOrDefault(inversor);
        var estado = InverterSiteView.Of(local, emCampo, local is not null && areas.ContainsKey(local.Site));
        return (estado, estado.LocalText(areas.GetValueOrDefault(estado.Site)?.Name));
    }

    /// <summary>A tabela dos inversores: uma linha por inversor e o total.</summary>
    private void MontarInversores()
    {
        var escolhidos = Escolhidos().Select(i => i.Id).ToHashSet();
        var foco = OndeEstaOFoco();
        if (AssentarNoTerreno(Documento) is { } assentados) Avisar(assentados.Texto, assentados.Erro);
        var (locais, areas, emCampo, problemaDoLocal) = LerOsLocais(Documento);
        _locais = locais;
        _areas = areas;
        if (problemaDoLocal is not null) Avisar(Tr.F("ATENÇÃO: o local dos inversores não se lê ({0}); a coluna Local mostra só as áreas pela geometria (os automáticos não aparecem) e nada é gravado.", problemaDoLocal), erro: true);
        var linhas = LinhasDaTabela(Documento, _setup, _contagem);
        _inversores.Items.Clear();
        _marcas.Clear();

        foreach (var linha in linhas)
        {
            var item = new ListBoxItem { Content = MontarLinha(linha, emCampo.Contains((EquipmentKind.Inverter, linha.Inverter.Id))), Tag = linha.Inverter, Padding = new Thickness(2, 1, 2, 1) };
            _inversores.Items.Add(item);
        }

        MontarIntervalo(linhas.Select(l => l.Inverter).ToList());

        // A escolha volta (o que sumiu do cadastro sai dela).
        foreach (var item in _inversores.Items.OfType<ListBoxItem>())
            if (escolhidos.Contains(((Inverter)item.Tag).Id)) _inversores.SelectedItems.Add(item);
        MostrarAsEscolhidas();

        var total = InverterTable.Total(linhas);
        var rodape = LinhaDaTabela();
        rodape.Margin = new Thickness(4, 3, 2, 0);

        // Item 1 da segunda rodada: a soma dos limites na coluna Limite, em vermelho se não bate com as strings úteis.
        var (somaDosLimites, bate) = BalancedLimits.TotalCell(BalancedLimits.Sum(_setup.Inverters, _setup.Models), _stringsUteis);
        var limiteTotal = Celula(somaDosLimites, numero: true);
        limiteTotal.ToolTip = BalancedLimits.Describe(BalancedLimits.Sum(_setup.Inverters, _setup.Models), _stringsUteis).Text;
        if (!bate) limiteTotal.Foreground = System.Windows.Media.Brushes.Firebrick;

        foreach (var t in new[]
        {
            Por(rodape, limiteTotal, ColunaMeta),
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

    /// <summary>O "Apagar os escolhidos": só com alguma linha escolhida, com quantas são.</summary>
    private void MostrarAsEscolhidas()
    {
        var n = _inversores.SelectedItems.Count;
        _apagarAsEscolhidas.IsEnabled = n > 0;
        _apagarAsEscolhidas.Content = n > 1 ? Tr.F("Apagar os {0} escolhidos", n) : Tr.T("Apagar os escolhidos");
    }

    /// <summary>Uma caixa do "do ... ao ...": os nomes dos inversores da tabela.</summary>
    private static ComboBox Intervalo() => new() { Height = 24, MinWidth = 105, Margin = new Thickness(0, 0, 6, 4) };

    /// <summary>Uma seção do topo: o título e as linhas dela, num quadro.</summary>
    private static Border Secao(string titulo, params UIElement[] linhas)
    {
        var corpo = new StackPanel();
        corpo.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        foreach (var l in linhas) corpo.Children.Add(l);

        return new Border
        {
            Child = corpo,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8, 4, 8, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = System.Windows.SystemColors.ActiveBorderBrush,
        };
    }

    /// <summary>O "Todos" de uma seção: do primeiro ao último.</summary>
    private static void Todos(ComboBox de, ComboBox ate)
    {
        de.SelectedIndex = de.Items.Count > 0 ? 0 : -1;
        ate.SelectedIndex = ate.Items.Count - 1;
    }

    /// <summary>Os nomes da tabela nas caixas do "do ... ao ...", mantendo a escolha (a primeira vez: todos).</summary>
    private void MontarIntervalo(IReadOnlyList<Inverter> inversores)
    {
        foreach (var (de, ate) in new[] { (_trafoDo, _trafoAo), (_limiteDo, _limiteAo) })
        {
            var (antesDe, antesAte) = (SelecionadoId(de), SelecionadoId(ate));
            foreach (var caixa in new[] { de, ate })
            {
                caixa.Items.Clear();
                foreach (var i in inversores) caixa.Items.Add(new ComboBoxItem { Content = i.Name, Tag = i.Id });
            }

            var posDe = inversores.ToList().FindIndex(i => i.Id == antesDe);
            var posAte = inversores.ToList().FindIndex(i => i.Id == antesAte);
            de.SelectedIndex = posDe >= 0 ? posDe : (inversores.Count > 0 ? 0 : -1);
            ate.SelectedIndex = posAte >= 0 ? posAte : inversores.Count - 1;
        }
    }

    private static Guid? SelecionadoId(ComboBox caixa) => (caixa.SelectedItem as ComboBoxItem)?.Tag as Guid?;

    /// <summary>Os inversores do "do ... ao ..." (na ordem da tabela), ou null com o aviso.</summary>
    private List<Guid>? DoIntervalo(ComboBox de, ComboBox ate)
    {
        if (de.SelectedIndex < 0 || ate.SelectedIndex < 0)
        {
            Avisar(Tr.T("Escolha o primeiro e o último inversor (do ... ao ...)."), erro: true);
            return null;
        }

        var (a, b) = (Math.Min(de.SelectedIndex, ate.SelectedIndex), Math.Max(de.SelectedIndex, ate.SelectedIndex));
        return de.Items.OfType<ComboBoxItem>().Skip(a).Take(b - a + 1).Select(i => (Guid)i.Tag).ToList();
    }

    /// <summary>
    /// Uma linha: a cor (clique abre a paleta), o nome (editável na própria
    /// célula), o modelo e o trafo (caixas que gravam ao escolher), strings,
    /// kWp, kW, CC/CA e as ações (+ Strings, Ver, Soltar, Pôr em campo ou
    /// Mover, Apagar). À esquerda, a alça "⠿" de arrastar a linha.
    /// </summary>
    private Grid MontarLinha(InverterTableRow linha, bool emCampo)
    {
        var inversor = linha.Inverter;
        var g = LinhaDaTabela();

        Por(g, Alca(inversor.Id), ColunaAlca);
        Por(g, CaixaDaCor(inversor), ColunaCor);

        // A marca de onde a linha arrastada cai: um traço na borda de cima ou de baixo, por cima de tudo.
        var marca = new Border
        {
            Height = 2,
            Background = System.Windows.SystemColors.HighlightBrush,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumnSpan(marca, Colunas.Length + 1);
        Panel.SetZIndex(marca, 1);
        g.Children.Add(marca);
        _marcas[inversor.Id] = marca;

        var aviso = StringAllocation.ExcessWarning(inversor, linha.Model, linha.Strings);
        var nome = Por(g, CaixaDoNome(inversor, aviso, emCampo), ColunaNome);
        Por(g, CaixaDoModelo(inversor), ColunaModelo);
        Por(g, CaixaDoTrafo(inversor), ColunaTrafo);
        Por(g, CaixaDaMeta(inversor, linha.Capacity), ColunaMeta);

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
        // A coluna Local e o botão de campo saem da mesma conta (item 4 de 10/10/2026).
        // Item 4 da segunda rodada: posto à mão em campo, fora de qualquer área, diz "À mão".
        var (estado, textoDoLocal) = EstadoDoLocal(inversor.Id, _locais, _areas, emCampo);
        Por<UIElement>(g, estado.Mode == InverterPlacementMode.Area && _areas.TryGetValue(estado.Site, out var area) ? CaixaDaArea(area) : Celula(textoDoLocal), ColunaLocal);

        var acoes = Por(g, new StackPanel { Orientation = Orientation.Horizontal }, ColunaAcoes);
        var id = inversor.Id;

        // 10/10/2026 (Renan: "tem o botão três pontinhos, mas tem espaço para
        // mais colunas"): as ações do antigo "⋯" à vista, do mesmo tamanho.
        var botoes = new List<Button>
        {
            Botao(acoes, Tr.T("+ Strings"), Tr.T("Pôr strings neste inversor: a janela some; clique nas strings no desenho (Shift+clique tira) e Enter volta."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaAlocar, id.ToString("D"))),
            Botao(acoes, Tr.T("Ver"), Tr.T("Mostra no desenho as strings deste inversor (ficam selecionadas)."),
                () => JanelaEletrica.SelecionarStrings(Documento, id)),
            Botao(acoes, Tr.T("Soltar"), Tr.T("Solta as strings deste inversor: ficam livres e continuam no desenho; as tags de numeração delas são apagadas."),
                () => SoltarTodas(inversor)),
        };

        if (estado.Button == InverterFieldButton.Automatic)
        {
            // Item 19: o inversor automático fora de campo é posto pela rota CC; aqui só o aviso, sem clique.
            acoes.Children.Add(new TextBlock
            {
                Text = Tr.T("Alocação automática"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.SystemColors.GrayTextBrush,
                Margin = new Thickness(2, 0, 8, 0),
                ToolTip = Tr.T("O Gerar da rota CC põe este inversor ao lado da vala, no ponto de menor cabo CC das strings dele. Para pôr à mão, escolha a linha e À mão."),
            });
        }
        else
        {
            // O automático já posto pela rota também se move (item 19): pelo mesmo
            // comando do Pôr em campo, que assenta a base no terreno + 0,80.
            botoes.Add(Botao(acoes, estado.Button == InverterFieldButton.Move ? Tr.T("Mover") : Tr.T("Pôr em campo"),
                estado.Mode == InverterPlacementMode.Automatic
                    ? Tr.T("A rota CC pôs este inversor ao lado da vala: a janela some e você clica o novo centro dele. Depois, Recalcular rota (ou Gerar) refaz os cabos dele da posição nova.")
                    : estado.Button == InverterFieldButton.Move
                        ? Tr.T("O retângulo do inversor já está no desenho: a janela some e você clica o novo centro dele. O vínculo não muda.")
                        : Tr.T("Põe o retângulo do inversor no desenho: a janela some e você clica o centro dele."),
                () => AlocarEmCampo(id)));
        }

        if (estado.CanSee)
            botoes.Add(Botao(acoes, Tr.T("Ver em campo"), Tr.T("A janela some e o retângulo do inversor fica selecionado no desenho, com zoom nele. Esc volta à janela."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaVer, id.ToString("D"))));

        botoes.Add(Botao(acoes, Tr.T("Apagar"), Tr.T("Tira o inversor do cadastro (pede confirmação): as strings dele ficam livres, as tags delas saem e o retângulo sai do campo."),
            () => Apagar([inversor])));

        foreach (var b in botoes)
        {
            b.Height = 22;
            b.Padding = new Thickness(6, 0, 6, 0);
            b.Margin = new Thickness(0, 1, 4, 1);
        }

        return g;
    }

    /// <summary>
    /// A alça "⠿" da linha: apertar e arrastar leva o inversor para outro
    /// lugar da lista (a caixa do nome e as outras continuam como eram). A
    /// alça prende o mouse para o arrasto começar mesmo saindo dela.
    /// </summary>
    private TextBlock Alca(Guid inversor)
    {
        var alca = new TextBlock
        {
            Text = "⠿",
            FontSize = 14,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.SizeNS,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = Tr.T("Arraste para mudar a ordem: a linha cai no lugar da linha onde você soltar."),
        };

        Point? inicio = null;
        alca.PreviewMouseLeftButtonDown += (_, e) =>
        {
            try
            {
                inicio = e.GetPosition(alca);
                alca.CaptureMouse();
                e.Handled = true;   // a lista não começa a escolher linhas arrastando
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao pegar a alça da linha de inversor.", erro);
            }
        };
        alca.PreviewMouseLeftButtonUp += (_, _) =>
        {
            try
            {
                inicio = null;
                if (alca.IsMouseCaptured) alca.ReleaseMouseCapture();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao largar a alça da linha de inversor.", erro);
            }
        };
        // A captura perdida (Alt+Tab, uma janela por cima) não deixa arrasto pendurado.
        alca.LostMouseCapture += (_, _) => inicio = null;
        alca.PreviewMouseMove += (_, e) =>
        {
            try
            {
                if (inicio is not { } de || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
                var agora = e.GetPosition(alca);
                if (Math.Abs(agora.Y - de.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(agora.X - de.X) < SystemParameters.MinimumHorizontalDragDistance) return;

                inicio = null;
                alca.ReleaseMouseCapture();
                try
                {
                    // O GUID como texto: um destino de fora (a área de desenho) não precisa serializar objeto.
                    DragDrop.DoDragDrop(alca, new DataObject(FormatoDoArrasto, inversor.ToString("D")), DragDropEffects.Move);
                }
                finally
                {
                    EsconderAsMarcas();
                }
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao arrastar a linha de inversor.", erro);
            }
        };

        return alca;
    }

    /// <summary>O inversor arrastado e a linha onde o mouse está (null se não é arrasto de linha ou está fora delas).</summary>
    private (Guid Arrastado, ListBoxItem Alvo)? AlvoDoArrasto(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoDoArrasto) || e.Data.GetData(FormatoDoArrasto) is not string texto || !Guid.TryParse(texto, out var arrastado)) return null;
        if (e.OriginalSource is not DependencyObject sob || ItemsControl.ContainerFromElement(_inversores, sob) is not ListBoxItem { Tag: Inverter } alvo) return null;
        return (arrastado, alvo);
    }

    /// <summary>
    /// O arrasto passa por cima da tabela: a marca mostra onde a linha cai
    /// (descendo, embaixo da linha de baixo do mouse; subindo, em cima dela);
    /// perto da borda a lista rola.
    /// </summary>
    private void ArrastandoPorCima(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoDoArrasto)) return;
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        Rolar(e.GetPosition(_inversores).Y);

        EsconderAsMarcas();
        if (AlvoDoArrasto(e) is not { } a) return;

        var de = PosicaoNaTabela(a.Arrastado);
        var para = _inversores.ItemContainerGenerator.IndexFromContainer(a.Alvo);
        if (de < 0 || para < 0 || de == para || !_marcas.TryGetValue(((Inverter)a.Alvo.Tag).Id, out var marca)) return;

        marca.VerticalAlignment = de < para ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        marca.Visibility = Visibility.Visible;
        e.Effects = DragDropEffects.Move;
    }

    /// <summary>Soltou a linha: o inversor vai para o lugar da linha de baixo do mouse (depois do evento: gravar refaz a lista).</summary>
    private void Soltou(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FormatoDoArrasto)) return;
        e.Handled = true;
        EsconderAsMarcas();
        if (AlvoDoArrasto(e) is not { } a) return;

        var alvo = ((Inverter)a.Alvo.Tag).Id;
        if (alvo == a.Arrastado) return;

        Dispatcher.BeginInvoke(() =>
        {
            try { if (DesenhoAberto()) MoverPelaTela(a.Arrastado, alvo); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mover o inversor na lista.", erro); }
        });
    }

    private int PosicaoNaTabela(Guid inversor) =>
        _inversores.Items.OfType<ListBoxItem>().ToList().FindIndex(i => ((Inverter)i.Tag).Id == inversor);

    private void EsconderAsMarcas()
    {
        foreach (var m in _marcas.Values) m.Visibility = Visibility.Collapsed;
    }

    /// <summary>Arrastando perto da borda de cima ou de baixo da tabela, a lista rola uma linha.</summary>
    private void Rolar(double y)
    {
        const double Borda = 18;
        if (AchaORolador(_inversores) is not { } rolador) return;
        if (y < Borda) rolador.LineUp();
        else if (y > _inversores.ActualHeight - Borda) rolador.LineDown();
    }

    private static ScrollViewer? AchaORolador(DependencyObject de)
    {
        for (var k = 0; k < System.Windows.Media.VisualTreeHelper.GetChildrenCount(de); k++)
        {
            var filho = System.Windows.Media.VisualTreeHelper.GetChild(de, k);
            if (filho is ScrollViewer rolador) return rolador;
            if (AchaORolador(filho) is { } achado) return achado;
        }

        return null;
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
    /// O nome da área na coluna Local (item 6 de 10/10/2026): parece texto;
    /// clicar edita, Enter ou sair grava (renomeia a área, para todos os
    /// inversores dela), Esc desfaz.
    /// </summary>
    private TextBox CaixaDaArea(SiteMark area)
    {
        var caixa = new TextBox
        {
            Text = area.Name,
            MinWidth = 50,
            MaxWidth = 140,
            Height = 22,
            MaxLength = SiteMark.MaxNameLength,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = Tr.T("A área (o retângulo) onde o inversor está. Clique para renomear a área: Enter (ou sair da caixa) grava, Esc desfaz. Também pelo botão Áreas… do quadro Local dos inversores."),

            // Item 3 da segunda rodada: o Renan não achou como renomear. Cara de link: cor e mãozinha.
            Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = System.Windows.SystemColors.HotTrackBrush,
        };

        var enviado = false;
        void Confirmar()
        {
            if (enviado || caixa.Text.Trim() == area.Name) return;
            enviado = true;
            var texto = caixa.Text;
            Dispatcher.BeginInvoke(() =>
            {
                try { if (DesenhoAberto()) GravarNaLinha(() => RenomearArea(Documento.Database, area.Id, texto), p => Tr.F("Não renomeei a área: {0}.", p)); }
                catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao renomear a área dos inversores.", erro); }
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
                RegistroDeDiagnostico.Registrar("Falha ao sair do nome da área.", erro);
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
                    caixa.Text = area.Name;
                    caixa.SelectAll();
                }
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha numa tecla do nome da área.", erro);
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

    /// <summary>O "Aplicar" da seção Trafo: os inversores do ... ao ... vão para o trafo escolhido.</summary>
    private void PorOIntervaloNoTrafo()
    {
        if (DoIntervalo(_trafoDo, _trafoAo) is not { } ids) return;

        if ((_trafoDasEscolhidas.SelectedItem as ComboBoxItem)?.Tag is not Guid trafo)
        {
            Avisar(Tr.T("Escolha o trafo (cadastre na aba Transformador, se não há)."), erro: true);
            return;
        }

        PorNoTrafoPelaTela(ids, trafo);
    }

    /// <summary>
    /// A meta da linha (07/10/2026): quantas strings o Distribuir põe nele.
    /// Enter ou sair da caixa grava (vazio: todas as entradas); Esc desfaz.
    /// Linha que faz parte de uma escolha de várias: vale para todas.
    /// </summary>
    private TextBox CaixaDaMeta(Inverter inversor, int entradas)
    {
        var gravado = inversor.Target?.ToString(Tr.Culture) ?? string.Empty;
        var caixa = new TextBox
        {
            Text = gravado,
            Width = 40,
            Height = 22,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = Tr.F("Limite de strings: quantas o Distribuir põe neste inversor (vazio: todas as {0} entradas). Enter grava, Esc desfaz.", entradas),
        };

        var id = inversor.Id;
        var enviado = false;

        void Confirmar()
        {
            if (enviado || caixa.Text.Trim() == gravado) return;
            enviado = true;
            var texto = caixa.Text;
            var escolhidos = Escolhidos().Select(i => i.Id).ToList();
            List<Guid> alvos = escolhidos.Count >= 2 && escolhidos.Contains(id) ? escolhidos : [id];

            // Fora do evento: gravar relê e refaz a tabela (e esta caixa).
            Dispatcher.BeginInvoke(() =>
            {
                try { if (DesenhoAberto()) PorAMetaPelaTela(alvos, texto); }
                catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao gravar a meta do inversor.", erro); }
            });
        }

        caixa.LostKeyboardFocus += (_, _) =>
        {
            try { Confirmar(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao sair da meta do inversor.", erro); }
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
                }
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha na tecla da meta do inversor.", erro);
            }
        };
        return caixa;
    }

    /// <summary>O "Aplicar" do limite: os inversores do ... ao ... ficam com o limite escrito.</summary>
    private void PorOLimiteNoIntervalo()
    {
        if (DoIntervalo(_limiteDo, _limiteAo) is { } ids) PorAMetaPelaTela(ids, _limiteDoLote.Text);
    }

    /// <summary>O "Repartir pelo kW": o limite de cada inversor do ... ao ... pela conta homogênea.</summary>
    private void RepartirPeloKw()
    {
        if (DoIntervalo(_limiteDo, _limiteAo) is { } ids)
            GravarNaLinha(() => Repartir(Documento.Database, ids), p => Tr.F("Não mudei: {0}.", p));
    }

    /// <summary>
    /// "Repartir pelo kW" (item 5 de 10/10/2026): simula o Distribuir (a fila
    /// das strings pela varredura gravada, os inversores na ordem da tabela)
    /// e dá a cada inversor o limite que deixa a potência das strings dele na
    /// proporção do kW dele (<see cref="BalancedLimits.SplitInOrder"/>), sem
    /// passar das entradas. Entram as strings livres e as dos inversores do
    /// trecho; as presas a inversores de fora ficam fora da conta. A potência
    /// de cada string é a soma dos módulos dela pela mesa dona (a do Resumo);
    /// módulo sem potência conhecida vale a média dos outros. Grava os limites
    /// e diz o resultado. Quem chama trava o documento.
    /// </summary>
    internal static (string? Frase, string? Problema) Repartir(Database database, IReadOnlyCollection<Guid> inversores)
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        if (problema is not null) return (null, problema);
        var (varredura, _) = AtribuicaoAutomatica.Varredura(database);

        var alvo = inversores.ToHashSet();
        List<ElectricalString> disponiveis;
        Dictionary<Guid, ModuleSpot> lugares;
        var potencia = new Dictionary<Guid, double>();

        // Com a potência trocada pela área (item 14), ela vale para todos os módulos, como no Resumo.
        var simulada = FonteDoModulo.Simulada(database)?.Watts;
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var todas = ElectricalStore.Strings(transacao, database).Select(x => x.String).ToList();
            var repetidas = todas.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            disponiveis = todas.Where(s => !repetidas.Contains(s.Id) && !(s.IsAllocated && !alvo.Contains(s.Inverter) && setup.FindInverter(s.Inverter) is not null)).ToList();

            var modulos = NumeracaoDesenho.Modulos(transacao, database);
            lugares = modulos.ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));

            var daMesa = new Dictionary<Guid, double?>();
            foreach (var (mesa, pecas) in LayoutScan.Tables(transacao, database)) daMesa[mesa] = pecas.Identity?.ModulePowerWatts;
            foreach (var (modulo, lugar) in modulos)
                if ((simulada ?? daMesa.GetValueOrDefault(lugar.Mesa)) is { } w && w > 0) potencia[modulo] = w;
        }

        var fila = StringAutoAllocation.Queue(disponiveis, lugares, varredura);
        if (fila.Count == 0) return (null, Tr.T("não há strings no desenho para repartir"));

        // Módulo sem potência conhecida: a média dos outros (nenhum conhecido: todos iguais, a conta vira por quantidade).
        var media = potencia.Count > 0 ? potencia.Values.Average() : 1;
        var porId = disponiveis.ToDictionary(s => s.Id);
        var kwp = fila.Select(id => porId[id].Modules.Sum(m => potencia.TryGetValue(m, out var w) ? w : media) / 1000).ToList();

        var partes = setup.Inverters.Where(i => alvo.Contains(i.Id))
            .Select(i => setup.FindModel(i.Model) is { } m ? new LimitShare(i.Id, m.PowerKw, m.TotalInputs) : new LimitShare(i.Id, 0, 0))
            .ToList();
        var r = BalancedLimits.SplitInOrder(partes, kwp);

        string? recusa = null;
        ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            foreach (var grupo in r.Limits.Where(x => x.Value > 0).GroupBy(x => x.Value))
            {
                recusa ??= s.SetTarget(grupo.Select(x => x.Key).ToList(), grupo.Key).Problem;
                if (recusa is not null) return false;
            }

            return true;
        });
        if (recusa is not null) return (null, recusa);

        // O kWp de cada inversor na simulação: os trechos seguidos da fila.
        var porInversor = new List<double>();
        var k = 0;
        foreach (var p in partes)
        {
            var n = r.Limits.GetValueOrDefault(p.Inverter);
            if (n > 0) porInversor.Add(kwp.Skip(k).Take(n).Sum());
            k += n;
        }

        var resumo = string.Join(", ", r.Limits.Values.Where(v => v > 0).GroupBy(v => v).OrderByDescending(g => g.Key).Select(g => Tr.F("{0} com {1}", g.Count(), g.Key)));
        var frase = Tr.F("{0} string(s) repartidas em {1} inversor(es) na ordem do Distribuir ({2}): {3}.", fila.Count - r.Leftover, partes.Count, varredura.Describe(), resumo);
        if (porInversor.Count > 0)
            frase += " " + Tr.F("kWp por inversor de {0} a {1}.", porInversor.Min().ToString("#,0.00", Tr.Culture), porInversor.Max().ToString("#,0.00", Tr.Culture));
        frase += " " + Tr.T("Agora Soltar todas da usina e Distribuir.");
        if (r.Leftover > 0) frase += " " + Tr.F("ATENÇÃO: {0} string(s) não cabem nas entradas desses inversores.", r.Leftover);
        var semPosicao = disponiveis.Count - fila.Count;
        if (semPosicao > 0) frase += " " + Tr.F("ATENÇÃO: {0} string(s) sem o primeiro módulo no desenho ficam fora da conta (o Distribuir também as pula).", semPosicao);
        return (frase, null);
    }

    private void PorAMetaPelaTela(IReadOnlyCollection<Guid> inversores, string texto) =>
        GravarNaLinha(() => PorAMeta(Documento.Database, inversores, texto), p => Tr.F("Não mudei: {0}.", p));

    /// <summary>
    /// Grava a meta dos inversores (<see cref="ElectricalSetup.SetTarget"/>) e
    /// diz o que mudou; o problema, se nada mudou. Quem chama trava o documento.
    /// </summary>
    internal static (string? Frase, string? Problema) PorAMeta(Database database, IReadOnlyCollection<Guid> inversores, string texto)
    {
        var (meta, invalida) = InverterTable.ParseTarget(texto);
        if (invalida is not null) return (null, invalida);

        (int Changed, string? Problem) r = default;
        ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            r = s.SetTarget(inversores, meta);
            return r.Problem is null;
        });

        if (r.Problem is { } problema) return (null, problema);
        return (meta is { } m
            ? Tr.F("Limite de {0} string(s) em {1} inversor(es); o Distribuir para nele.", m, r.Changed)
            : Tr.F("{0} inversor(es) sem limite: o Distribuir enche todas as entradas.", r.Changed), null);
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
        if (porque is null)
        {
            EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Inverter, inversor);
            PreTagDasStrings.Atualizar(database, soAsQueJaTem: true);   // "I3" segue o nome
        }

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
    /// Apaga os inversores (um, pelo Apagar da linha; as linhas escolhidas;
    /// ou todos): primeiro o cadastro (se o registro não pode ser gravado,
    /// nada muda), depois o vínculo das strings (e as tags delas, como no
    /// Soltar) e os retângulos em campo (se falhar, a string fica apontando
    /// para um inversor que não existe, e essa não trava: pode ser alocada de
    /// novo). Os apagados, quantas strings ficaram livres e quantas tags saíram.
    /// </summary>
    internal static (IReadOnlyList<Inverter> Apagados, int Soltas, int Tags) ApagarInversores(Database database, IReadOnlyCollection<Guid> inversores)
    {
        var apagados = ConfiguracaoEletricaStore.Mudar(database, s => s.RemoveInverters(inversores));
        if (apagados.Count == 0) return (apagados, 0, 0);

        // Todos de uma vez (uma leitura do desenho), não um por inversor (revisão de 10/10/2026).
        var (soltas, tags) = StringsDoDesenho.Soltar(database, apagados.Select(i => i.Id).ToList());
        foreach (var i in apagados) EquipamentoEmCampo.Apagar(database, EquipmentKind.Inverter, i.Id);

        return (apagados, soltas, tags);
    }

    private void ApagarAsEscolhidas() => Apagar(Escolhidos());

    /// <summary>"Escolher área…": a janela some e o comando pede o retângulo; os escolhidos vão para dentro dele.</summary>
    private void EscolherArea()
    {
        var escolhidos = Escolhidos();
        if (escolhidos.Count == 0)
        {
            Avisar(Tr.T("Escolha antes as linhas dos inversores (clique, Ctrl ou Shift + clique)."), erro: true);
            return;
        }

        JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaLocal, "Area " + string.Join(";", escolhidos.Select(i => i.Id.ToString("D"))));
    }

    /// <summary>
    /// Automático pelas strings ou à mão, para os escolhidos (grava na hora).
    /// Automático apaga o retângulo de quem já estava em campo (item 17 de
    /// 10/10/2026): a rota CC é quem põe.
    /// </summary>
    private void MudarOLocal(InverterPlacementMode? modo)
    {
        var escolhidos = Escolhidos();
        if (escolhidos.Count == 0)
        {
            Avisar(Tr.T("Escolha antes as linhas dos inversores (clique, Ctrl ou Shift + clique)."), erro: true);
            return;
        }

        GravarNaLinha(() => MudarOLocal(Documento.Database, escolhidos.Select(i => i.Id).ToList(), modo), p => Tr.F("Não mudei o local: {0}.", p));
        RedesenharODesenho();
    }

    /// <summary>O "Automático pelas strings" e o "À mão" da aba (o caminho da tela, também do nível 2). Quem chama trava o documento.</summary>
    internal static (string? Frase, string? Problema) MudarOLocal(Database database, IReadOnlyCollection<Guid> inversores, InverterPlacementMode? modo) =>
        modo == InverterPlacementMode.Automatic
            ? LocalDosInversores.TornarAutomaticos(database, inversores)
            : LocalDosInversores.TornarManuais(database, inversores);

    /// <summary>O "Apagar todos" (10/10/2026: "quero ter a opção de apagar TODOS os inversores da usina").</summary>
    private void ApagarTodos()
    {
        if (_setup.Inverters.Count == 0)
        {
            Avisar(Tr.T("A usina não tem inversor para apagar."), erro: true);
            return;
        }

        Apagar(_setup.Inverters, todos: true);
    }

    /// <summary>Apaga os inversores depois da confirmação (o nome de cada um na pergunta; todos: quantos são).</summary>
    private void Apagar(IReadOnlyList<Inverter> inversores, bool todos = false)
    {
        if (inversores.Count == 0)
        {
            Avisar(Tr.T("Escolha uma ou mais linhas na tabela (Ctrl ou Shift + clique para várias)."), erro: true);
            return;
        }

        var nomes = string.Join(", ", inversores.Take(8).Select(i => i.Name)) + (inversores.Count > 8 ? ", …" : string.Empty);
        var pergunta = todos
            ? Tr.F("Apagar TODOS os {0} inversores da usina? As strings deles ficam livres (continuam no desenho), as tags delas saem e os retângulos saem do campo.", inversores.Count)
            : inversores.Count == 1
                ? Tr.F("Apagar o {0}? As strings dele ficam livres (continuam no desenho), as tags delas saem e o retângulo sai do campo.", nomes)
                : Tr.F("Apagar {0} inversores ({1})? As strings deles ficam livres (continuam no desenho), as tags delas saem e os retângulos saem do campo.", inversores.Count, nomes);
        if (MessageBox.Show(Window.GetWindow(this), pergunta, Tr.T("Apagar inversor"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        var ids = inversores.Select(i => i.Id).ToList();
        var nenhum = false;
        Fazer(() =>
        {
            var (apagados, soltas, tags) = ApagarInversores(Documento.Database, ids);
            nenhum = apagados.Count == 0;
            if (nenhum) return null;
            return apagados.Count == 1
                ? Tr.F("{0} apagado do cadastro; {1} string(s) ficaram livres no desenho; {2} tag(s) apagada(s).", apagados[0].Name, soltas, tags)
                : Tr.F("{0} inversores apagados do cadastro; {1} string(s) ficaram livres no desenho; {2} tag(s) apagada(s).", apagados.Count, soltas, tags);
        });
        if (nenhum) Avisar(Tr.T("Esse inversor não está mais no cadastro."), erro: true);
        else RedesenharODesenho();
    }

    private void MoverPelaTela(Guid inversor, Guid alvo) =>
        GravarNaLinha(() => Mover(Documento.Database, inversor, alvo), p => Tr.F("Não mudei a ordem: {0}.", p));

    /// <summary>
    /// Arrastar a linha (10/10/2026): o inversor vai para o lugar do alvo na
    /// lista do cadastro (<see cref="ElectricalSetup.MoveInverter"/>). A
    /// frase, ou o porquê de nada mudar. Quem chama trava o documento.
    /// </summary>
    internal static (string? Frase, string? Problema) Mover(Database database, Guid inversor, Guid alvo)
    {
        var (nome, posicao) = (string.Empty, 0);
        var mudou = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            if (!s.MoveInverter(inversor, alvo)) return false;
            nome = s.FindInverter(inversor)!.Name;
            posicao = s.Inverters.ToList().FindIndex(i => i.Id == inversor) + 1;
            return true;
        });

        return mudou
            ? (Tr.F("{0} agora é o {1}º da lista (a ordem do Distribuir e do número do inversor na tag; gere as tags de novo).", nome, posicao), null)
            : (null, Tr.T("esse inversor não está mais no cadastro"));
    }

    private void OrdenarPelaTela()
    {
        if ((_ordem.SelectedItem as ComboBoxItem)?.Tag is not InverterOrder ordem) return;
        GravarNaLinha(() => Ordenar(Documento.Database, ordem), p => Tr.F("Não mudei a ordem: {0}.", p));
    }

    /// <summary>
    /// O "Ordenar" (10/10/2026): a lista do cadastro pelo nome, pelo trafo ou
    /// pelo trafo e nome (<see cref="ElectricalSetup.SortInverters"/>). A
    /// frase com quantos mudaram de lugar. Quem chama trava o documento.
    /// </summary>
    internal static (string? Frase, string? Problema) Ordenar(Database database, InverterOrder ordem)
    {
        var mudaram = ConfiguracaoEletricaStore.Mudar(database, s => s.SortInverters(ordem));
        return (mudaram == 0
            ? Tr.T("A lista já estava nessa ordem.")
            : Tr.F("Lista ordenada: {0} inversor(es) mudaram de lugar (a ordem do Distribuir e do número do inversor na tag; gere as tags de novo).", mudaram), null);
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
            Avisar(Tr.T("Nenhum inversor em campo: ponha os inversores no desenho antes (Pôr em campo, na linha de cada um)."), erro: true);
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
            PreTagDasStrings.Atualizar(Documento.Database, soAsQueJaTem: true);
            return Tr.F("{0}: cor trocada para {1}; {2} string(s) repintada(s).", inversor.Name, cor.ToHex(), n);
        });
        RedesenharODesenho();
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
        RedesenharODesenho();
        (AoMudar ?? Atualizar)();
        Avisar(string.Join("\n", linhas), erro: r.Changed.Count == 0 || r.Leftover > 0 || r.WithoutModel.Count > 0 || r.Unplaced > 0 || r.Duplicates > 0);
    }

    private void SoltarTodasDaUsina()
    {
        Fazer(() =>
        {
            var (soltas, tags) = StringsDoDesenho.SoltarTodasDaUsina(Documento.Database);
            return Tr.F("{0} string(s) soltas de todos os inversores; continuam no desenho, livres, na cor da camada; {1} tag(s) apagada(s).", soltas, tags);
        });
        RedesenharODesenho();
    }

    private void SoltarTodas(Inverter inversor)
    {
        Fazer(() =>
        {
            var (soltas, tags) = StringsDoDesenho.Soltar(Documento.Database, inversor.Id);
            return Tr.F("{0}: {1} string(s) soltas; continuam no desenho, livres, na cor da camada; {2} tag(s) apagada(s).", inversor.Name, soltas, tags);
        });
        RedesenharODesenho();
    }

    private void CriarInversores()
    {
        if ((_modeloParaCriar.SelectedItem as ComboBoxItem)?.Tag is not Guid modelo)
        {
            Avisar(Tr.T("Escolha o modelo (cadastre um em Modelos de inversor…, se não há)."), erro: true);
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
