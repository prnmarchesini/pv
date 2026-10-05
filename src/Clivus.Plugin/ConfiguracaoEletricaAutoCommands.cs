using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.ConfiguracaoEletricaAutoCommands))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 das etapas 12 a 14, e quem precisa de
/// trafos e inversores no desenho para testar): CLIVUS_ELETRICA_AUTO faz pela
/// linha de comando o que a janela da configuração elétrica faz, pelas mesmas
/// regras do Core. As linhas começam por "ELETRICA" e saem sem tradução: são
/// lidas pelo teste.
/// </summary>
public static class ConfiguracaoEletricaAutoCommands
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly string[] Palavras = ["Trafo", "Editar", "Uc", "Vincular", "Soltar", "Modelo", "Inversores", "Alocar", "SoltarInversor", "EditarInversor", "Skid", "Desagrupar", "Listar", "Formulario", "Bloco", "EditarUc", "ApagarBloco", "UcAntiga", "ModeloAntigo"];

#if DEBUG
    [CommandMethod(PluginInfo.ComandoEletricaAutomatico)]
#endif
    public static void Automatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var o = new PromptKeywordOptions($"\nEletrica [{string.Join("/", Palavras)}]: ") { AllowNone = false };
            foreach (var k in Palavras) o.Keywords.Add(k);

            var r = editor.GetKeywords(o);
            if (r.Status != PromptStatus.OK) return;

            var database = documento.Database;
            string? frase = r.StringResult switch
            {
                "Trafo" => NovoTrafo(editor, database),
                "Editar" => EditarTrafo(editor, database),
                "Uc" => NovaUc(editor, database),
                "Vincular" => Vincular(editor, database),
                "Soltar" => SoltarTrafo(editor, database),
                "Modelo" => NovoModelo(editor, database),
                "Inversores" => NovosInversores(editor, database),
                "Alocar" => AlocarLivres(editor, database),
                "SoltarInversor" => SoltarInversor(editor, database),
                "EditarInversor" => EditarInversor(editor, database),
                "Skid" => Agrupar(editor, database),
                "Desagrupar" => Desagrupar(editor, database),
                "Formulario" => Formulario(editor, database),
                "Bloco" => EditarBloco(editor, database),
                "EditarUc" => EditarUc(editor, database),
                "ApagarBloco" => ApagarBloco(database),
                "UcAntiga" => UcsDoFormatoAntigo(database),
                "ModeloAntigo" => ModeloDoFormatoAntigo(editor, database),
                _ => string.Empty,
            };

            if (frase is null) return;
            if (frase.Length > 0) editor.WriteMessage($"\nELETRICA {frase}\n");
            Listar(editor, database);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>Trafo &lt;0 = em branco | n = o n-ésimo padrão, a partir de 1&gt;.</summary>
    private static string? NovoTrafo(Editor editor, Database database)
    {
        if (Inteiro(editor, "\nPadrao (0 = em branco): ") is not { } n) return null;
        if (n > ElectricalDefaults.TransformerTemplates.Count) return $"ERRO padrao {n} nao existe";

        var padrao = n > 0 ? ElectricalDefaults.TransformerTemplates[n - 1] : null;
        return "trafo " + ConfiguracaoEletricaStore.Mudar(database, s => s.AddTransformer(padrao)).Nickname + " criado";
    }

    /// <summary>Uc Compartilhada | Uc Unitarias &lt;quantas&gt;.</summary>
    private static string? NovaUc(Editor editor, Database database)
    {
        var o = new PromptKeywordOptions("\nModo [Compartilhada/Unitarias]: ") { AllowNone = false };
        o.Keywords.Add("Compartilhada");
        o.Keywords.Add("Unitarias");
        var modo = editor.GetKeywords(o);
        if (modo.Status != PromptStatus.OK) return null;

        if (modo.StringResult == "Compartilhada")
            return "uc " + ConfiguracaoEletricaStore.Mudar(database, s => s.AddSharedUnit()).Code + " criada";

        if (Inteiro(editor, "\nQuantas: ") is not { } n) return null;
        if (n < 1 || n > ElectricalDefaults.MaxAtOnce) return $"ERRO quantas={n}";
        var novas = ConfiguracaoEletricaStore.Mudar(database, s => s.AddUnitaryUnits(n));
        return "uc " + string.Join(",", novas.Select(u => u.Code)) + " criada(s)";
    }

    /// <summary>Editar &lt;apelido&gt; &lt;campo&gt; &lt;valor&gt;: o mesmo caminho do Salvar da janela.</summary>
    private static string? EditarTrafo(Editor editor, Database database)
    {
        if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return null;
        if (Texto(editor, "\nCampo [Nome/Apelido/Kva/Entrada/Saida/K/Z/Notas]: ") is not { } campo) return null;
        if (Texto(editor, "\nValor: ") is not { } valor) return null;

        var id = Guid.Empty;
        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            if (Trafo(s, apelido) is not { } t) return "trafo nao existe";
            id = t.Id;
            double Num() => double.Parse(valor, Inv);
            return s.EditTransformer(campo switch
            {
                "Nome" => t with { Name = valor },
                "Apelido" => t with { Nickname = valor },
                "Kva" => t with { PowerKva = Num() },
                "Entrada" => t with { InputVoltage = Num() },
                "Saida" => t with { OutputVoltage = Num() },
                "K" => t with { KFactor = Num() },
                "Z" => t with { ImpedancePercent = Num() },
                _ => t with { Notes = valor },
            });
        });

        if (porque is null) EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Transformer, id);
        return porque is null ? $"trafo {apelido} editado" : $"recusado: {porque}";
    }

    /// <summary>
    /// Formulario &lt;apelido&gt; e os textos das caixas do trafo, na ordem da
    /// tela (nome, apelido, entrada, saída, kVA, K, Z, observações, largura,
    /// comprimento, altura) e a UC (código): o MESMO caminho do Salvar da
    /// janela (TransformerForm.Read e SaveTransformer). "-" é caixa vazia / nenhuma UC.
    /// </summary>
    private static string? Formulario(Editor editor, Database database)
    {
        if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return null;

        var textos = new string[12];
        string[] perguntas = ["Nome", "Apelido", "Entrada", "Saida", "Kva", "K", "Z", "Notas", "Largura", "Comprimento", "Altura", "Uc"];
        for (var i = 0; i < textos.Length; i++)
        {
            if (Texto(editor, $"\n{perguntas[i]}: ") is not { } t) return null;
            textos[i] = t == "-" ? string.Empty : t;
        }

        var lidos = new TransformerFormTexts(textos[0], textos[1], textos[2], textos[3], textos[4], textos[5], textos[6], textos[7], textos[8], textos[9], textos[10]);
        var id = Guid.Empty;
        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            if (Trafo(s, apelido) is not { } t) return "trafo nao existe";
            id = t.Id;

            var uc = Guid.Empty;
            if (textos[11].Length > 0)
            {
                if (Uc(s, textos[11]) is not { } u) return "subestacao nao existe";
                uc = u.Id;
            }

            return TransformerForm.Read(t, lidos, out var naoLeu) is { } editado ? s.SaveTransformer(editado, uc) : naoLeu;
        });

        if (porque is null) EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Transformer, id);
        return porque is null ? $"trafo {apelido} salvo pelo formulario" : $"recusado: {porque}";
    }

    /// <summary>Bloco &lt;nome&gt; &lt;largura&gt; &lt;comprimento&gt; &lt;altura&gt;: o Salvar do bloco compartilhado na janela.</summary>
    private static string? EditarBloco(Editor editor, Database database)
    {
        if (Texto(editor, "\nNome do bloco: ") is not { } nome) return null;
        if (Texto(editor, "\nLargura: ") is not { } w || Texto(editor, "\nComprimento: ") is not { } l || Texto(editor, "\nAltura: ") is not { } h) return null;

        if (!NumberInput.TryParseMeasure(w, out var largura) || !NumberInput.TryParseMeasure(l, out var comprimento) || !NumberInput.TryParseMeasure(h, out var altura))
            return "recusado: medida";

        var id = Guid.Empty;
        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            if (s.Substations.FirstOrDefault() is not { } b) return "sem bloco";
            id = b.Id;
            return s.EditSubstation(b.Id, nome, new EquipmentSize(largura, comprimento, altura));
        });

        if (porque is null) EquipamentoEmCampo.Redesenhar(database, EquipmentKind.ConsumerUnit, id);
        return porque is null ? "bloco editado" : $"recusado: {porque}";
    }

    /// <summary>EditarUc &lt;código&gt; &lt;nome&gt;: o "Salvar nome" da UC do bloco.</summary>
    private static string? EditarUc(Editor editor, Database database)
    {
        if (Texto(editor, "\nSubestacao (codigo): ") is not { } codigo) return null;
        if (Texto(editor, "\nNome: ") is not { } nome) return null;

        var porque = ConfiguracaoEletricaStore.Mudar(database, s => Uc(s, codigo) is not { } u ? "subestacao nao existe" : s.EditUnit(u.Id, nome, u.Size));
        return porque is null ? $"uc {codigo} editada" : $"recusado: {porque}";
    }

    private static string? ApagarBloco(Database database)
    {
        var r = ConfiguracaoEletricaStore.Mudar(database, s => s.Substations.FirstOrDefault() is { } b ? s.RemoveSubstation(b.Id) : null);
        return r is { } x ? $"bloco apagado ucs={x.Units} trafos={x.Transformers}" : "recusado: sem bloco";
    }

    /// <summary>
    /// UcAntiga: grava as UCs no formato 1 (antes do bloco físico, 7 campos):
    /// C1 e C2 compartilhadas e U1 unitária, sem bloco. É o desenho de antes
    /// de 05/10/2026, para provar que ele continua sendo lido.
    /// </summary>
    private static string UcsDoFormatoAntigo(Database database)
    {
        var caixa = new EquipmentSize(5, 4, 3);
        ConsumerUnit[] ucs =
        [
            new(Guid.NewGuid(), "C1", "Medicao 1", ConsumerUnitMode.Shared, caixa),
            new(Guid.NewGuid(), "C2", "Medicao 2", ConsumerUnitMode.Shared, ElectricalDefaults.ConsumerUnitSize),
            new(Guid.NewGuid(), "U1", "Posto", ConsumerUnitMode.Unitary, ElectricalDefaults.ConsumerUnitSize),
        ];

        PluginRecords.Save(database, "SUBESTACOES", 1, ConsumerUnit.LegacyFieldCount, ucs, u => u.ToFields().Take(ConsumerUnit.LegacyFieldCount).ToList());
        return "ucs do formato 1 gravadas";
    }

    /// <summary>Vincular &lt;código da UC&gt; &lt;apelido do trafo&gt;.</summary>
    private static string? Vincular(Editor editor, Database database)
    {
        if (Texto(editor, "\nSubestacao (codigo): ") is not { } codigo) return null;
        if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return null;

        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
            Uc(s, codigo) is not { } u ? "subestacao nao existe"
            : Trafo(s, apelido) is not { } t ? "trafo nao existe"
            : s.LinkTransformer(u.Id, t.Id));

        return porque is null ? $"{apelido} ligado a {codigo}" : $"recusado: {porque}";
    }

    private static string? SoltarTrafo(Editor editor, Database database)
    {
        if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return null;
        var soltou = ConfiguracaoEletricaStore.Mudar(database, s => Trafo(s, apelido) is { } t && s.UnlinkTransformer(t.Id));
        return $"{apelido} solto={soltou}";
    }

    /// <summary>
    /// Modelo &lt;nome&gt; &lt;MPPT&gt; &lt;entradas&gt;: cria e grava como o Salvar
    /// da janela. As entradas são um número (o mesmo em todos os MPPTs) ou a
    /// lista de cada MPPT ("4;4;4;5;5"), com um valor por MPPT.
    /// </summary>
    private static string? NovoModelo(Editor editor, Database database)
    {
        if (Texto(editor, "\nNome do modelo: ") is not { } nome) return null;
        if (Inteiro(editor, "\nMPPT: ") is not { } mppt) return null;
        if (Texto(editor, "\nEntradas (uma para todos ou a lista 4;4;5): ") is not { } texto) return null;

        if (InverterModel.ParseInputs(texto) is not { Count: > 0 } lista) return "recusado: entradas ilegiveis";
        IReadOnlyList<int> entradas = lista.Count == 1 ? Enumerable.Repeat(lista[0], Math.Max(0, mppt)).ToArray() : lista;
        if (entradas.Count != mppt) return $"recusado: {mppt} MPPT e {lista.Count} valor(es) na lista";

        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            var m = s.AddModel();
            var p = s.EditModel(m with { Name = nome, InputsByMppt = entradas });
            if (p is not null) s.RemoveModel(m.Id);
            return p;
        });

        return porque is null ? $"modelo {nome} criado" : $"recusado: {porque}";
    }

    /// <summary>
    /// ModeloAntigo &lt;nome&gt; &lt;MPPT&gt; &lt;entradas por MPPT&gt;: grava os
    /// modelos no formato 1 (antes de 05/10/2026: um número de entradas para
    /// todos os MPPTs), somando este aos que já há. É o desenho antigo, para
    /// provar que ele continua sendo lido.
    /// </summary>
    private static string? ModeloDoFormatoAntigo(Editor editor, Database database)
    {
        if (Texto(editor, "\nNome do modelo: ") is not { } nome) return null;
        if (Inteiro(editor, "\nMPPT: ") is not { } mppt) return null;
        if (Inteiro(editor, "\nEntradas por MPPT: ") is not { } entradas) return null;

        var modelos = ElectricalStore.InverterModels(database).Items.Append(new InverterModel(Guid.NewGuid(), nome, mppt, entradas, ElectricalDefaults.InverterSize)).ToList();
        PluginRecords.Save(database, ElectricalStore.ChaveDosModelos, 1, InverterModel.FieldCount, modelos,
            m => [m.Id.ToString("D"), m.Name, m.Mppts.ToString(Inv), m.InputsByMppt[0].ToString(Inv), R(m.Size.Width), R(m.Size.Length), R(m.Size.Height)]);
        return $"modelos do formato 1 gravados ({modelos.Count})";

        static string R(double v) => v.ToString("R", Inv);
    }

    /// <summary>Inversores &lt;nome do modelo&gt; &lt;quantos&gt;.</summary>
    private static string? NovosInversores(Editor editor, Database database)
    {
        if (Texto(editor, "\nModelo (nome): ") is not { } nome) return null;
        if (Inteiro(editor, "\nQuantos: ") is not { } n) return null;

        var (lido, _) = ConfiguracaoEletricaStore.Ler(database);
        if (lido.Models.FirstOrDefault(m => ElectricalSetup.SameName(m.Name, nome)) is not { } modelo) return "recusado: modelo nao existe";
        if (n < 1 || n > ElectricalDefaults.MaxAtOnce) return $"recusado: quantos={n}";

        var novos = ConfiguracaoEletricaStore.Mudar(database, s => s.AddInverters(modelo.Id, n));
        return "inversores " + string.Join(",", novos.Select(i => i.Name)) + " criados";
    }

    /// <summary>
    /// Alocar &lt;inversor&gt; &lt;quantas&gt;: as N primeiras strings livres (na
    /// ordem do handle) vão para o inversor, pelo mesmo Core da seleção.
    /// </summary>
    private static string? AlocarLivres(Editor editor, Database database)
    {
        if (Texto(editor, "\nInversor (nome): ") is not { } nome) return null;
        if (Inteiro(editor, "\nQuantas livres: ") is not { } n) return null;

        var setup = ConfiguracaoEletricaStore.Ler(database).Setup;
        if (setup.FindInverter(nome) is not { } inversor) return "recusado: inversor nao existe";

        var livres = StringsDoDesenho.Ler(database).OrderBy(x => x.Key.Handle.Value).Select(x => x.Value).Where(s => !s.IsAllocated).Take(n).ToList();
        var plano = StringAllocation.Allocate(inversor.Id, livres, AlocacaoDeStringsCommands.Cadastrados(setup));
        StringsDoDesenho.Gravar(database, plano.Changed);
        return $"alocadas {plano.Changed.Count} em {inversor.Name}";
    }

    /// <summary>SoltarInversor &lt;inversor&gt;: o "soltar todas" da janela.</summary>
    private static string? SoltarInversor(Editor editor, Database database)
    {
        if (Texto(editor, "\nInversor (nome): ") is not { } nome) return null;
        if (ConfiguracaoEletricaStore.Ler(database).Setup.FindInverter(nome) is not { } inversor) return "recusado: inversor nao existe";
        return $"soltas {StringsDoDesenho.Soltar(database, inversor.Id)} de {inversor.Name}";
    }

    /// <summary>EditarInversor &lt;inversor&gt; &lt;nome novo&gt; &lt;modelo&gt;.</summary>
    private static string? EditarInversor(Editor editor, Database database)
    {
        if (Texto(editor, "\nInversor (nome): ") is not { } nome) return null;
        if (Texto(editor, "\nNome novo: ") is not { } novo) return null;
        if (Texto(editor, "\nModelo (nome): ") is not { } doModelo) return null;

        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
            s.FindInverter(nome) is not { } i ? "inversor nao existe"
            : s.Models.FirstOrDefault(m => ElectricalSetup.SameName(m.Name, doModelo)) is not { } m ? "modelo nao existe"
            : s.EditInverter(i.Id, novo, m.Id));
        if (porque is null && ConfiguracaoEletricaStore.Ler(database).Setup.FindInverter(novo) is { } editado)
            EquipamentoEmCampo.Redesenhar(database, EquipmentKind.Inverter, editado.Id);
        return porque is null ? $"inversor {nome} editado" : $"recusado: {porque}";
    }

    /// <summary>Skid &lt;trafo&gt; &lt;nome&gt; &lt;inversores separados por ;&gt;: as mesmas regras da seleção em campo.</summary>
    private static string? Agrupar(Editor editor, Database database)
    {
        if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return null;
        if (Texto(editor, "\nNome do skid: ") is not { } nome) return null;
        if (Texto(editor, "\nInversores (separados por ;): ") is not { } lista) return null;

        SkidResult? r = null;
        ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            if (Trafo(s, apelido) is not { } t) return false;
            var ids = lista.Split(';').Select(n => s.FindInverter(n)?.Id ?? Guid.NewGuid()).ToList();
            r = s.Group(t.Id, nome, ids);
            return true;
        });

        return r is null ? "recusado: trafo nao existe"
            : r.Problem is not null ? $"recusado: {r.Problem}"
            : $"skid {apelido}: agrupados={r.Added} ja={r.AlreadyHere} recusados={r.Refused.Count} sumidos={r.Missing}";
    }

    private static string? Desagrupar(Editor editor, Database database)
    {
        if (Texto(editor, "\nInversor (nome): ") is not { } nome) return null;
        var tirou = ConfiguracaoEletricaStore.Mudar(database, s => s.FindInverter(nome) is { } i && s.Ungroup(i.Id));
        return $"{nome} desagrupado={tirou}";
    }

    private static Transformer? Trafo(ElectricalSetup s, string apelido) => s.Transformers.FirstOrDefault(t => ElectricalSetup.SameName(t.Nickname, apelido));

    private static ConsumerUnit? Uc(ElectricalSetup s, string codigo) => s.Units.FirstOrDefault(u => ElectricalSetup.SameName(u.Code, codigo));

    /// <summary>O cadastro inteiro, uma linha por registro, números invariantes.</summary>
    private static void Listar(Editor editor, Database database)
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        if (problema is not null) editor.WriteMessage($"\nELETRICA PROBLEMA {problema}\n");

        static string N(double v) => v.ToString("0.###", Inv);
        static string Tam(EquipmentSize t) => $"{N(t.Width)}x{N(t.Length)}x{N(t.Height)}";

        editor.WriteMessage($"\nELETRICA {setup.Units.Count} subestacao(oes)\n");
        foreach (var u in setup.Units)
            editor.WriteMessage($"ELETRICA UC {u.Code} modo={u.Mode} nome=\"{u.Name}\" tamanho={Tam(u.Size)} trafos={string.Join(",", setup.TransformersOf(u.Id).Select(t => t.Nickname))}\n");

        editor.WriteMessage($"ELETRICA {setup.Substations.Count} bloco(s) migradas={setup.MigratedUnits} formato_ucs={PluginRecords.Version(database, "SUBESTACOES")}\n");
        foreach (var b in setup.Substations)
            editor.WriteMessage($"ELETRICA BLOCO nome=\"{b.Name}\" tamanho={Tam(b.Size)} ucs={string.Join(",", setup.UnitsOf(b.Id).Select(u => u.Code))} id={b.Id:D} fim\n");

        // entradas=: o número quando todos os MPPTs têm o mesmo, senão a lista (4;4;4;5;5).
        editor.WriteMessage($"ELETRICA {setup.Models.Count} modelo(s) formato_modelos={PluginRecords.Version(database, ElectricalStore.ChaveDosModelos)}\n");
        foreach (var m in setup.Models)
            editor.WriteMessage($"ELETRICA MODELO nome=\"{m.Name}\" mppt={m.Mppts} entradas={(m.IsUniform ? m.InputsByMppt[0].ToString(Inv) : InverterModel.FormatInputs(m.InputsByMppt))} total={m.TotalInputs} tamanho={Tam(m.Size)}\n");

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var strings = ElectricalStore.Strings(transacao, database);
            var contagem = StringAllocation.CountByInverter(strings.Select(x => x.String));
            editor.WriteMessage($"ELETRICA {setup.Inverters.Count} inversor(es) {strings.Count} string(s) {strings.Count(x => !x.String.IsAllocated)} livre(s)\n");
            foreach (var i in setup.Inverters)
                editor.WriteMessage($"ELETRICA INVERSOR nome=\"{i.Name}\" modelo=\"{setup.FindModel(i.Model)?.Name}\" strings={contagem.GetValueOrDefault(i.Id)} entradas={setup.FindModel(i.Model)?.TotalInputs} trafo={setup.FindTransformer(i.Transformer)?.Nickname} excesso={StringAllocation.Excess(contagem.GetValueOrDefault(i.Id), setup.FindModel(i.Model))} fim\n");
        }

        editor.WriteMessage($"ELETRICA {setup.Skids.Count} skid(s)\n");
        foreach (var k in setup.Skids)
            editor.WriteMessage($"ELETRICA SKID nome=\"{k.Name}\" trafo={setup.FindTransformer(k.Transformer)?.Nickname} inversores={string.Join(",", setup.InvertersOf(k.Transformer).Select(i => i.Name))} fim\n");

        editor.WriteMessage($"ELETRICA {setup.Transformers.Count} trafo(s)\n");
        foreach (var t in setup.Transformers)
            editor.WriteMessage($"ELETRICA TRAFO {t.Nickname} nome=\"{t.Name}\" entrada={N(t.InputVoltage)} saida={N(t.OutputVoltage)} kva={N(t.PowerKva)} k={N(t.KFactor)} z={N(t.ImpedancePercent)} tamanho={Tam(t.Size)} notas=\"{t.Notes}\" uc={setup.FindUnit(t.ConsumerUnit)?.Code}\n");
    }

    private static string? Texto(Editor editor, string pergunta)
    {
        var r = editor.GetString(new PromptStringOptions(pergunta) { AllowSpaces = true });
        return r.Status == PromptStatus.OK ? r.StringResult.Trim() : null;
    }

    private static int? Inteiro(Editor editor, string pergunta)
    {
        var r = editor.GetInteger(new PromptIntegerOptions(pergunta) { AllowNegative = false, AllowZero = true });
        return r.Status == PromptStatus.OK ? r.Value : null;
    }
}
