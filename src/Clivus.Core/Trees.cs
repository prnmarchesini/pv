using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// As medidas de uma árvore como objeto de sombra (9.4). Renan, 03/10/2026:
/// "tamanho do tronco e sua largura, e a copa da árvore, altura e largura,
/// considerando que ela é um cilindro, aí fica tipo um pirulito". O tronco
/// vai do chão até <see cref="TrunkHeight"/>; a copa começa onde o tronco
/// acaba e sobe <see cref="CrownHeight"/>. Larguras são diâmetros, em metro.
/// </summary>
public sealed record TreeSpec(double TrunkHeight, double TrunkWidth, double CrownHeight, double CrownWidth)
{
    /// <summary>A árvore de partida: tronco de 3 m por 0,4 m, copa de 5 m por 6 m.</summary>
    public static readonly TreeSpec Default = new(3.0, 0.4, 5.0, 6.0);

    /// <summary>A altura total, do chão ao topo da copa.</summary>
    public double TotalHeight => TrunkHeight + CrownHeight;

    /// <summary>O que está errado nas medidas, ou null se servem.</summary>
    public string? WhyInvalid()
    {
        static bool Medida(double v, double max) => double.IsFinite(v) && v > 0 && v <= max;

        if (!Medida(TrunkHeight, 100)) return "a altura do tronco precisa ser maior que zero e até 100 m";
        if (!Medida(TrunkWidth, 20)) return "a largura do tronco precisa ser maior que zero e até 20 m";
        if (!Medida(CrownHeight, 100)) return "a altura da copa precisa ser maior que zero e até 100 m";
        if (!Medida(CrownWidth, 60)) return "a largura da copa precisa ser maior que zero e até 60 m";
        return null;
    }

    /// <summary>"tronco 3 × 0,4 m, copa 5 × 6 m (altura × largura)".</summary>
    public string Describe()
    {
        var br = CultureInfo.GetCultureInfo("pt-BR");
        return $"tronco {TrunkHeight.ToString("0.##", br)} × {TrunkWidth.ToString("0.##", br)} m, "
            + $"copa {CrownHeight.ToString("0.##", br)} × {CrownWidth.ToString("0.##", br)} m (altura × largura)";
    }

    /// <summary>Os dois cilindros da árvore com o pé em (x, y, chão).</summary>
    public IReadOnlyList<ShadowCylinder> Cylinders(double x, double y, double ground) =>
    [
        new ShadowCylinder(x, y, TrunkWidth / 2, ground, ground + TrunkHeight),
        new ShadowCylinder(x, y, CrownWidth / 2, ground + TrunkHeight, ground + TotalHeight),
    ];
}

/// <summary>A identidade de uma árvore no desenho (XData do bloco): GUID e medidas.</summary>
public sealed record TreeIdentity(Guid Id, TreeSpec Spec)
{
    /// <summary>O tipo gravado no XData.</summary>
    public const string Tipo = "Arvore";

    /// <summary>Se a identidade serve: GUID e medidas.</summary>
    public bool IsValid => Id != Guid.Empty && Spec.WhyInvalid() is null;
}

/// <summary>Um cilindro vertical que faz sombra: centro em planta, raio, base e topo (cotas).</summary>
public sealed record ShadowCylinder(double X, double Y, double Radius, double Bottom, double Top);
