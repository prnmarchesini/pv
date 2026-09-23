using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// As medidas da malha — cota mínima e máxima, área em planta e área no
/// espaço — são os números que o usuário confere contra as propriedades da
/// superfície no Civil 3D. Se elas estiverem erradas, a conferência dele dá
/// falso negativo e a desconfiança recai sobre o que estava certo.
///
/// Todos os casos aqui são analíticos: a resposta certa é calculável à mão.
/// </summary>
public class TinMedidasTests
{
    /// <summary>Quadrado de <paramref name="lado"/> metros, plano e horizontal na cota informada.</summary>
    private static Tin Plataforma(double lado, double cota)
    {
        Point3 P(double x, double y) => new(x, y, cota);

        return new Tin(
        [
            new Triangle(P(0, 0), P(lado, 0), P(lado, lado)),
            new Triangle(P(0, 0), P(lado, lado), P(0, lado)),
        ]);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AreaEmPlantaDeUmQuadradoEOQuadradoDoLado()
    {
        var tin = Plataforma(100, cota: 50);

        Assert.Equal(10_000, tin.Area2D, 6);

        // Terreno horizontal: a área no espaço é igual à projetada.
        Assert.Equal(10_000, tin.Area3D, 6);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TerrenoHorizontalTemCotaMinimaIgualAMaxima()
    {
        var tin = Plataforma(100, cota: 632.5);

        Assert.Equal(632.5, tin.MinZ, 9);
        Assert.Equal(632.5, tin.MaxZ, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AreaNoEspacoCresceComAInclinacao()
    {
        // Rampa a 45 graus (z = x): cada metro quadrado em planta corresponde
        // a raiz de 2 metros quadrados de chão.
        Point3 P(double x, double y) => new(x, y, x);

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(100, 0), P(100, 100)),
            new Triangle(P(0, 0), P(100, 100), P(0, 100)),
        ]);

        Assert.Equal(10_000, tin.Area2D, 6);
        Assert.Equal(10_000 * Math.Sqrt(2), tin.Area3D, 6);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AreaNoEspacoSegueOGradienteNosDoisEixos()
    {
        // z = 2x + 3y: o fator é a raiz de 1 + 2² + 3².
        Point3 P(double x, double y) => new(x, y, 2 * x + 3 * y);

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(50, 0), P(50, 50)),
            new Triangle(P(0, 0), P(50, 50), P(0, 50)),
        ]);

        Assert.Equal(2_500, tin.Area2D, 6);
        Assert.Equal(2_500 * Math.Sqrt(14), tin.Area3D, 6);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void CotasSaemDosVerticesDaMalha()
    {
        Point3 P(double x, double y, double z) => new(x, y, z);

        var tin = new Tin(
        [
            new Triangle(P(0, 0, 610.25), P(10, 0, 615.00), P(10, 10, 620.75)),
            new Triangle(P(0, 0, 610.25), P(10, 10, 620.75), P(0, 10, 612.10)),
        ]);

        Assert.Equal(610.25, tin.MinZ, 9);
        Assert.Equal(620.75, tin.MaxZ, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TrianguloDescartadoNaoEntraNasMedidas()
    {
        // A fatia tem cota absurda e área nenhuma. Como ela não responde cota,
        // também não pode aparecer no resumo: o usuário compararia com o Civil
        // 3D e veria um número que não existe no desenho dele.
        var boa = new Triangle(
            new Point3(0, 0, 100), new Point3(10, 0, 100), new Point3(10, 10, 100));

        var parede = new Triangle(
            new Point3(0, 0, 0), new Point3(10, 0, 0), new Point3(5, 0, 9_999));

        var tin = new Tin([boa, parede]);

        Assert.Equal(1, tin.TriangleCount);
        Assert.Equal(1, tin.DiscardedTriangleCount);
        Assert.Equal(100, tin.MinZ, 9);
        Assert.Equal(100, tin.MaxZ, 9);
        Assert.Equal(50, tin.Area2D, 9);

        // A parede tem 9 999 m de altura: se entrasse na conta, a área do
        // terreno explodiria. Ela é horizontal, então as duas áreas coincidem.
        Assert.Equal(50, tin.Area3D, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TrianguloHorarioSomaAreaEmVezDeSubtrair()
    {
        // O Civil 3D não garante que todos os triângulos girem para o mesmo
        // lado. Sem tomar o valor absoluto, um horário subtrai a área de um
        // anti-horário, e o terreno sai menor do que é — plausível, e errado.
        //
        // Este par cobre o mesmo quadrado do primeiro teste, mas com o segundo
        // triângulo invertido: se a área saísse assinada, daria zero.
        Point3 P(double x, double y) => new(x, y, 100);

        var antiHorario = new Triangle(P(0, 0), P(100, 0), P(100, 100));
        var horario = new Triangle(P(0, 0), P(0, 100), P(100, 100));

        Assert.True(antiHorario.DoubleSignedArea2D > 0);
        Assert.True(horario.DoubleSignedArea2D < 0, "O segundo triângulo precisa girar ao contrário.");

        var tin = new Tin([antiHorario, horario]);

        Assert.Equal(10_000, tin.Area2D, 6);
        Assert.Equal(10_000, tin.Area3D, 6);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AreaDoTerrenoNuncaEMenorQueAEmPlanta()
    {
        // O único invariante que vale para qualquer malha: o chão inclinado
        // tem mais superfície do que a sombra dele no mapa, e o plano tem a
        // mesma. Menor seria erro de conta.
        var sorteio = new Random(20260922);

        for (var caso = 0; caso < 50; caso++)
        {
            var triangulos = new List<Triangle>();

            for (var n = 0; n < 20; n++)
            {
                Point3 Sortear() => new(
                    sorteio.NextDouble() * 200,
                    sorteio.NextDouble() * 200,
                    sorteio.NextDouble() * 80);

                triangulos.Add(new Triangle(Sortear(), Sortear(), Sortear()));
            }

            var tin = new Tin(triangulos);
            if (tin.TriangleCount == 0) continue;

            Assert.True(
                tin.Area3D >= tin.Area2D - 1e-9,
                $"Área do terreno ({tin.Area3D}) menor que a projetada ({tin.Area2D}).");
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaVaziaNaoInventaMedida()
    {
        var tin = new Tin([]);

        Assert.Equal(0, tin.MinZ);
        Assert.Equal(0, tin.MaxZ);
        Assert.Equal(0, tin.Area2D);
        Assert.Equal(0, tin.Area3D);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MedidasEmCoordenadaUtm()
    {
        // Área de 12 ha, que é a ordem de grandeza de uma usina do projeto,
        // com o terreno lá longe da origem.
        const double x0 = 512_345.678;
        const double y0 = 7_456_789.012;

        Point3 P(double dx, double dy) => new(x0 + dx, y0 + dy, 600 + 0.01 * dx);

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(400, 0), P(400, 300)),
            new Triangle(P(0, 0), P(400, 300), P(0, 300)),
        ]);

        // 400 × 300 = 120 000 m², ou 12 hectares. A tolerância é apertada de
        // propósito: o erro real medido nessa malha fica na casa de 1e-9 m²,
        // então 1e-6 ainda deixa três ordens de folga e mesmo assim acusaria
        // qualquer perda de precisão de verdade.
        Assert.Equal(120_000, tin.Area2D, 6);
        Assert.Equal(600, tin.MinZ, 6);
        Assert.Equal(604, tin.MaxZ, 6);

        // Rampa de 1 cm por metro: o fator é a raiz de 1 + 0,01².
        Assert.Equal(120_000 * Math.Sqrt(1 + 0.01 * 0.01), tin.Area3D, 6);
    }
}
