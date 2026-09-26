using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>As peças de uma mesa no desenho, achadas pelo GUID dela no XData.</summary>
/// <param name="Identity">A identidade lida do contorno, ou null se o contorno sumiu.</param>
/// <param name="Contours">
/// Os contornos com este GUID: um, normalmente. Dois ou mais só se a cópia
/// (7.5) não conseguiu dar identidade nova (falha na transação) ou num
/// desfazer parcial; quem recalcula ou pinta precisa saber.
/// </param>
/// <param name="Pillars">Os blocos de pilar.</param>
/// <param name="Modules">Os blocos de módulo.</param>
/// <param name="Faces">As faces superiores.</param>
/// <param name="Notes">As notas: cotas (risco e texto) e avisos.</param>
internal sealed record TableParts(
    TableIdentity? Identity,
    IReadOnlyList<ObjectId> Contours,
    IReadOnlyList<ObjectId> Pillars,
    IReadOnlyList<ObjectId> Modules,
    IReadOnlyList<ObjectId> Faces,
    IReadOnlyList<ObjectId> Notes)
{
    /// <summary>O contorno (o primeiro, quando há cópia), ou nulo.</summary>
    public ObjectId? Contour => Contours.Count > 0 ? Contours[0] : null;

    /// <summary>Se há mais de um contorno com o GUID: mesa copiada e colada, assunto do 7.5.</summary>
    public bool IsDuplicated => Contours.Count > 1;

    /// <summary>Contornos, pilares e módulos: o que se pinta. A face nunca (é o que o PVsyst recebe); a nota também não.</summary>
    public IEnumerable<ObjectId> Paintable => Contours.Concat(Pillars).Concat(Modules);

    /// <summary>Tudo da mesa: o que se apaga ao refazer.</summary>
    public IEnumerable<ObjectId> All => Paintable.Concat(Faces).Concat(Notes);
}

/// <summary>
/// Varre o espaço do modelo e agrupa as nossas entidades por mesa, pelo
/// GUID no XData — nunca pela camada. É a base da etapa 7: sujar, recalcular,
/// validar e numerar precisam de "quais peças são desta mesa".
///
/// É uma varredura inteira do desenho a cada chamada. Numa usina de milhares
/// de mesas isso é dezenas de milhares de entidades, que o AutoCAD lê em
/// fração de segundo; um índice em memória fica para quando medir mostrar
/// que precisa.
/// </summary>
internal static class LayoutScan
{
    /// <summary>Todas as mesas do desenho, por GUID. Mesas cujo contorno sumiu aparecem sem identidade.</summary>
    internal static IReadOnlyDictionary<Guid, TableParts> Tables(Transaction transacao, Database database)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(database);

        var identidades = new Dictionary<Guid, TableIdentity>();
        var contornos = new Dictionary<Guid, List<ObjectId>>();
        var pilares = new Dictionary<Guid, List<ObjectId>>();
        var modulos = new Dictionary<Guid, List<ObjectId>>();
        var faces = new Dictionary<Guid, List<ObjectId>>();
        var notas = new Dictionary<Guid, List<ObjectId>>();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            // A classe vem sem abrir o objeto: só as nossas três classes são
            // abertas; superfície, curvas de nível e textos ficam fechados.
            if (!ENossaClasse(id)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            using (var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo))
                if (dados is null) continue;

            if (LayoutXData.LoadTable(entidade) is { } mesa)
            {
                // Duas entidades com o mesmo GUID de mesa (cópia): a primeira
                // dá a identidade; todos os contornos ficam na lista.
                identidades.TryAdd(mesa.Id, mesa);
                Juntar(contornos, mesa.Id, id);
            }
            else if (LayoutXData.LoadPillar(entidade) is { } pilar)
            {
                Juntar(pilares, pilar.Table, id);
            }
            else if (LayoutXData.LoadModule(entidade) is { } modulo)
            {
                Juntar(modulos, modulo.Table, id);
            }
            else if (LayoutXData.LoadFace(entidade) is { } face)
            {
                Juntar(faces, face.Table, id);
            }
            else if (LayoutXData.LoadNote(entidade) is { } nota)
            {
                Juntar(notas, nota.Table, id);
            }
        }

        var todas = contornos.Keys.Concat(pilares.Keys).Concat(modulos.Keys).Concat(faces.Keys).Concat(notas.Keys).Distinct();
        var resultado = new Dictionary<Guid, TableParts>();

        foreach (var guid in todas)
        {
            resultado[guid] = new TableParts(
                identidades.GetValueOrDefault(guid),
                contornos.GetValueOrDefault(guid) ?? [],
                pilares.GetValueOrDefault(guid) ?? [],
                modulos.GetValueOrDefault(guid) ?? [],
                faces.GetValueOrDefault(guid) ?? [],
                notas.GetValueOrDefault(guid) ?? []);
        }

        return resultado;
    }

    /// <summary>O GUID da mesa a que uma entidade nossa pertence, ou null se ela não é nossa.</summary>
    internal static Guid? TableOf(Entity entidade)
    {
        ArgumentNullException.ThrowIfNull(entidade);

        using (var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo))
            if (dados is null) return null;

        return LayoutXData.LoadTable(entidade)?.Id
            ?? LayoutXData.LoadPillar(entidade)?.Table
            ?? LayoutXData.LoadModule(entidade)?.Table
            ?? LayoutXData.LoadFace(entidade)?.Table
            ?? LayoutXData.LoadNote(entidade)?.Table;
    }

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoBloco = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(BlockReference));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaPolilinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaFace = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Face));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));

    /// <summary>Bloco (pilar, módulo), polilinha 3D (contorno), face, texto ou linha (notas): o que pode ser peça nossa.</summary>
    internal static bool ENossaClasse(ObjectId id) =>
        id.ObjectClass == ClasseDoBloco || id.ObjectClass == ClasseDaPolilinha || id.ObjectClass == ClasseDaFace
        || id.ObjectClass == ClasseDoTexto || id.ObjectClass == ClasseDaLinha;

    private static void Juntar(Dictionary<Guid, List<ObjectId>> grupos, Guid mesa, ObjectId id)
    {
        if (!grupos.TryGetValue(mesa, out var lista))
        {
            lista = [];
            grupos[mesa] = lista;
        }

        lista.Add(id);
    }
}
