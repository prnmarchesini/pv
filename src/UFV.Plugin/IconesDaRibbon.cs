using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace UFV.Plugin;

/// <summary>
/// Os ícones dos botões da ribbon, desenhados em vetor.
///
/// São geometrias e não arquivos de imagem por três motivos práticos: não há
/// binário para versionar nem para o bundle carregar, o traço continua nítido
/// em tela 4K (um PNG de 32 px fica borrado), e o mesmo desenho serve ao botão
/// grande e ao pequeno sem precisar de duas versões.
///
/// Como <see cref="RibbonUfv"/>, tudo aqui é WPF: quem chama de fora leva
/// [MethodImpl(NoInlining)] para o runtime não tentar resolver o WPF num host
/// sem interface, como o Core Console.
///
/// As cores foram escolhidas para funcionar nos dois temas da ribbon, o escuro
/// e o claro: cinza médio na estrutura e um tom saturado no que dá sentido ao
/// ícone. Nenhuma delas é quase-branca nem quase-preta, que sumiriam num dos
/// dois.
/// </summary>
internal static class IconesDaRibbon
{
    /// <summary>Lado da caixa em que os ícones são desenhados.</summary>
    private const double Lado = 32.0;

    private static readonly Brush Neutro = Congelar(new SolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C)));
    private static readonly Brush Azul = Congelar(new SolidColorBrush(Color.FromRgb(0x3C, 0x8C, 0xD2)));
    private static readonly Brush Ambar = Congelar(new SolidColorBrush(Color.FromRgb(0xC8, 0x8A, 0x3C)));
    private static readonly Brush Verde = Congelar(new SolidColorBrush(Color.FromRgb(0x52, 0xA0, 0x5A)));
    private static readonly Brush AzulTranslucido =
        Congelar(new SolidColorBrush(Color.FromArgb(0x38, 0x3C, 0x8C, 0xD2)));

    /// <summary>Balão de fala: o comando que só confirma que o plugin está vivo.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Ola() => Montar(
        Traco("M6,7 H26 A3,3 0 0 1 29,10 V20 A3,3 0 0 1 26,23 H15 L9,28 V23 H6 A3,3 0 0 1 3,20 V10 A3,3 0 0 1 6,7 Z", Azul, 2.0),
        Preenchimento("M10,15 A1.6,1.6 0 1 1 10,15.01 Z", Azul),
        Preenchimento("M16,15 A1.6,1.6 0 1 1 16,15.01 Z", Azul),
        Preenchimento("M22,15 A1.6,1.6 0 1 1 22,15.01 Z", Azul));

    /// <summary>Curvas de nível: é assim que o terreno chega ao desenho.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Terreno() => Montar(
        Traco("M2,9 C8,3 12,11 18,5 C22,1 27,4 30,2", Neutro, 2.0),
        Traco("M2,17 C8,11 12,19 18,13 C22,9 27,12 30,10", Ambar, 2.4),
        Traco("M2,25 C8,19 12,27 18,21 C22,17 27,20 30,18", Neutro, 2.0));

    /// <summary>Alvo: clica num ponto e responde X, Y e Z.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Coordenada() => Montar(
        Traco("M16,7 A9,9 0 1 1 15.99,7 Z", Neutro, 2.0),
        Traco("M16,1 V6 M16,26 V31 M1,16 H6 M26,16 H31", Neutro, 2.0),
        Preenchimento("M16,12.5 A3.5,3.5 0 1 1 15.99,12.5 Z", Azul));

    /// <summary>Visto dentro de um círculo: o terreno processado ainda vale.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Status() => Montar(
        Traco("M16,4 A12,12 0 1 1 15.99,4 Z", Neutro, 2.0),
        Traco("M10,16.5 L14.5,21 L22,12", Verde, 2.8));

    /// <summary>
    /// Uma mesa em planta: a fileira de módulos com os apoios embaixo. É o que
    /// a janela mostra, e é por isso que o ícone é a planta e não o corte.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Mesa() => Montar(
        Preenchimento("M3,7 H29 V19 H3 Z", AzulTranslucido),
        Traco("M3,7 H29 V19 H3 Z", Azul, 2.0),
        Traco("M9.5,7 V19 M16,7 V19 M22.5,7 V19", Azul, 1.2),
        Preenchimento("M5,21 H8 V26 H5 Z", Ambar),
        Preenchimento("M14.5,21 H17.5 V26 H14.5 Z", Ambar),
        Preenchimento("M24,21 H27 V26 H24 Z", Ambar));

    /// <summary>Contorno fechado sobre o relevo: a área de implantação.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ImageSource Area() => Montar(
        Preenchimento("M4,22 L11,9 L21,13 L28,8 L28,25 L4,27 Z", AzulTranslucido),
        Traco("M4,22 L11,9 L21,13 L28,8 L28,25 L4,27 Z", Azul, 2.0),
        Preenchimento("M11,9 A2,2 0 1 1 10.99,9 Z", Azul),
        Preenchimento("M28,8 A2,2 0 1 1 27.99,8 Z", Azul));

    private static Drawing Traco(string caminho, Brush cor, double espessura)
    {
        // StartLineCap/EndLineCap arredondados: sem eles as pontas das curvas
        // de nível ficam cortadas em quadrado e o ícone parece quebrado.
        var caneta = new Pen(cor, espessura)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        return Congelar(new GeometryDrawing(null, Congelar(caneta), Geometria(caminho)));
    }

    private static Drawing Preenchimento(string caminho, Brush cor) =>
        Congelar(new GeometryDrawing(cor, null, Geometria(caminho)));

    private static Geometry Geometria(string caminho) => Congelar(Geometry.Parse(caminho));

    /// <summary>
    /// Junta as partes numa imagem de 32x32.
    ///
    /// O retângulo transparente do tamanho da caixa entra primeiro de
    /// propósito: sem ele a imagem teria o tamanho do desenho, e cada ícone
    /// sairia numa escala diferente conforme o traço chegasse mais perto ou
    /// mais longe da borda.
    /// </summary>
    private static ImageSource Montar(params Drawing[] partes)
    {
        var grupo = new DrawingGroup();

        grupo.Children.Add(new GeometryDrawing(
            Brushes.Transparent,
            null,
            new RectangleGeometry(new Rect(0, 0, Lado, Lado))));

        foreach (var parte in partes) grupo.Children.Add(parte);

        return Congelar(new DrawingImage(Congelar(grupo)));
    }

    /// <summary>
    /// Congela o objeto WPF.
    ///
    /// Um Freezable congelado pode ser usado por qualquer thread e para de
    /// gastar com notificação de mudança. Os ícones nunca mudam depois de
    /// montados, e a ribbon do AutoCAD nem sempre os lê na thread que os criou.
    /// </summary>
    private static T Congelar<T>(T objeto) where T : Freezable
    {
        if (objeto.CanFreeze) objeto.Freeze();
        return objeto;
    }
}
