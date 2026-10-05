using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
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
            var o = new PromptKeywordOptions("\nEletrica [Trafo/Editar/Listar]: ") { AllowNone = false };
            foreach (var k in new[] { "Trafo", "Editar", "Listar" }) o.Keywords.Add(k);

            var r = editor.GetKeywords(o);
            if (r.Status != PromptStatus.OK) return;

            var database = documento.Database;

            switch (r.StringResult)
            {
                case "Trafo":
                    // Trafo <0 = em branco | n = o n-ésimo padrão, a partir de 1>
                    if (Inteiro(editor, "\nPadrao (0 = em branco): ") is not { } n) return;
                    TransformerTemplate? padrao = null;
                    if (n > 0)
                    {
                        if (n > ElectricalDefaults.TransformerTemplates.Count) { editor.WriteMessage($"\nELETRICA ERRO padrao {n} nao existe\n"); return; }
                        padrao = ElectricalDefaults.TransformerTemplates[n - 1];
                    }

                    var novo = ConfiguracaoEletricaStore.Mudar(database, s => s.AddTransformer(padrao));
                    editor.WriteMessage($"\nELETRICA trafo {novo.Nickname} criado\n");
                    break;

                case "Editar":
                    // Editar <apelido> <campo> <valor>: o mesmo caminho do Salvar da janela.
                    if (Texto(editor, "\nTrafo (apelido): ") is not { } apelido) return;
                    if (Texto(editor, "\nCampo [Nome/Apelido/Kva/Entrada/Saida/K/Z/Notas]: ") is not { } campo) return;
                    if (Texto(editor, "\nValor: ") is not { } valor) return;
                    var porque = ConfiguracaoEletricaStore.Mudar(database, s =>
                    {
                        var t = s.Transformers.FirstOrDefault(x => ElectricalSetup.SameName(x.Nickname, apelido));
                        if (t is null) return "nao existe";
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
                    editor.WriteMessage(porque is null ? $"\nELETRICA trafo {apelido} editado\n" : $"\nELETRICA recusado: {porque}\n");
                    break;
            }

            Listar(editor, database);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_ELETRICA_AUTO.", erro);
            editor.WriteMessage($"\nELETRICA ERRO {erro.Message}\n");
        }
    }

    /// <summary>O cadastro inteiro, uma linha por registro, números invariantes.</summary>
    private static void Listar(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        if (problema is not null) editor.WriteMessage($"\nELETRICA PROBLEMA {problema}\n");

        static string N(double v) => v.ToString("0.###", Inv);
        static string Tam(EquipmentSize t) => $"{N(t.Width)}x{N(t.Length)}x{N(t.Height)}";

        editor.WriteMessage($"\nELETRICA {setup.Transformers.Count} trafo(s)\n");
        foreach (var t in setup.Transformers)
            editor.WriteMessage($"ELETRICA TRAFO {t.Nickname} nome=\"{t.Name}\" entrada={N(t.InputVoltage)} saida={N(t.OutputVoltage)} kva={N(t.PowerKva)} k={N(t.KFactor)} z={N(t.ImpedancePercent)} tamanho={Tam(t.Size)} notas=\"{t.Notes}\"\n");
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
