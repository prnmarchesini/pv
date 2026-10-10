#if DEBUG
using System.Globalization;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AbaInversorAutoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 da tabela de inversores, 05/10/2026): os
/// botões e caixas da aba Inversor pelo MESMO caminho da janela solta, no
/// contexto da aplicação (Session), fora de comando do documento, com a
/// escrita pela <see cref="EscritaForaDeComando"/> como o Fazer da aba. As
/// linhas começam por "ELETRICA" e saem sem tradução: são lidas pelo teste.
/// </summary>
public static class AbaInversorAutoCommands
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// CLIVUS_ELETRICA_JANELA_TRAFO_AUTO &lt;inversores separados por ;&gt; &lt;trafo, ou - para sem trafo&gt;:
    /// a caixa Trafo da linha (um inversor) ou o "Aplicar" da barra das escolhidas (vários).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaJanelaTrafoAutomatico, CommandFlags.Session)]
    public static void Trafo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        try
        {
            var nomes = editor.GetString(new PromptStringOptions("\nInversores (nomes separados por ;): ") { AllowSpaces = true });
            if (nomes.Status != PromptStatus.OK) return;
            var qual = editor.GetString(new PromptStringOptions("\nTrafo (apelido, - = sem trafo): ") { AllowSpaces = true });
            if (qual.Status != PromptStatus.OK) return;

            var setup = ConfiguracaoEletricaStore.Ler(documento.Database).Setup;
            var ids = new List<Guid>();
            foreach (var nome in nomes.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (setup.FindInverter(nome) is not { } i)
                {
                    editor.WriteMessage($"\nELETRICA janela trafo recusado: inversor {nome} nao existe\n");
                    return;
                }

                ids.Add(i.Id);
            }

            var trafo = Guid.Empty;
            if (qual.StringResult.Trim() != "-")
            {
                if (setup.Transformers.FirstOrDefault(t => ElectricalSetup.SameName(t.Nickname, qual.StringResult)) is not { } t)
                {
                    editor.WriteMessage($"\nELETRICA janela trafo recusado: trafo {qual.StringResult} nao existe\n");
                    return;
                }

                trafo = t.Id;
            }

            var (frase, problema) = EscritaForaDeComando.Fazer(documento, () => AbaInversor.PorNoTrafo(documento.Database, ids, trafo));
            editor.WriteMessage(problema is null ? $"\nELETRICA janela trafo: {frase}\n" : $"\nELETRICA janela trafo recusado: {problema}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_TRAFO_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_JANELA_MODELO_AUTO &lt;modelo&gt; &lt;potência como digitada&gt;:
    /// o Salvar modelo da aba com a potência (a mesma leitura da caixa e a
    /// mesma gravação), o resto do modelo como está.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaJanelaModeloAutomatico, CommandFlags.Session)]
    public static void Modelo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        try
        {
            var nome = editor.GetString(new PromptStringOptions("\nModelo (nome): ") { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK) return;
            var texto = editor.GetString(new PromptStringOptions("\nPotencia (kW, como digitada): ") { AllowSpaces = true });
            if (texto.Status != PromptStatus.OK) return;

            var modelo = ConfiguracaoEletricaStore.Ler(documento.Database).Setup.Models.FirstOrDefault(m => ElectricalSetup.SameName(m.Name, nome.StringResult));
            if (modelo is null)
            {
                editor.WriteMessage($"\nELETRICA janela modelo recusado: modelo {nome.StringResult} nao existe\n");
                return;
            }

            if (!AbaInversor.LerPotencia(texto.StringResult == "." ? string.Empty : texto.StringResult, out var kw))
            {
                editor.WriteMessage($"\nELETRICA janela modelo recusado: potencia [{texto.StringResult}] ilegivel\n");
                return;
            }

            var porque = EscritaForaDeComando.Fazer(documento, () => AbaInversor.GravarModelo(documento.Database, modelo with { PowerKw = kw }));
            editor.WriteMessage(porque is null
                ? $"\nELETRICA janela modelo {modelo.Name} salvo com {kw.ToString("0.###", Inv)} kW\n"
                : $"\nELETRICA janela modelo recusado: {porque}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_MODELO_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_JANELA_LINHA_AUTO &lt;inversores separados por ;, ou * para todos&gt;
    /// &lt;Nome|Modelo|Apagar|Soltar|SoltarUsina|Mover|Ordenar&gt; &lt;valor&gt;:
    /// o nome editado na célula, o modelo escolhido na caixa da linha (um
    /// inversor), o Apagar das escolhidas e o "Apagar todos" (com *; o valor
    /// é ignorado: a confirmação da tela é o "sim" do teste), o Soltar da
    /// linha (cada um) e o "Soltar todas da usina" (10/10/2026: as tags saem
    /// junto), o arrastar da linha (Mover: o valor é o nome do inversor onde
    /// ela cai) e o "Ordenar" (o valor é Nome, Trafo ou TrafoNome; os
    /// inversores são ignorados).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaJanelaLinhaAutomatico, CommandFlags.Session)]
    public static void Linha()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        try
        {
            var nomes = editor.GetString(new PromptStringOptions("\nInversores (nomes separados por ;): ") { AllowSpaces = true });
            if (nomes.Status != PromptStatus.OK) return;
            var oQue = editor.GetString(new PromptStringOptions("\nNome, Modelo, Apagar, Soltar, SoltarUsina, Mover ou Ordenar: ") { AllowSpaces = false });
            if (oQue.Status != PromptStatus.OK) return;
            var valor = editor.GetString(new PromptStringOptions("\nValor (. = vazio): ") { AllowSpaces = true });
            if (valor.Status != PromptStatus.OK) return;
            var texto = valor.StringResult == "." ? string.Empty : valor.StringResult;

            var setup = ConfiguracaoEletricaStore.Ler(documento.Database).Setup;
            var ids = new List<Guid>();
            if (nomes.StringResult.Trim() == "*") ids.AddRange(setup.Inverters.Select(i => i.Id));
            else
            {
                foreach (var nome in nomes.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (setup.FindInverter(nome) is not { } i)
                    {
                        editor.WriteMessage($"\nELETRICA janela linha recusado: inversor {nome} nao existe\n");
                        return;
                    }

                    ids.Add(i.Id);
                }
            }

            switch (oQue.StringResult.Trim().ToUpperInvariant())
            {
                case "NOME" when ids.Count == 1:
                {
                    var porque = EscritaForaDeComando.Fazer(documento, () => AbaInversor.RenomearNaLinha(documento.Database, ids[0], texto));
                    editor.WriteMessage(porque is null ? $"\nELETRICA janela linha nome: [{texto.Trim()}] gravado\n" : $"\nELETRICA janela linha recusado: {porque}\n");
                    break;
                }

                case "MODELO" when ids.Count == 1:
                {
                    var modelo = setup.Models.FirstOrDefault(m => ElectricalSetup.SameName(m.Name, texto));
                    if (modelo is null)
                    {
                        editor.WriteMessage($"\nELETRICA janela linha recusado: modelo {texto} nao existe\n");
                        break;
                    }

                    var porque = EscritaForaDeComando.Fazer(documento, () => AbaInversor.TrocarModeloNaLinha(documento.Database, ids[0], modelo.Id));
                    editor.WriteMessage(porque is null ? $"\nELETRICA janela linha modelo: {modelo.Name} gravado\n" : $"\nELETRICA janela linha recusado: {porque}\n");
                    break;
                }

                case "APAGAR":
                {
                    var (apagados, soltas, tags) = EscritaForaDeComando.Fazer(documento, () => AbaInversor.ApagarInversores(documento.Database, ids));
                    editor.WriteMessage($"\nELETRICA janela linha apagados={string.Join(",", apagados.Select(i => i.Name))} soltas={soltas} fim\n");
                    editor.WriteMessage($"ELETRICA janela linha apagados={apagados.Count} tags={tags} fim\n");
                    break;
                }

                case "SOLTAR":
                    foreach (var id in ids)
                    {
                        var (soltas, tags) = EscritaForaDeComando.Fazer(documento, () => StringsDoDesenho.Soltar(documento.Database, id));
                        editor.WriteMessage($"\nELETRICA janela linha soltar {setup.FindInverter(id)!.Name}: soltas={soltas} tags={tags} fim\n");
                    }

                    break;

                case "SOLTARUSINA":
                {
                    var (soltas, tags) = EscritaForaDeComando.Fazer(documento, () => StringsDoDesenho.SoltarTodasDaUsina(documento.Database));
                    editor.WriteMessage($"\nELETRICA janela linha soltar usina: soltas={soltas} tags={tags} fim\n");
                    break;
                }

                case "MOVER" when ids.Count == 1:
                {
                    if (setup.FindInverter(texto) is not { } alvo)
                    {
                        editor.WriteMessage($"\nELETRICA janela linha recusado: inversor {texto} nao existe\n");
                        break;
                    }

                    var (frase, problema) = EscritaForaDeComando.Fazer(documento, () => AbaInversor.Mover(documento.Database, ids[0], alvo.Id));
                    editor.WriteMessage(problema is null ? $"\nELETRICA janela linha mover: {frase}\n" : $"\nELETRICA janela linha recusado: {problema}\n");
                    break;
                }

                case "ORDENAR":
                {
                    InverterOrder? ordem = texto.Trim().ToUpperInvariant() switch
                    {
                        "NOME" => InverterOrder.Name,
                        "TRAFO" => InverterOrder.Transformer,
                        "TRAFONOME" => InverterOrder.TransformerThenName,
                        _ => null,
                    };
                    if (ordem is not { } o)
                    {
                        editor.WriteMessage($"\nELETRICA janela linha recusado: ordem [{texto}]\n");
                        break;
                    }

                    var (frase, _) = EscritaForaDeComando.Fazer(documento, () => AbaInversor.Ordenar(documento.Database, o));
                    editor.WriteMessage($"\nELETRICA janela linha ordenar {o}: {frase}\n");
                    break;
                }

                default:
                    editor.WriteMessage($"\nELETRICA janela linha recusado: [{oQue.StringResult}] com {ids.Count} inversor(es)\n");
                    break;
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_LINHA_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_JANELA_LOCAL_AUTO &lt;Automatico|Manual|Repartir|Renomear|Linhas&gt;
    /// &lt;inversores separados por ;, * para todos (Renomear: o nome da área)&gt; &lt;valor (Renomear: o nome novo; . = vazio)&gt;:
    /// os botões "Automático pelas strings" e "À mão", o "Repartir pelo kW",
    /// a caixa do nome da área na coluna Local e o estado de cada linha
    /// (coluna Local, botão de campo, Ver em campo) com a soma dos limites,
    /// pelo mesmo caminho da aba (melhorias de 10/10/2026).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaJanelaLocalAutomatico, CommandFlags.Session)]
    public static void Local()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        try
        {
            var oQue = editor.GetString(new PromptStringOptions("\nAutomatico, Manual, Repartir, Renomear ou Linhas: ") { AllowSpaces = false });
            if (oQue.Status != PromptStatus.OK) return;
            var nomes = editor.GetString(new PromptStringOptions("\nInversores (nomes separados por ;) ou area: ") { AllowSpaces = true });
            if (nomes.Status != PromptStatus.OK) return;
            var valor = editor.GetString(new PromptStringOptions("\nValor (. = vazio): ") { AllowSpaces = true });
            if (valor.Status != PromptStatus.OK) return;
            var texto = valor.StringResult == "." ? string.Empty : valor.StringResult;
            var db = documento.Database;
            var setup = ConfiguracaoEletricaStore.Ler(db).Setup;

            var op = oQue.StringResult.Trim().ToUpperInvariant();
            if (op == "RENOMEAR")
            {
                var area = LocalDosInversores.Areas(db).Values.FirstOrDefault(a => string.Equals(a.Marca.Name, nomes.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));
                var (frase, problema) = area.Marca is null
                    ? (null, "area nao existe")
                    : EscritaForaDeComando.Fazer(documento, () => AbaInversor.RenomearArea(db, area.Marca.Id, texto));
                editor.WriteMessage(problema is null ? $"\nELETRICA janela local renomear: {frase}\n" : $"\nELETRICA janela local recusado: {problema}\n");
                return;
            }

            if (op == "AREAS")
            {
                // A lista do botão "Áreas…" (item 3 da segunda rodada): o nome e quantos inversores.
                foreach (var (_, nome, quantos) in AbaInversor.ListaDeAreas(db))
                    editor.WriteMessage($"\nELETRICA LOCAL_AREA nome={nome.Replace(' ', '_')} inversores={quantos} fim\n");
                return;
            }

            if (op == "PRETAG")
            {
                // As caixas da pré-tag na seção de distribuição (item 5): "1;1;0" = inserir, moldura, fundo.
                var c = texto.Split(';');
                var opcoes = PreTagOptions.Parse(c);
                var frase = opcoes is null ? null : EscritaForaDeComando.Fazer(documento, () => AbaInversor.GravarAPreTag(db, opcoes));
                editor.WriteMessage(frase is null ? $"\nELETRICA janela local recusado: pretag [{texto}]\n" : $"\nELETRICA janela local pretag: {frase}\n");
                return;
            }

            if (op == "LINHAS")
            {
                // O mesmo caminho da tabela: o registro acompanha a geometria e a coluna Local sai da mesma conta.
                var (locais, areas, emCampo, _) = AbaInversor.LerOsLocais(documento);
                foreach (var i in setup.Inverters)
                {
                    var (v, coluna) = AbaInversor.EstadoDoLocal(i.Id, locais, areas, emCampo.Contains((EquipmentKind.Inverter, i.Id)));
                    if (coluna.Length == 0) coluna = "-";
                    editor.WriteMessage($"\nELETRICA LOCAL_LINHA nome={i.Name.Replace(' ', '_')} local={coluna.Replace(' ', '_')} botao={v.Button} ver={v.CanSee} limite={i.Target?.ToString(Inv) ?? "-"} fim\n");
                }

                var (_, uteis) = AbaInversor.ContarTudo(db);
                var somaDosLimites = BalancedLimits.Sum(setup.Inverters, setup.Models);
                var (soma, bate) = BalancedLimits.Describe(somaDosLimites, uteis);
                editor.WriteMessage($"ELETRICA LOCAL_SOMA bate={bate} [{soma}] fim\n");

                // A célula Limite da linha Total (item 1 da segunda rodada), como a tabela monta.
                var (celula, bateNoTotal) = BalancedLimits.TotalCell(somaDosLimites, uteis);
                editor.WriteMessage($"ELETRICA LOCAL_TOTAL_LIMITE texto={celula.Replace(' ', '_')} vermelho={!bateNoTotal} fim\n");
                return;
            }

            var ids = new List<Guid>();
            if (nomes.StringResult.Trim() == "*") ids.AddRange(setup.Inverters.Select(i => i.Id));
            else
            {
                foreach (var nome in nomes.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (setup.FindInverter(nome) is not { } i)
                    {
                        editor.WriteMessage($"\nELETRICA janela local recusado: inversor {nome} nao existe\n");
                        return;
                    }

                    ids.Add(i.Id);
                }
            }

            (string? Frase, string? Problema) r = op switch
            {
                "AUTOMATICO" => EscritaForaDeComando.Fazer(documento, () => AbaInversor.MudarOLocal(db, ids, InverterPlacementMode.Automatic)),
                "MANUAL" => EscritaForaDeComando.Fazer(documento, () => AbaInversor.MudarOLocal(db, ids, null)),
                "REPARTIR" => EscritaForaDeComando.Fazer(documento, () => AbaInversor.Repartir(db, ids)),
                _ => (null, $"[{oQue.StringResult}]"),
            };
            editor.WriteMessage(r.Problema is null ? $"\nELETRICA janela local {op.ToLowerInvariant()}: {r.Frase}\n" : $"\nELETRICA janela local recusado: {r.Problema}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_LOCAL_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>CLIVUS_ELETRICA_JANELA_TABELA_AUTO: as linhas e o total da tabela, montados como a aba monta.</summary>
    [CommandMethod(PluginInfo.ComandoEletricaJanelaTabelaAutomatico, CommandFlags.Session)]
    public static void Tabela()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        try
        {
            var setup = ConfiguracaoEletricaStore.Ler(documento.Database).Setup;
            var linhas = AbaInversor.LinhasDaTabela(documento, setup, AbaInversor.ContarStrings(documento.Database));

            static string N(double? v) => v is { } x ? x.ToString("0.###", Inv) : "-";

            foreach (var l in linhas)
                editor.WriteMessage($"\nELETRICA TABELA nome={l.Inverter.Name.Replace(' ', '_')} trafo={setup.FindTransformer(l.Inverter.Transformer)?.Nickname ?? "-"} strings={l.Strings} entradas={l.Capacity} kwp={N(l.PowerKwp)} kw={N(l.PowerKw)} ccca={N(l.DcAcRatio)} fim\n");

            var t = InverterTable.Total(linhas);
            editor.WriteMessage($"ELETRICA TABELA_TOTAL inversores={t.Inverters} strings={t.Strings} entradas={t.Capacity} kwp={N(t.PowerKwp)} kw={N(t.PowerKw)} ccca={N(t.DcAcRatio)} fim\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_JANELA_TABELA_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }
}
#endif
