using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.ArvoreCommands))]

namespace UFV.Plugin;

/// <summary>
/// Árvore como objeto de sombra (9.4). Renan, 03/10/2026: "tamanho do tronco
/// e sua largura, e a copa da árvore, altura e largura, considerando que ela
/// é um cilindro ... aí eu pego, e saio clicando onde eu quero pôr, eu posso
/// arrastar manualmente e ela automaticamente vai se ajustar para altura do
/// terreno". Cada árvore é um bloco (tronco e copa, sólidos) com o pé na cota
/// do terreno do clique — nunca a cota do clique (regra 5) — e as medidas no
/// XData. Arrastada, o <see cref="ArvoreVigia"/> a põe de volta no chão.
/// </summary>
public static class ArvoreCommands
{
    private const string ChaveDaUltima = "ARVORE_ULTIMA";
    private const string PrefixoDoBloco = PluginInfo.PrefixoDeDados + "_ARVORE_";

    [CommandMethod(PluginInfo.ComandoArvore)]
    public static void Arvore()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var ultima = Ultima(documento.Database);
            TreeSpec? medidas;

            if (UfvExtension.TemInterface())
            {
                var janela = new JanelaDeArvore(ultima);
                if (AcadApp.ShowModalWindow(janela) != true || janela.Medidas is null) return;
                medidas = janela.Medidas;
            }
            else
            {
                medidas = PerguntarMedidas(editor, ultima);
                if (medidas is null) return;
            }

            GravarUltima(documento.Database, medidas);
            Clicar(editor, documento, terreno, medidas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao inserir árvores.", erro);
            editor.WriteMessage($"\nNão consegui inserir as árvores: {erro.Message}\n");
        }
    }

    /// <summary>UFV_ARVORE_AUTO: as quatro medidas e os pontos pela linha de comando (Enter termina). Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoArvoreAutomatico)]
    public static void ArvoreAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var medidas = PerguntarMedidas(editor, TreeSpec.Default);
            if (medidas is null) return;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Clicar(editor, documento, terreno, medidas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao inserir árvores (automático).", erro);
            editor.WriteMessage($"\nNão consegui inserir as árvores: {erro.Message}\n");
        }
    }

    private static TreeSpec? PerguntarMedidas(Editor editor, TreeSpec padrao)
    {
        double? Medida(string texto, double valor)
        {
            var r = editor.GetDouble(new PromptDoubleOptions($"\n{texto} (m) <{valor.ToString("0.##", CultureInfo.InvariantCulture)}>: ")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = valor,
                UseDefaultValue = true,
            });
            return r.Status == PromptStatus.OK ? r.Value : null;
        }

        if (Medida("Altura do tronco", padrao.TrunkHeight) is not { } ht) return null;
        if (Medida("Largura do tronco", padrao.TrunkWidth) is not { } lt) return null;
        if (Medida("Altura da copa", padrao.CrownHeight) is not { } hc) return null;
        if (Medida("Largura da copa", padrao.CrownWidth) is not { } lc) return null;

        var medidas = new TreeSpec(ht, lt, hc, lc);

        if (medidas.WhyInvalid() is { } porque)
        {
            editor.WriteMessage($"\nÁRVORE {porque}.\n");
            return null;
        }

        return medidas;
    }

    /// <summary>Clique a clique, uma árvore em cada ponto, com o pé no terreno; Enter termina.</summary>
    private static void Clicar(Editor editor, Document documento, ProcessedTerrain terreno, TreeSpec medidas)
    {
        editor.WriteMessage($"\nÁRVORE {medidas.Describe()}. Clique onde pôr cada árvore; Enter termina.\n");

        var postas = 0;

        while (true)
        {
            var ponto = editor.GetPoint(new PromptPointOptions("\nOnde pôr a árvore (Enter termina): ") { AllowNone = true });
            if (ponto.Status != PromptStatus.OK) break;

            // O clique é em coordenadas do usuário; a cota vem do terreno.
            var mundo = ponto.Value.TransformBy(editor.CurrentUserCoordinateSystem);

            if (!terreno.Mesh.TryGetZ(mundo.X, mundo.Y, out var chao))
            {
                editor.WriteMessage("\nÁRVORE Esse ponto está fora do terreno; a árvore não foi posta.\n");
                continue;
            }

            Inserir(documento.Database, medidas, new Point3d(mundo.X, mundo.Y, chao));
            postas++;
        }

        editor.WriteMessage($"\nÁRVORE {postas} árvore(s) posta(s) no terreno. Arrastadas, elas voltam ao chão do lugar novo.\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>Insere uma árvore com o pé em <paramref name="pe"/>.</summary>
    internal static ObjectId Inserir(Database database, TreeSpec medidas, Point3d pe)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var bloco = GarantirBloco(transacao, database, medidas);
        var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Arvore, new RgbColor(40, 140, 60));
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);

        var arvore = new BlockReference(pe, bloco) { Layer = camada };
        var id = espaco.AppendEntity(arvore);
        transacao.AddNewlyCreatedDBObject(arvore, true);
        LayoutXData.SaveTree(transacao, arvore, new TreeIdentity(Guid.NewGuid(), medidas));

        transacao.Commit();
        return id;
    }

    /// <summary>O bloco destas medidas: tronco marrom e copa verde, o pé na origem.</summary>
    private static ObjectId GarantirBloco(Transaction transacao, Database database, TreeSpec m)
    {
        // "R": medidas que diferem na quarta casa não podem dividir o mesmo bloco.
        static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        var nome = $"{PrefixoDoBloco}{N(m.TrunkHeight)}x{N(m.TrunkWidth)}_{N(m.CrownHeight)}x{N(m.CrownWidth)}";
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        if (tabela.Has(nome)) return tabela[nome];

        tabela.UpgradeOpen();
        var definicao = new BlockTableRecord { Name = nome, Origin = Point3d.Origin };
        var id = tabela.Add(definicao);
        transacao.AddNewlyCreatedDBObject(definicao, true);

        void Cilindro(double base_, double altura, double largura, Autodesk.AutoCAD.Colors.Color cor)
        {
            var solido = new Solid3d();
            solido.CreateFrustum(altura, largura / 2, largura / 2, largura / 2);
            solido.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, base_ + altura / 2)));
            solido.Layer = "0";
            solido.Color = cor;
            definicao.AppendEntity(solido);
            transacao.AddNewlyCreatedDBObject(solido, true);
        }

        Cilindro(0, m.TrunkHeight, m.TrunkWidth, Autodesk.AutoCAD.Colors.Color.FromRgb(120, 80, 40));
        Cilindro(m.TrunkHeight, m.CrownHeight, m.CrownWidth, Autodesk.AutoCAD.Colors.Color.FromRgb(40, 140, 60));

        return id;
    }

    /// <summary>As árvores do desenho: id, posição do pé e medidas.</summary>
    internal static List<(ObjectId Id, Point3d Pe, TreeIdentity Arvore)> Ler(Transaction transacao, Database database)
    {
        var lidas = new List<(ObjectId, Point3d, TreeIdentity)>();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(BlockReference));

        foreach (ObjectId id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classe)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
            if (LayoutXData.LoadTree(br) is { } arvore) lidas.Add((id, br.Position, arvore));
        }

        return lidas;
    }

    private static TreeSpec Ultima(Database database)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, ChaveDaUltima);
            var v = dados?.AsArray();

            if (v is { Length: 4 } && v.All(t => t.Value is double))
            {
                var m = new TreeSpec((double)v[0].Value, (double)v[1].Value, (double)v[2].Value, (double)v[3].Value);
                if (m.WhyInvalid() is null) return m;
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler as medidas da última árvore.", erro);
        }

        return TreeSpec.Default;
    }

    private static void GravarUltima(Database database, TreeSpec m) =>
        PluginDictionary.Save(database, ChaveDaUltima, new ResultBuffer(
            new TypedValue((int)DxfCode.Real, m.TrunkHeight),
            new TypedValue((int)DxfCode.Real, m.TrunkWidth),
            new TypedValue((int)DxfCode.Real, m.CrownHeight),
            new TypedValue((int)DxfCode.Real, m.CrownWidth)));
}
