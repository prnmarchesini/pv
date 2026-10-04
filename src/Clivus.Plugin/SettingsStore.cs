using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Grava e lê as configurações do projeto dentro do desenho.
///
/// Elas vivem no dicionário nomeado do plugin, ao lado do carimbo do terreno:
/// são do desenho, e não do usuário. Um perfil de mesa é do projetista e vale
/// para todo desenho que ele abrir; a faixa da ponta baixa é deste projeto, e
/// viaja com o DWG para quem o receber.
///
/// O formato — pares nome/valor, versão, o que é ilegível — é do Core
/// (<see cref="ProjectSettings"/>), com teste de nível 1. Aqui só se
/// converte a lista em <see cref="ResultBuffer"/> e de volta.
/// </summary>
internal static class SettingsStore
{
    private const string Chave = "CONFIGURACAO";

    /// <summary>Grava as configurações, substituindo as anteriores.</summary>
    /// <exception cref="InvalidOperationException">Se elas não fecham.</exception>
    internal static void Save(Database database, ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // ToFields recusa configuração inválida; chamado antes de criar o
        // buffer para ele não vazar quando recusa (ResultBuffer segura memória
        // não gerenciada).
        var campos = settings.ToFields();
        var buffer = new ResultBuffer();

        foreach (var (chave, valor) in campos)
        {
            buffer.Add(new TypedValue((int)DxfCode.Text, chave));
            buffer.Add(new TypedValue((int)DxfCode.Text, valor));
        }

        PluginDictionary.Save(database, Chave, buffer);
    }

    /// <summary>
    /// As configurações gravadas. Ausência (nunca gravadas) e problema
    /// (gravadas e ilegíveis) são coisas diferentes, e o resultado diz qual —
    /// inclusive quando o Xrecord existe e o AutoCAD não consegue abri-lo,
    /// caso em que o dicionário devolve null e é a existência da chave que
    /// separa os dois. Sem essa conferência, registro corrompido virava
    /// "vale o padrão" sem aviso.
    /// </summary>
    internal static ProjectSettingsResult Load(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);

        try
        {
            using var dados = PluginDictionary.Load(database, Chave);

            if (dados is null)
            {
                return PluginDictionary.Contains(database, Chave)
                    ? new ProjectSettingsResult(null, "a configuração gravada no desenho está ilegível")
                    : ProjectSettings.Parse(null);
            }

            var valores = dados.AsArray();
            var campos = new List<KeyValuePair<string, string>>(valores.Length / 2);

            for (var i = 0; i + 1 < valores.Length; i += 2)
            {
                if (valores[i].Value is not string chave) continue;

                campos.Add(new KeyValuePair<string, string>(chave, valores[i + 1].Value as string ?? string.Empty));
            }

            var lido = ProjectSettings.Parse(campos);

            if (lido.Problem is { } problema)
                RegistroDeDiagnostico.Registrar($"Configuração do desenho com problema: {problema}.");

            return lido;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a configuração do desenho.", erro);
            return new ProjectSettingsResult(null, "a configuração gravada no desenho está ilegível");
        }
    }
}
