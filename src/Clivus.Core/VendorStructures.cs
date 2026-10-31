using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// Uma estrutura pronta de fabricante (38.7): a mesa inteira, com as medidas
/// dos desenhos dele, e o que o catálogo cota mas a conta do Clivus não usa,
/// que é a altura livre e os pilares de catálogo.
///
/// Renan, 30/10/2026: "se for um biposte específico do fabricante, crie uma
/// aba de fabricantes e aí um genérico, para a pessoa montar o biposte dela".
/// O nome do fabricante só aparece aqui e na aba Fabricantes; o biposte
/// genérico (<see cref="BifilaReference.Generic"/>) não tem nenhum.
/// </summary>
/// <param name="Manufacturer">O fabricante.</param>
/// <param name="Model">O modelo, como o fabricante escreve.</param>
/// <param name="Profile">
/// A mesa, com o nome que vai para o desenho e, desde o 38.9, o P1 e o P2 do
/// catálogo na estrutura (a altura é dela).
/// </param>
/// <param name="ClearanceUnderModule">A altura livre que o fabricante cota, do chão ao fundo do módulo, na ponta baixa.</param>
/// <param name="CatalogSlope">O declive do chão caindo para trás (fração) para o qual o P2 de catálogo foi feito.</param>
/// <param name="Project">O projeto dos desenhos de onde vieram as medidas.</param>
/// <param name="Revision">A revisão dos desenhos, como o fabricante escreve.</param>
public sealed record VendorStructure(
    string Manufacturer,
    string Model,
    TableProfile Profile,
    double ClearanceUnderModule,
    double CatalogSlope,
    string Project,
    string Revision)
{
    /// <summary>O fabricante e o modelo, como aparecem na aba Fabricantes.</summary>
    public string Name => $"{Manufacturer} {Model}";

    /// <summary>O pilar menor (o dianteiro) do catálogo, inteiro: o P1 da estrutura.</summary>
    public double CatalogP1 => Profile.Frame.Bifila?.FrontPillar ?? 0;

    /// <summary>O pilar maior (o traseiro) do catálogo, inteiro, já com a folga do declive: o P2 da estrutura.</summary>
    public double CatalogP2 => Profile.Frame.Bifila?.RearPillar ?? 0;

    /// <summary>
    /// A ponta baixa do Clivus (a face de cima do módulo) que dá a altura
    /// livre do fabricante, com o módulo e a inclinação dele: a altura livre
    /// mais a espessura do módulo na vertical. É com ela que os pilares saem
    /// os do catálogo no chão plano.
    /// </summary>
    public double LowEdge => LowEdgeFor(Profile);

    /// <summary>
    /// A ponta baixa que dá a altura livre do fabricante com o módulo e a
    /// inclinação desta mesa, que podem não ser os dele: "Usar esta estrutura"
    /// mantém o módulo da janela (revisão do 38.7).
    /// </summary>
    public double LowEdgeFor(TableProfile mesa)
    {
        ArgumentNullException.ThrowIfNull(mesa);
        return ClearanceUnderModule + mesa.Layout.Module.Thickness * Math.Cos(mesa.TiltRadians);
    }

    /// <summary>De onde vieram as medidas, numa frase no idioma da tela.</summary>
    public string DescribeSource() => Tr.F("Desenhos da {0}, projeto {1}, {2}.", Manufacturer, Project, Revision);

    /// <summary>As peças da estrutura, numa frase.</summary>
    public string Describe()
    {
        var mesa = Profile.Layout;
        var estrutura = Profile.Frame;

        var partes = new List<string>
        {
            Tr.F("{0} módulos em {1}, a {2}°",
                mesa.ModuleCount,
                mesa.Arrangement == TableArrangement.DoubleRow ? Tr.T("duas fileiras") : Tr.T("uma fileira"),
                Texto(Profile.TiltDegrees)),
        };

        if (estrutura.Bifila is { } b)
        {
            partes.Add(Tr.F("pórtico de dois pilares a {0} m na horizontal e viga de {1} m",
                Texto(Profile.BifilaHorizontalSpacing ?? 0), Texto(estrutura.RafterLength)));

            partes.Add(b.FrontBraceLength > 0 || b.RearBraceLength > 0
                ? Tr.F("mãos francesas de {0} m (dianteira) e {1} m (traseira)", Texto(b.FrontBraceLength), Texto(b.RearBraceLength))
                : Tr.T("sem mão francesa"));

            partes.Add(Tr.F("{0} terças", 2 * (mesa.Arrangement == TableArrangement.DoubleRow ? 2 : 1)));

            var vaos = string.Join("; ", b.RodSpans.Select(v => v.ToString(CultureInfo.InvariantCulture)));
            partes.Add(b.RodLine switch
            {
                BifilaRodLine.Rear => Tr.F("X de tirantes nos vãos {0} da linha traseira", vaos),
                BifilaRodLine.Front => Tr.F("X de tirantes nos vãos {0} da linha dianteira", vaos),
                BifilaRodLine.Both => Tr.F("X de tirantes nos vãos {0} das duas linhas", vaos),
                _ => Tr.T("sem tirante"),
            });
        }

        return string.Join("; ", partes) + ".";
    }

    /// <summary>A altura livre e os pilares de catálogo, numa frase.</summary>
    public string DescribeCatalog() => Tr.F(
        "Altura livre de {0} m sob o módulo; P1 de {1} m e P2 de {2} m no catálogo (o P2 com folga para o chão caindo {3} % para trás); enterro de {4} m.",
        Texto(ClearanceUnderModule), Texto(CatalogP1), Texto(CatalogP2), Texto(CatalogSlope * 100), Texto(Profile.Frame.MinEmbedment ?? 0));

    private static string Texto(double valor) => valor.ToString("0.###", Tr.Culture);
}

/// <summary>
/// As estruturas de fabricante que o Clivus conhece (38.7). Por enquanto, as
/// duas da Arctech do projeto Aurora (plano/bifila/01-modelo-de-referencia.md).
/// </summary>
public static class VendorStructures
{
    /// <summary>Todas, na ordem em que aparecem na aba.</summary>
    public static IReadOnlyList<VendorStructure> All { get; } =
    [
        Arctech(BifilaVariant.Gc800),
        Arctech(BifilaVariant.Gc1000),
    ];

    private static VendorStructure Arctech(BifilaVariant variante)
    {
        var (modelo, curto, livre, p1, p2, declive, revisao) = variante switch
        {
            BifilaVariant.Gc800 => ("2P14 GC800MM-NS 10", "2P14 GC800", 0.800, 3.059, 3.888, 0.10, "rev. 3, 16/05/2026"),
            BifilaVariant.Gc1000 => ("2P14 GC1000MM-NS 20", "2P14 GC1000", 1.000, 3.259, 4.388, 0.20, "rev. 2, 29/04/2026"),
            _ => throw new ArgumentOutOfRangeException(nameof(variante), variante, null),
        };

        var referencia = BifilaReference.Profile(variante);

        return new VendorStructure(
            "Arctech",
            modelo,
            referencia with
            {
                Name = $"Arctech {curto}",
                Frame = referencia.Frame with { Bifila = referencia.Frame.Bifila! with { FrontPillar = p1, RearPillar = p2 } },
            },
            livre,
            declive,
            "Aurora (Panamá)",
            revisao);
    }
}
