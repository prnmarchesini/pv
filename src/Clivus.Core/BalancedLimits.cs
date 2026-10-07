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
}
