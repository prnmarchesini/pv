using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O corte do pórtico do carport na janela (35.2): o que a
/// <see cref="CarportGeometry"/> monta em chão plano, de lado, com a água
/// caindo para a direita. As cotas são as que o projetista confere de olho:
/// altura livre, ponto mais baixo sobre o chão e a cobertura.
/// </summary>
internal sealed class CorteDoCarport : FrameworkElement
{
    private static CultureInfo Cultura => Tr.Culture;

    private static readonly Brush Fundo = Congelar(new SolidColorBrush(Color.FromRgb(0x1B, 0x20, 0x27)));
    private static readonly Brush Texto = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0xD0, 0xD8)));
    private static readonly Brush Fraco = Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA1)));
    private static readonly Brush Laranja = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C)));
    private static readonly Brush Verde = Congelar(new SolidColorBrush(Color.FromRgb(0x4F, 0xC7, 0x6A)));
    private static readonly Brush Aco = Congelar(new SolidColorBrush(Color.FromRgb(0x9E, 0xA8, 0xB3)));

    private static readonly Pen Modulo = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xB5))), 6));
    private static readonly Pen Face = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x63, 0xD9, 0xC4))), 1.5));
    private static readonly Pen Solo = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x9C, 0x7A, 0x52))), 1.5));
    private static readonly Pen Cota = Congelar(new Pen(Fraco, 1));

    private const double Margem = 34;

    private CarportProfile? _perfil;
    private CarportPortal? _portico;
    private CarportBay? _vao;
    private string? _recado;

    /// <summary>Mostra o corte deste carport (o pórtico em chão plano) ou o motivo de não sair.</summary>
    internal void Mostrar(CarportProfile perfil)
    {
        _perfil = perfil;
        _recado = null;
        _portico = null;
        _vao = null;

        // A fila em +X, a água para a direita: a descida em planta é -Y.
        var de = CarportGeometry.Portal(perfil, 0, 0, 1, 0, caiADireita: true, (_, _) => 0, out var motivo);
        var ate = de is null ? null : CarportGeometry.Portal(perfil, perfil.Array.BayWidth, 0, 1, 0, true, (_, _) => 0, out motivo);
        if (de is null || ate is null)
        {
            _recado = motivo;
        }
        else
        {
            _portico = de;
            _vao = CarportGeometry.Bay(perfil, 0, de, ate);
        }

        InvalidateVisual();
    }

    /// <summary>Mostra um recado no lugar do desenho.</summary>
    internal void Dizer(string recado)
    {
        _portico = null;
        _vao = null;
        _recado = recado;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext tela)
    {
        ArgumentNullException.ThrowIfNull(tela);
        tela.DrawRectangle(Fundo, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (_portico is null || _vao is null || _perfil is null)
        {
            if (!string.IsNullOrWhiteSpace(_recado)) Escrever(tela, _recado, Laranja, 12, new Point(Margem, ActualHeight / 2), ActualWidth - 2 * Margem);
            return;
        }

        var p = _portico;
        var f = _perfil.Frame;

        // De lado: horizontal = -Y (o lado baixo à direita), vertical = Z.
        static (double H, double V) Lado(Point3 q) => (-q.Y, q.Z);

        var modulos = _vao.Modules.Where(m => m.Column == 0).ToList();
        var pontos = p.Bars.SelectMany(b => new[] { Lado(b.Start), Lado(b.End) })
            .Concat(modulos.SelectMany(m => m.Corners.Select(Lado)))
            .Concat(p.Blocks.Select(b => Lado(b.BottomCenter)))
            .Append((H: 0.0, V: 0.0))
            .ToList();

        var hMin = pontos.Min(q => q.H) - 0.4;
        var hMax = pontos.Max(q => q.H) + 0.4;
        var vMax = pontos.Max(q => q.V) + 0.2;
        var vMin = -0.15;

        var k = Math.Min((ActualWidth - 2 * Margem - 60) / (hMax - hMin), (ActualHeight - 2 * Margem - 20) / (vMax - vMin));
        if (!(k > 0) || !double.IsFinite(k)) return;

        var x0 = Margem + 30;
        var yChao = Margem + vMax * k;
        Point Tela((double H, double V) q) => new(x0 + (q.H - hMin) * k, yChao - q.V * k);
        Point T(Point3 q) => Tela(Lado(q));

        // O chão.
        tela.DrawLine(Solo, new Point(Margem, yChao), new Point(ActualWidth - Margem, yChao));
        for (var x = Margem + 4; x < ActualWidth - Margem; x += 9)
            tela.DrawLine(Solo, new Point(x, yChao), new Point(x - 6, yChao + 6));

        // Os blocos e as chapas.
        foreach (var b in p.Blocks)
        {
            var meio = T(b.BottomCenter);
            var largura = b.Size * k;
            tela.DrawRectangle(Aco, null, new Rect(meio.X - largura / 2, meio.Y - b.Height * k, largura, Math.Max(1, b.Height * k)));
        }

        // As barras do pórtico, na espessura da seção.
        foreach (var b in p.Bars)
        {
            var caneta = new Pen(b.Part == CarportPart.Brace ? Aco : Laranja, Math.Max(2, b.Section.Depth * k)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            tela.DrawLine(caneta, T(b.Start), T(b.End));
        }

        // As terças, de topo.
        foreach (var t in _vao.Purlins)
        {
            var meio = T(t.Start);
            var lado = Math.Max(3, t.Section.Depth * k);
            tela.DrawRectangle(Verde, null, new Rect(meio.X - lado / 2, meio.Y - lado / 2, lado, lado));
        }

        // Os módulos: a face de cima (corner 0 é a borda baixa, 3 a alta).
        foreach (var m in modulos)
        {
            tela.DrawLine(Modulo, T(m.Corners[0]), T(m.Corners[3]));
            tela.DrawLine(Face, T(m.Corners[0]), T(m.Corners[3]));
        }

        // Cotas: a altura livre na borda baixa e o ponto mais baixo.
        var bordaBaixa = modulos.Select(m => m.Corners[0]).OrderBy(c => c.Z).First();
        var xBorda = T(bordaBaixa).X + 18;
        var yBorda = yChao - p.LowEdgeZ * k;
        tela.DrawLine(Cota, new Point(xBorda, yChao), new Point(xBorda, yBorda));
        tela.DrawLine(Cota, new Point(xBorda - 5, yBorda), new Point(xBorda + 5, yBorda));
        Escrever(tela, Tr.F("altura livre {0} m", Medida(_perfil.ClearHeight)), Texto, 11, new Point(xBorda + 6, (yChao + yBorda) / 2 - 8));

        var cobertura = _perfil.Array.SlopeLength * Math.Cos(_perfil.TiltRadians);
        Escrever(tela,
            Tr.F("{0}° · cobertura {1} m em planta · ponto mais baixo {2} m do chão · bases a {3} m", Medida(_perfil.TiltDegrees), Medida(cobertura), Medida(p.LowestAboveGround), Medida(f.PillarBaseSpacing)),
            Fraco, 10.5, new Point(Margem, ActualHeight - 18), ActualWidth - 2 * Margem);
        Escrever(tela, Tr.T("lado alto"), Fraco, 10, new Point(Margem, Margem - 22));
        Escrever(tela, Tr.T("lado baixo (a água cai para cá)"), Fraco, 10, new Point(ActualWidth - Margem - 170, Margem - 22));
    }

    private void Escrever(DrawingContext tela, string texto, Brush cor, double tamanho, Point onde, double largura = 0)
    {
        var formatado = new FormattedText(texto, Cultura, FlowDirection.LeftToRight, new Typeface("Segoe UI"), tamanho, cor,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (largura > 0) formatado.MaxTextWidth = largura;
        tela.DrawText(formatado, onde);
    }

    private static string Medida(double valor) => valor.ToString("0.###", Cultura);

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
