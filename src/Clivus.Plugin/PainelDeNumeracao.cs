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

    // 15.1: a composição da tag (modelo livre, fundo e moldura).
    private readonly TextBox _modelo = new()
    {
        Width = 230,
        Height = 24,
        VerticalContentAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 6, 0),
        FontFamily = new FontFamily("Consolas"),
        ToolTip = Tr.T("O modelo da tag. {T}, {I} e {S} são os números do trafo, do inversor e da string; o resto é texto fixo (letras, risquinho, ponto, espaço ou nada). Zeros à esquerda: {I:00} dá 01."),
    };
    private readonly CheckBox _fundo = new() { Content = Tr.T("Fundo"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), ToolTip = Tr.T("Máscara atrás do texto, na cor do fundo da tela: esconde o que está embaixo e destaca a tag.") };
    private readonly CheckBox _moldura = new() { Content = Tr.T("Moldura"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = Tr.T("Um quadro em volta do texto da tag.") };
    private readonly TextBlock _exemplo = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    /// <summary>Quantos inversores há no cadastro (com mais de um, o modelo precisa de {I}); relido com a tela.</summary>
    private int _inversores = 1;

    // 15.2: a varredura e os blocos.
    private readonly ComboBox _sentidoDaUsina = CaixaDeSentido(Tr.T("O sentido em que a numeração avança nas mesas que não estão em bloco nenhum (a usina inteira, se não há blocos)."));
    private readonly ComboBox _faixaDaUsina = CaixaDeSentido(Tr.T("Na mesma faixa (linha ou coluna), em que sentido as strings fora de bloco são numeradas."));
    private readonly ListBox _blocos = new() { MaxHeight = 260, Margin = new Thickness(0, 6, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _resumo = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    // 15.5: gerar e apagar, na usina, num bloco ou num inversor.
    private readonly ComboBox _blocoDeGerar = new() { Width = 180, Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _inversor = new() { Width = 180, Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Grid _linhasDeGerar = new();

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
        pilha.Children.Add(SecaoGerar());
        Children.Add(new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        Carregar();

        // Voltar à aba (ou a janela reaparecer) relê o desenho: o usuário pode ter mexido no CAD.
        var primeira = true;
        Loaded += (_, _) =>
        {
            try
            {
                if (primeira)
                {
                    primeira = false;
                    return;
                }

                _desenho = null;
                Recarregar();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao reler a aba Numeração.", erro);
            }
        };
    }

    /// <summary>O painel do desenho (a aba Numeração).</summary>
    internal static UIElement Criar(Document documento) => new PainelDeNumeracao(documento);

    private static TextBlock Rotulo(string texto) => new() { Text = texto, VerticalAlignment = VerticalAlignment.Center };

    private static WrapPanel Linha(double acima = 0) => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, acima, 0, 0) };

    /// <summary>Um botão cujo clique nunca derruba o Civil 3D: a falha vai para o registro e para o recado.</summary>
    private void Botao(Panel onde, string texto, string dica, Action acao)
    {
        var b = new Button { Content = texto, Height = 26, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(10, 0, 10, 0), ToolTip = dica };
        b.Click += (_, _) =>
        {
            try
            {
                acao();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Falha no botão {texto} da aba Numeração.", erro);
                Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
            }
        };
        onde.Children.Add(b);
    }

    // ------------------------------------------------------------ 15.1 tag

    private UIElement SecaoDaTag()
    {
        var linha = Linha();
        linha.Children.Add(Rotulo(Tr.T("Modelo:")));
        linha.Children.Add(_modelo);
        BotaoPequeno(linha, Tr.T("+ Trafo"), Tr.T("Põe {T}, o número do trafo, onde está o cursor. Inversor sem trafo perde esse pedaço."), () => Inserir(TagScheme.TransformerField));
        BotaoPequeno(linha, Tr.T("+ Inversor"), Tr.T("Põe {I}, o número do inversor (a posição na lista do cadastro), onde está o cursor."), () => Inserir(TagScheme.InverterField));
        BotaoPequeno(linha, Tr.T("+ String"), Tr.T("Põe {S}, o número da string (recomeça do 1 em cada inversor), onde está o cursor."), () => Inserir(TagScheme.StringField));
        linha.Children.Add(_fundo);
        linha.Children.Add(_moldura);
        Botao(linha, Tr.T("Salvar"), Tr.T("Grava a composição no desenho. Fundo e moldura mudam já nas tags desenhadas; o texto delas só muda ao gerar de novo."), SalvarEsquema);
        var salvar = (Button)linha.Children[^1];
        salvar.Height = 24;
        salvar.Margin = new Thickness(0);

        var ajuda = new TextBlock
        {
            Text = Tr.T("{T} trafo, {I} inversor, {S} string; {I:00} põe zeros (01). Inversor sem trafo: some o {T} com o texto antes dele (e o separador que vinha depois, se ele era o primeiro)."),
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };

        _exemplo.Margin = new Thickness(0, 4, 0, 0);
        _modelo.TextChanged += (_, _) => Previa();

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(linha);
        corpo.Children.Add(_exemplo);
        corpo.Children.Add(ajuda);
        return new GroupBox { Header = Tr.T("Composição da tag"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    /// <summary>Um botão baixo e estreito ("+ Trafo"), que não tira o cursor da caixa do modelo.</summary>
    private void BotaoPequeno(Panel onde, string texto, string dica, Action acao)
    {
        Botao(onde, texto, dica, acao);
        var b = (Button)onde.Children[^1];
        b.Height = 24;
        b.Margin = new Thickness(0, 0, 4, 0);
        b.Padding = new Thickness(6, 0, 6, 0);
        b.Focusable = false;
    }

    /// <summary>Põe o campo onde está o cursor da caixa do modelo (troca o trecho marcado, se há).</summary>
    private void Inserir(string campo)
    {
        var onde = Math.Clamp(_modelo.SelectionStart, 0, _modelo.Text.Length);
        var tira = Math.Clamp(_modelo.SelectionLength, 0, _modelo.Text.Length - onde);
        _modelo.Text = _modelo.Text.Remove(onde, tira).Insert(onde, campo);
        _modelo.Focus();
        _modelo.CaretIndex = onde + campo.Length;
    }

    private TagScheme EsquemaDaTela() => new(_modelo.Text.Trim(), _fundo.IsChecked == true, _moldura.IsChecked == true);

    private void MostrarEsquema(TagScheme esquema)
    {
        _modelo.Text = esquema.Template;
        _fundo.IsChecked = esquema.Background;
        _moldura.IsChecked = esquema.Border;
        Previa();
    }

    /// <summary>O exemplo ao vivo, enquanto digita: três strings de dois inversores e um inversor sem trafo; ou o porquê de não valer.</summary>
    private void Previa()
    {
        try
        {
            var esquema = EsquemaDaTela();

            if (esquema.Problem(_inversores) is { } problema)
            {
                _exemplo.Foreground = Brushes.Firebrick;
                _exemplo.Text = problema;
                return;
            }

            _exemplo.Foreground = SystemColors.ControlTextBrush;
            _exemplo.Text = Exemplo(esquema);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no exemplo da tag.", erro);
        }
    }

    internal static string Exemplo(TagScheme esquema) =>
        Tr.F("Exemplo: {0}, {1}, {2}; inversor sem trafo: {3}", esquema.Compose(1, 1, 1), esquema.Compose(1, 1, 2), esquema.Compose(1, 2, 1), esquema.Compose(null, 3, 1));

    private void SalvarEsquema()
    {
        var esquema = EsquemaDaTela();

        try
        {
            var (frase, gravou) = SalvarComposicao(_documento, esquema);
            Recarregar();
            Avisar(frase, erro: !gravou);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao salvar a composição da tag.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }

        AtualizarTela();
    }

    /// <summary>
    /// O Salvar da composição (o botão e o nível 2 pelo mesmo caminho, fora de
    /// comando): confere o modelo com os inversores do cadastro, grava e põe
    /// o fundo e a moldura nas tags já desenhadas. A frase, e se gravou.
    /// </summary>
    internal static (string Frase, bool Gravou) SalvarComposicao(Document documento, TagScheme esquema)
    {
        var inversores = StringNumbering.CountInverters(ElectricalStore.Inverters(documento.Database).Items);
        if (esquema.Problem(inversores) is { } problema) return (Tr.F("Não salvei: {0}.", problema), false);

        var mudadas = EscritaForaDeComando.Fazer(documento, () =>
        {
            // Primeiro os textos (transação própria: se falhar, nada fica gravado), depois o registro.
            var quantas = NumeracaoDesenho.Reemoldurar(documento.Database, esquema);
            NumeracaoStore.GravarEsquema(documento.Database, esquema);
            return quantas;
        });

        return (mudadas > 0
            ? Tr.F("Composição salva no desenho; fundo e moldura em {0} tag(s) já desenhada(s). O texto delas muda ao gerar de novo.", mudadas)
            : Tr.T("Composição salva no desenho."), true);
    }

    // ------------------------------------------- 15.2 varredura e blocos

    private static ComboBox CaixaDeSentido(string dica) =>
        new() { Width = 150, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), ToolTip = dica };

    /// <summary>Preenche a caixa do sentido que avança (os quatro) e a da faixa (os dois perpendiculares a ele), com a escolha.</summary>
    private static void MontarSentidos(ComboBox sentido, ComboBox faixa, ScanDirection escolhido, ScanDirection naFaixa)
    {
        sentido.Items.Clear();
        foreach (var s in Enum.GetValues<ScanDirection>()) sentido.Items.Add(new ComboBoxItem { Content = ScanOrder.Describe(s), Tag = s });
        sentido.SelectedItem = sentido.Items.OfType<ComboBoxItem>().First(i => (ScanDirection)i.Tag == escolhido);

        faixa.Items.Clear();
        foreach (var f in ScanOrder.CrossOptions(escolhido)) faixa.Items.Add(new ComboBoxItem { Content = ScanOrder.Describe(f), Tag = f });
        faixa.SelectedItem = faixa.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (ScanDirection)i.Tag == naFaixa) ?? faixa.Items[0];
    }

    private static ScanDirection? Sentido(ComboBox caixa) => (caixa.SelectedItem as ComboBoxItem)?.Tag as ScanDirection?;

    private static TextBlock NaFaixa() => new() { Text = Tr.T("na faixa:"), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// A troca de uma caixa de sentido grava depois que a escolha assenta: a
    /// gravação refaz a lista inteira, inclusive a caixa que disparou. Nada
    /// escapa para o WPF.
    /// </summary>
    private void AoEscolher(ComboBox caixa, Action<ScanDirection> gravar)
    {
        caixa.SelectionChanged += (_, _) =>
        {
            try
            {
                if (_mostrando || Sentido(caixa) is not { } escolhido) return;

                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        gravar(escolhido);
                    }
                    catch (Exception erro)
                    {
                        RegistroDeDiagnostico.Registrar("Falha ao gravar o sentido da numeração.", erro);
                        Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
                    }
                });
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao escolher o sentido da numeração.", erro);
            }
        };
    }

    private UIElement SecaoDaVarredura()
    {
        var usina = Linha();
        usina.Children.Add(Rotulo(Tr.T("Usina inteira:")));
        usina.Children.Add(_sentidoDaUsina);
        usina.Children.Add(NaFaixa());
        usina.Children.Add(_faixaDaUsina);
        usina.Children.Add(new Border { Width = 8 });
        Botao(usina, Tr.T("Novo bloco"), Tr.T("Acrescenta um bloco no fim da lista, sem mesas, nos sentidos da usina."), NovoBloco);

        AoEscolher(_sentidoDaUsina, sentido => MudarVarredura(v => v.DefaultDirection = sentido, Tr.F("Usina inteira: {0}.", ScanOrder.Describe(sentido))));
        AoEscolher(_faixaDaUsina, faixa => MudarVarredura(v => v.SetDefaultCross(faixa), Tr.F("Usina inteira, na faixa: {0}.", ScanOrder.Describe(faixa))));

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(usina);
        corpo.Children.Add(_blocos);
        corpo.Children.Add(_resumo);
        return new GroupBox { Header = Tr.T("Varredura e blocos"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    private NumberingBlock? BlocoEscolhido => (_blocos.SelectedItem as ListBoxItem)?.Tag as NumberingBlock;

    /// <summary>A linha do bloco na linha de comando (CLIVUS_NUMERACAO_AUTO Listar): posição, nome, mesas e os dois sentidos.</summary>
    internal static string Descrever(NumberingBlock bloco, int posicao) =>
        Tr.F("{0}. {1} — {2} mesa(s), {3}", posicao, bloco.Name, bloco.Tables.Count, ScanOrder.Describe(bloco.Direction, bloco.Cross));

    /// <summary>O resumo da linha do bloco: "3 mesa(s), 12 string(s)".</summary>
    internal static string ResumoDoBloco(NumberingBlock bloco, BlockStringCount contagem) =>
        Tr.F("{0} mesa(s), {1} string(s)", bloco.Tables.Count, contagem.ByBlock.GetValueOrDefault(bloco.Id));

    /// <summary>O resumo embaixo da lista: o total em blocos e o que ficou fora.</summary>
    internal static string ResumoGeral(ScanSetup varredura, BlockStringCount contagem)
    {
        var frase = Tr.F("Em blocos: {0} mesa(s), {1} string(s). Fora de bloco: {2} string(s), nos sentidos da usina.",
            varredura.Blocks.Sum(b => b.Tables.Count), contagem.InBlocks, contagem.Outside);
        if (contagem.Unplaced > 0) frase += " " + Tr.F("{0} string(s) sem módulo no desenho ficam sem tag.", contagem.Unplaced);
        return frase;
    }

    /// <summary>As strings do plugin e onde está cada módulo (a mesa), lidos do desenho.</summary>
    private static (List<ElectricalString> Strings, Dictionary<Guid, ModuleSpot> Modulos) LerDesenho(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var strings = ElectricalStore.Strings(transacao, database).Select(s => s.String).ToList();
        var modulos = NumeracaoDesenho.Modulos(transacao, database)
            .ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));
        return (strings, modulos);
    }

    /// <summary>Quantas strings do plugin há em cada bloco (pela mesa do primeiro módulo), lidas do desenho.</summary>
    internal static BlockStringCount Contar(Database database, ScanSetup varredura)
    {
        var (strings, modulos) = LerDesenho(database);
        return varredura.CountStrings(strings, modulos);
    }

    /// <summary>
    /// O desenho lido para o resumo, guardado: trocar sentido ou mover bloco
    /// não muda strings nem módulos, e ler o espaço do modelo a cada clique
    /// pesa numa usina grande. Relido ao mostrar a aba, ao gerar e depois da
    /// seleção em campo.
    /// </summary>
    private (List<ElectricalString> Strings, Dictionary<Guid, ModuleSpot> Modulos)? _desenho;

    /// <summary>Um botão pequeno da linha do bloco.</summary>
    private void BotaoDaLinha(Panel onde, string texto, string dica, Action acao, double? largura = null)
    {
        Botao(onde, texto, dica, acao);
        var b = (Button)onde.Children[^1];
        b.Height = 24;
        b.Margin = new Thickness(4, 0, 0, 0);
        b.Padding = new Thickness(5, 0, 5, 0);
        if (largura is { } l) b.Width = l;
    }

    /// <summary>A linha de um bloco: nome, resumo, os dois sentidos e os botões dele.</summary>
    private ListBoxItem LinhaDoBloco(NumberingBlock bloco, int posicao, int total, BlockStringCount contagem)
    {
        var linha = new DockPanel();

        var acoes = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(acoes, Dock.Right);
        linha.Children.Add(acoes);

        var este = bloco;
        BotaoDaLinha(acoes, Tr.T("Selecionar"), Tr.F("A janela some: selecione no desenho as mesas do {0} (só mesas entram; substituem as de antes) e Enter. Mesa de outro bloco passa para este.", bloco.Name), () => PedirMesas(este));
        BotaoDaLinha(acoes, Tr.T("Mostrar"), Tr.F("Deixa selecionadas no desenho as mesas do {0}, com a janela aberta.", bloco.Name), () => Mostrar(este));
        BotaoDaLinha(acoes, "↑", Tr.T("Sobe o bloco na lista: ele passa a ser numerado antes do de cima."), () => MoverBloco(este, -1), 26);
        BotaoDaLinha(acoes, "↓", Tr.T("Desce o bloco na lista: ele passa a ser numerado depois do de baixo."), () => MoverBloco(este, +1), 26);
        BotaoDaLinha(acoes, "✎", Tr.T("Renomear o bloco."), () => RenomearBloco(este), 26);
        BotaoDaLinha(acoes, Tr.T("Apagar"), Tr.T("Tira o bloco da lista; as mesas dele voltam aos sentidos da usina. As tags já desenhadas não mudam."), () => ApagarBloco(este));
        ((Button)acoes.Children[2]).IsEnabled = posicao > 1;
        ((Button)acoes.Children[3]).IsEnabled = posicao < total;

        var esquerda = new StackPanel { Orientation = Orientation.Horizontal };
        esquerda.Children.Add(new TextBlock
        {
            Text = $"{posicao}. {bloco.Name}",
            Width = 100,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = bloco.Name,
        });
        esquerda.Children.Add(new TextBlock { Text = ResumoDoBloco(bloco, contagem), Width = 125, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });

        var sentido = CaixaDeSentido(Tr.T("O sentido em que a numeração avança nas mesas deste bloco."));
        var faixa = CaixaDeSentido(Tr.T("Na mesma faixa (linha ou coluna), em que sentido as strings deste bloco são numeradas."));
        MontarSentidos(sentido, faixa, bloco.Direction, bloco.Cross);
        AoEscolher(sentido, s => MudarVarredura(v => v.SetDirection(este.Id, s), Tr.F("{0}: {1}.", este.Name, ScanOrder.Describe(s)), este.Id));
        AoEscolher(faixa, f => MudarVarredura(v => v.SetCross(este.Id, f), Tr.F("{0}, na faixa: {1}.", este.Name, ScanOrder.Describe(f)), este.Id));
        esquerda.Children.Add(sentido);
        esquerda.Children.Add(NaFaixa());
        esquerda.Children.Add(faixa);
        linha.Children.Add(esquerda);

        var item = new ListBoxItem { Content = linha, Tag = bloco, Padding = new Thickness(2, 1, 2, 1) };

        // Clicar numa caixa ou botão da linha também escolhe o bloco (o da linha Bloco da seção Gerar).
        item.PreviewMouseLeftButtonDown += (_, _) =>
        {
            try
            {
                item.IsSelected = true;
                if (_blocoDeGerar.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as NumberingBlock)?.Id == este.Id) is { } doGerar) _blocoDeGerar.SelectedItem = doGerar;
            }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao escolher o bloco da numeração.", erro); }
        };

        return item;
    }

    private void MostrarVarredura(ScanSetup varredura, BlockStringCount contagem, Guid? manter)
    {
        _mostrando = true;
        try
        {
            MontarSentidos(_sentidoDaUsina, _faixaDaUsina, varredura.DefaultDirection, varredura.DefaultCross);

            var anterior = manter ?? BlocoEscolhido?.Id;
            _blocos.Items.Clear();

            for (var i = 0; i < varredura.Blocks.Count; i++)
            {
                var item = LinhaDoBloco(varredura.Blocks[i], i + 1, varredura.Blocks.Count, contagem);
                _blocos.Items.Add(item);
                if (varredura.Blocks[i].Id == anterior) _blocos.SelectedItem = item;
            }

            if (_blocos.SelectedItem is null && _blocos.Items.Count > 0) _blocos.SelectedIndex = 0;
            MostrarBlocosDeGerar(varredura, manter);
            _blocos.Visibility = _blocos.Items.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _resumo.Text = varredura.Blocks.Count == 0
                ? Tr.F("Sem blocos: as {0} string(s) são numeradas nos sentidos da usina. Novo bloco separa um pedaço com sentidos próprios.", contagem.Outside)
                : ResumoGeral(varredura, contagem);
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
        MudarVarredura(v => novo = v.AddBlock(), Tr.T("Bloco novo no fim da lista: escolha as mesas dele com Selecionar."));
        if (novo is not null) Recarregar(novo.Id);
    }

    /// <summary>15.3: a ordem da lista é a ordem da numeração.</summary>
    private void MoverBloco(NumberingBlock bloco, int delta)
    {
        var andou = false;
        MudarVarredura(v => andou = v.Move(bloco.Id, delta), Tr.F("{0} agora é numerado nesta posição da lista; gere de novo para as tags seguirem.", bloco.Name), bloco.Id);
        if (!andou) Avisar(delta < 0 ? Tr.F("{0} já é o primeiro da lista.", bloco.Name) : Tr.F("{0} já é o último da lista.", bloco.Name));
    }

    private void RenomearBloco(NumberingBlock bloco)
    {
        var janela = new JanelaDeNome(Tr.T("Renomear bloco"), Tr.T("Como se chama este bloco?"));
        if (AcadApp.ShowModalWindow(janela) != true || janela.Nome is not { } nome) return;

        string? problema = null;
        MudarVarredura(v => problema = v.Rename(bloco.Id, nome), Tr.F("Renomeado para {0}.", nome.Trim()), bloco.Id);
        if (problema is not null) Avisar(Tr.F("Não renomeei: {0}.", problema), erro: true);
    }

    private void ApagarBloco(NumberingBlock bloco) =>
        MudarVarredura(v => v.Remove(bloco.Id), Tr.F("{0} apagado; as mesas dele voltam aos sentidos da usina.", bloco.Name));

    /// <summary>Os painéis que pediram a seleção das mesas, por desenho: o comando os traz de volta (<see cref="Retomar"/>).</summary>
    private static readonly Dictionary<Document, (PainelDeNumeracao Painel, Guid Bloco)> Pedidos = [];

    /// <summary>
    /// "Selecionar" do bloco: a seleção em campo roda no comando
    /// CLIVUS_NUMERACAO_MESAS. Pedir a seleção direto do clique da janela
    /// solta (fora de comando, no contexto da aplicação) não funciona: o
    /// AutoCAD devolve a pergunta na hora, sem deixar selecionar (a causa do
    /// "não deixa selecionar", Renan, 05/10/2026). A janela some e o comando a
    /// traz de volta com a frase do resultado.
    /// </summary>
    private void PedirMesas(NumberingBlock bloco)
    {
        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Avisar(Tr.T("Ative o desenho desta janela para selecionar as mesas."), erro: true);
            return;
        }

        if (!ClivusExtension.TemInterface()) return;

        var janela = Window.GetWindow(this);

        try
        {
            Pedidos[_documento] = (this, bloco.Id);
            janela?.Hide();
            JanelaEletrica.Comando(_documento, PluginInfo.ComandoNumeracaoMesas, bloco.Id.ToString("D"));
        }
        catch
        {
            Pedidos.Remove(_documento);
            janela?.Show();
            throw;
        }
    }

    /// <summary>
    /// Depois do CLIVUS_NUMERACAO_MESAS: a janela que pediu volta, relê o
    /// desenho e mostra a frase. Sem pedido (comando digitado), nada.
    /// </summary>
    internal static void Retomar(Document documento, string? frase, bool erro)
    {
        if (!Pedidos.Remove(documento, out var pedido)) return;

        try
        {
            var janela = Window.GetWindow(pedido.Painel);
            if (janela is not null && !janela.IsVisible) janela.Show();
            pedido.Painel._desenho = null;
            pedido.Painel.Recarregar(pedido.Bloco);
            if (frase is not null) pedido.Painel.Avisar(frase, erro);
            janela?.Activate();
        }
        catch (Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trazer de volta a aba Numeração.", falha);
        }
    }

    /// <summary>"Mostrar" do bloco: as mesas dele ficam selecionadas no desenho, com a janela aberta.</summary>
    private void Mostrar(NumberingBlock bloco)
    {
        var quantas = MostrarMesas(_documento, bloco.Id);
        Avisar(quantas == 0
            ? Tr.F("{0} não tem mesa no desenho: escolha as mesas com Selecionar.", bloco.Name)
            : Tr.F("{0}: {1} mesa(s) selecionada(s) no desenho.", bloco.Name, quantas));
    }

    /// <summary>Os contornos das mesas do bloco que estão no desenho (nada é gravado).</summary>
    internal static ObjectId[] ContornosDoBloco(Database database, Guid bloco)
    {
        if (NumeracaoStore.Varredura(database).Varredura.Find(bloco) is not { } achado || achado.Tables.Count == 0) return [];

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var mesas = LayoutScan.Tables(transacao, database);
        return achado.Tables.Where(mesas.ContainsKey).SelectMany(t => mesas[t].Contours).ToArray();
    }

    /// <summary>
    /// O caminho do botão Mostrar (o mesmo do Selecionar da aba Inversor):
    /// primeiro a seleção implícita direto daqui, com o documento travado;
    /// depois, com interface, o comando CLIVUS_NUMERACAO_MOSTRAR pela linha de
    /// comando (foco no desenho), que a repõe pela marca Redraw. Quantas mesas.
    /// </summary>
    internal static int MostrarMesas(Document documento, Guid bloco)
    {
        ObjectId[] ids;
        using (documento.LockDocument()) ids = ContornosDoBloco(documento.Database, bloco);

        try
        {
            using (documento.LockDocument()) documento.Editor.SetImpliedSelection(ids);
            if (ClivusExtension.TemInterface()) documento.Editor.UpdateScreen();
        }
        catch (Exception erro)
        {
            // Fora de comando o AutoCAD pode recusar; o comando abaixo faz o mesmo.
            RegistroDeDiagnostico.Registrar("A seleção implícita direta das mesas do bloco foi recusada.", erro);
        }

        // Sem interface (Core Console) o SendStringToExecute fora de comando derruba o processo.
        if (ClivusExtension.TemInterface() && ids.Length > 0) JanelaEletrica.Comando(documento, PluginInfo.ComandoNumeracaoMostrar, bloco.ToString("D"));
        return ids.Length;
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

    // ------------------------------------------------------- 15.4 gerar

    /// <summary>
    /// Gerar e apagar em três alcances, uma linha cada (05/10/2026, Renan: "o
    /// gerar está por inversor e não por bloco, deveria ter as duas opções"):
    /// usina inteira, um bloco, um inversor. A conta é sempre a da usina
    /// inteira; o alcance só escolhe quais strings recebem a tag.
    /// </summary>
    private UIElement SecaoGerar()
    {
        for (var i = 0; i < 3; i++) _linhasDeGerar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _linhasDeGerar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++) _linhasDeGerar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        LinhaDeGerar(0, Tr.T("Usina inteira:"), null,
            Tr.T("Varre a usina na ordem dos blocos e grava a tag em cada string alocada, com o texto no desenho. String sem inversor fica sem tag."), () => Gerar(NumberingScope.All),
            Tr.T("Apaga as tags de todas as strings. Strings, alocação e traçado não mudam."), () => Apagar(NumberingScope.All));

        LinhaDeGerar(1, Tr.T("Bloco:"), _blocoDeGerar,
            Tr.T("Numera de novo só as strings das mesas do bloco (depois de mudar o sentido dele). As outras não mudam."), () => PorBloco(Gerar),
            Tr.T("Apaga só as tags das strings das mesas do bloco. As outras não mudam."), () => PorBloco(Apagar));

        LinhaDeGerar(2, Tr.T("Inversor:"), _inversor,
            Tr.T("Numera de novo só as strings do inversor. Como o sequencial recomeça em cada inversor, os outros não mudam."), () => PorInversor(Gerar),
            Tr.T("Apaga só as tags das strings do inversor. O vínculo não muda."), () => PorInversor(Apagar));

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(_linhasDeGerar);
        return new GroupBox { Header = Tr.T("Gerar"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    /// <summary>Uma linha da seção Gerar: rótulo, a caixa do alcance (se há) e Gerar e Apagar, alinhados em colunas.</summary>
    private void LinhaDeGerar(int linha, string rotulo, ComboBox? caixa, string dicaGerar, Action gerar, string dicaApagar, Action apagar)
    {
        var texto = Rotulo(rotulo);
        texto.Margin = new Thickness(0, 0, 8, 4);
        Grid.SetRow(texto, linha);
        _linhasDeGerar.Children.Add(texto);

        if (caixa is not null)
        {
            caixa.Margin = new Thickness(0, 0, 8, 4);
            ToolTipService.SetShowOnDisabled(caixa, true);
            Grid.SetRow(caixa, linha);
            Grid.SetColumn(caixa, 1);
            _linhasDeGerar.Children.Add(caixa);
        }

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Tag = caixa };
        Botao(botoes, Tr.T("Gerar"), dicaGerar, gerar);
        Botao(botoes, Tr.T("Apagar"), dicaApagar, apagar);
        ToolTipService.SetShowOnDisabled(botoes, true);
        Grid.SetRow(botoes, linha);
        Grid.SetColumn(botoes, caixa is null ? 1 : 2);
        if (caixa is null) Grid.SetColumnSpan(botoes, 2);
        _linhasDeGerar.Children.Add(botoes);
    }

    /// <summary>Liga ou desliga a linha da caixa (sem bloco ou sem inversor não há o que escolher), com o porquê na dica.</summary>
    private void HabilitarLinha(ComboBox caixa, bool liga, string dicaDesligada, string dicaLigada)
    {
        caixa.IsEnabled = liga;
        caixa.ToolTip = liga ? dicaLigada : dicaDesligada;

        foreach (var botoes in _linhasDeGerar.Children.OfType<StackPanel>().Where(p => ReferenceEquals(p.Tag, caixa)))
        {
            botoes.IsEnabled = liga;
            if (liga) botoes.ClearValue(ToolTipProperty);
            else botoes.ToolTip = dicaDesligada;
        }
    }

    /// <summary>A caixa de blocos da linha Bloco, na ordem da lista, mantendo o escolhido.</summary>
    private void MostrarBlocosDeGerar(ScanSetup varredura, Guid? manter)
    {
        var anterior = manter ?? ((_blocoDeGerar.SelectedItem as ComboBoxItem)?.Tag as NumberingBlock)?.Id;
        _blocoDeGerar.Items.Clear();

        foreach (var bloco in varredura.Blocks)
        {
            var item = new ComboBoxItem { Content = bloco.Name, Tag = bloco };
            _blocoDeGerar.Items.Add(item);
            if (bloco.Id == anterior) _blocoDeGerar.SelectedItem = item;
        }

        if (_blocoDeGerar.SelectedItem is null && _blocoDeGerar.Items.Count > 0) _blocoDeGerar.SelectedIndex = 0;
        HabilitarLinha(_blocoDeGerar, _blocoDeGerar.Items.Count > 0,
            Tr.T("Sem blocos: crie um bloco acima (Novo bloco)."),
            Tr.T("O bloco de Gerar e Apagar desta linha (os blocos da lista acima)."));
    }

    private void Gerar(NumberingScope alcance)
    {
        _desenho = null;
        Fazer(() => GerarNaTela(_documento, alcance));
        AtualizarTela();
    }

    private void Apagar(NumberingScope alcance)
    {
        _desenho = null;
        Fazer(() => ApagarNaTela(_documento, alcance));
        AtualizarTela();
    }

    /// <summary>O Gerar da aba (o botão e o nível 2 pelo mesmo caminho, fora de comando): o relatório numa frase.</summary>
    internal static string GerarNaTela(Document documento, NumberingScope alcance) =>
        EscritaForaDeComando.Fazer(documento, () => string.Join("\n", NumeracaoDesenho.Gerar(documento.Database, alcance)));

    /// <summary>O Apagar da aba, pelo mesmo caminho.</summary>
    internal static string ApagarNaTela(Document documento, NumberingScope alcance) =>
        EscritaForaDeComando.Fazer(documento, () => NumeracaoDesenho.Apagar(documento.Database, alcance));

    private void PorBloco(Action<NumberingScope> acao)
    {
        if ((_blocoDeGerar.SelectedItem as ComboBoxItem)?.Tag is NumberingBlock bloco) acao(NumberingScope.OfBlock(bloco.Id));
        else Avisar(Tr.T("Sem blocos: crie um bloco acima (Novo bloco)."), erro: true);
    }

    private void PorInversor(Action<NumberingScope> acao)
    {
        if ((_inversor.SelectedItem as ComboBoxItem)?.Tag is Inverter inversor) acao(NumberingScope.OfInverter(inversor.Id));
        else Avisar(Tr.T("Escolha um inversor (os inversores vêm do cadastro da aba Inversor)."), erro: true);
    }

    private void MostrarInversores()
    {
        var anterior = ((_inversor.SelectedItem as ComboBoxItem)?.Tag as Inverter)?.Id;
        var lido = ElectricalStore.Inverters(_documento.Database);

        _inversor.Items.Clear();
        foreach (var inversor in lido.Items)
        {
            var item = new ComboBoxItem { Content = inversor.Name, Tag = inversor };
            _inversor.Items.Add(item);
            if (inversor.Id == anterior) _inversor.SelectedItem = item;
        }

        if (_inversor.SelectedItem is null && _inversor.Items.Count > 0) _inversor.SelectedIndex = 0;
        HabilitarLinha(_inversor, _inversor.Items.Count > 0,
            Tr.T("Sem inversores: cadastre na aba Inversor."),
            Tr.T("O inversor de Gerar e Apagar desta linha (na ordem do cadastro)."));

        // Com mais de um inversor o modelo precisa de {I}: o exemplo confere com o cadastro de agora.
        _inversores = StringNumbering.CountInverters(lido.Items);
        Previa();
        if (lido.Problem is not null) Avisar(lido.Problem, erro: true);
    }

    private static void AtualizarTela()
    {
        try
        {
            AcadApp.UpdateScreen();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui atualizar a tela depois da numeração.", erro);
        }
    }

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
        _desenho ??= LerDesenho(_documento.Database);
        MostrarVarredura(varredura, varredura.CountStrings(_desenho.Value.Strings, _desenho.Value.Modulos), manter);
        MostrarInversores();
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
