using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A planta baixa da mesa, desenhada na janela: módulos, pilares e as sobras
/// das pontas.
///
/// Existe porque o número sozinho não denuncia erro de digitação. Um
/// comprimento de 18,702 m parece certo; a mesa com 14 colunas e uma sobra de
/// 10 cm de cada lado também. Mas 28 módulos em 1V, 2V com o espaçamento
/// vertical trocado pelo horizontal, ou uma sobra de 1 m no lugar de 10 cm —
/// tudo isso muda o desenho na hora, e o olho pega antes da conta.
///
/// Desenhada com <see cref="OnRender"/>, e não com um Canvas cheio de
/// retângulos: são até algumas dezenas de módulos redesenhados a cada tecla, e
/// um desenho imediato não cria nem descarta objeto nenhum.
/// </summary>
internal sealed class PlantaDaMesa : FrameworkElement
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Brush Fundo = Congelar(new SolidColorBrush(Color.FromRgb(0x1B, 0x20, 0x27)));
    private static readonly Brush Modulo = Congelar(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xB5)));
    private static readonly Brush Pilar = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C)));
    private static readonly Brush Texto = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0xD0, 0xD8)));
    private static readonly Brush Fraco = Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA1)));

    private static readonly Pen BordaDoModulo =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x7F, 0xB3, 0xE8))), 0.8));

    private static readonly Pen Estrutura =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xB8))), 1.2)
        {
            DashStyle = new DashStyle([5, 3], 0),
        });

    private static readonly Pen Cota =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x7A))), 1.0));

    private TableGeometry? _mesa;
    private string? _recado;

    /// <summary>Margem em volta do desenho, em pixels.</summary>
    private const double Folga = 34;

    /// <summary>Mostra esta mesa.</summary>
    internal void Mostrar(TableGeometry mesa)
    {
        _mesa = mesa;
        _recado = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Mostra um recado no lugar do desenho.
    ///
    /// Enquanto os campos estão pela metade não há mesa para desenhar, e
    /// deixar o desenho anterior na tela seria pior que apagá-lo: o projetista
    /// olharia uma planta que não corresponde mais ao que ele digitou.
    /// </summary>
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
            if (!string.IsNullOrWhiteSpace(_recado)) Escrever(tela, _recado, Fraco, 12, Centro());
            return;
        }

        if (_mesa.Modules.Count == 0) return;

        // A caixa é a da ESTRUTURA, não a dos módulos: é ela que mostra as
        // sobras das pontas, que são metade do que esta planta existe para
        // conferir.
        var comprimento = _mesa.Length;
        var profundidade = _mesa.Depth;

        if (comprimento <= 0 || profundidade <= 0) return;

        var largura = Math.Max(1, ActualWidth - 2 * Folga);
        var altura = Math.Max(1, ActualHeight - 2 * Folga);
        var escala = Math.Min(largura / comprimento, altura / profundidade);

        var x0 = (ActualWidth - comprimento * escala) / 2;
        var y0 = (ActualHeight - profundidade * escala) / 2;

        double X(double metros) => x0 + metros * escala;
        double Y(double metros) => y0 + (profundidade - metros) * escala;

        tela.DrawRectangle(null, Estrutura,
            new Rect(X(0), Y(profundidade), comprimento * escala, profundidade * escala));

        foreach (var modulo in _mesa.Modules)
        {
            var minX = modulo.TopFace.Min(p => p.X);
            var maxX = modulo.TopFace.Max(p => p.X);
            var minY = modulo.TopFace.Min(p => p.Y);
            var maxY = modulo.TopFace.Max(p => p.Y);

            tela.DrawRectangle(Modulo, BordaDoModulo,
                new Rect(X(minX), Y(maxY), (maxX - minX) * escala, (maxY - minY) * escala));
        }

        foreach (var pilar in _mesa.Pillars)
        {
            var minX = pilar.Footprint.Min(p => p.X);
            var maxX = pilar.Footprint.Max(p => p.X);
            var minY = pilar.Footprint.Min(p => p.Y);
            var maxY = pilar.Footprint.Max(p => p.Y);

            // Um pilar de 15 cm numa mesa de 19 m sairia com meio pixel. O
            // mínimo de três pixels é o que faz a fileira de pilares
            // aparecer, que é o ponto de desenhá-los.
            var largo = Math.Max(3, (maxX - minX) * escala);
            var fundo = Math.Max(3, (maxY - minY) * escala);
            var meioX = (X(minX) + X(maxX)) / 2;
            var meioY = (Y(minY) + Y(maxY)) / 2;

            tela.DrawRectangle(Pilar, null,
                new Rect(meioX - largo / 2, meioY - fundo / 2, largo, fundo));
        }

        CotaHorizontal(tela, X(0), X(comprimento), Y(0) + 20, $"{Medida(comprimento)} m");

        Escrever(
            tela,
            $"{_mesa.Modules.Count} módulos · {_mesa.Pillars.Count} pilares · "
            + $"{Medida(profundidade)} m na inclinação",
            Fraco,
            11,
            new Point(X(0), Y(0) + 34));
    }

    private void CotaHorizontal(DrawingContext tela, double de, double para, double y, string texto)
    {
        tela.DrawLine(Cota, new Point(de, y), new Point(para, y));
        tela.DrawLine(Cota, new Point(de, y - 4), new Point(de, y + 4));
        tela.DrawLine(Cota, new Point(para, y - 4), new Point(para, y + 4));

        var formatado = Formatar(texto, 11, Texto);

        tela.DrawRectangle(Fundo, null, new Rect(
            (de + para) / 2 - formatado.Width / 2 - 4,
            y - formatado.Height / 2,
            formatado.Width + 8,
            formatado.Height));

        tela.DrawText(formatado, new Point((de + para) / 2 - formatado.Width / 2, y - formatado.Height / 2));
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

    private Point Centro() => new(Folga, ActualHeight / 2);

    private static string Medida(double valor) => valor.ToString("0.###", Brasil);

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
