using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// A matriz que leva a mesa das coordenadas locais para o desenho.
///
/// Ela existe porque a arquitetura proíbe o contrário: "mesa construída em
/// coordenadas locais e posicionada por UMA matriz. Nada de trigonometria
/// canto a canto". Canto a canto, cada vértice ganha o seu próprio erro de
/// arredondamento e a mesa deixa de ser plana — e mesa que entorta fere a
/// regra sagrada 2.
/// </summary>
public class TransformTests
{
    private static void Perto(Point3 esperado, Point3 obtido)
    {
        Assert.Equal(esperado.X, obtido.X, 9);
        Assert.Equal(esperado.Y, obtido.Y, 9);
        Assert.Equal(esperado.Z, obtido.Z, 9);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void AIdentidadeNaoMexeEmNada()
    {
        var ponto = new Point3(3, -4, 5.5);

        Perto(ponto, Transform.Identity.Apply(ponto));
    }

    /// <summary>
    /// O tilt gira em torno do eixo X, que é o do comprimento da mesa. Um
    /// ponto a um metro na direção da inclinação sobe sen(tilt).
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OTiltGiraEmTornoDoComprimentoDaMesa()
    {
        var tilt = Transform.Tilt(Math.PI / 6);   // 30 graus

        // O eixo do giro não se move.
        Perto(new Point3(7, 0, 0), tilt.Apply(new Point3(7, 0, 0)));

        // E um metro na direção da inclinação sobe 0,5 e avança 0,866.
        var ponto = tilt.Apply(new Point3(0, 1, 0));

        Assert.Equal(0, ponto.X, 9);
        Assert.Equal(Math.Cos(Math.PI / 6), ponto.Y, 9);
        Assert.Equal(0.5, ponto.Z, 9);
    }

    /// <summary>
    /// Azimute é medido como em topografia: do norte (+Y), no sentido horário
    /// visto de cima. Azimute 90° aponta para o leste (+X).
    ///
    /// A convenção está fixada num teste de propósito. Trocar o sinal do
    /// azimute espelha a usina inteira, e em planta isso passa despercebido
    /// até alguém conferir contra o norte do desenho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OAzimuteEHorarioAPartirDoNorte()
    {
        var norte = new Point3(0, 1, 0);

        Perto(norte, Transform.Azimuth(0).Apply(norte));
        Perto(new Point3(1, 0, 0), Transform.Azimuth(Math.PI / 2).Apply(norte));
        Perto(new Point3(0, -1, 0), Transform.Azimuth(Math.PI).Apply(norte));
        Perto(new Point3(-1, 0, 0), Transform.Azimuth(3 * Math.PI / 2).Apply(norte));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void OAzimuteNaoMexeNaCota()
    {
        var ponto = Transform.Azimuth(1.234).Apply(new Point3(3, 4, 7.5));

        Assert.Equal(7.5, ponto.Z, 9);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ATranslacaoSomaODeslocamento()
    {
        var mover = Transform.Translation(new Point3(10, -20, 3));

        Perto(new Point3(11, -18, 8), mover.Apply(new Point3(1, 2, 5)));
    }

    /// <summary>
    /// A ordem da composição é a que o nome promete: em
    /// <c>a.Then(b)</c>, primeiro a, depois b. Trocar isso gira a mesa em
    /// torno do lugar errado, e ela sai deslocada de dezenas de metros.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ComporERespeitarAOrdem()
    {
        var girar = Transform.Azimuth(Math.PI / 2);
        var mover = Transform.Translation(new Point3(10, 0, 0));

        // Girar e depois mover: o ponto vira (1,0,0) e vai para (11,0,0).
        Perto(new Point3(11, 0, 0), girar.Then(mover).Apply(new Point3(0, 1, 0)));

        // Mover e depois girar: o ponto vira (10,1,0) e gira para (1,-10,0).
        Perto(new Point3(1, -10, 0), mover.Then(girar).Apply(new Point3(0, 1, 0)));
    }

    /// <summary>
    /// A colocação da mesa: tilt, depois azimute, depois translação.
    ///
    /// O resultado é calculado à mão, e não recomposto com as mesmas fábricas:
    /// um teste que repete a expressão do corpo de Place detecta mutação, mas
    /// não valida coisa nenhuma — quem trocar a ordem e "consertar" o teste na
    /// mesma linha não encontra resistência nenhuma.
    ///
    /// O caso: mesa a 30°, azimute 90° (virada para o leste), origem em
    /// (10, 20, 30). Um ponto 2 m acima da ponta baixa, em (0, 2, 0):
    ///   tilt 30°  → (0; 2·cos30°; 2·sen30°) = (0; √3; 1)
    ///   azimute 90° → o que apontava para o norte passa a apontar para o
    ///                 leste: (√3; 0; 1)
    ///   translação → (10 + √3; 20; 31)
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AColocacaoEhTiltDepoisAzimuteDepoisTranslacao()
    {
        var colocar = Transform.Place(30 * Math.PI / 180, Math.PI / 2, new Point3(10, 20, 30));

        Perto(new Point3(10 + Math.Sqrt(3), 20, 31), colocar.Apply(new Point3(0, 2, 0)));

        // E a origem local vai exatamente para a origem informada.
        Perto(new Point3(10, 20, 30), colocar.Apply(new Point3(0, 0, 0)));
    }

    /// <summary>
    /// A ordem importa e o teste prova qual é: trocada, a mesa vai parar em
    /// outro lugar. Sem isto, inverter tilt com azimute em Place passaria.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void TrocarAOrdemDaColocacaoMudaOResultado()
    {
        const double tilt = 0.35, azimute = 1.1;
        var origem = new Point3(500, -300, 42);
        var ponto = new Point3(0, 4.788, 0);

        var certo = Transform.Place(tilt, azimute, origem).Apply(ponto);
        var trocado = Transform.Azimuth(azimute)
            .Then(Transform.Tilt(tilt))
            .Then(Transform.Translation(origem))
            .Apply(ponto);

        Assert.True(
            Math.Abs(certo.X - trocado.X) > 0.1 || Math.Abs(certo.Z - trocado.Z) > 0.1,
            "Inverter tilt com azimute deu o mesmo resultado: o teste não prova a ordem.");
    }

    /// <summary>
    /// A rigidez não é promessa de documentação: é conferida.
    ///
    /// Enquanto o construtor era público, dava para montar uma matriz de
    /// escala e chamá-la de Transform. A mesa saía com módulos de 2,6 m de
    /// largura e o verificador da regra sagrada 2 aprovava, porque uma
    /// transformação afim também leva plano em plano.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void TodaTransformacaoConstruivelERigida()
    {
        Assert.True(Transform.Identity.IsRigid);
        Assert.True(Transform.Tilt(-1.2).IsRigid);
        Assert.True(Transform.Azimuth(4.9).IsRigid);
        Assert.True(Transform.Translation(new Point3(1e6, -2e6, 30)).IsRigid);
        Assert.True(Transform.Place(0.35, 1.1, new Point3(500, -300, 42)).IsRigid);

        // E a composição de rígidas continua rígida, que é o que permite
        // montar a colocação por partes.
        Assert.True(Transform.Tilt(0.3).Then(Transform.Azimuth(2.2)).Then(Transform.Tilt(-0.9)).IsRigid);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TransformacaoQuebradaNaoERigida()
    {
        Assert.False(Transform.Tilt(double.NaN).IsRigid);
    }

    /// <summary>
    /// Isto é a regra sagrada 2 antes de ela existir: uma transformação
    /// rígida leva plano em plano. Se três pontos eram coplanares com um
    /// quarto, continuam sendo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATransformacaoLevaPlanoEmPlano()
    {
        var colocar = Transform.Place(0.35, 1.1, new Point3(500, -300, 42));

        // Quatro cantos de um retângulo no plano z = 0.
        Point3[] locais =
        [
            new(0, 0, 0), new(18.7, 0, 0), new(18.7, 4.788, 0), new(0, 4.788, 0),
        ];

        var mundo = locais.Select(colocar.Apply).ToArray();

        // O quarto ponto tem que estar no plano dos três primeiros.
        var u = Menos(mundo[1], mundo[0]);
        var v = Menos(mundo[3], mundo[0]);
        var n = Cruz(u, v);
        var w = Menos(mundo[2], mundo[0]);

        var fora = Math.Abs(n.X * w.X + n.Y * w.Y + n.Z * w.Z) / Norma(n);

        Assert.True(fora < 1e-9, $"O canto saiu {fora:0.###e0} m fora do plano.");
    }

    /// <summary>
    /// Transformação rígida não estica nem encolhe. Se a distância entre dois
    /// cantos mudar, a mesa deixou de ser corpo rígido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATransformacaoNaoMudaDistancias()
    {
        var colocar = Transform.Place(0.35, 1.1, new Point3(500, -300, 42));

        var a = new Point3(0, 0, 0);
        var b = new Point3(18.702, 4.788, -0.033);

        var antes = Norma(Menos(b, a));
        var depois = Norma(Menos(colocar.Apply(b), colocar.Apply(a)));

        Assert.Equal(antes, depois, 9);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TransformacaoComNumeroQuebradoNaoEFinita()
    {
        Assert.False(Transform.Tilt(double.NaN).IsFinite);
        Assert.False(Transform.Translation(new Point3(1, double.PositiveInfinity, 0)).IsFinite);
        Assert.True(Transform.Place(0.35, 1.1, new Point3(1, 2, 3)).IsFinite);
    }

    private static Point3 Menos(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static Point3 Cruz(Point3 a, Point3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double Norma(Point3 a) => Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
}
