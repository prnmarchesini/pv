namespace UFV.Core.Tests;

/// <summary>
/// Vários testes desta etapa leem arquivos do repositório (os .csproj, o
/// PackageContents.xml, os scripts de tools/), porque é lá que mora a
/// configuração que não aparece em nenhuma assembly.
/// </summary>
internal static class Repositorio
{
    /// <summary>
    /// A pasta que contém UFV.sln, subindo a partir do diretório de saída dos
    /// testes.
    /// </summary>
    internal static string Raiz { get; } = Procurar();

    internal static string Caminho(params string[] partes) =>
        Path.Combine(new[] { Raiz }.Concat(partes).ToArray());

    private static string Procurar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UFV.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException($"UFV.sln não encontrada acima de {AppContext.BaseDirectory}.");

        return dir.FullName;
    }
}
