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
        var modelos = ElectricalStore.InverterModels(database);

        // Modelo sem {I} com mais de um inversor (o cadastro cresceu depois de salvar): nada muda, e se diz o porquê.
        if (esquema.Problem(StringNumbering.CountInverters(inversores.Items)) is { } recusa)
            return [Tr.F("Não gerei: {0}. Corrija o modelo da tag e salve.", recusa)];

        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database);
        var modulos = Modulos(transacao, database);
        var spots = modulos.ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));

        var resultado = StringNumbering.Number(esquema, varredura, trafos.Items, inversores.Items, strings.Select(s => s.String).ToList(), spots, alcance, modelos.Items);
        var orfaos = Aplicar(transacao, database, strings, resultado.Tags, modulos, apagarOrfaos: alcance is null || alcance.Kind == NumberingScopeKind.All, esquema);
        transacao.Commit();

        var linhas = Relatorio(resultado, strings.Count, inversores.Items);
        if (orfaos > 0) linhas.Add(Tr.F("  {0} texto(s) de tag de string que não existe mais foram apagados.", orfaos));
        foreach (var problema in new[] { problemaDoEsquema, problemaDaVarredura, inversores.Problem, trafos.Problem, modelos.Problem })
            if (problema is not null) linhas.Add(Tr.F("  ATENÇÃO: {0}.", problema));

        return linhas;
    }

    /// <summary>
    /// Apaga as tags do alcance (15.5): esvazia o campo Tag das strings e
    /// apaga os textos delas. Vínculo e geometria não mudam. Devolve a frase.
    /// </summary>
    internal static string Apagar(Database database, NumberingScope alcance)
    {
        var (varredura, problema) = NumeracaoStore.Varredura(database);

        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database);
        var modulos = Modulos(transacao, database);
        var spots = modulos.ToDictionary(m => m.Key, m => new ModuleSpot(m.Value.Mesa, m.Value.Centro.X, m.Value.Centro.Y));
        var vazias = StringNumbering.Clear(strings.Select(s => s.String).ToList(), alcance, varredura, spots);
        var tinham = strings.Count(s => vazias.ContainsKey(s.String.Id) && s.String.Tag.Length > 0);

        var orfaos = Aplicar(transacao, database, strings, vazias, modulos, apagarOrfaos: alcance.Kind == NumberingScopeKind.All, TagScheme.Default);
        transacao.Commit();

        // Sem a tag de verdade, a string que continua num inversor volta a ter a pré-tag dele.
        PreTagDasStrings.Atualizar(database, soEstas: vazias.Keys.ToList());

        var frase = Tr.F("{0} tag(s) apagada(s) de {1} string(s).", tinham, vazias.Count);
        if (orfaos > 0) frase += " " + Tr.F("{0} texto(s) de tag de string que não existe mais foram apagados.", orfaos);
        if (problema is not null) frase += " " + Tr.F("ATENÇÃO: {0}.", problema);
        return frase;
    }

    /// <summary>
    /// Grava as tags novas nas strings dadas (só o campo Tag do XData; a
    /// geometria não muda) e troca os textos delas: apaga os de antes e
    /// desenha um por string com tag. Com <paramref name="apagarOrfaos"/>
    /// (usina inteira), apaga também o texto de tag cuja string sumiu;
    /// devolve quantos. O texto ganha o fundo e a moldura da composição.
    /// </summary>
    internal static int Aplicar(
        Transaction transacao,
        Database database,
        IReadOnlyList<(ObjectId Id, ElectricalString String)> strings,
        IReadOnlyDictionary<Guid, string> tags,
        IReadOnlyDictionary<Guid, Lugar> modulos,
        bool apagarOrfaos,
        TagScheme esquema)
    {
        var textos = Textos(transacao, database);
        var orfaos = 0;

        // A pré-tag do inversor (item 8 de 10/10/2026) sai da string que ganhou
        // a tag de verdade ou que ficou livre (solta). A que só teve a tag
        // apagada e continua num inversor fica com a dela.
        PreTagDasStrings.Apagar(transacao, PreTagDasStrings.Textos(transacao, database), strings
            .Where(s => tags.TryGetValue(s.String.Id, out var nova) && (nova.Length > 0 || s.String.Inverter == Guid.Empty))
            .Select(s => s.String.Id).Distinct().ToList());

        if (apagarOrfaos)
        {
            var existem = strings.Select(s => s.String.Id).ToHashSet();
            foreach (var grupo in textos.Where(g => !existem.Contains(g.Key)))
            {
                foreach (var velho in grupo)
                {
                    if (transacao.GetObject(velho, OpenMode.ForWrite) is Entity texto)
                    {
                        texto.Erase();
                        orfaos++;
                    }
                }
            }
        }
        // A camada, o estilo e o espaço só quando há texto a desenhar: só
        // apagar (soltar as strings, 10/10/2026) não cria camada nem estilo.
        BlockTableRecord? espaco = null;
        string camada = string.Empty;
        Action<MText> estilo = _ => { };
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

            // O eixo da mesa, nunca o caminho do primeiro ao último módulo (a
            // string em U dava a tag em pé; Renan, 07/10/2026: "jamais quero
            // texto virado").
            var (rumo, comprimento) = StringTagLayout.Axis(lugares.Select(l => (l.Centro.X, l.Centro.Y)).ToList());

            if (espaco is null)
            {
                espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
                camada = LayoutLayers.Garantir(transacao, database, CamadaDaTag, new RgbColor(255, 140, 0));
                estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
            }

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
            Emoldurar(mtexto, esquema);

            // Não cabe no comprimento da string: a fonte diminui (o texto não gira).
            var largura = mtexto.ActualWidth * FolgaDaLargura;
            var altura = StringTagLayout.FitHeight(Altura, largura, comprimento);
            if (altura < Altura) mtexto.TextHeight = altura;
            PluginXData.Save(transacao, mtexto, StringTagText.Tipo, VersaoDaTag, [.. new StringTagText(s.Id, tag).ToFields()]);
            novos.Add(mtexto.ObjectId);
        }

        // Por cima dos módulos na ordem de desenho, como as outras tags.
        if (espaco is not null && novos.Count > 0)
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
            ordem.MoveToTop(new ObjectIdCollection(novos.ToArray()));
        }

        return orfaos;
    }

    /// <summary>Quanto a tag ocupa além do texto (o fundo e a moldura), na conta de caber na string.</summary>
    private const double FolgaDaLargura = 1.15;

    /// <summary>A folga do fundo e da moldura em volta do texto (1 = colado; o AutoCAD aceita de 1 a 5).</summary>
    internal const double FolgaDoFundo = 1.2;

    /// <summary>
    /// O fundo (máscara na cor do fundo da tela, que esconde o que está atrás
    /// e destaca a tag) e a moldura (o quadro do próprio MText): liga ou
    /// desliga conforme a composição. Nenhuma entidade a mais: apagar o texto
    /// leva os dois.
    /// </summary>
    internal static void Emoldurar(MText texto, TagScheme esquema)
    {
        // A folga liga o fundo no AutoCAD: só com fundo. Para desligar, a cor
        // da tela sai antes (senão o fundo volta) — conferido no nível 2.
        if (esquema.Background)
        {
            texto.BackgroundFill = true;
            texto.UseBackgroundColor = true;
            texto.BackgroundScaleFactor = FolgaDoFundo;
        }
        else
        {
            if (texto.UseBackgroundColor) texto.UseBackgroundColor = false;
            texto.BackgroundFill = false;
        }

        texto.ShowBorders = esquema.Border;
    }

    /// <summary>
    /// Põe o fundo e a moldura da composição nos textos de tag já desenhados
    /// (o texto da tag não muda; só gerar de novo muda). Devolve quantos.
    /// </summary>
    internal static int Reemoldurar(Database database, TagScheme esquema)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var quantos = 0;

        foreach (var grupo in Textos(transacao, database))
        {
            foreach (var id in grupo)
            {
                if (transacao.GetObject(id, OpenMode.ForWrite) is not MText texto) continue;
                Emoldurar(texto, esquema);
                quantos++;
            }
        }

        transacao.Commit();
        return quantos;
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

        if (r.DuplicateStrings > 0) linhas.Add(Tr.F("  ATENÇÃO: {0} string(s) com a identidade repetida (polilinha copiada?); ficaram sem tag.", r.DuplicateStrings));

        if (r.DuplicateTags.Count > 0)
            linhas.Add(Tr.F("  ATENÇÃO: {0} tag(s) repetida(s) com strings fora deste pedaço ({1}): a ordem dos blocos ou a alocação mudou; gere tudo de novo.", r.DuplicateTags.Count, string.Join(", ", r.DuplicateTags.Take(10))));
        else if (r.StaleOutside > 0)
            linhas.Add(Tr.F("  ATENÇÃO: {0} string(s) fora deste pedaço com a tag desatualizada (a ordem dos blocos ou a alocação mudou); gere tudo de novo.", r.StaleOutside));

        string Nome(Guid id) => inversores.FirstOrDefault(i => i.Id == id)?.Name ?? id.ToString("D");

        foreach (var carga in r.OverCapacity)
            linhas.Add(Tr.F("  ATENÇÃO: {0} acima da capacidade: {1} strings em {2} entradas.", Nome(carga.Inverter), carga.Strings, carga.Capacity));

        if (r.InvertersWithoutTransformer.Count > 0)
            linhas.Add(Tr.F("  {0} inversor(es) sem trafo ({1}): a tag sai sem o pedaço do trafo.", r.InvertersWithoutTransformer.Count, string.Join(", ", r.InvertersWithoutTransformer.Select(Nome))));

        if (r.InvertersWithMissingTransformer.Count > 0)
            linhas.Add(Tr.F("  ATENÇÃO: {0} inversor(es) apontam para trafo que não está no cadastro ({1}): a tag sai sem o pedaço do trafo.", r.InvertersWithMissingTransformer.Count, string.Join(", ", r.InvertersWithMissingTransformer.Select(Nome))));

        return linhas;
    }
}
