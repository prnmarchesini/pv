using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AnalisesIndependentesCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Os botões do menu Análises (passos 8.9 a 8.11): para cada análise
/// (ponta baixa, ponta alta, declividade, pilar), inserir os textos,
/// analisar (pintar pela regra), apagar os textos, tirar as cores e
/// quantificar. Cada comando é uma operação, uma transação, e não toca no
/// que outra análise fez.
/// </summary>
public static class AnalisesIndependentesCommands
{
    private enum Acao
    {
        Inserir,
        Analisar,
        AnalisarAutomatico,
        Apagar,
        TirarCores,
        Quantificar,
    }

    [CommandMethod(PluginInfo.ComandoAnPontaBaixaInserir)] public static void PbInserir() => Rodar(IndependentKind.LowEdge, Acao.Inserir);
    [CommandMethod(PluginInfo.ComandoAnPontaBaixaAnalisar)] public static void PbAnalisar() => Rodar(IndependentKind.LowEdge, Acao.Analisar);
    [CommandMethod(PluginInfo.ComandoAnPontaBaixaAnalisarAutomatico)] public static void PbAnalisarAuto() => Rodar(IndependentKind.LowEdge, Acao.AnalisarAutomatico);
    [CommandMethod(PluginInfo.ComandoAnPontaBaixaApagar)] public static void PbApagar() => Rodar(IndependentKind.LowEdge, Acao.Apagar);
    [CommandMethod(PluginInfo.ComandoAnPontaBaixaTirarCores)] public static void PbCores() => Rodar(IndependentKind.LowEdge, Acao.TirarCores);
    [CommandMethod(PluginInfo.ComandoAnPontaBaixaQuantificar)] public static void PbQuantificar() => Rodar(IndependentKind.LowEdge, Acao.Quantificar);

    [CommandMethod(PluginInfo.ComandoAnPontaAltaInserir)] public static void PaInserir() => Rodar(IndependentKind.HighEdge, Acao.Inserir);
    [CommandMethod(PluginInfo.ComandoAnPontaAltaAnalisar)] public static void PaAnalisar() => Rodar(IndependentKind.HighEdge, Acao.Analisar);
    [CommandMethod(PluginInfo.ComandoAnPontaAltaAnalisarAutomatico)] public static void PaAnalisarAuto() => Rodar(IndependentKind.HighEdge, Acao.AnalisarAutomatico);
    [CommandMethod(PluginInfo.ComandoAnPontaAltaApagar)] public static void PaApagar() => Rodar(IndependentKind.HighEdge, Acao.Apagar);
    [CommandMethod(PluginInfo.ComandoAnPontaAltaTirarCores)] public static void PaCores() => Rodar(IndependentKind.HighEdge, Acao.TirarCores);
    [CommandMethod(PluginInfo.ComandoAnPontaAltaQuantificar)] public static void PaQuantificar() => Rodar(IndependentKind.HighEdge, Acao.Quantificar);

    [CommandMethod(PluginInfo.ComandoAnDeclividadeInserir)] public static void DeclInserir() => Rodar(IndependentKind.Slope, Acao.Inserir);
    [CommandMethod(PluginInfo.ComandoAnDeclividadeAnalisar)] public static void DeclAnalisar() => Rodar(IndependentKind.Slope, Acao.Analisar);
    [CommandMethod(PluginInfo.ComandoAnDeclividadeAnalisarAutomatico)] public static void DeclAnalisarAuto() => Rodar(IndependentKind.Slope, Acao.AnalisarAutomatico);
    [CommandMethod(PluginInfo.ComandoAnDeclividadeApagar)] public static void DeclApagar() => Rodar(IndependentKind.Slope, Acao.Apagar);
    [CommandMethod(PluginInfo.ComandoAnDeclividadeTirarCores)] public static void DeclCores() => Rodar(IndependentKind.Slope, Acao.TirarCores);
    [CommandMethod(PluginInfo.ComandoAnDeclividadeQuantificar)] public static void DeclQuantificar() => Rodar(IndependentKind.Slope, Acao.Quantificar);

    [CommandMethod(PluginInfo.ComandoAnPilarInserir)] public static void PilarInserir() => Rodar(IndependentKind.PillarAbove, Acao.Inserir);
    [CommandMethod(PluginInfo.ComandoAnPilarAnalisar)] public static void PilarAnalisar() => Rodar(IndependentKind.PillarAbove, Acao.Analisar);
    [CommandMethod(PluginInfo.ComandoAnPilarAnalisarAutomatico)] public static void PilarAnalisarAuto() => Rodar(IndependentKind.PillarAbove, Acao.AnalisarAutomatico);
    [CommandMethod(PluginInfo.ComandoAnPilarApagar)] public static void PilarApagar() => Rodar(IndependentKind.PillarAbove, Acao.Apagar);
    [CommandMethod(PluginInfo.ComandoAnPilarTirarCores)] public static void PilarCores() => Rodar(IndependentKind.PillarAbove, Acao.TirarCores);
    [CommandMethod(PluginInfo.ComandoAnPilarQuantificar)] public static void PilarQuantificar() => Rodar(IndependentKind.PillarAbove, Acao.Quantificar);
    [CommandMethod(PluginInfo.ComandoAnPilarEnterradoInserir)] public static void PilarEnterradoInserir() => Rodar(IndependentKind.PillarBuried, Acao.Inserir);
    [CommandMethod(PluginInfo.ComandoAnPilarTotalInserir)] public static void PilarTotalInserir() => Rodar(IndependentKind.PillarLength, Acao.Inserir);

    /// <summary>CLIVUS_AN_TESTE_PECAS: para o nível 2, a ponta baixa com uma regra que pega tudo, pintando os módulos.</summary>
    [CommandMethod(PluginInfo.ComandoAnTestePecas)]
    public static void TestePecas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            // Abaixo de 100 m pega toda ponta: todo módulo, das mesas marcadas também, tem que sair pintado.
            var regra = new ThresholdRule(100, RgbColor.Red, null, RgbColor.Blue, PaintPieces: true);
            var (textos, pecas) = AnalisesIndependentes.Analisar(documento.Database, IndependentKind.LowEdge, regra, SlopeUnit.Percent);
            documento.Editor.WriteMessage($"\nANALISE_TESTE textos={textos} pecas={pecas}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_AN_TESTE_PECAS.", erro);
            documento.Editor.WriteMessage($"\nNão consegui: {erro.Message}\n");
        }
    }

    // ------------------------------------------------------------- regra

    /// <summary>A regra gravada no desenho para esta análise, ou a padrão.</summary>
    internal static ThresholdRule Regra(Database database, IndependentKind tipo)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, IndependentAnalysis.StorageKey(tipo));
            var campos = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            return campos is null ? IndependentAnalysis.Default(tipo) : IndependentAnalysis.Decode(campos, tipo);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Não consegui ler a regra da análise {tipo}.", erro);
            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\n  ATENÇÃO: a regra gravada de {IndependentAnalysis.Name(tipo)} não pôde ser lida; valeu a padrão.\n");
            return IndependentAnalysis.Default(tipo);
        }
    }

    internal static void GravarRegra(Database database, IndependentKind tipo, ThresholdRule regra) =>
        PluginDictionary.Save(database, IndependentAnalysis.StorageKey(tipo), new ResultBuffer(
            IndependentAnalysis.Encode(regra).Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));

    /// <summary>A unidade da declividade (a mesma da seta, gravada no desenho).</summary>
    internal static SlopeUnit Unidade(Database database) => SetaDeDeclividade.Ler(database).Unidade;

    // ------------------------------------------------------------- execução

    private static void Rodar(IndependentKind tipo, Acao acao)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var nome = IndependentAnalysis.Name(tipo);

        try
        {
            var database = documento.Database;
            var unidade = Unidade(database);

            switch (acao)
            {
                case Acao.Inserir:
                {
                    var (criados, mesas, ignoradas) = AnalisesIndependentes.Inserir(database, tipo, unidade);

                    editor.WriteMessage(mesas == 0
                        ? $"\nANÁLISE {nome}: o desenho não tem mesa gerada pelo plugin; nada inserido.\n"
                        : $"\nANÁLISE {nome}: {criados} entidade(s) de texto em {mesas} mesa(s), na camada {IndependentAnalysis.LayerName(tipo)}.\n");

                    if (mesas > 0 && criados == 0)
                        editor.WriteMessage("  As mesas não têm pilares com identidade: recalcule-as para a análise ter o que mostrar.\n");

                    if (ignoradas > 0)
                        editor.WriteMessage($"  {ignoradas} mesa(s) com contorno deformado (sem quatro cantos) ficaram de fora; use Validar.\n");
                    break;
                }

                case Acao.Analisar:
                case Acao.AnalisarAutomatico:
                {
                    var regra = Regra(database, tipo);

                    if (acao == Acao.Analisar)
                    {
                        if (!ClivusExtension.TemInterface())
                        {
                            editor.WriteMessage("\nA janela da regra precisa da interface do Civil 3D.\n");
                            return;
                        }

                        var escolha = PerguntarRegra(tipo, regra, unidade);
                        if (escolha is null) return;

                        if (escolha.Value.Unidade != unidade)
                        {
                            // Os textos guardam o valor na unidade em que
                            // nasceram: trocada a unidade, saem de novo nela,
                            // senão os limites novos comparariam % com graus.
                            SetaDeDeclividade.Gravar(database, SetaDeDeclividade.Ler(database).Ligada, escolha.Value.Unidade);
                            unidade = escolha.Value.Unidade;

                            if (AnalisesIndependentes.Apagar(database, tipo) > 0)
                            {
                                AnalisesIndependentes.Inserir(database, tipo, unidade);
                                editor.WriteMessage("\n  Unidade da declividade trocada: os textos foram inseridos de novo nela.\n");
                            }
                        }

                        regra = escolha.Value.Regra;
                        GravarRegra(database, tipo, regra);
                    }

                    var (textos, pecas) = AnalisesIndependentes.Analisar(database, tipo, regra, unidade);
                    editor.WriteMessage($"\nANÁLISE {nome}: {textos} texto(s) pintado(s)"
                        + (regra.PaintPieces ? $", {pecas} peça(s) pintada(s)" : "") + ".\n");

                    if (textos == 0 && !regra.PaintPieces)
                        editor.WriteMessage("  Nenhum texto desta análise no desenho: insira os textos primeiro.\n");
                    break;
                }

                case Acao.Apagar:
                {
                    var apagadas = AnalisesIndependentes.Apagar(database, tipo);
                    editor.WriteMessage($"\nANÁLISE {nome}: {apagadas} entidade(s) de texto apagada(s).\n");
                    break;
                }

                case Acao.TirarCores:
                {
                    var mexidas = AnalisesIndependentes.TirarCores(database, tipo);
                    editor.WriteMessage($"\nANÁLISE {nome}: {mexidas} entidade(s) de volta à cor de antes.\n");
                    break;
                }

                case Acao.Quantificar:
                {
                    var regra = Regra(database, tipo);
                    var (pontos, modulos) = AnalisesIndependentes.Quantificar(database, tipo, regra, unidade);

                    QuantificacaoGravada.Gravar(database, tipo, regra, unidade, pontos, modulos);

                    var oQue = tipo switch
                    {
                        IndependentKind.Slope => "mesas",
                        _ => "pilares",
                    };

                    editor.WriteMessage($"\nQUANTIFICAR {IndependentAnalysis.Describe(tipo, regra, pontos, unidade)} — em {oQue}.\n");

                    if (modulos is not null)
                        editor.WriteMessage($"  módulos: {IndependentAnalysis.Describe(tipo, regra, modulos, unidade)}.\n");
                    break;
                }
            }

            editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha na análise {tipo} ({acao}).", erro);
            editor.WriteMessage($"\nNão consegui fazer a análise de {nome}: {erro.Message}\n");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ThresholdRule Regra, SlopeUnit Unidade)? PerguntarRegra(IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade)
    {
        var janela = new JanelaDeAnalise(tipo, regra, unidade);
        if (AcadApp.ShowModalWindow(janela) != true || janela.Regra is null) return null;
        return (janela.Regra, janela.Unidade);
    }
}
