using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>Uma cópia que ganhou identidade própria.</summary>
/// <param name="Table">O GUID novo da mesa copiada.</param>
/// <param name="Label">O letreiro que ela herdou (renumerar é o 7.10).</param>
/// <param name="HasContour">Se a cópia trouxe o contorno (senão são peças órfãs com mesa nova).</param>
/// <param name="Pieces">Quantas entidades ganharam identidade nova.</param>
internal sealed record ReidentifiedCopy(Guid Table, string Label, bool HasContour, int Pieces);

/// <summary>
/// A cópia (7.5): entidades nossas acrescentadas ao desenho com XData de
/// mesa que já existe (COPY, COPYCLIP, ARRAY) ganham identidade nova: um
/// GUID novo para a mesa da cópia, um GUID novo para cada pilar, módulo,
/// face e nota, com os vínculos (mesa, módulo da face) refeitos. Regra
/// sagrada 3 restaurada no fim do comando que copiou.
///
/// Como saber quais peças acrescentadas formam UMA cópia: se, entre as
/// acrescentadas de uma mesa de origem, nenhum GUID de peça se repete, é
/// uma cópia só, seja qual for a transformação (COPY, MIRROR, ROTATE com
/// cópia, colagem). Só quando um GUID de peça aparece mais de uma vez (COPY
/// múltiplo, ARRAY) as cópias são separadas pelo deslocamento: cada peça
/// acrescentada tem um original no desenho com o mesmo GUID, e o vetor da
/// original para a cópia, ao milímetro, é o mesmo para todas as peças da
/// mesma cópia. Duas cópias múltiplas no mesmo ponto ficariam juntas: caso
/// de laboratório, anotado.
/// </summary>
internal static class CopyFixer
{
    /// <summary>O desenho está em metro; o deslocamento é comparado ao milímetro.</summary>
    private const double MilimetrosPorMetro = 1000;

    /// <summary>
    /// Dá identidade nova às entidades acrescentadas. Devolve uma entrada
    /// por cópia (mesa nova). Entidades sem original no desenho (colagem de
    /// outro desenho) formam uma cópia só por mesa de origem.
    /// </summary>
    internal static IReadOnlyList<ReidentifiedCopy> Reidentify(Transaction transacao, Database database, IReadOnlyCollection<ObjectId> appended)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(appended);

        if (appended.Count == 0) return [];

        // Só o que é nosso: o usuário desenha linhas o tempo todo, e elas
        // chegam aqui também. Sem peça nossa, nada de varrer o desenho.
        var acrescentadas = new HashSet<ObjectId>();

        foreach (var id in appended)
        {
            if (id.IsNull || id.IsErased) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            using var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo);
            if (dados is not null) acrescentadas.Add(id);
        }

        if (acrescentadas.Count == 0) return [];

        // Por mesa de origem: as peças acrescentadas e se algum GUID de peça se repete.
        var porMesa = new Dictionary<Guid, List<(ObjectId Id, Guid Peca)>>();

        foreach (var id in acrescentadas)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var mesa = LayoutScan.TableOf(entidade);
            if (mesa is null) continue;

            if (!porMesa.TryGetValue(mesa.Value, out var lista))
            {
                lista = [];
                porMesa[mesa.Value] = lista;
            }

            lista.Add((id, PieceId(entidade) ?? Guid.Empty));
        }

        var precisaDeslocamento = porMesa.Values.Any(l => l.Select(p => p.Peca).Where(g => g != Guid.Empty).GroupBy(g => g).Any(g => g.Count() > 1));

        // As posições dos originais, por GUID de peça, só quando há cópias
        // múltiplas para separar (é uma varredura do modelo).
        var originais = new Dictionary<Guid, Point3d>();

        if (precisaDeslocamento)
        {
            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
            var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in espaco)
            {
                if (acrescentadas.Contains(id) || !LayoutScan.ENossaClasse(id)) continue;
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

                using var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo);
                if (dados is null) continue;

                if (PieceId(entidade) is { } guid && Posicao(transacao, entidade) is { } p) originais.TryAdd(guid, p);
            }
        }

        // Os grupos: uma cópia por mesa, ou, com repetição, por (mesa, deslocamento ao milímetro).
        var grupos = new Dictionary<(Guid Mesa, long Dx, long Dy, long Dz), List<ObjectId>>();

        foreach (var (mesa, pecas) in porMesa)
        {
            foreach (var (id, peca) in pecas)
            {
                var chave = (mesa, 0L, 0L, 0L);

                if (precisaDeslocamento && peca != Guid.Empty && originais.TryGetValue(peca, out var original)
                    && transacao.GetObject(id, OpenMode.ForRead) is Entity entidade && Posicao(transacao, entidade) is { } atual)
                {
                    chave = (mesa,
                        (long)Math.Round((atual.X - original.X) * MilimetrosPorMetro),
                        (long)Math.Round((atual.Y - original.Y) * MilimetrosPorMetro),
                        (long)Math.Round((atual.Z - original.Z) * MilimetrosPorMetro));
                }

                if (!grupos.TryGetValue(chave, out var lista))
                {
                    lista = [];
                    grupos[chave] = lista;
                }

                lista.Add(id);
            }
        }

        var resultado = new List<ReidentifiedCopy>();

        foreach (var grupo in grupos.Values)
        {
            var novaMesa = Guid.NewGuid();
            var modulos = new Dictionary<Guid, Guid>();
            var pecas = 0;
            var contorno = false;
            var letreiro = "(cópia)";

            // Módulos primeiro, para as faces acharem o módulo novo.
            foreach (var id in grupo)
            {
                var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);

                if (LayoutXData.LoadModule(entidade) is { } modulo)
                {
                    var novo = Guid.NewGuid();
                    modulos[modulo.Id] = novo;
                    LayoutXData.SaveModule(transacao, entidade, modulo with { Id = novo, Table = novaMesa });
                    pecas++;
                }
            }

            foreach (var id in grupo)
            {
                var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);

                if (LayoutXData.LoadTable(entidade) is { } mesa)
                {
                    contorno = true;
                    letreiro = mesa.Label;
                    LayoutXData.SaveTable(transacao, entidade, (mesa with { Id = novaMesa }).AsDirty(PendingChanges.ReasonAppended));
                    pecas++;
                }
                else if (LayoutXData.LoadPillar(entidade) is { } pilar)
                {
                    LayoutXData.SavePillar(transacao, entidade, pilar with { Id = Guid.NewGuid(), Table = novaMesa });
                    pecas++;
                }
                else if (LayoutXData.LoadFace(entidade) is { } face)
                {
                    var modulo = modulos.TryGetValue(face.Module, out var m) ? m : Guid.NewGuid();
                    LayoutXData.SaveFace(transacao, entidade, face with { Id = Guid.NewGuid(), Module = modulo, Table = novaMesa });
                    pecas++;
                }
                else if (LayoutXData.LoadNote(entidade) is { } nota)
                {
                    LayoutXData.SaveNote(transacao, entidade, nota with { Id = Guid.NewGuid(), Table = novaMesa });
                    pecas++;
                }
                else if (LayoutXData.LoadAnalysisText(entidade) is { } texto)
                {
                    // Texto de análise (8.9) vai com a cópia, com identidade
                    // nova: sem isto ele continuaria apontando para a mesa
                    // original e sairia com ela ao recalcular (revisão do 8.9).
                    LayoutXData.SaveAnalysisText(transacao, entidade, texto with { Id = Guid.NewGuid(), Table = novaMesa });
                    pecas++;
                }
                else if (LayoutXData.LoadTag(entidade) is { } tag)
                {
                    LayoutXData.SaveTag(transacao, entidade, tag with { Id = Guid.NewGuid(), Table = novaMesa });
                    pecas++;
                }
            }

            resultado.Add(new ReidentifiedCopy(novaMesa, letreiro, contorno, pecas));
        }

        return resultado;
    }

    /// <summary>O GUID próprio da peça (mesa, pilar, módulo, face ou nota).</summary>
    internal static Guid? PieceId(Entity entidade) =>
        LayoutXData.LoadTable(entidade)?.Id
        ?? LayoutXData.LoadPillar(entidade)?.Id
        ?? LayoutXData.LoadModule(entidade)?.Id
        ?? LayoutXData.LoadFace(entidade)?.Id
        ?? LayoutXData.LoadNote(entidade)?.Id
        ?? LayoutXData.LoadAnalysisText(entidade)?.Id
        ?? LayoutXData.LoadTag(entidade)?.Id;

    /// <summary>Um ponto da entidade, para medir o deslocamento da cópia.</summary>
    private static Point3d? Posicao(Transaction transacao, Entity entidade)
    {
        switch (entidade)
        {
            case Polyline3d polilinha:
                foreach (ObjectId v in polilinha)
                    return ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position;
                return null;
            case BlockReference bloco:
                return bloco.Position;
            case Face face:
                return face.GetVertexAt(0);
            case MText texto:
                return texto.Location;
            case Line linha:
                return linha.StartPoint;
            default:
                return null;
        }
    }
}
