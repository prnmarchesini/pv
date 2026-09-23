using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// O dicionário nomeado do plugin dentro do desenho.
///
/// É o "dicionário nomeado central" de 02-arquitetura.md: um lugar só, com
/// nome próprio onde mais ninguém escreve, que sobrevive a fechar e reabrir o
/// arquivo. Tudo que o plugin precisa lembrar de um desenho para o outro mora
/// aqui — o carimbo do terreno, a localização geográfica, e o que vier.
///
/// Guardar num dicionário, e não em XData de alguma entidade, tem uma razão
/// prática: XData vive preso a um objeto, e some com ele. Isto é do desenho.
/// </summary>
internal static class PluginDictionary
{
    private const string Nome = PluginInfo.PrefixoDeDados;

    /// <summary>Grava um registro sob a chave, substituindo o anterior.</summary>
    internal static void Save(Database database, string chave, ResultBuffer dados)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        ArgumentNullException.ThrowIfNull(dados);

        using var transacao = database.TransactionManager.StartTransaction();

        var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForWrite);

        DBDictionary nosso;
        if (raiz.Contains(Nome))
        {
            nosso = (DBDictionary)transacao.GetObject(raiz.GetAt(Nome), OpenMode.ForWrite);
        }
        else
        {
            nosso = new DBDictionary();
            raiz.SetAt(Nome, nosso);
            transacao.AddNewlyCreatedDBObject(nosso, true);
        }

        var registro = new Xrecord { Data = dados };

        // SetAt substitui o anterior e o apaga: não é preciso remover antes.
        nosso.SetAt(chave, registro);
        transacao.AddNewlyCreatedDBObject(registro, true);

        transacao.Commit();
    }

    /// <summary>
    /// O registro gravado sob a chave, ou null se não houver — ou se o que
    /// estiver lá não puder ser lido.
    /// </summary>
    internal static ResultBuffer? Load(Database database, string chave)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);

        try
        {
            using var transacao = database.TransactionManager.StartOpenCloseTransaction();

            var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!raiz.Contains(Nome)) return null;

            var nosso = (DBDictionary)transacao.GetObject(raiz.GetAt(Nome), OpenMode.ForRead);
            if (!nosso.Contains(chave)) return null;

            var registro = (Xrecord)transacao.GetObject(nosso.GetAt(chave), OpenMode.ForRead);

            // O buffer é copiado: o original morre com a transação, e quem
            // chamou ainda vai lê-lo depois.
            var copia = registro.Data is null ? null : new ResultBuffer(registro.Data.AsArray());

            transacao.Commit();
            return copia;
        }
        catch (System.Exception erro)
        {
            // Registro ilegível é tratado como ausente. A alternativa seria
            // impedir o usuário de trabalhar por causa de um dado auxiliar.
            RegistroDeDiagnostico.Registrar($"Não consegui ler '{chave}' do dicionário do desenho.", erro);
            return null;
        }
    }
}
