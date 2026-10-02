using System.IO;
using System.Runtime.CompilerServices;

namespace UFV.Plugin;

/// <summary>
/// A janela do Windows para salvar e abrir arquivo. Regra do Renan
/// (02/10/2026): "toda interação de salvar e abrir é via janela do Windows"
/// — nunca a pergunta na linha de comando, que é o que o AutoCAD mostra com
/// FILEDIA desligado.
/// </summary>
internal static class DialogoDeArquivo
{
    /// <summary>O caminho escolhido para salvar, ou null se o usuário cancelou.</summary>
    /// <param name="titulo">O título da janela.</param>
    /// <param name="filtro">O filtro do Windows ("Planilha (*.xlsx)|*.xlsx").</param>
    /// <param name="nomeInicial">O nome sugerido.</param>
    /// <param name="pastaInicial">A pasta onde a janela abre, ou null.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? Salvar(string titulo, string filtro, string nomeInicial, string? pastaInicial)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = titulo,
            Filter = filtro,
            FileName = nomeInicial,
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (!string.IsNullOrEmpty(pastaInicial) && Directory.Exists(pastaInicial)) dialogo.InitialDirectory = pastaInicial;

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    /// <summary>O caminho escolhido para abrir, ou null se o usuário cancelou.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? Abrir(string titulo, string filtro, string? pastaInicial)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Title = titulo, Filter = filtro, CheckFileExists = true };

        if (!string.IsNullOrEmpty(pastaInicial) && Directory.Exists(pastaInicial)) dialogo.InitialDirectory = pastaInicial;

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }
}
