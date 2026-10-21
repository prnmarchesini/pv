using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As configurações da drenagem no desenho (<see cref="DrainageSettings"/>):
/// um JSON no dicionário CLIVUS, cortado em pedaços porque cada campo de
/// texto do registro tem limite de tamanho.
/// </summary>
internal static class DrenagemStore
{
    /// <summary>As configurações deste desenho; nunca gravadas, o padrão (memorial de Valparaiso).</summary>
    internal static DrainageSettings Ler(Database database)
    {
        using var dados = PluginDictionary.Load(database, DrainageSettings.StorageKey);
        var json = dados is null ? null : string.Concat(dados.AsArray().Select(v => v.Value as string ?? string.Empty));

        var lidas = DrainageSettings.Parse(json, out var problema);
        if (problema is not null) RegistroDeDiagnostico.Registrar($"Configurações da drenagem ilegíveis, voltei ao padrão: {problema}");

        return lidas;
    }

    /// <summary>Grava as configurações, substituindo as anteriores.</summary>
    internal static void Gravar(Database database, DrainageSettings configuracoes)
    {
        var pedacos = DrainageSettings.Chunks(configuracoes.ToJson());

        PluginDictionary.Save(database, DrainageSettings.StorageKey,
            new ResultBuffer(pedacos.Select(p => new TypedValue((int)DxfCode.Text, p)).ToArray()));
    }

    /// <summary>Lê, muda e grava.</summary>
    internal static DrainageSettings Mudar(Database database, Func<DrainageSettings, DrainageSettings> mudanca)
    {
        var novas = mudanca(Ler(database));
        Gravar(database, novas);
        return novas;
    }
}
