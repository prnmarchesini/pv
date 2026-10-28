using Clivus.Geo;

namespace Clivus.Core;

/// <summary>As peças do carport, como vão para o desenho e para a lista de material.</summary>
public enum CarportPart
{
    Pillar,
    Beam,
    Purlin,
    Brace,
    BasePlate,
    Pedestal,
}

/// <summary>
/// Uma barra: o eixo de uma ponta à outra, a seção e para onde a altura da
/// seção aponta (<paramref name="DepthDirection"/>, vetor unitário).
/// </summary>
public sealed record CarportBar(CarportPart Part, Point3 Start, Point3 End, CarportSection Section, Point3 DepthDirection)
{
    /// <summary>O comprimento do eixo.</summary>
    public double Length => CarportGeometry.Distancia(Start, End);
}

/// <summary>Um bloco de base ou uma chapa: caixa quadrada em pé, com o centro de baixo em <paramref name="BottomCenter"/>.</summary>
public sealed record CarportBlock(CarportPart Part, Point3 BottomCenter, double Size, double Height, Point3 Along);

/// <summary>
/// Um módulo do carport: os quatro cantos da face de cima, na ordem da mesa
/// fixa (borda baixa primeiro, no sentido da fila; depois a alta, voltando).
/// </summary>
public sealed record CarportModule(int Bay, int Row, int Column, IReadOnlyList<Point3> Corners);

/// <summary>
/// Um pórtico assentado: as duas bases no chão, os dois pilares em V, a viga,
/// as duas mãos francesas e os blocos. <see cref="LowEdgeZ"/> é a cota de
/// baixo da borda baixa dos módulos neste pórtico.
/// </summary>
public sealed record CarportPortal(
    Point3 Center,
    Point3 HighBase,
    Point3 LowBase,
    double LowEdgeZ,
    IReadOnlyList<CarportBar> Bars,
    IReadOnlyList<CarportBlock> Blocks,
    double LowestAboveGround)
{
    // O plano dos módulos neste pórtico: a borda baixa (de baixo do quadro),
    // a subida na rampa e a normal. O vão entre dois pórticos usa os dois.
    internal CarportGeometry.V Origem { get; init; }
    internal CarportGeometry.V Subida { get; init; }
    internal CarportGeometry.V Normal { get; init; }
}

/// <summary>Um vão entre dois pórticos: as terças e os módulos.</summary>
public sealed record CarportBay(IReadOnlyList<CarportBar> Purlins, IReadOnlyList<CarportModule> Modules);

/// <summary>
/// A geometria do carport biposte (etapa 34.2, <c>plano/carport</c>), sem CAD.
///
/// Tudo parte do chão: a cota de cada base vem de quem chama (do TIN, no
/// plugin), nunca do clique. A altura livre vale no pilar mais alto do
/// pórtico (decisão 2 do plano): a borda baixa dos módulos fica a
/// <see cref="CarportProfile.ClearHeight"/> acima dele, e o outro pilar sai
/// mais comprido.
///
/// Referência em planta: o centro do pórtico fica na linha do meio da
/// cobertura; <c>ao longo</c> é o sentido da fila; a água cai para a direita
/// de quem anda nesse sentido quando <c>caiADireita</c>.
/// Na rampa: <c>s</c> sobe da borda baixa dos módulos; <c>h</c> sai da face
/// de baixo dos módulos pela normal (negativo para baixo).
/// </summary>
public static class CarportGeometry
{
    /// <summary>
    /// O desnível entre as duas bases de um pórtico a partir do qual a recusa
    /// do pórtico aponta o chão como causa (e a fila avisa, <see cref="CarportRow"/>).
    /// </summary>
    public const double MaiorDesnivelEntreBases = 0.5;

    /// <summary>
    /// O pórtico com o centro em (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <param name="perfil">O carport.</param>
    /// <param name="x">Leste do centro do pórtico (no meio da cobertura).</param>
    /// <param name="y">Norte do centro do pórtico.</param>
    /// <param name="aoLongoX">Sentido da fila, leste (o vetor é normalizado aqui).</param>
    /// <param name="aoLongoY">Sentido da fila, norte.</param>
    /// <param name="caiADireita">A água cai para a direita de quem anda no sentido da fila.</param>
    /// <param name="chao">A cota do chão em (x, y), ou null onde não há terreno.</param>
    /// <param name="motivo">Por que o pórtico não sai, ou null.</param>
    public static CarportPortal? Portal(
        CarportProfile perfil, double x, double y, double aoLongoX, double aoLongoY, bool caiADireita,
        Func<double, double, double?> chao, out string? motivo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(chao);
        if (perfil.WhyInvalid is { } invalido)
        {
            motivo = invalido;
            return null;
        }

        var norma = Math.Sqrt(aoLongoX * aoLongoX + aoLongoY * aoLongoY);
        if (!(norma > 1e-9) || !double.IsFinite(norma))
        {
            motivo = Tr.T("o sentido da fila do carport não tem comprimento");
            return null;
        }

        var f = perfil.Frame;
        var t = perfil.TiltRadians;
        var a = new V(aoLongoX / norma, aoLongoY / norma, 0);
        // A descida em planta: a direita de quem anda em "a" é (ay, -ax).
        var d = caiADireita ? new V(a.Y, -a.X, 0) : new V(-a.Y, a.X, 0);
        var z = new V(0, 0, 1);
        var centro = new V(x, y, 0);

        var planta = perfil.Array.SlopeLength * Math.Cos(t);
        var meioDasBases = centro - d * f.PillarOffset;
        var baseAlta = meioDasBases - d * (f.PillarBaseSpacing / 2);
        var baseBaixa = meioDasBases + d * (f.PillarBaseSpacing / 2);

        if (chao(baseAlta.X, baseAlta.Y) is not { } zAlta || chao(baseBaixa.X, baseBaixa.Y) is not { } zBaixa
            || !double.IsFinite(zAlta) || !double.IsFinite(zBaixa))
        {
            motivo = Tr.T("um pilar do carport cai fora do terreno");
            return null;
        }

        baseAlta = baseAlta with { Z = zAlta };
        baseBaixa = baseBaixa with { Z = zBaixa };

        // A borda baixa: de baixo do quadro, a altura livre acima do chão mais
        // alto entre os dois pilares e o que fica embaixo dela (o chão que sobe
        // para o lado baixo não pode encostar na borda; revisão do 34).
        var bordaEmPlanta = centro + d * (planta / 2);
        var zSobABorda = chao(bordaEmPlanta.X, bordaEmPlanta.Y) is { } zb && double.IsFinite(zb) ? zb : (double?)null;
        var zBorda = Math.Max(Math.Max(zAlta, zBaixa), zSobABorda ?? double.NegativeInfinity) + perfil.ClearHeight;
        var origem = bordaEmPlanta + z * zBorda;
        var subida = d * -Math.Cos(t) + z * Math.Sin(t);
        var normal = d * Math.Sin(t) + z * Math.Cos(t);

        V Rampa(double s, double h) => origem + subida * s + normal * h;
        double HDe(V p) => (p - origem).Dot(normal);
        double SDe(V p) => (p - origem).Dot(subida);

        var hViga = -f.Purlin.Depth - f.Beam.Depth / 2;
        var hDeBaixoDaViga = -f.Purlin.Depth - f.Beam.Depth;
        var sVigaBaixa = f.BeamLowInset;
        var sVigaAlta = perfil.Array.SlopeLength - f.BeamHighInset;

        var bases = (f.Base.PedestalHeight + f.Base.PlateThickness);
        var barras = new List<CarportBar>();
        var blocos = new List<CarportBlock>();
        var pontosBaixos = new List<(V Ponto, double Chao)>();
        if (zSobABorda is { } chaoDaBorda) pontosBaixos.Add((origem, chaoDaBorda));

        // Os dois pilares abrem para fora: o alto para o lado alto (-d), o baixo para +d.
        foreach (var (pe, fora) in new[] { (baseAlta, -1.0), (baseBaixa, +1.0) })
        {
            var inicio = pe + z * bases;
            var eixo = d * (fora * Math.Sin(f.PillarLeanRadians)) + z * Math.Cos(f.PillarLeanRadians);

            // Onde o eixo do pilar encontra a face de baixo da viga.
            var subidaDoEixo = eixo.Dot(normal);
            if (!(subidaDoEixo > 1e-9))
            {
                motivo = Tr.T("o pilar do carport não sobe até a viga");
                return null;
            }

            var comprimento = (hDeBaixoDaViga - HDe(inicio)) / subidaDoEixo;
            if (!(comprimento > f.BraceOnPillar))
            {
                motivo = Tr.T("o pilar do carport fica mais curto que a mão francesa: aumente a altura livre");
                return null;
            }

            var topo = inicio + eixo * comprimento;
            var sTopo = SDe(topo);
            if (sTopo < sVigaBaixa || sTopo > sVigaAlta)
            {
                // Com muito desnível entre as bases, a causa é o chão, não a viga.
                var desnivel = Math.Abs(zAlta - zBaixa);
                motivo = desnivel > MaiorDesnivelEntreBases
                    ? Tr.F("o desnível de {0:0.##} m entre as bases do pórtico leva o topo de um pilar para fora da viga: o chão entre os pilares é íngreme demais para este carport", desnivel)
                    : Tr.T("a viga do carport não alcança o topo de um pilar: diminua o recuo da viga ou a abertura do pilar");
                return null;
            }

            barras.Add(new CarportBar(CarportPart.Pillar, inicio.P, topo.P, f.Pillar, d.P));
            blocos.Add(new CarportBlock(CarportPart.Pedestal, pe.P, f.Base.PedestalSize, f.Base.PedestalHeight, a.P));
            blocos.Add(new CarportBlock(CarportPart.BasePlate, (pe + z * f.Base.PedestalHeight).P, f.Base.PlateSize, f.Base.PlateThickness, a.P));

            // A mão francesa: do pilar, abaixo do topo, à viga, para dentro do pórtico.
            var noPilar = topo - eixo * f.BraceOnPillar;
            // O alto (fora -1) desce na rampa; o baixo sobe.
            var sNaViga = sTopo + fora * (fora < 0 ? f.BraceOnBeamHigh : f.BraceOnBeamLow);
            if (sNaViga < sVigaBaixa || sNaViga > sVigaAlta)
            {
                motivo = Tr.T("a mão francesa do carport cai fora da viga: diminua a distância na viga");
                return null;
            }

            var naViga = Rampa(sNaViga, hDeBaixoDaViga);
            barras.Add(new CarportBar(CarportPart.Brace, noPilar.P, naViga.P, f.Brace, d.P));
            pontosBaixos.Add((noPilar, pe.Z));
        }

        var vigaBaixa = Rampa(sVigaBaixa, hViga);
        var vigaAlta = Rampa(sVigaAlta, hViga);
        barras.Add(new CarportBar(CarportPart.Beam, vigaBaixa.P, vigaAlta.P, f.Beam, normal.P));

        // O ponto mais baixo sobre o chão: as pontas de baixo da viga e das mãos francesas.
        foreach (var s in new[] { sVigaBaixa, sVigaAlta })
        {
            var p = Rampa(s, hDeBaixoDaViga);
            if (chao(p.X, p.Y) is { } c && double.IsFinite(c)) pontosBaixos.Add((p, c));
        }

        motivo = null;
        return new CarportPortal(
            centro.P with { Z = zBorda - perfil.ClearHeight },
            baseAlta.P,
            baseBaixa.P,
            zBorda,
            barras,
            blocos,
            pontosBaixos.Min(q => q.Ponto.Z - q.Chao))
        {
            Origem = origem,
            Subida = subida,
            Normal = normal,
        };
    }

    /// <summary>
    /// O vão entre dois pórticos vizinhos da mesma fila: as terças de um ao
    /// outro e os módulos. Com desnível entre os pórticos, a cobertura do vão
    /// é o plano que passa pelas duas (as duas têm a mesma subida).
    /// </summary>
    /// <param name="perfil">O carport.</param>
    /// <param name="indice">O número do vão na fila, para os módulos.</param>
    /// <param name="de">O pórtico do começo do vão.</param>
    /// <param name="ate">O pórtico do fim.</param>
    public static CarportBay Bay(CarportProfile perfil, int indice, CarportPortal de, CarportPortal ate)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(de);
        ArgumentNullException.ThrowIfNull(ate);

        var arranjo = perfil.Array;
        var f = perfil.Frame;
        var passo = ate.Origem - de.Origem;          // de uma borda baixa à outra
        var inclinado = passo.Length;
        var ao = passo * (1 / inclinado);

        // Com desnível ao longo da fila, a subida do pórtico não é perpendicular
        // a "ao": os módulos usam a descida perpendicular dentro do plano, para
        // saírem retângulos (revisão do 34: 89,3° e frestas a 10 %).
        var descida = de.Subida - ao * de.Subida.Dot(ao);
        descida = descida * (1 / descida.Length);
        var normal = descida.Cross(ao);
        if (normal.Z < 0) normal = normal * -1;

        V Ponto(double s, double xNoVao, double h) => de.Origem + descida * s + ao * xNoVao + normal * h;

        // As terças vão de viga a viga: cada ponta no seu pórtico.
        var tercas = new List<CarportBar>();
        foreach (var s in PurlinStations(perfil))
        {
            var h = -f.Purlin.Depth / 2;
            tercas.Add(new CarportBar(CarportPart.Purlin,
                (de.Origem + de.Subida * s + de.Normal * h).P,
                (ate.Origem + ate.Subida * s + ate.Normal * h).P,
                f.Purlin, normal.P));
        }

        var modulos = new List<CarportModule>();
        var largura = arranjo.ModuleAcross;
        var naDescida = arranjo.ModuleDown;
        var espessura = arranjo.Module.Thickness;
        // As colunas dividem o vão inclinado; em chão plano o passo é módulo + folga.
        var passoDaColuna = inclinado / arranjo.ModulesAcross;
        for (var fiada = 0; fiada < arranjo.ModulesDown; fiada++)
        {
            var s0 = fiada * (naDescida + arranjo.GapDown);
            for (var coluna = 0; coluna < arranjo.ModulesAcross; coluna++)
            {
                var x0 = (passoDaColuna - largura) / 2 + coluna * passoDaColuna;
                Point3[] cantos =
                [
                    Ponto(s0, x0, espessura).P,
                    Ponto(s0, x0 + largura, espessura).P,
                    Ponto(s0 + naDescida, x0 + largura, espessura).P,
                    Ponto(s0 + naDescida, x0, espessura).P,
                ];

                // Sempre anti-horário em planta, como a mesa fixa: com a água
                // para a esquerda, a borda baixa é percorrida ao contrário.
                if (AreaEmPlanta(cantos) < 0) cantos = [cantos[1], cantos[0], cantos[3], cantos[2]];
                modulos.Add(new CarportModule(indice, fiada, coluna, cantos));
            }
        }

        return new CarportBay(tercas, modulos);
    }

    /// <summary>Onde ficam as terças, na rampa a partir da borda baixa (do eixo de cada uma).</summary>
    public static IReadOnlyList<double> PurlinStations(CarportProfile perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var arranjo = perfil.Array;
        var f = perfil.Frame;
        var lista = new List<double>();
        for (var fiada = 0; fiada < arranjo.ModulesDown; fiada++)
        {
            var s0 = fiada * (arranjo.ModuleDown + arranjo.GapDown);
            if (f.PurlinsPerModule == 1)
            {
                lista.Add(s0 + arranjo.ModuleDown / 2);
                continue;
            }

            var entre = (arranjo.ModuleDown - 2 * f.PurlinEdgeOffset) / (f.PurlinsPerModule - 1);
            for (var k = 0; k < f.PurlinsPerModule; k++) lista.Add(s0 + f.PurlinEdgeOffset + k * entre);
        }

        return lista;
    }

    private static double AreaEmPlanta(IReadOnlyList<Point3> c)
    {
        var soma = 0.0;
        for (var i = 0; i < c.Count; i++)
        {
            var (p, q) = (c[i], c[(i + 1) % c.Count]);
            soma += p.X * q.Y - q.X * p.Y;
        }

        return soma / 2;
    }

    internal static double Distancia(Point3 p, Point3 q) =>
        Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y) + (p.Z - q.Z) * (p.Z - q.Z));

    /// <summary>Vetor 3D em double, só para as contas daqui.</summary>
    internal readonly record struct V(double X, double Y, double Z)
    {
        public static V operator +(V p, V q) => new(p.X + q.X, p.Y + q.Y, p.Z + q.Z);
        public static V operator -(V p, V q) => new(p.X - q.X, p.Y - q.Y, p.Z - q.Z);
        public static V operator *(V p, double k) => new(p.X * k, p.Y * k, p.Z * k);
        public double Dot(V q) => X * q.X + Y * q.Y + Z * q.Z;
        public V Cross(V q) => new(Y * q.Z - Z * q.Y, Z * q.X - X * q.Z, X * q.Y - Y * q.X);
        public double Length => Math.Sqrt(Dot(this));
        public Point3 P => new(X, Y, Z);
    }
}
