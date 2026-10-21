using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>
/// Passo 25.4: o caminho que a água faz de um ponto, descendo pelo terreno.
/// É o que decide a bacia de cada canaleta (30.1) e o comprimento L do curso
/// principal (memorial, Tabela 1).
/// </summary>
public class FlowTracerTests
{
    [Fact]
    [Trait("Etapa", "25")]
    public void NaRampaAAguaVaiRetaParaOLesteAteSairDoTerreno()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), 20, 77);

        Assert.Equal(FlowStop.OutsideTerrain, caminho.Stop);
        Assert.All(caminho.Points, p => Assert.Equal(77, p.Y, 6));
        Assert.True(caminho.Points[^1].X > 199, $"parou em x = {caminho.Points[^1].X}");

        // Cota sempre do terreno, e sempre descendo.
        for (var i = 1; i < caminho.Points.Count; i++)
            Assert.True(caminho.Points[i].Z < caminho.Points[i - 1].Z);

        Assert.Equal(caminho.Points[^1].X - 20, caminho.Length2D, 6);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void ComCanaletaNoPeAAguaParaNela()
    {
        // Canaleta norte-sul em x = 150.
        var alvos = new FlowTargets([[new Point3(150, 0, 0), new Point3(150, 200, 0)]]);

        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), 20, 77, alvos);

        Assert.Equal(FlowStop.Target, caminho.Stop);
        Assert.Equal(0, caminho.TargetIndex);
        Assert.Equal(150, caminho.Points[^1].X, 6);
        Assert.Equal(77, caminho.Points[^1].Y, 6);
        Assert.Equal(130, caminho.Length2D, 6);
        Assert.Equal(100 - 0.05 * 20 - (100 - 0.05 * 150), caminho.Drop, 6);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void NoValeAAguaCaiNoTalvegueESegueParaONorte()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Vale(), 160, 20);

        Assert.Equal(FlowStop.OutsideTerrain, caminho.Stop);

        var fim = caminho.Points[^1];
        Assert.True(Math.Abs(fim.X - 100) < 1.0, $"saiu em x = {fim.X:0.##}, longe do talvegue");
        Assert.True(fim.Y > 199, $"saiu em y = {fim.Y:0.##}");

        // Encosta (~60 m) mais talvegue (~180 m): sem zigue-zague inchando o comprimento.
        Assert.True(caminho.Length2D < 60 + 180 + 10, $"comprimento {caminho.Length2D:0.#} m: zigue-zague no talvegue");
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void NoFundoDaBaciaFechadaParaComMotivoFundo()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Funil(), 140, 130);

        Assert.Equal(FlowStop.Sink, caminho.Stop);
        var fim = caminho.Points[^1];
        Assert.True(Math.Abs(fim.X - 100) < 3 && Math.Abs(fim.Y - 100) < 3, $"parou em ({fim.X:0.#}, {fim.Y:0.#}), longe do fundo");
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void NoPlanoNaoAndaEParaComoFundo()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Plano(), 50, 50);

        Assert.Equal(FlowStop.Sink, caminho.Stop);
        Assert.Single(caminho.Points);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void ForaDoTerrenoNemComeca()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), -10, 50);

        Assert.Equal(FlowStop.OutsideTerrain, caminho.Stop);
        Assert.Empty(caminho.Points);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void LimiteDeComprimentoNuncaTrava()
    {
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), 0.5, 50, options: new FlowOptions { MaxLength = 30 });

        Assert.Equal(FlowStop.LengthLimit, caminho.Stop);
        Assert.True(caminho.Length2D <= 30 + 1e-6);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void CanaletaQueOCaminhoSoCruzaNaDiagonalTambemPara()
    {
        // Canaleta em diagonal entre dois passos: o cruzamento do segmento é
        // que pega, não a distância dos pontos.
        var alvos = new FlowTargets([[new Point3(100.13, 0, 0), new Point3(100.37, 200, 0)]]);

        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), 20, 100, alvos);

        Assert.Equal(FlowStop.Target, caminho.Stop);
        Assert.Equal(100.25, caminho.Points[^1].X, 3);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void PatamarPlanoNoMeioDaRampaNaoViraFundo()
    {
        // Nível 2 das bacias em Itatiba (21/10/2026): TIN de curvas de nível tem
        // triângulos planos, e a água "empoçava" num terreno de 10 a 15 %.
        // Rampa de 5 % com um patamar plano de 6 m entre x = 40 e 46.
        var tin = TerrenoDeDrenagem.Grade((x, _) => 100 - 0.05 * Math.Min(x, 40) - 0.05 * Math.Max(0, x - 46), 0, 0, 100, 2);

        var caminho = FlowTracer.Trace(tin, 10, 77);

        Assert.Equal(FlowStop.OutsideTerrain, caminho.Stop);
        Assert.True(caminho.Points[^1].X > 199, $"parou em x = {caminho.Points[^1].X:0.#}");
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void ASondaNaoPulaOMorro()
    {
        // Revisão da etapa 31: patamar plano em 98 (x de 40 a 46), uma leira de
        // 1 m logo depois (x = 48) e o terreno mais baixo além dela. A água
        // empoça no patamar; a sonda em linha reta passaria por cima da leira.
        double Z(double x) => x < 40 ? 100 - 0.05 * x : x <= 46 ? 98 : x <= 48 ? 98 + 0.5 * (x - 46) : x <= 50 ? 99 - 1.5 * (x - 48) : 96 - 0.05 * (x - 50);
        var tin = TerrenoDeDrenagem.Grade((x, _) => Z(x), 0, 0, 100, 2);

        var caminho = FlowTracer.Trace(tin, 10, 77);

        Assert.Equal(FlowStop.Sink, caminho.Stop);
        Assert.True(caminho.Points[^1].X <= 46.5, $"pulou a leira: parou em x = {caminho.Points[^1].X:0.#}");
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void FundoPertoDaBordaContinuaSendoFundo()
    {
        // Funil com o fundo a 20 m da borda: a sonda acha borda, mas é fundo.
        var tin = TerrenoDeDrenagem.Grade((x, y) => 50 + 0.03 * Math.Sqrt((x - 20) * (x - 20) + (y - 100) * (y - 100)), 0, 0, 40, 5);

        var caminho = FlowTracer.Trace(tin, 60, 100);

        Assert.Equal(FlowStop.Sink, caminho.Stop);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void PlanicieDeVerdadeContinuaSendoFundo()
    {
        // Plano maior que o raio da sonda: aí a água empoça mesmo.
        var caminho = FlowTracer.Trace(TerrenoDeDrenagem.Plano(), 100, 100);

        Assert.Equal(FlowStop.Sink, caminho.Stop);
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void PassoOuCelulaZeroSaoRecusadosEmVezDeTravar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FlowTracer.Trace(TerrenoDeDrenagem.Rampa(), 20, 77, options: new FlowOptions { Step = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlowTargets([[new Point3(0, 0, 0), new Point3(1, 1, 0)]], 0));
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void CanaletaDiagonalLongaEAchadaEmQualquerPonto()
    {
        // O índice percorre as células do segmento (não o retângulo dele).
        var alvos = new FlowTargets([[new Point3(-500, -480, 0), new Point3(700, 720, 0)]], 10);

        foreach (var x in new[] { -300.0, 0.0, 123.4, 650.0 })
        {
            // Um passo curto que cruza a canaleta (y = x + 20) em x.
            Assert.True(alvos.TryHit(x - 1, x + 21, x + 1, x + 19, out var i, out var t), $"perdeu a canaleta em x = {x}");
            Assert.Equal(0, i);
            Assert.Equal(0.5, t, 9);
        }
    }
}
