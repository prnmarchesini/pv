using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ConfiguracaoEletricaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ELETRICA (etapas 12 a 15): a janela da configuração elétrica, com
/// as abas Subestação, Transformador, Inversor e Numeração (esta vem de
/// <see cref="PainelDeNumeracao"/>). Os comandos de campo (posicionar o
/// retângulo) são chamados pelos botões da janela e também podem ser
/// digitados; no fim, a janela volta.
/// </summary>
public static class ConfiguracaoEletricaCommands
{
    [CommandMethod(PluginInfo.ComandoEletrica)]
    public static void Eletrica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela da configuração elétrica precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaEletrica.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a configuração elétrica.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a configuração elétrica: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_POSICIONAR (12.3, 13.2, 14.6): o equipamento (tag,
    /// código ou GUID) e o ponto. O retângulo nasce com a base 0,80 m acima
    /// da cota do TERRENO no ponto (nunca a do clique); se o equipamento já
    /// estava em campo, o mesmo retângulo é movido. O vínculo elétrico não muda.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaPosicionar)]
    public static void Posicionar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var qual = editor.GetString(new PromptStringOptions(Tr.T("\nEquipamento (tag ou código): ")) { AllowSpaces = true });
            if (qual.Status != PromptStatus.OK) return;

            var (setup, _) = ConfiguracaoEletricaStore.Ler(documento.Database);
            var achados = setup.FindEquipment(qual.StringResult);

            if (achados.Count != 1)
            {
                editor.WriteMessage(achados.Count == 0
                    ? Tr.F("\nEQUIPAMENTO Não há equipamento \"{0}\" no cadastro.\n", qual.StringResult.Trim())
                    : Tr.F("\nEQUIPAMENTO Mais de um equipamento responde por \"{0}\"; use a janela da configuração elétrica.\n", qual.StringResult.Trim()));
                return;
            }

            var equipamento = achados[0];

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var ponto = editor.GetPoint(new PromptPointOptions(Tr.F("\nOnde pôr {0} (centro do retângulo): ", equipamento.Tag)));
            if (ponto.Status != PromptStatus.OK) return;

            // O clique é em coordenadas do usuário; a cota vem do terreno.
            var mundo = ponto.Value.TransformBy(editor.CurrentUserCoordinateSystem);

            if (!terreno.Mesh.TryGetZ(mundo.X, mundo.Y, out var chao))
            {
                editor.WriteMessage(Tr.F("\nEQUIPAMENTO Esse ponto está fora do terreno; {0} não foi posto.\n", equipamento.Tag));
                return;
            }

            var baseZ = EquipmentFootprint.BaseElevation(chao);
            var cantos = EquipmentFootprint.Corners(mundo.X, mundo.Y, equipamento.Size.Width, equipamento.Size.Length)
                .Select(c => terreno.Mesh.TryGetZ(c.X, c.Y, out var z) ? z : double.NaN);
            var enterrados = EquipmentFootprint.BuriedCorners(baseZ, cantos);

            EquipamentoEmCampo.Posicionar(documento.Database, equipamento, new Point3d(mundo.X, mundo.Y, baseZ));

            editor.WriteMessage(Tr.F("\nEQUIPAMENTO {0} em campo: terreno a {1:0.000} m, base a {2:0.000} m (0,80 m acima do terreno).\n", equipamento.Tag, chao, baseZ));
            if (enterrados > 0)
                editor.WriteMessage(Tr.F("  ATENÇÃO: o terreno passa da base do retângulo em {0} canto(s); o símbolo entra no chão ali.\n", enterrados));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao posicionar equipamento em campo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui pôr o equipamento em campo: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento);
        }
    }
}
