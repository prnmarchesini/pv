using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Grava e lê a identidade da área na própria entidade, em XData.
///
/// XData é um pacote de dados invisível grudado na entidade, registrado sob um
/// nome próprio onde mais ninguém escreve. Ele tem duas propriedades que o
/// dicionário do desenho não tem, e que são a razão de ele ser usado aqui:
///
/// - viaja junto na cópia entre desenhos, que é como a área chega ao arquivo
///   do colega com nome e parâmetros;
/// - morre com a entidade, que é o que se quer de um dado que só faz sentido
///   enquanto a polilinha existir.
///
/// O limite de 255 bytes por string do XData não incomoda: o que se grava aqui
/// são um GUID, um nome curto e uma data.
/// </summary>
internal static class AreaXData
{
    /// <summary>
    /// O nome do aplicativo registrado no desenho. Precisa existir na tabela
    /// de aplicativos antes de qualquer XData ser gravado — sem isso o AutoCAD
    /// recusa a gravação em silêncio.
    /// </summary>
    internal const string Aplicativo = PluginInfo.PrefixoDeDados;

    /// <summary>
    /// Versão do formato. Um XData de versão diferente é tratado como ausente,
    /// pelo mesmo motivo do carimbo: melhor pedir para refazer do que entender
    /// errado o que está escrito.
    /// </summary>
    private const int VersaoDoFormato = 1;

    /// <summary>Grava a identidade na entidade, substituindo a anterior.</summary>
    internal static void Save(Transaction transacao, Entity entidade, AreaIdentity identidade)
    {
        ArgumentNullException.ThrowIfNull(transacao);
        ArgumentNullException.ThrowIfNull(entidade);
        ArgumentNullException.ThrowIfNull(identidade);

        GarantirAplicativoRegistrado(transacao, entidade.Database);

        if (!entidade.IsWriteEnabled) entidade.UpgradeOpen();

        using var dados = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, Aplicativo),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, AreaIdentity.Tipo),
            new TypedValue((int)DxfCode.ExtendedDataInteger32, VersaoDoFormato),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, identidade.Id.ToString("D")),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, identidade.Name),
            new TypedValue(
                (int)DxfCode.ExtendedDataAsciiString,
                identidade.CreatedAt.ToString("O", CultureInfo.InvariantCulture)));

        entidade.XData = dados;
    }

    /// <summary>
    /// A identidade gravada na entidade, ou null se ela não for nossa — ou se
    /// o que está lá não puder ser lido.
    /// </summary>
    internal static AreaIdentity? Load(Entity entidade)
    {
        ArgumentNullException.ThrowIfNull(entidade);

        try
        {
            // GetXDataForApplication devolve só o nosso pedaço, já separado do
            // que outros aplicativos tenham gravado na mesma entidade.
            using var dados = entidade.GetXDataForApplication(Aplicativo);
            if (dados is null) return null;

            var campos = dados.AsArray();

            // [0] é o nome do aplicativo, devolvido sempre.
            if (campos.Length < 6) return null;
            if (campos[1].Value as string != AreaIdentity.Tipo) return null;
            if (Convert.ToInt32(campos[2].Value, CultureInfo.InvariantCulture) != VersaoDoFormato) return null;

            if (!Guid.TryParse(campos[3].Value as string, out var id)) return null;

            var nome = campos[4].Value as string ?? string.Empty;

            if (!DateTime.TryParse(
                    campos[5].Value as string,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var criadaEm))
            {
                return null;
            }

            var identidade = new AreaIdentity(id, nome, criadaEm);
            return identidade.IsValid ? identidade : null;
        }
        catch (System.Exception erro)
        {
            // XData ilegível é tratado como ausente: a entidade deixa de ser
            // reconhecida como nossa, o que é recuperável pelo reindexar, e
            // não impede o usuário de trabalhar.
            RegistroDeDiagnostico.Registrar("Não consegui ler o XData de uma entidade.", erro);
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
