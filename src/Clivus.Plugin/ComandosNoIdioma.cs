using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Registra os nomes digitáveis dos comandos no idioma atual (10.5): cada
/// nome traduzido de <see cref="CommandNames"/> chama o mesmo método, com as
/// mesmas opções, do comando global em português, que continua valendo.
/// Ao trocar de idioma, os do idioma anterior saem.
/// </summary>
internal static class ComandosNoIdioma
{
    private const string Grupo = "CLIVUS_IDIOMA_NOMES";
    private static readonly List<string> Registrados = [];

    internal static int Registrar(UiLanguage idioma)
    {
        Remover();
        if (idioma == UiLanguage.Portuguese) return 0;

        var metodos = typeof(ComandosNoIdioma).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Metodo: m, Atributo: m.GetCustomAttribute<CommandMethodAttribute>()))
            .Where(x => x.Atributo is not null)
            .GroupBy(x => x.Atributo!.GlobalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (global, _) in CommandNames.Table)
        {
            if (CommandNames.LocalName(global, idioma) is not { } local) continue;
            if (!metodos.TryGetValue(global, out var alvo)) continue;

            var metodo = alvo.Metodo;
            try
            {
                Autodesk.AutoCAD.Internal.Utils.AddCommand(Grupo, local, local, alvo.Atributo!.Flags, () => metodo.Invoke(null, null));
                Registrados.Add(local);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Não consegui registrar o comando {local} ({global}).", erro);
            }
        }

        return Registrados.Count;
    }

    private static void Remover()
    {
        foreach (var nome in Registrados)
        {
            try
            {
                Autodesk.AutoCAD.Internal.Utils.RemoveCommand(Grupo, nome);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Não consegui tirar o comando {nome}.", erro);
            }
        }

        Registrados.Clear();
    }
}
