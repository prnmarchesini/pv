namespace Clivus.Core;

/// <summary>Um inversor na repartição: o peso (o kW do modelo) e quantas entradas ele tem.</summary>
public sealed record LimitShare(Guid Inverter, double PowerKw, int Inputs);

/// <summary>O que a repartição dá: o limite de cada inversor e as strings que não couberam em entrada nenhuma.</summary>
public sealed record BalancedLimitsResult(IReadOnlyDictionary<Guid, int> Limits, int Leftover);

/// <summary>
/// Repartir as strings da usina nos limites dos inversores (Renan,
/// 07/10/2026: "quero distribuir as strings da forma mais homogênea possível
/// e tenho que ficar adivinhando quantas strings por inversor; quero um botão
/// que distribua os limites ... conforme a potência"). Cada inversor recebe
/// na proporção do kW dele (kW iguais: todos com o mesmo número, no máximo um
/// a mais), sem passar das entradas; o que não cabe vai para os outros. A
/// sobra de arredondamento vai para os de maior resto, na ordem da tabela.
/// Sem kW informado em nenhum, todos pesam igual.
/// </summary>
public static class BalancedLimits
{
    public static BalancedLimitsResult Split(IReadOnlyList<LimitShare> inverters, int strings)
    {
        ArgumentNullException.ThrowIfNull(inverters);

        var limites = inverters.ToDictionary(i => i.Inverter, _ => 0);
        if (inverters.Count == 0 || strings <= 0) return new BalancedLimitsResult(limites, Math.Max(0, strings));

        var algumComKw = inverters.Any(i => i.PowerKw > 0);
        double Peso(LimitShare i) => algumComKw ? Math.Max(0, i.PowerKw) : 1;

        // Quem tem entrada e peso entra; quem bate nas entradas sai com elas, e
        // o resto é repartido de novo entre os que sobraram.
        var ativos = inverters.Where(i => i.Inputs > 0 && Peso(i) > 0).ToList();
        var resto = Math.Min(strings, ativos.Sum(i => i.Inputs));
        var sobra = strings - resto;

        while (ativos.Count > 0)
        {
            var soma = ativos.Sum(Peso);
            var cheios = ativos.Where(i => resto * Peso(i) / soma >= i.Inputs).ToList();
            if (cheios.Count == 0) break;

            foreach (var i in cheios)
            {
                limites[i.Inverter] = i.Inputs;
                resto -= i.Inputs;
                ativos.Remove(i);
            }
        }

        if (ativos.Count > 0 && resto > 0)
        {
            var soma = ativos.Sum(Peso);
            var cotas = ativos.Select((i, ordem) => (i, ordem, cota: resto * Peso(i) / soma)).ToList();
            foreach (var (i, _, cota) in cotas) limites[i.Inverter] = (int)Math.Floor(cota + 1e-9);

            var faltam = resto - cotas.Sum(c => limites[c.i.Inverter]);
            foreach (var (i, _, _) in cotas
                .Where(c => limites[c.i.Inverter] < c.i.Inputs)
                .OrderByDescending(c => Math.Round(c.cota - Math.Floor(c.cota + 1e-9), 9))
                .ThenBy(c => c.ordem)
                .Take(faltam))
                limites[i.Inverter]++;
        }

        return new BalancedLimitsResult(limites, sobra);
    }

    /// <summary>
    /// A soma dos limites da usina (item 5 de 10/10/2026): de cada inversor,
    /// o limite dele (sem limite: todas as entradas do modelo); sem modelo, zero.
    /// </summary>
    public static int Sum(IEnumerable<Inverter> inverters, IEnumerable<InverterModel> models)
    {
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(models);
        var entradas = models.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First().TotalInputs);
        return inverters.DistinctBy(i => i.Id).Sum(i => entradas.TryGetValue(i.Model, out var n) ? i.Limit(n) : 0);
    }

    /// <summary>"Limites: 480 de 480 strings úteis", e o aviso quando não bate (faltam vagas ou sobram). Se bate.</summary>
    public static (string Text, bool Matches) Describe(int limites, int uteis)
    {
        var texto = Tr.F("Limites: {0} de {1} strings úteis", limites, uteis);
        if (limites == uteis) return (texto, true);
        return (limites < uteis
            ? texto + " — " + Tr.F("ATENÇÃO: faltam {0} vaga(s); o Distribuir deixa {0} string(s) sem inversor.", uteis - limites)
            : texto + " — " + Tr.F("ATENÇÃO: sobram {0} vaga(s) nos limites; algum inversor fica abaixo do limite.", limites - uteis), false);
    }

    /// <summary>
    /// O "Repartir pelo kW" pela distribuição de verdade (item 5 de
    /// 10/10/2026: "pegar como seria a distribuição automática ... a
    /// sequência das strings, ver a potência das strings, e aí então atribuir
    /// o limite para cada inversor"). As strings vêm na ordem em que o
    /// Distribuir as toma (a varredura) com a potência de cada uma; os
    /// inversores na ordem da tabela, que é a ordem em que o Distribuir os
    /// enche. Cada inversor leva um trecho seguido da fila, do tamanho que
    /// deixa a potência dele o mais perto possível da parte dele (o kW dele
    /// sobre o kW dos que faltam, vezes a potência que falta), sem passar das
    /// entradas e sem deixar para os de depois mais do que cabe neles. O
    /// limite é o tamanho do trecho: Soltar todas e Distribuir dá exatamente
    /// esses trechos. Sem kW informado em nenhum, todos pesam igual; potência
    /// de string zero ou negativa conta como zero.
    /// </summary>
    public static BalancedLimitsResult SplitInOrder(IReadOnlyList<LimitShare> inverters, IReadOnlyList<double> stringPowers)
    {
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(stringPowers);

        var limites = inverters.ToDictionary(i => i.Inverter, _ => 0);
        var n = stringPowers.Count;
        if (inverters.Count == 0 || n == 0) return new BalancedLimitsResult(limites, n);

        var algumComKw = inverters.Any(i => i.PowerKw > 0);
        double Peso(LimitShare i) => algumComKw ? Math.Max(0, i.PowerKw) : 1;

        // A soma acumulada da potência: o trecho [a, b) vale acumulada[b] - acumulada[a].
        var acumulada = new double[n + 1];
        for (var k = 0; k < n; k++) acumulada[k + 1] = acumulada[k] + Math.Max(0, stringPowers[k]);

        var ativos = inverters.Where(i => i.Inputs > 0 && Peso(i) > 0).DistinctBy(i => i.Inverter).ToList();
        var inicio = 0;
        for (var a = 0; a < ativos.Count && inicio < n; a++)
        {
            var inv = ativos[a];
            var restantes = n - inicio;
            var cabeDepois = ativos.Skip(a + 1).Sum(i => (long)i.Inputs);
            var maximo = Math.Min(inv.Inputs, restantes);
            var minimo = (int)Math.Max(0, Math.Min(maximo, restantes - cabeDepois));

            int leva;
            if (a == ativos.Count - 1)
            {
                leva = maximo;
            }
            else
            {
                var pesoQueFalta = ativos.Skip(a).Sum(Peso);
                var alvo = (acumulada[n] - acumulada[inicio]) * Peso(inv) / pesoQueFalta;

                // Potência toda zero: a conta pela quantidade (o mesmo número para kW iguais).
                if (acumulada[n] - acumulada[inicio] <= 1e-12) alvo = double.NaN;
                var alvoEmStrings = restantes * Peso(inv) / pesoQueFalta;

                leva = minimo;
                var melhor = double.PositiveInfinity;
                for (var c = minimo; c <= maximo; c++)
                {
                    var erro = double.IsNaN(alvo)
                        ? Math.Abs(c - alvoEmStrings)
                        : Math.Abs(acumulada[inicio + c] - acumulada[inicio] - alvo);
                    if (erro < melhor - 1e-9)
                    {
                        melhor = erro;
                        leva = c;
                    }
                }
            }

            limites[inv.Inverter] = leva;
            inicio += leva;
        }

        return new BalancedLimitsResult(limites, n - inicio);
    }
}
