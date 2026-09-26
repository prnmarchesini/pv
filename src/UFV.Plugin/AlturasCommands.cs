using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.AlturasCommands))]

namespace UFV.Plugin;

/// <summary>
/// As alturas dos pilares como análise que se liga, desliga e REGERA
/// (pedido do Renan em 26/09/2026: "os textos de ponta alta, baixa, etc.
/// precisam ficar em um menu de análises, onde eu posso ligar e desligar,
/// se eu apagar, nesse menu eu posso regerar"). Regerar apaga TUDO que
/// está na camada das alturas (inclusive texto órfão de versão antiga, sem
/// identidade) e redesenha as cotas a partir do XData dos pilares e do
/// contorno de cada mesa.
/// </summary>
public static class AlturasCommands
{
    /// <summary>UFV_ALTURAS_REGERAR: apaga e redesenha as cotas de todas as mesas.</summary>
    [CommandMethod(PluginInfo.ComandoAlturasRegerar)]
    public static void Regerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var (apagadas, mesas, cotas, semPontas) = Regerar(documento.Database);

            editor.WriteMessage(
                $"\nALTURAS {apagadas} entidade(s) apagada(s) da camada {LayoutLayers.Alturas}; "
                + $"{cotas} cota(s) redesenhada(s) em {mesas} mesa(s).\n");

            if (semPontas > 0)
            {
                editor.WriteMessage(
                    $"  {semPontas} pilar(es) de desenho antigo sem as alturas de ponta gravadas: só o P3 foi redesenhado. "
                    + "Recalcular a mesa grava as pontas.\n");
            }

            editor.Regen();
            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao regerar as alturas.", erro);
            editor.WriteMessage($"\nNão consegui regerar as alturas: {erro.Message}\n");
        }
    }

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));

    internal static (int Apagadas, int Mesas, int Cotas, int SemPontas) Regerar(Database database)
    {
        var apagadas = 0;
        var mesasFeitas = 0;
        var cotas = 0;
        var semPontas = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        // 1. As notas da camada das alturas: as com identidade de nota e o
        //    texto ou linha SEM XData nosso (órfão de versão antiga). Outra classe na camada é do usuário e
        //    fica. Só texto e linha são abertos: superfície e curvas de
        //    nível ficam fechadas. (O enumerador do espaço pula o que já foi
        //    apagado, por isso a varredura das mesas logo abaixo, na mesma
        //    transação, não tropeça nas notas.)
        var deixadas = 0;

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDoTexto && id.ObjectClass != ClasseDaLinha)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity outra
                    && string.Equals(outra.Layer, LayoutLayers.Alturas, StringComparison.OrdinalIgnoreCase))
                    deixadas++;
                continue;
            }

            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var nossa = LayoutXData.LoadNote(entidade) is not null;
            var naCamada = string.Equals(entidade.Layer, LayoutLayers.Alturas, StringComparison.OrdinalIgnoreCase);
            bool semXData;

            using (var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo))
                semXData = dados is null;

            // Só a camada das alturas: o aviso "NÃO CABE" também é nota, mas
            // mora na camada de marcadas e não é regerado aqui.
            if (!naCamada || !(nossa || semXData)) continue;

            entidade.UpgradeOpen();
            entidade.Erase();
            apagadas++;
        }

        if (deixadas > 0) RegistroDeDiagnostico.Registrar($"Regerar alturas: {deixadas} entidade(s) de outro tipo deixada(s) na camada {LayoutLayers.Alturas}.");

        var desligada = LayoutLayers.EstaDesligada(transacao, database, LayoutLayers.Alturas) ?? true;
        var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Alturas, new RgbColor(200, 200, 200), desligada: desligada);

        // 2. As cotas de cada mesa, pelo contorno e pelo XData dos pilares.
        foreach (var (guid, mesa) in LayoutScan.Tables(transacao, database))
        {
            if (mesa.Identity is null || mesa.Contour is not { } contorno || mesa.Pillars.Count == 0) continue;

            var cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao);
            if (cantos.Count != 4) continue;

            var baixa = new Point3(cantos[1].X - cantos[0].X, cantos[1].Y - cantos[0].Y, cantos[1].Z - cantos[0].Z);
            var comprimento = Math.Sqrt(baixa.X * baixa.X + baixa.Y * baixa.Y + baixa.Z * baixa.Z);
            if (comprimento < RowDistributor.MenorMedida) continue;

            var direcao = new Point3(baixa.X / comprimento, baixa.Y / comprimento, baixa.Z / comprimento);
            var rumo = LayoutDrawer.RumoLegivel(direcao.X, direcao.Y);
            var feita = false;

            foreach (var id in mesa.Pillars)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference bloco) continue;
                if (LayoutXData.LoadPillar(bloco) is not { } pilar) continue;

                var t = Math.Clamp(pilar.Station / comprimento, 0, 1);
                var pontaBaixa = new Point3(cantos[0].X + (cantos[1].X - cantos[0].X) * t, cantos[0].Y + (cantos[1].Y - cantos[0].Y) * t, cantos[0].Z + (cantos[1].Z - cantos[0].Z) * t);
                var pontaAlta = new Point3(cantos[3].X + (cantos[2].X - cantos[3].X) * t, cantos[3].Y + (cantos[2].Y - cantos[3].Y) * t, cantos[3].Z + (cantos[2].Z - cantos[3].Z) * t);
                var topo = new Point3(bloco.Position.X, bloco.Position.Y, bloco.Position.Z);

                if (pilar.Problem is { } problema)
                {
                    LayoutDrawer.AvisoDePilar(transacao, espaco, camada, guid, problema, topo, rumo);
                    cotas++;
                    feita = true;
                    continue;
                }

                if (pilar.LowEdgeClearance is null && pilar.HighEdgeClearance is null)
                {
                    semPontas++;
                }
                else
                {
                    LayoutDrawer.Cota(transacao, espaco, camada, guid, pilar.LowEdgeClearance, "PB", pontaBaixa, direcao, rumo);
                    LayoutDrawer.Cota(transacao, espaco, camada, guid, pilar.HighEdgeClearance, "PA", pontaAlta, direcao, rumo);
                    cotas += 2;
                }

                LayoutDrawer.Cota(transacao, espaco, camada, guid, pilar.FreeHeight, "P3", topo, direcao, rumo);
                cotas++;
                feita = true;
            }

            if (feita) mesasFeitas++;
        }

        transacao.Commit();

        return (apagadas, mesasFeitas, cotas, semPontas);
    }
}
