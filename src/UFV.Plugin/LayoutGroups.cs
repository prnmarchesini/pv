using Autodesk.AutoCAD.DatabaseServices;

namespace UFV.Plugin;

/// <summary>
/// A mesa como UMA coisa na tela: um grupo anônimo e selecionável do
/// AutoCAD com todas as peças dela (contorno, pilares, módulos, faces e
/// notas). Renan, 27/09/2026: "para eu clicar em uma mesa, a mesa precisaria
/// ser BLOCO". Com o grupo, um clique numa peça seleciona a mesa inteira
/// (PICKSTYLE, ligado por padrão), o MOVE e o COPY levam a mesa toda, e o
/// botão direito vale para ela — sem trocar a estrutura em que cada peça
/// carrega a própria identidade no XData, que todo o resto do plugin lê.
///
/// A identidade continua no XData, nunca no grupo: o grupo é conveniência
/// de seleção e pode sumir (o usuário pode desagrupar) sem que nada se perca.
/// </summary>
internal static class LayoutGroups
{
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoGrupo = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Group));

    /// <summary>A descrição que o grupo leva (aparece no comando GROUP).</summary>
    private const string Descricao = "UFV: mesa";

    /// <summary>Cria o grupo de uma mesa com estas peças, na transação dada.</summary>
    internal static void Criar(Transaction transacao, Database database, IReadOnlyCollection<ObjectId> pecas)
    {
        if (pecas.Count == 0) return;

        var grupos = (DBDictionary)transacao.GetObject(database.GroupDictionaryId, OpenMode.ForWrite);
        var grupo = new Group(Descricao, true);

        // "*" dá um nome anônimo (*A1, *A2…): não polui a lista de grupos
        // com nome e não colide entre mesas.
        grupos.SetAt("*", grupo);
        transacao.AddNewlyCreatedDBObject(grupo, true);
        grupo.SetAnonymous();

        grupo.Append(new ObjectIdCollection(pecas.ToArray()));
    }

    /// <summary>Os grupos a que estas entidades pertencem (pelos reatores persistentes delas).</summary>
    internal static HashSet<ObjectId> GruposDe(Transaction transacao, IEnumerable<ObjectId> pecas)
    {
        var grupos = new HashSet<ObjectId>();

        foreach (var id in pecas)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var reatores = entidade.GetPersistentReactorIds();
            if (reatores is null) continue;

            foreach (ObjectId reator in reatores)
            {
                if (reator.IsNull || !reator.IsValid || reator.IsErased) continue;

                if (reator.ObjectClass?.IsDerivedFrom(ClasseDoGrupo) == true) grupos.Add(reator);
            }
        }

        return grupos;
    }

    /// <summary>
    /// Apaga, dos grupos dados, os que ficaram sem nenhuma peça viva. Chamado
    /// depois de apagar as peças de uma mesa, na mesma transação: um grupo
    /// vazio não faz mal, mas acumularia um por recálculo.
    /// </summary>
    internal static void ApagarVazios(Transaction transacao, IEnumerable<ObjectId> grupos)
    {
        foreach (var id in grupos)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Group grupo) continue;

            // Só os nossos: anônimos, com a nossa descrição. Um grupo com
            // nome que o usuário fez com peças da mesa é dele.
            if (!grupo.Name.StartsWith('*') || grupo.Description != Descricao) continue;

            if (grupo.GetAllEntityIds().All(peca => peca.IsErased))
            {
                grupo.UpgradeOpen();
                grupo.Erase();
            }
        }
    }
}
