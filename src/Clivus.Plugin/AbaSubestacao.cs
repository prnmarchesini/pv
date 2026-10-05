using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A aba Subestação (etapa 12). A lista tem as subestações físicas: o bloco
/// compartilhado (um cubículo em campo com as UCs C1, C2... dentro) e as
/// unitárias (U1, U2..., cada uma bloco e UC ao mesmo tempo). Escolhido o
/// bloco, o formulário mostra o nome e a dimensão dele e a lista das UCs dele
/// (nome de cada uma e os trafos de cada uma); escolhida uma unitária, o nome,
/// a dimensão e o trafo dela. O vínculo mora no trafo
/// (<see cref="Transformer.ConsumerUnit"/>) e na UC (<see cref="ConsumerUnit.Substation"/>).
/// </summary>
internal sealed class AbaSubestacao : AbaEletrica
{
    private readonly ListBox _lista = new() { MinWidth = 380 };
    private readonly TextBlock _codigo = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _nome, _largura, _comprimento, _altura;
    private readonly WrapPanel _acoes = new() { Margin = new Thickness(0, 4, 0, 0) };

    // A unitária: os trafos dela.
    private readonly StackPanel _daUnitaria = new();
    private readonly StackPanel _trafosDaUnitaria = new();

    // O bloco compartilhado: as UCs dele e, da UC escolhida, o nome e os trafos.
    private readonly StackPanel _doBloco = new();
    private readonly ListBox _ucs = new() { MinHeight = 70, MaxHeight = 140, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBox _nomeDaUc = new() { Height = 24, MinWidth = 180, Margin = new Thickness(0, 0, 6, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _tituloDosTrafosDaUc = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 4) };
    private readonly StackPanel _trafosDaUc = new();

    private ElectricalSetup _setup = new();

    internal AbaSubestacao(Document documento) : base(documento)
    {
        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Adicionar subestação"), Tr.T("Pergunta se é a subestação compartilhada (um bloco com as UCs C1, C2... dentro) ou várias unitárias (U1, U2...)."), Adicionar);
        Botao(botoes, Tr.T("Apagar"), Tr.T("Tira a subestação do cadastro e o retângulo dela do campo (a compartilhada leva as UCs dela junto). Os trafos ficam sem subestação; nada mais é apagado."), Apagar);

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
        _altura = Campo(grade, Tr.T("Altura (m)"), Tr.F("Altura do retângulo 3D (a base flutua {0:0.00} m acima do terreno).", Clivus.Geo.EquipmentFootprint.FloatHeight));

        Botao(_acoes, Tr.T("Salvar alterações"), Tr.T("Grava o nome e o tamanho da subestação escolhida."), () => Salvar());
        Botao(_acoes, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido; o vínculo não muda. O que não foi salvo é salvo antes."), () =>
        {
            if (EscolhidaId is not { } id) return;
            if (Sujo() && !Salvar()) return;
            AlocarEmCampo(id);
        });

        // A unitária: os trafos dela (um só).
        _daUnitaria.Children.Add(new TextBlock { Text = Tr.T("Trafo desta subestação"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
        _daUnitaria.Children.Add(_trafosDaUnitaria);

        // O bloco: a lista das UCs dele.
        _doBloco.Children.Add(new TextBlock { Text = Tr.T("Unidades consumidoras desta subestação"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
        _doBloco.Children.Add(_ucs);
        var botoesDaUc = new WrapPanel();
        Botao(botoesDaUc, Tr.T("Adicionar UC"), Tr.T("Cria a próxima UC (C1, C2...) dentro desta subestação."), AdicionarUc);
        Botao(botoesDaUc, Tr.T("Apagar UC"), Tr.T("Tira a UC escolhida da subestação e do cadastro. Os trafos dela ficam sem subestação; nada mais é apagado."), ApagarUc);
        _doBloco.Children.Add(botoesDaUc);
        var nomeDaUc = new WrapPanel();
        nomeDaUc.Children.Add(new TextBlock { Text = Tr.T("Nome da UC:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        nomeDaUc.Children.Add(_nomeDaUc);
        Botao(nomeDaUc, Tr.T("Salvar nome"), Tr.T("Grava o nome da UC escolhida na lista acima."), SalvarUc);
        _doBloco.Children.Add(nomeDaUc);
        _doBloco.Children.Add(_tituloDosTrafosDaUc);
        _doBloco.Children.Add(_trafosDaUc);

        var formulario = new StackPanel();
        formulario.Children.Add(grade);
        formulario.Children.Add(_acoes);
        formulario.Children.Add(_daUnitaria);
        formulario.Children.Add(_doBloco);

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

        _ucs.SelectionChanged += (_, _) =>
        {
            try { PreencherUc(); }
            catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar a UC escolhida.", erro); }
        };
    }

    private Substation? Bloco => (_lista.SelectedItem as ListBoxItem)?.Tag as Substation;

    private ConsumerUnit? Unitaria => (_lista.SelectedItem as ListBoxItem)?.Tag as ConsumerUnit;

    private Guid? EscolhidaId => Bloco?.Id ?? Unitaria?.Id;

    private ConsumerUnit? UcEscolhida => (_ucs.SelectedItem as ListBoxItem)?.Tag as ConsumerUnit;

    /// <summary>O nome e o tamanho como estavam no desenho quando o formulário foi preenchido.</summary>
    private (Guid Id, string Nome, EquipmentSize Tamanho)? _preenchida;

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        var anterior = EscolhidaId;
        var ucAnterior = UcEscolhida?.Id;

        // O nome e o tamanho digitados e não salvos voltam se a subestação não mudou no desenho.
        var antes = _preenchida;
        (string Nome, string Largura, string Comprimento, string Altura)? digitado = Sujo() ? (_nome.Text, _largura.Text, _comprimento.Text, _altura.Text) : null;
        (ConsumerUnit Uc, string Nome)? nomeDaUc = UcEscolhida is { } e && _nomeDaUc.Text != e.Name ? (e, _nomeDaUc.Text) : null;

        _setup = setup;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);

        _lista.Items.Clear();
        foreach (var b in setup.Substations)
            Linha(ComCampo(DescreverBloco(setup, b), emCampo.Contains((EquipmentKind.ConsumerUnit, b.Id))), b, b.Id);
        foreach (var u in setup.Units.Where(u => u.Mode == ConsumerUnitMode.Unitary))
            Linha(ComCampo(Descrever(setup, u), emCampo.Contains((EquipmentKind.ConsumerUnit, u.Id))), u, u.Id);

        void Linha(string texto, object tag, Guid id)
        {
            var item = new ListBoxItem { Content = texto, Tag = tag };
            _lista.Items.Add(item);
            if (id == anterior) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
        Preencher(ucAnterior);

        if (digitado is { } d && antes is { } a && _preenchida == a)
        {
            _nome.Text = d.Nome;
            _largura.Text = d.Largura;
            _comprimento.Text = d.Comprimento;
            _altura.Text = d.Altura;
        }

        // O nome da UC digitado e não salvo volta se a UC não mudou no desenho
        // (marcar um trafo relê a janela antes do "Salvar nome").
        if (nomeDaUc is { } n && UcEscolhida is { } ucAgora && ucAgora == n.Uc) _nomeDaUc.Text = n.Nome;

        // Desenho antigo: as compartilhadas sem bloco foram postas num bloco na
        // leitura; retângulo de UC compartilhada (formato antigo) é sobra.
        var avisos = new List<string>();
        if (setup.MigratedUnits > 0)
            avisos.Add(Tr.F("{0} UC(s) compartilhada(s) do formato antigo foram postas na {1}; a próxima alteração grava isso no desenho.", setup.MigratedUnits, setup.Substations[0].Name));
        var sobras = setup.Units.Where(u => u.Mode == ConsumerUnitMode.Shared && emCampo.Contains((EquipmentKind.ConsumerUnit, u.Id))).Select(u => u.Code).ToList();
        if (sobras.Count > 0)
            avisos.Add(Tr.F("Retângulo antigo de UC compartilhada em campo ({0}): agora quem vai para o campo é o bloco; aloque o bloco e apague o retângulo antigo.", string.Join(", ", sobras)));

        if (problema is not null) Avisar(problema, erro: true);
        else if (avisos.Count > 0) Avisar(string.Join("\n", avisos), erro: sobras.Count > 0);
        else if (_lista.Items.Count == 0) Avisar(Tr.T("Nenhuma subestação ainda: use Adicionar subestação."));
    }

    internal static string DescreverBloco(ElectricalSetup setup, Substation b)
    {
        var ucs = setup.UnitsOf(b.Id).Select(u => u.Code).ToList();
        return ucs.Count == 0
            ? Tr.F("{0} — compartilhada — sem UC", b.Name)
            : Tr.F("{0} — compartilhada — {1}", b.Name, string.Join(", ", ucs));
    }

    internal static string Descrever(ElectricalSetup setup, ConsumerUnit uc)
    {
        var trafos = setup.TransformersOf(uc.Id).Select(t => t.Nickname).ToList();
        var modo = uc.Mode == ConsumerUnitMode.Shared ? Tr.T("compartilhada") : Tr.T("unitária");
        return trafos.Count == 0
            ? Tr.F("{0} — {1} — {2} — sem trafo", uc.Code, uc.Name, modo)
            : Tr.F("{0} — {1} — {2} — {3}", uc.Code, uc.Name, modo, string.Join(", ", trafos));
    }

    private void Preencher(Guid? ucParaManter = null)
    {
        var bloco = Bloco;
        var unitaria = Unitaria;
        var algum = bloco is not null || unitaria is not null;

        foreach (var caixa in new[] { _nome, _largura, _comprimento, _altura }) caixa.IsEnabled = algum;
        _acoes.IsEnabled = algum;
        _daUnitaria.Visibility = unitaria is not null ? Visibility.Visible : Visibility.Collapsed;
        _doBloco.Visibility = bloco is not null ? Visibility.Visible : Visibility.Collapsed;
        _trafosDaUnitaria.Children.Clear();

        if (!algum)
        {
            _preenchida = null;
            _codigo.Text = string.Empty;
            foreach (var caixa in new[] { _nome, _largura, _comprimento, _altura }) caixa.Text = string.Empty;
            return;
        }

        var (id, nome, tamanho) = bloco is not null ? (bloco.Id, bloco.Name, bloco.Size) : (unitaria!.Id, unitaria.Name, unitaria.Size);
        _preenchida = (id, nome, tamanho);
        _nome.Text = nome;
        _largura.Text = Numero(tamanho.Width);
        _comprimento.Text = Numero(tamanho.Length);
        _altura.Text = Numero(tamanho.Height);

        if (unitaria is not null)
        {
            _codigo.Text = Tr.F("{0} (unitária)", unitaria.Code);
            MontarTrafos(unitaria, _trafosDaUnitaria);
            return;
        }

        _codigo.Text = Tr.T("compartilhada: um bloco em campo com as UCs abaixo");

        _ucs.Items.Clear();
        foreach (var uc in _setup.UnitsOf(bloco!.Id))
        {
            var item = new ListBoxItem { Content = Descrever(_setup, uc), Tag = uc };
            _ucs.Items.Add(item);
            if (uc.Id == ucParaManter) _ucs.SelectedItem = item;
        }

        if (_ucs.SelectedItem is null && _ucs.Items.Count > 0) _ucs.SelectedIndex = 0;
        PreencherUc();
    }

    private void PreencherUc()
    {
        var uc = UcEscolhida;
        _trafosDaUc.Children.Clear();
        _nomeDaUc.IsEnabled = uc is not null;
        _nomeDaUc.Text = uc?.Name ?? string.Empty;
        _tituloDosTrafosDaUc.Text = uc is null ? Tr.T("Nenhuma UC nesta subestação: use Adicionar UC.") : Tr.F("Trafos da {0}", uc.Code);

        if (uc is not null) MontarTrafos(uc, _trafosDaUc);
    }

    /// <summary>
    /// A tabela dos trafos da UC: um por linha. Marcado = desta UC; trafo de
    /// outra fica travado (solte lá antes). A unitária tem um trafo só.
    /// </summary>
    private void MontarTrafos(ConsumerUnit uc, Panel onde)
    {
        if (_setup.Transformers.Count == 0)
        {
            onde.Children.Add(new TextBlock { Text = Tr.T("Nenhum trafo cadastrado: cadastre na aba Transformador."), TextWrapping = TextWrapping.Wrap });
            return;
        }

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

            onde.Children.Add(caixa);
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
            var soltou = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.UnlinkTransformer(trafo.Id));
            return soltou ? Tr.F("{0} solto da subestação.", trafo.Nickname) : null;
        });

    private void Adicionar()
    {
        var pergunta = new JanelaDeNovaSubestacao();
        if (AcadApp.ShowModalWindow(pergunta) != true) return;

        Guid? escolher = null;
        Fazer(() =>
        {
            if (pergunta.Unitarias is not { } quantas)
            {
                // A usina tem um bloco compartilhado só: o segundo pedido mostra o que existe.
                var criada = ConfiguracaoEletricaStore.Mudar(Documento.Database, s =>
                {
                    var (bloco, criou) = s.EnsureSharedSubstation();
                    var c = criou ? s.AddSharedUnit(bloco.Id) : null;
                    return (bloco, c);
                });

                escolher = criada.bloco.Id;
                return criada.c is { } c
                    ? Tr.F("{0} criada com a {1}; use Adicionar UC para as outras.", criada.bloco.Name, c.Code)
                    : Tr.F("A usina já tem a subestação compartilhada ({0}): adicione as UCs nela com Adicionar UC.", criada.bloco.Name);
            }

            var novas = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddUnitaryUnits(quantas));
            escolher = novas[0].Id;
            return Tr.F("{0} subestação(ões) unitária(s) criada(s): {1} a {2}.", novas.Count, novas[0].Code, novas[^1].Code);
        });
        Selecionar(escolher);
    }

    private void Selecionar(Guid? id)
    {
        foreach (ListBoxItem item in _lista.Items)
            if ((item.Tag is Substation b && b.Id == id) || (item.Tag is ConsumerUnit u && u.Id == id)) _lista.SelectedItem = item;
    }

    /// <summary>O nome ou o tamanho do formulário não foi salvo.</summary>
    private bool Sujo() =>
        _preenchida is { } p
        && (_nome.Text != p.Nome || _largura.Text != Numero(p.Tamanho.Width) || _comprimento.Text != Numero(p.Tamanho.Length) || _altura.Text != Numero(p.Tamanho.Height));

    /// <summary>Grava o nome e o tamanho do bloco ou da unitária escolhida. Se gravou.</summary>
    private bool Salvar()
    {
        if (EscolhidaId is not { } id)
        {
            Avisar(Tr.T("Escolha uma subestação na lista."), erro: true);
            return false;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return false;

        var nome = _nome.Text;
        var ehBloco = Bloco is not null;
        string? porque = null;
        var gravou = Gravar(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => ehBloco ? s.EditSubstation(id, nome, tamanho) : s.EditUnit(id, nome, tamanho));
            if (porque is not null) return null;

            EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.ConsumerUnit, id);
            return Tr.F("{0} salva.", nome.Trim());
        });

        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
        return gravou;
    }

    private void AdicionarUc()
    {
        if (Bloco is not { } bloco) return;

        ConsumerUnit? nova = null;
        Fazer(() =>
        {
            nova = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddSharedUnit(bloco.Id));
            return Tr.F("{0} criada em {1}.", nova.Code, bloco.Name);
        });

        if (nova is null) return;
        foreach (ListBoxItem item in _ucs.Items)
            if (item.Tag is ConsumerUnit u && u.Id == nova.Id) _ucs.SelectedItem = item;
    }

    private void SalvarUc()
    {
        if (UcEscolhida is not { } uc)
        {
            Avisar(Tr.T("Escolha uma UC na lista."), erro: true);
            return;
        }

        var nome = _nomeDaUc.Text;
        string? porque = null;
        Gravar(() =>
        {
            porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditUnit(uc.Id, nome, uc.Size));
            return porque is null ? Tr.F("{0} salva.", uc.Code) : null;
        });
        if (porque is not null) Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
    }

    private void ApagarUc()
    {
        if (UcEscolhida is not { } uc)
        {
            Avisar(Tr.T("Escolha uma UC na lista."), erro: true);
            return;
        }

        int? soltos = null;
        Fazer(() =>
        {
            soltos = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveUnit(uc.Id));
            if (soltos is null) return null;

            // Retângulo do formato antigo (a UC compartilhada ia para o campo) sai junto.
            EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.ConsumerUnit, uc.Id);
            return Tr.F("{0} apagada do cadastro; {1} trafo(s) ficaram sem subestação.", uc.Code, soltos);
        });
        if (soltos is null) Avisar(Tr.T("Essa subestação não está mais no cadastro."), erro: true);
    }

    private void Apagar()
    {
        if (Bloco is { } bloco)
        {
            (int Units, int Transformers)? saiu = null;
            Fazer(() =>
            {
                var ucs = ConfiguracaoEletricaStore.Ler(Documento.Database).Setup.UnitsOf(bloco.Id).Select(u => u.Id).ToList();
                saiu = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveSubstation(bloco.Id));
                if (saiu is not { } r) return null;

                EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.ConsumerUnit, bloco.Id);
                foreach (var u in ucs) EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.ConsumerUnit, u);
                return Tr.F("{0} apagada do cadastro com {1} UC(s); {2} trafo(s) ficaram sem subestação.", bloco.Name, r.Units, r.Transformers);
            });
            if (saiu is null) Avisar(Tr.T("Essa subestação não está mais no cadastro."), erro: true);
            return;
        }

        if (Unitaria is not { } uc)
        {
            Avisar(Tr.T("Escolha uma subestação na lista."), erro: true);
            return;
        }

        int? soltos = null;
        Fazer(() =>
        {
            soltos = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveUnit(uc.Id));
            if (soltos is null) return null;
            EquipamentoEmCampo.Apagar(Documento.Database, EquipmentKind.ConsumerUnit, uc.Id);
            return Tr.F("{0} apagada do cadastro; {1} trafo(s) ficaram sem subestação.", uc.Code, soltos);
        });
        if (soltos is null) Avisar(Tr.T("Essa subestação não está mais no cadastro."), erro: true);
    }
}
