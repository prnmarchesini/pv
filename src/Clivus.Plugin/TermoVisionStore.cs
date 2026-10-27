using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>O envio do TermoVision que este desenho abriu.</summary>
internal sealed record TermoVisionDrawing(string Server, string Shipment, string PlantName, string AreaName, int Epsg);

/// <summary>
/// Onde fica o envio do TermoVision (etapa 33, decisão 1 do plano): servidor e
/// envio no desenho (dicionário do plugin, chave <c>TERMOVISION</c>); o token
/// fora dele, em <c>%LOCALAPPDATA%\Clivus Solar\termovision\&lt;envio&gt;.token</c>,
/// cifrado para o usuário do Windows (DPAPI). O DWG vai para terceiros; o token
/// grava mesas na usina por 7 dias.
/// </summary>
internal static class TermoVisionStore
{
    private const string CampoServidor = "SERVIDOR";
    private const string CampoEnvio = "ENVIO";
    private const string CampoUsina = "USINA";
    private const string CampoArea = "AREA";
    private const string CampoEpsg = "EPSG";

    internal static void Save(Database database, TermoVisionDrawing envio)
    {
        var buffer = new ResultBuffer();
        void Campo(string nome, string valor)
        {
            buffer.Add(new TypedValue((int)DxfCode.Text, nome));
            buffer.Add(new TypedValue((int)DxfCode.Text, valor));
        }

        Campo(CampoServidor, envio.Server);
        Campo(CampoEnvio, envio.Shipment);
        Campo(CampoUsina, envio.PlantName);
        Campo(CampoArea, envio.AreaName);
        Campo(CampoEpsg, envio.Epsg.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PluginDictionary.Save(database, PluginInfo.ChaveTermoVision, buffer);
    }

    internal static TermoVisionDrawing? Load(Database database)
    {
        using var buffer = PluginDictionary.Load(database, PluginInfo.ChaveTermoVision);
        if (buffer is null) return null;

        var valores = buffer.AsArray();
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var k = 0; k + 1 < valores.Length; k += 2)
            if (valores[k].Value is string nome && valores[k + 1].Value is string valor) campos[nome] = valor;

        if (!campos.TryGetValue(CampoServidor, out var servidor) || !campos.TryGetValue(CampoEnvio, out var envio)) return null;
        return new TermoVisionDrawing(servidor, envio,
            campos.GetValueOrDefault(CampoUsina, ""), campos.GetValueOrDefault(CampoArea, ""),
            int.TryParse(campos.GetValueOrDefault(CampoEpsg), out var epsg) ? epsg : 0);
    }

    private static string PastaDosTokens =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "termovision");

    // O envio vira nome de arquivo: só o que é seguro num nome.
    private static string ArquivoDoToken(string envio) =>
        Path.Combine(PastaDosTokens, new string(envio.Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray()) + ".token");

    internal static void SaveToken(TermoVisionLink link)
    {
        Directory.CreateDirectory(PastaDosTokens);
        var cifrado = Dpapi.Proteger(Encoding.UTF8.GetBytes(link.Token));
        File.WriteAllBytes(ArquivoDoToken(link.Shipment), cifrado);
    }

    /// <summary>O endereço completo deste envio, se o token estiver neste PC.</summary>
    internal static TermoVisionLink? LinkOf(TermoVisionDrawing envio)
    {
        var arquivo = ArquivoDoToken(envio.Shipment);
        if (!File.Exists(arquivo)) return null;

        try
        {
            var token = Encoding.UTF8.GetString(Dpapi.Abrir(File.ReadAllBytes(arquivo)));
            return new TermoVisionLink(envio.Server, envio.Shipment, token);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // De outro usuário do Windows, ou corrompido: o envio pede o endereço de novo.
            RegistroDeDiagnostico.Registrar($"Não consegui ler o token do envio {envio.Shipment}.", erro);
            return null;
        }
    }

    /// <summary>CryptProtectData do Windows, para o usuário atual (sem pacote a mais no bundle).</summary>
    private static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Blob
        {
            public int Tamanho;
            public IntPtr Dados;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref Blob entrada, string? descricao, IntPtr entropia, IntPtr reservado, IntPtr prompt, int flags, out Blob saida);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref Blob entrada, IntPtr descricao, IntPtr entropia, IntPtr reservado, IntPtr prompt, int flags, out Blob saida);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memoria);

        private const int SemInterface = 0x1; // CRYPTPROTECT_UI_FORBIDDEN

        internal static byte[] Proteger(byte[] dados) => Chamar(dados, proteger: true);

        internal static byte[] Abrir(byte[] dados) => Chamar(dados, proteger: false);

        private static byte[] Chamar(byte[] dados, bool proteger)
        {
            var fixo = GCHandle.Alloc(dados, GCHandleType.Pinned);
            try
            {
                var entrada = new Blob { Tamanho = dados.Length, Dados = fixo.AddrOfPinnedObject() };
                var certo = proteger
                    ? CryptProtectData(ref entrada, "Clivus Solar TermoVision", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SemInterface, out var saida)
                    : CryptUnprotectData(ref entrada, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SemInterface, out saida);
                if (!certo) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    var resultado = new byte[saida.Tamanho];
                    Marshal.Copy(saida.Dados, resultado, 0, saida.Tamanho);
                    return resultado;
                }
                finally
                {
                    LocalFree(saida.Dados);
                }
            }
            finally
            {
                fixo.Free();
            }
        }
    }
}
