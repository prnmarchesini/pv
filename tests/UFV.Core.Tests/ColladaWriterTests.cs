using System.Globalization;
using System.Xml.Linq;
using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// O escritor DAE: XML bem formado, uma geometria e um nó por face, os
/// cantos de volta iguais, a normal para cima, o nome da camada nos nós.
/// </summary>
public class ColladaWriterTests
{
    private static readonly XNamespace Ns = ColladaWriter.Ns;

    private static ModuleFace Face(Guid id, double x, double y, double z) => new(id,
    [
        new Point3(x, y, z),
        new Point3(x + 1.303, y, z),
        new Point3(x + 1.303, y + 2.384 * Math.Cos(0.35), z + 2.384 * Math.Sin(0.35)),
        new Point3(x, y + 2.384 * Math.Cos(0.35), z + 2.384 * Math.Sin(0.35)),
    ]);

    private static IReadOnlyList<ModuleFace> Faces(int quantas) =>
        Enumerable.Range(0, quantas).Select(i => Face(Guid.NewGuid(), i * 1.323, 0, 700 + i * 0.01)).ToList();

    private static XDocument Escrever(IReadOnlyList<ModuleFace> faces) =>
        ColladaWriter.Write(faces, "MARCHENG_UFV_FACE", new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc), "Renan");

    /// <summary>O documento é XML bem formado e volta a ser lido do texto: raiz COLLADA 1.4.1, metro, Z para cima.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void ODocumentoEBemFormadoEDizUnidadeEEixo()
    {
        var texto = Escrever(Faces(3)).ToString();
        var lido = XDocument.Parse(texto);

        Assert.Equal(Ns + "COLLADA", lido.Root!.Name);
        Assert.Equal("1.4.1", lido.Root.Attribute("version")!.Value);

        var asset = lido.Root.Element(Ns + "asset")!;
        Assert.Equal("meter", asset.Element(Ns + "unit")!.Attribute("name")!.Value);
        Assert.Equal("1", asset.Element(Ns + "unit")!.Attribute("meter")!.Value);
        Assert.Equal("Z_UP", asset.Element(Ns + "up_axis")!.Value);
        Assert.Equal("2026-09-26T12:00:00Z", asset.Element(Ns + "created")!.Value);
        Assert.Equal("Renan", asset.Descendants(Ns + "author").Single().Value);
    }

    /// <summary>Uma geometria e um nó por face, e cada nó aponta para a sua geometria.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void UmaGeometriaEUmNoPorFace()
    {
        var faces = Faces(28);
        var doc = Escrever(faces);

        var geometrias = doc.Descendants(Ns + "geometry").ToList();
        var nos = doc.Descendants(Ns + "node").ToList();

        Assert.Equal(28, geometrias.Count);
        Assert.Equal(28, nos.Count);

        foreach (var no in nos)
        {
            var url = no.Element(Ns + "instance_geometry")!.Attribute("url")!.Value;
            Assert.Contains(geometrias, g => "#" + g.Attribute("id")!.Value == url);
            Assert.Equal("MARCHENG_UFV_FACE", no.Attribute("name")!.Value);
        }

        // A cena instancia a cena visual, e ela é uma só.
        Assert.Equal("#cena", doc.Descendants(Ns + "instance_visual_scene").Single().Attribute("url")!.Value);
        Assert.Single(doc.Descendants(Ns + "visual_scene"));
    }

    /// <summary>
    /// Os doze números de cada face voltam iguais, com ponto e sem perda:
    /// quem lê noutra máquina lê os mesmos cantos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OsCantosVoltamIguais()
    {
        var faces = Faces(5);
        var doc = Escrever(faces);

        foreach (var face in faces)
        {
            var id = "face-" + face.Id.ToString("N");
            var geometria = doc.Descendants(Ns + "geometry").Single(g => g.Attribute("id")!.Value == id);
            var posicoes = geometria.Descendants(Ns + "float_array").First(a => a.Attribute("id")!.Value == id + "-pos-array");

            Assert.Equal("12", posicoes.Attribute("count")!.Value);
            Assert.DoesNotContain(",", posicoes.Value);

            var numeros = posicoes.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToList();

            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(face.Corners[i].X, numeros[3 * i], 12);
                Assert.Equal(face.Corners[i].Y, numeros[3 * i + 1], 12);
                Assert.Equal(face.Corners[i].Z, numeros[3 * i + 2], 12);
            }
        }
    }

    /// <summary>A normal é unitária e aponta para cima (Z positivo) numa face anti-horária inclinada 0,35 rad.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void ANormalApontaParaCima()
    {
        var doc = Escrever(Faces(1));
        var normal = doc.Descendants(Ns + "float_array").Single(a => a.Attribute("id")!.Value.EndsWith("-nrm-array"));
        var n = normal.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(1, Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]), 9);
        Assert.True(n[2] > 0.9);

        // Dois triângulos: (0 1 2) e (0 2 3), ambos anti-horários.
        var p = doc.Descendants(Ns + "p").Single().Value;
        Assert.Equal("0 0 1 0 2 0 0 0 2 0 3 0", p);
        Assert.Equal("2", doc.Descendants(Ns + "triangles").Single().Attribute("count")!.Value);
    }

    /// <summary>
    /// O material tem o nome da camada, e toda face o usa, no nó e nos
    /// triângulos: é por ele que o PVsyst reconhece as superfícies PV.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void TodaFaceUsaOMaterialComONomeDaCamada()
    {
        var doc = Escrever(Faces(4));

        var material = doc.Descendants(Ns + "material").Single();
        Assert.Equal("MARCHENG_UFV_FACE", material.Attribute("name")!.Value);

        var alvo = "#" + material.Attribute("id")!.Value;
        var ligacoes = doc.Descendants(Ns + "instance_material").ToList();

        Assert.Equal(4, ligacoes.Count);
        Assert.All(ligacoes, l => Assert.Equal(alvo, l.Attribute("target")!.Value));
        Assert.All(ligacoes, l => Assert.Equal(ColladaWriter.MaterialSymbol, l.Attribute("symbol")!.Value));
        Assert.All(doc.Descendants(Ns + "triangles"), t => Assert.Equal(ColladaWriter.MaterialSymbol, t.Attribute("material")!.Value));

        // O efeito do material existe e é o único.
        var efeito = doc.Descendants(Ns + "effect").Single();
        Assert.Equal("#" + efeito.Attribute("id")!.Value, material.Element(Ns + "instance_effect")!.Attribute("url")!.Value);
    }

    /// <summary>Camada com espaço e cedilha: o nome fica igual, o id vira um id XML válido.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void CamadaComCaractereEstranhoDaIdValido()
    {
        var doc = ColladaWriter.Write(Faces(1), "1 Módulos ção", DateTime.UtcNow, "Renan");
        var material = doc.Descendants(Ns + "material").Single();

        Assert.Equal("1 Módulos ção", material.Attribute("name")!.Value);
        Assert.Matches("^[A-Za-z_][A-Za-z0-9._-]*$", material.Attribute("id")!.Value);
        Assert.Equal("#" + material.Attribute("id")!.Value, doc.Descendants(Ns + "instance_material").Single().Attribute("target")!.Value);
    }

    /// <summary>
    /// Sem face é erro: o esquema exige geometria e nó, e um arquivo vazio
    /// não serve no PVsyst. Quem chama diz "nenhum módulo" antes.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void SemFaceERecusado()
    {
        Assert.Throws<ArgumentException>(() => Escrever([]));
    }

    /// <summary>
    /// Cantos em ordem horária (como o 6.2 pode receber de uma entidade
    /// qualquer): o escritor inverte a ordem e a normal sai para cima, com
    /// os mesmos quatro cantos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void CantosHorariosSaoInvertidosENormalSobe()
    {
        var certa = Face(Guid.NewGuid(), 0, 0, 700);
        var horaria = new ModuleFace(Guid.NewGuid(), [certa.Corners[0], certa.Corners[3], certa.Corners[2], certa.Corners[1]]);
        var doc = Escrever([horaria]);

        var normal = doc.Descendants(Ns + "float_array").Single(a => a.Attribute("id")!.Value.EndsWith("-nrm-array"));
        var n = normal.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToList();
        Assert.True(n[2] > 0.9);

        var posicoes = doc.Descendants(Ns + "float_array").Single(a => a.Attribute("id")!.Value.EndsWith("-pos-array"));
        var numeros = posicoes.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToList();

        // Os quatro cantos gravados são os mesmos quatro, noutra ordem.
        var gravados = Enumerable.Range(0, 4).Select(i => (numeros[3 * i], numeros[3 * i + 1], numeros[3 * i + 2])).ToHashSet();
        Assert.Equal(certa.Corners.Select(p => (p.X, p.Y, p.Z)).ToHashSet(), gravados);
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void FaceVerticalERecusada()
    {
        var parede = new ModuleFace(Guid.NewGuid(), [new Point3(0, 0, 0), new Point3(1, 0, 0), new Point3(1, 0, 1), new Point3(0, 0, 1)]);

        Assert.Throws<ArgumentException>(() => Escrever([parede]));
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void CantoNaNOuGuidVazioSaoRecusados()
    {
        var nan = new ModuleFace(Guid.NewGuid(), [new Point3(double.NaN, 0, 0), new Point3(1, 0, 0), new Point3(1, 1, 0), new Point3(0, 1, 0)]);
        Assert.Throws<ArgumentException>(() => Escrever([nan]));

        Assert.Throws<ArgumentException>(() => Escrever([Face(Guid.Empty, 0, 0, 700)]));
    }

    /// <summary>Camada chamada "cena" não colide com o id da cena.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void CamadaChamadaCenaNaoColideComIdDaCena()
    {
        var doc = ColladaWriter.Write(Faces(2), "cena", DateTime.UtcNow, "Renan");
        var ids = doc.Descendants().Select(e => e.Attribute("id")?.Value).Where(v => v is not null).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void FaceSemQuatroCantosERecusada()
    {
        var torta = new ModuleFace(Guid.NewGuid(), [new Point3(0, 0, 0), new Point3(1, 0, 0), new Point3(1, 1, 0)]);

        Assert.Throws<ArgumentException>(() => Escrever([torta]));
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void FaceRepetidaERecusada()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Escrever([Face(id, 0, 0, 700), Face(id, 5, 0, 700)]));
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void FaceSemAreaERecusada()
    {
        var linha = new ModuleFace(Guid.NewGuid(), [new Point3(0, 0, 0), new Point3(1, 0, 0), new Point3(2, 0, 0), new Point3(3, 0, 0)]);

        Assert.Throws<ArgumentException>(() => Escrever([linha]));
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void CamadaOuAutorEmBrancoSaoRecusados()
    {
        Assert.Throws<ArgumentException>(() => ColladaWriter.Write(Faces(1), " ", DateTime.UtcNow, "Renan"));
        Assert.Throws<ArgumentException>(() => ColladaWriter.Write(Faces(1), "FACE", DateTime.UtcNow, ""));
    }

    /// <summary>
    /// A origem local é o menor canto arredondado para baixo ao metro; as
    /// coordenadas gravadas são relativas a ela e o cabeçalho a registra,
    /// legível de volta. Somar os dois devolve o canto original ao milímetro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OrigemLocalESubtraidaERegistrada()
    {
        var faces = Enumerable.Range(0, 3).Select(i => Face(Guid.NewGuid(), 312_345.678 + i * 1.323, 7_412_345.912, 703.25 + i * 0.01)).ToList();
        var origem = ColladaWriter.LocalOrigin(faces);

        Assert.Equal(312_345, origem.X);
        Assert.Equal(7_412_345, origem.Y);
        Assert.Equal(703, origem.Z);

        var doc = ColladaWriter.Write(faces, "MARCHENG_UFV_FACE", DateTime.UtcNow, "Renan", origem);

        var comentario = doc.Descendants(Ns + "comments").Single().Value;
        var lida = ColladaWriter.ParseOriginComment(comentario);
        Assert.NotNull(lida);
        Assert.Equal(origem, lida.Value);

        foreach (var face in faces)
        {
            var id = "face-" + face.Id.ToString("N");
            var posicoes = doc.Descendants(Ns + "float_array").Single(a => a.Attribute("id")!.Value == id + "-pos-array");
            var numeros = posicoes.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToList();

            // Tudo pequeno (cabe em float32 com folga) e volta ao original.
            Assert.All(numeros, v => Assert.True(Math.Abs(v) < 100));

            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(face.Corners[i].X, numeros[3 * i] + lida.Value.X, 6);
                Assert.Equal(face.Corners[i].Y, numeros[3 * i + 1] + lida.Value.Y, 6);
                Assert.Equal(face.Corners[i].Z, numeros[3 * i + 2] + lida.Value.Z, 6);
            }
        }
    }

    /// <summary>Sem origem (a sobrecarga curta), as coordenadas ficam as do desenho e o cabeçalho diz origem zero.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void SemOrigemAsCoordenadasFicamAsDoDesenho()
    {
        var doc = Escrever(Faces(1));

        Assert.Equal(new Point3(0, 0, 0), ColladaWriter.ParseOriginComment(doc.Descendants(Ns + "comments").Single().Value));
        Assert.Null(ColladaWriter.ParseOriginComment("outra coisa"));
        Assert.Null(ColladaWriter.ParseOriginComment(null));
    }

    [Fact]
    [Trait("Etapa", "6")]
    public void OrigemNaoFinitaERecusadaEListaVaziaDaOrigemZero()
    {
        Assert.Throws<ArgumentException>(() => ColladaWriter.Write(Faces(1), "FACE", DateTime.UtcNow, "Renan", new Point3(double.NaN, 0, 0)));
        Assert.Equal(new Point3(0, 0, 0), ColladaWriter.LocalOrigin([]));
    }

    /// <summary>Mil faces cabem num arquivo de alguns megabytes e saem em menos de cinco segundos.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void MilFacesSaemRapido()
    {
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var texto = Escrever(Faces(1000)).ToString();
        relogio.Stop();

        Assert.True(relogio.ElapsedMilliseconds < 5000, $"levou {relogio.ElapsedMilliseconds} ms");
        Assert.True(texto.Length < 5_000_000);
    }
}
