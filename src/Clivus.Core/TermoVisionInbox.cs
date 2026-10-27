namespace Clivus.Core;

/// <summary>
/// A pasta onde o <c>Clivus.Link.exe</c> (chamado pelo Windows no endereço
/// <c>clivus://</c>) deixa o endereço para o plugin pegar. Um arquivo
/// <c>.clivus</c> por clique, com o endereço inteiro dentro.
///
/// A pasta é do usuário (<c>%LOCALAPPDATA%</c>), e o token vai junto no
/// arquivo: ele é apagado assim que o plugin o lê, e o que passou de 7 dias
/// (o prazo do token) é descartado sem abrir.
/// </summary>
public sealed class TermoVisionInbox(string folder)
{
    /// <summary>Extensão dos arquivos da pasta.</summary>
    public const string Extension = ".clivus";

    /// <summary>O prazo do token do envio: depois disso o arquivo não serve.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>A pasta de entrada do usuário atual.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "entrada");

    public string Folder { get; } = folder;

    /// <summary>
    /// Deixa o endereço na pasta. Escreve num temporário e renomeia, para o
    /// vigia nunca ler um arquivo pela metade.
    /// </summary>
    public string Deposit(string endereco)
    {
        Directory.CreateDirectory(Folder);
        var nome = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var temporario = Path.Combine(Folder, nome + ".tmp");
        var final = Path.Combine(Folder, nome + Extension);

        File.WriteAllText(temporario, endereco.Trim());
        File.Move(temporario, final);
        return final;
    }

    /// <summary>
    /// Os endereços que estão na pasta, do mais antigo ao mais novo; cada arquivo
    /// lido sai da pasta. Os que não são endereço válido ou passaram do prazo saem
    /// também, contados em <paramref name="descartados"/>.
    /// </summary>
    public List<TermoVisionLink> Take(DateTime agoraUtc, out int descartados)
    {
        descartados = 0;
        var lidos = new List<TermoVisionLink>();
        if (!Directory.Exists(Folder)) return lidos;

        foreach (var arquivo in Directory.GetFiles(Folder, "*" + Extension).Order(StringComparer.Ordinal))
        {
            string? texto;
            try
            {
                var velho = agoraUtc - File.GetLastWriteTimeUtc(arquivo) > MaxAge;
                texto = velho ? null : File.ReadAllText(arquivo);
                File.Delete(arquivo);
            }
            catch (IOException)
            {
                // Ainda em uso (o Link escrevendo, ou outro AutoCAD lendo): fica para a próxima.
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (texto is not null && TermoVisionLink.TryParse(texto, out var link, out _)) lidos.Add(link!);
            else descartados++;
        }
        return lidos;
    }
}
