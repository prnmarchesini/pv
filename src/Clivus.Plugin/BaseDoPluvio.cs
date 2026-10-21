using System.IO;
using System.Windows;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Onde está a base do Plúvio 2.1 neste PC, lida uma vez por sessão (32.9).
/// A base é do GPRH/UFV: o plugin lê a instalada, nunca leva uma cópia.
/// </summary>
internal static class BaseDoPluvio
{
    /// <summary>Acima disso, em km, a estação mais perto está longe demais para confiar.</summary>
    internal const double Longe = 150;

    private static string? _escolhida;
    private static (string Caminho, DateTime Data, PluvioDatabase Base)? _lida;

    /// <summary>
    /// O caminho da base: o da instalação padrão do Plúvio ou, se não achar,
    /// o que o usuário escolher na janela do Windows. Null se ele cancelar.
    /// </summary>
    internal static string? Caminho(Window dono)
    {
        if (CaminhoInstalado() is { } achado) return achado;

        MessageBox.Show(dono,
            Tr.F("Não achei o Plúvio 2.1 instalado (procurei a pasta GPRH em Arquivos de Programas). Mostre onde está o arquivo {0}.", PluvioDatabase.FileName),
            Tr.T("Clivus Solar — Plúvio"), MessageBoxButton.OK, MessageBoxImage.Information);

        var escolhida = DialogoDeArquivo.Abrir(Tr.T("Base do Plúvio 2.1"), Tr.F("Base do Plúvio ({0})|{0}|Todos os arquivos (*.*)|*.*", PluvioDatabase.FileName), null);
        if (escolhida is not null) _escolhida = escolhida;
        return escolhida;
    }

    /// <summary>O caminho já escolhido ou o da instalação padrão, sem perguntar; null se não achar.</summary>
    internal static string? CaminhoInstalado()
    {
        if (_escolhida is not null && File.Exists(_escolhida)) return _escolhida;

        return PluvioDatabase
            .UsualPaths(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>A base lida do arquivo (com as localidades, se o arquivo delas está junto); lê de novo só se mudou.</summary>
    internal static PluvioDatabase Ler(string caminho)
    {
        var data = File.GetLastWriteTimeUtc(caminho);
        if (_lida is { } l && l.Caminho == caminho && l.Data == data) return l.Base;

        var base_ = PluvioDatabase.Parse(File.ReadAllBytes(caminho));

        var localidades = Path.Combine(Path.GetDirectoryName(caminho) ?? string.Empty, PluvioDatabase.LocalitiesFileName);
        if (File.Exists(localidades)) base_ = base_.WithLocalities(File.ReadAllBytes(localidades));
        _lida = (caminho, data, base_);
        return base_;
    }
}
