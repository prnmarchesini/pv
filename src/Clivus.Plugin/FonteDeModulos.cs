using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// De onde vêm os módulos da janela de Mesa: do servidor do Clivus (38.8),
/// do serviço local quando <c>CLIVUS_SERVICO</c> aponta para ele (passo 8.3)
/// e, sem nenhum no ar, da biblioteca embutida na DLL. Quem decide o endereço
/// e se o cadastro pode ir é o <see cref="ModuleServer"/> do Core.
///
/// No servidor do Clivus o cadastro leva a chave (<c>CLIVUS_SERVIDOR_CHAVE</c>)
/// e o módulo fica pendente até a gestão conferir o .PAN; só então entra na
/// lista de todos (plano/contrato-servidor-3d.md). Até lá, ele fica na lista
/// das janelas desta sessão do Civil 3D (<see cref="Pendentes"/>).
/// </summary>
internal static class FonteDeModulos
{
    /// <summary>
    /// Curto de propósito: a janela espera por isto ao abrir, e servidor fora
    /// do ar não pode virar janela travada. Três segundos dão para o servidor
    /// do Clivus pela internet; e a lista boa e o "fora do ar" ficam guardados
    /// (<see cref="ValidadeDaLista"/>), para a espera não se repetir.
    /// </summary>
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// Para o cadastro, mais folga: gravar pode demorar (banco frio,
    /// antivírus, o .PAN subindo), e um "não respondeu" com o módulo já
    /// gravado leva o usuário a um "já existe" na segunda tentativa.
    /// </summary>
    private static readonly HttpClient ClienteDoCadastro = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Quanto vale a última lista boa: a janela de Mesa abre sem esperar a
    /// internet de novo (revisão do 38.8: até 3 s em cada abertura). A lista
    /// muda pouco, só quando a gestão aprova um módulo.
    /// </summary>
    private static readonly TimeSpan ValidadeDaLista = TimeSpan.FromMinutes(10);

    /// <summary>Quanto vale o "fora do ar": sem rede, a janela não espera a cada abertura.</summary>
    private static readonly TimeSpan ValidadeDoForaDoAr = TimeSpan.FromMinutes(5);

    private static (IReadOnlyList<SolarModule> Modulos, IReadOnlyDictionary<string, PanModule> Eletrica, string Origem, string Endereco, DateTime Quando)? _ultimaLista;

    private static DateTime? _foraDoArAte;

    /// <summary>
    /// Os módulos mandados ao servidor do Clivus nesta sessão do Civil 3D que
    /// esperam a gestão (revisão do 38.8), com a parte elétrica: entram na
    /// lista de toda janela de Mesa até a lista do servidor trazê-los
    /// aprovados. Sem isto, o pendente sumia ao carregar outro .PAN, e
    /// mandá-lo de novo dava "já existe".
    /// </summary>
    private static readonly Dictionary<string, (SolarModule Modulo, PanModule? Eletrica)> Pendentes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A parte elétrica que o servidor tem de cada módulo (os que vieram de um
    /// .PAN, 38.6), pelo modelo, mais a dos pendentes desta sessão; vazia com
    /// a biblioteca embutida.
    /// </summary>
    internal static IReadOnlyDictionary<string, PanModule> Eletrica { get; private set; } = new Dictionary<string, PanModule>();

    /// <summary>Se a última lista veio do servidor (e não da biblioteca embutida).</summary>
    internal static bool NoAr { get; private set; }

    /// <summary>O serviço local de CLIVUS_SERVICO (ou UFV_SERVICO, o nome de antes de 04/10/2026), ou null.</summary>
    private static string? ServicoLocal =>
        Environment.GetEnvironmentVariable("CLIVUS_SERVICO") ?? Environment.GetEnvironmentVariable("UFV_SERVICO");

    /// <summary>Onde fica a biblioteca agora (as variáveis podem mudar com o Civil 3D aberto).</summary>
    internal static ModuleServer Servidor => ModuleServer.Resolve(ServicoLocal, Publicador3D.Endereco);

    /// <summary>Se os módulos vêm do servidor do Clivus, e não de um serviço local.</summary>
    internal static bool DoServidorDoClivus => Servidor.IsClivus;

    /// <summary>O endereço da biblioteca, para as mensagens.</summary>
    internal static string Endereco => Servidor.Address ?? "—";

    /// <summary>Por que um cadastro não pode ir agora (sem endereço, sem HTTPS, sem chave), ou null.</summary>
    internal static string? PorQueNaoCadastra => Servidor.WhyCannotRegister(Publicador3D.Chave);

    /// <summary>Se o modelo foi mandado nesta sessão e espera a validação da gestão.</summary>
    internal static bool EhPendente(string modelo) => Pendentes.ContainsKey(modelo.Trim());

    /// <summary>
    /// Os módulos e uma linha dizendo de onde vieram, para a janela mostrar,
    /// com os pendentes desta sessão no fim.
    /// </summary>
    internal static (IReadOnlyList<SolarModule> Modulos, string Origem) Carregar()
    {
        var servidor = Servidor;
        Eletrica = new Dictionary<string, PanModule>();
        NoAr = false;

        if (servidor.Address is not { } endereco)
        {
            RegistroDeDiagnostico.Registrar($"Biblioteca de módulos sem endereço: {servidor.Problem}.");
            return ComPendentes(ModuleLibrary.Default(), new Dictionary<string, PanModule>(), Tr.F("Biblioteca embutida ({0}).", servidor.Problem ?? string.Empty));
        }

        // A última lista boa do mesmo endereço, enquanto vale.
        if (_ultimaLista is { } ultima && ultima.Endereco == endereco && DateTime.UtcNow - ultima.Quando < ValidadeDaLista)
        {
            NoAr = true;
            return ComPendentes(ultima.Modulos, ultima.Eletrica, ultima.Origem);
        }

        // Fora do ar há pouco: não espera de novo a cada janela.
        if (_foraDoArAte is { } ate && DateTime.UtcNow < ate)
            return ComPendentes(ModuleLibrary.Default(), new Dictionary<string, PanModule>(), Tr.T("Biblioteca embutida (serviço fora do ar)."));

        try
        {
            // Task.Run: sem ele, o .Result no fio da interface do AutoCAD
            // pode travar esperando o próprio fio.
            var json = Task.Run(() => Cliente.GetStringAsync($"{endereco}/modulos")).GetAwaiter().GetResult();
            var modulos = ModuleLibrary.ParseService(json);
            var eletrica = ModuleLibrary.ParseServiceElectrical(json);
            var origem = servidor.IsClivus
                ? Tr.F("Módulos do servidor do Clivus: {0}.", modulos.Count)
                : Tr.F("Módulos do serviço ({0}): {1}.", endereco, modulos.Count);

            _ultimaLista = (modulos, eletrica, origem, endereco, DateTime.UtcNow);
            _foraDoArAte = null;
            NoAr = true;

            return ComPendentes(modulos, eletrica, origem);
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            RegistroDeDiagnostico.Registrar($"Servidor de módulos indisponível em {endereco}; usando a biblioteca embutida.", erro);
            if (erro is not InvalidOperationException) _foraDoArAte = DateTime.UtcNow + ValidadeDoForaDoAr;

            var motivo = erro is InvalidOperationException ? erro.Message : Tr.T("serviço fora do ar");
            return ComPendentes(ModuleLibrary.Default(), new Dictionary<string, PanModule>(), Tr.F("Biblioteca embutida ({0}).", motivo));
        }
    }

    /// <summary>
    /// A lista com os pendentes desta sessão que ela ainda não tem, e a parte
    /// elétrica deles. O pendente que a lista já traz (a gestão aprovou) sai
    /// dos pendentes.
    /// </summary>
    private static (IReadOnlyList<SolarModule> Modulos, string Origem) ComPendentes(
        IReadOnlyList<SolarModule> modulos, IReadOnlyDictionary<string, PanModule> eletrica, string origem)
    {
        foreach (var aprovado in Pendentes.Keys.Where(m => ModuleLibrary.Find(modulos, m) is not null).ToList())
            Pendentes.Remove(aprovado);

        var juntos = new Dictionary<string, PanModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var (modelo, dados) in eletrica) juntos[modelo] = dados;
        foreach (var (modelo, (_, dados)) in Pendentes)
        {
            if (dados is not null) juntos[modelo] = dados;
        }

        Eletrica = juntos;

        if (Pendentes.Count == 0) return (modulos, origem);

        return (
            [.. modulos, .. Pendentes.Values.Select(p => p.Modulo)],
            origem + " " + Tr.F("Esperando a validação da equipe: {0}.", string.Join(", ", Pendentes.Values.Select(p => p.Modulo.Model))));
    }

    /// <summary>
    /// Cadastra o módulo (passo 8.4). Devolve se o servidor aceitou, se o
    /// módulo ficou pendente da validação da gestão (servidor do Clivus, 38.8)
    /// e a frase para o usuário. O motivo da recusa vem do próprio servidor
    /// (medida fora da faixa, chave) ou do <see cref="ModuleServer"/>.
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

        var servidor = Servidor;
        var chave = Publicador3D.Chave;

        // A chave só vai por HTTPS e só para o servidor do Clivus; o serviço
        // local não pede nenhuma.
        if (servidor.WhyCannotRegister(chave) is { } motivo) return (false, false, motivo);
        var endereco = servidor.Address!;

        try
        {
            using var corpo = new StringContent(json, Encoding.UTF8, "application/json");
            using var pedido = new HttpRequestMessage(HttpMethod.Post, $"{endereco}/modulos") { Content = corpo };

            if (servidor.IsClivus) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave!.Trim());
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
                    Pendentes[modulo.Model.Trim()] = (modulo, eletrica);

                    return (true, true, pan is not null && ModuleLibrary.ServiceKeptPan(texto)
                        ? Tr.F("O módulo {0} foi para o servidor do Clivus, com o PAN {1}, e espera a validação da equipe. Depois de conferido, ele entra na lista de todos; até lá, fica na lista das janelas de Mesa desta sessão do Civil 3D.", modulo.DisplayName, panNome ?? string.Empty)
                        : Tr.F("O módulo {0} foi para o servidor do Clivus e espera a validação da equipe. Depois de conferido, ele entra na lista de todos; até lá, fica na lista das janelas de Mesa desta sessão do Civil 3D.", modulo.DisplayName));
                }

                // No serviço local ele entra na lista na hora: a guardada já não vale.
                _ultimaLista = null;

                return (true, false, pan is null
                    ? Tr.F("Módulo {0} cadastrado no serviço.", modulo.DisplayName)
                    : ModuleLibrary.ServiceKeptPan(texto)
                        ? Tr.F("Módulo {0} cadastrado no serviço, com o PAN {1}.", modulo.DisplayName, panNome ?? string.Empty)
                        : Tr.F("Módulo {0} cadastrado no serviço, mas sem o PAN: o serviço é de antes do cadastro com PAN. Atualize-o (tools\\servico-local.ps1), apague o módulo lá e cadastre de novo.", modulo.DisplayName));
            }

            // No servidor do Clivus, "já existe" é de um módulo que espera a
            // gestão (mandado antes, talvez em outra sessão) ou que ela já
            // aprovou: o envio já foi. Ele fica com os pendentes, e a próxima
            // lista mostra se foi aprovado (revisão do 38.8).
            if (servidor.IsClivus && resposta.StatusCode == HttpStatusCode.Conflict)
            {
                Pendentes[modulo.Model.Trim()] = (modulo, eletrica);
                _ultimaLista = null;

                return (true, true, Tr.F("O módulo {0} já está no servidor do Clivus: espera a validação da equipe ou já foi aprovado. Nesta sessão do Civil 3D, ele fica na lista das janelas de Mesa.", modulo.DisplayName));
            }

            return (false, false, resposta.StatusCode == HttpStatusCode.Conflict
                ? ModuleServiceReply.ErrorMessage(texto) ?? Tr.T("Já existe um módulo com esse modelo.")
                : Tr.F("O serviço recusou ({0}): {1}", (int)resposta.StatusCode, ModuleServiceReply.ErrorMessage(texto) ?? Cortar(texto)));
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            RegistroDeDiagnostico.Registrar($"Falha ao cadastrar módulo em {endereco}.", erro);
            return (false, false, servidor.IsClivus
                ? Tr.F("O servidor do Clivus não respondeu em {0}. Se ele estava no ar, o módulo pode ter ido: mande de novo daqui a pouco, e o Clivus diz se ele já está lá.", endereco)
                : Tr.F("O serviço de módulos não respondeu em {0}. Se ele estava no ar, o módulo pode ter sido gravado: feche e abra a janela de Mesa para conferir a lista. Fora do ar, rode tools\\servico-local.ps1.", endereco));
        }
    }

    /// <summary>Resposta crua (uma página de erro) cortada para caber numa mensagem.</summary>
    private static string Cortar(string texto) =>
        texto.Length <= 200 ? texto : texto[..200] + "...";
}
