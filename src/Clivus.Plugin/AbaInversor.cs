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

    private ElectricalSetup _setup = new();

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

        Children.Add(new ScrollViewer { Content = modelos, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

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
            return porque is null ? Tr.F("{0} salvo.", editado.Name.Trim()) : null;
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
