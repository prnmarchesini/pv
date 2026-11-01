using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O nome da usina no 3D, gravado no desenho (42.10, Renan, 31/10/2026: "na
/// hora de exportar o 3D eu quero que apareça a opção de colocar o nome no
/// desenho"). O CLIVUS_3D pergunta numa janela, com o nome de agora na caixa;
/// o OK grava no dicionário do plugin quando o nome muda (<see cref="PlantName"/>).
/// </summary>
internal static class NomeDaUsina
{
    /// <summary>O nome gravado no desenho, ou null.</summary>
    internal static string? Ler(Database database)
    {
        using var dados = PluginDictionary.Load(database, PlantName.StorageKey);
        if (dados is null) return null;

        foreach (var valor in dados.AsArray())
            if (valor.Value is string texto) return PlantName.Clean(texto);

        return null;
    }

    /// <summary>O nome que o 3D usa agora: o gravado; sem ele, o do arquivo do desenho.</summary>
    internal static string Atual(Document documento) =>
        PlantName.Resolve(Ler(documento.Database), documento.Name, Tr.T("Usina"));

    /// <summary>
    /// O nome escolhido, limpo e gravado no desenho quando muda; null se não
    /// sobra nome. O OK da janela e o CLIVUS_3D_NOME_AUTO (o nível 2) passam por
    /// aqui. <paramref name="gravado"/>: se foi preciso gravar.
    /// </summary>
    internal static string? Confirmar(Document documento, string? digitado, out bool gravado)
    {
        gravado = false;
        if (PlantName.Clean(digitado) is not { } nome) return null;

        var database = documento.Database;
        if (!PlantName.NeedsSaving(nome, Ler(database), documento.Name)) return nome;

        PluginDictionary.Save(database, PlantName.StorageKey, new ResultBuffer(new TypedValue((int)DxfCode.Text, nome)));
        gravado = true;
        return nome;
    }

    /// <summary>A janela que pergunta o nome, com o de agora na caixa.</summary>
    internal static JanelaDeNome Janela(Document documento) => new(
        Tr.T("Publicar o 3D"),
        Tr.T("Nome da usina no 3D:"),
        Atual(documento),
        Tr.T("Aparece no título da página, no link e na área do cliente, e fica gravado no desenho para a próxima vez."));

    /// <summary>Pergunta o nome pela janela. O nome confirmado, ou null se o usuário cancelou.</summary>
    internal static string? Perguntar(Document documento)
    {
        var janela = Janela(documento);
        if (Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowModalWindow(janela) != true) return null;

        return Confirmar(documento, janela.Nome, out _);
    }
}
