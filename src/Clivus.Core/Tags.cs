using System.Globalization;
using System.Text.RegularExpressions;

namespace Clivus.Core;

/// <summary>As tags do menu Tags (passo 8.14).</summary>
public enum TagKind
{
    /// <summary>"F1", no começo de cada fileira.</summary>
    Row,

    /// <summary>"F1.2", no meio de cada mesa.</summary>
    Table,

    /// <summary>O número do módulo dentro da mesa.</summary>
    Module,

    /// <summary>"S12", no meio de cada string.</summary>
    String,
}

/// <summary>Um módulo para a numeração: de qual mesa, coluna e fileira dentro dela.</summary>
public sealed record ModuleSlot(Guid Table, int Column, int Row);

/// <summary>Uma string: o número, os módulos na ordem, e se fechou o tamanho pedido.</summary>
public sealed record StringAssignment(int Number, IReadOnlyList<ModuleSlot> Modules, bool Complete)
{
    /// <summary>"S12", ou "S12*" quando ficou incompleta.</summary>
    public string Label => $"S{Number}{(Complete ? "" : "*")}";
}

/// <summary>Uma mesa para as tags: o letreiro (F1.2), o GUID e os módulos (coluna, fileira).</summary>
public sealed record TaggedTable(string Label, Guid Id, IReadOnlyList<(int Column, int Row)> Modules);

/// <summary>
/// As tags (passo 8.14, Melhorias.docx, 01/10/2026): "fazer numeração das
/// fileiras, numeração das mesas, numeração dos módulos; visualmente eu
/// preciso mesmo é da numeração das strings".
///
/// A ordem de tudo é a dos letreiros (F1.1, F1.2, ..., F2.1): a mesma do
/// Numerar. Dentro da mesa os módulos andam em serpentina: a fileira de
/// baixo da esquerda para a direita, a de cima voltando, que é como o cabo
/// corre. A string não atravessa mesa: o que sobra numa mesa vira uma
/// string incompleta, marcada com asterisco.
/// </summary>
public static class Tags
{
    private static readonly Regex Letreiro = new(@"^F(\d+)\.(\d+)[a-z]*$", RegexOptions.CultureInvariant);

    /// <summary>A camada das tags do tipo. Só para ligar e desligar: a identidade vai no XData.</summary>
    public static string LayerName(TagKind tipo) => PluginInfo.PrefixoDeDados + "_TAG_" + tipo switch
    {
        TagKind.Row => "FILEIRA",
        TagKind.Table => "MESA",
        TagKind.Module => "MODULO",
        _ => "STRING",
    };

    /// <summary>O nome da tag para o usuário.</summary>
    public static string Name(TagKind tipo) => tipo switch
    {
        TagKind.Row => Tr.T("fileiras"),
        TagKind.Table => Tr.T("mesas"),
        TagKind.Module => Tr.T("módulos"),
        _ => Tr.T("strings"),
    };

    /// <summary>A fileira e o número de um letreiro "F3.12".</summary>
    public static bool TryParseLabel(string? letreiro, out int fileira, out int numero)
    {
        fileira = numero = 0;
        if (letreiro is null) return false;

        var m = Letreiro.Match(letreiro.Trim());
        return m.Success
            && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out fileira)
            && int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out numero);
    }

    /// <summary>As mesas na ordem dos letreiros; letreiro que não é F#.# vai para o fim, pela ordem do texto.</summary>
    public static IReadOnlyList<TaggedTable> InOrder(IEnumerable<TaggedTable> mesas)
    {
        ArgumentNullException.ThrowIfNull(mesas);

        return mesas
            .Select(m => (Mesa: m, Ok: TryParseLabel(m.Label, out var f, out var n), F: f, N: n))
            .OrderBy(x => x.Ok ? 0 : 1)
            .ThenBy(x => x.F)
            .ThenBy(x => x.N)
            .ThenBy(x => x.Mesa.Label, StringComparer.Ordinal)
            .Select(x => x.Mesa)
            .ToList();
    }

    /// <summary>
    /// Os módulos da mesa na ordem da serpentina: fileira 0 com a coluna
    /// subindo, fileira 1 com a coluna descendo, e assim por diante.
    /// </summary>
    public static IReadOnlyList<(int Column, int Row)> Serpentine(IEnumerable<(int Column, int Row)> modulos)
    {
        ArgumentNullException.ThrowIfNull(modulos);

        return modulos
            .Distinct()
            .OrderBy(m => m.Row)
            .ThenBy(m => m.Row % 2 == 0 ? m.Column : -m.Column)
            .ToList();
    }

    /// <summary>As fileiras ("F1", "F2"...) e a primeira mesa de cada uma.</summary>
    public static IReadOnlyList<(string Label, TaggedTable First)> Rows(IEnumerable<TaggedTable> mesas) =>
        InOrder(mesas)
            .Where(m => TryParseLabel(m.Label, out _, out _))
            .GroupBy(m => { TryParseLabel(m.Label, out var f, out _); return f; })
            .Select(g => ($"F{g.Key}", g.First()))
            .ToList();

    /// <summary>As strings da usina, numeradas na ordem das mesas.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o tamanho da string não for positivo.</exception>
    public static IReadOnlyList<StringAssignment> Strings(IEnumerable<TaggedTable> mesas, int modulosPorString)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(modulosPorString, 1);

        var strings = new List<StringAssignment>();

        foreach (var mesa in InOrder(mesas))
        {
            var ordem = Serpentine(mesa.Modules);

            for (var i = 0; i < ordem.Count; i += modulosPorString)
            {
                var pedaco = ordem.Skip(i).Take(modulosPorString).Select(m => new ModuleSlot(mesa.Id, m.Column, m.Row)).ToList();
                strings.Add(new StringAssignment(strings.Count + 1, pedaco, pedaco.Count == modulosPorString));
            }
        }

        return strings;
    }
}
