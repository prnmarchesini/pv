using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A atualização do Clivus Solar no PC do cliente (etapa 43; Renan,
/// 31/10/2026: "quero que o cliente receba atualizações também, veja a
/// melhor forma").
///
/// Ao abrir o Civil 3D (só com interface), no máximo uma vez por dia, a
/// versão publicada (<c>GET /api/v1/versao</c>) é consultada em segundo plano,
/// com tempo curto. Versão maior que a instalada: na folga, com um desenho
/// aberto e nenhum comando rodando, o aviso vai para a linha de comando e a
/// janela da atualização abre (<see cref="JanelaDeAtualizacao"/>). O comando
/// Atualizar consulta na hora.
///
/// Atualizar agora: baixa o instalador para a pasta temporária (só de HTTPS
/// do servidor ou do site de download), confere o tamanho e o SHA-256 contra
/// os da versão e o roda com <c>/aguardar-civil3d</c>: ele espera o Civil 3D
/// fechar e instala sozinho. As regras são do <see cref="PluginUpdate"/> (Core).
/// </summary>
internal static class Atualizacao
{
    /// <summary>A consulta da abertura: em segundo plano e curta, para não pesar em quem está sem internet.</summary>
    private static readonly HttpClient ClienteDaAbertura = new() { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 1024 * 1024 };

    /// <summary>A consulta do comando: o usuário pediu, então espera um pouco mais.</summary>
    private static readonly HttpClient ClienteDoComando = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1024 * 1024 };

    /// <summary>
    /// O download: sem tempo total (internet lenta), cada leitura com o seu
    /// limite (<see cref="LeituraParada"/>). Redirecionamento só à mão
    /// (<see cref="InstallerDownload"/>), para o endereço novo passar pela
    /// mesma regra do primeiro.
    /// </summary>
    private static readonly HttpClient ClienteDoDownload = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Uma leitura do download que demora mais que isso é conexão parada.</summary>
    private static readonly TimeSpan LeituraParada = TimeSpan.FromSeconds(30);

    /// <summary>O resultado da consulta da abertura, quando ela termina (escrito por outra linha).</summary>
    private static volatile Consulta? _daAbertura;

    private static bool _assinada;

    /// <summary>
    /// O resultado de uma consulta: se o servidor respondeu dentro do contrato
    /// (a versão, ou 404 sem versão), a versão publicada e o erro.
    /// </summary>
    internal sealed record Consulta(bool Respondeu, PluginRelease? Versao, string? Erro);

    /// <summary>A versão deste plugin, como o usuário lê ("0.1.0").</summary>
    internal static string Instalada => PluginInfo.VersaoLegivel(ClivusCommands.VersaoDoPlugin());

    /// <summary>O registro do instalador que espera o Civil 3D (o /log= dele), ao lado do registro de diagnóstico.</summary>
    internal static string ArquivoDoRegistro => Path.Combine(RegistroDeDiagnostico.Pasta, "atualizacao.log");

    /// <summary>Onde o instalador baixado fica: a pasta temporária do usuário.</summary>
    internal static string PastaDoDownload => Path.Combine(Path.GetTempPath(), PluginInfo.PastaDoUsuario);

    /// <summary>A pasta de onde este plugin foi carregado.</summary>
    internal static string? PastaDoPlugin => Path.GetDirectoryName(typeof(Atualizacao).Assembly.Location);

    /// <summary>Se este plugin é o que o instalador pôs na pasta do usuário (só esse a atualização troca).</summary>
    internal static bool InstaladoPeloInstalador =>
        PluginUpdate.IsInstallerBundle(PastaDoPlugin, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>Se um instalador baixado já espera o Civil 3D fechar (o mutex dele existe nesta sessão).</summary>
    internal static bool JaEsperando
    {
        get
        {
            try
            {
                if (!Mutex.TryOpenExisting(PluginUpdate.WaitingMutexName, out var mutex)) return false;
                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Existe, só não deixa abrir: alguém espera.
                return true;
            }
            catch (Exception erro) when (erro is IOException or WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }
    }

    // ------------------------------------------------------------ a consulta

    /// <summary>Consulta a versão publicada. Não lança: o problema vem na consulta.</summary>
    internal static Consulta Consultar(bool doComando)
    {
        if (Publicador3D.Endereco is not { } endereco) return new Consulta(false, null, Tr.T("o servidor do Clivus não está configurado"));
        if (Viewer3DPage.WhyServerUnsafe(endereco) is { } inseguro) return new Consulta(false, null, inseguro);

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, endereco + PluginUpdate.VersionRoute);
            pedido.Headers.AcceptLanguage.ParseAdd(Tr.AcceptLanguage);

            using var resposta = (doComando ? ClienteDoComando : ClienteDaAbertura).Send(pedido);
            using var leitor = new StreamReader(resposta.Content.ReadAsStream(), Encoding.UTF8);
            var (versao, erro) = PluginUpdate.ParseReply((int)resposta.StatusCode, leitor.ReadToEnd());

            return new Consulta(erro is null, versao, erro);
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException or IOException)
        {
            RegistroDeDiagnostico.Registrar($"Atualização: não consegui consultar {endereco}{PluginUpdate.VersionRoute}.", erro);
            return new Consulta(false, null, Tr.T("não consegui falar com o servidor do Clivus (sem internet?)"));
        }
    }

    /// <summary>A versão publicada, se é mais nova que esta; senão null.</summary>
    internal static PluginRelease? MaisNova(Consulta consulta) =>
        consulta.Versao is { } versao && SemanticVersion.IsNewer(versao.Version.ToString(), Instalada) ? versao : null;

    /// <summary>Grava agora como a última consulta, nas preferências do usuário.</summary>
    internal static void GravarConsulta()
    {
        try
        {
            (UserPreferences.Load(IdiomaDoPlugin.Arquivo) with { LastUpdateCheck = DateTime.UtcNow }).Save(IdiomaDoPlugin.Arquivo);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui gravar a consulta da versão nas preferências.", erro);
        }
    }

    /// <summary>
    /// Na abertura do Civil 3D (só com interface): se faz um dia da última
    /// consulta, consulta em segundo plano e deixa a folga avisar.
    /// </summary>
    internal static void ConsultarAoAbrir()
    {
        var ultima = UserPreferences.Load(IdiomaDoPlugin.Arquivo).LastUpdateCheck;
        if (!PluginUpdate.IsCheckDue(ultima, DateTime.UtcNow))
        {
            RegistroDeDiagnostico.Registrar($"Atualização: consultada em {ultima:yyyy-MM-dd HH:mm} UTC; a próxima, um dia depois.");
            return;
        }

        // A folga é assinada aqui, na linha do AutoCAD (como a entrada do
        // TermoVision); só a consulta vai para outra linha.
        AcadApp.Idle += NaFolga;
        _assinada = true;

        Task.Run(() =>
        {
            try
            {
                _daAbertura = Consultar(doComando: false);
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao consultar a versão publicada.", erro);
                _daAbertura = new Consulta(false, null, erro.Message);
            }
        });
    }

    internal static void Desinstalar()
    {
        if (_assinada) AcadApp.Idle -= NaFolga;
        _assinada = false;
    }

    /// <summary>
    /// A folga do AutoCAD depois da consulta da abertura: grava a consulta e,
    /// com versão nova, avisa (com um desenho aberto e nenhum comando rodando;
    /// até lá, a folga espera).
    /// </summary>
    private static void NaFolga(object? remetente, EventArgs e)
    {
        var consulta = _daAbertura;
        if (consulta is null) return;

        PluginRelease? nova = null;
        Document? documento = null;

        try
        {
            nova = MaisNova(consulta);
            documento = AcadApp.DocumentManager.MdiActiveDocument;
            if (nova is not null && (documento is null || !string.IsNullOrEmpty(documento.CommandInProgress))) return;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na folga da atualização.", erro);
            nova = null;
        }

        // Daqui em diante, uma vez só: a janela modal tem folgas dela.
        Desinstalar();

        try
        {
            // Conta como a consulta do dia quando o servidor respondeu, menos
            // com versão obrigatória pendente: essa avisa a cada abertura.
            if (consulta.Respondeu && nova is not { Required: true }) GravarConsulta();

            RegistroDeDiagnostico.Registrar(consulta.Erro is { } erro
                ? $"Atualização: a consulta não deu ({erro})."
                : nova is null
                    ? $"Atualização: em dia ({Instalada}; publicada {consulta.Versao?.Version.ToString() ?? "nenhuma"})."
                    : $"Atualização: versão nova {nova.Version} (instalada {Instalada}).");

            if (nova is not null && documento is not null) Avisar(documento, nova);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao avisar da versão nova.", erro);
        }
    }

    /// <summary>O aviso: a linha de comando e, com interface, a janela.</summary>
    internal static void Avisar(Document documento, PluginRelease versao)
    {
        documento.Editor.WriteMessage(Tr.F(
            "\nHá uma versão nova do Clivus Solar: {0} (esta é a {1}). Para atualizar depois, use o botão Atualizar ({2}).\n",
            versao.Version, Instalada, PluginInfo.ComandoAtualizar));

        if (!ClivusExtension.TemInterface()) return;

        AcadApp.ShowModalWindow(new JanelaDeAtualizacao(versao));
    }

    // ------------------------------------------------------------ o download

    /// <summary>
    /// Baixa o instalador da versão para a pasta temporária. As regras (o
    /// endereço e cada redirecionamento, o tamanho, o SHA-256) são do
    /// <see cref="InstallerDownload"/>, no Core; aqui, o arquivo: grava num
    /// ".parcial", apaga o que não serve e renomeia o que serve. O arquivo
    /// conferido, ou o porquê. Roda fora da linha do AutoCAD; o andamento vai
    /// em bytes.
    /// </summary>
    internal static async Task<(string? Arquivo, string? Erro)> Baixar(PluginRelease versao, Action<long> andamento, CancellationToken cancelar)
    {
        var destino = Path.Combine(PastaDoDownload, PluginUpdate.InstallerFileName(versao.Version));
        var parcial = destino + ".parcial";

        // Já baixado e conferido (o Depois, ou a espera que passou do limite): não baixa de novo.
        if (Conferido(destino, versao))
        {
            andamento(versao.Size);
            return (destino, null);
        }

        try
        {
            Directory.CreateDirectory(PastaDoDownload);

            InstallerDownloadResult resultado;
            using (var arquivo = new FileStream(parcial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                resultado = await InstallerDownload.RunAsync(
                    ClienteDoDownload, versao, Publicador3D.Endereco, PluginInfo.SiteDeDownload, arquivo, andamento, LeituraParada, cancelar).ConfigureAwait(false);
            }

            if (resultado.Error is { } erro)
            {
                Apagar(parcial);
                RegistroDeDiagnostico.Registrar($"Atualização: o download de {resultado.From?.ToString() ?? versao.Url} não serve ({erro}).");
                return (null, resultado.Mismatch ? Tr.F("{0}; apaguei o arquivo", erro) : erro);
            }

            File.Move(parcial, destino, overwrite: true);
            RegistroDeDiagnostico.Registrar($"Atualização: {destino} baixado de {resultado.From} e conferido (SHA-256 {versao.Sha256}).");
            return (destino, null);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            Apagar(parcial);
            RegistroDeDiagnostico.Registrar($"Atualização: falha ao gravar o instalador em {parcial}.", erro);
            return (null, Tr.F("não consegui gravar o instalador ({0})", erro.Message));
        }
    }

    /// <summary>Se o arquivo existe e tem o tamanho e o SHA-256 da versão.</summary>
    internal static bool Conferido(string arquivo, PluginRelease versao)
    {
        try
        {
            if (!File.Exists(arquivo) || new FileInfo(arquivo).Length != versao.Size) return false;

            using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.Read);
            return PluginUpdate.WhyDownloadRejected(fluxo.Length, PluginUpdate.Sha256Hex(fluxo), versao) is null;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Apagar(string arquivo)
    {
        try
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            RegistroDeDiagnostico.Registrar($"Não consegui apagar {arquivo}.", erro);
        }
    }

    // ---------------------------------------------------------- a instalação

    /// <summary>
    /// Roda o instalador baixado com <c>/aguardar-civil3d</c>: ele espera o
    /// Civil 3D fechar e instala sozinho. Null se rodou; senão o porquê.
    /// </summary>
    internal static string? Instalar(string arquivo, PluginRelease versao)
    {
        // Conferido de novo logo antes de rodar: o arquivo esperou na pasta temporária.
        if (!Conferido(arquivo, versao)) return Tr.T("o instalador baixado mudou depois de conferido; tente de novo");

        try
        {
            Directory.CreateDirectory(RegistroDeDiagnostico.Pasta);

            // Pelo Windows (sem herdar nada do Civil 3D, que vai fechar).
            using var processo = Process.Start(new ProcessStartInfo(arquivo)
            {
                UseShellExecute = true,
                Arguments = PluginUpdate.InstallerArguments(ArquivoDoRegistro, Tr.Current),
                WorkingDirectory = PastaDoDownload,
            });

            RegistroDeDiagnostico.Registrar($"Atualização: {arquivo} rodando com {PluginUpdate.WaitForCivil3DOption}; o registro dele em {ArquivoDoRegistro}.");
            return null;
        }
        catch (Exception erro) when (erro is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            RegistroDeDiagnostico.Registrar($"Atualização: não consegui rodar {arquivo}.", erro);
            return Tr.F("não consegui rodar o instalador ({0})", erro.Message);
        }
    }
}
