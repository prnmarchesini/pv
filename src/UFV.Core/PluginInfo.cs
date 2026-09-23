namespace UFV.Core;

/// <summary>
/// Identificacao do plugin. Mora no Core, e nao no Plugin, para a mensagem que
/// o usuario le poder ser testada sem abrir o AutoCAD.
/// </summary>
public static class PluginInfo
{
    /// <summary>Nome do plugin como aparece para o usuario.</summary>
    public const string Nome = "Plugin UFV";

    /// <summary>
    /// Nome do comando de prova de vida, na linha de comando do Civil 3D.
    ///
    /// E const, e nao so um texto solto, porque tres lugares precisam dizer
    /// exatamente o mesmo nome: o atributo CommandMethod que registra o
    /// comando, o botao da ribbon que o dispara e o script do Core Console.
    /// Sendo uma constante, renomear o comando arrasta o botao junto.
    /// </summary>
    public const string ComandoOla = "UFV_OLA";

    /// <summary>
    /// Comando que lista as superfícies do desenho para o usuário escolher
    /// qual é o terreno. Mesma razão de ser constante que <see cref="ComandoOla"/>.
    /// </summary>
    public const string ComandoTerreno = "UFV_TERRENO";

    /// <summary>
    /// Comando que processa a superfície sem perguntar nada, escolhendo a
    /// primeira aproveitável. Existe para o teste de nível 2 poder exercitar
    /// o processamento sem ninguém clicar — o comando do produto continua
    /// exigindo que o usuário confirme.
    /// </summary>
    public const string ComandoTerrenoAutomatico = "UFV_TERRENO_AUTO";

    /// <summary>
    /// Comando que diz se o terreno gravado no desenho ainda corresponde à
    /// superfície como ela está agora.
    /// </summary>
    public const string ComandoTerrenoStatus = "UFV_TERRENO_STATUS";

    /// <summary>
    /// Comando que responde X, Y e Z de um ponto clicado no terreno.
    /// </summary>
    public const string ComandoCoordenada = "UFV_COORD";

    /// <summary>
    /// Comando que define ou corrige a latitude e a longitude do terreno.
    /// Sem ele, um sinal trocado ficaria gravado para sempre — e latitude
    /// trocada põe a usina no hemisfério errado.
    /// </summary>
    public const string ComandoLocalizacao = "UFV_LOCAL";

    /// <summary>
    /// Comando que traça a área de implantação e a assenta no terreno.
    /// </summary>
    public const string ComandoArea = "UFV_AREA";

    /// <summary>
    /// Comando que lista as áreas do desenho, lendo a identidade de cada
    /// polilinha. É por onde se confere que o GUID sobreviveu ao arquivo.
    /// </summary>
    public const string ComandoAreas = "UFV_AREAS";

    /// <summary>
    /// Comando que reconstrói o registro central a partir do XData das
    /// entidades. É o que reconhece uma área copiada de outro desenho.
    /// </summary>
    public const string ComandoReindexar = "UFV_REINDEXAR";

    /// <summary>
    /// Comando que abre a janela da mesa: os campos da estrutura, o
    /// comprimento que sai deles e a planta baixa.
    /// </summary>
    public const string ComandoMesa = "UFV_MESA";

    /// <summary>
    /// Prefixo de tudo que o plugin grava com nome próprio: XData, dicionário
    /// do desenho, dados pendurados no documento (ver 02-arquitetura.md).
    /// </summary>
    public const string PrefixoDeDados = "MARCHENG_UFV";

    /// <summary>Texto usado quando a versao nao pode ser lida da assembly.</summary>
    public const string VersaoDesconhecida = "desconhecida";

    /// <summary>
    /// A linha que o comando UFV_OLA escreve na linha de comando do Civil 3D.
    /// </summary>
    public static string MensagemDeApresentacao(string? versao) =>
        $"{Nome} carregado, versão {VersaoLegivel(versao)}";

    /// <summary>
    /// A versão como o usuário lê: sem o sufixo de build que o SDK acrescenta.
    ///
    /// O sufixo continua valendo onde ele serve — gravado no carimbo do
    /// desenho, ele identifica exatamente qual build processou o terreno.
    /// Numa mensagem de tela, é ruído.
    /// </summary>
    public static string VersaoLegivel(string? versao) => Limpar(versao);

    /// <summary>
    /// Tira o sufixo de build que o SDK acrescenta a versao informativa
    /// ("0.1.0+3f2a1c9" vira "0.1.0") e apara espaco em volta.
    ///
    /// Versao ausente, em branco ou que se resume ao sufixo (um
    /// AssemblyInformationalVersion malformado como "+abc") vira
    /// "desconhecida". Melhor a palavra do que a frase terminando no vazio.
    /// </summary>
    private static string Limpar(string? versao)
    {
        if (string.IsNullOrWhiteSpace(versao)) return VersaoDesconhecida;

        var texto = versao.Trim();

        var mais = texto.IndexOf('+');
        if (mais >= 0) texto = texto[..mais].Trim();

        return texto.Length == 0 ? VersaoDesconhecida : texto;
    }
}
