namespace Clivus.Core.Tests;

/// <summary>
/// A fonte única do módulo e a potência trocada pela área (Melhorias de
/// 10/10/2026, itens 14 e 15). Renan: "Carreguei o arquivo pan de 700Wp e a
/// estrutura está com módulo de 720, preciso que o sistema indique
/// divergências ... uma fonte única, na estrutura"; e "Trocar potência do
/// módulo ... isso vai desabilitar todos os cálculos elétricos, vai ter
/// apenas a soma das potências".
/// </summary>
public class ModuleSourceTests
{
    /// <summary>O PAN de verdade que o Renan usou (cópia em tests/dados/pan).</summary>
    private static PanModule PanReal()
    {
        var pan = PanModule.Load(Repositorio.Caminho("tests", "dados", "pan", "Risen_RSM132-8-700BHDG.PAN"), out var faltam);
        Assert.Empty(faltam);
        return pan!;
    }

    private static TableProfile Perfil(string nome = "Mesa 2V28", double watts = 720, int modulos = 28) => new(
        nome,
        new TableLayout(new SolarModule("Risen", "RSM132-8-720BHDG", watts, 2.384, 1.303, 0.033), modulos, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
        20 * Math.PI / 180);

    private static DrawingTable Mesa(TableProfile p) => new(p, new RgbColor(0, 160, 0), true);

    // ------------------------------------------------------------- o PAN

    /// <summary>O PAN real (UTF-8 com BOM, CRLF) é lido com os números do arquivo e o fabricante com a vírgula e o ponto.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void OPanRealELidoDoDisco()
    {
        var pan = PanReal();

        Assert.Equal("Risen Energy Co., Ltd", pan.Manufacturer);
        Assert.Equal("RSM132-8-700BHDG", pan.Model);
        Assert.Equal(700, pan.Pmax);
        Assert.Equal(49.83, pan.Voc);
        Assert.Equal(17.82, pan.Isc);
        Assert.Equal(41.78, pan.Vmp);
        Assert.Equal(16.77, pan.Imp);
        Assert.Equal(-0.1096, pan.VocCoefficient, 9);
        Assert.Equal(-0.24, pan.PowerCoefficient);

        // O que a tela mostra sai arredondado (antes: "-0,10959999999999999 V/°C").
        Assert.DoesNotContain("99999", pan.Describe());
        Assert.Contains("-0", pan.Describe());
    }

    // ------------------------------------------------- PAN na estrutura (15)

    /// <summary>Carregar o PAN na estrutura grava os dados elétricos e a potência dele; nada mais muda.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void OPanNaEstruturaTrocaSoAPotenciaEOsDadosEletricos()
    {
        var antes = Perfil();
        var depois = antes.WithPan(PanReal());

        Assert.Equal(700, depois.Layout.Module.PowerWatts);
        Assert.Equal(PanReal(), depois.ModuleElectrical);
        Assert.Equal(antes.Layout with { Module = antes.Layout.Module with { PowerWatts = 700 } }, depois.Layout);
        Assert.Equal(antes.Frame, depois.Frame);
        Assert.Equal(antes.TiltRadians, depois.TiltRadians);
        Assert.Equal(antes.Name, depois.Name);
    }

    /// <summary>
    /// O perfil com os dados elétricos sai na versão 3 e volta igual; o sem
    /// eles continua na versão 2, igual ao de antes (perfil antigo abre como
    /// sempre abriu).
    /// </summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void PerfilComPanSaiNaVersao3EVoltaIgualOSemPanContinuaNa2()
    {
        var com = Perfil().WithPan(PanReal());
        var json = com.ToJson();

        Assert.Contains("\"formatVersion\": 3", json);
        Assert.Contains("\"moduleElectrical\"", json);
        Assert.Equal(com, TableProfile.Parse(json));
        Assert.Equal(com.ModuleElectrical, TableProfile.Parse(json).ModuleElectrical);

        var sem = Perfil().ToJson();
        Assert.Contains("\"formatVersion\": 2", sem);
        Assert.DoesNotContain("moduleElectrical", sem);
        Assert.Null(TableProfile.Parse(sem).ModuleElectrical);
    }

    /// <summary>Versão e campo têm que bater: a 2 com dados elétricos e a 3 sem eles são recusadas com o motivo.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void VersaoEDadosEletricosTemQueBater()
    {
        var com = Perfil().WithPan(PanReal()).ToJson();
        var erro2 = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(com.Replace("\"formatVersion\": 3", "\"formatVersion\": 2")));
        Assert.Contains("versão 3", erro2.Message);

        var sem = Perfil().ToJson().Replace("\"formatVersion\": 2", "\"formatVersion\": 3");
        var erro3 = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(sem));
        Assert.Contains("moduleElectrical", erro3.Message);
    }

    /// <summary>As mesas do desenho guardam o perfil com o PAN e o devolvem (registro MESAS, versão 1, sem mudar).</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void AsMesasDoDesenhoGuardamOPan()
    {
        var mesas = new[] { Mesa(Perfil().WithPan(PanReal())), Mesa(Perfil("Mesa 2V14", 720, 14)) };
        var lidas = DrawingTables.Decode(DrawingTables.Encode(mesas), out var problemas);

        Assert.Empty(problemas);
        Assert.Equal(PanReal(), lidas[0].Profile.ModuleElectrical);
        Assert.Null(lidas[1].Profile.ModuleElectrical);
    }

    // ------------------------------------------------------- fonte única (15)

    /// <summary>
    /// A estrutura manda; mesa sem estrutura conhecida usa a única estrutura
    /// com dados; o PAN antigo da rota só vale para estrutura sem dados.
    /// </summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void AEstruturaEAFonteUnicaEOPanAntigoEReserva()
    {
        var pan = PanReal();
        var antigo = new PanModule("X", "RSM132-8-720BHDG", 720, 50, 18, 42, 17, -0.125, 0.007, null);
        var com = Mesa(Perfil().WithPan(pan));
        var sem = Mesa(Perfil("Mesa 2V14", 720, 14));

        var fonte = new ModuleSource([com, sem], [antigo], null);

        Assert.True(fonte.ElectricalEnabled);
        Assert.Equal(pan, fonte.ElectricalFor("Mesa 2V28"));
        Assert.Equal(pan, fonte.ElectricalFor("  mesa 2v28 "));
        Assert.Equal(antigo, fonte.ElectricalFor("Mesa 2V14"));
        Assert.Equal(pan, fonte.ElectricalFor(null));

        // Sem PAN antigo, a estrutura sem dados fica sem cálculo (nunca pega o PAN de outra estrutura).
        var semReserva = new ModuleSource([com, sem], [], null);
        Assert.Null(semReserva.ElectricalFor("Mesa 2V14"));

        // Desenho antigo, sem nada na estrutura: o PAN da rota continua valendo (migração).
        var antigoSo = new ModuleSource([sem], [antigo], null);
        Assert.Equal(antigo, antigoSo.ElectricalFor("Mesa 2V14"));
        Assert.Equal(antigo, antigoSo.ElectricalFor(null));
    }

    /// <summary>Dados à mão (sem PAN) valem como fonte: o cálculo precisa do PAN ou da inserção manual.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void ValoresAMaoNaEstruturaValemComoFonte()
    {
        var manual = new PanModule("Risen", "RSM132-8-720BHDG", 720, 50.2, 18.3, 41.7, 17.28, -0.25 / 100 * 50.2, 0, null, null);
        var perfil = Perfil() with { ModuleElectrical = manual };

        Assert.Null(perfil.WhyInvalid);
        Assert.Null(ModuleSource.ProfileDivergence(perfil));
        Assert.Equal(manual, new ModuleSource([Mesa(perfil)], [], null).ElectricalFor("Mesa 2V28"));
        Assert.Equal(perfil, TableProfile.Parse(perfil.ToJson()));
    }

    // ------------------------------------------------------- divergência (15)

    /// <summary>O caso do Renan: PAN de 700 Wp e a estrutura com módulo de 720. O sistema avisa, nos dois sentidos.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void PanDe700ComEstruturaDe720EDivergencia()
    {
        var pan = PanReal();

        // PAN na estrutura, potência ainda de 720.
        var naEstrutura = Perfil() with { ModuleElectrical = pan };
        var aviso = ModuleSource.ProfileDivergence(naEstrutura);
        Assert.NotNull(aviso);
        Assert.Contains("700", aviso);
        Assert.Contains("720", aviso);
        Assert.Single(new ModuleSource([Mesa(naEstrutura)], [], null).Divergences());

        // O PAN antigo, carregado na rota, contra a estrutura de 720 (o print do Renan).
        var naRota = new ModuleSource([Mesa(Perfil())], [pan], null).Divergences();
        Assert.Single(naRota);
        Assert.Contains("rota", naRota[0]);

        // PAN carregado pelo caminho certo (WithPan): estrutura e PAN batem.
        Assert.Null(ModuleSource.ProfileDivergence(Perfil().WithPan(pan)));
        Assert.Empty(new ModuleSource([Mesa(Perfil().WithPan(pan))], [], null).Divergences());
    }

    /// <summary>As mesas já desenhadas com 720 contra a estrutura que agora diz 700: avisa quantas, e só as dela.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void MesasDesenhadasComOutraPotenciaSaoAvisadas()
    {
        var estrutura = Mesa(Perfil().WithPan(PanReal()));
        var outra = Mesa(Perfil("Mesa 2V14", 720, 14));
        var desenhadas = new[]
        {
            new DrawnTablePower(Guid.NewGuid(), "Mesa 2V28", 720),
            new DrawnTablePower(Guid.NewGuid(), "Mesa 2V28", 720),
            new DrawnTablePower(Guid.NewGuid(), "Mesa 2V28", 700),
            new DrawnTablePower(Guid.NewGuid(), "Mesa 2V14", 720),
        };

        var avisos = new ModuleSource([estrutura, outra], [], null).Divergences(desenhadas);

        var aviso = Assert.Single(avisos);
        Assert.Contains("2 mesa(s)", aviso);
        Assert.Contains("Mesa 2V28", aviso);
    }

    // ------------------------------------------- atualizar a potência (15)

    /// <summary>
    /// "Atualizar a potência de todos os módulos sem alterar NADA": só as
    /// mesas da estrutura, para a potência do PAN; a de outra estrutura fica.
    /// </summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void AtualizarAPotenciaMexeSoNasMesasDaEstrutura()
    {
        var estrutura = Mesa(Perfil().WithPan(PanReal()) with { Layout = Perfil().Layout });   // PAN 700, potência 720
        var outra = Mesa(Perfil("Mesa 2V14", 720, 14));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var semNome = Guid.NewGuid();
        var desenhadas = new[]
        {
            new DrawnTablePower(a, "Mesa 2V28", 720),
            new DrawnTablePower(b, "mesa 2v28", null),
            new DrawnTablePower(c, "Mesa 2V14", 720),
            new DrawnTablePower(semNome, null, 720),
        };

        var plano = ModuleSource.PowerUpdate([estrutura, outra], estrutura, desenhadas);

        Assert.Equal(700, ModuleSource.TargetPower(estrutura.Profile));
        Assert.Equal([(a, 700.0), (b, 700.0)], plano);

        // Com uma estrutura só, a mesa sem nome (de antes do 8.6) é dela.
        var so = ModuleSource.PowerUpdate([estrutura], estrutura, desenhadas);
        Assert.Contains((semNome, 700.0), so);

        // Já na potência: nada a mudar.
        Assert.Empty(ModuleSource.PowerUpdate([estrutura], estrutura, [new DrawnTablePower(a, "Mesa 2V28", 700)]));

        // Sem PAN, vale a potência do módulo da estrutura.
        Assert.Equal(720, ModuleSource.TargetPower(outra.Profile));
    }

    // ------------------------------------------- potência pela área (14)

    /// <summary>
    /// Com a potência trocada pela área, os cálculos elétricos desligam (nem
    /// o PAN da estrutura nem o antigo valem) e sobra a soma das potências.
    /// </summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void PotenciaSimuladaDesligaOsCalculosEMandaNoKwp()
    {
        var simulada = new SimulatedModulePower(650);
        var fonte = new ModuleSource([Mesa(Perfil().WithPan(PanReal()))], [PanReal()], simulada);

        Assert.False(fonte.ElectricalEnabled);
        Assert.Null(fonte.ElectricalFor("Mesa 2V28"));
        Assert.Null(fonte.ElectricalFor(null));
        Assert.Equal(650, fonte.PowerFor(720));
        Assert.Equal(650, fonte.PowerFor(null));
        Assert.Contains("650", simulada.Reason());
        Assert.Contains("desligados", simulada.Reason());

        var mesa = new TableIdentity(Guid.NewGuid(), "F1.1", 0, 0, 0, false, null, ModulePowerWatts: 720);
        var mesas = new[] { new CountedTable(mesa, 1, 28, [], 0), new CountedTable(mesa with { Id = Guid.NewGuid() }, 1, 14, [], 0) };

        Assert.Equal(42 * 0.72, LayoutCensus.Count(mesas, 720).PowerKwp, 9);
        var censo = LayoutCensus.Count(mesas, 720, simulada.Watts);
        Assert.Equal(42 * 0.65, censo.PowerKwp, 9);
        Assert.Contains(censo.Lines(), l => l.Contains("trocada pela área"));
    }

    /// <summary>Desfazer ("Usar a configuração da mesa"): sem a simulada, os cálculos e a potência de cada mesa voltam.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void DesfazerAPotenciaSimuladaVoltaOsCalculos()
    {
        var estruturas = new[] { Mesa(Perfil().WithPan(PanReal())) };
        var desfeita = new ModuleSource(estruturas, [], null);

        Assert.True(desfeita.ElectricalEnabled);
        Assert.Equal(PanReal(), desfeita.ElectricalFor("Mesa 2V28"));
        Assert.Equal(720, desfeita.PowerFor(720));
        Assert.Null(desfeita.PowerFor(null));
    }

    /// <summary>O registro da potência simulada: versão 1, um campo, número invariante; fora de 1 a 2000 Wp é recusada.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void ARegistroDaPotenciaSimuladaVaiEVolta()
    {
        var s = new SimulatedModulePower(652.5);
        var texto = RecordTable.Write(SimulatedModulePower.Version, SimulatedModulePower.FieldCount, [s], x => x.ToFields());
        var lido = RecordTable.Read(texto, SimulatedModulePower.Version, SimulatedModulePower.FieldCount, SimulatedModulePower.Parse, "da potência");

        Assert.Null(lido.Problem);
        Assert.Equal(s, Assert.Single(lido.Items));
        Assert.Equal(["652.5"], s.ToFields());

        // Vazio é "sem simulação" (o desfazer grava a lista vazia).
        var vazio = RecordTable.Read(RecordTable.Write<SimulatedModulePower>(1, 1, [], x => x.ToFields()), 1, 1, SimulatedModulePower.Parse, "da potência");
        Assert.Empty(vazio.Items);

        Assert.NotNull(new SimulatedModulePower(0).WhyInvalid());
        Assert.NotNull(new SimulatedModulePower(5000).WhyInvalid());
        Assert.NotNull(new SimulatedModulePower(double.NaN).WhyInvalid());
        Assert.Null(SimulatedModulePower.Parse(["abc"]));
        Assert.Null(SimulatedModulePower.Parse(["0"]));
    }
}
