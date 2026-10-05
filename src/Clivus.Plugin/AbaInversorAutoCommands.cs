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
    /// a caixa Trafo da linha (um inversor) ou o "Pôr no trafo" (vários).
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
