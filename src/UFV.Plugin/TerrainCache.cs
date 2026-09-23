using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// O terreno processado de um desenho, guardado em memória.
/// </summary>
/// <param name="SurfaceId">Qual superfície gerou esta malha.</param>
/// <param name="Mesh">A malha, pronta para responder cota.</param>
/// <param name="Summary">Os números que o usuário viu quando processou.</param>
internal sealed record ProcessedTerrain(ObjectId SurfaceId, Tin Mesh, TerrainSummary Summary);

/// <summary>
/// Guarda o terreno processado de cada desenho aberto.
///
/// Processar uma superfície grande leva segundos; os comandos seguintes — o
/// Obter Coordenada do passo 1.7, e adiante o motor inteiro — perguntam cota
/// milhares de vezes e não podem reprocessar a cada pergunta.
///
/// O terreno fica pendurado no próprio documento, no UserData dele, e não num
/// dicionário estático com o documento por chave. A diferença não é estilo:
///
/// - O UserData morre junto com o documento. Um dicionário estático seguraria
///   a malha — centenas de megabytes — de todo desenho já aberto na sessão.
/// - Não há chave para errar. A primeira versão usava o identificador do bloco
///   do espaço do modelo, que é o mesmo em qualquer DWG e só se distingue pelo
///   endereço interno do objeto; fechado um desenho e aberto outro, o endereço
///   pode ser reaproveitado, e o desenho novo herdaria em silêncio a malha do
///   antigo. Cota errada, plausível, impossível de notar a olho.
///
/// O que se guarda é memória, não verdade: a superfície do desenho pode mudar
/// depois, e nada aqui percebe isso. Quem vai perceber é o carimbo de
/// proveniência do passo 1.5, que compara a data de modificação da superfície
/// com a do processamento. Até lá, reprocessar é responsabilidade do usuário.
/// </summary>
internal static class TerrainCache
{
    /// <summary>
    /// Chave no UserData do documento. Prefixada como todo o resto que o
    /// plugin grava, para não colidir com outro aplicativo.
    /// </summary>
    private const string Chave = PluginInfo.PrefixoDeDados + "_TERRENO";

    /// <summary>Guarda o terreno processado deste desenho, substituindo o anterior.</summary>
    internal static void Store(Document documento, ProcessedTerrain terreno)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(terreno);

        documento.UserData[Chave] = terreno;
    }

    /// <summary>O terreno processado deste desenho, ou null se ainda não houve um.</summary>
    internal static ProcessedTerrain? Get(Document documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return documento.UserData[Chave] as ProcessedTerrain;
    }

    /// <summary>
    /// Esquece o terreno deste desenho.
    ///
    /// Usado quando o processamento não produz malha aproveitável: sem isto, o
    /// terreno anterior continuaria valendo e os comandos seguintes
    /// responderiam cota de uma superfície que o usuário acabou de trocar.
    /// </summary>
    internal static void Forget(Document documento)
    {
        if (documento is null) return;

        documento.UserData.Remove(Chave);
    }
}
