using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A pré-tag das strings (item 8 de 10/10/2026): no Distribuir, cada string
/// de um inversor ganha o nome curto dele ("I3") por cima, na camada
/// CLIVUS_STRING_PRETAG e na cor do inversor, seguindo o eixo da mesa como a
/// tag de verdade (texto nunca em pé; se não cabe, a fonte diminui). A tag de
/// verdade (Numeração) e o Soltar apagam a pré-tag da string
/// (<see cref="NumeracaoDesenho.Aplicar"/>). A cota vem do módulo, nunca de clique.
/// </summary>
internal static class PreTagDasStrings
{
    internal const string Camada = PluginInfo.PrefixoDeDados + "_STRING_PRETAG";
    private const int Versao = 1;

    /// <summary>A altura do texto (a tag de verdade tem 0,5 m; a pré-tag é curta e fica um pouco menor).</summary>
    private const double Altura = 0.4;

    /// <summary>Quanto o texto fica acima do módulo mais alto da string, em metro.</summary>
    private const double AcimaDosModulos = 0.15;

    // As opções do Distribuir (item 5 da segunda rodada de 10/10/2026): registro
    // próprio, versão 1. Sem registro (desenho de antes), o padrão: tudo sim.
    private const string ChaveDasOpcoes = "PRETAG_OPCOES";
    private const int VersaoDasOpcoes = 1;
    private static readonly string OQueOpcoes = Tr.N("das opções da pré-tag");

    /// <summary>As opções gravadas, ou o padrão (tudo sim); o problema do registro, se havia.</summary>
    internal static PreTagOptions Opcoes(Database database, out string? problema)
    {
        var lido = PluginRecords.Load<PreTagOptions>(database, ChaveDasOpcoes, VersaoDasOpcoes, PreTagOptions.FieldCount, PreTagOptions.Parse, OQueOpcoes);
        problema = lido.Problem;
        return lido.Items.Count > 0 ? lido.Items[0] : PreTagOptions.Default;
    }

    internal static void GravarOpcoes(Database database, PreTagOptions opcoes) =>
        PluginRecords.Save(database, ChaveDasOpcoes, VersaoDasOpcoes, PreTagOptions.FieldCount, [opcoes], o => o.ToFields());

    /// <summary>Os textos de pré-tag do desenho, pela string de cada um.</summary>
    internal static ILookup<Guid, ObjectId> Textos(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
        var achados = new List<(Guid, ObjectId)>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || id.ObjectClass != classe) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is MText texto && Ler(texto) is { } p) achados.Add((p.String, id));
        }

        return achados.ToLookup(x => x.Item1, x => x.Item2);
    }

    internal static StringPreTag? Ler(Entity entidade) =>
        PluginXData.Load(entidade, StringPreTag.Tipo, Versao, StringPreTag.FieldCount) is { } c ? StringPreTag.Parse(c) : null;

    /// <summary>Apaga as pré-tags das strings dadas (na transação de quem chama). Quantas.</summary>
    internal static int Apagar(Transaction transacao, ILookup<Guid, ObjectId> textos, IEnumerable<Guid> strings)
    {
        var n = 0;
        foreach (var s in strings)
            foreach (var id in textos[s])
                if (!id.IsErased && transacao.GetObject(id, OpenMode.ForWrite) is Entity e)
                {
                    e.Erase();
                    n++;
                }

        return n;
    }

    /// <summary>
    /// Acerta as pré-tags do desenho com o vínculo de agora
    /// (<see cref="StringPreTag.Expected"/>): a string de um inversor sem tag
    /// de verdade tem a dela (refeita, se o texto ou a cor mudou); as outras
    /// (livres, com tag, de inversor que sumiu) e as de string que sumiu
    /// ficam sem. Com <paramref name="soAsQueJaTem"/> (renomear ou trocar a
    /// cor do inversor), só as strings que já têm pré-tag são refeitas: a
    /// alocação à mão não ganha pré-tag por tabela. Com <paramref name="soEstas"/>
    /// (o Apagar tags da numeração), só essas strings são acertadas; as
    /// outras ficam como estão. Quantas pré-tags há no fim (entre as acertadas).
    /// </summary>
    internal static int Atualizar(Database database, bool soAsQueJaTem = false, IReadOnlyCollection<Guid>? soEstas = null)
    {
        var setup = ConfiguracaoEletricaStore.Ler(database).Setup;
        using var transacao = database.TransactionManager.StartTransaction();

        var strings = ElectricalStore.Strings(transacao, database).GroupBy(x => x.String.Id).Where(g => g.Count() == 1).Select(g => g.First()).ToList();
        var esperadas = StringPreTag.Expected(setup.Inverters, strings.Select(x => x.String));

        // "Inserir nome do inversor" desmarcado: nenhuma string deve ter pré-tag (as que havia saem).
        var opcoes = Opcoes(database, out _);
        if (!opcoes.Insert) esperadas = new Dictionary<Guid, string>();
        var textos = Textos(transacao, database);
        var alvo = soEstas?.ToHashSet();
        if (alvo is not null)
        {
            strings = strings.Where(x => alvo.Contains(x.String.Id)).ToList();
            esperadas = esperadas.Where(x => alvo.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        }

        if (soAsQueJaTem)
        {
            if (textos.Count == 0) return 0;
            esperadas = esperadas.Where(x => textos.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        }

        // As que não devem ter (ou cuja string sumiu) saem.
        Apagar(transacao, textos, textos.Select(g => g.Key).Where(s => (alvo is null || alvo.Contains(s)) && !esperadas.ContainsKey(s)).ToList());

        var modulos = esperadas.Count > 0 ? NumeracaoDesenho.Modulos(transacao, database) : new Dictionary<Guid, NumeracaoDesenho.Lugar>();
        BlockTableRecord? espaco = null;
        string camada = string.Empty;
        Action<MText> estilo = _ => { };
        var novos = new List<ObjectId>();
        var total = 0;

        foreach (var (_, s) in strings)
        {
            if (!esperadas.TryGetValue(s.Id, out var texto)) continue;
            var cor = setup.FindInverter(s.Inverter)?.Color;

            // Já está certa (mesmo texto, uma só): fica como está (a cor é refeita).
            var velhas = textos[s.Id].Where(id => !id.IsErased).ToList();
            if (velhas.Count == 1 && transacao.GetObject(velhas[0], OpenMode.ForRead) is MText atual && Ler(atual)?.Text == texto)
            {
                atual.UpgradeOpen();
                atual.Color = CorDasStrings.Cor(cor);
                NumeracaoDesenho.Emoldurar(atual, opcoes.Background, opcoes.Border);
                total++;
                continue;
            }

            Apagar(transacao, textos, [s.Id]);

            var lugares = s.Modules.Select(m => modulos.GetValueOrDefault(m)).OfType<NumeracaoDesenho.Lugar>().ToList();
            if (lugares.Count == 0) continue;

            // O eixo da mesa (StringTagLayout), como a tag de verdade: nunca em pé.
            var (rumo, comprimento) = StringTagLayout.Axis(lugares.Select(l => (l.Centro.X, l.Centro.Y)).ToList());

            if (espaco is null)
            {
                espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
                camada = LayoutLayers.Garantir(transacao, database, Camada, new RgbColor(200, 200, 200));
                estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
            }

            var mtexto = new MText
            {
                Location = new Point3d(lugares.Average(l => l.Centro.X), lugares.Average(l => l.Centro.Y), lugares.Max(l => l.Topo) + AcimaDosModulos),
                TextHeight = Altura,
                Layer = camada,
                Color = CorDasStrings.Cor(cor),
                Attachment = AttachmentPoint.MiddleCenter,
                Rotation = rumo,
                Contents = texto,
            };

            espaco.AppendEntity(mtexto);
            transacao.AddNewlyCreatedDBObject(mtexto, true);
            estilo(mtexto);

            // Moldura e fundo como os das tags da Numeração (item 5 da segunda rodada); com eles, o texto ocupa um pouco mais.
            NumeracaoDesenho.Emoldurar(mtexto, opcoes.Background, opcoes.Border);
            var largura = mtexto.ActualWidth * (opcoes.Background || opcoes.Border ? 1.15 : 1);
            var altura = StringTagLayout.FitHeight(Altura, largura, comprimento);
            if (altura < Altura) mtexto.TextHeight = altura;
            PluginXData.Save(transacao, mtexto, StringPreTag.Tipo, Versao, [.. new StringPreTag(s.Id, texto).ToFields()]);
            novos.Add(mtexto.ObjectId);
            total++;
        }

        // Por cima dos módulos na ordem de desenho, como as tags.
        if (espaco is not null && novos.Count > 0)
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
            ordem.MoveToTop(new ObjectIdCollection(novos.ToArray()));
        }

        transacao.Commit();
        return total;
    }
}
