using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// De onde vêm os módulos da janela de Mesa: do servidor do Clivus (38.8),
/// do serviço local quando <c>CLIVUS_SERVICO</c> aponta para ele (passo 8.3)
/// e, sem nenhum no ar, da biblioteca embutida na DLL.
///
/// Sem <c>CLIVUS_SERVICO</c>, o endereço é o do servidor do Clivus, o mesmo
/// do Publicar 3D (<c>CLIVUS_SERVIDOR</c> ou o de produção), com
/// <c>/api/v1</c>. Lá o cadastro leva a chave (<c>CLIVUS_SERVIDOR_CHAVE</c>)
/// e o módulo fica pendente até a gestão conferir o .PAN; só então entra na
/// lista de todos (plano/contrato-servidor-3d.md).
/// </summary>
internal static class FonteDeModulos
{
    /// <summary>O serviço local, se nem o servidor do Clivus estiver configurado.</summary>
    private const string EnderecoLocal = "http://localhost:8765";

    /// <summary>
    /// Curto de propósito: a janela espera por isto ao abrir, e servidor fora
    /// do ar não pode virar janela travada. Três segundos dão para o servidor
    /// do Clivus pela internet (o serviço local respondia em 1,5).
    /// </summary>
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// Para o cadastro, mais folga: gravar pode demorar (banco frio,
    /// antivírus, o .PAN subindo), e um "não respondeu" com o módulo já
    /// gravado leva o usuário a um 409 na segunda tentativa.
    /// </summary>
    private static readonly HttpClient ClienteDoCadastro = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>O endereço, se for http(s) absoluto; null se CLIVUS_SERVICO não presta.</summary>
    private static string? EnderecoValido() =>
        Uri.TryCreate(Endereco, UriKind.Absolute, out var base_)
        && (base_.Scheme == Uri.UriSchemeHttp || base_.Scheme == Uri.UriSchemeHttps)
            ? Endereco
            : null;

    private static DateTime? _foraDoArAte;

    /// <summary>
    /// A parte elétrica que o servidor tem de cada módulo (os que vieram de um
    /// .PAN, 38.6), pelo modelo; vazia com a biblioteca embutida.
    /// </summary>
    internal static IReadOnlyDictionary<string, PanModule> Eletrica { get; private set; } = new Dictionary<string, PanModule>();

    /// <summary>Se a última lista veio do servidor (e não da biblioteca embutida).</summary>
    internal static bool NoAr { get; private set; }

    /// <summary>O serviço local de CLIVUS_SERVICO (ou UFV_SERVICO, o nome de antes de 04/10/2026), ou null.</summary>
    private static string? ServicoLocal =>
        (Environment.GetEnvironmentVariable("CLIVUS_SERVICO") ?? Environment.GetEnvironmentVariable("UFV_SERVICO")) is { Length: > 0 } definido
            ? definido.Trim()
            : null;

    /// <summary>Se os módulos vêm do servidor do Clivus, e não de um serviço local: lá o cadastro pede a chave e fica pendente.</summary>
    internal static bool DoServidorDoClivus => ServicoLocal is null && Publicador3D.Endereco is not null;

    internal static string Endereco =>
        (ServicoLocal ?? (Publicador3D.Endereco is { } servidor ? servidor + "/api/v1" : EnderecoLocal)).TrimEnd('/');

    /// <summary>
    /// Os módulos e uma linha dizendo de onde vieram, para a janela mostrar.
    /// </summary>
    internal static (IReadOnlyList<SolarModule> Modulos, string Origem) Carregar()
    {
        Eletrica = new Dictionary<string, PanModule>();
        NoAr = false;

        if (EnderecoValido() is null)
        {
            RegistroDeDiagnostico.Registrar($"CLIVUS_SERVICO não é um endereço http: \"{Endereco}\".");
            return (ModuleLibrary.Default(), Tr.F("Biblioteca embutida (CLIVUS_SERVICO inválido: {0}).", Endereco));
        }

        // Servidor fora do ar há pouco: não espera de novo a cada janela.
        if (_foraDoArAte is { } ate && DateTime.UtcNow < ate)
            return (ModuleLibrary.Default(), Tr.T("Biblioteca embutida (serviço fora do ar)."));

        try
        {
            // Task.Run: sem ele, o .Result no fio da interface do AutoCAD
            // pode travar esperando o próprio fio.
            var json = Task.Run(() => Cliente.GetStringAsync($"{Endereco}/modulos")).GetAwaiter().GetResult();
            var modulos = ModuleLibrary.ParseService(json);
            Eletrica = ModuleLibrary.ParseServiceElectrical(json);
            NoAr = true;

            return (modulos, DoServidorDoClivus
                ? Tr.F("Módulos do servidor do Clivus: {0}.", modulos.Count)
                : Tr.F("Módulos do serviço ({0}): {1}.", Endereco, modulos.Count));
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            RegistroDeDiagnostico.Registrar($"Servidor de módulos indisponível em {Endereco}; usando a biblioteca embutida.", erro);
            if (erro is not InvalidOperationException) _foraDoArAte = DateTime.UtcNow.AddMinutes(1);

            var motivo = erro is InvalidOperationException ? erro.Message : Tr.T("serviço fora do ar");
            return (ModuleLibrary.Default(), Tr.F("Biblioteca embutida ({0}).", motivo));
        }
    }

    /// <summary>
    /// Cadastra o módulo (passo 8.4). Devolve se o servidor aceitou, se o
    /// módulo ficou pendente da validação da gestão (servidor do Clivus, 38.8)
    /// e a frase para o usuário. O motivo da recusa vem do próprio servidor
    /// (modelo repetido, medida fora da faixa, chave).
    /// </summary>
    /// <param name="eletrica">A parte elétrica do .PAN (38.6), ou null.</param>
    /// <param name="pan">O texto do .PAN, que o servidor guarda como veio, ou null.</param>
    /// <param name="panNome">O nome do arquivo .PAN.</param>
    internal static (bool Cadastrou, bool Pendente, string Mensagem) Cadastrar(SolarModule modulo, PanModule? eletrica = null, string? pan = null, string? panNome = null)
    {
        string json;

        try
        {
            json = ModuleLibrary.ToServiceJson(modulo, eletrica, pan, panNome);
        }
        catch (InvalidOperationException erro)
        {
            return (false, false, erro.Message);
        }

        if (EnderecoValido() is null)
        {
            return (false, false, Tr.F("CLIVUS_SERVICO não é um endereço http: \"{0}\".", Endereco));
        }

        var doClivus = DoServidorDoClivus;
        var chave = Publicador3D.Chave;

        if (doClivus)
        {
            // A chave só vai por HTTPS (plano/seguranca.md), como no Publicar 3D.
            if (Viewer3DPage.WhyServerUnsafe(Endereco) is { } inseguro) return (false, false, inseguro);

            if (chave is null)
            {
                return (false, false, Tr.T("Para mandar o módulo ao servidor do Clivus falta a chave (variável CLIVUS_SERVIDOR_CHAVE)."));
            }
        }

        try
        {
            using var corpo = new StringContent(json, Encoding.UTF8, "application/json");
            using var pedido = new HttpRequestMessage(HttpMethod.Post, $"{Endereco}/modulos") { Content = corpo };

            // O serviço local não pede chave; o do Clivus, sim. O erro volta no idioma da tela.
            if (doClivus && chave is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
            pedido.Headers.AcceptLanguage.ParseAdd(Tr.AcceptLanguage);

            using var resposta = Task.Run(() => ClienteDoCadastro.SendAsync(pedido)).GetAwaiter().GetResult();
            var texto = Task.Run(() => resposta.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

            if (resposta.IsSuccessStatusCode)
            {
                _foraDoArAte = null;

                // No servidor do Clivus o módulo espera a gestão conferir o
                // .PAN; só depois entra na lista de todos (38.8).
                if (ModuleServiceReply.IsPending(texto))
                {
                    return (true, true, pan is not null && ModuleLibrary.ServiceKeptPan(texto)
                        ? Tr.F("O módulo {0} foi para o servidor do Clivus, com o PAN {1}, e espera a validação da equipe. Depois de conferido, ele entra na lista de todos; até lá, fica só nesta janela.", modulo.DisplayName, panNome ?? string.Empty)
                        : Tr.F("O módulo {0} foi para o servidor do Clivus e espera a validação da equipe. Depois de conferido, ele entra na lista de todos; até lá, fica só nesta janela.", modulo.DisplayName));
                }

                return (true, false, pan is null
                    ? Tr.F("Módulo {0} cadastrado no serviço.", modulo.DisplayName)
                    : ModuleLibrary.ServiceKeptPan(texto)
                        ? Tr.F("Módulo {0} cadastrado no serviço, com o PAN {1}.", modulo.DisplayName, panNome ?? string.Empty)
                        : Tr.F("Módulo {0} cadastrado no serviço, mas sem o PAN: o serviço é de antes do cadastro com PAN. Atualize-o (tools\\servico-local.ps1), apague o módulo lá e cadastre de novo.", modulo.DisplayName));
            }

            return (false, false, resposta.StatusCode == HttpStatusCode.Conflict
                ? ModuleServiceReply.ErrorMessage(texto) ?? Tr.T("Já existe um módulo com esse modelo.")
                : Tr.F("O serviço recusou ({0}): {1}", (int)resposta.StatusCode, ModuleServiceReply.ErrorMessage(texto) ?? Cortar(texto)));
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            RegistroDeDiagnostico.Registrar($"Falha ao cadastrar módulo em {Endereco}.", erro);
            return (false, false, doClivus
                ? Tr.F("O servidor do Clivus não respondeu em {0}. Se ele estava no ar, o módulo pode ter ido: tente de novo daqui a pouco; um \"já existe\" quer dizer que foi.", Endereco)
                : Tr.F("O serviço de módulos não respondeu em {0}. Se ele estava no ar, o módulo pode ter sido gravado: feche e abra a janela de Mesa para conferir a lista. Fora do ar, rode tools\\servico-local.ps1.", Endereco));
        }
    }

    /// <summary>Resposta crua (uma página de erro) cortada para caber numa mensagem.</summary>
    private static string Cortar(string texto) =>
        texto.Length <= 200 ? texto : texto[..200] + "...";
}
