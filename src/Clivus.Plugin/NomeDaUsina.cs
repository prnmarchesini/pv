using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O nome da usina no 3D, gravado no desenho (42.10, Renan, 31/10/2026: "na
/// hora de exportar o 3D eu quero que apareça a opção de colocar o nome no
/// desenho"). O CLIVUS_3D pergunta numa janela, com o nome de agora na caixa;
/// o OK grava no dicionário do plugin, com o nome do arquivo em que foi dado,
/// quando o nome muda (<see cref="PlantName"/>).
/// </summary>
internal static class NomeDaUsina
{
    /// <summary>O nome gravado no desenho e o arquivo em que foi dado; nulls sem nome gravado.</summary>
    internal static (string? Nome, string? Arquivo) Ler(Database database)
    {
        using var dados = PluginDictionary.Load(database, PlantName.StorageKey);
        if (dados is null) return (null, null);

        var textos = dados.AsArray().Select(v => v.Value as string).OfType<string>().ToList();
        return (textos.Count > 0 ? PlantName.Clean(textos[0]) : null, textos.Count > 1 ? textos[1] : null);
    }

    /// <summary>O nome que o 3D usa agora: o gravado; sem ele, o do arquivo <paramref name="caminho"/> (o do desenho).</summary>
    internal static string Atual(Document documento, string? caminho = null) =>
        PlantName.Resolve(Ler(documento.Database).Nome, caminho ?? documento.Name, Tr.T("Usina"));

    /// <summary>
    /// O nome escolhido, limpo e gravado no desenho quando muda; null se não
    /// sobra nome. Aceitar o que já valia devolve ele como era (o nome do
    /// arquivo sem limpar). O OK da janela e o CLIVUS_3D_NOME_AUTO (o nível 2)
    /// passam por aqui. Se gravar falhar, o 3D sai com o nome assim mesmo,
    /// avisado. <paramref name="gravado"/>: se o nome foi para o desenho.
    /// </summary>
    internal static string? Confirmar(Document documento, string? digitado, out bool gravado)
    {
        gravado = false;
        if (PlantName.Clean(digitado) is not { } nome) return null;

        var database = documento.Database;
        var (salvo, arquivo) = Ler(database);
        if (!PlantName.NeedsSaving(nome, salvo, arquivo, documento.Name)) return salvo ?? PlantName.Resolve(null, documento.Name, nome);

        try
        {
            PluginDictionary.Save(database, PlantName.StorageKey, new ResultBuffer(
                new TypedValue((int)DxfCode.Text, nome),
                new TypedValue((int)DxfCode.Text, PlantName.FileName(documento.Name))));
            gravado = true;
            GeoCommands.AvisarSeNaoVaiSalvar(documento.Editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gravar o nome da usina no desenho.", erro);
            documento.Editor.WriteMessage(Tr.F("\n  Não consegui gravar o nome da usina no desenho ({0}); o 3D sai com ele assim mesmo.\n", erro.Message));
        }

        return nome;
    }

    /// <summary>
    /// A janela que pergunta o nome, com o de agora na caixa e, se ele foi
    /// dado noutro arquivo, o aviso. <paramref name="caminho"/>: o arquivo do
    /// desenho (o nível 2 finge uma cópia para desenhar o aviso).
    /// </summary>
    internal static JanelaDeNome Janela(Document documento, string? caminho = null)
    {
        caminho ??= documento.Name;
        var (salvo, arquivo) = Ler(documento.Database);
        var alerta = salvo is not null && PlantName.GivenInOtherFile(arquivo, caminho)
            ? Tr.F("Este nome foi dado no arquivo {0}. Se este desenho é de outra usina, troque o nome.", arquivo)
            : null;

        return new JanelaDeNome(
            Tr.T("Publicar o 3D"),
            Tr.T("Nome da usina no 3D:"),
            Atual(documento, caminho),
            Tr.T("Aparece no título da página do 3D e na área do cliente, e fica gravado no desenho para a próxima vez."),
            alerta);
    }

    /// <summary>
    /// Pergunta o nome pela janela. O nome confirmado, ou null se o usuário
    /// cancelou. Se a janela não abrir, o 3D sai com o nome de agora, avisado.
    /// </summary>
    internal static string? Perguntar(Document documento)
    {
        JanelaDeNome janela;
        bool? resposta;

        try
        {
            janela = Janela(documento);
            resposta = Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowModalWindow(janela);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela do nome da usina.", erro);
            var atual = Atual(documento);
            documento.Editor.WriteMessage(Tr.F("\n  Não consegui abrir a janela do nome; o 3D sai com \"{0}\".\n", atual));
            return atual;
        }

        return resposta == true ? Confirmar(documento, janela.Nome, out _) : null;
    }
}
