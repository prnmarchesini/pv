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

    private static readonly string[] Palavras = ["Trafo", "Editar", "Uc", "Vincular", "Soltar", "Modelo", "Inversores", "Alocar", "Listar"];

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

    /// <summary>Modelo &lt;nome&gt; &lt;MPPT&gt; &lt;entradas por MPPT&gt;: cria e grava como o Salvar da janela.</summary>
    private static string? NovoModelo(Editor editor, Database database)
    {
        if (Texto(editor, "\nNome do modelo: ") is not { } nome) return null;
        if (Inteiro(editor, "\nMPPT: ") is not { } mppt) return null;
        if (Inteiro(editor, "\nEntradas por MPPT: ") is not { } entradas) return null;

        var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
        {
            var m = s.AddModel();
            var p = s.EditModel(m with { Name = nome, Mppts = mppt, InputsPerMppt = entradas });
            if (p is not null) s.RemoveModel(m.Id);
            return p;
        });

        return porque is null ? $"modelo {nome} criado" : $"recusado: {porque}";
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

        if (ConfiguracaoEletricaStore.Ler(database).Setup.FindInverter(nome) is not { } inversor) return "recusado: inversor nao existe";

        var livres = StringsDoDesenho.Ler(database).OrderBy(x => x.Key.Handle.Value).Select(x => x.Value).Where(s => !s.IsAllocated).Take(n).ToList();
        var plano = StringAllocation.Allocate(inversor.Id, livres);
        StringsDoDesenho.Gravar(database, plano.Changed);
        return $"alocadas {plano.Changed.Count} em {inversor.Name}";
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

        editor.WriteMessage($"ELETRICA {setup.Models.Count} modelo(s)\n");
        foreach (var m in setup.Models)
            editor.WriteMessage($"ELETRICA MODELO nome=\"{m.Name}\" mppt={m.Mppts} entradas={m.InputsPerMppt} total={m.TotalInputs} tamanho={Tam(m.Size)}\n");

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var strings = ElectricalStore.Strings(transacao, database);
            var contagem = StringAllocation.CountByInverter(strings.Select(x => x.String));
            editor.WriteMessage($"ELETRICA {setup.Inverters.Count} inversor(es) {strings.Count} string(s) {strings.Count(x => !x.String.IsAllocated)} livre(s)\n");
            foreach (var i in setup.Inverters)
                editor.WriteMessage($"ELETRICA INVERSOR nome=\"{i.Name}\" modelo=\"{setup.FindModel(i.Model)?.Name}\" strings={contagem.GetValueOrDefault(i.Id)} entradas={setup.FindModel(i.Model)?.TotalInputs} trafo={setup.FindTransformer(i.Transformer)?.Nickname} excesso={StringAllocation.Excess(contagem.GetValueOrDefault(i.Id), setup.FindModel(i.Model))} fim\n");
        }

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
