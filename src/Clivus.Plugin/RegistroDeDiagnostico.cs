using Clivus.Core;
using System.IO;
using System.Text;

namespace Clivus.Plugin;

/// <summary>
/// Registro de diagnostico do plugin, em arquivo.
///
/// Existe porque Debug.WriteLine desaparece na compilacao Release: o bundle
/// que o usuario instala e Release, entao uma falha registrada assim nao
/// deixaria rastro nenhum - nem aba, nem mensagem, nem log. Uma falha ao
/// montar a ribbon e justamente o caso em que nao ha onde escrever na tela.
///
/// O arquivo fica em %LOCALAPPDATA%\Clivus Solar\clivus.log. Cheio, ele vira
/// clivus.1.log (o anterior a esse sai) e um novo comeca: o que aconteceu
/// pouco antes de um erro nao se perde na virada (30/10/2026, "quero que voce
/// gere logs de tudo [...] tendo log, voce podera analisar").
/// </summary>
internal static class RegistroDeDiagnostico
{
    private static readonly object Tranca = new();

    /// <summary>Acima disso o arquivo vira o anterior; e diagnostico, nao auditoria.</summary>
    private const long TamanhoMaximoEmBytes = 4 * 1024 * 1024;

    internal static string Caminho { get; } = MontarCaminho();

    /// <summary>A pasta do plugin, onde ficam os registros.</summary>
    internal static string Pasta => Path.GetDirectoryName(Caminho) ?? Path.GetTempPath();

    internal static void Registrar(string mensagem, Exception? erro = null)
    {
        try
        {
            var linha = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append("  ")
                .Append(mensagem);

            if (erro is not null) linha.AppendLine().Append(erro);

            lock (Tranca)
            {
                var pasta = Path.GetDirectoryName(Caminho);
                if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

                // A virada num try próprio: com o clivus.1.log aberto noutro
                // programa ela falha, e a linha ainda tem de ser escrita.
                try
                {
                    if (File.Exists(Caminho) && new FileInfo(Caminho).Length > TamanhoMaximoEmBytes)
                        File.Move(Caminho, Path.ChangeExtension(Caminho, ".1.log"), overwrite: true);
                }
                catch (Exception)
                {
                    // Fica para a próxima linha.
                }

                File.AppendAllText(Caminho, linha.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Diagnostico que derruba o plugin e pior que diagnostico nenhum.
            // Disco cheio, pasta sem permissao, caminho longo demais: engolimos.
        }
    }

    private static string MontarCaminho()
    {
        try
        {
            var raiz = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(raiz, PluginInfo.PastaDoUsuario, "clivus.log");
        }
        catch (Exception)
        {
            return Path.Combine(Path.GetTempPath(), "clivus.log");
        }
    }
}
