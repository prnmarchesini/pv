using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// Gerar as strings (elétrica, 11.6 a 11.8): lê as mesas selecionadas e as
/// strings que já estão nelas, pede ao Core o plano (que tipo cai em que
/// grupo, que mesa não casou, que grupo tem string ligada a inversor) e
/// desenha (11.7): cada string é uma Polyline3d na camada CLIVUS_STRING, com
/// o vínculo no XData (o contrato), pelo traçado do Geo, que acompanha o plano
/// de cada módulo; o + e o − são textos nas pontas, no plano do módulo. As
/// strings livres que já estavam no grupo são apagadas antes (com os sinais).
/// Quem decide é o Core; aqui só se lê e se escreve.
/// </summary>
internal static class GeracaoDeStrings
{
    /// <summary>O relatório de uma geração: linhas para a tela e os números para o nível 2.</summary>
    internal sealed record Relatorio(IReadOnlyList<string> Linhas, int Strings, int Grupos, int SemTipo, int Pulados, int Substituidas, IReadOnlyList<Guid> MesasSemTipo);

    /// <summary>A altura do texto do + e do −, em metro.</summary>
    private const double AlturaDoSinal = 0.35;

    /// <summary>
    /// Gera nas mesas dadas com os tipos dados (vazio = todos os que têm
    /// traçado). Roda dentro de um comando (documento travado).
    /// </summary>
    internal static Relatorio Gerar(Document documento, IReadOnlySet<Guid> guids, IReadOnlyCollection<Guid> tiposEscolhidos)
    {
        var database = documento.Database;
        var linhas = new List<string>();

        var biblioteca = StringTypeStore.Ler(database);
        if (biblioteca.Problem is { } problema) linhas.Add(Tr.F("ATENÇÃO: {0}.", problema));

        var tipos = biblioteca.Items.Where(t => tiposEscolhidos.Count == 0 ? t.CanGenerate : tiposEscolhidos.Contains(t.Id)).ToList();

        GenerationPlan plano;
        var desenhadas = 0;
        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var problemas = new List<string>();
            var mesas = MesasDaString.Ler(transacao, database, guids, problemas);
            linhas.AddRange(problemas.Select(p => Tr.F("ATENÇÃO: {0}.", p)));

            plano = StringGeneration.Plan(tipos, mesas, Existentes(transacao, database, mesas));

            Apagar(transacao, database, plano.Groups.SelectMany(g => g.Replaces).ToHashSet());

            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.String, new RgbColor(230, 60, 60));

            foreach (var g in plano.Groups)
            {
                var faces = g.Tables.SelectMany(o => o.Table.Modules).Select(m => m.Face).Where(f => f.Count == 4).ToList();
                var feitas = 0;

                foreach (var s in g.Strings)
                {
                    if (s.Modules.Any(m => m.Face.Count != 4))
                    {
                        linhas.Add(Tr.F("Aviso: {0}.", Tr.F("{0}: módulo sem a face de cima; a string não foi desenhada", g.Labels)));
                        continue;
                    }

                    Desenhar(transacao, espaco, camada, s, StringPath.Build(s.Modules.Select(m => m.Face).ToList(), faces));
                    feitas++;
                }

                desenhadas += feitas;
                linhas.Add(Tr.F("{0} → {1}: {2} string(s).", g.Type.Name, g.Labels, feitas));
            }

            transacao.Commit();
        }

        linhas.AddRange(plano.Notes.Select(n => Tr.F("Aviso: {0}.", n)));
        linhas.AddRange(plano.Skipped.Select(n => Tr.F("Aviso: {0}.", n)));
        linhas.AddRange(plano.Unmatched.Select(n => Tr.F("Aviso: {0}.", n)));

        linhas.Insert(0, Tr.F("{0} string(s) em {1} grupo(s) de mesas; {2} mesa(s) sem tipo; {3} grupo(s) não regerado(s).",
            desenhadas, plano.Groups.Count, plano.Unmatched.Count, plano.Skipped.Count));
        if (plano.ReplacedCount > 0) linhas.Insert(1, Tr.F("{0} string(s) livre(s) que já estavam nessas mesas foram substituídas.", plano.ReplacedCount));

        return new Relatorio(linhas, desenhadas, plano.Groups.Count, plano.Unmatched.Count, plano.Skipped.Count, plano.ReplacedCount, plano.UnmatchedTables);
    }

    /// <summary>
    /// A string no desenho: a polilinha 3D pelo traçado (que já tem a cota
    /// do plano de cada módulo) com o vínculo no XData, e os sinais nas
    /// pontas, na cota do traçado ali (o centro do módulo do + e do −).
    /// </summary>
    private static void Desenhar(Transaction transacao, BlockTableRecord espaco, string camada, PlannedString s, IReadOnlyList<Point3> traco)
    {
        var linha = new Polyline3d { Layer = camada };
        espaco.AppendEntity(linha);
        transacao.AddNewlyCreatedDBObject(linha, true);

        foreach (var p in traco)
        {
            var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            linha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
        }

        ElectricalStore.SaveString(transacao, linha, s.String);

        Sinal(transacao, espaco, camada, s.String.Id, traco[0], s.Modules[0].Face, positivo: true);
        Sinal(transacao, espaco, camada, s.String.Id, traco[^1], s.Modules[^1].Face, positivo: false);
    }

    /// <summary>
    /// O + ou o − deitado no plano do módulo (a normal do texto é a da face),
    /// na cota do traçado: o texto inteiro fica acima do módulo, sem um
    /// pedaço enterrado no lado alto da mesa inclinada.
    /// </summary>
    private static void Sinal(Transaction transacao, BlockTableRecord espaco, string camada, Guid stringId, Point3 ponto, IReadOnlyList<Point3> face, bool positivo)
    {
        var u = new Vector3d(face[2].X - face[0].X, face[2].Y - face[0].Y, face[2].Z - face[0].Z);
        var v = new Vector3d(face[3].X - face[1].X, face[3].Y - face[1].Y, face[3].Z - face[1].Z);
        var normal = u.CrossProduct(v).GetNormal();
        if (normal.Z < 0) normal = normal.Negate();

        var texto = new DBText
        {
            Layer = camada,
            TextString = positivo ? "+" : "-",
            Height = AlturaDoSinal,
            Normal = normal,
            Position = new Point3d(ponto.X, ponto.Y, ponto.Z),
        };

        espaco.AppendEntity(texto);
        transacao.AddNewlyCreatedDBObject(texto, true);
        PluginXData.Save(transacao, texto, StringSign.Tipo, 1, [.. new StringSign(stringId, positivo).ToFields()]);
    }

    /// <summary>Apaga as strings dadas (polilinha e sinais), pelo GUID no XData.</summary>
    private static void Apagar(Transaction transacao, Database database, IReadOnlySet<Guid> strings)
    {
        if (strings.Count == 0) return;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
        var classeDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || (id.ObjectClass != classeDaLinha && id.ObjectClass != classeDoTexto)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

            var dela = e is Polyline3d
                ? ElectricalStore.LoadString(e)?.Id
                : PluginXData.Load(e, StringSign.Tipo, 1, StringSign.FieldCount) is { } c ? StringSign.Parse(c)?.String : null;

            if (dela is { } g && strings.Contains(g))
            {
                e.UpgradeOpen();
                e.Erase();
            }
        }
    }

    /// <summary>
    /// 11.8: as mesas que não casaram ficam selecionadas no desenho (o
    /// contorno), para o usuário ver quais são além de ler o aviso.
    /// </summary>
    internal static void SelecionarSemTipo(Document documento, IReadOnlyList<Guid> mesas)
    {
        if (mesas.Count == 0) return;

        var ids = new List<ObjectId>();
        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var todas = LayoutScan.Tables(transacao, documento.Database);
            foreach (var guid in mesas)
                if (todas.TryGetValue(guid, out var partes)) ids.AddRange(partes.Contours);
        }

        documento.Editor.SetImpliedSelection([.. ids]);
    }

    /// <summary>
    /// As strings do desenho que tocam as mesas lidas, com as mesas de cada
    /// uma pelo GUID dos módulos (o vínculo, nunca a posição). Módulo que não
    /// é de mesa lida conta como "mesa de fora" (Guid.Empty): a string que
    /// passa para fora do grupo não é regerada.
    /// </summary>
    private static List<ExistingString> Existentes(Transaction transacao, Database database, IReadOnlyList<FieldTable> mesas)
    {
        var mesaDoModulo = new Dictionary<Guid, Guid>();
        foreach (var mesa in mesas)
            foreach (var m in mesa.Modules) mesaDoModulo[m.Id] = mesa.Id;

        var lista = new List<ExistingString>();
        foreach (var (_, s) in ElectricalStore.Strings(transacao, database))
        {
            var mesasDaString = s.Modules.Select(m => mesaDoModulo.TryGetValue(m, out var t) ? t : Guid.Empty).ToHashSet();
            if (mesasDaString.Any(t => t != Guid.Empty)) lista.Add(new ExistingString(s, mesasDaString));
        }

        return lista;
    }
}
