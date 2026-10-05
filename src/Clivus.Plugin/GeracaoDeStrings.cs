using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Gerar as strings (elétrica, 11.6 a 11.8): lê as mesas selecionadas e as
/// strings que já estão nelas, pede ao Core o plano (que tipo cai em que
/// grupo, que mesa não casou, que grupo tem string ligada a inversor) e
/// devolve o relatório. Quem decide é o Core; aqui só se lê e se escreve.
/// </summary>
internal static class GeracaoDeStrings
{
    /// <summary>O relatório de uma geração: linhas para a tela e os números para o nível 2.</summary>
    internal sealed record Relatorio(IReadOnlyList<string> Linhas, int Strings, int Grupos, int SemTipo, int Pulados, int Substituidas);

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
        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var problemas = new List<string>();
            var mesas = MesasDaString.Ler(transacao, database, guids, problemas);
            linhas.AddRange(problemas.Select(p => Tr.F("ATENÇÃO: {0}.", p)));

            plano = StringGeneration.Plan(tipos, mesas, Existentes(transacao, database, mesas));
            transacao.Commit();
        }

        foreach (var g in plano.Groups)
            linhas.Add(Tr.F("{0} → {1}: {2} string(s).", g.Type.Name, g.Labels, g.Strings.Count));

        linhas.AddRange(plano.Notes.Select(n => Tr.F("Aviso: {0}.", n)));
        linhas.AddRange(plano.Skipped.Select(n => Tr.F("Aviso: {0}.", n)));
        linhas.AddRange(plano.Unmatched.Select(n => Tr.F("Aviso: {0}.", n)));

        linhas.Insert(0, Tr.F("{0} string(s) em {1} grupo(s) de mesas; {2} mesa(s) sem tipo; {3} grupo(s) não regerado(s).",
            plano.StringCount, plano.Groups.Count, plano.Unmatched.Count, plano.Skipped.Count));

        return new Relatorio(linhas, plano.StringCount, plano.Groups.Count, plano.Unmatched.Count, plano.Skipped.Count, plano.ReplacedCount);
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
