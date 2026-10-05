using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Transformador (etapa 13): a lista dos trafos, o cadastro do
/// escolhido (livre ou a partir de um padrão), a UC a que ele pertence e a
/// posição em campo.
/// <para>
/// O texto das caixas vira número em <see cref="TransformerForm"/> (o mesmo
/// caminho do nível 2). O que foi digitado e não salvo sobrevive à releitura
/// da janela (troca de aba, volta do Alocar em campo, Salvar recusado)
/// enquanto o trafo no desenho não mudar (reprovação de 05/10/2026: "não
/// mostra a potência depois de salvo").
/// </para>
/// </summary>
internal sealed class AbaTransformador : AbaEletrica
{
    private readonly ListBox _lista = new() { MinWidth = 380 };
    private readonly ComboBox _padroes = new() { Height = 26, MinWidth = 250, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _nome, _apelido, _entrada, _saida, _potencia, _fatorK, _impedancia, _notas, _largura, _comprimento, _altura;
    private readonly ComboBox _uc;

    private ElectricalSetup _setup = new();

    /// <summary>O trafo como estava no desenho quando o formulário foi preenchido.</summary>
    private Transformer? _preenchido;

    internal AbaTransformador(Document documento) : base(documento)
    {
        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Novo trafo"), Tr.T("Cria um trafo em branco (Trafo 1, apelido T1...)."), () => Novo(null));
        botoes.Children.Add(_padroes);
        Botao(botoes, Tr.T("Novo do padrão"), Tr.T("Cria um trafo com os campos do padrão escolhido ao lado."), () =>
        {
            if ((_padroes.SelectedItem as ComboBoxItem)?.Tag is TransformerTemplate padrao) Novo(padrao);
            else Avisar(Tr.T("Escolha um padrão na lista ao lado."), erro: true);
        });
        Botao(botoes, Tr.T("Apagar"), Tr.T("Tira o trafo do cadastro e o retângulo dele do campo. Os inversores do skid dele ficam sem trafo; nada mais é apagado."), Apagar);

        foreach (var p in ElectricalDefaults.TransformerTemplates) _padroes.Items.Add(new ComboBoxItem { Content = p.Describe(), Tag = p });
        _padroes.SelectedIndex = 1;

        var grade = Grade();
        _nome = Campo(grade, Tr.T("Transformador"), Tr.T("O nome do trafo (descrição livre)."));
        _apelido = Campo(grade, Tr.T("Apelido (tag)"), Tr.T("A tag que aparece em campo e na numeração (T1, T2...)."));
        _uc = Escolha(grade, Tr.T("Subestação (UC)"), Tr.T("A unidade consumidora a que o trafo pertence. Trafo de outra UC: escolha (nenhuma) e salve antes; a unitária tem um trafo só."));
        _entrada = Campo(grade, Tr.T("Tensão de entrada (V)"), Tr.T("Lado dos inversores (baixa tensão)."));
        _saida = Campo(grade, Tr.T("Tensão de saída (V)"), Tr.T("Lado da rede (média tensão), ex. 13800."));
        _potencia = Campo(grade, Tr.T("Potência (kVA)"));
        _fatorK = Campo(grade, Tr.T("Fator K"));
        _impedancia = Campo(grade, Tr.T("Impedância (%)"));
        _notas = Campo(grade, Tr.T("Observações"), Tr.T("Outros campos elétricos: para-raios, ligação, refrigeração..."));
        _largura = Campo(grade, Tr.T("Largura (m)"), Tr.T("Medida em X do retângulo em campo."));
        _comprimento = Campo(grade, Tr.T("Comprimento (m)"), Tr.T("Medida em Y do retângulo em campo."));
        _altura = Campo(grade, Tr.T("Altura (m)"), Tr.F("Altura do retângulo 3D (a base flutua {0:0.00} m acima do terreno).", Clivus.Geo.EquipmentFootprint.FloatHeight));

        var acoes = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        Botao(acoes, Tr.T("Salvar alterações"), Tr.T("Grava o cadastro do trafo escolhido no desenho."), () => Salvar());
        Botao(acoes, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda. O que não foi salvo é salvo antes."), () =>
        {
            if (Escolhido is not { } t)
            {
                Avisar(Tr.T("Escolha um trafo na lista."), erro: true);
                return;
            }

            // O retângulo nasce com a dimensão gravada: salva antes o que foi digitado.
            if (Sujo() && !Salvar()) return;
            AlocarEmCampo(t.Id);
        });

        var formulario = new StackPanel();
        formulario.Children.Add(grade);
        formulario.Children.Add(acoes);

        var esquerda = new DockPanel();
        DockPanel.SetDock(botoes, Dock.Bottom);
        esquerda.Children.Add(botoes);
        esquerda.Children.Add(_lista);

        Children.Add(DuasColunas(esquerda, formulario));

        _lista.SelectionChanged += (_, _) =>
        {
            try { Preencher(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar o trafo escolhido.", erro); }
        };
    }

    private Transformer? Escolhido => (_lista.SelectedItem as ListBoxItem)?.Tag as Transformer;

    private TextBox[] Caixas => [_nome, _apelido, _entrada, _saida, _potencia, _fatorK, _impedancia, _notas, _largura, _comprimento, _altura];

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        var anterior = Escolhido?.Id;

        // O que foi digitado e não salvo volta para a tela se o trafo não mudou no desenho.
        var antes = _preenchido;
        var digitado = antes is not null && Sujo() ? (Textos: TextosDaTela(), Uc: UcDaTela()) : default((TransformerFormTexts Textos, Guid Uc)?);

        _setup = setup;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);

        _lista.Items.Clear();
        foreach (var t in setup.Transformers)
        {
            var linha = Descrever(t);
            if (setup.FindUnit(t.ConsumerUnit) is { } uc) linha = Tr.F("{0} — em {1}", linha, uc.Code);
            var item = new ListBoxItem { Content = ComCampo(linha, emCampo.Contains((EquipmentKind.Transformer, t.Id))), Tag = t };
            _lista.Items.Add(item);
            if (t.Id == anterior) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
        Preencher();

        // Só volta o que ainda difere do gravado: "13800,0" salvo como 13800 não
        // deixa o formulário "sujo" para sempre.
        if (digitado is { } d && Escolhido is { } agora && agora == antes
            && (d.Uc != agora.ConsumerUnit || TransformerForm.Read(agora, d.Textos, out _) != agora))
        {
            Mostrar(d.Textos);
            EscolherUc(d.Uc);
        }

        if (problema is not null) Avisar(problema, erro: true);
        else if (_lista.Items.Count == 0) Avisar(Tr.T("Nenhum trafo ainda: use Novo trafo ou Novo do padrão."));
    }

    internal static string Descrever(Transformer t) =>
        Tr.F("{0} — {1} — {2:#,0} kVA, {3:#,0} V / {4:#,0} V", t.Nickname, t.Name, t.PowerKva, t.InputVoltage, t.OutputVoltage);

    private void Preencher()
    {
        var t = Escolhido;
        _preenchido = t;
        foreach (var caixa in Caixas) caixa.IsEnabled = t is not null;
        _uc.IsEnabled = t is not null;
        _uc.Items.Clear();

        if (t is null)
        {
            foreach (var caixa in Caixas) caixa.Text = string.Empty;
            return;
        }

        Mostrar(TransformerForm.Texts(t));

        // A UC: (nenhuma), as do cadastro (a que não pode receber o trafo agora
        // fica cinza, com o porquê) e, se o vínculo aponta para UC que sumiu, ela.
        _uc.Items.Add(new ComboBoxItem { Content = Tr.T("(nenhuma)"), Tag = Guid.Empty });
        foreach (var o in _setup.UnitChoices(t.Id))
        {
            var bloco = _setup.FindSubstation(o.Unit.Substation);
            var texto = o.Unit.Mode == ConsumerUnitMode.Unitary ? Tr.F("{0} — {1} (unitária)", o.Unit.Code, o.Unit.Name)
                : bloco is not null ? Tr.F("{0} — {1} (em {2})", o.Unit.Code, o.Unit.Name, bloco.Name)
                : Tr.F("{0} — {1}", o.Unit.Code, o.Unit.Name);
            _uc.Items.Add(new ComboBoxItem { Content = texto, Tag = o.Unit.Id, IsEnabled = o.Allowed, ToolTip = o.Reason });
        }

        if (t.ConsumerUnit != Guid.Empty && _setup.FindUnit(t.ConsumerUnit) is null)
            _uc.Items.Add(new ComboBoxItem { Content = Tr.T("(subestação que não está mais no cadastro)"), Tag = t.ConsumerUnit });

        EscolherUc(t.ConsumerUnit);
    }

    private void Mostrar(TransformerFormTexts x)
    {
        _nome.Text = x.Name;
        _apelido.Text = x.Nickname;
        _entrada.Text = x.InputVoltage;
        _saida.Text = x.OutputVoltage;
        _potencia.Text = x.PowerKva;
        _fatorK.Text = x.KFactor;
        _impedancia.Text = x.ImpedancePercent;
        _notas.Text = x.Notes;
        _largura.Text = x.Width;
        _comprimento.Text = x.Length;
        _altura.Text = x.Height;
    }

    private TransformerFormTexts TextosDaTela() =>
        new(_nome.Text, _apelido.Text, _entrada.Text, _saida.Text, _potencia.Text, _fatorK.Text, _impedancia.Text, _notas.Text, _largura.Text, _comprimento.Text, _altura.Text);

    private Guid UcDaTela() => (_uc.SelectedItem as ComboBoxItem)?.Tag is Guid g ? g : Guid.Empty;

    private void EscolherUc(Guid uc)
    {
        foreach (ComboBoxItem item in _uc.Items)
            if (item.Tag is Guid g && g == uc) _uc.SelectedItem = item;
        if (_uc.SelectedItem is null && _uc.Items.Count > 0) _uc.SelectedIndex = 0;
    }

    /// <summary>O formulário tem o que não foi salvo.</summary>
    private bool Sujo() =>
        _preenchido is { } t && (TextosDaTela() != TransformerForm.Texts(t) || UcDaTela() != t.ConsumerUnit);

    private void Novo(TransformerTemplate? padrao)
    {
        Transformer? novo = null;
        Fazer(() =>
        {
            novo = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddTransformer(padrao));
            return Tr.F("{0} criado.", novo.Nickname);
        });
        Selecionar(novo?.Id);
    }

    private void Selecionar(Guid? id)
    {
        foreach (ListBoxItem item in _lista.Items)
            if (item.Tag is Transformer t && t.Id == id) _lista.SelectedItem = item;
    }

    /// <summary>Grava o formulário (campos e UC, tudo ou nada). Se gravou.</summary>
    private bool Salvar()
    {
        if (Escolhido is not { } t)
        {
            Avisar(Tr.T("Escolha um trafo na lista."), erro: true);
            return false;
        }

        if (TransformerForm.Read(t, TextosDaTela(), out var naoLeu) is not { } editado)
        {
            Avisar(Tr.F("Não salvei: {0}.", naoLeu), erro: true);
            return false;
        }

        var uc = UcDaTela();
        string? porque = null;
        var gravou = Gravar(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.SaveTransformer(editado, uc));
            if (porque is not null) return null;

            EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.Transformer, t.Id);
            return Tr.F("{0} salvo.", editado.Nickname.Trim());
        });

        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
        return gravou;
    }

    private void Apagar()
    {
        if (Escolhido is not { } t)
        {
            Avisar(Tr.T("Escolha um trafo na lista."), erro: true);
            return;
        }

        int? soltos = null;
        Fazer(() =>
        {
            soltos = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveTransformer(t.Id));
            if (soltos is null) return null;
            EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.Transformer, t.Id);
            return Tr.F("{0} apagado do cadastro; {1} inversor(es) ficaram sem trafo.", t.Nickname, soltos);
        });
        if (soltos is null) Avisar(Tr.T("Esse trafo não está mais no cadastro."), erro: true);
    }
}
