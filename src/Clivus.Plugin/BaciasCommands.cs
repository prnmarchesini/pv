using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.BaciasCommands))]

namespace Clivus.Plugin;

/// <summary>Memória › Bacias (30): calcular, apagar e o ajuste manual.</summary>
public static class BaciasCommands
{
    private const string Areas = "Areas";
    private const string Tudo = "Tudo";
    private const string Polilinha = "Polilinha";
    private const string Soltar = "Soltar";

    /// <summary>CLIVUS_DRN_BACIAS_CALCULAR: [Areas/Tudo/Polilinha].</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemBaciasCalcular)]
    public static void Calcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var configuracao = DrenagemStore.Ler(documento.Database);

            var opcoes = new PromptKeywordOptions(Tr.T("\nRegião das bacias"));
            opcoes.Keywords.Add(Areas);
            opcoes.Keywords.Add(Tudo);
            opcoes.Keywords.Add(Polilinha);
            opcoes.Keywords.Default = configuracao.Basins.Region is Tudo or Polilinha ? configuracao.Basins.Region : Areas;
            opcoes.AllowNone = true;

            var resposta = editor.GetKeywords(opcoes);
            if (resposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;
            var escolha = string.IsNullOrEmpty(resposta.StringResult) ? opcoes.Keywords.Default : resposta.StringResult;

            var handle = string.Empty;
            if (escolha == Polilinha)
            {
                var pedido = new PromptEntityOptions(Tr.T("\nEscolha a polilinha fechada da região: "));
                pedido.SetRejectMessage(Tr.T("\nSó polilinha."));
                pedido.AddAllowedClass(typeof(Polyline), true);
                pedido.AddAllowedClass(typeof(Polyline3d), true);
                pedido.AddAllowedClass(typeof(Polyline2d), true);

                var entidade = editor.GetEntity(pedido);
                if (entidade.Status != PromptStatus.OK) return;
                handle = entidade.ObjectId.Handle.ToString();
            }

            DrenagemStore.Mudar(documento.Database, d => d with { Basins = d.Basins with { Region = escolha, RegionHandle = handle } });

            editor.WriteMessage($"\n{CalcularComARegiaoGravada(documento, terreno.Mesh)}\n");
            editor.Regen();
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nBACIAS {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no cálculo das bacias.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui calcular as bacias: {0}\n", erro.Message));
        }
    }

    /// <summary>Calcula com a região gravada (a da janela e a do Calcular da memória).</summary>
    internal static string CalcularComARegiaoGravada(Document documento, Tin terreno, Func<double, bool>? andamento = null)
    {
        var configuracao = DrenagemStore.Ler(documento.Database);
        return BaciasDeDrenagem.Calcular(documento.Database, terreno, Regiao(documento.Database, configuracao.Basins), andamento);
    }

    /// <summary>Os polígonos da região gravada: as áreas, nenhum (o terreno todo) ou a polilinha.</summary>
    internal static IReadOnlyList<IReadOnlyList<Point3>> Regiao(Database database, BasinSettings bacias)
    {
        switch (bacias.Region)
        {
            case Tudo:
                return [];

            case Polilinha:
                if (!long.TryParse(bacias.RegionHandle, System.Globalization.NumberStyles.HexNumber, null, out var h)
                    || !database.TryGetObjectId(new Handle(h), out var id) || id.IsErased)
                    throw new ArgumentException(Tr.T("A polilinha da região das bacias não está mais no desenho. Escolha a região de novo."));

                var contorno = LinhaCommands.Caminho(database, id);
                if (contorno.Count < 3) throw new ArgumentException(Tr.T("A polilinha da região precisa de pelo menos três vértices."));
                return [contorno];

            default:
                return FlechasDeDrenagem.Regioes(database, OndeFlechas.Areas);
        }
    }

    /// <summary>CLIVUS_DRN_BACIAS_APAGAR.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemBaciasApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage(Tr.F("\nBACIAS {0} entidade(s) apagada(s); os ajustes manuais ficam.\n", BaciasDeDrenagem.Apagar(documento.Database)));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar as bacias.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui apagar as bacias: {0}\n", erro.Message));
        }
    }

    private static bool EAjusteManual(Database database, ObjectId id)
    {
        using var t = database.TransactionManager.StartOpenCloseTransaction();
        return t.GetObject(id, OpenMode.ForRead) is Entity e && PluginXData.Load(e, BaciasDeDrenagem.TipoManual, 1, 1) is not null;
    }

    /// <summary>
    /// CLIVUS_DRN_BACIA_MANUAL: a polilinha fechada (desenhada pelo Renan) e a
    /// canaleta para onde a área dela vai; [Soltar] tira a marca (a polilinha
    /// fica). Recalcule as bacias depois.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoDrenagemBaciaManual)]
    public static void Manual()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var pedido = new PromptEntityOptions(Tr.T("\nEscolha a polilinha fechada da área"));
            pedido.Keywords.Add(Soltar);
            pedido.SetRejectMessage(Tr.T("\nSó polilinha."));
            pedido.AddAllowedClass(typeof(Polyline), true);
            pedido.AddAllowedClass(typeof(Polyline2d), true);
            pedido.AddAllowedClass(typeof(Polyline3d), true);

            var area = editor.GetEntity(pedido);

            if (area.Status == PromptStatus.Keyword)
            {
                var soltar = editor.GetEntity(new PromptEntityOptions(Tr.T("\nEscolha o ajuste manual a soltar: ")));
                if (soltar.Status != PromptStatus.OK) return;

                using var t = documento.Database.TransactionManager.StartTransaction();
                var e = (Entity)t.GetObject(soltar.ObjectId, OpenMode.ForWrite);
                if (PluginXData.Load(e, BaciasDeDrenagem.TipoManual, 1, 1) is null)
                {
                    editor.WriteMessage(Tr.T("\nBACIAS Isso não é um ajuste manual.\n"));
                    return;
                }

                // Tira só o nosso XData; a polilinha é do Renan e fica.
                e.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginXData.Aplicativo));
                t.Commit();
                editor.WriteMessage(Tr.T("\nBACIAS Ajuste manual solto. Calcule as bacias de novo.\n"));
                return;
            }

            if (area.Status != PromptStatus.OK) return;

            var fechada = false;
            using (var t = documento.Database.TransactionManager.StartOpenCloseTransaction())
            {
                fechada = t.GetObject(area.ObjectId, OpenMode.ForRead) switch
                {
                    Polyline p => p.Closed,
                    Polyline2d p2 => p2.Closed,
                    Polyline3d p3 => p3.Closed,
                    _ => false,
                };
            }

            if (LinhaCommands.EDoClivus(documento.Database, area.ObjectId) && !EAjusteManual(documento.Database, area.ObjectId))
            {
                editor.WriteMessage(Tr.T("\nBACIAS Essa polilinha é do Clivus (mesa, área, linha, bacia); desenhe uma polilinha sua.\n"));
                return;
            }

            if (!fechada)
            {
                editor.WriteMessage(Tr.T("\nBACIAS A polilinha precisa ser fechada.\n"));
                return;
            }

            var canaleta = editor.GetEntity(new PromptEntityOptions(Tr.T("\nClique na canaleta para onde a água dessa área vai: ")));
            if (canaleta.Status != PromptStatus.OK) return;

            using (var t = documento.Database.TransactionManager.StartTransaction())
            {
                if (t.GetObject(canaleta.ObjectId, OpenMode.ForRead) is not Polyline3d p3 || LinhasDeDrenagem.Identidade(p3) is not { IsChannel: true } identidade)
                {
                    editor.WriteMessage(Tr.T("\nBACIAS Isso não é uma canaleta.\n"));
                    return;
                }

                var poli = (Entity)t.GetObject(area.ObjectId, OpenMode.ForWrite);
                PluginXData.Save(t, poli, BaciasDeDrenagem.TipoManual, 1, identidade.Id.ToString("D"));
                t.Commit();

                editor.WriteMessage(Tr.F("\nBACIAS A área escolhida vai para {0}. Calcule as bacias de novo para valer.\n", identidade.Name));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no ajuste manual das bacias.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer o ajuste: {0}\n", erro.Message));
        }
    }
}
