using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A pergunta "o terreno que estou usando ainda vale?", num lugar só.
///
/// Todo comando que entrega número calculado sobre o terreno precisa fazer
/// essa pergunta antes de responder. Ter a resposta apenas num comando de
/// status seria inútil: quem consulta a cota não vai lembrar de conferir
/// antes, e a resposta velha sai com a mesma cara da certa.
/// </summary>
internal static class TerrenoEnvelhecido
{
    /// <summary>
    /// O aviso a mostrar, ou null quando o terreno corresponde à superfície
    /// como ela está agora.
    /// </summary>
    internal static string? Conferir(Document documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var carimbo = ProvenanceStore.Load(documento.Database);

        // Sem carimbo não há o que comparar. Acontece com terreno processado
        // por uma versão anterior do plugin, e não é motivo de alarme.
        if (carimbo is null) return null;

        return ProvenanceCheck.Warning(carimbo, LerIdentidadeAtual(documento, carimbo.Surface.Handle));
    }

    /// <summary>
    /// A superfície do carimbo, como ela está agora, ou null se ela não
    /// estiver mais no desenho.
    /// </summary>
    internal static SurfaceFingerprint? LerIdentidadeAtual(Document documento, string handle)
    {
        var id = AcharPorHandle(documento.Database, handle);
        if (id is null) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        if (transacao.GetObject(id.Value, OpenMode.ForRead) is not TinSurface superficie) return null;

        var identidade = FingerprintReader.Read(superficie);
        transacao.Commit();

        return identidade;
    }

    /// <summary>
    /// O identificador do objeto com esse handle, ou null se ele não existir
    /// neste desenho.
    /// </summary>
    private static ObjectId? AcharPorHandle(Database database, string handle)
    {
        try
        {
            // O handle é texto hexadecimal no desenho; aqui ele volta a ser o
            // identificador do objeto.
            var id = database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);

            return id.IsNull || id.IsErased ? null : id;
        }
        catch (System.Exception erro)
        {
            // Duas causas bem diferentes caem aqui: o objeto não existe mais
            // (normal, e é o que o carimbo quer detectar) ou o handle gravado
            // está corrompido (anormal). Sem registro, a segunda se disfarçaria
            // de primeira e o usuário leria "a superfície não está mais aqui"
            // sobre uma superfície que está.
            RegistroDeDiagnostico.Registrar(
                $"Não consegui resolver o handle '{handle}' do carimbo.", erro);
            return null;
        }
    }
}
