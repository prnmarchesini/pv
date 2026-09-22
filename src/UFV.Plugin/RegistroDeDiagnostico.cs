using System.IO;
using System.Text;

namespace UFV.Plugin;

/// <summary>
/// Registro de diagnostico do plugin, em arquivo.
///
/// Existe porque Debug.WriteLine desaparece na compilacao Release: o bundle
/// que o usuario instala e Release, entao uma falha registrada assim nao
/// deixaria rastro nenhum - nem aba, nem mensagem, nem log. Uma falha ao
/// montar a ribbon e justamente o caso em que nao ha onde escrever na tela.
///
/// O arquivo fica em %LOCALAPPDATA%\MarchEng\UFV\ufv.log.
/// </summary>
internal static class RegistroDeDiagnostico
{
    private static readonly object Tranca = new();

    /// <summary>Acima disso o arquivo e reiniciado; e diagnostico, nao auditoria.</summary>
    private const long TamanhoMaximoEmBytes = 1 * 1024 * 1024;

    internal static string Caminho { get; } = MontarCaminho();

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

                if (File.Exists(Caminho) && new FileInfo(Caminho).Length > TamanhoMaximoEmBytes)
                    File.Delete(Caminho);

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
            return Path.Combine(raiz, "MarchEng", "UFV", "ufv.log");
        }
        catch (Exception)
        {
            return Path.Combine(Path.GetTempPath(), "ufv.log");
        }
    }
}
