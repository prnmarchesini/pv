using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.GeoCommands))]

namespace UFV.Plugin;

/// <summary>
/// A localização geográfica do terreno: ver, definir e corrigir.
///
/// Corrigir é a parte que importa. A latitude alimenta a posição do sol, e
/// portanto o azimute das mesas e toda a conta de sombreamento. Sem um
/// caminho de volta, um sinal trocado na hora de digitar — 23 em vez de -23 —
/// ficaria gravado no desenho para sempre, pondo a usina no hemisfério errado,
/// e o usuário não teria onde ver nem como desfazer.
/// </summary>
public static class GeoCommands
{
    /// <summary>
    /// UFV_LOCAL: mostra a localização gravada e deixa informar outra.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoLocalizacao)]
    public static void Localizacao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            Mostrar(editor, documento);

            if (!UfvExtension.TemInterface())
            {
                // Sem interface não há a quem perguntar; mostrar já é o que
                // este comando pode fazer.
                return;
            }

            editor.WriteMessage(
                "\nInforme a localização do terreno. Negativo para sul e para oeste:\n"
                + "no Brasil, a latitude é sempre negativa.\n");

            if (!PerguntarGrau(editor, "Latitude", -90, 90, out var latitude)) return;
            if (!PerguntarGrau(editor, "Longitude", -180, 180, out var longitude)) return;

            var lugar = new GeoLocation(latitude, longitude, GeoLocationSource.Usuario);

            if (!lugar.IsValid)
            {
                editor.WriteMessage("  Valor fora dos limites; nada foi gravado.\n");
                return;
            }

            // Confirmação em palavras, e não em números: é o hemisfério que se
            // erra, e "23° N" salta aos olhos de quem esperava sul.
            editor.WriteMessage($"\n  Gravando: {lugar.Describe()}\n");

            if (lugar.Latitude > 0)
            {
                editor.WriteMessage(
                    "  ATENÇÃO: latitude positiva é hemisfério NORTE. "
                    + "No Brasil ela é negativa.\n");
            }

            GeoStore.Save(documento.Database, lugar);
            AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao definir a localização do terreno.", erro);
            editor.WriteMessage($"\nNão consegui definir a localização: {erro.Message}\n");
        }
    }

    private static void Mostrar(Editor editor, Document documento)
    {
        var gravada = GeoStore.Gravada(documento.Database);

        if (gravada is not null)
        {
            var origem = gravada.Source == GeoLocationSource.Desenho ? "do desenho" : "informada";
            editor.WriteMessage($"\nLocalização gravada: {gravada.Describe()}  ({origem})\n");
            return;
        }

        editor.WriteMessage("\nEste desenho ainda não tem localização gravada pelo plugin.\n");
    }

    private static bool PerguntarGrau(
        Editor editor,
        string rotulo,
        double minimo,
        double maximo,
        out double valor)
    {
        valor = 0;

        var resposta = editor.GetDouble(new PromptDoubleOptions($"\n  {rotulo} em graus: ")
        {
            AllowNone = false,
        });

        if (resposta.Status != PromptStatus.OK)
        {
            editor.WriteMessage("  Nada foi gravado.\n");
            return false;
        }

        if (resposta.Value < minimo || resposta.Value > maximo)
        {
            editor.WriteMessage($"  {rotulo} precisa ficar entre {minimo} e {maximo}.\n");
            return false;
        }

        valor = resposta.Value;
        return true;
    }

    /// <summary>
    /// Avisa que o que foi gravado só vira arquivo quando o desenho for salvo.
    ///
    /// O plugin escreve no banco em memória. Num desenho somente leitura, ou
    /// se o usuário fechar sem salvar, nada disso chega ao arquivo — e o
    /// plugin teria dito "gravei" sem ter gravado.
    /// </summary>
    internal static void AvisarSeNaoVaiSalvar(Editor editor, Document documento)
    {
        if (documento.IsReadOnly)
        {
            editor.WriteMessage(
                "  ATENÇÃO: este desenho está somente para leitura. O que foi gravado vale\n"
                + "  nesta sessão e se perde ao fechar.\n");
            return;
        }

        editor.WriteMessage("  (salve o desenho para isto ficar guardado no arquivo)\n");
    }
}
