using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.CombinerCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A aba Combiner da configuração elétrica (roteamento, 19.1 e 19.2): o
/// cadastro no molde do inversor e do trafo (nome = tag, entradas, dimensão,
/// inversor), a posição em campo e as strings de cada combiner. A string em
/// combiner passa a ligar no inversor da combiner (inversor -> combiner -> string).
/// </summary>
internal sealed class AbaCombiner : AbaEletrica
{
    private readonly ListBox _lista = new() { MinWidth = 380 };
    private readonly TextBox _nome, _entradas, _largura, _comprimento, _altura;
    private readonly ComboBox _inversor;

    internal AbaCombiner(Document documento) : base(documento)
    {
        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Nova combiner"), Tr.T("Cria uma combiner (CB1, CB2...) com 16 entradas, sem inversor."), () => Fazer(() =>
        {
            var c = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.AddCombiner());
            return Tr.F("{0} criada.", c.Name);
        }));
        Botao(botoes, Tr.T("Apagar"), Tr.T("Tira a combiner do cadastro e o retângulo dela do campo; as strings dela ficam soltas (no desenho)."), Apagar);

        var grade = Grade();
        _nome = Campo(grade, Tr.T("Nome (tag)"), Tr.T("A tag que aparece em campo (CB1, CB2...)."));
        _entradas = Campo(grade, Tr.T("Entradas"), Tr.T("Quantas strings a combiner recebe; passar disso é avisado."));
        _inversor = Escolha(grade, Tr.T("Inversor"), Tr.T("O inversor a que a combiner liga. As strings dela passam a ligar nele."));
        _largura = Campo(grade, Tr.T("Largura (m)"), Tr.T("Medida em X do retângulo em campo."));
        _comprimento = Campo(grade, Tr.T("Comprimento (m)"), Tr.T("Medida em Y do retângulo em campo."));
        _altura = Campo(grade, Tr.T("Altura (m)"), Tr.F("Altura do retângulo 3D (a base flutua {0:0.00} m acima do terreno).", Clivus.Geo.EquipmentFootprint.FloatHeight));

        var acoes = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        Botao(acoes, Tr.T("Salvar alterações"), Tr.T("Grava a combiner escolhida."), Salvar);
        Botao(acoes, Tr.T("Alocar em campo"), Tr.T("A janela some: clique o centro do retângulo na planta. Se já está em campo, ele é movido."), () =>
        {
            if (Escolhida is { } c) AlocarEmCampo(c.Id);
            else Avisar(Tr.T("Escolha uma combiner na lista."), erro: true);
        });
        Botao(acoes, Tr.T("+ Strings"), Tr.T("A janela some: clique as strings desta combiner (Shift+clique tira; Enter termina). String de outra combiner é recusada; passar das entradas é avisado."), () =>
        {
            if (Escolhida is { } c) JanelaEletrica.Campo(Documento, PluginInfo.ComandoEletricaCombiner, c.Id.ToString("D"));
            else Avisar(Tr.T("Escolha uma combiner na lista."), erro: true);
        });
        Botao(acoes, Tr.T("Soltar strings"), Tr.T("Tira todas as strings desta combiner (só o vínculo; as strings ficam no desenho)."), Soltar);

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
            catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao mostrar a combiner escolhida.", erro); }
        };
    }

    private Combiner? Escolhida => (_lista.SelectedItem as ListBoxItem)?.Tag as Combiner;

    private ElectricalSetup _setup = new();

    internal override void Atualizar()
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        _setup = setup;
        var anterior = Escolhida?.Id;
        var emCampo = EquipamentoEmCampo.EmCampo(Documento.Database);
        var strings = ElectricalStore.CombinerStrings(Documento.Database).Items;

        _lista.Items.Clear();
        foreach (var c in setup.Combiners)
        {
            var n = strings.Count(x => x.Combiner == c.Id);
            var linha = Tr.F("{0} — {1} de {2} entradas — {3}", c.Name, n, c.Inputs, setup.FindInverter(c.Inverter)?.Name ?? Tr.T("sem inversor"));
            if (n > c.Inputs) linha += " — " + Tr.T("EXCESSO");
            var item = new ListBoxItem { Content = ComCampo(linha, emCampo.Contains((EquipmentKind.Combiner, c.Id))), Tag = c };
            _lista.Items.Add(item);
            if (c.Id == anterior) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
        Preencher();
        if (problema is not null) Avisar(Tr.F("ATENÇÃO: {0}", problema), erro: true);
    }

    private void Preencher()
    {
        _inversor.Items.Clear();
        _inversor.Items.Add(new ComboBoxItem { Content = Tr.T("(nenhum)"), Tag = Guid.Empty });
        foreach (var i in _setup.Inverters) _inversor.Items.Add(new ComboBoxItem { Content = i.Name, Tag = i.Id });

        if (Escolhida is not { } c)
        {
            foreach (var caixa in new[] { _nome, _entradas, _largura, _comprimento, _altura }) caixa.Text = string.Empty;
            return;
        }

        _nome.Text = c.Name;
        _entradas.Text = c.Inputs.ToString(Tr.Culture);
        _largura.Text = Numero(c.Size.Width);
        _comprimento.Text = Numero(c.Size.Length);
        _altura.Text = Numero(c.Size.Height);
        _inversor.SelectedItem = _inversor.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (Guid)i.Tag! == c.Inverter) ?? _inversor.Items[0];
    }

    private void Salvar()
    {
        if (Escolhida is not { } c)
        {
            Avisar(Tr.T("Escolha uma combiner na lista."), erro: true);
            return;
        }

        if (!NumberInput.TryParseCount(_entradas.Text, out var entradas))
        {
            Avisar(Tr.T("Não consigo ler as entradas (um número inteiro)."), erro: true);
            return;
        }

        if (LerTamanho(_largura, _comprimento, _altura) is not { } tamanho) return;
        var inversor = _inversor.SelectedItem is ComboBoxItem { Tag: Guid g } ? g : Guid.Empty;
        var editada = c with { Name = _nome.Text, Inputs = entradas, Size = tamanho, Inverter = inversor };

        Gravar(() =>
        {
            var porque = ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.EditCombiner(editada));
            if (porque is not null)
            {
                Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
                return null;
            }

            EquipamentoEmCampo.Redesenhar(Documento.Database, EquipmentKind.Combiner, c.Id);
            if (inversor == Guid.Empty)
                return Tr.F("{0} salva, sem inversor: as strings dela continuam no inversor que tinham (a rota Combiner avisa até escolher).", editada.Name.Trim());

            // As strings da combiner passam a ligar no inversor dela.
            try
            {
                var mudadas = CombinerCommands.ReligarStrings(Documento.Database, c.Id, inversor);
                return Tr.F("{0} salva ({1} string(s) religada(s) ao inversor).", editada.Name.Trim(), mudadas);
            }
            catch (InvalidOperationException erro)
            {
                return Tr.F("{0} salva, mas as strings não foram religadas ao inversor: {1}", editada.Name.Trim(), erro.Message);
            }
        });
    }

    private void Apagar()
    {
        if (Escolhida is not { } c) return;
        Fazer(() =>
        {
            ConfiguracaoEletricaStore.Mudar(Documento.Database, s => s.RemoveCombiner(c.Id));
            var db = Documento.Database;
            ElectricalStore.SaveCombinerStrings(db, CombinerAllocation.Release(c.Id, ElectricalStore.CombinerStrings(db).Items));
            EquipamentoEmCampo.Apagar(db, EquipmentKind.Combiner, c.Id);
            return Tr.F("{0} apagada; as strings dela ficaram soltas da combiner.", c.Name);
        });
    }

    private void Soltar()
    {
        if (Escolhida is not { } c) return;
        Fazer(() =>
        {
            var db = Documento.Database;
            var antes = ElectricalStore.CombinerStrings(db).Items;
            var depois = CombinerAllocation.Release(c.Id, antes);
            ElectricalStore.SaveCombinerStrings(db, depois);
            return Tr.F("{0} string(s) soltas de {1}.", antes.Count - depois.Count, c.Name);
        });
    }
}

/// <summary>CLIVUS_ELETRICA_COMBINER (19.2): as strings de uma combiner, escolhidas em campo, com as regras da alocação no inversor.</summary>
public static class CombinerCommands
{
    [CommandMethod(PluginInfo.ComandoEletricaCombiner)]
    public static void Strings()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            var qual = editor.GetString(new PromptStringOptions(Tr.T("\nCombiner (nome ou GUID): ")) { AllowSpaces = true });
            if (qual.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
            var texto = qual.StringResult.Trim();
            var combiner = Guid.TryParse(texto, out var g) ? setup.FindCombiner(g) : setup.Combiners.FirstOrDefault(c => ElectricalSetup.SameName(c.Name, texto));
            if (combiner is null)
            {
                editor.WriteMessage(Tr.F("\nCOMBINER Não há combiner \"{0}\" no cadastro.\n", texto));
                return;
            }

            var strings = StringsDoDesenho.Ler(db);
            var atual = ElectricalStore.CombinerStrings(db).Items;
            var deOutra = atual.Where(x => x.Combiner != combiner.Id).Select(x => x.String).ToHashSet();
            var jaDela = atual.Count(x => x.Combiner == combiner.Id);
            var escolhidas = new HashSet<ObjectId>();
            var recusadas = 0;
            CaixaDeSelecao? placar = null;

            void Placar()
            {
                try
                {
                    if (!ClivusExtension.TemInterface()) return;
                    placar ??= AlocacaoDeStringsCommands.NovoPlacar(620);
                    var total = jaDela + escolhidas.Count(id => !atual.Any(x => x.String == strings[id].Id));
                    placar.TextoLivre = Tr.F("{0}: {1} de {2} entradas", combiner.Name, total, combiner.Inputs)
                        + (recusadas > 0 ? "\n" + Tr.F("{0} recusada(s): de outra combiner", recusadas) : string.Empty)
                        + (total > combiner.Inputs ? "\n" + Tr.F("EXCESSO: {0} string(s) a mais que as entradas", total - combiner.Inputs) : string.Empty);
                    if (!placar.IsVisible) placar.Show();
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha no placar da combiner.", erro);
                }
            }

            void Somou(object? _, SelectionAddedEventArgs e)
            {
                try
                {
                    var ids = e.AddedObjects.GetObjectIds();
                    for (var i = ids.Length - 1; i >= 0; i--)
                    {
                        if (!strings.TryGetValue(ids[i], out var s)) e.Remove(i);
                        else if (deOutra.Contains(s.Id))
                        {
                            recusadas++;
                            e.Remove(i);
                        }
                        else escolhidas.Add(ids[i]);
                    }
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha ao conferir as strings da combiner.", erro);
                }

                Placar();
            }

            void Tirou(object? _, SelectionRemovedEventArgs e)
            {
                try
                {
                    foreach (var id in e.RemovedObjects.GetObjectIds()) escolhidas.Remove(id);
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha ao tirar strings da combiner.", erro);
                }

                Placar();
            }

            var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE"), new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginInfo.PrefixoDeDados)]);
            editor.SelectionAdded += Somou;
            editor.SelectionRemoved += Tirou;
            PromptSelectionResult r;
            try
            {
                Placar();
                r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nStrings da {0} (Shift+clique tira; Enter termina): ", combiner.Name) }, filtro);
            }
            finally
            {
                editor.SelectionAdded -= Somou;
                editor.SelectionRemoved -= Tirou;
                placar?.Close();
            }

            if (r.Status != PromptStatus.OK) return;

            var guids = r.Value.GetObjectIds().Where(strings.ContainsKey).Select(id => strings[id].Id).ToList();
            var resultado = CombinerAllocation.Allocate(combiner, guids, atual);
            ElectricalStore.SaveCombinerStrings(db, resultado.Allocation);
            var religadas = combiner.Inverter == Guid.Empty ? 0 : ReligarStrings(db, combiner.Id, combiner.Inverter);
            if (combiner.Inverter == Guid.Empty)
                editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0} não tem inversor; as strings continuam no inversor que tinham até você escolher o da combiner.", combiner.Name));

            editor.WriteMessage(Tr.F("\nCOMBINER {0}: {1} string(s) nova(s), {2} recusada(s) (de outra combiner), {3} ligada(s) ao inversor dela.\n", combiner.Name, resultado.Added, resultado.Refused, religadas));
            if (resultado.Excess > 0) editor.WriteMessage(Tr.F("  ATENÇÃO: EXCESSO de {0} string(s) além das {1} entradas.\n", resultado.Excess, combiner.Inputs));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao alocar strings na combiner.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui alocar as strings na combiner: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento);
        }
    }

    /// <summary>As strings da combiner passam a ligar no inversor dela (a cadeia inversor -> combiner -> string). Quantas mudaram.</summary>
    internal static int ReligarStrings(Database db, Guid combiner, Guid inversor)
    {
        var dela = ElectricalStore.CombinerStrings(db).Items.Where(x => x.Combiner == combiner).Select(x => x.String).ToHashSet();
        var mudadas = StringsDoDesenho.Ler(db).Values.Where(s => dela.Contains(s.Id) && s.Inverter != inversor).Select(s => s with { Inverter = inversor }).ToList();
        return StringsDoDesenho.Gravar(db, mudadas);
    }
}
