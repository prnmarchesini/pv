namespace Clivus.Core;

/// <summary>
/// Os quantitativos de uma canaleta (29.2): comprimento, concreto e
/// escavação. A seção é vertical (como no desenho e no sólido), então o
/// volume é a área da seção vezes o comprimento em planta.
/// </summary>
/// <param name="Length2D">Comprimento em planta, em m.</param>
/// <param name="Length3D">Comprimento ao longo do fundo, em m.</param>
/// <param name="Concrete">Volume de concreto da parede, em m³.</param>
/// <param name="Excavation">Volume de escavação, em m³: a seção externa mais o aprofundamento (fundo com declividade mínima).</param>
/// <param name="ExtraExcavation">A parte da escavação que vem do aprofundamento, em m³.</param>
public sealed record ChannelQuantities(double Length2D, double Length3D, double Concrete, double Excavation, double ExtraExcavation)
{
    public static ChannelQuantities Of(ChannelProfileResult profile, ChannelSection section)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(section);

        double em3d = 0, extra = 0;
        var larguraExterna = section.Kind == ChannelKind.HalfPipe
            ? 2 * (section.Radius + section.Thickness)
            : section.OuterOutline().Max(p => p.X) * 2;

        for (var i = 1; i < profile.Points.Count; i++)
        {
            var (a, b) = (profile.Points[i - 1], profile.Points[i]);
            var dl = b.Distance - a.Distance;
            var dz = b.Bottom - a.Bottom;
            em3d += Math.Sqrt(dl * dl + dz * dz);

            // Quanto o fundo foi além da altura da seção (só no modo de declividade mínima).
            var a0 = Math.Max(0, a.Depth - section.UsefulHeight);
            var b0 = Math.Max(0, b.Depth - section.UsefulHeight);
            extra += (a0 + b0) / 2 * dl * larguraExterna;
        }

        var comprimento = profile.Length;
        return new ChannelQuantities(comprimento, em3d, section.WallArea * comprimento, section.ExternalArea * comprimento + extra, extra);
    }
}
