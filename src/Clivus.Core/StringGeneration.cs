namespace Clivus.Core;

/// <summary>Uma string a desenhar: o registro (o contrato) e os módulos do campo na ordem elétrica, do + ao −.</summary>
public sealed record PlannedString(ElectricalString String, StringRoute Route, IReadOnlyList<FieldModule> Modules);

/// <summary>
/// Um grupo de mesas vizinhas que casou com um tipo (11.6): as mesas na
/// ordem da fileira, as strings a desenhar e as strings livres que já
/// estavam nessas mesas e saem (regerar por cima não duplica).
/// </summary>
public sealed record GroupPlan(StringType Type, IReadOnlyList<OrderedTable> Tables, IReadOnlyList<PlannedString> Strings, IReadOnlyList<Guid> Replaces)
{
    public string Labels => string.Join(", ", Tables.Select(t => t.Table.Label));
}

/// <summary>
/// O que gerar faz (11.6 a 11.8): os grupos que casaram, as mesas que não
/// casaram com tipo nenhum (com o aviso nominal, regra elétrica 6), os
/// grupos pulados por terem string ligada a inversor, e os recados.
/// </summary>
public sealed record GenerationPlan(
    IReadOnlyList<GroupPlan> Groups,
    IReadOnlyList<string> Unmatched,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Notes,
    IReadOnlyList<Guid> UnmatchedTables)
{
    public int StringCount => Groups.Sum(g => g.Strings.Count);

    public int ReplacedCount => Groups.Sum(g => g.Replaces.Count);
}

/// <summary>Uma string que já está no desenho, com as mesas dos seus módulos.</summary>
public sealed record ExistingString(ElectricalString String, IReadOnlySet<Guid> Tables);

/// <summary>
/// A aba Gerar (11.6): casa cada tipo escolhido com grupos de mesas
/// vizinhas da mesma fileira, de assinatura igual (um tipo de duas mesas
/// de 14 só preenche grupos de duas mesas de 14), e monta as strings de
/// cada grupo pelo traçado do tipo. Determinístico: mesma entrada, mesmo
/// resultado, em qualquer ordem de seleção.
/// </summary>
public static class StringGeneration
{
    /// <summary>
    /// Casa e planeja.
    /// </summary>
    /// <param name="tipos">Os tipos que valem nesta porção (os sem mesas ou sem traçado são deixados de lado, com recado).</param>
    /// <param name="mesas">As mesas selecionadas.</param>
    /// <param name="existentes">As strings que já estão no desenho, para regerar por cima sem duplicar.</param>
    public static GenerationPlan Plan(IReadOnlyList<StringType> tipos, IReadOnlyList<FieldTable> mesas, IReadOnlyList<ExistingString> existentes)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(mesas);
        ArgumentNullException.ThrowIfNull(existentes);

        var recados = new List<string>();
        var semTipo = new List<string>();
        var mesasSemTipo = new List<Guid>();
        var pulados = new List<string>();
        var grupos = new List<GroupPlan>();

        var validos = new List<StringType>();
        foreach (var tipo in tipos)
        {
            if (tipo.CanGenerate) validos.Add(tipo);
            else recados.Add(Tr.F("{0} não tem mesas ou traçado e ficou de fora", tipo.Name));
        }

        if (validos.Count == 0) recados.Add(Tr.T("nenhum tipo de string com traçado para gerar"));

        // Os tipos de mais mesas primeiro (um de duas de 14 antes de um de uma
        // de 14), depois a ordem da biblioteca.
        var candidatos = validos.Select((t, i) => (Tipo: t, Ordem: i))
            .OrderByDescending(x => x.Tipo.Arrangement.Tables.Count).ThenBy(x => x.Ordem)
            .Select(x => x.Tipo).ToList();

        foreach (var tipo in candidatos)
        {
            var sobra = StringRouting.Uncovered(tipo.Arrangement, tipo.Routes);
            if (sobra > 0) recados.Add(Tr.F("{0} deixa {1} módulo(s) sem string em cada grupo", tipo.Name, sobra));
        }

        var boas = new List<FieldTable>();
        foreach (var mesa in mesas.DistinctBy(m => m.Id))
        {
            if (mesa.WhyInvalid() is { } porque)
            {
                semTipo.Add(Tr.F("{0}; não recebe string", porque));
                mesasSemTipo.Add(mesa.Id);
            }
            else
            {
                boas.Add(mesa);
            }
        }

        foreach (var corrida in Corridas(boas, recados))
        {
            var i = 0;
            while (i < corrida.Count)
            {
                var tipo = candidatos.FirstOrDefault(t => Casa(t.Arrangement, corrida, i));
                if (tipo is null)
                {
                    var mesa = corrida[i].Table;
                    semTipo.Add(Tr.F("{0}: mesa de {1} módulos ({2}) sem tipo de string", mesa.Label, mesa.Modules.Count, mesa.Shape.Columns + "x" + mesa.Shape.Rows));
                    mesasSemTipo.Add(mesa.Id);
                    i++;
                    continue;
                }

                var doGrupo = corrida.Skip(i).Take(tipo.Arrangement.Tables.Count).ToList();
                i += doGrupo.Count;

                var ids = doGrupo.Select(o => o.Table.Id).ToHashSet();
                var tocadas = existentes.Where(e => e.Tables.Overlaps(ids)).ToList();
                var rotulos = string.Join(", ", doGrupo.Select(o => o.Table.Label));

                if (tocadas.Any(e => e.String.IsAllocated))
                {
                    pulados.Add(Tr.F("{0}: tem string ligada a inversor; não regerei (desaloque antes para regerar)", rotulos));
                    continue;
                }

                if (tocadas.Any(e => !e.Tables.IsSubsetOf(ids)))
                {
                    pulados.Add(Tr.F("{0}: tem string que passa para mesa de fora do grupo; não regerei", rotulos));
                    continue;
                }

                grupos.Add(new GroupPlan(tipo, doGrupo, Strings(tipo, doGrupo), tocadas.Select(e => e.String.Id).ToList()));
            }
        }

        return new GenerationPlan(grupos, semTipo, pulados, recados, mesasSemTipo);
    }

    /// <summary>
    /// As corridas de mesas vizinhas: mesmas fileira e reta, em ordem ao
    /// longo dela, números de letreiro seguidos (F3.4 e F3.5; uma mesa
    /// fora da seleção no meio quebra a corrida). Letreiro ilegível, ou
    /// fileira que não fecha numa reta (letreiros repetidos), vira corrida de
    /// uma mesa só.
    /// </summary>
    internal static List<List<OrderedTable>> Corridas(IReadOnlyList<FieldTable> mesas, List<string> recados)
    {
        var corridas = new List<List<OrderedTable>>();

        var porFileira = mesas
            .GroupBy(m => TableCells.TryParseLabel(m.Label, out var f, out _) ? f : -1)
            .OrderBy(g => g.Key);

        foreach (var fileira in porFileira)
        {
            var lista = fileira.OrderBy(m => StringFieldTables.ChaveDoLetreiro(m.Label)).ThenBy(m => m.Label, StringComparer.Ordinal).ThenBy(m => m.Id).ToList();
            string? porque = null;
            var ordem = fileira.Key > 0 ? StringFieldTables.Order(lista, out porque) : null;

            if (ordem is null)
            {
                // Nunca calado: a causa vai junto (cada mesa fica sozinha).
                recados.Add(fileira.Key > 0
                    ? Tr.F("as mesas da fileira F{0} não fecham numa reta ({1}); cada uma foi tratada sozinha", fileira.Key, porque ?? string.Empty)
                    : Tr.F("letreiro ilegível em {0}; cada mesa foi tratada sozinha", string.Join(", ", lista.Select(m => m.Label))));
                foreach (var m in lista)
                    corridas.Add([.. StringFieldTables.Order([m], out _)!]);
                continue;
            }

            var atual = new List<OrderedTable> { ordem[0] };
            for (var k = 1; k < ordem.Count; k++)
            {
                var a = StringFieldTables.ChaveDoLetreiro(ordem[k - 1].Table.Label).Item2;
                var b = StringFieldTables.ChaveDoLetreiro(ordem[k].Table.Label).Item2;

                if (Math.Abs(a - b) <= 1)
                {
                    atual.Add(ordem[k]);
                }
                else
                {
                    corridas.Add(atual);
                    atual = [ordem[k]];
                }
            }

            corridas.Add(atual);
        }

        return corridas;
    }

    /// <summary>Se as mesas da corrida a partir de i têm a assinatura do tipo, mesa a mesa, na ordem.</summary>
    private static bool Casa(StringArrangement arranjo, IReadOnlyList<OrderedTable> corrida, int i)
    {
        var n = arranjo.Tables.Count;
        if (i + n > corrida.Count) return false;

        for (var k = 0; k < n; k++)
            if (corrida[i + k].Table.Shape != arranjo.Tables[k]) return false;

        return true;
    }

    /// <summary>As strings do grupo pelo traçado do tipo: cada célula do cartesiano vira o módulo do campo.</summary>
    private static List<PlannedString> Strings(StringType tipo, IReadOnlyList<OrderedTable> grupo)
    {
        var lista = new List<PlannedString>();

        foreach (var rota in tipo.Routes)
        {
            var modulos = rota.Cells.Select(c =>
            {
                var o = grupo[c.Table];
                var (coluna, fileira) = o.FromSketch(c.Column, c.Row);
                return o.Table.ModuleAt(coluna, fileira)!;
            }).ToList();

            lista.Add(new PlannedString(new ElectricalString(Guid.NewGuid(), tipo.Id, modulos.Select(m => m.Id).ToList(), Guid.Empty, string.Empty), rota, modulos));
        }

        return lista;
    }
}

/// <summary>
/// O sinal (+ ou −) desenhado na ponta de uma string (11.7): o GUID da
/// string e qual ponta. Regerar a string leva os sinais dela junto.
/// </summary>
public sealed record StringSign(Guid String, bool Positive)
{
    public const string Tipo = "SinalString";
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [String.ToString("D"), Positive ? "+" : "-"];

    public static StringSign? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var s) && s != Guid.Empty && (c[1] == "+" || c[1] == "-") ? new StringSign(s, c[1] == "+") : null;
}
