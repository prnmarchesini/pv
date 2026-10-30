using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

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
///
/// O solo que aparece desde o 8.2 é um símbolo, sem cota até a ponta baixa: o
/// que ele mostra é o <b>T3</b>, o mínimo que o pilar entra nele (Melhorias.docx,
/// 01/10/2026: "tem o símbolo do solo, mas não tem a profundidade mínima de
/// enterramento desenhada").
/// </summary>
internal sealed class CorteDaMesa : FrameworkElement
{
    private static CultureInfo Cultura => Tr.Culture;

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

    private static readonly Pen Solo =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x9C, 0x7A, 0x52))), 1.5));

    private static readonly Brush Terra = Congelar(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)));

    private static readonly Pen Cota =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA1))), 1));

    private const double Margem = 30;

    private static readonly Pen MaoFrancesa =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xB8, 0xC4, 0xD0))), 3));

    private TableGeometry? _mesa;
    private double _tilt;
    private double? _enterro;
    private BifilaFrame? _bifila;
    private BasicPillars? _basica;
    private string? _recado;

    /// <summary>
    /// Mostra o corte desta mesa, com esta inclinação em radianos e o enterro
    /// mínimo T3 da estrutura (null: o da configuração do projeto). Com a
    /// parte bifila (38.1), os dois pilares, a viga e as mãos francesas; com a
    /// altura básica (38.7), o chão na ponta baixa mínima do projeto e os
    /// pilares cotados, P1 e P2.
    /// </summary>
    internal void Mostrar(TableGeometry mesa, double tiltRadians, double? enterroMinimo = null, BifilaFrame? bifila = null, BasicPillars? basica = null)
    {
        _mesa = mesa;
        _tilt = tiltRadians;
        _enterro = enterroMinimo;
        _bifila = mesa.RearPillarRow is null ? null : bifila;
        _basica = _bifila is null ? null : basica;
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

        // O solo é símbolo: fica a um vão fixo abaixo da ponta baixa, e o
        // pilar entra nele o T3 (ou um T3 de mostra, sem cota, sem T3 escrito).
        // No biposte com a altura básica (38.7), é o chão plano de verdade: a
        // ponta baixa mínima do projeto abaixo dela, e o enterro que vale.
        var vaoAteOSolo = _basica?.LowEdge ?? 0.35;
        var t3 = _basica?.Embedment ?? _enterro ?? 0.6;

        // A caixa do desenho vai da ponta baixa até a ponta alta, mais o
        // pedaço abaixo dela até o pé do pilar dentro do solo.
        var largura = Math.Max(0.001, m2 * cos);
        var altura = Math.Max(0.001, alto + vaoAteOSolo + t3 + 0.05);

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

        // A tesoura, recuada por baixo dos módulos. Na bifila ela sai no eixo
        // de verdade, debaixo da terça, junto com os pilares.
        if (_bifila is null) tela.DrawLine(Tesoura, No(sobra, 11), No(sobra + t1, 11));

        // Os módulos, fileira por fileira, com a face superior destacada.
        foreach (var faixa in Fileiras())
        {
            tela.DrawLine(Modulo, No(faixa.De), No(faixa.Ate));
            tela.DrawLine(Face, No(faixa.De, -7), No(faixa.Ate, -7));
        }

        // O pilar: cheio até a linha de referência, tracejado até o solo,
        // porque esse trecho só existe com o terreno, e cheio de novo dentro
        // do solo, o T3.
        var encosto = No(pilarEm, 11);
        var ySolo = yBaixa + vaoAteOSolo * k;
        var yPe = ySolo + t3 * k;

        if (_bifila is { } bifila && _mesa.RearPillarRow is { } trasEm)
        {
            DesenharBifila(tela, bifila, pilarEm, trasEm, sobra, t1, x0, yBaixa, k, ySolo, yPe, m2);
            return;
        }

        tela.DrawLine(Pilar, encosto, new Point(encosto.X, yBaixa));
        tela.DrawLine(PilarSemFim, new Point(encosto.X, yBaixa), new Point(encosto.X, ySolo));
        tela.DrawLine(Pilar, new Point(encosto.X, ySolo), new Point(encosto.X, yPe));

        DesenharSolo(tela, ySolo, encosto.X);

        if (_enterro is { } escrito)
            CotaVertical(tela, encosto.X - 26, ySolo, yPe, $"T3 {Medida(escrito)}", Terra, aEsquerda: true);
        else
            Escrever(tela, Tr.T("T3: o da configuração"), Fraco, 10, new Point(encosto.X + 8, (ySolo + yPe) / 2 - 7));

        Angulo(tela, No(0), k);

        CotaInclinada(tela, No(0, -26), No(m2, -26), $"M2 {Medida(m2)}", Texto);
        CotaInclinada(tela, No(sobra, 30), No(sobra + t1, 30), $"T1 {Medida(t1)}", Verde);
        CotaInclinada(tela, No(sobra, 52), No(pilarEm, 52), $"T2 {Medida(pilarEm - sobra)}", Laranja);

        if (sobra > 0.001)
            CotaInclinada(tela, No(0, 30), No(sobra, 30), Medida(sobra), Fraco);

        CotaVertical(tela, encosto.X + 22, encosto.Y, yBaixa, Medida(subida), Laranja);

        Escrever(
            tela,
            Tr.F("o pilar encosta {0} m acima da ponta baixa · tracejado: o resto depende do terreno", Medida(subida)),
            Fraco,
            10.5,
            new Point(Margem, ActualHeight - 16));
    }

    /// <summary>
    /// A bifila (38.1): os dois pilares com o topo abaixo dos módulos, a viga
    /// (o eixo dela, debaixo da terça), as mãos francesas pelo comprimento da
    /// peça e as cotas que o fornecedor dá: T2 e T2t na tesoura e a
    /// distância entre os pilares na horizontal.
    /// </summary>
    private void DesenharBifila(
        DrawingContext tela, BifilaFrame bifila, double frenteEm, double trasEm, double sobra, double t1,
        double x0, double yBaixa, double k, double ySolo, double yPe, double m2)
    {
        var cos = Math.Cos(_tilt);
        var sen = Math.Sin(_tilt);

        // Em metro, com a origem na ponta baixa do módulo: h para o lado alto,
        // v para cima. A tela é só a escala.
        Point Tela(double h, double v) => new(x0 + h * k, yBaixa - v * k);

        var espessura = _mesa!.Modules.Count > 0 ? -_mesa.Modules[0].Solid.Min(p => p.Z) : 0;
        var eixoDaViga = espessura + bifila.Purlin.Depth + bifila.Beam.Depth / 2;

        (double H, double V) NaViga(double d) => (d * cos + sen * eixoDaViga, d * sen - cos * eixoDaViga);

        // A viga.
        var (ha, va) = NaViga(sobra);
        var (hb, vb) = NaViga(sobra + t1);
        tela.DrawLine(Tesoura, Tela(ha, va), Tela(hb, vb));

        // Meia seção do pilar na inclinação, para a cabeça da mão francesa ficar
        // entre a face deste pilar e a do outro, como o Core confere.
        var meiaSecao = _mesa.Pillars.Count > 0
            ? (_mesa.Pillars[0].Footprint.Max(p => p.Y) - _mesa.Pillars[0].Footprint.Min(p => p.Y)) / 2
            : 0;
        var recusadas = new List<string>();

        foreach (var (em, outroEm, comprimento, abaixo, paraTras) in new[]
        {
            (frenteEm, trasEm, bifila.FrontBraceLength, bifila.FrontBraceBelowTop, true),
            (trasEm, frenteEm, bifila.RearBraceLength, bifila.RearBraceBelowTop, false),
        })
        {
            var hp = em * cos;
            var vTopo = em * sen - bifila.PillarTopDrop;
            var topo = Tela(hp, vTopo);

            tela.DrawLine(Pilar, topo, new Point(topo.X, yBaixa));
            tela.DrawLine(PilarSemFim, new Point(topo.X, yBaixa), new Point(topo.X, ySolo));
            tela.DrawLine(Pilar, new Point(topo.X, ySolo), new Point(topo.X, yPe));
            DesenharSolo(tela, ySolo, topo.X);

            // Com a altura básica, o pilar inteiro, do pé ao topo: P1 o
            // dianteiro (o menor), P2 o traseiro (38.7). Sem ela, a subida
            // acima da ponta baixa, como antes.
            if (_basica is { } alturaBasica)
            {
                var inteiro = paraTras ? alturaBasica.P1 : alturaBasica.P2 ?? alturaBasica.P1;
                CotaVertical(tela, topo.X + 22, topo.Y, yPe, $"{(paraTras ? "P1" : "P2")} {Milimetro(inteiro)}", Laranja);
            }
            else
            {
                CotaVertical(tela, topo.X + 22, topo.Y, yBaixa, Medida(vTopo), Laranja);
            }

            if (comprimento <= 0) continue;

            // A mão francesa: do pé no pilar até onde uma peça deste
            // comprimento encosta no eixo da viga, do lado do outro pilar.
            var pv = vTopo - abaixo;
            var (fh, fv) = NaViga(0);
            var dx = fh - hp;
            var dy = fv - pv;
            var qb = 2 * (dx * cos + dy * sen);
            var qc = dx * dx + dy * dy - comprimento * comprimento;
            var delta = qb * qb - 4 * qc;

            var lado = paraTras ? Tr.T("dianteira") : Tr.T("traseira");

            // As mesmas regras do desenho (BifilaStructure): o pé abaixo do
            // fundo da viga, a cabeça dentro da viga e entre os dois pilares.
            // Sem elas, o corte mostrava uma peça que o desenho não põe.
            var vigaNoPilar = NaViga((hp - sen * eixoDaViga) / cos).V - bifila.Beam.Depth / 2;
            if (pv >= vigaNoPilar)
            {
                recusadas.Add(Tr.F("mão francesa {0}: pega acima do fundo da viga", lado));
                continue;
            }

            if (delta < 0)
            {
                recusadas.Add(Tr.F("mão francesa {0}: não alcança a viga", lado));
                continue;
            }

            var d = paraTras ? (-qb + Math.Sqrt(delta)) / 2 : (-qb - Math.Sqrt(delta)) / 2;
            if (d < sobra || d > sobra + t1)
            {
                recusadas.Add(Tr.F("mão francesa {0}: passa da ponta da viga", lado));
                continue;
            }

            var (hc, vc) = NaViga(d);
            var avanco = paraTras ? hc - hp : hp - hc;
            var vaoEntrePilares = Math.Abs(outroEm - em) * cos;

            if (avanco <= meiaSecao || avanco >= vaoEntrePilares - meiaSecao)
            {
                recusadas.Add(Tr.F("mão francesa {0}: encosta fora do vão entre os pilares", lado));
                continue;
            }

            tela.DrawLine(MaoFrancesa, Tela(hp, pv), Tela(hc, vc));
        }

        if (recusadas.Count > 0)
            Escrever(tela, string.Join(" · ", recusadas), Laranja, 10.5, new Point(Margem, ActualHeight - 32));

        Angulo(tela, Tela(0, 0), k);

        Point No(double d, double recuo = 0) => new(x0 + d * cos * k + sen * recuo, yBaixa - d * sen * k + cos * recuo);

        CotaInclinada(tela, No(0, -26), No(m2, -26), $"M2 {Medida(m2)}", Texto);
        CotaInclinada(tela, No(sobra, 36), No(sobra + t1, 36), $"T1 {Medida(t1)}", Verde);
        CotaInclinada(tela, No(sobra, 58), No(frenteEm, 58), $"T2 {Medida(frenteEm - sobra)}", Laranja);
        CotaInclinada(tela, No(sobra, 80), No(trasEm, 80), $"T2t {Medida(trasEm - sobra)}", Laranja);

        var xFrente = Tela(frenteEm * cos, 0).X;
        var xTras = Tela(trasEm * cos, 0).X;
        CotaHorizontal(tela, xFrente, xTras, yPe + 12, Medida((trasEm - frenteEm) * cos), Laranja);

        // O T3 é o da mesa; sem ele, o enterro que vale é o do projeto, e a
        // cota diz isso (revisão do 38.7).
        if (_enterro is { } escrito)
            CotaVertical(tela, xFrente - 26, ySolo, yPe, $"T3 {Medida(escrito)}", Terra, aEsquerda: true);
        else if (_basica is { } doProjeto)
            CotaVertical(tela, xFrente - 26, ySolo, yPe, Tr.F("enterro {0} (projeto)", Medida(doProjeto.Embedment)), Terra, aEsquerda: true);

        Escrever(
            tela,
            _basica is { } basica
                ? Tr.F("biposte na altura básica: chão plano, ponta baixa {0} m (a mínima do projeto na grade de 1 cm) · tracejado: a parte livre, que muda com o terreno",
                    Medida(basica.LowEdge))
                : Tr.F("biposte: pilares a {0} m na horizontal, topo {1} m abaixo dos módulos · tracejado: o resto depende do terreno",
                    Medida((trasEm - frenteEm) * cos), Medida(bifila.PillarTopDrop)),
            Fraco,
            10.5,
            new Point(Margem, ActualHeight - 16));
    }

    private void CotaHorizontal(DrawingContext tela, double de, double ate, double y, string texto, Brush cor)
    {
        if (Math.Abs(ate - de) < 26) return;

        tela.DrawLine(Cota, new Point(de, y), new Point(ate, y));
        tela.DrawLine(Cota, new Point(de, y - 4), new Point(de, y + 4));
        tela.DrawLine(Cota, new Point(ate, y - 4), new Point(ate, y + 4));

        var formatado = Formatar(texto, 10.5, cor);
        tela.DrawText(formatado, new Point((de + ate) / 2 - formatado.Width / 2, y + 2));
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
        Escrever(tela, $"{graus.ToString("0.#", Cultura)}°", Fraco, 11,
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

    private void CotaVertical(DrawingContext tela, double x, double de, double ate, string texto, Brush cor, bool aEsquerda = false)
    {
        if (Math.Abs(ate - de) < 18) return;

        tela.DrawLine(Cota, new Point(x, de), new Point(x, ate));
        tela.DrawLine(Cota, new Point(x - 4, de), new Point(x + 4, de));
        tela.DrawLine(Cota, new Point(x - 4, ate), new Point(x + 4, ate));

        var formatado = Formatar(texto, 10.5, cor);
        var xTexto = aEsquerda ? x - 6 - formatado.Width : x + 6;
        tela.DrawText(formatado, new Point(xTexto, (de + ate) / 2 - formatado.Height / 2));
    }

    /// <summary>
    /// O símbolo do solo: a linha do terreno com riscos inclinados por baixo,
    /// num trecho em volta do pilar.
    /// </summary>
    private void DesenharSolo(DrawingContext tela, double y, double xPilar)
    {
        var de = Math.Max(Margem, xPilar - 90);
        var ate = Math.Min(ActualWidth - Margem, xPilar + 90);

        tela.DrawLine(Solo, new Point(de, y), new Point(ate, y));

        for (var x = de + 4; x < ate; x += 9)
            tela.DrawLine(Solo, new Point(x, y), new Point(x - 6, y + 6));

        Escrever(tela, Tr.T("solo"), Fraco, 10, new Point(ate - 26, y - 15));
    }

    private void Escrever(DrawingContext tela, string texto, Brush cor, double tamanho, Point onde) =>
        tela.DrawText(Formatar(texto, tamanho, cor), onde);

    private FormattedText Formatar(string texto, double tamanho, Brush cor) =>
        new(
            texto,
            Cultura,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            tamanho,
            cor,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static double Distancia(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string Medida(double valor) => valor.ToString("0.###", Cultura);

    /// <summary>O pilar ao milímetro, com as três casas, como o fabricante cota (3,059).</summary>
    private static string Milimetro(double valor) => valor.ToString("0.000", Cultura);

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
