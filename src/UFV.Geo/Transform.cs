namespace UFV.Geo;

/// <summary>
/// Uma transformação rígida: gira e move, sem esticar nem espelhar.
///
/// Existe por exigência da arquitetura: a mesa é construída em coordenadas
/// locais — origem no canto, plano horizontal, azimute zero — e posicionada no
/// desenho por UMA matriz. Nada de trigonometria canto a canto.
///
/// O motivo não é elegância. Calculando vértice a vértice, cada um ganha o seu
/// próprio erro de arredondamento e a mesa deixa de ser plana por alguns
/// décimos de milímetro. Isso fere a regra sagrada 2 ("mesa é monolito, nunca
/// entorta") de um jeito que nenhum olho pega no desenho, e que o verificador
/// pega sempre. Com uma matriz só, todos os vértices sofrem exatamente a mesma
/// conta, e um plano continua plano.
///
/// O construtor é privado de propósito. Enquanto era público, qualquer um
/// podia montar uma matriz de escala e chamá-la de Transform: a mesa saía com
/// módulos de 2,6 m de largura e o verificador de regra aprovava, porque uma
/// transformação afim também leva plano em plano. Uma reflexão passava do
/// mesmo jeito, e virava todas as faces para baixo — a usina inteira com
/// produção zero no PVsyst e o desenho perfeito. Quem quiser uma
/// transformação, monta com as fábricas.
///
/// Guardada como matriz afim 3×4: as três primeiras colunas giram, a quarta
/// translada. Struct e sem vetor interno, para não alocar em laço sobre
/// milhares de vértices.
/// </summary>
public readonly struct Transform : IEquatable<Transform>
{
    /// <summary>
    /// Folga na conferência de rigidez.
    ///
    /// Uma composição de senos e cossenos não fecha exatamente em 1, e exigir
    /// igualdade perfeita reprovaria toda matriz real.
    /// </summary>
    private const double FolgaDeRigidez = 1e-9;

    private Transform(
        double xx, double xy, double xz, double tx,
        double yx, double yy, double yz, double ty,
        double zx, double zy, double zz, double tz)
    {
        XX = xx; XY = xy; XZ = xz; TX = tx;
        YX = yx; YY = yy; YZ = yz; TY = ty;
        ZX = zx; ZY = zy; ZZ = zz; TZ = tz;
    }

    /// <summary>A transformação que não mexe em nada.</summary>
    public static Transform Identity { get; } = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0);

    public double XX { get; }
    public double XY { get; }
    public double XZ { get; }
    public double TX { get; }
    public double YX { get; }
    public double YY { get; }
    public double YZ { get; }
    public double TY { get; }
    public double ZX { get; }
    public double ZY { get; }
    public double ZZ { get; }
    public double TZ { get; }

    /// <summary>
    /// A inclinação da mesa: giro em torno do eixo X, que em coordenadas
    /// locais é o do comprimento.
    ///
    /// Em radianos, como manda a arquitetura — grau só na interface.
    /// </summary>
    public static Transform Tilt(double radianos)
    {
        var c = Math.Cos(radianos);
        var s = Math.Sin(radianos);

        return new Transform(
            1, 0, 0, 0,
            0, c, -s, 0,
            0, s, c, 0);
    }

    /// <summary>
    /// O azimute da mesa: giro em torno do eixo vertical.
    ///
    /// Medido como em topografia — do norte (+Y), no sentido horário visto de
    /// cima. Azimute de 90° aponta para o leste (+X).
    ///
    /// O sinal está fixado em teste de propósito: trocá-lo espelha a usina
    /// inteira, e em planta isso passa despercebido até alguém conferir contra
    /// o norte do desenho.
    /// </summary>
    public static Transform Azimuth(double radianos)
    {
        var c = Math.Cos(radianos);
        var s = Math.Sin(radianos);

        return new Transform(
            c, s, 0, 0,
            -s, c, 0, 0,
            0, 0, 1, 0);
    }

    /// <summary>O deslocamento puro.</summary>
    public static Transform Translation(Point3 deslocamento) => new(
        1, 0, 0, deslocamento.X,
        0, 1, 0, deslocamento.Y,
        0, 0, 1, deslocamento.Z);

    /// <summary>
    /// A colocação da mesa: inclina, orienta e leva para o lugar, nesta ordem.
    ///
    /// Tem nome próprio para nenhum chamador inventar a sua ordem — compor na
    /// ordem errada gira a mesa em torno do ponto errado e a joga dezenas de
    /// metros para longe, com a forma intacta.
    /// </summary>
    public static Transform Place(double tilt, double azimute, Point3 origem) =>
        Tilt(tilt).Then(Azimuth(azimute)).Then(Translation(origem));

    /// <summary>
    /// Esta transformação seguida da outra: <c>a.Then(b)</c> aplica primeiro a
    /// e depois b.
    /// </summary>
    public Transform Then(Transform depois)
    {
        var a = this;
        var b = depois;

        return new Transform(
            b.XX * a.XX + b.XY * a.YX + b.XZ * a.ZX,
            b.XX * a.XY + b.XY * a.YY + b.XZ * a.ZY,
            b.XX * a.XZ + b.XY * a.YZ + b.XZ * a.ZZ,
            b.XX * a.TX + b.XY * a.TY + b.XZ * a.TZ + b.TX,

            b.YX * a.XX + b.YY * a.YX + b.YZ * a.ZX,
            b.YX * a.XY + b.YY * a.YY + b.YZ * a.ZY,
            b.YX * a.XZ + b.YY * a.YZ + b.YZ * a.ZZ,
            b.YX * a.TX + b.YY * a.TY + b.YZ * a.TZ + b.TY,

            b.ZX * a.XX + b.ZY * a.YX + b.ZZ * a.ZX,
            b.ZX * a.XY + b.ZY * a.YY + b.ZZ * a.ZY,
            b.ZX * a.XZ + b.ZY * a.YZ + b.ZZ * a.ZZ,
            b.ZX * a.TX + b.ZY * a.TY + b.ZZ * a.TZ + b.TZ);
    }

    /// <summary>O ponto depois da transformação.</summary>
    public Point3 Apply(Point3 ponto) => new(
        XX * ponto.X + XY * ponto.Y + XZ * ponto.Z + TX,
        YX * ponto.X + YY * ponto.Y + YZ * ponto.Z + TY,
        ZX * ponto.X + ZY * ponto.Y + ZZ * ponto.Z + TZ);

    /// <summary>Se todos os doze números são finitos.</summary>
    public bool IsFinite =>
        double.IsFinite(XX) && double.IsFinite(XY) && double.IsFinite(XZ) && double.IsFinite(TX)
        && double.IsFinite(YX) && double.IsFinite(YY) && double.IsFinite(YZ) && double.IsFinite(TY)
        && double.IsFinite(ZX) && double.IsFinite(ZY) && double.IsFinite(ZZ) && double.IsFinite(TZ);

    /// <summary>
    /// Se a transformação de fato gira e move, sem esticar nem espelhar.
    ///
    /// As três colunas precisam ser unitárias, perpendiculares entre si, e o
    /// determinante precisa dar +1. Determinante −1 é reflexão: a forma se
    /// mantém, as distâncias também, e todas as faces viram do avesso.
    ///
    /// As fábricas desta classe só produzem transformações rígidas, e a
    /// composição de rígidas é rígida — então isto nunca deveria dar falso. É
    /// uma rede contra um erro futuro na própria classe, e o lugar de conferir
    /// é quem vai confiar no resultado.
    /// </summary>
    public bool IsRigid
    {
        get
        {
            if (!IsFinite) return false;

            // As colunas: para onde vão os eixos X, Y e Z locais.
            var u = new Point3(XX, YX, ZX);
            var v = new Point3(XY, YY, ZY);
            var w = new Point3(XZ, YZ, ZZ);

            if (!Unitario(u) || !Unitario(v) || !Unitario(w)) return false;

            if (Math.Abs(Escalar(u, v)) > FolgaDeRigidez) return false;
            if (Math.Abs(Escalar(u, w)) > FolgaDeRigidez) return false;
            if (Math.Abs(Escalar(v, w)) > FolgaDeRigidez) return false;

            // Determinante: u · (v × w). Negativo seria reflexão.
            var determinante = Escalar(u, new Point3(
                v.Y * w.Z - v.Z * w.Y,
                v.Z * w.X - v.X * w.Z,
                v.X * w.Y - v.Y * w.X));

            return Math.Abs(determinante - 1) <= FolgaDeRigidez;
        }
    }

    /// <inheritdoc/>
    public bool Equals(Transform outra) =>
        XX.Equals(outra.XX) && XY.Equals(outra.XY) && XZ.Equals(outra.XZ) && TX.Equals(outra.TX)
        && YX.Equals(outra.YX) && YY.Equals(outra.YY) && YZ.Equals(outra.YZ) && TY.Equals(outra.TY)
        && ZX.Equals(outra.ZX) && ZY.Equals(outra.ZY) && ZZ.Equals(outra.ZZ) && TZ.Equals(outra.TZ);

    /// <inheritdoc/>
    public override bool Equals(object? outro) => outro is Transform t && Equals(t);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var codigo = new HashCode();

        codigo.Add(XX); codigo.Add(XY); codigo.Add(XZ); codigo.Add(TX);
        codigo.Add(YX); codigo.Add(YY); codigo.Add(YZ); codigo.Add(TY);
        codigo.Add(ZX); codigo.Add(ZY); codigo.Add(ZZ); codigo.Add(TZ);

        return codigo.ToHashCode();
    }

    public static bool operator ==(Transform a, Transform b) => a.Equals(b);

    public static bool operator !=(Transform a, Transform b) => !a.Equals(b);

    private static double Escalar(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static bool Unitario(Point3 a) => Math.Abs(Escalar(a, a) - 1) <= FolgaDeRigidez;
}
