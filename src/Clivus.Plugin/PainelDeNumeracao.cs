using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A aba Numeração da janela da configuração elétrica (etapa 15). A janela
/// (etapas 12 a 14) só chama <see cref="Criar"/>; CLIVUS_NUMERACAO mostra o
/// mesmo painel numa janela solta. A regra toda está no Core
/// (<see cref="TagScheme"/>, <see cref="ScanSetup"/>, <see cref="ScanOrder"/>);
/// aqui só se mostra, seleciona, grava e desenha.
/// </summary>
internal sealed class PainelDeNumeracao : DockPanel
{
    private readonly Document _documento;
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    // 15.1: a composição da tag.
    private readonly CheckBox _comTrafo = new() { Content = Tr.T("Trafo"), VerticalAlignment = VerticalAlignment.Center, ToolTip = Tr.T("Com o pedaço do trafo na tag. Inversor sem trafo fica sem esse pedaço.") };
    private readonly TextBox _prefixoTrafo = Caixa(Tr.T("Prefixo do trafo (ex. T ou Trafo; vazio é só o número). O número é a posição do trafo na lista."));
    private readonly TextBox _prefixoInversor = Caixa(Tr.T("Prefixo do inversor (ex. I ou Inv; vazio é só o número). O número é a posição do inversor na lista."));
    private readonly TextBox _prefixoString = Caixa(Tr.T("Prefixo da string (ex. S). O número recomeça do 1 em cada inversor."));
    private readonly ComboBox _separador = new() { Width = 120, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = Tr.T("O que vai entre os pedaços.") };
    private readonly TextBlock _exemplo = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    // 15.2: a varredura e os blocos.
    private readonly ComboBox _sentidoDaUsina = Sentidos(Tr.T("O sentido do sequencial das strings nas mesas que não estão em bloco nenhum (a usina inteira, se não há blocos)."));
    private readonly ComboBox _sentidoDoBloco = Sentidos(Tr.T("O sentido do sequencial das strings nas mesas do bloco escolhido."));
    private readonly ListBox _blocos = new() { Height = 150, ToolTip = Tr.T("Os blocos, na ordem da numeração: o bloco 1 inteiro, depois o 2, e segue; as mesas fora de bloco vêm por último.") };

    /// <summary>Verdadeiro enquanto a tela é preenchida pelo código: troca de combo aí não é pedido do usuário.</summary>
    private bool _mostrando;

    private PainelDeNumeracao(Document documento)
    {
        _documento = documento;
        Margin = new Thickness(8);
        LastChildFill = true;

        DockPanel.SetDock(_recado, Dock.Bottom);
        Children.Add(_recado);

        var pilha = new StackPanel();
        pilha.Children.Add(SecaoDaTag());
        pilha.Children.Add(SecaoDaVarredura());
        Children.Add(new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        Carregar();
    }

    /// <summary>O painel do desenho (a aba Numeração).</summary>
    internal static UIElement Criar(Document documento) => new PainelDeNumeracao(documento);

    private static TextBox Caixa(string dica) =>
        new() { Width = 64, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0), ToolTip = dica };

    private static TextBlock Rotulo(string texto) => new() { Text = texto, VerticalAlignment = VerticalAlignment.Center };

    private static WrapPanel Linha(double acima = 0) => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, acima, 0, 0) };

    private static void Botao(Panel onde, string texto, string dica, Action acao)
    {
        var b = new Button { Content = texto, Height = 26, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(10, 0, 10, 0), ToolTip = dica };
        b.Click += (_, _) => acao();
        onde.Children.Add(b);
    }

    // ------------------------------------------------------------ 15.1 tag

    private UIElement SecaoDaTag()
    {
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Ponto (T1.I1.S1)"), Tag = "." });
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Risquinho (T1-I1-S1)"), Tag = "-" });
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Nada (T1I1S1)"), Tag = string.Empty });

        var linha = Linha();
        linha.Children.Add(_comTrafo);
        linha.Children.Add(_prefixoTrafo);
        linha.Children.Add(Rotulo(Tr.T("Inversor")));
        linha.Children.Add(_prefixoInversor);
        linha.Children.Add(Rotulo(Tr.T("String")));
        linha.Children.Add(_prefixoString);
        linha.Children.Add(Rotulo(Tr.T("Separador")));
        linha.Children.Add(new Border { Width = 4 });
        linha.Children.Add(_separador);

        var salvar = new Button { Content = Tr.T("Salvar composição"), Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 12, 0), ToolTip = Tr.T("Grava a composição no desenho. As tags já desenhadas só mudam ao gerar de novo.") };
        salvar.Click += (_, _) => SalvarEsquema();

        var embaixo = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(salvar, Dock.Left);
        embaixo.Children.Add(salvar);
        embaixo.Children.Add(_exemplo);

        foreach (var caixa in new[] { _prefixoTrafo, _prefixoInversor, _prefixoString }) caixa.TextChanged += (_, _) => Previa();
        _comTrafo.Checked += (_, _) => Previa();
        _comTrafo.Unchecked += (_, _) => Previa();
        _separador.SelectionChanged += (_, _) => Previa();

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(linha);
        corpo.Children.Add(embaixo);
        return new GroupBox { Header = Tr.T("Composição da tag"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    private TagScheme EsquemaDaTela() => new(
        _comTrafo.IsChecked == true,
        _prefixoTrafo.Text,
        _prefixoInversor.Text,
        _prefixoString.Text,
        (_separador.SelectedItem as ComboBoxItem)?.Tag as string ?? ".");

    private void MostrarEsquema(TagScheme esquema)
    {
        _comTrafo.IsChecked = esquema.IncludeTransformer;
        _prefixoTrafo.Text = esquema.TransformerPrefix;
        _prefixoInversor.Text = esquema.InverterPrefix;
        _prefixoString.Text = esquema.StringPrefix;
        _separador.SelectedItem = _separador.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == esquema.Separator) ?? _separador.Items[0];
        Previa();
    }

    /// <summary>O exemplo ao vivo: três strings de dois inversores e um inversor sem trafo.</summary>
    private void Previa()
    {
        var esquema = EsquemaDaTela();

        if (esquema.Problem() is { } problema)
        {
            _exemplo.Foreground = Brushes.Firebrick;
            _exemplo.Text = problema;
            return;
        }

        _exemplo.Foreground = SystemColors.ControlTextBrush;
        _exemplo.Text = Exemplo(esquema);
    }

    internal static string Exemplo(TagScheme esquema) =>
        Tr.F("Exemplo: {0}, {1}, {2}; inversor sem trafo: {3}", esquema.Compose(1, 1, 1), esquema.Compose(1, 1, 2), esquema.Compose(1, 2, 1), esquema.Compose(null, 3, 1));

    private void SalvarEsquema()
    {
        var esquema = EsquemaDaTela();

        if (esquema.Problem() is { } problema)
        {
            Avisar(Tr.F("Não salvei: {0}.", problema), erro: true);
            return;
        }

        Fazer(() =>
        {
            NumeracaoStore.GravarEsquema(_documento.Database, esquema);
            return Tr.T("Composição salva no desenho.");
        });
    }

    // ------------------------------------------- 15.2 varredura e blocos

    private static ComboBox Sentidos(string dica)
    {
        var caixa = new ComboBox { Width = 210, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 12, 0), ToolTip = dica };
        foreach (var sentido in Enum.GetValues<ScanDirection>()) caixa.Items.Add(new ComboBoxItem { Content = ScanOrder.Describe(sentido), Tag = sentido });
        return caixa;
    }

    private static void Escolher(ComboBox caixa, ScanDirection sentido) =>
        caixa.SelectedItem = caixa.Items.OfType<ComboBoxItem>().First(i => (ScanDirection)i.Tag == sentido);

    private static ScanDirection? Sentido(ComboBox caixa) => (caixa.SelectedItem as ComboBoxItem)?.Tag as ScanDirection?;

    private UIElement SecaoDaVarredura()
    {
        var usina = Linha();
        usina.Children.Add(Rotulo(Tr.T("Sentido da usina inteira")));
        usina.Children.Add(_sentidoDaUsina);
        _sentidoDaUsina.SelectionChanged += (_, _) =>
        {
            if (_mostrando || Sentido(_sentidoDaUsina) is not { } sentido) return;
            MudarVarredura(v => v.DefaultDirection = sentido, Tr.F("Usina inteira: {0}.", ScanOrder.Describe(sentido)));
        };

        var botoes = Linha(6);
        Botao(botoes, Tr.T("Novo bloco"), Tr.T("Acrescenta um bloco no fim da lista, sem mesas, no sentido da usina."), NovoBloco);
        Botao(botoes, Tr.T("Selecionar mesas"), Tr.T("Seleciona no desenho as mesas do bloco escolhido (só mesas entram). Mesa que estava em outro bloco passa para este."), SelecionarMesas);
        Botao(botoes, Tr.T("Subir"), Tr.T("Sobe o bloco escolhido na lista: ele passa a ser numerado antes do de cima."), () => MoverBloco(-1));
        Botao(botoes, Tr.T("Descer"), Tr.T("Desce o bloco escolhido na lista: ele passa a ser numerado depois do de baixo."), () => MoverBloco(+1));
        Botao(botoes, Tr.T("Renomear"), Tr.T("Troca o nome do bloco escolhido."), RenomearBloco);
        Botao(botoes, Tr.T("Apagar bloco"), Tr.T("Tira o bloco da lista; as mesas dele voltam ao sentido da usina. As tags já desenhadas não mudam."), ApagarBloco);
        botoes.Children.Add(Rotulo(Tr.T("Sentido do bloco")));
        botoes.Children.Add(_sentidoDoBloco);

        _sentidoDoBloco.SelectionChanged += (_, _) =>
        {
            if (_mostrando || Sentido(_sentidoDoBloco) is not { } sentido || BlocoEscolhido is not { } bloco || bloco.Direction == sentido) return;
            MudarVarredura(v => v.SetDirection(bloco.Id, sentido), Tr.F("{0}: {1}.", bloco.Name, ScanOrder.Describe(sentido)), bloco.Id);
        };

        _blocos.SelectionChanged += (_, _) =>
        {
            if (_mostrando || BlocoEscolhido is not { } bloco) return;

            _mostrando = true;
            Escolher(_sentidoDoBloco, bloco.Direction);
            _mostrando = false;
        };

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(usina);
        corpo.Children.Add(new Border { Height = 6 });
        corpo.Children.Add(_blocos);
        corpo.Children.Add(botoes);
        return new GroupBox { Header = Tr.T("Varredura e blocos"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    private NumberingBlock? BlocoEscolhido => (_blocos.SelectedItem as ListBoxItem)?.Tag as NumberingBlock;

    internal static string Descrever(NumberingBlock bloco, int posicao) =>
        Tr.F("{0}. {1} — {2} mesa(s), {3}", posicao, bloco.Name, bloco.Tables.Count, ScanOrder.Describe(bloco.Direction));

    private void MostrarVarredura(ScanSetup varredura, Guid? manter)
    {
        _mostrando = true;
        try
        {
            Escolher(_sentidoDaUsina, varredura.DefaultDirection);

            var anterior = manter ?? BlocoEscolhido?.Id;
            _blocos.Items.Clear();

            for (var i = 0; i < varredura.Blocks.Count; i++)
            {
                var bloco = varredura.Blocks[i];
                var item = new ListBoxItem { Content = Descrever(bloco, i + 1), Tag = bloco };
                _blocos.Items.Add(item);
                if (bloco.Id == anterior) _blocos.SelectedItem = item;
            }

            if (_blocos.SelectedItem is null && _blocos.Items.Count > 0) _blocos.SelectedIndex = 0;
            Escolher(_sentidoDoBloco, BlocoEscolhido?.Direction ?? varredura.DefaultDirection);
            _sentidoDoBloco.IsEnabled = BlocoEscolhido is not null;
        }
        finally
        {
            _mostrando = false;
        }
    }

    /// <summary>Muda a varredura gravada (trava, vigia calado), recarrega a lista e avisa.</summary>
    private void MudarVarredura(Action<ScanSetup> mudanca, string feito, Guid? manter = null) =>
        Fazer(() =>
        {
            var problema = NumeracaoStore.MudarVarredura(_documento.Database, mudanca);
            return problema is null ? feito : Tr.F("{0} ATENÇÃO: {1}.", feito, problema);
        }, manter);

    private void NovoBloco()
    {
        NumberingBlock? novo = null;
        MudarVarredura(v => novo = v.AddBlock(), Tr.T("Bloco novo no fim da lista: escolha as mesas dele com Selecionar mesas."));
        if (novo is not null) Recarregar(novo.Id);
    }

    private NumberingBlock? BlocoOuAviso()
    {
        if (BlocoEscolhido is { } bloco) return bloco;

        Avisar(Tr.T("Escolha um bloco na lista."), erro: true);
        return null;
    }

    /// <summary>15.3: a ordem da lista é a ordem da numeração.</summary>
    private void MoverBloco(int delta)
    {
        if (BlocoOuAviso() is not { } bloco) return;

        var andou = false;
        MudarVarredura(v => andou = v.Move(bloco.Id, delta), Tr.F("{0} agora é numerado nesta posição da lista; gere de novo para as tags seguirem.", bloco.Name), bloco.Id);
        if (!andou) Avisar(delta < 0 ? Tr.F("{0} já é o primeiro da lista.", bloco.Name) : Tr.F("{0} já é o último da lista.", bloco.Name));
    }

    private void RenomearBloco()
    {
        if (BlocoOuAviso() is not { } bloco) return;

        var janela = new JanelaDeNome(Tr.T("Renomear bloco"), Tr.T("Como se chama este bloco?"));
        if (AcadApp.ShowModalWindow(janela) != true || janela.Nome is not { } nome) return;

        string? problema = null;
        MudarVarredura(v => problema = v.Rename(bloco.Id, nome), Tr.F("Renomeado para {0}.", nome.Trim()), bloco.Id);
        if (problema is not null) Avisar(Tr.F("Não renomeei: {0}.", problema), erro: true);
    }

    private void ApagarBloco()
    {
        if (BlocoOuAviso() is not { } bloco) return;
        MudarVarredura(v => v.Remove(bloco.Id), Tr.F("{0} apagado; as mesas dele voltam ao sentido da usina.", bloco.Name));
    }

    /// <summary>
    /// A seleção em campo das mesas do bloco (regra elétrica 4: só mesa; o
    /// que não é peça de mesa do plugin é ignorado). A janela some enquanto
    /// o usuário seleciona.
    /// </summary>
    private void SelecionarMesas()
    {
        try
        {
            if (BlocoOuAviso() is not { } bloco) return;

            if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
            {
                Avisar(Tr.T("Ative o desenho desta janela para selecionar as mesas."), erro: true);
                return;
            }

            var editor = _documento.Editor;
            PromptSelectionResult selecao;

            using (var interacao = editor.StartUserInteraction(Window.GetWindow(this)))
            {
                selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nSelecione as mesas do {0} (só mesas entram): ", bloco.Name) }, FiltroDeMesas);
                interacao.End();
            }

            if (selecao.Status != PromptStatus.OK) return;

            var ids = selecao.Value.GetObjectIds();
            Fazer(() => GravarMesas(_documento, bloco, ids), bloco.Id);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar as mesas do bloco.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>As mesas tocadas pela seleção passam a ser as do bloco; a frase do que foi feito.</summary>
    internal static string GravarMesas(Document documento, NumberingBlock bloco, IEnumerable<ObjectId> ids)
    {
        var mesas = SelecaoCommands.MesasTocadas(documento, ids);
        if (mesas.Count == 0) return Tr.T("Nenhuma mesa do plugin na seleção; o bloco ficou como estava.");

        var tiradas = 0;
        var problema = NumeracaoStore.MudarVarredura(documento.Database, v => tiradas = v.SetTables(bloco.Id, mesas));
        var feito = tiradas > 0
            ? Tr.F("{0}: {1} mesa(s); {2} saíram de outro bloco.", bloco.Name, mesas.Count, tiradas)
            : Tr.F("{0}: {1} mesa(s).", bloco.Name, mesas.Count);
        return problema is null ? feito : Tr.F("{0} ATENÇÃO: {1}.", feito, problema);
    }

    /// <summary>Peças de mesa (bloco, contorno, face); curva de nível em polilinha 2D e o resto nem entram, e o que não é mesa do plugin é ignorado depois.</summary>
    internal static SelectionFilter FiltroDeMesas => new([new TypedValue((int)DxfCode.Start, "INSERT,POLYLINE,3DFACE")]);

    // -------------------------------------------------------------- comum

    private void Carregar()
    {
        try
        {
            var (esquema, problema) = NumeracaoStore.Esquema(_documento.Database);
            MostrarEsquema(esquema);
            Recarregar();
            if (problema is not null) Avisar(problema, erro: true);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao carregar a aba Numeração.", erro);
            Avisar(Tr.F("Não consegui ler a numeração do desenho: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>Relê a varredura do desenho e mostra (mantendo o bloco escolhido).</summary>
    private void Recarregar(Guid? manter = null)
    {
        var (varredura, problema) = NumeracaoStore.Varredura(_documento.Database);
        MostrarVarredura(varredura, manter);
        if (problema is not null) Avisar(problema, erro: true);
    }

    private void Avisar(string texto, bool erro = false)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    /// <summary>Escreve no desenho fora de comando (trava e vigia calado), recarrega a tela, sem derrubar o Civil 3D num clique.</summary>
    private void Fazer(Func<string?> operacao, Guid? manter = null)
    {
        try
        {
            var frase = EscritaForaDeComando.Fazer(_documento, operacao);
            Recarregar(manter);
            if (frase is not null) Avisar(frase);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na aba Numeração.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }
}
