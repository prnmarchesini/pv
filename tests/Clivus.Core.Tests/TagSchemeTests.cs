namespace Clivus.Core.Tests;

/// <summary>
/// A composição da tag num modelo de texto livre (15.1, 05/10/2026): campos
/// {T}, {I}, {S}, zeros à esquerda, o pedaço do trafo que some, a validação e
/// a conversão do formato antigo (5 campos) para o modelo.
/// </summary>
public class TagSchemeTests
{
    [Fact]
    [Trait("Etapa", "15")]
    public void OPadraoEhOMesmoDeAntes()
    {
        Assert.Equal("T{T}.I{I}.S{S}", TagScheme.Default.Template);
        Assert.Equal("T1.I1.S1", TagScheme.Default.Compose(1, 1, 1));
        Assert.Equal("T2.I13.S7", TagScheme.Default.Compose(2, 13, 7));
        Assert.Equal("I4.S2", TagScheme.Default.Compose(null, 4, 2));
        Assert.Null(TagScheme.Default.Problem(10));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OTextoForaDasChavesEhLivre()
    {
        // O pedido do Renan: risquinho entre trafo e inversor, nada entre inversor e string.
        var livre = new TagScheme("T{T}-INV{I}S{S}");
        Assert.Null(livre.Problem(5));
        Assert.Equal("T1-INV2S3", livre.Compose(1, 2, 3));
        Assert.Equal("T12-INV10S11", livre.Compose(12, 10, 11));

        Assert.Equal("UFV T3 / INV 2 / S1", new TagScheme("UFV T{T} / INV {I} / S{S}").Compose(3, 2, 1));
        Assert.Equal("S1-2", new TagScheme("S{I}-{S}").Compose(9, 1, 2));
        Assert.Equal("1.2.3A", new TagScheme("{T}.{I}.{S}A").Compose(1, 2, 3));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ZerosAEsquerda()
    {
        var com = new TagScheme("T{T}.I{I:00}.S{S:000}");
        Assert.Equal("T1.I01.S001", com.Compose(1, 1, 1));
        Assert.Equal("T1.I12.S045", com.Compose(1, 12, 45));

        // Número maior que a largura sai inteiro.
        Assert.Equal("T1.I123.S1234", com.Compose(1, 123, 1234));

        // Com zeros, dois campos podem ficar colados: a largura separa.
        var colado = new TagScheme("{I:00}{S:00}");
        Assert.Null(colado.Problem(3));
        Assert.Equal("0307", colado.Compose(null, 3, 7));

        // Minúscula também vale.
        Assert.Equal("T1.I02.S3", new TagScheme("T{t}.I{i:00}.S{s}").Compose(1, 2, 3));
    }

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData("T{T}-INV{I}S{S}", "INV3S1")]       // o pedaço do trafo é o primeiro: o risquinho sai junto
    [InlineData("T{T}.I{I}.S{S}", "I3.S1")]         // o mesmo que a composição antiga dava
    [InlineData("TR{T} - INV{I} - S{S}", "INV3 - S1")]
    [InlineData("{T}.{I}.{S}", "3.1")]
    [InlineData("I{I}.T{T}.S{S}", "I3.S1")]         // no meio: sai com o texto antes dele
    [InlineData("I{I}.S{S}-T{T}", "I3.S1")]         // no fim, idem
    [InlineData("I{I}.S{S}", "I3.S1")]              // sem o campo do trafo, nada muda
    [InlineData("T{T}I{I:00}S{S}x", "I03S1x")]      // o texto depois do último campo fica
    public void InversorSemTrafoPerdeOPedacoDoTrafo(string modelo, string esperado)
    {
        var esquema = new TagScheme(modelo);
        Assert.Null(esquema.Problem(4));
        Assert.Equal(esperado, esquema.Compose(null, 3, 1));
    }

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData("", "vazio")]
    [InlineData("T{T}.I{I}", "{S}")]                        // sem a string as tags repetem
    [InlineData("T{T}.S{S}", "{I}")]                        // com mais de um inversor, idem
    [InlineData("T{T}.I{X}.S{S}", "{X}")]                   // campo desconhecido
    [InlineData("T{T}.I{I:5}.S{S}", "{I:5}")]               // zero é 0, não outro algarismo
    [InlineData("T{T}.I{I:0000000}.S{S}", "desconhecido")]  // mais de 6 zeros
    [InlineData("T{T}.I{I.S{S}", "{ sem")]
    [InlineData("T{T}.I}I.S{S}", "} sem")]
    [InlineData("T{T}.I{I}.S{S}.{S}", "duas vezes")]
    [InlineData("T{T}{I}.S{S}", "entre {T} e {I}")]         // T12: T1 com o inversor 2 ou o trafo 12?
    [InlineData("T{T}1{I}.S{S}", "entre {T} e {I}")]        // só algarismo entre dois campos, idem
    [InlineData("T{T}.I{I}.S{S}\\P", "\\")]                 // \P é quebra de linha no MText
    [InlineData("T{T}.I{I}.S{S}%%d", "%")]
    [InlineData(" T{T}.I{I}.S{S}", "espaço")]
    [InlineData("T{T}.I{I}.S{S}-012345678901234567890123456789012345678901234567", "60")]
    [InlineData("{I:0}{S}", "desconhecido")]                   // um zero só não põe zero: 112 seria 1 e 12 ou 11 e 2
    public void ModeloInvalidoEhRecusadoComOPorque(string modelo, string trecho)
    {
        var problema = new TagScheme(modelo).Problem(2);
        Assert.NotNull(problema);
        Assert.Contains(trecho, problema);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void SemInversorNoModeloSoComUmInversor()
    {
        var semInversor = new TagScheme("S{S}");
        Assert.Null(semInversor.Problem(1));
        Assert.Null(semInversor.Problem(0));
        Assert.Contains("3 inversores", semInversor.Problem(3));
        Assert.Equal("S4", semInversor.Compose(1, 1, 4));
        Assert.False(semInversor.HasTransformer);
        Assert.True(TagScheme.Default.HasTransformer);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OEsquemaVaiEVoltaDosCamposComFundoEMoldura()
    {
        var esquema = new TagScheme("T{T}-INV{I:00}S{S}", Background: true, Border: false);
        Assert.Equal(TagScheme.FieldCount, esquema.ToFields().Count);
        Assert.Equal(esquema, TagScheme.Parse(esquema.ToFields()));
        Assert.Equal(TagScheme.Default, TagScheme.Parse(TagScheme.Default.ToFields()));
        Assert.Equal(esquema with { Border = true }, TagScheme.Parse((esquema with { Border = true }).ToFields()));

        // Gravado estragado não volta.
        Assert.Null(TagScheme.Parse(["T{T}.I{I}", "0", "0"]));
        Assert.Null(TagScheme.Parse(["T{T}.I{I}.S{S}", "2", "0"]));
        Assert.Null(TagScheme.Parse(["T{T}.I{I}.S{S}", "0"]));
    }

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData(true, "T", "I", "S", ".", "T{T}.I{I}.S{S}")]
    [InlineData(false, "T", "I", "S", ".", "I{I}.S{S}")]
    [InlineData(true, "Trafo", "Inv", "S", "-", "Trafo{T}-Inv{I}-S{S}")]
    [InlineData(false, "T", "", "S", "", "{I}S{S}")]
    [InlineData(true, "T", "I", "S", "", "T{T}I{I}S{S}")]
    [InlineData(true, "", "I", "S", ".", "{T}.I{I}.S{S}")]
    [InlineData(true, "T", "", "S", ".", "T{T}.{I}.S{S}")]
    [InlineData(true, "TransformerX", "InversorLong", "StringLongXY", "-", "TransformerX{T}-InversorLong{I}-StringLongXY{S}")]
    public void OFormatoAntigoViraOModeloEquivalente(bool trafo, string t, string i, string s, string separador, string modelo)
    {
        var antiga = new LegacyTagScheme(trafo, t, i, s, separador);
        Assert.Null(antiga.Problem());

        var nova = TagScheme.ParseLegacy(antiga.ToFields());
        Assert.NotNull(nova);
        Assert.Equal(modelo, nova!.Template);
        Assert.False(nova.Background);
        Assert.False(nova.Border);
        Assert.Null(nova.Problem(4));

        // As mesmas tags de antes, com e sem trafo.
        foreach (var (tr, inv, str) in new (int?, int, int)[] { (1, 1, 1), (2, 13, 7), (null, 4, 2), (null, 12, 3), (10, 1, 11) })
            Assert.Equal(antiga.Compose(tr, inv, str), nova.Compose(tr, inv, str));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void TodaComposicaoAntigaValidaDaAsMesmasTags()
    {
        // Varre as combinações de prefixos e separadores que o formato 1 aceitava.
        // Prefixos de letras (o caso real); um prefixo que começa com pontuação
        // (ex. "-I") perde essa pontuação quando o trafo some (limite aceito).
        string[] prefixos = ["", "T", "Inv", "Trafo", "InversorLong"];
        var conferidas = 0;

        foreach (var trafo in new[] { true, false })
        foreach (var t in prefixos)
        foreach (var i in prefixos)
        foreach (var s in prefixos)
        foreach (var sep in LegacyTagScheme.Separators)
        {
            var antiga = new LegacyTagScheme(trafo, t, i, s, sep);
            if (antiga.Problem() is not null) continue;

            var nova = TagScheme.ParseLegacy(antiga.ToFields());
            Assert.NotNull(nova);
            Assert.Null(nova!.Problem(4));
            for (var n = 1; n <= 12; n += 11)
            {
                Assert.Equal(antiga.Compose(n, n, n + 1), nova.Compose(n, n, n + 1));
                Assert.Equal(antiga.Compose(null, n, n + 1), nova.Compose(null, n, n + 1));
            }

            conferidas++;
        }

        Assert.True(conferidas > 50, $"só {conferidas} composições conferidas");
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ANumeracaoUsaOModeloLivreERecusaSemInversorComVarios()
    {
        var t1 = new Transformer(Guid.NewGuid(), "Trafo", "TA", 800, 13800, 2500, 0, 6, "", new EquipmentSize(2, 1, 2), Guid.Empty);
        var i1 = new Inverter(Guid.NewGuid(), Guid.Empty, "Inversor 1", t1.Id);
        var i2 = new Inverter(Guid.NewGuid(), Guid.Empty, "Inversor 2", Guid.Empty);
        var mesa = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var s1 = new ElectricalString(Guid.NewGuid(), Guid.Empty, [m1], i1.Id, "");
        var s2 = new ElectricalString(Guid.NewGuid(), Guid.Empty, [m2], i2.Id, "");
        var modulos = new Dictionary<Guid, ModuleSpot> { [m1] = new(mesa, 0, 0), [m2] = new(mesa, 10, 0) };
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);

        var r = StringNumbering.Number(new TagScheme("T{T}-INV{I:00}S{S}"), setup, [t1], [i1, i2], [s1, s2], modulos);
        Assert.Equal("T1-INV01S1", r.Tags[s1.Id]);
        Assert.Equal("INV02S1", r.Tags[s2.Id]);

        Assert.Throws<ArgumentException>(() => StringNumbering.Number(new TagScheme("T{T}S{S}"), setup, [t1], [i1, i2], [s1, s2], modulos));
    }
}
