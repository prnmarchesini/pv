using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A configuração da numeração das strings gravada no desenho (elétrica,
/// etapa 15), no dicionário do plugin: a composição da tag (modelo livre,
/// fundo e moldura) na chave "NUMERACAO"; o sentido da usina e os blocos (na ordem da lista) na chave
/// "NUMERACAO_VARREDURA".
/// </summary>
internal static class NumeracaoStore
{
    private const string ChaveDoEsquema = "NUMERACAO";
    private static readonly string OQueEsquema = Tr.N("da composição da tag");

    private const string ChaveDaVarredura = "NUMERACAO_VARREDURA";
    private const int VersaoDaVarredura = ScanRow.Version;
    private static readonly string OQueVarredura = Tr.N("da varredura das strings");

    /// <summary>
    /// A composição gravada, ou a padrão (T{T}.I{I}.S{S}) se não há nenhuma; o
    /// problema do registro, se havia. Formato 2 (05/10/2026): o modelo livre,
    /// o fundo e a moldura. O 1 (trafo sim/não, três prefixos e o separador)
    /// continua sendo lido e vira o modelo que dá as mesmas tags, sem fundo
    /// nem moldura. Grava sempre o 2.
    /// </summary>
    internal static (TagScheme Esquema, string? Problema) Esquema(Database database)
    {
        var lido = PluginRecords.Version(database, ChaveDoEsquema) == TagScheme.LegacyVersion
            ? PluginRecords.Load<TagScheme>(database, ChaveDoEsquema, TagScheme.LegacyVersion, TagScheme.LegacyFieldCount, TagScheme.ParseLegacy, OQueEsquema)
            : PluginRecords.Load<TagScheme>(database, ChaveDoEsquema, TagScheme.Version, TagScheme.FieldCount, TagScheme.Parse, OQueEsquema);
        return (lido.Items.Count > 0 ? lido.Items[0] : TagScheme.Default, lido.Problem);
    }

    /// <summary>Grava a composição (formato 2). Recusa o modelo que não vale nem para um inversor só.</summary>
    internal static void GravarEsquema(Database database, TagScheme esquema)
    {
        if (esquema.Problem() is { } problema) throw new ArgumentException(problema, nameof(esquema));
        PluginRecords.Save(database, ChaveDoEsquema, TagScheme.Version, TagScheme.FieldCount, [esquema], e => e.ToFields());
    }

    /// <summary>Só para o nível 2: grava a composição no formato 1 (o de antes de 05/10/2026), como um desenho antigo.</summary>
    internal static void GravarEsquemaAntigo(Database database, LegacyTagScheme antiga)
    {
        if (antiga.Problem() is { } problema) throw new ArgumentException(problema, nameof(antiga));
        PluginRecords.Save(database, ChaveDoEsquema, TagScheme.LegacyVersion, TagScheme.LegacyFieldCount, [antiga], e => e.ToFields());
    }

    /// <summary>A versão gravada do registro da composição (null: não há).</summary>
    internal static int? VersaoDoEsquema(Database database) => PluginRecords.Version(database, ChaveDoEsquema);

    /// <summary>
    /// A varredura gravada (sentidos da usina e blocos), ou a da esquerda para
    /// a direita sem blocos; o problema do registro, se havia. Formato 2
    /// (05/10/2026) tem o sentido na faixa; o 1 (sem ele) continua sendo lido,
    /// com o sentido na faixa de antes. Grava sempre o 2.
    /// </summary>
    internal static (ScanSetup Varredura, string? Problema) Varredura(Database database)
    {
        var lido = PluginRecords.Version(database, ChaveDaVarredura) == ScanRow.LegacyVersion
            ? PluginRecords.Load<ScanRow>(database, ChaveDaVarredura, ScanRow.LegacyVersion, ScanRow.LegacyFieldCount, ScanRow.ParseLegacy, OQueVarredura)
            : PluginRecords.Load<ScanRow>(database, ChaveDaVarredura, VersaoDaVarredura, ScanRow.FieldCount, ScanRow.Parse, OQueVarredura);
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
