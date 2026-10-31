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

    /// <summary>Quanto do enterro o biposte mostra antes da quebra (38.9): o resto só na cota.</summary>
    private const double EnterroDesenhado = 0.6;

    private static readonly Pen MaoFrancesa =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xB8, 0xC4, 0xD0))), 3));

    /// <summary>O chão caindo para trás que o P2 de catálogo alcança (38.9), tracejado.</summary>
    private static readonly Pen ChaoCaindo =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x9C, 0x7A, 0x52))), 1.5)
        {
            DashStyle = new DashStyle([6, 4], 0),
        });

    private static readonly Brush Terca = Congelar(new SolidColorBrush(Color.FromRgb(0x63, 0xD9, 0xC4)));
    private static readonly Brush Claro = Congelar(new SolidColorBrush(Color.FromRgb(0xE6, 0xEB, 0xF0)));

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
    internal void Mostrar(TableGeometry mesa, double tiltRadians, double? enterroNominal = null, BifilaFrame? bifila = null, BasicPillars? basica = null)
    {
        _mesa = mesa;
        _tilt = tiltRadians;
        _enterro = enterroNominal;
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
        // ponta baixa que o P1 dá (ou a mínima do projeto) abaixo dela, e o
        // enterro que vale.
        var vaoAteOSolo = _basica?.LowEdge ?? 0.35;
        var t3 = _basica?.Embedment ?? _enterro ?? 0.6;

        // No biposte (38.9): o P2 com folga desce além do chão plano, e o
        // desenho reserva a folga embaixo, as cotas da tesoura em cima e as
        // dos pilares dos lados. O enterro sai cortado (a quebra): os 2,20 m
        // achatavam o desenho todo; as cotas dizem o valor certo.
        var folgaDoP2 = FolgaDoP2();
        var t3Desenhado = _bifila is null ? t3 : Math.Min(t3, EnterroDesenhado);
        var emCima = _bifila is null ? 0 : 70;
        var (ladoEsquerdo, ladoDireito) = _bifila is null ? (46.0, 12.0) : (124.0, 112.0);

        // A caixa do desenho vai da ponta baixa até a ponta alta, mais o
        // pedaço abaixo dela até o pé do pilar dentro do solo.
        var largura = Math.Max(0.001, m2 * cos);
        var altura = Math.Max(0.001, alto + vaoAteOSolo + folgaDoP2 + t3Desenhado + 0.05);

        var disponivelX = Math.Max(1, ActualWidth - 2 * Margem - ladoEsquerdo - ladoDireito);
        var disponivelY = Math.Max(1, ActualHeight - 2 * Margem - 26 - emCima - (_bifila is null ? 0 : 36));
        var k = Math.Min(disponivelX / largura, disponivelY / altura);

        // O biposte fica no meio da largura que sobra.
        var x0 = Margem + ladoEsquerdo + (_bifila is null ? 0 : Math.Max(0, (disponivelX - largura * k) / 2));
        var yBaixa = Margem + emCima + alto * k;

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
        var yPe = ySolo + t3Desenhado * k;

        if (_bifila is { } bifila && _mesa.RearPillarRow is { } trasEm)
        {
            DesenharBifila(tela, bifila, pilarEm, trasEm, sobra, t1, x0, yBaixa, k, ySolo, yPe, m2, t3 > t3Desenhado + 1e-9);
            return;
        }

        tela.DrawLine(Pilar, encosto, new Point(encosto.X, yBaixa));
        tela.DrawLine(PilarSemFim, new Point(encosto.X, yBaixa), new Point(encosto.X, ySolo));
        tela.DrawLine(Pilar, new Point(encosto.X, ySolo), new Point(encosto.X, yPe));

        DesenharSolo(tela, ySolo, encosto.X);

        if (_enterro is { } escrito)
            CotaVertical(tela, encosto.X - 26, ySolo, yPe, Tr.F("enterro {0}", Medida(escrito)), Terra, aEsquerda: true);
        else
            Escrever(tela, Tr.T("Enterro: o da configuração"), Fraco, 10, new Point(encosto.X + 8, (ySolo + yPe) / 2 - 7));

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

    /// <summary>Quanto o P2 escrito passa do P2 do chão plano: a folga para o chão caindo para trás (38.9). Zero sem ela.</summary>
    private double FolgaDoP2() =>
        _bifila?.RearPillar is { } p2 && _basica?.P2 is { } plano ? Math.Max(0, p2 - plano) : 0;

    /// <summary>
    /// O biposte peça por peça, como o desenho do fabricante (38.9; Renan,
    /// 30/10/2026: "é PILAR P1, Mão francesa MF1, pilar P2, Mão francesa MF2,
    /// aí tem as terças, as tesouras"): os dois pilares inteiros, do topo ao
    /// pé, com o nome e o comprimento; as mãos francesas com o nome, o
    /// comprimento e onde prendem no pilar; a tesoura e as terças; o chão
    /// plano na altura que o P1 dá e, com a folga do P2, o chão caindo para
    /// trás que ele alcança. As cotas da tesoura vão em cima dos módulos, as
    /// dos pilares do lado de fora deles.
    /// </summary>
    /// <param name="quebrado">Se o enterro saiu cortado (mais fundo que <see cref="EnterroDesenhado"/>): os pilares e as cotas levam a quebra.</param>
    private void DesenharBifila(
        DrawingContext tela, BifilaFrame bifila, double frenteEm, double trasEm, double sobra, double t1,
        double x0, double yBaixa, double k, double ySolo, double yPe, double m2, bool quebrado)
    {
        var cos = Math.Cos(_tilt);
        var sen = Math.Sin(_tilt);

        // Em metro, com a origem na ponta baixa do módulo: h para o lado alto,
        // v para cima. A tela é só a escala.
        Point Tela(double h, double v) => new(x0 + h * k, yBaixa - v * k);
        Point No(double d, double recuo = 0) => new(x0 + d * cos * k + sen * recuo, yBaixa - d * sen * k + cos * recuo);

        var espessura = _mesa!.Modules.Count > 0 ? -_mesa.Modules[0].Solid.Min(p => p.Z) : 0;
        var eixoDaViga = espessura + bifila.Purlin.Depth + bifila.Beam.Depth / 2;

        (double H, double V) NaViga(double d) => (d * cos + sen * eixoDaViga, d * sen - cos * eixoDaViga);

        var folga = _basica is null ? 0 : FolgaDoP2();
        var xFrente = Tela(frenteEm * cos, 0).X;
        var xTras = Tela(trasEm * cos, 0).X;
        // O pé de trás com o nominal dele (38.12): mais fundo, ou mais raso, que o da frente.
        var diferencaDoEnterro = (_basica?.RearEmbedment ?? _basica?.Embedment ?? 0) - (_basica?.Embedment ?? 0);
        var yPeTras = yPe + (folga + diferencaDoEnterro) * k;

        // O chão plano, na altura que o P1 dá; com a folga do P2, o chão
        // caindo para trás, girando no pé do P1, até onde o P2 alcança.
        DesenharChao(tela, ySolo);
        var yChaoAtras = ySolo;
        var emPlanta = (trasEm - frenteEm) * cos;
        var declive = folga > 0.0005 && emPlanta > 1e-9 ? folga / emPlanta : 0.0;

        if (declive > 0 && xTras - xFrente > 1)
        {
            // O tracejado para logo depois do P2, antes das cotas dele; o
            // nome vai no começo, longe do T3 do P2.
            var xFim = xTras + 8;
            tela.DrawLine(ChaoCaindo, new Point(xFrente, ySolo), new Point(xFim, ySolo + (xFim - xFrente) * declive));
            yChaoAtras = ySolo + folga * k;

            // Embaixo da linha em toda a largura do texto: a altura é a do
            // fim dele, onde a linha já desceu mais.
            var rotulo = Formatar(Tr.F("chão caindo {0} % para trás", Medida(Math.Round(declive * 100, 1))), 10.5, Terra);
            var xRotulo = xFrente + 14;
            tela.DrawText(rotulo, new Point(xRotulo, ySolo + (xRotulo + rotulo.Width - xFrente) * declive + 3));
        }

        // A tesoura (a viga do pórtico), embaixo das terças.
        var (ha, va) = NaViga(sobra);
        var (hb, vb) = NaViga(sobra + t1);
        tela.DrawLine(Tesoura, Tela(ha, va), Tela(hb, vb));

        // As terças: duas por fileira de módulos, cada uma a esta distância
        // de uma borda, entre os módulos e a tesoura.
        var meioDaTerca = espessura + bifila.Purlin.Depth / 2;
        var ladoDaTerca = Math.Max(6, bifila.Purlin.Depth * k);
        foreach (var faixa in Fileiras())
        {
            foreach (var d in new[] { faixa.De + bifila.PurlinFromModuleEdge, faixa.Ate - bifila.PurlinFromModuleEdge })
            {
                var c = Tela(d * cos + sen * meioDaTerca, d * sen - cos * meioDaTerca);
                tela.DrawRectangle(Terca, null, new Rect(c.X - ladoDaTerca / 2, c.Y - ladoDaTerca / 2, ladoDaTerca, ladoDaTerca));
            }
        }

        // Meia seção do pilar na inclinação, para a cabeça da mão francesa ficar
        // entre a face deste pilar e a do outro, como o Core confere.
        var meiaSecao = _mesa.Pillars.Count > 0
            ? (_mesa.Pillars[0].Footprint.Max(p => p.Y) - _mesa.Pillars[0].Footprint.Min(p => p.Y)) / 2
            : 0;
        var recusadas = new List<string>();

        // Onde cada mão francesa prende no pilar (na tela), para a cota.
        var prende = new Dictionary<bool, double>();

        foreach (var (em, outroEm, comprimento, abaixo, paraTras) in new[]
        {
            (frenteEm, trasEm, bifila.FrontBraceLength, bifila.FrontBraceBelowTop, true),
            (trasEm, frenteEm, bifila.RearBraceLength, bifila.RearBraceBelowTop, false),
        })
        {
            var hp = em * cos;
            var vTopo = em * sen - bifila.PillarTopDrop;
            var topo = Tela(hp, vTopo);

            // O pilar inteiro, do topo ao pé; sem a altura (a conta não
            // fechou), cheio até a ponta baixa, tracejado até o solo.
            if (_basica is not null)
            {
                tela.DrawLine(Pilar, topo, new Point(topo.X, paraTras ? yPe : yPeTras));
            }
            else
            {
                tela.DrawLine(Pilar, topo, new Point(topo.X, yBaixa));
                tela.DrawLine(PilarSemFim, new Point(topo.X, yBaixa), new Point(topo.X, ySolo));
                tela.DrawLine(Pilar, new Point(topo.X, ySolo), new Point(topo.X, yPe));
                CotaVertical(tela, topo.X + (paraTras ? -22 : 22), topo.Y, yBaixa, Medida(vTopo), Laranja, aEsquerda: paraTras);
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

            var pe = Tela(hp, pv);
            var cabeca = Tela(hc, vc);
            tela.DrawLine(MaoFrancesa, pe, cabeca);
            prende[paraTras] = pe.Y;

            // O nome da peça e o comprimento, embaixo do meio dela, longe dos
            // módulos e da tesoura.
            Rotulo(tela, $"{(paraTras ? "MF1" : "MF2")} {Milimetro(comprimento)}", Claro,
                new Point((pe.X + cabeca.X) / 2, Math.Max(pe.Y, cabeca.Y) + 10));
        }

        Angulo(tela, Tela(0, 0), k);

        // Em cima dos módulos, ao longo da inclinação: os módulos, a tesoura
        // e onde o P1 encosta nela (o P2 sai da distância entre os pilares,
        // cotada embaixo).
        CotaInclinada(tela, No(0, -18), No(m2, -18), Tr.F("módulos {0}", Medida(m2)), Texto);
        CotaInclinada(tela, No(sobra, -38), No(sobra + t1, -38), Tr.F("tesoura T1 {0}", Medida(t1)), Verde);
        CotaInclinada(tela, No(sobra, -58), No(frenteEm, -58), Tr.F("T2 {0}", Medida(frenteEm - sobra)), Laranja);

        // O T3: o do P1 do chão plano; o do P2, do chão caindo que ele
        // alcança. Sem T3 na mesa, o enterro que vale é o do projeto, e a cota
        // diz isso (revisão do 38.7). O do P1 à esquerda dele (à direita fica
        // o nome do chão caindo), antes das outras cotas: a do P1 inteiro vai
        // por fora do texto dele (revisão do 38.9: sem a MF1, as duas caíam na
        // mesma linha).
        var nomeDoEnterro = _enterro is { } escrito ? Tr.F("enterro {0}", Medida(escrito))
            : _basica is { } doProjeto ? Tr.F("enterro {0} (projeto)", Medida(doProjeto.Embedment))
            : null;

        // O P2 com o enterro nominal que vale para ele (38.12, a regra da estrutura).
        var nomeDoEnterroTras = _basica?.RearEmbedment is { } nominalTras && _basica is { } comOsDois && Math.Abs(nominalTras - comOsDois.Embedment) > 0.0005
            ? Tr.F("enterro {0}", Medida(nominalTras))
            : nomeDoEnterro;

        var foraDoT3 = xFrente - 12;
        if (nomeDoEnterro is not null)
        {
            foraDoT3 = CotaVertical(tela, xFrente - 12, ySolo, yPe, nomeDoEnterro, Terra, aEsquerda: true);
            if (folga > 0.0005 && nomeDoEnterroTras is not null) CotaVertical(tela, xTras - 12, yChaoAtras, yPeTras, nomeDoEnterroTras, Terra, aEsquerda: true);
        }

        // Os pilares inteiros e onde as mãos francesas prendem: do P1 para a
        // esquerda, do P2 para a direita, de dentro para fora.
        if (_basica is { } alturaBasica)
        {
            var topoFrente = Tela(frenteEm * cos, frenteEm * sen - bifila.PillarTopDrop);
            var topoTras = Tela(trasEm * cos, trasEm * sen - bifila.PillarTopDrop);

            var foraDaMao = xFrente - 12;
            if (prende.TryGetValue(true, out var yMf1))
                foraDaMao = CotaVertical(tela, xFrente - 12, topoFrente.Y, yMf1, Medida(bifila.FrontBraceBelowTop), Claro, aEsquerda: true);
            var xEsquerdaDoP1 = Math.Min(foraDaMao, foraDoT3) - 10;
            CotaVertical(tela, xEsquerdaDoP1, topoFrente.Y, yPe, $"P1 {Milimetro(alturaBasica.P1)}", Laranja, aEsquerda: true, fracaoDoTexto: 0.8);

            var xDireitaDoP2 = xTras + 12;
            if (prende.TryGetValue(false, out var yMf2))
                xDireitaDoP2 = CotaVertical(tela, xDireitaDoP2, topoTras.Y, yMf2, Medida(bifila.RearBraceBelowTop), Claro) + 10;
            CotaVertical(tela, xDireitaDoP2, topoTras.Y, yPeTras, $"P2 {Milimetro(bifila.RearPillar ?? alturaBasica.P2 ?? alturaBasica.P1)}", Laranja, fracaoDoTexto: 0.8);

            // A altura livre sob o módulo, na ponta baixa, do chão plano.
            var fundo = Tela(espessura * sen, -espessura * cos);
            CotaVertical(tela, x0 - 12, ySolo, fundo.Y, Tr.F("livre {0}", Milimetro(alturaBasica.LowEdge - espessura * cos)), Texto, aEsquerda: true);

            // A quebra do enterro cortado, nos pilares e nas cotas que passam por ela.
            if (quebrado)
            {
                var yQuebra = (ySolo + yPe) / 2;
                var yQuebraTras = yQuebra + folga * k;
                Quebra(tela, xFrente, yQuebra);
                Quebra(tela, xTras, yQuebraTras);
                Quebra(tela, xFrente - 12, yQuebra);
                if (folga > 0.0005) Quebra(tela, xTras - 12, yQuebraTras);
                Quebra(tela, xEsquerdaDoP1, yQuebra);
                Quebra(tela, xDireitaDoP2, yQuebraTras);
            }
        }

        CotaHorizontal(tela, xFrente, xTras, Math.Max(yPe, yPeTras) + 12, Milimetro((trasEm - frenteEm) * cos), Laranja);

        EscreverEmBaixo(
            tela,
            (_basica is { } doProjetoSemP1 && bifila.FrontPillar is null
                ? Tr.F("Medidas em metro. Chão plano com a ponta baixa na mínima do projeto ({0} m): sem o P1 e o P2, a altura vem da faixa do projeto. Na usina, cada pilar sai com o comprimento que o terreno pede.",
                    Milimetro(doProjetoSemP1.LowEdge))
                : _basica is { } basica
                ? (folga > 0.0005
                    ? Tr.F("Medidas em metro. Chão plano na altura do P1 (ponta baixa a {0} m); tracejado: o chão caindo {1} % do P1 ao P2, a folga do catálogo, que o P2 alcança com o enterro. Na usina, cada pilar sai com o comprimento que o terreno pede.",
                        Milimetro(basica.LowEdge), Medida(Math.Round(declive * 100, 1)))
                    : Tr.F("Medidas em metro. Chão plano na altura do P1 (ponta baixa a {0} m). Na usina, cada pilar sai com o comprimento que o terreno pede.",
                        Milimetro(basica.LowEdge)))
                : Tr.F("biposte: pilares a {0} m na horizontal, topo {1} m abaixo dos módulos · tracejado: o resto depende do terreno",
                    Medida((trasEm - frenteEm) * cos), Medida(bifila.PillarTopDrop)))
            + (quebrado ? " " + Tr.T("O enterro sai cortado; as cotas dão o comprimento inteiro.") : string.Empty),
            recusadas);
    }

    /// <summary>
    /// A legenda embaixo do desenho, quebrada na largura dele, e as mãos
    /// francesas recusadas logo acima dela.
    /// </summary>
    private void EscreverEmBaixo(DrawingContext tela, string legenda, IReadOnlyList<string> recusadas)
    {
        var texto = Formatar(legenda, 10.5, Fraco);
        texto.MaxTextWidth = Math.Max(50, ActualWidth - 2 * Margem);
        var y = ActualHeight - 6 - texto.Height;
        tela.DrawText(texto, new Point(Margem, y));

        if (recusadas.Count > 0)
            Escrever(tela, string.Join(" · ", recusadas), Laranja, 10.5, new Point(Margem, y - 16));
    }

    /// <summary>O chão plano de ponta a ponta do desenho, com os riscos por baixo (38.9).</summary>
    private void DesenharChao(DrawingContext tela, double y)
    {
        var de = Margem;
        var ate = ActualWidth - Margem;

        tela.DrawLine(Solo, new Point(de, y), new Point(ate, y));

        for (var x = de + 4; x < ate; x += 9)
            tela.DrawLine(Solo, new Point(x, y), new Point(x - 6, y + 6));

        Escrever(tela, Tr.T("chão plano"), Fraco, 10, new Point(de, y - 15));
    }

    /// <summary>
    /// A quebra de uma linha cortada (38.9): o fundo apaga um pedaço dela, e
    /// dois riscos inclinados marcam o corte, como nos desenhos de fabricante.
    /// </summary>
    private static void Quebra(DrawingContext tela, double x, double y)
    {
        tela.DrawRectangle(Fundo, null, new Rect(x - 6, y - 3, 12, 6));
        tela.DrawLine(Cota, new Point(x - 7, y - 1), new Point(x + 7, y - 5));
        tela.DrawLine(Cota, new Point(x - 7, y + 5), new Point(x + 7, y + 1));
    }

    /// <summary>Um texto centrado no ponto, com o fundo do desenho por trás, para não brigar com as linhas.</summary>
    private void Rotulo(DrawingContext tela, string texto, Brush cor, Point centro)
    {
        var formatado = Formatar(texto, 11, cor);
        var caixa = new Rect(centro.X - formatado.Width / 2 - 3, centro.Y - formatado.Height / 2, formatado.Width + 6, formatado.Height);
        tela.DrawRectangle(Fundo, null, caixa);
        tela.DrawText(formatado, new Point(caixa.X + 3, caixa.Y));
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

        // Com o fundo por trás: no biposte o número cai em cima dos módulos.
        var graus = _tilt * 180 / Math.PI;
        var texto = Formatar($"{graus.ToString("0.#", Cultura)}°", 11, Fraco);
        var onde = new Point(origem.X + raio + 4, origem.Y - raio * 0.55);
        tela.DrawRectangle(Fundo, null, new Rect(onde.X - 2, onde.Y, texto.Width + 4, texto.Height));
        tela.DrawText(texto, onde);
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

    /// <summary>
    /// Cota vertical com o texto ao lado. Devolve a borda de fora do texto
    /// (a esquerda dele, ou a direita), para a cota seguinte ir depois dela;
    /// sem espaço para a cota, devolve o próprio x.
    /// </summary>
    /// <param name="fracaoDoTexto">Onde o texto fica, de <paramref name="de"/> (0) a <paramref name="ate"/> (1): no meio, ou mais embaixo para fugir do chão.</param>
    private double CotaVertical(DrawingContext tela, double x, double de, double ate, string texto, Brush cor, bool aEsquerda = false, double fracaoDoTexto = 0.5)
    {
        if (Math.Abs(ate - de) < 18) return x;

        tela.DrawLine(Cota, new Point(x, de), new Point(x, ate));
        tela.DrawLine(Cota, new Point(x - 4, de), new Point(x + 4, de));
        tela.DrawLine(Cota, new Point(x - 4, ate), new Point(x + 4, ate));

        var formatado = Formatar(texto, 10.5, cor);
        var xTexto = aEsquerda ? x - 6 - formatado.Width : x + 6;
        tela.DrawText(formatado, new Point(xTexto, de + (ate - de) * fracaoDoTexto - formatado.Height / 2));

        return aEsquerda ? xTexto : xTexto + formatado.Width;
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
