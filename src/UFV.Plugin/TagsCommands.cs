using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.TagsCommands))]

namespace UFV.Plugin;

/// <summary>
/// O menu Tags (passo 8.14): numeração visível de fileiras, mesas, módulos e
/// strings, cada uma com inserir e apagar. A ordem é a dos letreiros das
/// mesas (o Numerar arruma); a conta é do <see cref="Tags"/>, no Core.
/// Todo texto no plano da mesa (regra 5), no estilo do projeto (8.13).
/// </summary>
public static class TagsCommands
{
    private const string ChaveDoTamanho = "MODULOS_POR_STRING";

    /// <summary>Alturas de reserva, quando o projeto não tem estilo de texto.</summary>
    private static double Altura(TagKind tipo) => tipo switch
    {
        TagKind.Row => 1.2,
        TagKind.Table => 0.6,
        TagKind.Module => 0.25,
        _ => 0.5,
    };

    [CommandMethod(PluginInfo.ComandoTagFileirasInserir)] public static void FileirasInserir() => Rodar(TagKind.Row, apagar: false);
    [CommandMethod(PluginInfo.ComandoTagFileirasApagar)] public static void FileirasApagar() => Rodar(TagKind.Row, apagar: true);
    [CommandMethod(PluginInfo.ComandoTagMesasInserir)] public static void MesasInserir() => Rodar(TagKind.Table, apagar: false);
    [CommandMethod(PluginInfo.ComandoTagMesasApagar)] public static void MesasApagar() => Rodar(TagKind.Table, apagar: true);
    [CommandMethod(PluginInfo.ComandoTagModulosInserir)] public static void ModulosInserir() => Rodar(TagKind.Module, apagar: false);
    [CommandMethod(PluginInfo.ComandoTagModulosApagar)] public static void ModulosApagar() => Rodar(TagKind.Module, apagar: true);
    [CommandMethod(PluginInfo.ComandoTagStringsInserir)] public static void StringsInserir() => Rodar(TagKind.String, apagar: false);
    [CommandMethod(PluginInfo.ComandoTagStringsApagar)] public static void StringsApagar() => Rodar(TagKind.String, apagar: true);
    [CommandMethod(PluginInfo.ComandoTagStringsAutomatico)] public static void StringsAuto() => Rodar(TagKind.String, apagar: false);

    private static void Rodar(TagKind tipo, bool apagar)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (apagar)
            {
                editor.WriteMessage($"\nTAGS {Tags.Name(tipo)}: {Apagar(documento.Database, tipo)} tag(s) apagada(s).\n");
                return;
            }

            int? tamanho = null;

            if (tipo == TagKind.String)
            {
                tamanho = PerguntarTamanho(editor, documento.Database);
                if (tamanho is null) return;
            }

            var (criadas, mesas, incompletas) = Inserir(documento.Database, tipo, tamanho ?? 1);

            editor.WriteMessage(mesas == 0
                ? $"\nTAGS {Tags.Name(tipo)}: o desenho não tem mesa gerada pelo plugin.\n"
                : $"\nTAGS {Tags.Name(tipo)}: {criadas} tag(s) em {mesas} mesa(s), na camada {Tags.LayerName(tipo)}.\n");

            if (incompletas > 0)
                editor.WriteMessage($"  {incompletas} string(s) incompleta(s), com asterisco: a mesa não fecha um número inteiro de strings de {tamanho} módulos.\n");

            editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha nas tags de {tipo}.", erro);
            editor.WriteMessage($"\nNão consegui fazer as tags de {Tags.Name(tipo)}: {erro.Message}\n");
        }
    }

    private static int? PerguntarTamanho(Editor editor, Database database)
    {
        var gravado = 0;

        using (var dados = PluginDictionary.Load(database, ChaveDoTamanho))
        {
            if (dados?.AsArray() is { Length: > 0 } valores && valores[0].Value is string texto)
                int.TryParse(texto, out gravado);
        }

        var opcoes = new PromptIntegerOptions("\nMódulos por string")
        {
            LowerLimit = 1,
            UpperLimit = 200,
            AllowNegative = false,
            AllowZero = false,
        };

        if (gravado > 0)
        {
            opcoes.DefaultValue = gravado;
            opcoes.UseDefaultValue = true;
        }

        var resposta = editor.GetInteger(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        PluginDictionary.Save(database, ChaveDoTamanho, new ResultBuffer(new TypedValue((int)DxfCode.Text, resposta.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return resposta.Value;
    }

    // ------------------------------------------------------------- desenho

    private sealed record Mesa(Guid Id, string Letreiro, IReadOnlyList<Point3> Cantos, IReadOnlyList<(int Column, int Row)> Modulos)
    {
        internal int Colunas => Modulos.Count == 0 ? 1 : Modulos.Max(m => m.Column) + 1;

        internal int Fileiras => Modulos.Count == 0 ? 1 : Modulos.Max(m => m.Row) + 1;

        /// <summary>O ponto do plano da mesa em (u, v) de 0 a 1: u ao longo da fileira, v da borda baixa à alta.</summary>
        internal Point3 Em(double u, double v)
        {
            Point3 Lerp(Point3 a, Point3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
            return Lerp(Lerp(Cantos[0], Cantos[1], u), Lerp(Cantos[3], Cantos[2], u), v);
        }

        internal Point3 CentroDoModulo(int coluna, int fileira) => Em((coluna + 0.5) / Colunas, (fileira + 0.5) / Fileiras);

        internal double Rumo => LayoutDrawer.RumoLegivel(Cantos[1].X - Cantos[0].X, Cantos[1].Y - Cantos[0].Y);
    }

    private static (int Criadas, int Mesas, int Incompletas) Inserir(Database database, TagKind tipo, int modulosPorString)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var mesas = Ler(transacao, database, tipo, apagarAsDoTipo: true);
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, Tags.LayerName(tipo), new RgbColor(255, 200, 0));
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
        var porId = mesas.ToDictionary(m => m.Id);
        var paraOCore = mesas.Select(m => new TaggedTable(m.Letreiro, m.Id, m.Modulos)).ToList();

        var criadas = 0;
        var incompletas = 0;

        void Escrever(Guid mesa, string texto, Point3 onde, double rumo)
        {
            var mtexto = new MText
            {
                Location = new Point3d(onde.X, onde.Y, onde.Z),
                TextHeight = Altura(tipo),
                Layer = camada,
                Attachment = AttachmentPoint.MiddleCenter,
                Rotation = rumo,
                Contents = texto,
            };

            espaco.AppendEntity(mtexto);
            transacao.AddNewlyCreatedDBObject(mtexto, true);
            estilo(mtexto);
            LayoutXData.SaveTag(transacao, mtexto, new TagIdentity(Guid.NewGuid(), mesa, tipo, texto));
            criadas++;
        }

        switch (tipo)
        {
            case TagKind.Row:
                foreach (var (letreiro, primeira) in Tags.Rows(paraOCore))
                {
                    var mesa = porId[primeira.Id];

                    // Antes da primeira mesa, um pouco para fora, no plano dela.
                    var comprimento = Distancia(mesa.Cantos[0], mesa.Cantos[1]);
                    var recuo = comprimento < 1e-6 ? 0 : -1.5 / comprimento;
                    Escrever(mesa.Id, letreiro, mesa.Em(recuo, 0.5), mesa.Rumo);
                }

                break;

            case TagKind.Table:
                foreach (var mesa in mesas) Escrever(mesa.Id, mesa.Letreiro, mesa.Em(0.5, 0.5), mesa.Rumo);
                break;

            case TagKind.Module:
                foreach (var mesa in mesas)
                {
                    var ordem = Tags.Serpentine(mesa.Modulos);
                    for (var i = 0; i < ordem.Count; i++)
                        Escrever(mesa.Id, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), mesa.CentroDoModulo(ordem[i].Column, ordem[i].Row), mesa.Rumo);
                }

                break;

            default:
                foreach (var s in Tags.Strings(paraOCore, modulosPorString))
                {
                    var mesa = porId[s.Modules[0].Table];
                    var centros = s.Modules.Select(m => mesa.CentroDoModulo(m.Column, m.Row)).ToList();
                    var meio = new Point3(centros.Average(p => p.X), centros.Average(p => p.Y), centros.Average(p => p.Z));

                    Escrever(mesa.Id, s.Label, meio, mesa.Rumo);
                    if (!s.Complete) incompletas++;
                }

                break;
        }

        transacao.Commit();
        return (criadas, mesas.Count, incompletas);
    }

    private static int Apagar(Database database, TagKind tipo)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var antes = Contar(transacao, database, tipo);
        Ler(transacao, database, tipo, apagarAsDoTipo: true);

        transacao.Commit();
        return antes;
    }

    private static int Contar(Transaction transacao, Database database, TagKind tipo)
    {
        var n = 0;

        foreach (var (_, partes) in LayoutScan.Tables(transacao, database))
        {
            foreach (var id in partes.Notes)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadTag(e) is { } tag && tag.Kind == tipo) n++;
            }
        }

        return n;
    }

    /// <summary>
    /// Uma varredura: as mesas com contorno de quatro cantos, letreiro e
    /// módulos; de quebra apaga as tags deste tipo que já existiam (inserir
    /// de novo não empilha).
    /// </summary>
    private static List<Mesa> Ler(Transaction transacao, Database database, TagKind tipo, bool apagarAsDoTipo)
    {
        var mesas = new List<Mesa>();

        foreach (var (guid, partes) in LayoutScan.Tables(transacao, database))
        {
            if (apagarAsDoTipo)
            {
                foreach (var id in partes.Notes)
                {
                    if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadTag(e) is { } tag && tag.Kind == tipo)
                    {
                        e.UpgradeOpen();
                        e.Erase();
                    }
                }
            }

            if (partes.Identity is not { } identidade || partes.Contour is not { } contorno) continue;

            var cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao);
            if (cantos.Count != 4) continue;

            var modulos = new List<(int, int)>();

            foreach (var id in partes.Modules)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadModule(e) is { } m)
                    modulos.Add((m.Column, m.Row));
            }

            mesas.Add(new Mesa(guid, identidade.Label, cantos, modulos));
        }

        return mesas;
    }

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
