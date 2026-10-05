using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Inversor (etapa 14): à esquerda os modelos de inversor (os MPPTs
/// com as entradas de cada um, o total somado, a dimensão); à direita os
/// inversores da usina.
/// </summary>
internal sealed class AbaInversor : AbaEletrica
{
    private readonly ListBox _modelos = new() { MinHeight = 110 };
    private readonly TextBox _nomeDoModelo, _mppt, _largura, _comprimento, _altura;

    /// <summary>As entradas de cada MPPT: uma caixa por MPPT, na ordem (cresce e encolhe com o número de MPPTs).</summary>
    private readonly WrapPanel _entradas = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly List<TextBox> _caixasDasEntradas = [];
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), FontWeight = FontWeights.SemiBold };

    private readonly ComboBox _modeloParaCriar = new() { Height = 26, MinWidth = 160, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _quantos = new() { Text = "1", Width = 50, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ListBox _inversores = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
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
        var grade = Grade(140);
        _nomeDoModelo = Campo(grade, Tr.T("Nome do modelo"), Tr.T("Genérico do cliente ou cadastrado (ex. Huawei 250)."));
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
        Botao(atribuir, Tr.T("Atribuir strings"), Tr.T("As strings livres, na ordem desta varredura, enchem os inversores na ordem da lista, cada um até o total de entradas. As já alocadas não mudam (e contam); inversor cheio é pulado; as que sobrarem são avisadas."), AtribuirStrings);
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

        // O inversor escolhido na lista: editar e apagar (14.5).
        var editar = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        editar.Children.Add(new TextBlock { Text = Tr.T("Escolhido:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        editar.Children.Add(_nomeDoInversor);
        editar.Children.Add(_modeloDoInversor);
        editar.Children.Add(_lugarDaCor);
        Botao(editar, Tr.T("Salvar inversor"), Tr.T("Grava o nome e o modelo do inversor escolhido."), SalvarInversor);
        Botao(editar, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda."), () =>
        {
            if (InversorEscolhido is { } i) AlocarEmCampo(i.Id);
            else Avisar(Tr.T("Escolha um inversor na lista."), erro: true);
        });
        Botao(editar, Tr.T("Apagar inversor"), Tr.T("Tira o inversor do cadastro: as strings dele ficam livres (continuam no desenho) e o retângulo dele sai do campo."), ApagarInversor);

        // O skid (14.7): trafo + inversores escolhidos em campo.
        var skid = new WrapPanel();
        skid.Children.Add(new TextBlock { Text = Tr.T("Skid: trafo"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        skid.Children.Add(_trafoDoSkid);
        skid.Children.Add(new TextBlock { Text = Tr.T("nome"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        skid.Children.Add(_nomeDoSkid);
        Botao(skid, Tr.T("Agrupar em campo"), Tr.T("A janela some: selecione em campo só os retângulos dos inversores do skid (Shift+clique tira), Enter volta. Inversor de outro skid fica travado."), AgruparEmCampo);
        Botao(skid, Tr.T("Tirar do skid"), Tr.T("Tira o inversor escolhido do skid dele (o inversor fica, sem trafo)."), TirarDoSkid);
        _trafoDoSkid.SelectionChanged += (_, _) =>
        {
            try { _nomeDoSkid.Text = (_trafoDoSkid.SelectedItem as ComboBoxItem)?.Tag is Guid t ? _setup.FindSkid(t)?.Name ?? string.Empty : string.Empty; }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o skid do trafo.", erro); }
        };

        var rodape = new StackPanel();
        rodape.Children.Add(editar);
        rodape.Children.Add(skid);

        var inversores = new DockPanel();
        var topo = new StackPanel();
        topo.Children.Add(Titulo(Tr.T("Inversores da usina")));
        topo.Children.Add(criar);
        topo.Children.Add(atribuir);
        DockPanel.SetDock(topo, Dock.Top);
        DockPanel.SetDock(rodape, Dock.Bottom);
        inversores.Children.Add(topo);
        inversores.Children.Add(rodape);
        inversores.Children.Add(_inversores);

        _inversores.SelectionChanged += (_, _) =>
        {
            try { PreencherInversor(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o inversor escolhido.", erro); }
        };

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
        MostrarVarredura(AtribuicaoAutomatica.Varredura(Documento.Database).Varredura);

        var trafoDoSkid = (_trafoDoSkid.SelectedItem as ComboBoxItem)?.Tag as Guid?;
        _trafoDoSkid.Items.Clear();
        foreach (var t in setup.Transformers)
        {
            var item = new ComboBoxItem { Content = t.Nickname, Tag = t.Id };
            _trafoDoSkid.Items.Add(item);
            if (t.Id == trafoDoSkid) _trafoDoSkid.SelectedItem = item;
        }

        if (_trafoDoSkid.SelectedItem is null && _trafoDoSkid.Items.Count > 0) _trafoDoSkid.SelectedIndex = 0;

        // 14.4: o excesso aparece em vermelho na linha do inversor e no rodapé.
        var excessos = _setup.Inverters
            .Select(i => StringAllocation.ExcessWarning(i, _setup.FindModel(i.Model), _contagem.GetValueOrDefault(i.Id)))
            .OfType<string>().ToList();
        if (problema is null && excessos.Count > 0) Avisar(string.Join("\n", excessos), erro: true);

        if (problema is not null) Avisar(problema, erro: true);
        else if (_modelos.Items.Count == 0) Avisar(Tr.T("Nenhum modelo de inversor ainda: use Novo modelo."));
    }

    /// <summary>"Huawei 250 — 5 MPPT × 4 entradas = 20 entradas", ou com a lista quando os MPPTs diferem.</summary>
    internal static string DescreverModelo(InverterModel m) =>
        m.IsUniform && m.Mppts > 0
            ? Tr.F("{0} — {1} MPPT × {2} entradas = {3} entradas", m.Name, m.Mppts, m.InputsByMppt[0], m.TotalInputs)
            : Tr.F("{0} — {1} MPPT ({2}) = {3} entradas", m.Name, m.Mppts, string.Join(", ", m.InputsByMppt), m.TotalInputs);

    private void PreencherModelo()
    {
        var m = ModeloEscolhido;
        var caixas = new[] { _nomeDoModelo, _mppt, _largura, _comprimento, _altura };
        foreach (var caixa in caixas) caixa.IsEnabled = m is not null;

        if (m is null)
        {
            foreach (var caixa in caixas) caixa.Text = string.Empty;
            MontarEntradas([]);
            return;
        }

        _nomeDoModelo.Text = m.Name;
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

    private Inverter? InversorEscolhido => (_inversores.SelectedItem as ListBoxItem)?.Tag as Inverter;

    /// <summary>A lista dos inversores: nome, modelo e quantas strings tem de quantas entradas.</summary>
    private void MontarInversores()
    {
        var anterior = InversorEscolhido?.Id;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);
        _inversores.Items.Clear();

        foreach (var inversor in _setup.Inverters)
        {
            var linha = new DockPanel();
            var acoes = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(acoes, Dock.Right);
            linha.Children.Add(acoes);

            // A cor do inversor (a das strings dele no desenho).
            var cor = inversor.Color ?? InverterColors.Palette[0].Color;
            var quadrado = new System.Windows.Shapes.Rectangle
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(cor.R, cor.G, cor.B)),
                Stroke = System.Windows.Media.Brushes.Gray,
                ToolTip = Tr.F("Cor das strings deste inversor: {0}", cor.ToHex()),
            };
            DockPanel.SetDock(quadrado, Dock.Left);
            linha.Children.Add(quadrado);

            var este = inversor;
            Botao(acoes, "+", Tr.T("Alocar strings: a janela some; selecione só strings em campo (Shift+clique tira), Enter volta."),
                () => JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaAlocar, este.Id.ToString("D")), largura: 30);
            Botao(acoes, Tr.T("Selecionar"), Tr.T("Seleciona no CAD todas as strings deste inversor."),
                () => JanelaEletrica.SelecionarStrings(Documento, este.Id));
            Botao(acoes, Tr.T("Soltar todas"), Tr.T("Solta todas as strings deste inversor: elas ficam livres e continuam no desenho (nada é apagado)."),
                () => SoltarTodas(este));

            var strings = _contagem.GetValueOrDefault(inversor.Id);
            var aviso = StringAllocation.ExcessWarning(inversor, _setup.FindModel(inversor.Model), strings);
            linha.Children.Add(new TextBlock
            {
                Text = ComCampo(DescreverInversor(_setup, inversor, strings), emCampo.Contains((EquipmentKind.Inverter, inversor.Id))),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = aviso is null ? System.Windows.SystemColors.ControlTextBrush : System.Windows.Media.Brushes.Firebrick,
                FontWeight = aviso is null ? FontWeights.Normal : FontWeights.SemiBold,
                ToolTip = aviso,
            });

            var item = new ListBoxItem { Content = linha, Tag = inversor };
            _inversores.Items.Add(item);
            if (inversor.Id == anterior) _inversores.SelectedItem = item;
        }
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
        var (r, linhas) = EscritaForaDeComando.Fazer(Documento, () => AtribuicaoAutomatica.Atribuir(Documento.Database));
        (AoMudar ?? Atualizar)();
        Avisar(string.Join("\n", linhas), erro: r.Changed.Count == 0 || r.Leftover > 0 || r.WithoutModel.Count > 0 || r.Unplaced > 0 || r.Duplicates > 0);
    }

    private void SalvarInversor()
    {
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
        if (InversorEscolhido is not { } inversor)
        {
            Avisar(Tr.T("Escolha um inversor na lista."), erro: true);
            return;
        }

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

    private void SoltarTodas(Inverter inversor) =>
        Fazer(() =>
        {
            var soltas = StringsDoDesenho.Soltar(Documento.Database, inversor.Id);
            return Tr.F("{0}: {1} string(s) soltas; continuam no desenho, livres.", inversor.Name, soltas);
        });

    internal static string DescreverInversor(ElectricalSetup setup, Inverter inversor, int strings)
    {
        var modelo = setup.FindModel(inversor.Model);
        var linha = modelo is null
            ? Tr.F("{0} — sem modelo — {1} string(s)", inversor.Name, strings)
            : Tr.F("{0} — {1} — {2} de {3} string(s)", inversor.Name, modelo.Name, strings, modelo.TotalInputs);

        return setup.FindTransformer(inversor.Transformer) is { } trafo
            ? Tr.F("{0} — {1} ({2})", linha, setup.FindSkid(trafo.Id)?.Name ?? Tr.F("Skid {0}", trafo.Nickname), trafo.Nickname)
            : linha;
    }

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

    private void TirarDoSkid()
    {
        if (InversorEscolhido is not { } inversor)
        {
            Avisar(Tr.T("Escolha um inversor na lista."), erro: true);
            return;
        }

        Fazer(() => ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.Ungroup(inversor.Id))
            ? Tr.F("{0} saiu do skid.", inversor.Name)
            : Tr.F("{0} não está em skid nenhum.", inversor.Name));
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

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;

        var editado = m with { Name = _nomeDoModelo.Text, InputsByMppt = entradas, Size = tamanho };
        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditModel(editado));
            if (porque is not null) return null;

            // A dimensão do modelo é a dos retângulos dos inversores dele em campo.
            EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.Inverter,
                ConfiguracaoEletricaStore.Ler(Documento.Database).Setup.Inverters.Where(i => i.Model == m.Id).Select(i => i.Id).ToList());
            return Tr.F("{0} salvo.", editado.Name.Trim());
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
