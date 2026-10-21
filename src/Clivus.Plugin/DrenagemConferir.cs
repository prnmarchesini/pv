#if DEBUG
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.DrenagemConferir))]

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 da drenagem): confere contra o TIN cada
/// ponto do que a drenagem desenhou, e os ganchos que fazem o caminho da
/// janela sem a janela (memória "testar pelo caminho da tela").
/// </summary>
public static class DrenagemConferir
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>CLIVUS_DRN_CONFERIR_AUTO: escreve "DRN_CONFERIR ..." com as contagens.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemConferirAutomatico)]
    public static void Conferir()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var terreno = TerrainCache.Get(documento);
        if (terreno is null)
        {
            documento.Editor.WriteMessage("\nDRN_CONFERIR sem_terreno\n");
            return;
        }

        var tin = terreno.Mesh;
        var database = documento.Database;

        int linhas = 0, ok = 0, fora = 0, semTerreno = 0, pontos = 0;

        void Ponto(double x, double y, double z)
        {
            if (!tin.TryGetZ(x, y, out var zt)) { semTerreno++; return; }
            if (Math.Abs(z - (zt + FlechasDeDrenagem.Acima)) <= 0.01) ok++; else fora++;
        }

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

            if (tabela.Has(FlechasDeDrenagem.NomeDoBloco))
            {
                var bloco = (BlockTableRecord)transacao.GetObject(tabela[FlechasDeDrenagem.NomeDoBloco], OpenMode.ForRead);

                foreach (ObjectId id in bloco)
                {
                    switch (transacao.GetObject(id, OpenMode.ForRead))
                    {
                        case Line l:
                            linhas++;
                            Ponto(l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z);
                            Ponto(l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z);
                            break;
                        case Circle c:
                            pontos++;
                            Ponto(c.Center.X, c.Center.Y, c.Center.Z);
                            break;
                    }
                }
            }

            transacao.Commit();
        }

        documento.Editor.WriteMessage(string.Format(Inv,
            "\nDRN_CONFERIR flechas_linhas={0} pontos={1} ok={2} fora={3} sem_terreno={4} fim\n",
            linhas, pontos, ok, fora, semTerreno));
    }

    /// <summary>(clivus-drn-flechas-pela-janela "Areas"|"Tudo" espacamento): o botão Gerar da janela, fora de comando.</summary>
    [LispFunction("CLIVUS-DRN-FLECHAS-PELA-JANELA")]
    public static object? FlechasPelaJanela(ResultBuffer argumentos)
    {
        var valores = argumentos?.AsArray().Select(v => Convert.ToString(v.Value, Inv) ?? string.Empty).ToArray() ?? [];
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 2 || documento is null) return null;

        var onde = valores[0] == "Tudo" ? OndeFlechas.Tudo : OndeFlechas.Areas;
        var configuracao = new ArrowSettings { Spacing = double.Parse(valores[1], Inv) };

        try
        {
            var frase = EscritaForaDeComando.Fazer(documento, () => FlechasCommands.GerarPelaJanela(documento, onde, configuracao));
            documento.Editor.WriteMessage($"\n{frase}\n");
            return true;
        }
        catch (System.Exception erro)
        {
            documento.Editor.WriteMessage($"\nFLECHAS_ERRO {erro.Message}\n");
            return null;
        }
    }
}
#endif
