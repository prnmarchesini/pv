using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// O pacote de identidade que grudamos nas entidades do usuário, num lugar só.
///
/// XData é um pacote invisível preso à entidade, registrado sob um nome próprio
/// onde mais ninguém escreve. Ele tem duas propriedades que o dicionário do
/// desenho não tem, e que são a razão de ser usado:
///
/// - viaja junto na cópia entre desenhos, que é como a área ou o alinhamento
///   chega ao arquivo do colega com nome e parâmetros;
/// - morre com a entidade, que é o que se quer de um dado que só faz sentido
///   enquanto ela existir.
///
/// O formato é sempre o mesmo: tipo, versão, e depois os campos de texto de
/// quem está gravando. O tipo é o que distingue uma área de um alinhamento —
/// os dois moram sob o mesmo nome de aplicativo.
///
/// Estava escrito duas vezes, uma em cada comando. A revisão do 4.2 apontou o
/// óbvio: se repetir o formato do registro central era errado, repetir o do
/// XData também era.
/// </summary>
internal static class PluginXData
{
    /// <summary>
    /// O nome do aplicativo registrado no desenho. É um só para todas as
    /// nossas coisas; quem distingue é o campo de tipo.
    /// </summary>
    internal const string Aplicativo = PluginInfo.PrefixoDeDados;

    /// <summary>
    /// Grava o pacote na entidade, substituindo o anterior.
    /// </summary>
    /// <param name="tipo">O que a entidade é: "Area", "Alinhamento".</param>
    /// <param name="versao">
    /// A versão do formato de quem grava. Um pacote de versão diferente é
    /// tratado como ausente na leitura, pelo mesmo motivo do carimbo: melhor
    /// pedir para refazer do que entender errado o que está escrito.
    /// </param>
    /// <param name="campos">Os campos de texto, na ordem.</param>
    internal static void Save(
        Transaction transacao,
        Entity entidade,
        string tipo,
        int versao,
        params string[] campos)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(entidade);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentNullException.ThrowIfNull(campos);

        GarantirAplicativoRegistrado(transacao, entidade.Database);

        if (!entidade.IsWriteEnabled) entidade.UpgradeOpen();

        using var dados = new ResultBuffer();

        dados.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, Aplicativo));
        dados.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, tipo));
        dados.Add(new TypedValue((int)DxfCode.ExtendedDataInteger32, versao));

        foreach (var campo in campos)
            dados.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, campo ?? string.Empty));

        entidade.XData = dados;
    }

    /// <summary>
    /// Os campos gravados na entidade, ou null se ela não for deste tipo — ou
    /// se o que está lá não puder ser lido.
    /// </summary>
    /// <param name="quantosCampos">Quantos campos de texto são esperados depois do cabeçalho.</param>
    internal static IReadOnlyList<string>? Load(
        Entity entidade,
        string tipo,
        int versao,
        int quantosCampos)
    {
        ArgumentNullException.ThrowIfNull(entidade);

        try
        {
            // GetXDataForApplication devolve só o nosso pedaço, já separado do
            // que outros aplicativos tenham gravado na mesma entidade.
            using var dados = entidade.GetXDataForApplication(Aplicativo);
            if (dados is null) return null;

            var campos = dados.AsArray();

            // [0] é o nome do aplicativo, devolvido sempre; [1] é o tipo e
            // [2] a versão.
            if (campos.Length < 3 + quantosCampos) return null;
            if (campos[1].Value as string != tipo) return null;
            if (Convert.ToInt32(campos[2].Value, CultureInfo.InvariantCulture) != versao) return null;

            var texto = new string[quantosCampos];

            for (var i = 0; i < quantosCampos; i++)
                texto[i] = campos[3 + i].Value as string ?? string.Empty;

            return texto;
        }
        catch (System.Exception erro)
        {
            // XData ilegível é tratado como ausente: a entidade deixa de ser
            // reconhecida como nossa, o que é recuperável pelo reindexar, e
            // não impede o usuário de trabalhar.
            RegistroDeDiagnostico.Registrar($"Não consegui ler o XData de uma entidade ({tipo}).", erro);
            return null;
        }
    }

    /// <summary>
    /// Registra o aplicativo na tabela do desenho, se ainda não estiver.
    ///
    /// Sem este registro o AutoCAD descarta o XData sem avisar: a gravação
    /// parece funcionar e na releitura não há nada.
    /// </summary>
    private static void GarantirAplicativoRegistrado(Transaction transacao, Database database)
    {
        var tabela = (RegAppTable)transacao.GetObject(database.RegAppTableId, OpenMode.ForRead);

        if (tabela.Has(Aplicativo)) return;

        tabela.UpgradeOpen();

        var registro = new RegAppTableRecord { Name = Aplicativo };
        tabela.Add(registro);
        transacao.AddNewlyCreatedDBObject(registro, true);
    }
}
