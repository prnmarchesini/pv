using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.MigracaoDoNome))]

namespace Clivus.Plugin;

/// <summary>
/// A troca do nome para Clivus Solar (Renan, 04/10/2026: "vai chamar Clivus
/// Solar, troque tudo do sistema, pastas, código, layers etc"). Desenho feito
/// antes tem tudo com o prefixo antigo: o XData das peças, as camadas, os
/// blocos de pilar, módulo e árvore, o dicionário do desenho (com as
/// configurações, que guardam nomes de camada). Ao abrir, tudo passa para o
/// prefixo novo, uma vez; o registro do aplicativo antigo sai do desenho.
///
/// O prefixo antigo não fica escrito aqui (Renan: nenhuma menção ao nome de
/// antes): ele é achado no próprio desenho, como o aplicativo de XData cujo
/// nome termina em "_UFV", o formato que o plugin usava.
///
/// Só o que é do plugin muda: camada e bloco cujo nome COMEÇA com o prefixo
/// antigo e o nosso aplicativo de XData. Os estilos e as camadas do usuário
/// ficam como estão.
/// </summary>
public static class MigracaoDoNome
{
    private const string Novo = PluginInfo.PrefixoDeDados;

    /// <summary>O fim do nome do aplicativo de XData de antes da troca de nome.</summary>
    private const string FimAntigo = "_UFV";

    /// <summary>
    /// O prefixo antigo deste desenho: o aplicativo de XData registrado (ou a
    /// chave do dicionário do desenho) cujo nome termina em "_UFV". Null se
    /// não há.
    /// </summary>
    internal static string? PrefixoAntigo(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var apps = (RegAppTable)transacao.GetObject(database.RegAppTableId, OpenMode.ForRead);
        foreach (ObjectId id in apps)
        {
            var nome = ((RegAppTableRecord)transacao.GetObject(id, OpenMode.ForRead)).Name;
            if (EAntigo(nome)) return nome;
        }

        var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        foreach (DBDictionaryEntry entrada in raiz)
            if (EAntigo(entrada.Key)) return entrada.Key;

        return null;
    }

    private static bool EAntigo(string nome) =>
        nome.Length > FimAntigo.Length && nome.EndsWith(FimAntigo, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(nome, Novo, StringComparison.OrdinalIgnoreCase);

    /// <summary>CLIVUS_MIGRAR: migra o desenho aberto, se ele tiver coisas do nome antigo.</summary>
    [CommandMethod(PluginInfo.ComandoMigrar)]
    public static void Migrar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage($"\n{Migrar(documento.Database) ?? Tr.T("MIGRAR Nada do nome antigo neste desenho.")}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao migrar o desenho para o nome novo.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui migrar o desenho: {0}\n", erro.Message));
        }
    }

    /// <summary>Se o desenho tem algo do nome antigo (aplicativo de XData ou dicionário).</summary>
    internal static bool TemNomeAntigo(Database database) => PrefixoAntigo(database) is not null;

    /// <summary>Migra o desenho. O relato, ou null se não havia nada do nome antigo.</summary>
    internal static string? Migrar(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (PrefixoAntigo(database) is not { } antigo) return null;

        var pecas = 0;
        var camadas = 0;
        var blocos = 0;
        var registros = 0;
        var conflitos = new List<string>();

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            // O aplicativo novo registrado antes de gravar XData nele.
            var apps = (RegAppTable)transacao.GetObject(database.RegAppTableId, OpenMode.ForRead);
            if (!apps.Has(Novo))
            {
                apps.UpgradeOpen();
                var app = new RegAppTableRecord { Name = Novo };
                apps.Add(app);
                transacao.AddNewlyCreatedDBObject(app, true);
            }

            // 1. O XData de toda peça, em todo espaço e definição de bloco.
            var tabelaDeBlocos = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId idDoBloco in tabelaDeBlocos)
            {
                var bloco = (BlockTableRecord)transacao.GetObject(idDoBloco, OpenMode.ForRead);

                foreach (ObjectId id in bloco)
                {
                    if (id.IsErased) continue;
                    if (transacao.GetObject(id, OpenMode.ForRead, false, true) is not Entity entidade) continue;
                    if (MigrarXData(transacao, id, entidade, antigo)) pecas++;
                }
            }

            // 2. As camadas do plugin.
            var tabelaDeCamadas = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

            foreach (ObjectId id in tabelaDeCamadas)
            {
                var camada = (LayerTableRecord)transacao.GetObject(id, OpenMode.ForRead);
                if (!camada.Name.StartsWith(antigo + "_", StringComparison.OrdinalIgnoreCase)) continue;

                var nome = Novo + camada.Name[antigo.Length..];
                if (tabelaDeCamadas.Has(nome))
                {
                    conflitos.Add(Tr.F("camada {0} (já existe {1})", camada.Name, nome));
                    continue;
                }

                camada.UpgradeOpen();
                camada.Name = nome;
                camadas++;
            }

            // 3. Os blocos do plugin (pilar, módulos, árvores).
            foreach (ObjectId id in tabelaDeBlocos)
            {
                var bloco = (BlockTableRecord)transacao.GetObject(id, OpenMode.ForRead);
                if (bloco.IsLayout || bloco.IsAnonymous || !bloco.Name.StartsWith(antigo + "_", StringComparison.OrdinalIgnoreCase)) continue;

                var nome = Novo + bloco.Name[antigo.Length..];
                if (tabelaDeBlocos.Has(nome))
                {
                    conflitos.Add(Tr.F("bloco {0} (já existe {1})", bloco.Name, nome));
                    continue;
                }

                bloco.UpgradeOpen();
                bloco.Name = nome;
                blocos++;
            }

            // 4. O dicionário do desenho: a chave e os textos dentro (as
            //    configurações guardam nomes de camada com o prefixo).
            var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);

            if (raiz.Contains(antigo))
            {
                var dicAntigo = (DBDictionary)transacao.GetObject(raiz.GetAt(antigo), OpenMode.ForWrite);
                DBDictionary destino;

                if (raiz.Contains(Novo))
                {
                    destino = (DBDictionary)transacao.GetObject(raiz.GetAt(Novo), OpenMode.ForWrite);
                }
                else
                {
                    raiz.UpgradeOpen();
                    raiz.Remove(dicAntigo.ObjectId);
                    raiz.SetAt(Novo, dicAntigo);
                    destino = dicAntigo;
                }

                // O foreach tipado: o enumerador não tipado do DBDictionary
                // entrega DictionaryEntry, não DBDictionaryEntry.
                var entradas = new List<DBDictionaryEntry>();
                foreach (DBDictionaryEntry e in dicAntigo) entradas.Add(e);

                foreach (var entrada in entradas)
                {
                    if (transacao.GetObject(entrada.Value, OpenMode.ForWrite) is not Xrecord registro) continue;

                    var data = Trocar(registro.Data, antigo);
                    if (data is not null) registro.Data = data;

                    // Os dois dicionários existiam: o que o novo não tem vem do antigo.
                    if (!ReferenceEquals(destino, dicAntigo) && !destino.Contains(entrada.Key))
                    {
                        var copia = new Xrecord { Data = registro.Data };
                        destino.SetAt(entrada.Key, copia);
                        transacao.AddNewlyCreatedDBObject(copia, true);
                    }

                    registros++;
                }

                if (!ReferenceEquals(destino, dicAntigo))
                {
                    raiz.UpgradeOpen();
                    raiz.Remove(dicAntigo.ObjectId);
                    dicAntigo.Erase();
                }
            }

            // O registro do aplicativo antigo sai (sem XData nenhum agora):
            // o desenho salvo não é mais reconhecido como antigo.
            var tabelaDeApps = (RegAppTable)transacao.GetObject(database.RegAppTableId, OpenMode.ForRead);
            if (tabelaDeApps.Has(antigo))
            {
                var ids = new ObjectIdCollection { tabelaDeApps[antigo] };
                database.Purge(ids);
                foreach (ObjectId id in ids) transacao.GetObject(id, OpenMode.ForWrite).Erase();
            }

            transacao.Commit();
        }

        return Tr.F("MIGRAR Desenho passado para o nome Clivus Solar: {0} peça(s), {1} camada(s), {2} bloco(s), {3} registro(s) do desenho.", pecas, camadas, blocos, registros)
            + (conflitos.Count > 0 ? " " + Tr.F("Ficaram com o nome antigo (o novo já existia): {0}.", string.Join(", ", conflitos)) : string.Empty)
            + " " + Tr.T("Salve o desenho para não migrar de novo.");
    }

    /// <summary>Reescreve o XData do aplicativo antigo no novo, trocando o prefixo nos textos. Se havia.</summary>
    private static bool MigrarXData(Transaction transacao, ObjectId id, Entity entidade, string antigo)
    {
        using var dados = entidade.GetXDataForApplication(antigo);
        if (dados is null) return false;

        var valores = dados.AsArray();
        var novos = new List<TypedValue> { new((int)DxfCode.ExtendedDataRegAppName, Novo) };

        foreach (var v in valores.Skip(1))
            novos.Add(v.Value is string s ? new TypedValue(v.TypeCode, TrocarTexto(s, antigo)) : v);

        var escrita = (Entity)transacao.GetObject(id, OpenMode.ForWrite, false, true);

        // Só o nome do aplicativo apaga o XData dele; depois grava o novo.
        escrita.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, antigo));
        escrita.XData = new ResultBuffer(novos.ToArray());
        return true;
    }

    private static ResultBuffer? Trocar(ResultBuffer? dados, string antigo)
    {
        if (dados is null) return null;

        var valores = dados.AsArray();
        if (!valores.Any(v => v.Value is string s && s.Contains(antigo, StringComparison.Ordinal))) return null;

        return new ResultBuffer(valores.Select(v => v.Value is string s ? new TypedValue(v.TypeCode, TrocarTexto(s, antigo)) : v).ToArray());
    }

    private static string TrocarTexto(string s, string antigo) => s.Replace(antigo, Novo, StringComparison.Ordinal);

    /// <summary>
    /// Os perfis de mesa da pasta antiga (%LOCALAPPDATA%\&lt;empresa&gt;\UFV\perfis,
    /// achada pelo formato) vêm para a nova uma vez, se a nova ainda não tem
    /// perfil nenhum.
    /// </summary>
    internal static void CopiarPastaDoUsuario()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var antiga = Directory.EnumerateDirectories(local)
                .Select(d => Path.Combine(d, "UFV", "perfis"))
                .FirstOrDefault(Directory.Exists);
            var nova = MesaCommands.PastaDosPerfis;

            if (antiga is null) return;
            if (Directory.Exists(nova) && Directory.EnumerateFileSystemEntries(nova).Any()) return;

            Directory.CreateDirectory(nova);
            foreach (var arquivo in Directory.GetFiles(antiga))
                File.Copy(arquivo, Path.Combine(nova, Path.GetFileName(arquivo)), overwrite: false);

            RegistroDeDiagnostico.Registrar($"Perfis de mesa copiados de {antiga} para {nova} (troca do nome para Clivus Solar).");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui copiar os perfis da pasta antiga.", erro);
        }
    }
}
