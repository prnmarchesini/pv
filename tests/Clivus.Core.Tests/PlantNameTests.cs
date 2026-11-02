namespace Clivus.Core.Tests;

/// <summary>
/// O nome da usina no 3D (42.10, Renan, 31/10/2026: "eu exporto e fica o nome
/// do arquivo do dwg, isso é ruim"): o gravado no desenho manda; sem ele, o do
/// arquivo, exatamente como antes; o digitado sai limpo; aceitar o nome de
/// sempre não mexe no desenho; e o nome dado em outro arquivo (o "Salvar como"
/// da plataforma A para a B) é avisado.
/// </summary>
[Trait("Etapa", "42")]
public class PlantNameTests
{
    private const string Arquivo = @"C:\Projetos\Panama\PLAT A_Perfiles.dwg";

    [Fact]
    public void O_nome_gravado_manda()
    {
        Assert.Equal("UFV Aurora", PlantName.Resolve("UFV Aurora", Arquivo, "Usina"));
    }

    [Fact]
    public void Sem_nome_gravado_vale_o_nome_do_arquivo_sem_pasta_e_sem_extensao()
    {
        Assert.Equal("PLAT A_Perfiles", PlantName.Resolve(null, Arquivo, "Usina"));
        Assert.Equal("PLAT A_Perfiles", PlantName.Resolve("   ", Arquivo, "Usina"));
        Assert.Equal("Drawing1", PlantName.Resolve(null, "Drawing1.dwg", "Usina"));
    }

    [Fact]
    public void Sem_nome_gravado_o_nome_do_arquivo_vai_como_sempre_foi_sem_limpar()
    {
        // O servidor agrupa os projetos pelo nome: limpar o do arquivo (os dois
        // espaços, o corte em 100) abriria um projeto novo para o mesmo desenho.
        var longo = new string('p', 120);

        Assert.Equal("UFV  Aurora", PlantName.Resolve(null, @"C:\x\UFV  Aurora.dwg", "Usina"));
        Assert.Equal(longo, PlantName.Resolve(null, $@"C:\x\{longo}.dwg", "Usina"));
    }

    [Fact]
    public void Sem_nome_e_sem_arquivo_vale_o_padrao()
    {
        Assert.Equal("Usina", PlantName.Resolve(null, null, "Usina"));
        Assert.Equal("Usina", PlantName.Resolve("", "  ", "Usina"));
        Assert.Equal(string.Empty, PlantName.FileName(null));
    }

    [Theory]
    [InlineData("  UFV Aurora  ", "UFV Aurora")]
    [InlineData("UFV\r\nAurora", "UFV Aurora")]
    [InlineData("UFV\t\tAurora   Panamá", "UFV Aurora Panamá")]
    [InlineData("UFV\u0007Aurora", "UFV Aurora")]
    [InlineData("Usina São João — 11,9 MWp", "Usina São João — 11,9 MWp")]
    public void O_nome_digitado_sai_limpo(string digitado, string esperado)
    {
        Assert.Equal(esperado, PlantName.Clean(digitado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    [InlineData("\u0007\u0001")]
    public void Nome_vazio_nao_serve(string? digitado)
    {
        Assert.Null(PlantName.Clean(digitado));
    }

    [Fact]
    public void Nome_longo_e_cortado_no_limite()
    {
        var nome = PlantName.Clean(new string('a', 99) + " bcd");

        Assert.Equal(new string('a', 99), nome);
        Assert.Equal(PlantName.MaxLength, PlantName.Clean(new string('x', 250))!.Length);
    }

    [Fact]
    public void O_corte_nao_parte_um_caractere_em_dois()
    {
        // Um emoji (um par de surrogates no UTF-16) começando bem no último caractere aceito.
        var nome = PlantName.Clean(new string('a', PlantName.MaxLength - 1) + "\U0001F31E" + "fim");

        Assert.Equal(new string('a', PlantName.MaxLength - 1), nome);
        Assert.False(char.IsHighSurrogate(nome![^1]));
    }

    [Fact]
    public void Aceitar_o_nome_do_arquivo_sem_nada_gravado_nao_mexe_no_desenho()
    {
        Assert.False(PlantName.NeedsSaving("PLAT A_Perfiles", null, null, Arquivo));
        Assert.True(PlantName.NeedsSaving("UFV Aurora", null, null, Arquivo));

        // O nome do arquivo com dois espaços, aceito na janela, chega limpo e continua sendo ele.
        Assert.False(PlantName.NeedsSaving("UFV Aurora", null, null, @"C:\x\UFV  Aurora.dwg"));
    }

    [Fact]
    public void Com_nome_gravado_so_grava_quando_muda()
    {
        Assert.False(PlantName.NeedsSaving("UFV Aurora", "UFV Aurora", "PLAT A_Perfiles", Arquivo));
        Assert.True(PlantName.NeedsSaving("UFV Aurora II", "UFV Aurora", "PLAT A_Perfiles", Arquivo));

        // Voltar ao nome do arquivo com outro gravado é mudar: o gravado sai.
        Assert.True(PlantName.NeedsSaving("PLAT A_Perfiles", "UFV Aurora", "PLAT A_Perfiles", Arquivo));
    }

    [Fact]
    public void O_nome_dado_em_outro_arquivo_e_avisado_e_confirmar_passa_a_ser_deste()
    {
        const string copia = @"C:\Projetos\Panama\PLAT B_Perfiles.dwg";

        Assert.True(PlantName.GivenInOtherFile("PLAT A_Perfiles", copia));
        Assert.False(PlantName.GivenInOtherFile("PLAT A_Perfiles", Arquivo));
        Assert.False(PlantName.GivenInOtherFile("plat a_perfiles", Arquivo));
        Assert.False(PlantName.GivenInOtherFile(null, copia));

        // Confirmar o mesmo nome na cópia grava de novo, agora com o arquivo dela, e o aviso some.
        Assert.True(PlantName.NeedsSaving("UFV Aurora", "UFV Aurora", "PLAT A_Perfiles", copia));
        Assert.False(PlantName.NeedsSaving("UFV Aurora", "UFV Aurora", "PLAT B_Perfiles", copia));
    }

    [Fact]
    public void O_nome_vai_no_desenho_e_no_titulo_do_envio()
    {
        var cena = new Scene3D("Usina São João \"Norte\" <1>", null, [], [], [], []);
        var corpo = Viewer3DPage.PublishBody(cena, "0.1.10", cena.Title);

        Assert.Contains("\"desenho\":\"Usina São João \\\"Norte\\\" \\u003c1>\"", corpo, StringComparison.Ordinal);
        Assert.Contains("\"titulo\":\"Usina São João \\\"Norte\\\" \\u003c1>\"", corpo, StringComparison.Ordinal);
    }
}
