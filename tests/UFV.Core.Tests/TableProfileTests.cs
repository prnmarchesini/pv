namespace UFV.Core.Tests;

/// <summary>
/// O perfil nomeado: a mesa inteira guardada com um nome, para não digitar
/// dez campos a cada projeto.
///
/// O risco deste passo não é o JSON — é o que ele faz com o tempo. Um perfil
/// gravado hoje será aberto daqui a um ano, por outra versão do plugin, num
/// computador com o separador decimal diferente. Se qualquer uma dessas três
/// coisas mudar o número em silêncio, o projetista monta a usina com a mesa
/// errada e não tem como desconfiar: o nome é o mesmo.
/// </summary>
public class TableProfileTests
{
    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    /// <summary>A mesa do Renan, como ele a descreveu em 23/09/2026.</summary>
    private static TableProfile Perfil() => new(
        "Mesa do Renan 28 módulos",
        new TableLayout(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        new TableFrame(3.00, 2.50, 0.15, 0.07),
        20 * Math.PI / 180);

    // ---------------------------------------------------------- ida e volta

    /// <summary>
    /// O que entra é o que sai. Este é o teste que dá sentido ao passo: um
    /// perfil que volta diferente do que foi gravado é pior que perfil nenhum.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OPerfilVoltaIgualAoQueFoiGravado()
    {
        var original = Perfil();
        var voltou = TableProfile.Parse(original.ToJson());

        Assert.Equal(original, voltou);
    }

    /// <summary>
    /// Campo a campo, e não só pela igualdade do record: se um dia alguém
    /// mexer no Equals, este teste continua apontando o campo perdido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void CadaCampoVoltaComOSeuValor()
    {
        var voltou = TableProfile.Parse(Perfil().ToJson());

        Assert.Equal("Mesa do Renan 28 módulos", voltou.Name);

        Assert.Equal("RSM132-8-720BHDG", voltou.Layout.Module.Model);
        Assert.Equal("Risen", voltou.Layout.Module.Brand);
        Assert.Equal(720, voltou.Layout.Module.PowerWatts, 9);
        Assert.Equal(2.384, voltou.Layout.Module.Height, 9);
        Assert.Equal(1.303, voltou.Layout.Module.Width, 9);
        Assert.Equal(0.033, voltou.Layout.Module.Thickness, 9);

        Assert.Equal(28, voltou.Layout.ModuleCount);
        Assert.Equal(TableArrangement.DoubleRow, voltou.Layout.Arrangement);
        Assert.Equal(0.02, voltou.Layout.HorizontalGap, 9);
        Assert.Equal(0.02, voltou.Layout.VerticalGap, 9);
        Assert.Equal(0.10, voltou.Layout.LeftMargin, 9);
        Assert.Equal(0.10, voltou.Layout.RightMargin, 9);

        Assert.Equal(3.00, voltou.Frame.RafterLength, 9);
        Assert.Equal(2.50, voltou.Frame.PillarAlongRafter, 9);
        Assert.Equal(0.15, voltou.Frame.PillarWidth, 9);
        Assert.Equal(0.07, voltou.Frame.PillarDepth, 9);

        Assert.Equal(20 * Math.PI / 180, voltou.TiltRadians, 12);
    }

    /// <summary>
    /// E o perfil que voltou produz a mesma mesa: 18,702 m de comprimento,
    /// 4,788 m na inclinação. É o que o projetista vai conferir na tela.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OPerfilQueVoltouProduzAMesmaMesa()
    {
        var voltou = TableProfile.Parse(Perfil().ToJson());

        Assert.Equal(18.702, voltou.Layout.Length, 6);
        Assert.Equal(4.788, voltou.Layout.Depth, 6);
    }

    // ------------------------------------------------------------ o formato

    /// <summary>
    /// A inclinação é gravada em GRAUS, e não em radianos.
    ///
    /// É o único lugar do projeto onde grau entra num arquivo, e é de
    /// propósito: um perfil é feito para o projetista abrir num editor de
    /// texto e conferir. "0.3490658503988659" não se confere; "20" se confere
    /// de relance. A arquitetura manda radiano dentro do motor, e o motor
    /// continua em radiano — a conversão acontece na borda, que é aqui.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AInclinacaoEGravadaEmGraus()
    {
        var json = Perfil().ToJson();

        Assert.Contains("\"tiltDegrees\": 20", json);
        Assert.DoesNotContain("0.34906", json);
    }

    /// <summary>
    /// Ponto decimal no arquivo, sempre, em qualquer máquina.
    ///
    /// Gravado com a cultura do sistema, um perfil feito no Brasil viraria
    /// "1,303" e um leitor invariante entenderia 1303 — ou falharia. A vírgula
    /// é para a tela; o arquivo é invariante.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ONumeroNoArquivoUsaPontoDecimal()
    {
        var json = Perfil().ToJson();

        Assert.Contains("1.303", json);
        Assert.DoesNotContain("1,303", json);
    }

    /// <summary>
    /// O arranjo é gravado como texto ("DoubleRow"), e não como o número da
    /// enumeração. Acrescentar um valor no meio da enum renumeraria os
    /// seguintes, e todo perfil antigo passaria a dizer outra coisa — sem
    /// erro nenhum na leitura.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OArranjoEGravadoComoTexto()
    {
        Assert.Contains("DoubleRow", Perfil().ToJson());
    }

    /// <summary>
    /// O perfil carrega a versão do formato. Sem ela, um arquivo de amanhã
    /// seria lido pelo plugin de hoje com os campos que ele reconhece e sem os
    /// que não reconhece — uma mesa parecida, e errada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OArquivoTrazAVersaoDoFormato()
    {
        Assert.Contains("\"formatVersion\": 1", Perfil().ToJson());
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilDeOutraVersaoERecusadoComOMotivo()
    {
        var json = Perfil().ToJson().Replace("\"formatVersion\": 1", "\"formatVersion\": 7");

        var erro = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));

        Assert.Contains("7", erro.Message);
        Assert.Contains("versão", erro.Message);
    }

    // ------------------------------------------------------- o que recusar

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("isto nao e json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    public void TextoQueNaoEPerfilDaErroClaro(string json)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));

        Assert.Contains("perfil", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Campo faltando vira zero na desserialização, e zero passa por qualquer
    /// conferência de nulo. O perfil tem que recusar, nomeando o que está
    /// errado — e não montar uma mesa de comprimento zero.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilComCampoFaltandoERecusado()
    {
        const string json = """
        {
          "formatVersion": 1,
          "name": "Sem módulo",
          "tiltDegrees": 20,
          "layout": { "moduleCount": 28, "arrangement": "DoubleRow" },
          "frame": { "rafterLength": 3, "pillarAlongRafter": 2.5, "pillarWidth": 0.15, "pillarDepth": 0.07 }
        }
        """;

        var erro = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));

        Assert.Contains("layout.", erro.Message);
    }

    /// <summary>
    /// Um perfil que descreve uma mesa impossível não pode ser salvo nem
    /// lido: ele viraria uma mesa impossível com nome bonito, e o nome é
    /// exatamente o que faz ninguém desconfiar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilDeMesaImpossivelNaoEGravado()
    {
        var impossivel = Perfil() with
        {
            Layout = new TableLayout(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        };

        Assert.False(impossivel.IsValid);
        Assert.Throws<InvalidOperationException>(() => impossivel.ToJson());
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilDeEstruturaImpossivelNaoEGravado()
    {
        var impossivel = Perfil() with { Frame = new TableFrame(3.00, 9.00, 0.15, 0.07) };

        Assert.False(impossivel.IsValid);
        Assert.Contains("fora dela", impossivel.WhyInvalid!);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilSemNomeNaoEValido()
    {
        Assert.False((Perfil() with { Name = "   " }).IsValid);
    }

    /// <summary>
    /// A inclinação tem a mesma faixa da fórmula do 3.5: de deitada a em pé.
    /// Um perfil com 120° descreveria uma mesa de cabeça para baixo.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-1)]
    [InlineData(2.0)]
    [InlineData(double.NaN)]
    public void InclinacaoForaDaFaixaNaoEValida(double tilt)
    {
        Assert.False((Perfil() with { TiltRadians = tilt }).IsValid);
    }

    /// <summary>
    /// A tesoura precisa caber nos módulos, que é a exigência do 3.4. Um
    /// perfil que ferisse isso só explodiria na hora de desenhar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilComTesouraMaiorQueOsModulosNaoEValido()
    {
        var ruim = Perfil() with { Frame = new TableFrame(6.00, 2.50, 0.15, 0.07) };

        Assert.False(ruim.IsValid);
        Assert.Contains("tesoura", ruim.WhyInvalid!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O perfil válido vira geometria sem reclamar — é o que prova que ele
    /// guarda tudo o que a mesa precisa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OPerfilValidoViraMesaDesenhavel()
    {
        var perfil = TableProfile.Parse(Perfil().ToJson());

        var geo = TableGeometry.Local(
            perfil.Layout,
            PillarTable.Distribute(perfil.Layout.Length, 3),
            perfil.Frame);

        Assert.Equal(28, geo.Modules.Count);
        Assert.Equal(3.394, geo.PillarRow, 3);
        Assert.Null(UFV.Core.Invariants.RigidTable.Check(geo));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoTrazONomeEOComprimento()
    {
        var texto = Perfil().Describe();

        Assert.Contains("Mesa do Renan", texto);
        Assert.Contains("18,702", texto);

        // "20" sozinho casaria com quase qualquer coisa na frase.
        Assert.Contains("a 20°", texto);
    }

    /// <summary>
    /// O buraco que a revisão do 3.6 achou: o teste principal do passo passava
    /// por SORTE da escolha do ângulo.
    ///
    /// Grau → radiano → grau não fecha em ponto flutuante para 37 dos 901
    /// ângulos de décimo em décimo entre 0° e 90°. Vinte graus fecha; 37,5°
    /// saía como 37.50000000000001 no arquivo e o perfil voltava diferente do
    /// que entrou. Estes cinco são os ângulos que o revisor achou quebrados.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0)]
    [InlineData(8.9)]
    [InlineData(18.7)]
    [InlineData(20)]
    [InlineData(23.7)]
    [InlineData(37.5)]
    [InlineData(69)]
    [InlineData(75)]
    [InlineData(90)]
    public void OPerfilVoltaIgualComQualquerInclinacao(double graus)
    {
        var original = Perfil() with { TiltRadians = graus * Math.PI / 180 };
        var json = original.ToJson();

        Assert.Equal(original, TableProfile.Parse(json));

        // E o número no arquivo é o que o projetista digitou, não o vizinho
        // dele: um perfil existe para ser conferido de relance num editor.
        Assert.Contains(
            $"\"tiltDegrees\": {graus.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            json);
    }

    /// <summary>
    /// Nome com espaço em volta também tem que voltar igual. Trimar só na
    /// gravação fazia o perfil voltar diferente do que entrou.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ONomeVoltaExatamenteComoEntrou()
    {
        foreach (var nome in new[] { "  Mesa com espaço  ", "Mesa \"aspas\"", "Mesa ção ã é", "Mesa 🌞" })
        {
            var original = Perfil() with { Name = nome };

            Assert.Equal(nome, TableProfile.Parse(original.ToJson()).Name);
        }
    }

    /// <summary>
    /// O achado mais perigoso da revisão do 3.6: o arranjo era gravado como
    /// texto, mas a LEITURA aceitava número — inclusive um número que não é
    /// nenhum dos valores.
    ///
    /// Um 7 no arquivo não é 1V nem 2V, toda comparação com 2V dá falso, e a
    /// mesa sai como 1V: 37,224 m no lugar de 18,702 m. O dobro do
    /// comprimento, sem erro nenhum.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("\"Tracker\"")]
    public void ArranjoQueNaoSejaUmDosNomesERecusado(string valor)
    {
        var json = Perfil().ToJson().Replace("\"arrangement\": \"DoubleRow\"", $"\"arrangement\": {valor}");

        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));
    }

    /// <summary>
    /// O outro achado: campo ausente virava zero, e zero é valor legítimo para
    /// folga e para margem. "Faltou" e "vale zero" ficavam indistinguíveis, e
    /// um perfil sem horizontalGap carregava sem um aviso, 26 cm mais curto.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("layout.horizontalGap", "\"horizontalGap\": 0.02,")]
    [InlineData("layout.verticalGap", "\"verticalGap\": 0.02,")]
    [InlineData("layout.leftMargin", "\"leftMargin\": 0.1,")]
    [InlineData("layout.rightMargin", "\"rightMargin\": 0.1")]
    [InlineData("frame.pillarAlongRafter", "\"pillarAlongRafter\": 2.5,")]
    [InlineData("tiltDegrees", "\"tiltDegrees\": 20,")]
    public void CampoAusenteERecusadoPeloNome(string campo, string linha)
    {
        var json = Perfil().ToJson();

        Assert.Contains(linha, json);

        var erro = Assert.Throws<InvalidOperationException>(
            () => TableProfile.Parse(json.Replace(linha, string.Empty)));

        Assert.Contains(campo, erro.Message);
    }

    /// <summary>
    /// Campo que este plugin não conhece é erro, não ruído para ignorar. Quem
    /// acrescentar campo sobe a versão do formato — é para isso que ela existe.
    ///
    /// Sem isto, trocar o nome de um campo na etapa 4 e esquecer de subir a
    /// versão daria arquivos que carregam calados com folga zero.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void CampoDesconhecidoERecusado()
    {
        var json = Perfil().ToJson().Replace(
            "\"formatVersion\": 1,",
            "\"formatVersion\": 1,\n  \"moduleGap\": 0.05,");

        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));
    }

    /// <summary>
    /// Ponto decimal mesmo com a máquina em português.
    ///
    /// O teste anterior rodava na cultura da máquina e passava aqui porque
    /// esta máquina é pt-BR — não provava nada. Agora a cultura é trocada de
    /// propósito, nos dois sentidos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OArquivoUsaPontoDecimalMesmoComAMaquinaEmPortugues()
    {
        var antes = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

            var json = Perfil().ToJson();

            Assert.Contains("1.303", json);
            Assert.DoesNotContain("1,303", json);

            // E a leitura também não depende da cultura.
            Assert.Equal(1.303, TableProfile.Parse(json).Layout.Module.Width, 9);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = antes;
        }
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void NoventaGrausEAceitoEInfinitoNao()
    {
        Assert.True((Perfil() with { TiltRadians = PillarSizing.MaiorInclinacao }).IsValid);
        Assert.False((Perfil() with { TiltRadians = double.PositiveInfinity }).IsValid);
    }
}
