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
/// antes tem tudo com o prefixo antigo (MARCHENG_UFV): o XData das peças, as
/// camadas, os blocos de pilar, módulo e árvore, o dicionário do desenho (com
/// as configurações, que guardam nomes de camada). Ao abrir, tudo passa para
/// o prefixo novo, uma vez; salvo o desenho, não há mais o que migrar.
///
/// Só o que é do plugin muda: camada e bloco cujo nome COMEÇA com o prefixo
/// antigo e o nosso aplicativo de XData. O que é do usuário (um estilo de
/// texto "Marcheng Anotativa", por exemplo) fica como está.
/// </summary>
public static class MigracaoDoNome
{
    private const string Antigo = PluginInfo.PrefixoAntigo;
    private const string Novo = PluginInfo.PrefixoDeDados;

    /// <summary>CLIVUS_MIGRAR: migra o desenho aberto, se ele tiver coisas do nome antigo.</summary>
    [CommandMethod(PluginInfo.ComandoMigrar)]
    public static void Migrar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage($"\n{Migrar(documento.Database) ?? "MIGRAR Nada do nome antigo neste desenho."}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao migrar o desenho para o nome novo.", erro);
            documento.Editor.WriteMessage($"\nNão consegui migrar o desenho: {erro.Message}\n");
        }
    }

    /// <summary>Se o desenho tem algo do nome antigo (aplicativo de XData, dicionário, camada ou bloco).</summary>
    internal static bool TemNomeAntigo(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var apps = (RegAppTable)transacao.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (apps.Has(Antigo)) return true;

        var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (raiz.Contains(Antigo)) return true;

        var camadas = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in camadas)
            if (((LayerTableRecord)transacao.GetObject(id, OpenMode.ForRead)).Name.StartsWith(Antigo + "_", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// <summary>Migra o desenho. O relato, ou null se não havia nada do nome antigo.</summary>
    internal static string? Migrar(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (!TemNomeAntigo(database)) return null;

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
                    if (MigrarXData(transacao, id, entidade)) pecas++;
                }
            }

            // 2. As camadas do plugin.
            var tabelaDeCamadas = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

            foreach (ObjectId id in tabelaDeCamadas)
            {
                var camada = (LayerTableRecord)transacao.GetObject(id, OpenMode.ForRead);
                if (!camada.Name.StartsWith(Antigo + "_", StringComparison.OrdinalIgnoreCase)) continue;

                var nome = Novo + camada.Name[Antigo.Length..];
                if (tabelaDeCamadas.Has(nome))
                {
                    conflitos.Add($"camada {camada.Name} (já existe {nome})");
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
                if (bloco.IsLayout || bloco.IsAnonymous || !bloco.Name.StartsWith(Antigo + "_", StringComparison.OrdinalIgnoreCase)) continue;

                var nome = Novo + bloco.Name[Antigo.Length..];
                if (tabelaDeBlocos.Has(nome))
                {
                    conflitos.Add($"bloco {bloco.Name} (já existe {nome})");
                    continue;
                }

                bloco.UpgradeOpen();
                bloco.Name = nome;
                blocos++;
            }

            // 4. O dicionário do desenho: a chave e os textos dentro (as
            //    configurações guardam nomes de camada com o prefixo).
            var raiz = (DBDictionary)transacao.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);

            if (raiz.Contains(Antigo))
            {
                var antigo = (DBDictionary)transacao.GetObject(raiz.GetAt(Antigo), OpenMode.ForWrite);
                DBDictionary destino;

                if (raiz.Contains(Novo))
                {
                    destino = (DBDictionary)transacao.GetObject(raiz.GetAt(Novo), OpenMode.ForWrite);
                }
                else
                {
                    raiz.UpgradeOpen();
                    raiz.Remove(antigo.ObjectId);
                    raiz.SetAt(Novo, antigo);
                    destino = antigo;
                }

                // O foreach tipado: o enumerador não tipado do DBDictionary
                // entrega DictionaryEntry, não DBDictionaryEntry.
                var entradas = new List<DBDictionaryEntry>();
                foreach (DBDictionaryEntry e in antigo) entradas.Add(e);

                foreach (var entrada in entradas)
                {
                    if (transacao.GetObject(entrada.Value, OpenMode.ForWrite) is not Xrecord registro) continue;

                    var data = Trocar(registro.Data);
                    if (data is not null) registro.Data = data;

                    // Os dois dicionários existiam: o que o novo não tem vem do antigo.
                    if (!ReferenceEquals(destino, antigo) && !destino.Contains(entrada.Key))
                    {
                        var copia = new Xrecord { Data = registro.Data };
                        destino.SetAt(entrada.Key, copia);
                        transacao.AddNewlyCreatedDBObject(copia, true);
                    }

                    registros++;
                }

                if (!ReferenceEquals(destino, antigo))
                {
                    raiz.UpgradeOpen();
                    raiz.Remove(antigo.ObjectId);
                    antigo.Erase();
                }
            }

            transacao.Commit();
        }

        return $"MIGRAR Desenho passado para o nome Clivus Solar: {pecas} peça(s), {camadas} camada(s), {blocos} bloco(s), {registros} registro(s) do desenho."
            + (conflitos.Count > 0 ? $" Ficaram com o nome antigo (o novo já existia): {string.Join(", ", conflitos)}." : string.Empty)
            + " Salve o desenho para não migrar de novo.";
    }

    /// <summary>Reescreve o XData do aplicativo antigo no novo, trocando o prefixo nos textos. Se havia.</summary>
    private static bool MigrarXData(Transaction transacao, ObjectId id, Entity entidade)
    {
        using var antigo = entidade.GetXDataForApplication(Antigo);
        if (antigo is null) return false;

        var valores = antigo.AsArray();
        var novos = new List<TypedValue> { new((int)DxfCode.ExtendedDataRegAppName, Novo) };

        foreach (var v in valores.Skip(1))
            novos.Add(v.Value is string s ? new TypedValue(v.TypeCode, TrocarTexto(s)) : v);

        var escrita = (Entity)transacao.GetObject(id, OpenMode.ForWrite, false, true);

        // Só o nome do aplicativo apaga o XData dele; depois grava o novo.
        escrita.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, Antigo));
        escrita.XData = new ResultBuffer(novos.ToArray());
        return true;
    }

    private static ResultBuffer? Trocar(ResultBuffer? dados)
    {
        if (dados is null) return null;

        var valores = dados.AsArray();
        if (!valores.Any(v => v.Value is string s && s.Contains(Antigo, StringComparison.Ordinal))) return null;

        return new ResultBuffer(valores.Select(v => v.Value is string s ? new TypedValue(v.TypeCode, TrocarTexto(s)) : v).ToArray());
    }

    private static string TrocarTexto(string s) => s.Replace(Antigo, Novo, StringComparison.Ordinal);

    /// <summary>
    /// Os perfis de mesa da pasta antiga (%LOCALAPPDATA%\MarchEng\UFV) vêm para
    /// a nova uma vez, se a nova ainda não tem perfil nenhum.
    /// </summary>
    internal static void CopiarPastaDoUsuario()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var antiga = Path.Combine(local, PluginInfo.PastaDoUsuarioAntiga, "perfis");
            var nova = MesaCommands.PastaDosPerfis;

            if (!Directory.Exists(antiga)) return;
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
