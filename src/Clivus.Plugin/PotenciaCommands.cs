using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.PotenciaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A potência do módulo e o PAN na estrutura (Melhorias de 10/10/2026):
/// <list type="bullet">
/// <item>CLIVUS_TROCAR_POTENCIA (item 14, botão direito da área): a potência
/// em Wp, uma simulação para a usina inteira (kWp e resumos), que desliga os
/// cálculos elétricos; "Mesa" desfaz.</item>
/// <item>CLIVUS_POTENCIA_PELO_PAN (item 15): as mesas desenhadas de uma
/// estrutura passam à potência do PAN dela, sem mudar mais nada.</item>
/// <item>CLIVUS_ESTRUTURA_PAN (item 15): carrega o .PAN numa estrutura do
/// desenho (o mesmo que o "Carregar .PAN…" da janela de Mesa seguido de
/// "Salvar no desenho").</item>
/// </list>
/// </summary>
public static class PotenciaCommands
{
    /// <summary>A palavra que desfaz a potência trocada pela área.</summary>
    private const string Mesa = "Mesa";

    [CommandMethod(PluginInfo.ComandoTrocarPotencia, CommandFlags.Modal | CommandFlags.UsePickSet)]
    public static void TrocarPotencia()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var db = documento.Database;
            var atual = FonteDoModulo.Simulada(db);

            editor.WriteMessage(atual is null
                ? Tr.T("\nTROCAR POTÊNCIA: simulação para a usina inteira. O tamanho das mesas não muda; o kWp e os resumos passam a usar a potência digitada, e os cálculos elétricos ficam desligados.\n")
                : Tr.F("\nTROCAR POTÊNCIA: hoje a usina está simulada com {0:0.#} Wp por módulo. Digite outra potência, ou {1} para voltar à configuração da mesa.\n", atual.Watts, Mesa));

            var opcoes = new PromptDoubleOptions(Tr.F("\nPotência do módulo, em Wp, ou [{0}]: ", Mesa))
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false,
                AppendKeywordsToMessage = false,
            };
            opcoes.Keywords.Add(Mesa);

            var resposta = editor.GetDouble(opcoes);

            if (resposta.Status == PromptStatus.Keyword)
            {
                editor.WriteMessage("\n" + UsarAMesa(documento) + "\n");
                editor.WriteMessage("POTENCIA_SIMULADA nenhuma\n");
                return;
            }

            if (resposta.Status != PromptStatus.OK) return;

            var nova = new SimulatedModulePower(resposta.Value);
            if (nova.WhyInvalid() is { } porque)
            {
                editor.WriteMessage(Tr.F("\nNão troquei: {0}.\n", porque));
                return;
            }

            FonteDoModulo.GravarSimulada(db, nova);
            editor.WriteMessage(string.Format(CultureInfo.InvariantCulture, "\nPOTENCIA_SIMULADA watts={0:0.###}\n", nova.Watts));
            editor.WriteMessage(Tr.F("Potência do módulo trocada para {0:0.#} Wp na usina inteira (simulação). Os cálculos elétricos ficam desligados até você escolher \"Usar a configuração da mesa\" na rota de cabos, no resumo ou na configuração elétrica.\n", nova.Watts));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trocar a potência do módulo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui trocar a potência: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Desfaz a potência trocada pela área: a usina volta à potência de cada
    /// mesa e os cálculos elétricos voltam. É o que o botão "Usar a
    /// configuração da mesa…" das janelas faz (fora de comando, com o
    /// documento travado) e o "Mesa" do comando.
    /// </summary>
    internal static string UsarAMesa(Document documento)
    {
        if (FonteDoModulo.Simulada(documento.Database) is null)
            return Tr.T("A usina já usa a configuração da mesa: não havia potência trocada pela área.");

        FonteDoModulo.GravarSimulada(documento.Database, null);
        return Tr.T("A usina voltou a usar a potência do módulo de cada mesa, e os cálculos elétricos voltaram.");
    }

    [CommandMethod(PluginInfo.ComandoPotenciaPeloPan)]
    public static void PotenciaPeloPan()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var db = documento.Database;
            var estruturas = MesasDoDesenho.Ler(db).ToList();
            if (estruturas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nNenhuma estrutura cadastrada no desenho (Configurações > Estruturas).\n"));
                return;
            }

            var nome = editor.GetString(new PromptStringOptions(Tr.T("\nEstrutura (nome; Enter: todas): ")) { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK) return;

            var alvo = string.IsNullOrWhiteSpace(nome.StringResult) ? estruturas : [.. new[] { DrawingTables.Find(estruturas, nome.StringResult) }.OfType<DrawingTable>()];
            if (alvo.Count == 0)
            {
                editor.WriteMessage(Tr.F("\nNão há estrutura chamada \"{0}\" no desenho.\n", nome.StringResult.Trim()));
                return;
            }

            foreach (var estrutura in alvo.ToList())
            {
                var (mesas, watts, frase) = AtualizarPotencia(db, estruturas, estrutura);
                editor.WriteMessage(string.Format(CultureInfo.InvariantCulture, "\nPOTENCIA_PAN estrutura=\"{0}\" mesas={1} watts={2:0.###}\n", estrutura.Name, mesas, watts));
                editor.WriteMessage(frase + "\n");
            }

            MesasDoDesenho.Gravar(db, estruturas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar a potência pelo PAN.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui atualizar a potência: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Atualiza as mesas desenhadas de uma estrutura para a potência do PAN
    /// dela, e a própria estrutura na lista (a potência do módulo passa a ser
    /// a do PAN). Quem chama grava a lista. Quantas mesas mudaram, a
    /// potência nova e a frase do que foi feito.
    /// </summary>
    internal static (int Mesas, double Watts, string Frase) AtualizarPotencia(Autodesk.AutoCAD.DatabaseServices.Database db, List<DrawingTable> estruturas, DrawingTable estrutura)
    {
        var watts = ModuleSource.TargetPower(estrutura.Profile);
        var mesas = FonteDoModulo.AtualizarPotencia(db, estruturas, estrutura);

        var i = estruturas.IndexOf(estrutura);
        if (i >= 0) estruturas[i] = estrutura with { Profile = estrutura.Profile.WithModulePower(watts) };

        return (mesas, watts, estrutura.Profile.ModuleElectrical is null
                ? Tr.F("Estrutura \"{0}\" sem PAN: {1} mesa(s) desenhada(s) passaram para a potência do módulo dela, {2:0.#} Wp. Só a potência mudou.", estrutura.Name, mesas, watts)
                : Tr.F("Estrutura \"{0}\": {1} mesa(s) desenhada(s) passaram para a potência do PAN, {2:0.#} Wp. Só a potência mudou.", estrutura.Name, mesas, watts));
    }

    [CommandMethod(PluginInfo.ComandoEstruturaPan)]
    public static void EstruturaPan()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var db = documento.Database;
            var estruturas = MesasDoDesenho.Ler(db).ToList();
            if (estruturas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nNenhuma estrutura cadastrada no desenho (Configurações > Estruturas).\n"));
                return;
            }

            var nome = editor.GetString(new PromptStringOptions(Tr.T("\nEstrutura (nome; Enter: a única do desenho): ")) { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK) return;

            var estrutura = string.IsNullOrWhiteSpace(nome.StringResult) && estruturas.Count == 1 ? estruturas[0] : DrawingTables.Find(estruturas, nome.StringResult);
            if (estrutura is null)
            {
                editor.WriteMessage(Tr.F("\nNão há estrutura chamada \"{0}\" no desenho.\n", nome.StringResult.Trim()));
                return;
            }

            var caminho = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo .PAN: ")) { AllowSpaces = true });
            if (caminho.Status != PromptStatus.OK) return;

            var arquivo = caminho.StringResult.Trim().Trim('"');
            if (LerPan(arquivo, out var porque) is not { } pan)
            {
                editor.WriteMessage("\n" + porque + "\n");
                return;
            }

            var antes = estrutura.Profile.Layout.Module.PowerWatts;
            var nova = estrutura with { Profile = estrutura.Profile.WithPan(pan) };
            estruturas[estruturas.IndexOf(estrutura)] = nova;
            MesasDoDesenho.Gravar(db, estruturas);

            editor.WriteMessage(string.Format(CultureInfo.InvariantCulture, "\nESTRUTURA_PAN estrutura=\"{0}\" modelo={1} pmax={2:0.###} antes={3:0.###}\n", nova.Name, pan.Model.Trim().Replace(' ', '_'), pan.Pmax, antes));
            editor.WriteMessage(Tr.F("PAN carregado na estrutura \"{0}\": {1}.", nova.Name, pan.Describe()) + "\n");
            if (Math.Abs(antes - pan.Pmax) > ModuleSource.Tolerance)
                editor.WriteMessage(Tr.F("A potência do módulo da estrutura passou de {0:0.#} para {1:0.#} Wp. As mesas já desenhadas continuam como estavam: use \"Atualizar a potência das mesas desenhadas\".", antes, pan.Pmax) + "\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao carregar o PAN na estrutura.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui carregar o PAN: {0}\n", erro.Message));
        }
    }

    /// <summary>Lê o .PAN do disco, ou null com o porquê (nunca inventa valor). O mesmo da janela de Mesa.</summary>
    internal static PanModule? LerPan(string caminho, out string porque)
    {
        porque = string.Empty;

        if (!System.IO.File.Exists(caminho))
        {
            porque = Tr.F("Não achei o arquivo {0}.", caminho);
            return null;
        }

        if (PanModule.Load(caminho, out var faltam) is { } pan) return pan;

        porque = Tr.F("Não li o PAN {0}: faltou ou está ilegível {1}. Nenhum valor foi inventado.", System.IO.Path.GetFileName(caminho), string.Join(", ", faltam));
        return null;
    }
}
