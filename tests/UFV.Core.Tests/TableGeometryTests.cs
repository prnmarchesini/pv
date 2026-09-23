using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A mesa em coordenadas locais: origem no canto, plano horizontal, azimute
/// zero. É aqui que ela vira geometria — caixas de módulo, faces superiores e
/// pilares — antes de qualquer terreno.
///
/// O sistema local: X ao longo do comprimento, Y ao longo da inclinação (o M2
/// do desenho do Renan), Z para cima. A face superior dos módulos fica em
/// z = 0, e o corpo desce até menos a espessura. Assim o plano dos módulos é
/// o plano z = 0, e a regra sagrada 2 vira uma conferência de uma linha.
/// </summary>
public class TableGeometryTests
{
    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    /// <summary>A mesa 2V do Renan: 28 módulos, 14 colunas, 18,702 × 4,788 m.</summary>
    private static TableLayout Mesa() =>
        new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    /// <summary>Tesoura de 3 m, pilar a 2,5 m dela, seção de 0,15 × 0,07 m.</summary>
    private static TableFrame Estrutura() => new(3.00, 2.50, 0.15, 0.07);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), Estrutura());

    // ------------------------------------------------------------ contagens

    [Fact]
    [Trait("Etapa", "3")]
    public void HaUmaCaixaEUmaFacePorModulo()
    {
        var geo = Geometria();

        Assert.Equal(28, geo.Modules.Count);
        Assert.All(geo.Modules, m => Assert.Equal(4, m.TopFace.Count));
        Assert.All(geo.Modules, m => Assert.Equal(8, m.Solid.Count));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void HaUmPilarPorPosicaoDaTabela()
    {
        var geo = Geometria();

        Assert.Equal(7, geo.Pillars.Count);
        Assert.Equal(
            PillarTable.Distribute(Mesa().Length, 3).Positions,
            geo.Pillars.Select(p => p.Station));
    }

    /// <summary>
    /// Em 2V são duas fileiras de 14. Cada módulo aparece uma vez só, e as
    /// coordenadas (coluna, fileira) não se repetem — senão dois módulos
    /// ocupariam o mesmo lugar e a potência sairia dobrada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void CadaModuloTemSuaColunaEFileira()
    {
        var geo = Geometria();

        Assert.Equal(28, geo.Modules.Select(m => (m.Column, m.Row)).Distinct().Count());
        Assert.Equal(14, geo.Modules.Select(m => m.Column).Distinct().Count());
        Assert.Equal(2, geo.Modules.Select(m => m.Row).Distinct().Count());
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void Em1VHaUmaFileiraSo()
    {
        var mesa = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);
        var geo = TableGeometry.Local(mesa, PillarTable.Distribute(mesa.Length, 3), new TableFrame(2.0, 1.5, 0.15, 0.07));

        Assert.Equal(28, geo.Modules.Count);
        Assert.Single(geo.Modules.Select(m => m.Row).Distinct());
    }

    // ------------------------------------------------------------ posições

    /// <summary>
    /// O primeiro módulo começa depois da sobra da esquerda, e o último acaba
    /// antes da sobra da direita. Errar isso desloca a mesa inteira por 10 cm
    /// — o suficiente para encostar na vizinha e ninguém ver em planta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OsModulosRespeitamAsSobrasDasPontas()
    {
        var geo = Geometria();

        var menorX = geo.Modules.SelectMany(m => m.TopFace).Min(p => p.X);
        var maiorX = geo.Modules.SelectMany(m => m.TopFace).Max(p => p.X);

        Assert.Equal(0.10, menorX, 9);
        Assert.Equal(Mesa().Length - 0.10, maiorX, 9);
    }

    /// <summary>
    /// A fileira de baixo começa na origem e a de cima começa depois dela mais
    /// o espaçamento vertical. Juntas ocupam o M2 inteiro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsDuasFileirasOcupamOM2()
    {
        var geo = Geometria();

        var baixo = geo.Modules.Where(m => m.Row == 0).SelectMany(m => m.TopFace).ToList();
        var cima = geo.Modules.Where(m => m.Row == 1).SelectMany(m => m.TopFace).ToList();

        Assert.Equal(0, baixo.Min(p => p.Y), 9);
        Assert.Equal(2.384, baixo.Max(p => p.Y), 9);

        Assert.Equal(2.404, cima.Min(p => p.Y), 9);
        Assert.Equal(4.788, cima.Max(p => p.Y), 9);
        Assert.Equal(Mesa().Depth, cima.Max(p => p.Y), 9);
    }

    /// <summary>
    /// O pilar fica a T2 da ponta baixa da TESOURA, e a tesoura é centrada no
    /// M2 — foi o que o Renan confirmou no segundo desenho. Então, medido da
    /// ponta baixa do módulo, ele está em sobra + T2.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OPilarFicaNaTesouraQueECentradaNosModulos()
    {
        var geo = Geometria();

        // sobra = (4,788 - 3,000) / 2 = 0,894 ; 0,894 + 2,500 = 3,394
        Assert.Equal(0.894, geo.RafterOffset, 9);
        Assert.Equal(3.394, geo.PillarRow, 9);

        Assert.All(geo.Pillars, p => Assert.Equal(3.394, p.Anchor.Y, 9));
    }

    /// <summary>
    /// Com as duas sobras iguais, trocar a da esquerda pela da direita não
    /// muda nada — e foi assim que o mutante sobreviveu. Aqui elas são
    /// diferentes de propósito.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsSobrasDiferentesVaoCadaUmaParaOSeuLado()
    {
        var mesa = new TableLayout(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.30);
        var geo = TableGeometry.Local(mesa, PillarTable.Distribute(mesa.Length, 3), Estrutura());

        var pontos = geo.Modules.SelectMany(m => m.TopFace).ToList();

        Assert.Equal(0.10, pontos.Min(p => p.X), 9);
        Assert.Equal(mesa.Length - 0.30, pontos.Max(p => p.X), 9);
    }

    /// <summary>
    /// A pegada do pilar é centrada na estação, e não começa nela. Descentrada
    /// por meia seção, a mesa inteira ganha 7 cm de deslocamento no ferro —
    /// invisível em planta e errado na obra.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void APegadaDoPilarECentradaNaEstacao()
    {
        var geo = Geometria();

        foreach (var pilar in geo.Pillars)
        {
            var centroX = (pilar.Footprint.Min(p => p.X) + pilar.Footprint.Max(p => p.X)) / 2;
            var centroY = (pilar.Footprint.Min(p => p.Y) + pilar.Footprint.Max(p => p.Y)) / 2;

            Assert.Equal(pilar.Station, centroX, 9);
            Assert.Equal(geo.PillarRow, centroY, 9);
            Assert.Equal(pilar.Station, pilar.Anchor.X, 9);
        }
    }

    /// <summary>
    /// A ordem dos cantos do sólido é contrato: os quatro de cima na ordem da
    /// face, e depois os quatro de baixo, cada um sob o seu. Quem for montar a
    /// caixa no CAD vai confiar nisso, e embaralhar os de baixo dá uma caixa
    /// torcida que nenhum teste de contagem pega.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OsCantosDeBaixoFicamSobOsDeCimaNaMesmaOrdem()
    {
        foreach (var modulo in Geometria().Modules)
        {
            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(modulo.TopFace[i], modulo.Solid[i]);

                var baixo = modulo.Solid[i + 4];

                Assert.Equal(modulo.TopFace[i].X, baixo.X, 9);
                Assert.Equal(modulo.TopFace[i].Y, baixo.Y, 9);
                Assert.Equal(modulo.TopFace[i].Z - 0.033, baixo.Z, 9);
            }
        }
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void APegadaDoPilarTemASecaoInformada()
    {
        var pilar = Geometria().Pillars[3];

        var largura = pilar.Footprint.Max(p => p.X) - pilar.Footprint.Min(p => p.X);
        var profundidade = pilar.Footprint.Max(p => p.Y) - pilar.Footprint.Min(p => p.Y);

        Assert.Equal(0.15, largura, 9);
        Assert.Equal(0.07, profundidade, 9);
        Assert.Equal(4, pilar.Footprint.Count);
    }

    // ------------------------------------------------- faces e espessura

    /// <summary>
    /// A face superior está em z = 0 e o corpo desce a espessura. É essa face,
    /// e só ela, que vai para o PVsyst — ele não recebe sólido, recebe plano.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AFaceSuperiorEstaNoTopoDoModulo()
    {
        var geo = Geometria();

        Assert.All(geo.Modules.SelectMany(m => m.TopFace), p => Assert.Equal(0, p.Z, 9));

        foreach (var modulo in geo.Modules)
        {
            Assert.Equal(0, modulo.Solid.Max(p => p.Z), 9);
            Assert.Equal(-0.033, modulo.Solid.Min(p => p.Z), 9);
        }
    }

    /// <summary>
    /// A face tem o tamanho do módulo, e não o da célula com espaçamento. Um
    /// erro de 2 cm por módulo vira 2% de área a mais no PVsyst, e a simulação
    /// inteira sai otimista.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AFaceTemOTamanhoDoModuloENaoODaCelula()
    {
        var face = Geometria().Modules[0].TopFace;

        Assert.Equal(1.303, face.Max(p => p.X) - face.Min(p => p.X), 9);
        Assert.Equal(2.384, face.Max(p => p.Y) - face.Min(p => p.Y), 9);
    }

    /// <summary>
    /// Os cantos da face saem no sentido anti-horário visto de cima, com a
    /// normal para cima. Invertido, o PVsyst entende a face virada para o
    /// chão e a produção da usina inteira vai a zero — com o desenho
    /// parecendo perfeito.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ANormalDaFaceApontaParaCima()
    {
        foreach (var modulo in Geometria().Modules)
        {
            var f = modulo.TopFace;

            var u = new Point3(f[1].X - f[0].X, f[1].Y - f[0].Y, f[1].Z - f[0].Z);
            var v = new Point3(f[3].X - f[0].X, f[3].Y - f[0].Y, f[3].Z - f[0].Z);

            var nz = u.X * v.Y - u.Y * v.X;

            Assert.True(nz > 0, $"A face do módulo {modulo.Column},{modulo.Row} está virada para baixo.");
        }
    }

    /// <summary>
    /// E continua apontando para cima depois de colocada, com tilt de
    /// qualquer sinal e azimute qualquer. Na mesa deitada, conferir só a
    /// componente Z é um teste de enrolamento em planta; aqui a normal é a de
    /// verdade, em três dimensões.
    ///
    /// Virada para baixo, o PVsyst entende a face olhando para o chão e a
    /// produção da usina inteira vai a zero, com o desenho perfeito.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, 0)]
    [InlineData(0.35, 1.1)]
    [InlineData(-0.35, 4.9)]
    [InlineData(1.2, 3.14)]
    public void ANormalDaFaceApontaParaCimaDepoisDeColocada(double tilt, double azimute)
    {
        var mundo = Geometria().Transformed(Transform.Place(tilt, azimute, new Point3(500_000, 7_400_000, 812)));

        foreach (var modulo in mundo.Modules)
        {
            var f = modulo.TopFace;

            var u = new Point3(f[1].X - f[0].X, f[1].Y - f[0].Y, f[1].Z - f[0].Z);
            var v = new Point3(f[3].X - f[0].X, f[3].Y - f[0].Y, f[3].Z - f[0].Z);

            var nz = u.X * v.Y - u.Y * v.X;

            Assert.True(nz > 0, $"A face do módulo {modulo.Column},{modulo.Row} virou para baixo.");
        }
    }

    /// <summary>
    /// Uma matriz que estica ou espelha é recusada. Ela levaria plano em plano
    /// — o verificador da regra sagrada 2 aprovaria — e entregaria módulos do
    /// tamanho errado, ou todas as faces do avesso.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ColocarComMatrizNaoRigidaERecusado()
    {
        // Não há como construir uma: o construtor de Transform é privado e as
        // fábricas só produzem rígidas. Este teste guarda essa porta.
        Assert.True(Transform.Place(0.35, 1.1, new Point3(1, 2, 3)).IsRigid);

        Assert.Throws<ArgumentException>(
            () => Geometria().Transformed(Transform.Tilt(double.NaN)));
    }

    /// <summary>
    /// O plano manda o verificador de regra sagrada rodar "sobre toda saída do
    /// motor em todo teste". A mesa que este arquivo monta é saída do motor.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AMesaMontadaAquiObedeceAsRegrasSagradas()
    {
        Assert.Null(UFV.Core.Invariants.RigidTable.Check(Geometria()));
        Assert.Null(UFV.Core.Invariants.RigidTable.Check(
            Geometria().Transformed(Transform.Place(0.35, 1.1, new Point3(500, -300, 42)))));
    }

    // ------------------------------------------------------ transformação

    /// <summary>
    /// A mesa colocada no desenho é a mesma mesa: mesma contagem, mesmas
    /// distâncias. Se algo esticar, deixou de ser corpo rígido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ColocarNaoMudaAMesa()
    {
        var local = Geometria();
        var mundo = local.Transformed(Transform.Place(0.35, 1.1, new Point3(500, -300, 42)));

        Assert.Equal(local.Modules.Count, mundo.Modules.Count);
        Assert.Equal(local.Pillars.Count, mundo.Pillars.Count);

        var a = local.Modules[0].TopFace[0];
        var b = local.Modules[^1].TopFace[2];

        var ta = mundo.Modules[0].TopFace[0];
        var tb = mundo.Modules[^1].TopFace[2];

        Assert.Equal(Distancia(a, b), Distancia(ta, tb), 9);
    }

    /// <summary>
    /// Inclinada de verdade: com tilt de 20°, a ponta alta dos módulos sobe
    /// 4,788 × sen 20° = 1,638 m acima da ponta baixa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ComTiltAPontaAltaSobeOQueOSenoManda()
    {
        var mundo = Geometria().Transformed(Transform.Tilt(20 * Math.PI / 180));

        var pontos = mundo.Modules.SelectMany(m => m.TopFace).ToList();
        var subida = pontos.Max(p => p.Z) - pontos.Min(p => p.Z);

        Assert.Equal(4.788 * Math.Sin(20 * Math.PI / 180), subida, 6);
    }

    // ------------------------------------------------------------ recusas

    /// <summary>
    /// "Tesoura menor que o módulo" é exigência do plano. Tesoura maior que o
    /// M2 daria sobra negativa e poria o pilar fora da mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void TesouraMaiorQueOsModulosERecusada()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(9, 2.5, 0.15, 0.07)));

        Assert.Contains("tesoura", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void MesaInvalidaNaoViraGeometria()
    {
        var quebrada = new TableLayout(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.Throws<InvalidOperationException>(
            () => TableGeometry.Local(quebrada, PillarTable.Distribute(18.702, 3), Estrutura()));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TabelaDePilarQueNaoFechaComAMesaERecusada()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => TableGeometry.Local(Mesa(), new PillarTable([3, 3, 3]), Estrutura()));

        Assert.Contains("não fecha", erro.Message);
    }

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
