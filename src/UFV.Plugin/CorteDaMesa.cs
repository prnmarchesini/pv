using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A vista lateral da mesa: o corte na direção da inclinação.
///
/// É o desenho que o Renan fez à mão em 23/09/2026, e usa a nomenclatura dele:
/// <b>M2</b> para os módulos mais o espaçamento, <b>T1</b> para a tesoura,
/// <b>T2</b> para a posição do pilar sobre ela. O que a planta baixa não conta,
/// este conta: onde a tesoura fica em relação ao módulo, quanto sobra de cada
/// lado, e quanto o pilar sobe acima da ponta baixa.
///
/// Não há terreno aqui, e é de propósito. A ponta baixa do módulo é a linha de
/// referência, e o pilar desce até ela e segue tracejado: quanto ele mede de
/// verdade depende do terreno, e o terreno entra na etapa 5. Desenhar um chão
/// qualquer seria inventar um número que parece calculado.
/// </summary>
internal sealed class CorteDaMesa : FrameworkElement
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Brush Fundo = Congelar(new SolidColorBrush(Color.FromRgb(0x1B, 0x20, 0x27)));
    private static readonly Brush Texto = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0xD0, 0xD8)));
    private static readonly Brush Fraco = Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA1)));
    private static readonly Brush Laranja = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C)));
    private static readonly Brush Verde = Congelar(new SolidColorBrush(Color.FromRgb(0x4F, 0xC7, 0x6A)));

    private static readonly Pen Modulo =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xB5))), 9));

    private static readonly Pen Face =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x63, 0xD9, 0xC4))), 2.5));

    private static readonly Pen Tesoura =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x4F, 0xC7, 0x6A))), 4));

    private static readonly Pen Pilar =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C))), 4));

    private static readonly Pen PilarSemFim =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C))), 2)
        {
            DashStyle = new DashStyle([4, 4], 0),
        });

    private static readonly Pen Referencia =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x5A, 0x66, 0x72))), 1)
        {
            DashStyle = new DashStyle([5, 4], 0),
        });

    private static readonly Pen Cota =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA1))), 1));

    private const double Margem = 30;

    private TableGeometry? _mesa;
    private double _tilt;
    private string? _recado;

    /// <summary>Mostra o corte desta mesa, com esta inclinação em radianos.</summary>
    internal void Mostrar(TableGeometry mesa, double tiltRadians)
    {
        _mesa = mesa;
        _tilt = tiltRadians;
        _recado = null;
        InvalidateVisual();
    }

    /// <summary>Mostra um recado no lugar do desenho.</summary>
    internal void Dizer(string recado)
    {
        _mesa = null;
        _recado = recado;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext tela)
    {
        ArgumentNullException.ThrowIfNull(tela);

        tela.DrawRectangle(Fundo, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (_mesa is null)
        {
            if (!string.IsNullOrWhiteSpace(_recado))
                Escrever(tela, _recado, Fraco, 12, new Point(Margem, ActualHeight / 2));

            return;
        }

        if (_mesa.Modules.Count == 0 || _mesa.Depth <= 0) return;

        var m2 = _mesa.Depth;
        var sobra = _mesa.RafterOffset;
        var t1 = m2 - 2 * sobra;
        var pilarEm = _mesa.PillarRow;

        var cos = Math.Cos(_tilt);
        var sen = Math.Sin(_tilt);

        // A subida do pilar acima da ponta baixa: é ela que decide se o pilar
        // cabe, e é o número que ninguém estima de cabeça.
        var subida = pilarEm * sen;
        var alto = m2 * sen;

        // A caixa do desenho vai da ponta baixa até a ponta alta, mais um
        // pedaço abaixo da linha de referência para o pilar descer.
        var largura = Math.Max(0.001, m2 * cos);
        var altura = Math.Max(0.001, alto + Math.Max(subida, 0.3) * 0.9);

        var disponivelX = Math.Max(1, ActualWidth - 2 * Margem - 58);
        var disponivelY = Math.Max(1, ActualHeight - 2 * Margem - 26);
        var k = Math.Min(disponivelX / largura, disponivelY / altura);

        var x0 = Margem + 46;
        var yBaixa = Margem + alto * k;

        // Ponto sobre o plano dos módulos, a distância d da ponta baixa.
        Point No(double d, double recuo = 0) => new(
            x0 + d * cos * k + sen * recuo,
            yBaixa - d * sen * k + cos * recuo);

        // Linha de referência: a altura da ponta baixa do módulo.
        tela.DrawLine(Referencia, new Point(Margem, yBaixa), new Point(ActualWidth - Margem, yBaixa));

        // A tesoura, recuada por baixo dos módulos.
        tela.DrawLine(Tesoura, No(sobra, 11), No(sobra + t1, 11));

        // Os módulos, fileira por fileira, com a face superior destacada.
        foreach (var faixa in Fileiras())
        {
            tela.DrawLine(Modulo, No(faixa.De), No(faixa.Ate));
            tela.DrawLine(Face, No(faixa.De, -7), No(faixa.Ate, -7));
        }

        // O pilar: cheio até a linha de referência, tracejado depois dela,
        // porque o comprimento de verdade só existe com o terreno.
        var encosto = No(pilarEm, 11);

        tela.DrawLine(Pilar, encosto, new Point(encosto.X, yBaixa));
        tela.DrawLine(PilarSemFim, new Point(encosto.X, yBaixa),
            new Point(encosto.X, Math.Min(ActualHeight - 14, yBaixa + 30)));

        Angulo(tela, No(0), k);

        CotaInclinada(tela, No(0, -26), No(m2, -26), $"M2 {Medida(m2)}", Texto);
        CotaInclinada(tela, No(sobra, 30), No(sobra + t1, 30), $"T1 {Medida(t1)}", Verde);
        CotaInclinada(tela, No(sobra, 52), No(pilarEm, 52), $"T2 {Medida(pilarEm - sobra)}", Laranja);

        if (sobra > 0.001)
            CotaInclinada(tela, No(0, 30), No(sobra, 30), Medida(sobra), Fraco);

        CotaVertical(tela, encosto.X + 22, encosto.Y, yBaixa, Medida(subida), Laranja);

        Escrever(
            tela,
            $"o pilar encosta {Medida(subida)} m acima da ponta baixa · "
            + $"tracejado: o resto depende do terreno",
            Fraco,
            10.5,
            new Point(Margem, ActualHeight - 16));
    }

    /// <summary>
    /// As faixas que cada fileira de módulos ocupa ao longo da inclinação.
    ///
    /// Sai da geometria, e não de uma conta repetida aqui: em 2V são duas
    /// faixas separadas pelo espaçamento vertical, e é essa folga no meio que
    /// o corte mostra e a planta não.
    /// </summary>
    private IEnumerable<(double De, double Ate)> Fileiras()
    {
        if (_mesa is null) yield break;

        foreach (var fileira in _mesa.Modules.GroupBy(m => m.Row).OrderBy(g => g.Key))
        {
            var de = fileira.SelectMany(m => m.TopFace).Min(p => p.Y);
            var ate = fileira.SelectMany(m => m.TopFace).Max(p => p.Y);

            yield return (de, ate);
        }
    }

    private void Angulo(DrawingContext tela, Point origem, double k)
    {
        var raio = Math.Min(44, k * 0.9);
        if (raio < 12) return;

        var fim = new Point(origem.X + raio * Math.Cos(_tilt), origem.Y - raio * Math.Sin(_tilt));

        var arco = new StreamGeometry();

        using (var caneta = arco.Open())
        {
            caneta.BeginFigure(new Point(origem.X + raio, origem.Y), false, false);
            caneta.ArcTo(fim, new Size(raio, raio), 0, false, SweepDirection.Counterclockwise, true, false);
        }

        arco.Freeze();
        tela.DrawGeometry(null, Cota, arco);

        var graus = _tilt * 180 / Math.PI;
        Escrever(tela, $"{graus.ToString("0.#", Brasil)}°", Fraco, 11,
            new Point(origem.X + raio + 4, origem.Y - raio * 0.55));
    }

    /// <summary>
    /// Cota paralela à inclinação, com o texto sobre a linha.
    ///
    /// Inclinada, e não na horizontal: a medida é ao longo do plano, e uma
    /// cota horizontal mediria a projeção — 4,50 m no lugar de 4,788, que é
    /// exatamente o tipo de número plausível e errado que este desenho existe
    /// para não deixar passar.
    /// </summary>
    private void CotaInclinada(DrawingContext tela, Point de, Point ate, string texto, Brush cor)
    {
        if (Distancia(de, ate) < 26) return;

        tela.DrawLine(Cota, de, ate);

        var normal = new Vector(-(ate.Y - de.Y), ate.X - de.X);
        if (normal.Length > 0) normal.Normalize();

        tela.DrawLine(Cota, de - normal * 4, de + normal * 4);
        tela.DrawLine(Cota, ate - normal * 4, ate + normal * 4);

        var formatado = Formatar(texto, 10.5, cor);
        var meio = new Point((de.X + ate.X) / 2, (de.Y + ate.Y) / 2);

        tela.DrawRectangle(Fundo, null, new Rect(
            meio.X - formatado.Width / 2 - 3,
            meio.Y - formatado.Height / 2,
            formatado.Width + 6,
            formatado.Height));

        tela.DrawText(formatado, new Point(meio.X - formatado.Width / 2, meio.Y - formatado.Height / 2));
    }

    private void CotaVertical(DrawingContext tela, double x, double de, double ate, string texto, Brush cor)
    {
        if (Math.Abs(ate - de) < 18) return;

        tela.DrawLine(Cota, new Point(x, de), new Point(x, ate));
        tela.DrawLine(Cota, new Point(x - 4, de), new Point(x + 4, de));
        tela.DrawLine(Cota, new Point(x - 4, ate), new Point(x + 4, ate));

        var formatado = Formatar(texto, 10.5, cor);
        tela.DrawText(formatado, new Point(x + 6, (de + ate) / 2 - formatado.Height / 2));
    }

    private void Escrever(DrawingContext tela, string texto, Brush cor, double tamanho, Point onde) =>
        tela.DrawText(Formatar(texto, tamanho, cor), onde);

    private FormattedText Formatar(string texto, double tamanho, Brush cor) =>
        new(
            texto,
            Brasil,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            tamanho,
            cor,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static double Distancia(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string Medida(double valor) => valor.ToString("0.###", Brasil);

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
