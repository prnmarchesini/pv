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

    /// <summary>Texto usado quando a versao nao pode ser lida da assembly.</summary>
    public const string VersaoDesconhecida = "desconhecida";

    /// <summary>
    /// A linha que o comando UFV_OLA escreve na linha de comando do Civil 3D.
    /// </summary>
    public static string MensagemDeApresentacao(string? versao) =>
        $"{Nome} carregado, versão {Limpar(versao)}";

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
