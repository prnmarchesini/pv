using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// A troca de mesa olhando o terreno (Renan, 02/10/2026, com print: mesas de
/// 28 com o morro atravessando os módulos — "será que a de 14 não passaria
/// aí?"; "o motor não faz esse tipo de análise, só usa 14 ou 28 pelo
/// comprimento perante a lateral, a linha da área").
///
/// A distribuição põe as mesas pela prioridade da lista (a primeira sempre
/// que couber). Depois de a fileira ser resolvida no terreno, a mesa que
/// "não dá" (módulo enterrado: ponta baixa abaixo do terreno) é testada
/// trocada pelo tipo SEGUINTE da lista que seja mais curto, encostada no
/// começo ou no fim do lugar dela; a fileira inteira é resolvida de novo (a
/// junta com as vizinhas continua fechada, regra 6). A troca só fica quando
/// diminuem as mesas que não dão (02/10/2026: "o primeiro da lista vai ser
/// a prioridade, sempre vai tentar encaixar o primeiro, se não der, aí o
/// segundo"): mesa que dá nunca é trocada.
/// </summary>
public static class TerrainFit
{
    /// <summary>Uma mesa na mesma posição, com outro tipo, encostada no começo ou no fim do lugar dela.</summary>
    public static PlacedTable Replace(PlacedTable celula, TableFootprint tipo, int indice, bool noFim)
    {
        ArgumentNullException.ThrowIfNull(celula);
        ArgumentNullException.ThrowIfNull(tipo);

        var c = celula.Corners;
        var comprimento = Math.Sqrt(Math.Pow(c[1].X - c[0].X, 2) + Math.Pow(c[1].Y - c[0].Y, 2));
        var fundo = Math.Sqrt(Math.Pow(c[3].X - c[0].X, 2) + Math.Pow(c[3].Y - c[0].Y, 2));
        if (comprimento < 1e-9 || fundo < 1e-9) return celula;

        var direcao = new Point3((c[1].X - c[0].X) / comprimento, (c[1].Y - c[0].Y) / comprimento, 0);
        var normal = new Point3((c[3].X - c[0].X) / fundo, (c[3].Y - c[0].Y) / fundo, 0);
        var recuo = noFim ? celula.Length - tipo.Length : 0;

        var origem = new Point3(c[0].X + direcao.X * recuo, c[0].Y + direcao.Y * recuo, 0);
        var fim = new Point3(origem.X + direcao.X * tipo.Length, origem.Y + direcao.Y * tipo.Length, 0);
        var oposto = new Point3(fim.X + normal.X * tipo.PlanDepth, fim.Y + normal.Y * tipo.PlanDepth, 0);
        var atras = new Point3(origem.X + normal.X * tipo.PlanDepth, origem.Y + normal.Y * tipo.PlanDepth, 0);

        return celula with
        {
            Origin = origem,
            Length = tipo.Length,
            PlanDepth = tipo.PlanDepth,
            Corners = [origem, fim, oposto, atras],
            Kind = indice,
        };
    }

    /// <summary>Quantos módulos da mesa ficaram enterrados: colunas com a ponta baixa abaixo do terreno, vezes as fileiras dela.</summary>
    public static int BuriedModules(ProcessedTable mesa, int modulosDaMesa)
    {
        ArgumentNullException.ThrowIfNull(mesa);

        var colunas = mesa.Report.Modules.Count;
        if (colunas == 0) return 0;

        var porColuna = Math.Max(1, modulosDaMesa / colunas);
        return mesa.Report.Modules.Count(m => m.Clearance is < 0) * porColuna;
    }

    /// <summary>Os módulos úteis da fileira: todos menos os enterrados.</summary>
    public static int UsefulModules(ProcessedRow fileira, IReadOnlyList<int> modulosPorTipo)
    {
        ArgumentNullException.ThrowIfNull(fileira);

        return fileira.Tables.Sum(m => modulosPorTipo[m.Cell.Kind] - BuriedModules(m, modulosPorTipo[m.Cell.Kind]));
    }

    /// <summary>Quantas mesas da fileira não dão: têm algum módulo enterrado.</summary>
    public static int FailingTables(ProcessedRow fileira, IReadOnlyList<int> modulosPorTipo)
    {
        ArgumentNullException.ThrowIfNull(fileira);

        return fileira.Tables.Count(m => BuriedModules(m, modulosPorTipo[m.Cell.Kind]) > 0);
    }

    /// <summary>
    /// A fileira com as trocas que diminuem as mesas que não dão. Só troca
    /// uma mesa que não dá, e só pelo tipo seguinte na prioridade que seja
    /// mais curto (cabe no lugar sem invadir a vizinha); entre trocas que
    /// resolvem, fica a do tipo de maior prioridade. Com um tipo só, ou sem
    /// mesa que não dá, devolve a fileira como veio.
    /// </summary>
    public static ProcessedRow Improve(
        ProcessedRow fileira,
        IReadOnlyList<TableFootprint> tipos,
        IReadOnlyList<int> modulosPorTipo,
        Func<PlanRow, ProcessedRow> resolver)
    {
        ArgumentNullException.ThrowIfNull(fileira);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(modulosPorTipo);
        ArgumentNullException.ThrowIfNull(resolver);

        if (tipos.Count < 2) return fileira;

        var atual = fileira;
        var falhas = FailingTables(atual, modulosPorTipo);

        // Cada passada troca no máximo uma mesa; para quando nenhuma troca
        // resolve. O limite é o número de mesas vezes os degraus da lista.
        for (var passada = 0; passada < fileira.Tables.Count * (tipos.Count - 1) && falhas > 0; passada++)
        {
            ProcessedRow? melhor = null;
            var melhorFalhas = falhas;
            var melhorTipo = int.MaxValue;

            for (var i = 0; i < atual.Tables.Count; i++)
            {
                var mesa = atual.Tables[i];
                var tipo = mesa.Cell.Kind;

                if (BuriedModules(mesa, modulosPorTipo[tipo]) == 0) continue;

                for (var outro = tipo + 1; outro < tipos.Count; outro++)
                {
                    if (tipos[outro].Length >= tipos[tipo].Length - 1e-6) continue;

                    foreach (var noFim in new[] { false, true })
                    {
                        var celulas = atual.Tables.Select(t => t.Cell).ToList();
                        celulas[i] = Replace(mesa.Cell, tipos[outro], outro, noFim);

                        var tentativa = resolver(new PlanRow(atual.Row.Number, celulas));
                        var f = FailingTables(tentativa, modulosPorTipo);

                        if (f < melhorFalhas || (f == melhorFalhas && melhor is not null && outro < melhorTipo))
                        {
                            melhorFalhas = f;
                            melhorTipo = outro;
                            melhor = tentativa;
                        }
                    }
                }
            }

            if (melhor is null) break;

            atual = melhor;
            falhas = melhorFalhas;
        }

        return atual;
    }
}
