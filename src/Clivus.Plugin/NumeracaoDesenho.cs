using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A numeração das strings no desenho (elétrica, 15.4): lê a cadeia pelo
/// vínculo (strings, inversores, trafos), pede a conta ao Core
/// (<see cref="StringNumbering"/>), grava a tag no XData de cada string e
/// desenha o texto sobre ela, na camada CLIVUS_STRING_TAG, no plano dos
/// módulos (regra 5: a cota vem do módulo, nunca de clique).
/// </summary>
internal static class NumeracaoDesenho
{
    internal const string CamadaDaTag = PluginInfo.PrefixoDeDados + "_STRING_TAG";
    private const int VersaoDaTag = 1;

    /// <summary>Quanto o texto fica acima do módulo mais alto da string, em metro (como as tags do 8.14).</summary>
    private const double AcimaDosModulos = 0.15;

    /// <summary>A altura do texto quando o projeto não tem estilo de texto, em metro.</summary>
    private const double Altura = 0.5;

    /// <summary>Um módulo no desenho: a mesa, o centro da face de cima e a cota mais alta dela.</summary>
    internal sealed record Lugar(Guid Mesa, Point3d Centro, double Topo);

    /// <summary>
    /// Os módulos do desenho pelo GUID: o centro da face de cima (o plano
    /// real do módulo); sem face, a inserção do bloco.
    /// </summary>
    internal static Dictionary<Guid, Lugar> Modulos(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeDaFace = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Face));
        var classeDoBloco = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(BlockReference));
        var porFace = new Dictionary<Guid, Lugar>();
        var porBloco = new Dictionary<Guid, Lugar>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased) continue;

            if (id.ObjectClass == classeDaFace)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not Face face || LayoutXData.LoadFace(face) is not { } f) continue;

                var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).ToList();
                porFace.TryAdd(f.Module, new Lugar(f.Table, new Point3d(cantos.Average(p => p.X), cantos.Average(p => p.Y), cantos.Average(p => p.Z)), cantos.Max(p => p.Z)));
            }
            else if (id.ObjectClass == classeDoBloco)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference bloco || LayoutXData.LoadModule(bloco) is not { } m) continue;
                porBloco.TryAdd(m.Id, new Lugar(m.Table, bloco.Position, bloco.Position.Z));
            }
        }

        foreach (var (modulo, lugar) in porBloco) porFace.TryAdd(modulo, lugar);
        return porFace;
    }

    /// <summary>Os textos de tag de string do desenho, pela string de cada um.</summary>
    internal static ILookup<Guid, ObjectId> Textos(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
        var achados = new List<(Guid, ObjectId)>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || id.ObjectClass != classe) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is MText texto && LerTag(texto) is { } tag) achados.Add((tag.String, id));
        }

        return achados.ToLookup(x => x.Item1, x => x.Item2);
    }

    internal static StringTagText? LerTag(Entity entidade) =>
        PluginXData.Load(entidade, StringTagText.Tipo, VersaoDaTag, StringTagText.FieldCount) is { } c ? StringTagText.Parse(c) : null;

    /// <summary>
    /// Gera as tags (15.4) do alcance (15.5: tudo, um inversor ou um bloco):
    /// varre a usina inteira na ordem dos blocos, grava a tag em cada string
    /// do alcance e redesenha os textos delas; o resto não é tocado. Devolve
    /// as linhas do relatório (a primeira é o resumo).
    /// </summary>
    internal static IReadOnlyList<string> Gerar(Database database, NumberingScope? alcance = null)
    {
        var (esquema, problemaDoEsquema) = NumeracaoStore.Esquema(database);
        var (varredura, problemaDaVarredura) = NumeracaoStore.Varredura(database);
        var inversores = ElectricalStore.Inverters(database);
        var trafos = ElectricalStore.Transformers(database);

        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database);
        var modulos = Modulos(transacao, database);
        var spots = modulos.ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));

        var resultado = StringNumbering.Number(esquema, varredura, trafos.Items, inversores.Items, strings.Select(s => s.String).ToList(), spots, alcance);
        Aplicar(transacao, database, strings, resultado.Tags, modulos);
        transacao.Commit();

        var linhas = Relatorio(resultado, strings.Count, inversores.Items);
        foreach (var problema in new[] { problemaDoEsquema, problemaDaVarredura, inversores.Problem, trafos.Problem })
            if (problema is not null) linhas.Add(Tr.F("  ATENÇÃO: {0}.", problema));

        return linhas;
    }

    /// <summary>
    /// Apaga as tags do alcance (15.5): esvazia o campo Tag das strings e
    /// apaga os textos delas. Vínculo e geometria não mudam. Devolve a frase.
    /// </summary>
    internal static string Apagar(Database database, NumberingScope alcance)
    {
        var (varredura, _) = NumeracaoStore.Varredura(database);

        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database);
        var modulos = Modulos(transacao, database);
        var spots = modulos.ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));
        var vazias = StringNumbering.Clear(strings.Select(s => s.String).ToList(), alcance, varredura, spots);
        var tinham = strings.Count(s => vazias.ContainsKey(s.String.Id) && s.String.Tag.Length > 0);

        Aplicar(transacao, database, strings, vazias, modulos);
        transacao.Commit();

        return Tr.F("{0} tag(s) apagada(s) de {1} string(s).", tinham, vazias.Count);
    }

    /// <summary>
    /// Grava as tags novas nas strings dadas (só o campo Tag do XData; a
    /// geometria não muda) e troca os textos delas: apaga os de antes e
    /// desenha um por string com tag.
    /// </summary>
    internal static void Aplicar(
        Transaction transacao,
        Database database,
        IReadOnlyList<(ObjectId Id, ElectricalString String)> strings,
        IReadOnlyDictionary<Guid, string> tags,
        IReadOnlyDictionary<Guid, Lugar> modulos)
    {
        var textos = Textos(transacao, database);
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, CamadaDaTag, new RgbColor(255, 140, 0));
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
        var novos = new List<ObjectId>();

        foreach (var (id, s) in strings)
        {
            if (!tags.TryGetValue(s.Id, out var tag)) continue;

            if (s.Tag != tag && transacao.GetObject(id, OpenMode.ForWrite) is Entity entidade)
                ElectricalStore.SaveString(transacao, entidade, s with { Tag = tag });

            foreach (var velho in textos[s.Id])
                if (transacao.GetObject(velho, OpenMode.ForWrite) is Entity texto) texto.Erase();

            if (tag.Length == 0) continue;

            var lugares = s.Modules.Select(m => modulos.GetValueOrDefault(m)).OfType<Lugar>().ToList();
            if (lugares.Count == 0) continue;

            var primeiro = lugares[0].Centro;
            var ultimo = lugares[^1].Centro;
            var rumo = primeiro.DistanceTo(ultimo) < 1e-6 ? 0 : LayoutDrawer.RumoLegivel(ultimo.X - primeiro.X, ultimo.Y - primeiro.Y);

            var mtexto = new MText
            {
                // No meio da string, acima do módulo mais alto dela (o texto
                // não some debaixo da mesa inclinada no sombreado).
                Location = new Point3d(lugares.Average(l => l.Centro.X), lugares.Average(l => l.Centro.Y), lugares.Max(l => l.Topo) + AcimaDosModulos),
                TextHeight = Altura,
                Layer = camada,
                Attachment = AttachmentPoint.MiddleCenter,
                Rotation = rumo,
                Contents = tag,
            };

            espaco.AppendEntity(mtexto);
            transacao.AddNewlyCreatedDBObject(mtexto, true);
            estilo(mtexto);
            PluginXData.Save(transacao, mtexto, StringTagText.Tipo, VersaoDaTag, [.. new StringTagText(s.Id, tag).ToFields()]);
            novos.Add(mtexto.ObjectId);
        }

        // Por cima dos módulos na ordem de desenho, como as outras tags.
        if (novos.Count > 0)
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
            ordem.MoveToTop(new ObjectIdCollection(novos.ToArray()));
        }
    }

    /// <summary>O relatório da numeração: o resumo e o que ficou sem tag ou sem trafo (nada falha calado, regra elétrica 6).</summary>
    internal static List<string> Relatorio(StringNumberingResult r, int strings, IReadOnlyList<Inverter> inversores)
    {
        if (strings == 0) return [Tr.T("O desenho não tem string: gere as strings antes de numerar.")];
        if (r.Tags.Count == 0) return [Tr.T("Nenhuma string neste pedaço: nada mudou.")];

        var linhas = new List<string> { Tr.F("{0} string(s) com tag; {1} sem tag.", r.Tagged, r.Tags.Count - r.Tagged) };

        if (r.Free > 0) linhas.Add(Tr.F("  {0} string(s) sem inversor ficaram sem tag: aloque num inversor e gere de novo.", r.Free));
        if (r.UnknownInverter > 0) linhas.Add(Tr.F("  ATENÇÃO: {0} string(s) apontam para inversor que não está no cadastro; ficaram sem tag.", r.UnknownInverter));
        if (r.Unplaced > 0) linhas.Add(Tr.F("  ATENÇÃO: {0} string(s) com o primeiro módulo fora do desenho; ficaram sem tag.", r.Unplaced));

        if (r.DuplicateTags.Count > 0)
            linhas.Add(Tr.F("  ATENÇÃO: {0} tag(s) repetida(s) com strings fora deste pedaço ({1}): a ordem dos blocos ou a alocação mudou; gere tudo de novo.", r.DuplicateTags.Count, string.Join(", ", r.DuplicateTags.Take(10))));

        if (r.InvertersWithoutTransformer.Count > 0)
        {
            var nomes = r.InvertersWithoutTransformer.Select(id => inversores.FirstOrDefault(i => i.Id == id)?.Name ?? id.ToString("D"));
            linhas.Add(Tr.F("  {0} inversor(es) sem trafo ({1}): a tag sai sem o pedaço do trafo.", r.InvertersWithoutTransformer.Count, string.Join(", ", nomes)));
        }

        return linhas;
    }
}
