namespace UFV.Core.Tests;

/// <summary>
/// As cotas viáveis por mesa: os pares (cota da ponta baixa no início, no
/// fim) em que os módulos da fileira de baixo respeitam a faixa, com a
/// tolerância de lombo e o limite de declividade.
///
/// O que se trava: números conferidos à mão em terreno plano e em rampa; a
/// tolerância de lombo deixando passar um calombo; o limite de declividade
/// cortando o giro; e, o mais importante, que os intervalos calculados de
/// uma vez dizem exatamente o mesmo que a conta direta módulo a módulo.
/// </summary>
public class ViableElevationsTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>Mesa de 18,7 m com 14 módulos de 1,303 m e folga de 2 cm, sobra de 10 cm.</summary>
    private const double Comprimento = 18.702;

    private static double Estacao(int coluna) => 0.10 + coluna * (1.303 + 0.02) + 1.303 / 2;

    /// <summary>Amostra com o terreno dado por uma função da estação.</summary>
    private static TableSamples Amostra(Func<double, double?> terreno, int colunas = 14)
    {
        var pontaBaixa = Enumerable.Range(0, colunas)
            .Select(c => new LowEdgeSample(c, Estacao(c), 0, 0, terreno(Estacao(c))))
            .ToList();

        return new TableSamples([], pontaBaixa);
    }

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    // ------------------------------------------------------- terreno plano

    /// <summary>
    /// Terreno plano em 700, faixa de 0,30 a 0,80: a mesa nivelada é viável
    /// com a ponta baixa de 700,30 a 700,80, e fora disso não.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NoPlanoAMesaNiveladaEViavelDentroDaFaixa()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config());

        Assert.Null(viaveis.Problem);
        Assert.False(viaveis.IsEmpty);

        Assert.True(viaveis.IsViable(700.30, 700.30));
        Assert.True(viaveis.IsViable(700.55, 700.55));
        Assert.True(viaveis.IsViable(700.80, 700.80));
        Assert.False(viaveis.IsViable(700.29, 700.29));
        Assert.False(viaveis.IsViable(700.81, 700.81));
    }

    /// <summary>
    /// No plano, com a cota inicial em 700,50, a cota final pode ir de onde
    /// o último módulo chega a 0,30 até onde chega a 0,80. O último módulo
    /// está na estação 17,9 de 18,7: a cota final vai um pouco além da faixa.
    /// Conta: z1 = z0 + (limite − z0)·L/s.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NoPlanoOIntervaloDaCotaFinalEDadoPeloUltimoModulo()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config());
        var inicio = viaveis.Starts.Single(s => Math.Abs(s.StartElevation - 700.50) < 1e-9);

        var ultima = Estacao(13);
        var esperadoMin = 700.50 + (700.30 - 700.50) * Comprimento / ultima;
        var esperadoMax = 700.50 + (700.80 - 700.50) * Comprimento / ultima;

        var faixa = Assert.Single(inicio.EndRanges);

        Assert.Equal(esperadoMin, faixa.Min, 6);
        Assert.Equal(esperadoMax, faixa.Max, 6);
    }

    /// <summary>
    /// O limite de declividade corta o giro: com 0,5° sobre 18,7 m, a cota
    /// final fica a no máximo 0,163 m da inicial.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OLimiteDeDeclividadeCortaOGiro()
    {
        var config = Config() with { MaxLongitudinalSlope = 0.5 * Grau };
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, config);
        var inicio = viaveis.Starts.Single(s => Math.Abs(s.StartElevation - 700.50) < 1e-9);

        var giro = Math.Sin(0.5 * Grau) * Comprimento;
        var faixa = Assert.Single(inicio.EndRanges);

        Assert.Equal(700.50 - giro, faixa.Min, 6);
        Assert.Equal(700.50 + giro, faixa.Max, 6);

        Assert.True(viaveis.IsViable(700.50, 700.50 + giro - 0.001));
        Assert.False(viaveis.IsViable(700.50, 700.50 + giro + 0.001));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void SemLimiteDeDeclividadeOGiroSoDependeDaFaixa()
    {
        var config = Config() with { MaxLongitudinalSlope = null };
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, config);

        // Ponta baixa em 700,30 no início e o último módulo em 700,80: um
        // giro de meio metro que 10° permitiria também, mas sem limite não
        // há o que conferir.
        var ultima = Estacao(13);
        var z1 = 700.30 + (700.80 - 700.30) * Comprimento / ultima;

        Assert.True(viaveis.IsViable(700.30, z1));
        Assert.False(viaveis.IsViable(700.30, z1 + 0.01));
    }

    // -------------------------------------------------------------- rampa

    /// <summary>
    /// Terreno subindo 5 cm por metro ao longo da mesa: a mesa paralela ao
    /// terreno (mesma subida) com 0,50 de folga é viável; a mesa nivelada
    /// não, porque a folga cai a 0,50 − 0,05·17,9 &lt; 0,30 nos últimos módulos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NaRampaAMesaAcompanhaOTerreno()
    {
        var viaveis = ViableElevations.Compute(Amostra(s => 700 + 0.05 * s), Comprimento, Config());

        Assert.True(viaveis.IsViable(700.50, 700.50 + 0.05 * Comprimento));
        Assert.False(viaveis.IsViable(700.50, 700.50));
        Assert.Equal(0, viaveis.Violations(700.50, 700.50 + 0.05 * Comprimento));
        Assert.True(viaveis.Violations(700.50, 700.50) > 0);
    }

    /// <summary>
    /// A rampa a 5% é 2,86°, dentro dos 10°. Com o limite em 1°, a mesa não
    /// consegue acompanhar e nenhuma cota serve: os módulos cobrem 17,2 m de
    /// estação, o terreno sobe 0,86 m nesse vão, e a faixa de 0,50 m mais o
    /// giro de tan(1°) × 17,2 = 0,30 m não absorvem isso. (Com 2° o giro
    /// seria 0,60 m e a mesa voltaria a caber.)
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaMaisIngremeQueOLimiteNaoTemCotaViavel()
    {
        var config = Config() with { MaxLongitudinalSlope = 1 * Grau };
        var viaveis = ViableElevations.Compute(Amostra(s => 700 + 0.05 * s), Comprimento, config);

        Assert.Null(viaveis.Problem);
        Assert.True(viaveis.IsEmpty);
        Assert.Contains("Sem cota viável", viaveis.Describe());
    }

    // ------------------------------------------------------- lombo

    /// <summary>
    /// Plano em 700 com um calombo de 0,60 m sob a coluna 7: com tolerância
    /// zero não há cota (a coluna 7 pede ponta baixa ≥ 700,90 e as outras
    /// pedem ≤ 700,80; a mesa é rígida). Com tolerância de um módulo, o
    /// calombo é ignorado e a mesa nivelada volta a ser viável. É a exceção
    /// única da regra sagrada 4.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AToleranciaDeLomboDeixaUmCalomboPassar()
    {
        TableSamples comCalombo = Amostra(s => Math.Abs(s - Estacao(7)) < 0.01 ? 700.60 : 700);

        var semTolerancia = ViableElevations.Compute(comCalombo, Comprimento, Config());
        Assert.True(semTolerancia.IsEmpty);
        Assert.Equal(1, semTolerancia.Violations(700.50, 700.50));

        var comTolerancia = ViableElevations.Compute(comCalombo, Comprimento, Config() with { BumpToleranceModules = 1 });
        Assert.False(comTolerancia.IsEmpty);
        Assert.True(comTolerancia.IsViable(700.50, 700.50));
        Assert.Equal(1, comTolerancia.ToleratedModules);
    }

    /// <summary>Dois calombos com tolerância de um: continua sem cota.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DoisCalombosComToleranciaDeUmNaoPassam()
    {
        TableSamples dois = Amostra(s =>
            Math.Abs(s - Estacao(3)) < 0.01 || Math.Abs(s - Estacao(10)) < 0.01 ? 700.60 : 700);

        var viaveis = ViableElevations.Compute(dois, Comprimento, Config() with { BumpToleranceModules = 1 });

        Assert.True(viaveis.IsEmpty);
        Assert.False(ViableElevations.Compute(dois, Comprimento, Config() with { BumpToleranceModules = 2 }).IsEmpty);
    }

    /// <summary>
    /// Com tolerância, a mesa pode estourar os módulos do fim e aproveitar o
    /// começo: na rampa a 5%, a mesa nivelada em 700,80 tem folga
    /// 0,80 − 0,05·s, que cai abaixo de 0,30 a partir da estação 10 — as
    /// colunas 7 a 13, sete módulos. Com tolerância 7 é viável; com 6, não.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComToleranciaOsModulosDoFimPodemEstourar()
    {
        var amostra = Amostra(s => 700 + 0.05 * s);

        var sete = ViableElevations.Compute(amostra, Comprimento, Config() with { BumpToleranceModules = 7 });
        var seis = ViableElevations.Compute(amostra, Comprimento, Config() with { BumpToleranceModules = 6 });

        Assert.Equal(7, sete.Violations(700.80, 700.80));
        Assert.True(sete.IsViable(700.80, 700.80));
        Assert.False(seis.IsViable(700.80, 700.80));
    }

    // --------------------------------------------- intervalos = definição

    /// <summary>
    /// O teste que importa: para toda cota inicial da grade e uma malha fina
    /// de cotas finais, "está em algum intervalo" é exatamente "IsViable",
    /// que é a definição módulo a módulo. Em terrenos aleatórios, com e sem
    /// tolerância, com e sem limite de declividade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OsIntervalosDizemOMesmoQueADefinicao()
    {
        var sorteio = new Random(53);

        for (var caso = 0; caso < 30; caso++)
        {
            var base_ = 700 + sorteio.NextDouble() * 10;
            var rampa = (sorteio.NextDouble() - 0.5) * 0.12;
            var ruido = sorteio.NextDouble() * 0.4;
            var cotas = Enumerable.Range(0, 14).Select(_ => sorteio.NextDouble() * ruido).ToArray();

            var amostra = Amostra(s => base_ + rampa * s + cotas[(int)Math.Round((s - Estacao(0)) / 1.323)]);

            var config = Config() with
            {
                BumpToleranceModules = sorteio.Next(4),
                MaxLongitudinalSlope = sorteio.Next(3) == 0 ? null : (2 + sorteio.NextDouble() * 10) * Grau,
            };

            var viaveis = ViableElevations.Compute(amostra, Comprimento, config, step: 0.05);

            Assert.Null(viaveis.Problem);

            foreach (var inicio in viaveis.Starts)
            {
                // Toda cota inicial da grade é múltiplo do passo.
                Assert.Equal(0, Math.Abs(inicio.StartElevation / 0.05 - Math.Round(inicio.StartElevation / 0.05)), 6);

                // Intervalos em ordem, sem se tocar, e não vazios.
                for (var i = 0; i < inicio.EndRanges.Count; i++)
                {
                    Assert.True(inicio.EndRanges[i].Max >= inicio.EndRanges[i].Min - 1e-9);
                    if (i > 0) Assert.True(inicio.EndRanges[i].Min > inicio.EndRanges[i - 1].Max + 1e-9);
                }

                // A malha fina de cotas finais, 5 m para cada lado.
                for (var z1 = inicio.StartElevation - 5; z1 <= inicio.StartElevation + 5; z1 += 0.013)
                {
                    var nosIntervalos = inicio.EndRanges.Any(f => f.Contains(z1));
                    var pelaDefinicao = viaveis.IsViable(inicio.StartElevation, z1);

                    // Na borda de um intervalo os dois podem discordar por
                    // arredondamento; a um milímetro da borda, nunca.
                    var naBorda = inicio.EndRanges.Any(f => Math.Abs(z1 - f.Min) < 1e-3 || Math.Abs(z1 - f.Max) < 1e-3);

                    if (!naBorda) Assert.Equal(pelaDefinicao, nosIntervalos);
                }
            }

            // E toda cota inicial da grade que NÃO está em Starts não tem
            // cota final viável nenhuma (amostrada).
            var presentes = viaveis.Starts.Select(s => Math.Round(s.StartElevation / 0.05)).ToHashSet();

            for (var z0 = base_ - 2; z0 <= base_ + 4; z0 += 0.05)
            {
                var chave = Math.Round(z0 / 0.05);
                if (presentes.Contains(chave)) continue;

                var z0Grade = chave * 0.05;

                for (var z1 = z0Grade - 5; z1 <= z0Grade + 5; z1 += 0.05)
                    Assert.False(viaveis.IsViable(z0Grade, z1), $"z0={z0Grade} z1={z1} é viável e não está em Starts");
            }
        }
    }

    // ---------------------------------------------- estação zero e empate

    /// <summary>
    /// Módulo exatamente na estação zero: a cota final não muda a dele, então
    /// ou está dentro com a inicial ou é uma violação fixa. Com k = 0, cota
    /// inicial que o deixa fora não tem cota final nenhuma; com k = 1, tem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ModuloNaEstacaoZeroEViolacaoFixa()
    {
        // Estações 0, 5, 10 e 15 numa mesa de 18,7; terreno plano em 700.
        var amostra = new TableSamples([], [
            new LowEdgeSample(0, 0, 0, 0, 700),
            new LowEdgeSample(1, 5, 0, 0, 700),
            new LowEdgeSample(2, 10, 0, 0, 700),
            new LowEdgeSample(3, 15, 0, 0, 700),
        ]);

        var semTolerancia = ViableElevations.Compute(amostra, Comprimento, Config());
        var comTolerancia = ViableElevations.Compute(amostra, Comprimento, Config() with { BumpToleranceModules = 1 });

        // 700,20 deixa o módulo da estação zero a 0,20: fora.
        Assert.DoesNotContain(semTolerancia.Starts, s => Math.Abs(s.StartElevation - 700.20) < 1e-9);
        Assert.False(semTolerancia.IsViable(700.20, 700.50));

        // Com um módulo de tolerância, o da estação zero pode estourar, e a
        // final segue a faixa dos outros três: com a final em 700,90, as
        // estações 5, 10 e 15 ficam em 700,39, 700,57 e 700,76.
        var inicio = comTolerancia.Starts.Single(s => Math.Abs(s.StartElevation - 700.20) < 1e-9);
        Assert.NotEmpty(inicio.EndRanges);
        Assert.True(comTolerancia.IsViable(700.20, 700.90));
        Assert.Equal(1, comTolerancia.Violations(700.20, 700.90));

        // E com todos os módulos fixos, a cota final é livre dentro do giro.
        var todosFixos = new TableSamples([], [new LowEdgeSample(0, 0, 0, 0, 700)]);
        var livre = ViableElevations.Compute(todosFixos, Comprimento, Config());
        var em700_50 = livre.Starts.Single(s => Math.Abs(s.StartElevation - 700.50) < 1e-9);
        var faixa = Assert.Single(em700_50.EndRanges);
        Assert.Equal(700.50 - Math.Sin(10 * Grau) * Comprimento, faixa.Min, 9);
        Assert.Equal(700.50 + Math.Sin(10 * Grau) * Comprimento, faixa.Max, 9);
    }

    /// <summary>
    /// O empate de pontas: com módulos nas estações 1 e 6 de uma mesa de 10 m
    /// e a inicial em 700,20, o de 1 m pede z1 ≥ 701,20 e o de 6 m pede
    /// z1 ≤ 701,20. Um único z1 serve, e ele existe: os intervalos são
    /// fechados, e abrir antes de fechar no mesmo Z é o que o preserva.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PontasEmpatadasDaoIntervaloDeUmPontoSo()
    {
        var amostra = new TableSamples([], [
            new LowEdgeSample(0, 1, 0, 0, 700),
            new LowEdgeSample(1, 6, 0, 0, 700),
        ]);

        var viaveis = ViableElevations.Compute(amostra, 10, Config() with { MaxLongitudinalSlope = null });
        var inicio = viaveis.Starts.Single(s => Math.Abs(s.StartElevation - 700.20) < 1e-9);
        var faixa = Assert.Single(inicio.EndRanges);

        Assert.Equal(701.20, faixa.Min, 9);
        Assert.Equal(701.20, faixa.Max, 9);
        Assert.True(viaveis.IsViable(700.20, 701.20));
    }

    // -------------------------------------------------------- sem limite

    /// <summary>
    /// O contraexemplo da revisão: plano, sem limite de declividade,
    /// tolerância 12 em 14 (dois módulos precisam ficar dentro). A mesa pode
    /// ficar íngreme a ponto de a inicial estar em 706 com a final em
    /// 700,109 (as colunas 12 e 13 dentro). Esse par é viável e tem que
    /// estar na grade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SemLimiteAGradeAlcancaAsCotasIngremes()
    {
        var config = Config() with { MaxLongitudinalSlope = null, BumpToleranceModules = 12 };
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, config);

        Assert.False(viaveis.IsUnbounded);
        Assert.True(viaveis.IsViable(706, 700.109));

        var inicio = viaveis.Starts.Single(s => Math.Abs(s.StartElevation - 706) < 1e-9);
        Assert.Contains(inicio.EndRanges, f => f.Contains(700.109));
    }

    /// <summary>
    /// Sem limite e com tolerância que deixa um módulo só dentro (ou nenhum),
    /// qualquer cota inicial serve: o conjunto é ilimitado, e o objeto diz.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SemLimiteEComUmModuloSoExigidoOConjuntoEIlimitado()
    {
        var config = Config() with { MaxLongitudinalSlope = null, BumpToleranceModules = 13 };
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, config);

        Assert.True(viaveis.IsUnbounded);
        Assert.False(viaveis.IsEmpty);

        // Qualquer cota inicial tem alguma final: a que põe a coluna 0 em
        // 700,50 de ponta baixa, por exemplo, mesmo partindo de 750.
        var z1 = 750 + (700.50 - 750) * Comprimento / Estacao(0);
        Assert.True(viaveis.IsViable(750, z1));
    }

    /// <summary>
    /// A grade é completa também sem limite, em terrenos aleatórios: nenhuma
    /// cota inicial fora de Starts tem cota final viável, numa varredura
    /// larga, enquanto o conjunto é limitado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SemLimiteNenhumaCotaViavelFicaForaDaGrade()
    {
        var sorteio = new Random(54);

        for (var caso = 0; caso < 20; caso++)
        {
            var rampa = (sorteio.NextDouble() - 0.5) * 0.2;
            var cotas = Enumerable.Range(0, 14).Select(_ => sorteio.NextDouble() * 0.5).ToArray();
            var amostra = Amostra(s => 700 + rampa * s + cotas[(int)Math.Round((s - Estacao(0)) / 1.323)]);
            var config = Config() with { MaxLongitudinalSlope = null, BumpToleranceModules = sorteio.Next(13) };

            var viaveis = ViableElevations.Compute(amostra, Comprimento, config, step: 0.05);

            if (viaveis.IsUnbounded) continue;

            var presentes = viaveis.Starts.Select(s => s.Key(0.05)).ToHashSet();

            for (var z0 = 690.0; z0 <= 712; z0 += 0.05)
            {
                var chave = (long)Math.Round(z0 / 0.05);
                if (presentes.Contains(chave)) continue;

                var z0Grade = chave * 0.05;

                for (var z1 = z0Grade - 25; z1 <= z0Grade + 25; z1 += 0.1)
                    Assert.False(viaveis.IsViable(z0Grade, z1), $"z0={z0Grade} z1={z1} é viável e não está em Starts");
            }
        }
    }

    // --------------------------------------------------------- utilidades

    /// <summary>
    /// EndRanges por número de violações: cresce com k (mais tolerância,
    /// mais cotas), fecha com a conta direta módulo a módulo, é preso ao
    /// número de módulos, e com Problem não responde nada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OsIntervalosPorViolacaoCrescemComATolerancia()
    {
        var viaveis = ViableElevations.Compute(Amostra(s => 700 + 0.04 * s), Comprimento, Config() with { BumpToleranceModules = 3, MaxLongitudinalSlope = null });

        for (var k = 0; k < 3; k++)
        {
            var menos = viaveis.EndRanges(700.50, k);
            var mais = viaveis.EndRanges(700.50, k + 1);

            for (var z1 = 698.0; z1 <= 704; z1 += 0.01)
            {
                var emMenos = menos.Any(f => f.Contains(z1));
                var emMais = mais.Any(f => f.Contains(z1));
                var naBorda = menos.Concat(mais).Any(f => Math.Abs(z1 - f.Min) < 1e-3 || Math.Abs(z1 - f.Max) < 1e-3);

                if (emMenos) Assert.True(emMais);
                if (!naBorda) Assert.Equal(viaveis.Violations(700.50, z1) <= k, emMenos);
            }
        }

        Assert.Equal(viaveis.EndRanges(700.50, 14), viaveis.EndRanges(700.50, 99));
        Assert.Empty(viaveis.EndRanges(double.NaN, 1));

        var comProblema = ViableElevations.Compute(Amostra(_ => null), Comprimento, Config());
        Assert.Empty(comProblema.EndRanges(700.50, 5));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void OTerrenoMaisAltoEODosModulosComTerreno()
    {
        Assert.Equal(700 + 0.05 * Estacao(13), ViableElevations.Compute(Amostra(s => 700 + 0.05 * s), Comprimento, Config()).HighestGroundOrNull()!.Value, 9);
        Assert.Null(ViableElevations.Compute(Amostra(_ => null), Comprimento, Config()).HighestGroundOrNull());
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void AChaveDaGradeEInteira()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config());

        foreach (var inicio in viaveis.Starts)
            Assert.Equal(inicio.StartElevation, inicio.Key(0.01) * 0.01, 9);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ADeclividadeLongitudinalEOArcoTangenteDoDesnivel()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config());

        Assert.Equal(Math.Asin(0.5 / Comprimento), viaveis.LongitudinalSlope(700, 700.5), 12);
        Assert.Equal(Math.Asin(0.5 / Comprimento), viaveis.LongitudinalSlope(700.5, 700), 12);
        Assert.Equal(Math.PI / 2, viaveis.LongitudinalSlope(700, 800), 12);
        Assert.Equal(0.3, new ElevationRange(1.2, 1.5).Width, 9);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ToleranciaMaiorQueAMesaValeAMesaInteira()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config() with { BumpToleranceModules = 50 });

        Assert.Equal(14, viaveis.ToleratedModules);
        Assert.Equal(14, viaveis.ModuleCount);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void CotaNaNNoTerrenoEProblema()
    {
        var viaveis = ViableElevations.Compute(Amostra(s => s > 10 ? double.NaN : 700), Comprimento, Config());

        Assert.NotNull(viaveis.Problem);
        Assert.Equal(14, viaveis.ModuleCount);
        Assert.False(viaveis.IsViable(700.5, 700.5));
    }

    // ------------------------------------------------------------- recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void ModuloSemTerrenoEProblemaENaoVazioCalado()
    {
        var viaveis = ViableElevations.Compute(Amostra(s => s > 10 ? null : 700), Comprimento, Config());

        Assert.NotNull(viaveis.Problem);
        Assert.Contains("sem terreno", viaveis.Problem);
        Assert.True(viaveis.IsEmpty);
        Assert.False(viaveis.IsViable(700.5, 700.5));
        Assert.Contains("Sem cota viável", viaveis.Describe());
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void MesaSemModuloEProblema()
    {
        var viaveis = ViableElevations.Compute(new TableSamples([], []), Comprimento, Config());

        Assert.NotNull(viaveis.Problem);
        Assert.True(viaveis.IsEmpty);
    }

    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(0.05)]
    [InlineData(double.NaN)]
    [InlineData(500)]
    public void ComprimentoImpossivelERecusado(double comprimento)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViableElevations.Compute(Amostra(_ => 700), comprimento, Config()));
    }

    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(5)]
    public void PassoImpossivelERecusado(double passo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config(), passo));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ConfiguracaoQuebradaERecusada()
    {
        Assert.Throws<InvalidOperationException>(
            () => ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config() with { MinLowEdge = 9 }));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ModuloForaDoComprimentoERecusado()
    {
        var amostra = new TableSamples([], [new LowEdgeSample(0, 25, 0, 0, 700)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => ViableElevations.Compute(amostra, Comprimento, Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void CotaNaoFinitaNaoEViavel()
    {
        var viaveis = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config());

        Assert.False(viaveis.IsViable(double.NaN, 700.5));
        Assert.False(viaveis.IsViable(700.5, double.PositiveInfinity));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ADescricaoDizAFaixaDeCotasIniciais()
    {
        var texto = ViableElevations.Compute(Amostra(_ => 700), Comprimento, Config()).Describe();

        Assert.Contains("cota inicial de", texto);
        Assert.Contains("posições", texto);
        Assert.DoesNotContain(".", texto.Replace("posições", ""));
    }
}
