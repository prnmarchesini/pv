using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A aba Subestação (etapa 12): as unidades consumidoras, o cadastro da
/// escolhida e a tabela dos trafos dela (o vínculo mora no trafo,
/// <see cref="Transformer.ConsumerUnit"/>).
/// </summary>
internal sealed class AbaSubestacao : AbaEletrica
{
    private readonly ListBox _lista = new() { MinWidth = 380 };
    private readonly TextBlock _codigo = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBox _nome, _largura, _comprimento, _altura;
    private readonly StackPanel _trafos = new();
    private readonly WrapPanel _acoes = new() { Margin = new Thickness(0, 4, 0, 0) };

    internal AbaSubestacao(Document documento) : base(documento)
    {
        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Adicionar subestação"), Tr.T("Pergunta se é uma subestação compartilhada (C1, C2...) ou várias unitárias (U1, U2...)."), Adicionar);
        Botao(botoes, Tr.T("Apagar"), Tr.T("Tira a subestação do cadastro e o retângulo dela do campo. Os trafos dela ficam sem subestação; nada mais é apagado."), Apagar);

        var grade = Grade();
        var linha = grade.RowDefinitions.Count;
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var rotulo = new TextBlock { Text = Tr.T("Código"), Margin = new Thickness(0, 0, 8, 4) };
        Grid.SetRow(rotulo, linha);
        Grid.SetRow(_codigo, linha);
        Grid.SetColumn(_codigo, 1);
        grade.Children.Add(rotulo);
        grade.Children.Add(_codigo);

        _nome = Campo(grade, Tr.T("Nome"), Tr.T("O nome da subestação; é a tag que aparece em campo."));
        _largura = Campo(grade, Tr.T("Largura (m)"), Tr.T("Medida em X do retângulo em campo."));
        _comprimento = Campo(grade, Tr.T("Comprimento (m)"), Tr.T("Medida em Y do retângulo em campo."));
        _altura = Campo(grade, Tr.T("Altura (m)"), Tr.T("Altura do retângulo 3D (a base flutua 0,80 m acima do terreno)."));

        Botao(_acoes, Tr.T("Salvar alterações"), Tr.T("Grava o nome e o tamanho da subestação escolhida."), Salvar);
        Botao(_acoes, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda."), () =>
        {
            if (Escolhida is { } uc) AlocarEmCampo(uc.Id);
        });

        var formulario = new StackPanel();
        formulario.Children.Add(grade);
        formulario.Children.Add(_acoes);
        formulario.Children.Add(new TextBlock { Text = Tr.T("Trafos desta subestação"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
        formulario.Children.Add(_trafos);

        var esquerda = new DockPanel();
        DockPanel.SetDock(botoes, Dock.Bottom);
        esquerda.Children.Add(botoes);
        esquerda.Children.Add(_lista);

        Children.Add(DuasColunas(esquerda, formulario));

        _lista.SelectionChanged += (_, _) =>
        {
            try { Preencher(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar a subestação escolhida.", erro); }
        };
    }

    private ConsumerUnit? Escolhida => (_lista.SelectedItem as ListBoxItem)?.Tag as ConsumerUnit;

    private ElectricalSetup _setup = new();

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        _setup = setup;
        var anterior = Escolhida?.Id;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);

        _lista.Items.Clear();
        foreach (var uc in setup.Units)
        {
            var item = new ListBoxItem { Content = ComCampo(Descrever(setup, uc), emCampo.Contains((EquipmentKind.ConsumerUnit, uc.Id))), Tag = uc };
            _lista.Items.Add(item);
            if (uc.Id == anterior) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
        Preencher();

        if (problema is not null) Avisar(problema, erro: true);
        else if (_lista.Items.Count == 0) Avisar(Tr.T("Nenhuma subestação ainda: use Adicionar subestação."));
    }

    internal static string Descrever(ElectricalSetup setup, ConsumerUnit uc)
    {
        var trafos = setup.TransformersOf(uc.Id).Select(t => t.Nickname).ToList();
        var modo = uc.Mode == ConsumerUnitMode.Shared ? Tr.T("compartilhada") : Tr.T("unitária");
        return trafos.Count == 0
            ? Tr.F("{0} — {1} — {2} — sem trafo", uc.Code, uc.Name, modo)
            : Tr.F("{0} — {1} — {2} — {3}", uc.Code, uc.Name, modo, string.Join(", ", trafos));
    }

    private void Preencher()
    {
        var uc = Escolhida;
        foreach (var caixa in new[] { _nome, _largura, _comprimento, _altura }) caixa.IsEnabled = uc is not null;
        _acoes.IsEnabled = uc is not null;
        _trafos.Children.Clear();

        if (uc is null)
        {
            _codigo.Text = string.Empty;
            foreach (var caixa in new[] { _nome, _largura, _comprimento, _altura }) caixa.Text = string.Empty;
            return;
        }

        _codigo.Text = uc.Mode == ConsumerUnitMode.Shared ? Tr.F("{0} (compartilhada)", uc.Code) : Tr.F("{0} (unitária)", uc.Code);
        _nome.Text = uc.Name;
        _largura.Text = Numero(uc.Size.Width);
        _comprimento.Text = Numero(uc.Size.Length);
        _altura.Text = Numero(uc.Size.Height);

        if (_setup.Transformers.Count == 0)
        {
            _trafos.Children.Add(new TextBlock { Text = Tr.T("Nenhum trafo cadastrado: cadastre na aba Transformador."), TextWrapping = TextWrapping.Wrap });
            return;
        }

        // A tabela: um trafo por linha. Marcado = desta subestação; trafo de
        // outra fica travado (solte lá antes). A unitária tem um trafo só.
        var cheia = uc.Mode == ConsumerUnitMode.Unitary && _setup.TransformersOf(uc.Id).Count > 0;

        foreach (var t in _setup.Transformers)
        {
            var dona = t.ConsumerUnit == Guid.Empty ? null : _setup.FindUnit(t.ConsumerUnit);
            var deOutra = dona is not null && dona.Id != uc.Id;
            var dela = dona?.Id == uc.Id;

            var caixa = new CheckBox
            {
                Content = deOutra ? Tr.F("{0} — {1} (em {2})", t.Nickname, t.Name, dona!.Code) : Tr.F("{0} — {1}", t.Nickname, t.Name),
                IsChecked = dela,
                IsEnabled = !deOutra && (dela || !cheia),
                Margin = new Thickness(0, 0, 0, 3),
                ToolTip = deOutra ? Tr.F("Este trafo é de {0}: solte lá antes de ligar aqui.", dona!.Code)
                    : !dela && cheia ? Tr.T("Subestação unitária: um trafo só. Solte o dela antes.")
                    : Tr.T("Marque para ligar o trafo a esta subestação."),
            };

            var trafo = t;
            caixa.Click += (_, _) =>
            {
                try
                {
                    if (caixa.IsChecked == true) Ligar(uc, trafo);
                    else Soltar(trafo);
                }
                catch (Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha ao ligar ou soltar o trafo da subestação.", erro);
                    Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
                }
            };

            _trafos.Children.Add(caixa);
        }
    }

    private void Ligar(ConsumerUnit uc, Transformer trafo)
    {
        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.LinkTransformer(uc.Id, trafo.Id));
            return porque is null ? Tr.F("{0} ligado a {1}.", trafo.Nickname, uc.Code) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não liguei: {0}.", porque), erro: true);
    }

    private void Soltar(Transformer trafo) =>
        Fazer(() =>
        {
            ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.UnlinkTransformer(trafo.Id));
            return Tr.F("{0} solto da subestação.", trafo.Nickname);
        });

    private void Adicionar()
    {
        var pergunta = new JanelaDeNovaSubestacao();
        if (AcadApp.ShowModalWindow(pergunta) != true) return;

        ConsumerUnit? primeira = null;
        Fazer(() =>
        {
            if (pergunta.Unitarias is not { } quantas)
            {
                primeira = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddSharedUnit());
                return Tr.F("{0} criada.", primeira.Code);
            }

            var novas = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddUnitaryUnits(quantas));
            primeira = novas[0];
            return Tr.F("{0} subestação(ões) unitária(s) criada(s): {1} a {2}.", novas.Count, novas[0].Code, novas[^1].Code);
        });
        Selecionar(primeira?.Id);
    }

    private void Selecionar(Guid? id)
    {
        foreach (ListBoxItem item in _lista.Items)
            if (item.Tag is ConsumerUnit u && u.Id == id) _lista.SelectedItem = item;
    }

    private void Salvar()
    {
        if (Escolhida is not { } uc)
        {
            Avisar(Tr.T("Escolha uma subestação na lista."), erro: true);
            return;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;

        string? porque = null;
        Fazer(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => RedesenharSeDeuCerto(s, s.EditUnit(uc.Id, _nome.Text, tamanho), EquipmentKind.ConsumerUnit, uc.Id));
            return porque is null ? Tr.F("{0} salva.", uc.Code) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
    }

    private void Apagar()
    {
        if (Escolhida is not { } uc)
        {
            Avisar(Tr.T("Escolha uma subestação na lista."), erro: true);
            return;
        }

        Fazer(() =>
        {
            var soltos = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveUnit(uc.Id)) ?? 0;
            EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.ConsumerUnit, uc.Id);
            return Tr.F("{0} apagada do cadastro; {1} trafo(s) ficaram sem subestação.", uc.Code, soltos);
        });
    }
}
