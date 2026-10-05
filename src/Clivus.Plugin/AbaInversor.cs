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
    private readonly TextBox _nomeDoInversor = new() { Width = 150, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _modeloDoInversor = new() { Height = 26, MinWidth = 140, Margin = new Thickness(0, 0, 6, 6) };
    private readonly ComboBox _trafoDoSkid = new() { Height = 26, MinWidth = 90, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _nomeDoSkid = new() { Width = 140, Height = 26, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };

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

        // O inversor escolhido na lista: editar e apagar (14.5).
        var editar = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        editar.Children.Add(new TextBlock { Text = Tr.T("Escolhido:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        editar.Children.Add(_nomeDoInversor);
        editar.Children.Add(_modeloDoInversor);
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
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);
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
            Botao(acoes, Tr.T("Selecionar"), Tr.T("Seleciona no CAD todas as strings deste inversor."),
                () => JanelaEletrica.Comando(Documento, PluginInfo.ComandoEletricaSelecionar, este.Id.ToString("D")));
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

        _modeloDoInversor.Items.Clear();
        foreach (var m in _setup.Models)
        {
            var item = new ComboBoxItem { Content = m.Name, Tag = m.Id };
            _modeloDoInversor.Items.Add(item);
            if (m.Id == inversor?.Model) _modeloDoInversor.SelectedItem = item;
        }
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
