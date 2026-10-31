namespace Clivus.Core.Tests;

/// <summary>
/// O registro dos motivos por módulo (42.1, Renan: "vou querer apagar as
/// sombras de algumas areas, recaldular de outras"): cada cálculo troca só
/// as mesas do escopo dele; os tópicos e a frase do resumo saem do registro.
/// </summary>
public class ShadowReasonsTests
{
    private static readonly Guid A1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid A2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid B1 = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly DateTime Sete = new(2026, 6, 21, 7, 0, 0);

    private static ModuleShadow M(string handle, Guid mesa, double fracao, ShadowCause causa = ShadowCause.Table) =>
        new(handle, mesa, fracao, causa, Sete, "?");

    [Fact]
    [Trait("Etapa", "42")]
    public void OCalculoDeUmaAreaTrocaSoAsMesasDelaEAsOutrasConvivem()
    {
        var areaA = ShadowReasons.Empty.Replace([A1, A2], "21/06/2026 às 07:00", [M("1A", A1, 1), M("1B", A1, 0.25), M("2A", A2, 0)]);

        // O módulo sem sombra não vira linha; a mesa calculada vira.
        Assert.Equal(2, areaA.Modules.Count);
        Assert.Equal("21/06/2026 às 07:00", areaA.PeriodOf(A2));
        Assert.Null(areaA.Find("2A"));
        Assert.Equal("21/06/2026 às 07:00", areaA.Find("1A")!.Period);

        // A área B depois: a A fica.
        var depois = areaA.Replace([B1], "o dia 21/06/2026", [M("3A", B1, 0.5, ShadowCause.Terrain)]);
        Assert.Equal(3, depois.Modules.Count);
        Assert.Equal("o dia 21/06/2026", depois.PeriodOf(B1));
        Assert.Equal("21/06/2026 às 07:00", depois.PeriodOf(A1));

        // A A de novo, com menos sombra: o 1B sai, o 1A muda, a B fica.
        var deNovo = depois.Replace([A1, A2], "21/06/2026 às 12:00", [M("1A", A1, 0.5)]);
        Assert.Equal(0.5, deNovo.Find("1A")!.Fraction);
        Assert.Null(deNovo.Find("1B"));
        Assert.NotNull(deNovo.Find("3A"));
        Assert.Equal("21/06/2026 às 12:00", deNovo.PeriodOf(A2));

        // Apagar a A: as linhas dela saem; a B fica.
        var semA = deNovo.Remove(m => m == A1 || m == A2);
        Assert.Null(semA.PeriodOf(A1));
        Assert.Null(semA.Find("1A"));
        Assert.Equal(ShadowCause.Terrain, semA.Find("3A")!.Cause);
        Assert.Single(semA.Modules);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OV2VaiEVoltaIgualEOPeriodoPodeTerBarra()
    {
        var registro = ShadowReasons.Empty
            .Replace([A1], "21/06/2026 | teste", [M("1A", A1, 0.375)])
            .Replace([B1], "o ano", [new ModuleShadow("3A", B1, 1, ShadowCause.Object, null, "?")]);

        var linhas = registro.Serialize();
        Assert.Equal("V2", linhas[0]);

        var lido = ShadowReasons.Parse(linhas);
        Assert.Equal(linhas, lido.Serialize());
        Assert.Equal("21/06/2026 | teste", lido.PeriodOf(A1));
        Assert.Equal(0.375, lido.Find("1a")!.Fraction);   // o handle não diferencia maiúscula
        Assert.Equal(Sete, lido.Find("1A")!.When);
        Assert.Null(lido.Find("3A")!.When);
        Assert.Equal(ShadowCause.Object, lido.Find("3A")!.Cause);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OV1DeAntesELidoComAMesaDeCadaModuloEOPrimeiroCalculoGravaEmV2()
    {
        // O formato até 30/10/2026: um cálculo só, do desenho inteiro.
        string[] v1 = ["V1", "21/06/2026 às 09:00", "1A|0.5|Object|2026-06-21T09:00:00", "3A|1|Table|2026-06-21T09:00:00", "lixo"];
        Guid? Mesa(string handle) => handle == "1A" ? A1 : handle == "3A" ? B1 : null;

        var antigo = ShadowReasons.Parse(v1, Mesa);
        Assert.Equal("21/06/2026 às 09:00", antigo.LegacyPeriod);
        Assert.Equal("21/06/2026 às 09:00", antigo.PeriodOf(A2));   // tudo foi calculado de uma vez
        Assert.Equal(B1, antigo.Find("3A")!.Table);
        Assert.Equal(2, antigo.Modules.Count);

        // A área do A1 calculada de novo: o módulo do B1 fica, com o período dele.
        var novo = antigo.Replace([A1], "o dia 21/06/2026", []);
        Assert.Null(novo.LegacyPeriod);
        Assert.Null(novo.Find("1A"));
        Assert.Equal("21/06/2026 às 09:00", novo.Find("3A")!.Period);
        Assert.Equal("V2", novo.Serialize()[0]);

        // O registro zerado (o Apagar de antes deixava só a versão) e o que não se entende.
        Assert.True(ShadowReasons.Parse(["V1"]).IsEmpty);
        Assert.True(ShadowReasons.Parse([]).IsEmpty);
        Assert.True(ShadowReasons.Parse(["V9", "x"]).IsEmpty);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OsTopicosVaoDaAreaAoModuloDoPiorParaOMelhor()
    {
        ShadowTopicInput T(string handle, Guid mesa, string letreiro, string? area, double fracao, int coluna) =>
            new(M(handle, mesa, fracao), area, area is null ? null : "AREA-" + area, letreiro, "MESA-" + letreiro, coluna, 0);

        var topicos = ShadowTopics.Build(
        [
            T("1A", A1, "F1.1", "Área 1", 0.25, 0),
            T("1B", A1, "F1.1", "Área 1", 1, 1),
            T("2A", A2, "F1.2", "Área 1", 0.5, 0),
            T("3A", B1, "F9.9", null, 1, 3),
            T("4A", B1, "F9.9", null, 0, 4),   // sem sombra: fora
        ]);

        Assert.Equal(2, topicos.Count);
        var area = topicos[0];
        Assert.StartsWith("Área 1 — 2 mesa(s), 3 módulo(s) com sombra; pior 100%", area.Text);
        Assert.Equal("AREA-Área 1", area.Handle);

        // A F1.1 (pior 100%) antes da F1.2; dentro dela o 1B (100%) antes do 1A.
        Assert.Equal(["MESA-F1.1", "MESA-F1.2"], area.Children.Select(m => m.Handle));
        Assert.Equal(["1B", "1A"], area.Children[0].Children.Select(m => m.Handle));
        Assert.Equal("col 2 fil 1 — 100% · mesa · 21/06/2026 07:00", area.Children[0].Children[0].Text);

        // Fora das áreas, no fim, mesmo com 100%.
        Assert.StartsWith("Fora das áreas — 1 mesa(s), 1 módulo(s)", topicos[1].Text);
        Assert.Null(topicos[1].Handle);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AFraseDoResumoCabeNumaLinha()
    {
        var modulos = new List<(string, ModuleShadow)>
        {
            ("F3.2", M("1A", A1, 1)),
            ("F3.2", M("1B", A1, 0.5)),
            ("F3.3", M("2A", A2, 0.25, ShadowCause.Object)),
        };

        Assert.Equal(
            "3 módulo(s) com sombra em 2 mesa(s) de Área 1; o pior: F3.2 100% (mesa) às 07:00. Conta em 7,3 s.",
            ShadowTopics.Summary(modulos, "de Área 1", 7.3));

        // Em mais de um dia, a data vai junto.
        modulos.Add(("F3.3", new ModuleShadow("2B", A2, 0.1, ShadowCause.Terrain, Sete.AddDays(30), "?")));
        Assert.Contains("o pior: F3.2 100% (mesa) em 21/06 07:00.", ShadowTopics.Summary(modulos, "de Área 1", 1));

        Assert.Equal("Nenhum módulo da seleção com sombra. Conta em 0,4 s.", ShadowTopics.Summary([], "da seleção", 0.4));
    }
}
