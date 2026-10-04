using System.Globalization;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O que o ajuste das pontas produziu.</summary>
/// <param name="Row">A mesa processada com as cotas novas (uma fileira de uma mesa).</param>
/// <param name="FirstLowEdge">A altura livre da ponta baixa no primeiro pilar, como ficou.</param>
/// <param name="LastLowEdge">A mesma no último pilar.</param>
/// <param name="Warnings">O que o projetista precisa saber (declividade acima do limite, módulos fora da faixa).</param>
public sealed record ManualEndsResult(ProcessedRow Row, double FirstLowEdge, double LastLowEdge, IReadOnlyList<string> Warnings);

/// <summary>
/// As alturas das pontas escolhidas pelo projetista (Renan, 27/09/2026:
/// "quando o sistema insiste em fazer errado, eu quero ter a liberdade de
/// escolher a altura de cada ponta da mesa... redesenha a mesa com isso").
///
/// A "ponta" é a que ele vê na tela: a altura livre da ponta baixa (PB) no
/// primeiro e no último pilar. Uma ponta sem valor fica TRAVADA na altura
/// que tem hoje ("tem uma ponta que está boa, aí eu quero mudar a outra").
///
/// A mesa é rígida (regra sagrada 2): a cota da ponta baixa é linear na
/// estação, então duas alturas em duas estações dão as duas cotas. Só que
/// o terreno sob cada pilar muda com o giro (o pé anda em planta), então a
/// conta se refaz algumas vezes até a altura medida bater com a pedida, em
/// dois décimos de milímetro. Toda cota sai do terreno (regra sagrada 5): a pedida é altura
/// sobre o chão, nunca Z de clique.
/// </summary>
public static class ManualEnds
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A diferença aceita entre a altura pedida e a medida, em metro.</summary>
    private const double Folga = 0.0002;

    private const int Tentativas = 12;

    /// <summary>A nota que a mesa carrega quando as pontas foram escolhidas à mão.</summary>
    public const string Note = "alturas das pontas definidas à mão";

    /// <summary>
    /// Refaz a mesa com as alturas pedidas.
    /// </summary>
    /// <param name="cell">A célula em planta.</param>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="tiltRadians">A inclinação transversal.</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="settings">Configuração e regras de análise.</param>
    /// <param name="currentStart">A cota da ponta baixa hoje, na estação zero.</param>
    /// <param name="currentEnd">A cota hoje, na estação final.</param>
    /// <param name="firstLowEdge">A altura livre pedida no primeiro pilar, ou null para travar.</param>
    /// <param name="lastLowEdge">A altura livre pedida no último pilar, ou null para travar.</param>
    /// <exception cref="ArgumentOutOfRangeException">Altura pedida que não é número, ou fora de ±20 m.</exception>
    /// <exception cref="InvalidOperationException">Pilar da ponta sem terreno: não há altura a medir.</exception>
    public static ManualEndsResult Apply(
        PlacedTable cell,
        TableGeometry geometry,
        double tiltRadians,
        Tin terrain,
        ProjectSettings settings,
        double currentStart,
        double currentEnd,
        double? firstLowEdge,
        double? lastLowEdge)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var pedida in new[] { firstLowEdge, lastLowEdge })
        {
            if (pedida is { } h && (!double.IsFinite(h) || Math.Abs(h) > PillarSizing.MaiorAlturaLivre))
                throw new ArgumentOutOfRangeException(nameof(firstLowEdge), h, "A altura pedida precisa ser um número de até 20 m.");
        }

        if (geometry.Pillars.Count == 0)
            throw new InvalidOperationException("A mesa não tem pilar para medir a altura das pontas.");

        var sA = geometry.Pillars[0].Station;
        var sB = geometry.Pillars[^1].Station;
        var l = geometry.Length;

        // Um pilar só: as duas pontas são o mesmo ponto, e duas alturas
        // diferentes não têm reta que as una.
        if (Math.Abs(sB - sA) < 1e-9 && firstLowEdge is { } a1 && lastLowEdge is { } b1 && Math.Abs(a1 - b1) > Folga)
            throw new InvalidOperationException("A mesa tem um pilar só: as duas pontas têm a mesma altura.");

        var z0 = currentStart;
        var z1 = currentEnd;

        var linha = RowPipeline.ProcessFixed(cell, geometry, tiltRadians, terrain, settings, z0, z1, Note);
        var (cA, cB) = Pontas(linha);

        // A ponta sem valor fica como está: o alvo é o que ela mede agora.
        var alvoA = firstLowEdge ?? cA;
        var alvoB = lastLowEdge ?? cB;

        for (var i = 0; i < Tentativas && (Math.Abs(cA - alvoA) > Folga || Math.Abs(cB - alvoB) > Folga); i++)
        {
            var dA = alvoA - cA;
            var dB = alvoB - cB;

            // Somar dA em sA e dB em sB a uma reta: δ(s) = δ0 + (δ1 − δ0)·s/L.
            // Com um pilar só (sA = sB) a mesa sobe inteira.
            var inclinacao = Math.Abs(sB - sA) > 1e-9 ? (dB - dA) / (sB - sA) : 0;
            var d0 = dA - inclinacao * sA;

            z0 += d0;
            z1 += d0 + inclinacao * l;

            linha = RowPipeline.ProcessFixed(cell, geometry, tiltRadians, terrain, settings, z0, z1, Note);
            (cA, cB) = Pontas(linha);
        }

        // Não chegou: dito, e nada é gravado. Gravar a altura obtida como se
        // fosse a pedida faria o Recalcular perpetuar um número que ninguém
        // escolheu.
        if (Math.Abs(cA - alvoA) > Folga * 10 || Math.Abs(cB - alvoB) > Folga * 10)
        {
            throw new InvalidOperationException(
                $"Não consegui chegar às alturas pedidas ({alvoA.ToString("0.00", Brasil)} e {alvoB.ToString("0.00", Brasil)} m): "
                + $"fiquei em {cA.ToString("0.000", Brasil)} e {cB.ToString("0.000", Brasil)} m. O terreno sob a ponta muda demais com o giro.");
        }

        var avisos = new List<string>();
        var mesa = linha.Tables[0];
        var giro = Math.Abs(mesa.Pillars.LongitudinalTiltRadians);

        if (settings.Configuration.MaxLongitudinalSlope is { } limite && giro > limite + 1e-9)
        {
            avisos.Add(
                $"a mesa ficou com {(giro * 180 / Math.PI).ToString("0.#", Brasil)}° de declividade longitudinal, "
                + $"acima do limite de {(limite * 180 / Math.PI).ToString("0.#", Brasil)}°");
        }

        var enterrados = mesa.Report.Modules.Count(m => m.Clearance is < 0);
        var foraDaFaixa = mesa.Report.ModulesOutsideBand;

        if (mesa.Solved.Marked) avisos.Add($"a mesa ficou MARCADA (não cabe): {mesa.Solved.Reason}");
        if (enterrados > 0) avisos.Add($"{enterrados} módulo(s) com a ponta baixa ENTERRADA");
        if (foraDaFaixa > 0) avisos.Add($"{foraDaFaixa} módulo(s) com a ponta baixa fora da faixa (pintados pela análise)");
        if (mesa.Pillars.ProblemCount > 0) avisos.Add($"{mesa.Pillars.ProblemCount} pilar(es) com problema");

        return new ManualEndsResult(linha, cA, cB, avisos);
    }

    /// <summary>A altura livre da ponta baixa no primeiro e no último pilar.</summary>
    private static (double A, double B) Pontas(ProcessedRow linha)
    {
        var pilares = linha.Tables[0].Pillars.Pillars;

        if (pilares[0].LowEdgeClearance is not { } a || pilares[^1].LowEdgeClearance is not { } b)
            throw new InvalidOperationException("A ponta baixa de um pilar da ponta está fora do terreno: não há altura a medir.");

        return (a, b);
    }
}
