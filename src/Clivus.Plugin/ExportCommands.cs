using System.IO;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ExportCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A exportação para o PVsyst: o passo 6.2. Desde o 42.4 (Renan, 31/10/2026:
/// "tenho que ter a liberdade de exportar uma area, varias, todas, e pode
/// selecionar as mesas que eu quero se for o caso"), a janela pergunta o
/// escopo, como a das sombras: as mesas escolhidas, uma área (com as de
/// dentro), área + seleção, várias áreas ou o desenho inteiro. O plugin
/// recolhe as faces de módulo das mesas do escopo e grava o arquivo.
///
/// O que é "face de módulo nossa" é dito pelo XData (<see cref="FaceIdentity"/>),
/// nunca pela camada: a seleção filtra 3DFACE com o nosso nome de aplicativo
/// e a identidade é lida de cada uma. Face na camada certa sem identidade é
/// ignorada e contada.
///
/// As coordenadas vão relativas a uma origem local (ver <see cref="ColladaWriter"/>),
/// dita na linha de comando e gravada no cabeçalho do arquivo.
/// </summary>
public static class ExportCommands
{
    /// <summary>CLIVUS_EXPORTAR: seleção, formato, arquivo.</summary>
    [CommandMethod(PluginInfo.ComandoExportar, CommandFlags.UsePickSet)]
    public static void Exportar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            IReadOnlyList<ModuleFace> faces;
            int ignoradas, repetidas;

            if (ClivusExtension.TemInterface())
            {
                // A seleção feita antes do botão abre a janela em Seleção.
                var antes = editor.SelectImplied();
                var escolhidas = antes.Status == PromptStatus.OK ? SelecaoCommands.MesasTocadas(documento, antes.Value.GetObjectIds()) : [];

                if (JanelaDoPvsyst.Perguntar(documento, escolhidas) is not { } escopo)
                {
                    editor.WriteMessage(Tr.T("\nEXPORTAR Cancelado.\n"));
                    return;
                }

                (faces, ignoradas, repetidas) = Recolher(documento.Database, TodasAsFaces(documento.Database), escopo.Mesas);
                editor.WriteMessage(Tr.F("\nEXPORTAR {0} mesa(s) {1}.", escopo.Mesas.Count, escopo.Nome));
            }
            else
            {
                var selecao = Selecionar(editor);
                if (selecao is null) return;

                (faces, ignoradas, repetidas) = Recolher(documento.Database, selecao);
            }

            if (ignoradas > 0)
                editor.WriteMessage(Tr.F("\n  {0} face(s) da seleção não tem identidade de módulo e foi ignorada.\n", ignoradas));

            if (!AvisarRepetidas(editor, repetidas)) return;

            if (faces.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nEXPORTAR Nenhum módulo na seleção. Selecione as faces desenhadas pelo plugin.\n"));
                return;
            }

            // O formato vem do tipo de arquivo escolhido na janela (hoje só
            // DAE; o PVC é o passo 6.5): nada de pergunta na linha de comando.
            var caminho = PerguntarArquivo(editor, documento);
            if (caminho is null) return;

            Gravar(editor, faces, caminho, linhaParaTeste: false);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar para o PVsyst.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_EXPORTAR_AUTO: todas as faces de módulo do espaço do modelo, em
    /// DAE, no caminho pedido na linha de comando. Para o nível 2.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoExportarAutomatico)]
    public static void ExportarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resposta = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo DAE: ")) { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return;

            var (faces, ignoradas, repetidas) = Recolher(documento.Database, TodasAsFaces(documento.Database));

            if (ignoradas > 0)
                editor.WriteMessage(Tr.F("\n  {0} face(s) sem identidade de módulo foi ignorada.\n", ignoradas));

            if (!AvisarRepetidas(editor, repetidas)) return;

            if (faces.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nEXPORTAR Nenhum módulo no desenho.\n"));
                return;
            }

            Gravar(editor, faces, ComExtensao(resposta.StringResult.Trim()), linhaParaTeste: true);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar automaticamente para o PVsyst.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_EXPORTAR_ESCOPO_AUTO: o escopo (TUDO, AREA, AREAS, SELECAO,
    /// AREA+SELECAO), as áreas pelo nome e as mesas pelo letreiro (separados
    /// por ";", "-" para nenhum) e o arquivo DAE. Para o nível 2 (42.4).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoExportarEscopoAutomatico)]
    public static void ExportarEscopoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            string? Texto(string pergunta)
            {
                var r = editor.GetString(new PromptStringOptions($"\n{pergunta}: ") { AllowSpaces = true });
                return r.Status == PromptStatus.OK ? r.StringResult : null;
            }

            var tipo = Texto(Tr.T("Escopo (TUDO, AREA, AREAS, SELECAO, AREA+SELECAO)"));
            var areas = Texto(Tr.T("Áreas, pelo nome, separadas por ; (- para nenhuma)"));
            var letreiros = Texto(Tr.T("Mesas, pelo letreiro, separadas por ; (- para nenhuma)"));
            var arquivo = Texto(Tr.T("Arquivo DAE"));
            if (tipo is null || areas is null || letreiros is null || string.IsNullOrWhiteSpace(arquivo)) return;

            if (SombrasCommands.EscopoPelosNomes(documento.Database, tipo, areas, letreiros, out var porque) is not { } escopo)
            {
                editor.WriteMessage(Tr.F("\nEXPORTAR {0}\n", porque));
                return;
            }

            var (mesas, nome) = MesasDoEscopo(documento.Database, escopo);
            editor.WriteMessage($"\nEXPORTAR_MESAS {mesas.Count}");

            var (faces, _, repetidas) = Recolher(documento.Database, TodasAsFaces(documento.Database), mesas);
            if (!AvisarRepetidas(editor, repetidas)) return;

            if (faces.Count == 0)
            {
                editor.WriteMessage(Tr.F("\nEXPORTAR Nenhuma mesa {0}: nada a exportar.\n", nome));
                return;
            }

            Gravar(editor, faces, ComExtensao(arquivo.Trim().Trim('"')), linhaParaTeste: true);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar o escopo para o PVsyst (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar: {0}\n", erro.Message));
        }
    }

    /// <summary>As mesas do escopo (a mesma conta do escopo das sombras) e o nome dele.</summary>
    internal static (IReadOnlySet<Guid> Mesas, string Nome) MesasDoEscopo(Database database, ShadowScope escopo) =>
        SombrasCommands.MesasDoEscopo(database, escopo);

    /// <summary>
    /// A seleção: 3DFACE com o nosso XData. Aceita a seleção prévia (o
    /// usuário pode selecionar antes de clicar o botão) e a janela.
    /// </summary>
    private static ObjectId[]? Selecionar(Editor editor)
    {
        var filtro = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "3DFACE"),
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginXData.Aplicativo),
        ]);

        var opcoes = new PromptSelectionOptions
        {
            MessageForAdding = Tr.T("\nSelecione os módulos a exportar (janela sobre a área): "),
            MessageForRemoval = Tr.T("\nRetire da seleção: "),
        };

        var resultado = editor.GetSelection(opcoes, filtro);

        if (resultado.Status != PromptStatus.OK)
        {
            editor.WriteMessage(Tr.T("\nEXPORTAR Nada selecionado.\n"));
            return null;
        }

        return resultado.Value.GetObjectIds();
    }

    private static ObjectId[] TodasAsFaces(Database database)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var ids = new List<ObjectId>();

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass.DxfName == "3DFACE") ids.Add(id);
        }

        transacao.Commit();

        return [.. ids];
    }

    /// <summary>
    /// Lê cada face: identidade do XData, os quatro cantos da geometria. O
    /// GUID da face é o GUID do arquivo (regra sagrada 3 atravessando para o
    /// PVsyst). Face sem identidade é contada como ignorada; GUID visto
    /// duas vezes (mesa copiada e colada) é contado como repetido, e a
    /// exportação não sai com ele. Com <paramref name="mesas"/> (42.4), só as
    /// faces dessas mesas entram; as outras nem contam.
    /// </summary>
    private static (IReadOnlyList<ModuleFace> Faces, int Ignoradas, int Repetidas) Recolher(Database database, ObjectId[] ids, IReadOnlySet<Guid>? mesas = null)
    {
        var faces = new List<ModuleFace>();
        var vistos = new HashSet<Guid>();
        var ignoradas = 0;
        var repetidas = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        foreach (var id in ids)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Face face) continue;

            var identidade = LayoutXData.LoadFace(face);

            if (identidade is null)
            {
                // Fora de uma seleção à mão, face sem identidade não é nossa: nem conta.
                if (mesas is null) ignoradas++;
                continue;
            }

            if (mesas is not null && !mesas.Contains(identidade.Table)) continue;

            if (!vistos.Add(identidade.Id))
            {
                repetidas++;
                continue;
            }

            var cantos = new List<Point3>(4);

            for (short i = 0; i < 4; i++)
            {
                var p = face.GetVertexAt(i);
                cantos.Add(new Point3(p.X, p.Y, p.Z));
            }

            faces.Add(new ModuleFace(identidade.Id, cantos));
        }

        transacao.Commit();

        return (faces, ignoradas, repetidas);
    }

    /// <summary>Diz o que fazer com faces de GUID repetido; false para não exportar.</summary>
    private static bool AvisarRepetidas(Editor editor, int repetidas)
    {
        if (repetidas == 0) return true;

        editor.WriteMessage(
            Tr.F("\nEXPORTAR {0} face(s) com identidade repetida (mesa copiada e colada?). A exportação não sai com GUID duplicado: apague as cópias ou reprocesse a fileira.\n", repetidas));

        return false;
    }

    private static string ComExtensao(string caminho) =>
        caminho.EndsWith(".dae", StringComparison.OrdinalIgnoreCase) ? caminho : caminho + ".dae";

    /// <summary>
    /// O arquivo, pela janela de salvar do Windows. O nome sugerido é o do
    /// desenho, e a pasta é a dele quando o desenho já foi salvo.
    /// </summary>
    private static string? PerguntarArquivo(Editor editor, Document documento)
    {
        var nome = Path.GetFileNameWithoutExtension(documento.Name);
        if (string.IsNullOrWhiteSpace(nome)) nome = "usina";

        // Sempre a janela do Windows (regra de 02/10/2026: "toda interação de
        // salvar e abrir é via janela do Windows").
        var caminho = DialogoDeArquivo.Salvar(
            Tr.T("Exportar para o PVsyst"), Tr.T("Cena 3D Collada (*.dae)") + "|*.dae", nome + ".dae",
            documento.IsNamedDrawing ? Path.GetDirectoryName(documento.Name) : null);

        if (caminho is null)
        {
            editor.WriteMessage(Tr.T("\nEXPORTAR Cancelado.\n"));
            return null;
        }

        return ComExtensao(caminho);
    }

    /// <summary>
    /// Grava e relata. A linha "ORIGEM E= N= Z=" em formato invariante existe
    /// para o teste de nível 2 ler; o usuário vê a mesma origem em pt-BR.
    /// </summary>
    private static void Gravar(Editor editor, IReadOnlyList<ModuleFace> faces, string caminho, bool linhaParaTeste)
    {
        caminho = Path.GetFullPath(caminho);

        var origem = ColladaWriter.LocalOrigin(faces);
        var documento = ColladaWriter.Write(faces, LayoutLayers.Face, DateTime.UtcNow, Environment.UserName, origem);

        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        // XmlWriter com UTF-8 sem BOM: ToString descartaria a declaração XML,
        // e Save poria um BOM que parte dos leitores de cena rejeita.
        var ajustes = new System.Xml.XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };

        using (var escritor = System.Xml.XmlWriter.Create(caminho, ajustes))
            documento.Save(escritor);

        var tamanho = new FileInfo(caminho).Length;

        editor.WriteMessage(
            Tr.F("\nEXPORTAR {0} face(s) de módulo gravada(s) em {1} ({2:0.#} KB)\n  material: {3} (é o que se escolhe no PVsyst ao importar)\n  origem local: E={4:0.###} N={5:0.###} Z={6:0.###} m (as coordenadas do arquivo são relativas a ela)\n",
                faces.Count, caminho, tamanho / 1024.0, LayoutLayers.Face, origem.X, origem.Y, origem.Z));

        if (linhaParaTeste)
            editor.WriteMessage($"  ORIGEM E={origem.X.ToString("R", CultureInfo.InvariantCulture)} N={origem.Y.ToString("R", CultureInfo.InvariantCulture)} Z={origem.Z.ToString("R", CultureInfo.InvariantCulture)}\n");
    }
}
