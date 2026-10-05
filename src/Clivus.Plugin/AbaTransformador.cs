using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Transformador (etapa 13): a lista dos trafos, o cadastro do
/// escolhido (livre ou a partir de um padrão) e a posição em campo.
/// </summary>
internal sealed class AbaTransformador : AbaEletrica
{
    private readonly ListBox _lista = new() { MinWidth = 380 };
    private readonly ComboBox _padroes = new() { Height = 26, MinWidth = 250, Margin = new Thickness(0, 0, 6, 6) };
    private readonly TextBox _nome, _apelido, _entrada, _saida, _potencia, _fatorK, _impedancia, _notas, _largura, _comprimento, _altura;

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
        Botao(acoes, Tr.T("Salvar alterações"), Tr.T("Grava o cadastro do trafo escolhido no desenho."), Salvar);
        Botao(acoes, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda."), () =>
        {
            if (Escolhido is { } t) AlocarEmCampo(t.Id);
            else Avisar(Tr.T("Escolha um trafo na lista."), erro: true);
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

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        var anterior = Escolhido?.Id;

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

        if (problema is not null) Avisar(problema, erro: true);
        else if (_lista.Items.Count == 0) Avisar(Tr.T("Nenhum trafo ainda: use Novo trafo ou Novo do padrão."));
    }

    internal static string Descrever(Transformer t) =>
        Tr.F("{0} — {1} — {2:#,0} kVA, {3:#,0} V / {4:#,0} V", t.Nickname, t.Name, t.PowerKva, t.InputVoltage, t.OutputVoltage);

    private void Preencher()
    {
        var t = Escolhido;
        foreach (var caixa in new[] { _nome, _apelido, _entrada, _saida, _potencia, _fatorK, _impedancia, _notas, _largura, _comprimento, _altura })
            caixa.IsEnabled = t is not null;

        if (t is null)
        {
            foreach (var caixa in new[] { _nome, _apelido, _entrada, _saida, _potencia, _fatorK, _impedancia, _notas, _largura, _comprimento, _altura }) caixa.Text = string.Empty;
            return;
        }

        _nome.Text = t.Name;
        _apelido.Text = t.Nickname;
        _entrada.Text = Numero(t.InputVoltage);
        _saida.Text = Numero(t.OutputVoltage);
        _potencia.Text = Numero(t.PowerKva);
        _fatorK.Text = Numero(t.KFactor);
        _impedancia.Text = Numero(t.ImpedancePercent);
        _notas.Text = t.Notes;
        _largura.Text = Numero(t.Size.Width);
        _comprimento.Text = Numero(t.Size.Length);
        _altura.Text = Numero(t.Size.Height);
    }

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

    private void Salvar()
    {
        if (Escolhido is not { } t)
        {
            Avisar(Tr.T("Escolha um trafo na lista."), erro: true);
            return;
        }

        if (!NumberInput.TryParseLarge(_entrada.Text, out var entrada) || !NumberInput.TryParseLarge(_saida.Text, out var saida)
            || !NumberInput.TryParseLarge(_potencia.Text, out var potencia) || !NumberInput.TryParseMeasure(_fatorK.Text, out var k)
            || !NumberInput.TryParseMeasure(_impedancia.Text, out var z))
        {
            Avisar(Tr.T("Não consigo ler um dos números (tensões, potência, fator K, impedância)."), erro: true);
            return;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;

        var editado = t with
        {
            Name = _nome.Text, Nickname = _apelido.Text, InputVoltage = entrada, OutputVoltage = saida, PowerKva = potencia,
            KFactor = k, ImpedancePercent = z, Notes = _notas.Text, Size = tamanho,
        };

        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditTransformer(editado));
            if (porque is null) EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.Transformer, t.Id);
            return porque is null ? Tr.F("{0} salvo.", editado.Nickname.Trim()) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
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
