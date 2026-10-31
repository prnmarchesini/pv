using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ResumoDaMesaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// O resumo da mesa (42.9, Renan, 31/10/2026: "quero clicar com o botão
/// direito, no menu Clivus de um objeto, quero ter resumo [...] a inclinação
/// da mesa em relação ao azimute (o tilt), a inclinação do terreno e para qual
/// lado, quantos módulos, potência, tipo de estrutura"). A mesa vem da
/// seleção (o botão direito) ou do clique; o resumo sai numa janela e na linha
/// de comando, e as contas são do Core (<see cref="TableSummaries"/>), pela
/// mesa como está desenhada.
/// </summary>
public static class ResumoDaMesaCommands
{
    /// <summary>CLIVUS_RESUMO_MESA: a mesa selecionada (ou clicada) e o resumo dela.</summary>
    [CommandMethod(PluginInfo.ComandoResumoMesa, CommandFlags.UsePickSet)]
    public static void Resumo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var mesa = RecalcularCommands.MesaDaSelecao(editor, documento)
                ?? RecalcularCommands.MesaClicada(editor, documento, Tr.T("\nClique numa peça da mesa: "));
            if (mesa is null) return;

            if (Montar(documento, mesa.Value) is not { } resumo)
            {
                editor.WriteMessage(Tr.T("\nRESUMO Não achei essa mesa no desenho.\n"));
                return;
            }

            editor.WriteMessage("\n" + resumo.Describe() + "\n");

            if (ClivusExtension.TemInterface()) AcadApp.ShowModalWindow(new JanelaDoResumoDaMesa(resumo));
            else editor.WriteMessage(LinhaParaTeste(resumo));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no resumo da mesa.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui montar o resumo: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// O resumo de uma mesa do desenho: a identidade, as faces dos módulos, os
    /// pilares, o tipo (e o módulo dele), o terreno e o lugar. Null se a mesa
    /// não está no desenho.
    /// </summary>
    internal static TableSummary? Montar(Document documento, Guid mesa)
    {
        var database = documento.Database;
        var tipos = MesasDoDesenho.Ler(database);
        var carports = CarportsDoDesenho.Ler(database);
        var terreno = TerrainCache.Get(documento);
        var lugar = SombrasCommands.Lugar(documento);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        if (!LayoutScan.Tables(transacao, database).TryGetValue(mesa, out var partes) || partes.Identity is not { } identidade)
            return null;

        var faces = new List<PvModuleFace>();
        foreach (var id in partes.Faces)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Face face || LayoutXData.LoadFace(face) is not { } f) continue;

            var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
            faces.Add(new PvModuleFace(f.Column, f.Row, cantos));
        }

        var pilares = new List<PillarIdentity>();
        foreach (var id in partes.Pillars)
            if (transacao.GetObject(id, OpenMode.ForRead) is Entity pilar && LayoutXData.LoadPillar(pilar) is { } p) pilares.Add(p);

        var contorno = new List<Point3>();
        if (partes.Contour is { } idDoContorno && transacao.GetObject(idDoContorno, OpenMode.ForRead) is Polyline3d linha)
            foreach (ObjectId v in linha)
                if (!v.IsErased && transacao.GetObject(v, OpenMode.ForRead) is PolylineVertex3d vertice) contorno.Add(new Point3(vertice.Position.X, vertice.Position.Y, vertice.Position.Z));

        transacao.Commit();

        var perfil = DrawingTables.Find(tipos, identidade.ProfileName)?.Profile;
        var carport = identidade.ProfileName is { Length: > 0 } nome ? DrawingCarports.Find(carports, nome)?.Profile : null;

        // Sem tipo no desenho (desenho de antes das mesas cadastradas), o módulo
        // é o do perfil que fez a mesa, como no TermoVision: o da janela de Mesa
        // pelo comprimento do contorno, se a medida dele bate com a das faces.
        var modulo = perfil?.Layout.Module ?? carport?.Array.Module;
        if (modulo is null && carport is null && contorno.Count == 4 && faces.Count > 0)
        {
            var provavel = FileiraCommands.PerfilDaMesaDesenhada(contorno, FileiraCommands.PerfilDaMesa(documento.Editor, silencioso: true), identidade.ProfileName, database).Layout.Module;
            var c = faces[0].Corners;
            var lados = new[] { Distancia(c[0], c[1]), Distancia(c[1], c[2]) };
            if (Math.Abs(lados.Min() - Math.Min(provavel.Width, provavel.Height)) < 0.002 && Math.Abs(lados.Max() - Math.Max(provavel.Width, provavel.Height)) < 0.002)
                modulo = provavel;
        }

        var estrutura = carport is not null ? Tr.T("Carport")
            : perfil?.IsBifila == true || pilares.Any(p => p.Line == PillarLine.Rear) ? Tr.T("Biposte (dois pilares por pórtico)")
            : Tr.T("Monoposte");

        Func<double, double, double?>? cota = terreno is null ? null : (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

        return TableSummaries.Build(new TableSummaryInput(
            mesa,
            identidade.Label,
            perfil?.Name ?? carport?.Name,
            estrutura,
            modulo,
            identidade.ModulePowerWatts,
            faces,
            identidade.TiltRadians,
            pilares,
            identidade.Dirty,
            identidade.DirtyReason,
            identidade.Marked,
            identidade.Reason,
            lugar is { IsValid: true } l ? l.Latitude : null,
            cota));
    }

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>A linha invariante para o nível 2 (sem interface): os números do resumo.</summary>
    private static string LinhaParaTeste(TableSummary r)
    {
        static string N(double? v) => v is { } x ? x.ToString("0.###", CultureInfo.InvariantCulture) : "-";

        return $"\nRESUMO_MESA modulos={r.Modules} colunas={r.Columns} fileiras={r.Rows} tilt={N(r.TiltDegrees)} tiltProjeto={N(r.DesignTiltDegrees)} "
            + $"voltada={N(r.FacingDegrees)} pvsyst={N(r.PvsystAzimuthDegrees)} giro={N(r.AxisSlopePercent)} terreno={N(r.TerrainSlopePercent)} "
            + $"terrenoDesce={N(r.TerrainDownhillDegrees)} pilares={r.Pillars} kwp={N(r.PowerKwp)} comprimento={N(r.Length)} fim\n";
    }
}
