using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A configuração da numeração das strings gravada no desenho (elétrica,
/// etapa 15), no dicionário do plugin: a composição da tag na chave
/// "NUMERACAO"; o sentido da usina e os blocos (na ordem da lista) na chave
/// "NUMERACAO_VARREDURA".
/// </summary>
internal static class NumeracaoStore
{
    private const string ChaveDoEsquema = "NUMERACAO";
    private const int VersaoDoEsquema = 1;
    private static readonly string OQueEsquema = Tr.N("da composição da tag");

    private const string ChaveDaVarredura = "NUMERACAO_VARREDURA";
    private const int VersaoDaVarredura = 1;
    private static readonly string OQueVarredura = Tr.N("da varredura das strings");

    /// <summary>A composição gravada, ou a padrão (T1.I1.S1) se não há nenhuma; o problema do registro, se havia.</summary>
    internal static (TagScheme Esquema, string? Problema) Esquema(Database database)
    {
        var lido = PluginRecords.Load<TagScheme>(database, ChaveDoEsquema, VersaoDoEsquema, TagScheme.FieldCount, TagScheme.Parse, OQueEsquema);
        return (lido.Items.Count > 0 ? lido.Items[0] : TagScheme.Default, lido.Problem);
    }

    internal static void GravarEsquema(Database database, TagScheme esquema)
    {
        if (esquema.Problem() is { } problema) throw new ArgumentException(problema, nameof(esquema));
        PluginRecords.Save(database, ChaveDoEsquema, VersaoDoEsquema, TagScheme.FieldCount, [esquema], e => e.ToFields());
    }

    /// <summary>A varredura gravada (sentido da usina e blocos), ou a da esquerda para a direita sem blocos; o problema do registro, se havia.</summary>
    internal static (ScanSetup Varredura, string? Problema) Varredura(Database database)
    {
        var lido = PluginRecords.Load<ScanRow>(database, ChaveDaVarredura, VersaoDaVarredura, ScanRow.FieldCount, ScanRow.Parse, OQueVarredura);
        var (varredura, perdidas) = ScanSetup.FromRows(lido.Items);

        var problema = lido.Problem;
        if (perdidas > 0)
        {
            var frase = Tr.F("{0} linha(s) da varredura sem bloco ou repetidas foram ignoradas", perdidas);
            problema = problema is null ? frase : problema + "; " + frase;
        }

        return (varredura, problema);
    }

    internal static void GravarVarredura(Database database, ScanSetup varredura) =>
        PluginRecords.Save(database, ChaveDaVarredura, VersaoDaVarredura, ScanRow.FieldCount, varredura.ToRows(), r => r.ToFields());

    /// <summary>Lê a varredura, aplica a mudança e grava; devolve o problema do registro (se havia).</summary>
    internal static string? MudarVarredura(Database database, Action<ScanSetup> mudanca)
    {
        var (varredura, problema) = Varredura(database);
        mudanca(varredura);
        GravarVarredura(database, varredura);
        return problema;
    }
}
