using Autodesk.AutoCAD.DatabaseServices;

namespace UFV.Plugin;

/// <summary>
/// Acha os alinhamentos do desenho pelo XData, sem olhar o registro central.
///
/// É o par do que a área já tinha, e a revisão do 4.2 mostrou por que ele não
/// podia faltar: sem esta varredura, <c>AlignmentXData.Load</c> era código
/// morto — a identidade era GRAVADA na entidade e nunca lida por caminho
/// nenhum. Na prática, um alinhamento copiado para outro desenho ficava
/// invisível e irrecuperável, e o comentário que prometia "o XData é a verdade
/// e o reindexar o reconstrói" era verdadeiro para a área e falso para o
/// alinhamento.
///
/// Promessa escrita que o código não cumpre é pior que promessa nenhuma.
/// </summary>
internal static class AlignmentScan
{
    /// <summary>
    /// Os alinhamentos que existem no ModelSpace, lidos do XData de cada
    /// entidade.
    ///
    /// Varre o espaço inteiro porque é isso que o reindexar precisa: depois de
    /// uma cópia entre desenhos, o registro central não sabe de nada e a única
    /// fonte é a própria entidade.
    /// </summary>
    internal static IReadOnlyList<AlignmentRecord> Varrer(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var achados = new List<AlignmentRecord>();

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(
            tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (var id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var identidade = AlignmentXData.Load(entidade);
            if (identidade is null) continue;

            achados.Add(new AlignmentRecord(identidade, entidade.Handle.ToString()));
        }

        transacao.Commit();

        return achados;
    }
}
