using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A planta baixa da mesa, desenhada na janela: módulos, pilares, as sobras
/// das pontas, os vãos entre pilares, o balanço das extremidades e para onde a
/// mesa olha.
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

    private static readonly Brush Norte = Congelar(new SolidColorBrush(Color.FromRgb(0xE0, 0x61, 0x4F)));

    private static readonly Pen Cota =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x7A))), 1.0));

    private static readonly Pen CotaDoPilar =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x85, 0x3C))), 1.0));

    private static readonly Pen SetaDoNorte =
        Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromRgb(0xE0, 0x61, 0x4F))), 2.0));

    private TableGeometry? _mesa;
    private string? _recado;

    /// <summary>
    /// Margens do desenho, em pixels.
    ///
    /// A da esquerda leva a seta do norte e os rótulos das pontas; a de baixo
    /// leva duas linhas de cota, a dos pilares e a do comprimento.
    /// </summary>
    private const double MargemEsquerda = 86;

    private const double MargemDireita = 26;
    private const double MargemDeCima = 18;
    private const double MargemDeBaixo = 86;

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
            if (!string.IsNullOrWhiteSpace(_recado))
                Escrever(tela, _recado, Fraco, 12, new Point(MargemEsquerda, ActualHeight / 2));

            return;
        }

        if (_mesa.Modules.Count == 0) return;

        // A caixa é a da ESTRUTURA, não a dos módulos: é ela que mostra as
        // sobras das pontas, que são metade do que esta planta existe para
        // conferir.
        var comprimento = _mesa.Length;
        var profundidade = _mesa.Depth;

        if (comprimento <= 0 || profundidade <= 0) return;

        var largura = Math.Max(1, ActualWidth - MargemEsquerda - MargemDireita);
        var altura = Math.Max(1, ActualHeight - MargemDeCima - MargemDeBaixo);
        var escala = Math.Min(largura / comprimento, altura / profundidade);

        var x0 = MargemEsquerda + (largura - comprimento * escala) / 2;
        var y0 = MargemDeCima + (altura - profundidade * escala) / 2;

        double X(double metros) => x0 + metros * escala;

        // A ponta BAIXA do módulo fica embaixo na tela, e a alta em cima: é a
        // mesma orientação do corte que o Renan desenhou, e é o que deixa a
        // seta do norte apontar para onde a mesa olha.
        double Y(double metros) => y0 + (profundidade - metros) * escala;

        var baixo = Y(0);
        var cima = Y(profundidade);

        tela.DrawRectangle(null, Estrutura, new Rect(X(0), cima, comprimento * escala, baixo - cima));

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

        Pontas(tela, cima, baixo);
        SetaNorte(tela, cima, baixo);
        CotasDosPilares(tela, X, baixo + 22, comprimento, escala);
        CotaHorizontal(tela, X(0), X(comprimento), baixo + 50, $"{Medida(comprimento)} m", Cota);

        Escrever(
            tela,
            $"{_mesa.Modules.Count} módulos · {_mesa.Pillars.Count} pilares · "
            + $"{Medida(profundidade)} m na inclinação",
            Fraco,
            11,
            new Point(X(0), baixo + 62));
    }

    /// <summary>
    /// Diz qual lado é a ponta baixa e qual é a alta.
    ///
    /// Sem isto a planta é simétrica e não há como saber para onde a mesa
    /// olha — e é a ponta baixa que manda em tudo: a altura livre é medida
    /// nela, o pilar sobe a partir dela, e é para o lado dela que os módulos
    /// apontam.
    /// </summary>
    private void Pontas(DrawingContext tela, double cima, double baixo)
    {
        var alta = Formatar("ponta alta", 10.5, Fraco);
        var baixa = Formatar("ponta baixa", 10.5, Fraco);

        tela.DrawText(alta, new Point(MargemEsquerda - alta.Width - 8, cima - 2));
        tela.DrawText(baixa, new Point(MargemEsquerda - baixa.Width - 8, baixo - baixa.Height + 2));
    }

    /// <summary>
    /// A seta do norte, apontando para a ponta baixa.
    ///
    /// No hemisfério sul o módulo olha para o norte, então a ponta baixa é a
    /// face norte — e como ela está embaixo na tela, a seta desce.
    ///
    /// Vale para a mesa deitada, com azimute zero, que é o que esta janela
    /// mostra. O azimute de verdade entra quando a mesa for colocada no
    /// terreno, na etapa 5, e aí a seta passa a girar com ele.
    /// </summary>
    private void SetaNorte(DrawingContext tela, double cima, double baixo)
    {
        const double x = 20;

        var topo = Math.Max(cima + 14, baixo - 64);
        var ponta = baixo;

        if (ponta - topo < 24) return;

        tela.DrawLine(SetaDoNorte, new Point(x, topo), new Point(x, ponta));
        tela.DrawLine(SetaDoNorte, new Point(x, ponta), new Point(x - 5, ponta - 9));
        tela.DrawLine(SetaDoNorte, new Point(x, ponta), new Point(x + 5, ponta - 9));

        var letra = Formatar("N", 12, Norte);
        tela.DrawText(letra, new Point(x - letra.Width / 2, topo - letra.Height - 1));
    }

    /// <summary>
    /// As cotas da fileira de pilares: o balanço de cada ponta e os vãos.
    ///
    /// O balanço é o que sobra de estrutura para fora do primeiro e do último
    /// pilar. Quando ele é zero, o pilar está cravado na ponta — e a cota some
    /// em vez de mostrar um "0" que só ocuparia espaço; quem diz isso é a
    /// linha do resumo, embaixo.
    /// </summary>
    private void CotasDosPilares(
        DrawingContext tela, Func<double, double> X, double y, double comprimento, double escala)
    {
        if (_mesa is null || _mesa.Pillars.Count == 0) return;

        var estacoes = _mesa.Pillars.Select(p => p.Station).ToList();

        // Um milímetro: abaixo disso o pilar está na ponta.
        const double Nada = 0.001;

        var balancoEsquerdo = estacoes[0];
        var balancoDireito = comprimento - estacoes[^1];

        if (balancoEsquerdo > Nada)
            CotaHorizontal(tela, X(0), X(estacoes[0]), y, Medida(balancoEsquerdo), CotaDoPilar);

        if (balancoDireito > Nada)
            CotaHorizontal(tela, X(estacoes[^1]), X(comprimento), y, Medida(balancoDireito), CotaDoPilar);

        if (estacoes.Count < 2) return;

        var vaos = new List<double>(estacoes.Count - 1);
        for (var i = 1; i < estacoes.Count; i++) vaos.Add(estacoes[i] - estacoes[i - 1]);

        // Cada vão ganha a sua cota quando há espaço; senão, uma linha só com
        // a contagem. Dezesseis rótulos sobrepostos não se leem.
        var cabeRotulo = vaos.Min() * escala >= 46;

        for (var i = 1; i < estacoes.Count; i++)
        {
            var rotulo = cabeRotulo ? Medida(vaos[i - 1]) : string.Empty;

            CotaHorizontal(tela, X(estacoes[i - 1]), X(estacoes[i]), y, rotulo, CotaDoPilar);
        }

        if (cabeRotulo) return;

        var todosIguais = vaos.Max() - vaos.Min() <= Nada;

        var resumo = todosIguais
            ? $"{vaos.Count} vãos de {Medida(vaos[0])} m"
            : $"{vaos.Count} vãos, de {Medida(vaos.Min())} a {Medida(vaos.Max())} m";

        var texto = Formatar(resumo, 10.5, Pilar);

        tela.DrawText(texto, new Point((X(0) + X(comprimento) - texto.Width) / 2, y + 4));
    }

    private void CotaHorizontal(
        DrawingContext tela, double de, double para, double y, string texto, Pen caneta)
    {
        tela.DrawLine(caneta, new Point(de, y), new Point(para, y));
        tela.DrawLine(caneta, new Point(de, y - 4), new Point(de, y + 4));
        tela.DrawLine(caneta, new Point(para, y - 4), new Point(para, y + 4));

        if (string.IsNullOrEmpty(texto)) return;

        var formatado = Formatar(texto, 10.5, Texto);

        // O fundo atrás do rótulo abre espaço na linha de cota, que senão
        // passa por trás do número e o deixa ilegível.
        tela.DrawRectangle(Fundo, null, new Rect(
            (de + para) / 2 - formatado.Width / 2 - 3,
            y - formatado.Height / 2,
            formatado.Width + 6,
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

    private static string Medida(double valor) => valor.ToString("0.###", Brasil);

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
