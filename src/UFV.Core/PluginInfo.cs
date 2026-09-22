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
    /// Prefixo do dicionario de identidade no desenho (ver 02-arquitetura.md).
    /// Fica aqui porque e o mesmo nome em todo lugar e nao pode divergir.
    /// </summary>
    public const string PrefixoDeIdentidade = "MARCHENG_UFV";

    /// <summary>Texto usado quando a versao nao pode ser lida da assembly.</summary>
    public const string VersaoDesconhecida = "desconhecida";

    /// <summary>
    /// A linha que o comando UFV_OLA escreve na linha de comando do Civil 3D.
    /// </summary>
    public static string MensagemDeApresentacao(string? versao)
    {
        var texto = string.IsNullOrWhiteSpace(versao) ? VersaoDesconhecida : Limpar(versao);
        return $"{Nome} carregado, versão {texto}";
    }

    /// <summary>
    /// Tira o sufixo de build que o SDK acrescenta a versao informativa
    /// ("0.1.0+3f2a1c9" vira "0.1.0") e apara espaco em volta.
    /// </summary>
    private static string Limpar(string versao)
    {
        var texto = versao.Trim();
        var mais = texto.IndexOf('+');
        return mais >= 0 ? texto[..mais] : texto;
    }
}
