namespace Clivus.Core.Tests;

/// <summary>Passo 8.5: as mesas cadastradas no desenho.</summary>
public class DrawingTablesTests
{
    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableProfile Perfil(string nome, int modulos) =>
        new(nome, new TableLayout(Risen(), modulos, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0) { MinEmbedment = 1.1 }, 15 * Math.PI / 180);

    [Fact]
    [Trait("Etapa", "8")]
    public void IdaEVoltaPeloRegistro()
    {
        var mesas = new[]
        {
            new DrawingTable(Perfil("Mesa 2V28", 28), new RgbColor(0, 160, 0), true),
            new DrawingTable(Perfil("Mesa 2V14", 14), new RgbColor(255, 128, 0), false),
        };

        var campos = DrawingTables.Encode(mesas);
        var volta = DrawingTables.Decode(campos, out var problemas);

        Assert.Empty(problemas);
        Assert.Equal(mesas, volta);

        // O perfil não cabe num valor só: vai em pedaços de no máximo 200.
        Assert.All(campos, c => Assert.True(c.Length <= 200));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void RegistroVazioOuDeOutraVersao()
    {
        Assert.Empty(DrawingTables.Decode(null, out var p1));
        Assert.Empty(p1);

        Assert.Empty(DrawingTables.Decode(["9", "1"], out var p2));
        Assert.Single(p2);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void MesaIlegivelFicaDeForaComOMotivo()
    {
        var boa = new DrawingTable(Perfil("Boa", 28), new RgbColor(0, 160, 0), true);
        var campos = DrawingTables.Encode([boa]).ToList();

        // Uma segunda mesa com JSON que não é perfil.
        campos[1] = "2";
        campos.AddRange(["#FF8000", "1", "1", "{\"lixo\":1}"]);

        var volta = DrawingTables.Decode(campos, out var problemas);

        Assert.Equal([boa], volta);
        Assert.Contains("mesa 2", Assert.Single(problemas));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void EmUsoNaOrdemDaListaQueEAPrioridade()
    {
        // Renan, 02/10/2026: "o primeiro da lista vai ser a prioridade".
        var mesas = new[]
        {
            new DrawingTable(Perfil("Curta", 14), RgbColor.Red, true),
            new DrawingTable(Perfil("Fora", 28), RgbColor.Red, false),
            new DrawingTable(Perfil("Longa", 28), RgbColor.Red, true),
        };

        Assert.Equal(["Curta", "Longa"], DrawingTables.InUse(mesas).Select(m => m.Name));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void SemNenhumaMarcadaOMotorUsaALista()
    {
        // 02/10/2026: lista 28, 14 sem marca saiu toda de 14 (valia a mesa da janela de Mesa).
        var longa = new DrawingTable(Perfil("Longa", 28), RgbColor.Red, false);
        var curta = new DrawingTable(Perfil("Curta", 14), RgbColor.Red, false);

        Assert.Equal(["Longa", "Curta"], DrawingTables.ForEngine([longa, curta]).Select(m => m.Name));
        Assert.Equal(["Curta"], DrawingTables.ForEngine([longa, curta with { Use = true }]).Select(m => m.Name));
        Assert.Empty(DrawingTables.ForEngine([]));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void NomeRepetidoOuEmBrancoERecusado()
    {
        var a = new DrawingTable(Perfil("Mesa", 28), RgbColor.Red, true);
        var b = new DrawingTable(Perfil("mesa ", 14), RgbColor.Blue, true);

        Assert.Contains("duas mesas", DrawingTables.WhyInvalid([a, b]));
        Assert.Null(DrawingTables.WhyInvalid([a]));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ACorDaProximaMesaEAPrimeiraLivre()
    {
        var a = new DrawingTable(Perfil("A", 28), DrawingTables.Palette[0], true);

        Assert.Equal(DrawingTables.Palette[0], DrawingTables.NextColor([]));
        Assert.Equal(DrawingTables.Palette[1], DrawingTables.NextColor([a]));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AchaPeloNome()
    {
        var a = new DrawingTable(Perfil("Mesa 2V28", 28), RgbColor.Red, true);

        Assert.Same(a, DrawingTables.Find([a], " mesa 2v28 "));
        Assert.Null(DrawingTables.Find([a], "outra"));
    }
    /// <summary>
    /// 05/10/2026, bug do Renan: abriu "Mesa 28 módulos" pelas Configurações,
    /// trocou para 56, "Salvar perfil", confirmou a substituição e fechou. A
    /// janela devolvia null (fechada sem "Usar esta mesa") e a mesa do
    /// desenho ficava com 28. Gravado com o mesmo nome, o gravado volta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void SalvarPerfilComOMesmoNomeAtualizaAMesaAberta()
    {
        var aberta = Perfil("Mesa 28 módulos", 28);
        var salva = Perfil("Mesa 28 módulos", 56);

        var volta = DrawingTables.AfterEdit(aberta.Name, confirmada: null, salva: salva);

        Assert.Same(salva, volta);
        Assert.Equal(56, volta!.Layout.ModuleCount);

        // Na lista das Configurações, a mesa aberta passa a ter 56.
        var mesas = new List<DrawingTable> { new(aberta, new RgbColor(0, 160, 0), true) };
        mesas[0] = mesas[0] with { Profile = volta };
        Assert.Equal(56, DrawingTables.Find(mesas, "Mesa 28 módulos")!.Profile.Layout.ModuleCount);

        // Maiúscula e espaço nas pontas não fazem outro nome (mesma regra do Find).
        Assert.Same(salva, DrawingTables.AfterEdit(" mesa 28 MÓDULOS ", null, salva));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void UsarEstaMesaValeAntesDoPerfilSalvo()
    {
        var salva = Perfil("Mesa 28 módulos", 56);
        var confirmada = Perfil("Mesa 28 módulos", 14);

        Assert.Same(confirmada, DrawingTables.AfterEdit("Mesa 28 módulos", confirmada, salva));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void SalvoComOutroNomeEUmaCopiaENaoMexeNaMesaAberta()
    {
        var copia = Perfil("Mesa 56 módulos", 56);

        Assert.Null(DrawingTables.AfterEdit("Mesa 28 módulos", null, copia));
        Assert.Null(DrawingTables.AfterEdit("Mesa 28 módulos", null, null));
        Assert.Null(DrawingTables.AfterEdit(null, null, copia));
    }

    /// <summary>
    /// A parte do disco do mesmo bug: gravar 56 por cima de 28 com o mesmo
    /// nome deixa 56 no arquivo (a biblioteca nunca foi o problema).
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void SubstituirOPerfilDe28Por56GravaOs56()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clivus-perfis-" + Guid.NewGuid().ToString("N"));

        try
        {
            var store = new TableProfileStore(pasta);
            store.Save(Perfil("Mesa 28 módulos", 28));
            Assert.True(store.Exists("Mesa 28 módulos"));

            store.Save(Perfil("Mesa 28 módulos", 56));

            Assert.Equal(56, store.Load("Mesa 28 módulos").Layout.ModuleCount);
            Assert.Equal(["Mesa 28 módulos"], store.List());
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }
}
