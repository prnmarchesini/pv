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

        ConferirLinhas(documento.Editor, database, tin);
    }

    /// <summary>
    /// Cada linha da drenagem: vértices na cota do TIN (sem folga: a linha é o
    /// chão), quantas entidades o gráfico dela tem e o texto do título, para o
    /// teste ver o gráfico mudar quando a linha muda.
    /// </summary>
    private static void ConferirLinhas(Autodesk.AutoCAD.EditorInput.Editor editor, Database database, Tin tin)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var linhas = LinhasDeDrenagem.Todas(transacao, database);
        var perfis = new Dictionary<string, (int Entidades, string Titulo)>();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e || e.Layer != LayoutLayers.DrenagemPerfil) continue;
            if (PluginXData.Load(e, "DrnPerfil", 1, 1) is not { } campos) continue;

            perfis.TryGetValue(campos[0], out var atual);
            var titulo = e is DBText t && t.TextString.Contains('·') ? t.TextString : atual.Titulo;
            perfis[campos[0]] = (atual.Entidades + 1, titulo ?? string.Empty);
        }

        foreach (var linha in linhas)
        {
            int ok = 0, fora = 0, semTerreno = 0;

            foreach (var p in linha.Vertices)
            {
                if (!tin.TryGetZ(p.X, p.Y, out var z)) { semTerreno++; continue; }
                if (Math.Abs(p.Z - z) <= 0.001) ok++; else fora++;
            }

            perfis.TryGetValue(linha.Identidade.Id.ToString("D"), out var perfil);

            editor.WriteMessage(string.Format(Inv,
                "\nDRN_LINHA nome={0} guid={1} canaleta={2} vertices={3} ok={4} fora={5} sem_terreno={6} perfil={7} titulo=[{8}] fim\n",
                linha.Identidade.Name.Replace(' ', '_'), linha.Identidade.Id.ToString("N"), linha.Identidade.IsChannel ? 1 : 0,
                linha.Vertices.Count, ok, fora, semTerreno, perfil.Entidades, perfil.Titulo));
        }

        var orfaos = perfis.Keys.Count(g => linhas.All(l => l.Identidade.Id.ToString("D") != g));
        editor.WriteMessage(string.Format(Inv, "\nDRN_LINHAS total={0} perfis_orfaos={1} fim\n", linhas.Count, orfaos));

        ConferirCanaletas(editor, transacao, database, tin, linhas);

        transacao.Commit();
    }

    /// <summary>
    /// Cada canaleta: o sólido (ou a malha) dela, o volume contra a área da
    /// parede vezes o comprimento em planta, as cotas do 3D contra o fundo e
    /// o terreno do perfil, e quantas pinturas de alerta.
    /// </summary>
    private static void ConferirCanaletas(Autodesk.AutoCAD.EditorInput.Editor editor, Transaction transacao, Database database, Tin tin, List<LinhaNoDesenho> linhas)
    {
        var configuracao = DrenagemStore.Ler(database);
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var por = new Dictionary<string, List<Entity>>();

        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (e.Layer != LayoutLayers.DrenagemCanaleta3D && e.Layer != LayoutLayers.DrenagemAlerta) continue;

            var campos = PluginXData.Load(e, Canaleta3D.Tipo3D, 1, 1) ?? PluginXData.Load(e, "DrnAlerta", 1, 1);
            if (campos is null) continue;

            if (!por.TryGetValue(campos[0], out var lista)) por[campos[0]] = lista = [];
            lista.Add(e);
        }

        var orfaos = por.Keys.Count(g => linhas.All(l => l.Identidade.Id.ToString("D") != g || !l.Identidade.IsChannel));
        var canaletas = 0;

        foreach (var linha in linhas.Where(l => l.Identidade.IsChannel))
        {
            canaletas++;
            var secao = linha.Identidade.Section!;
            var (_, perfil) = LinhasDeDrenagem.Perfil(tin, configuracao, linha);
            var entidades = por.GetValueOrDefault(linha.Identidade.Id.ToString("D")) ?? [];

            var solidos = entidades.OfType<Solid3d>().ToList();
            var malhas = entidades.OfType<PolyFaceMesh>().Count();
            var alertas = entidades.Count(e => e.Layer == LayoutLayers.DrenagemAlerta);

            double volume = 0, zmin = double.NaN, zmax = double.NaN;
            foreach (var s in solidos)
            {
                volume += s.MassProperties.Volume;
                var ext = s.GeometricExtents;
                zmin = double.IsNaN(zmin) ? ext.MinPoint.Z : Math.Min(zmin, ext.MinPoint.Z);
                zmax = double.IsNaN(zmax) ? ext.MaxPoint.Z : Math.Max(zmax, ext.MaxPoint.Z);
            }

            var esperado = secao.WallArea * (perfil?.Length ?? 0);
            var zminEsperado = perfil is null ? double.NaN : perfil.Points.Min(p => p.Bottom) - secao.Thickness;
            var zmaxEsperado = perfil is null ? double.NaN : perfil.Points.Max(p => p.Bottom) + secao.UsefulHeight;

            editor.WriteMessage(string.Format(Inv,
                "\nDRN_CANALETA nome={0} solidos={1} malhas={2} volume={3:0.####} esperado={4:0.####} zmin={5:0.###} zmin_esperado={6:0.###} zmax={7:0.###} zmax_esperado={8:0.###} alertas={9} baixos={10} invertidos={11} fim\n",
                linha.Identidade.Name.Replace(' ', '_'), solidos.Count, malhas, volume, esperado, zmin, zminEsperado, zmax, zmaxEsperado,
                alertas, perfil?.LowSegments.Count() ?? 0, perfil?.InvertedSegments.Count() ?? 0));
        }

        editor.WriteMessage(string.Format(Inv, "\nDRN_CANALETAS total={0} orfaos_3d={1} fim\n", canaletas, orfaos));
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
