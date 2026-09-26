using System.Globalization;

namespace UFV.Core;

/// <summary>O que a leitura das configurações encontrou.</summary>
/// <param name="Settings">As configurações lidas, ou null se não havia ou não deu para ler.</param>
/// <param name="Problem">
/// O que estava errado, em português, ou null. Null com <paramref name="Settings"/>
/// null é ausência: o desenho nunca teve configuração gravada, que é o estado
/// normal antes da primeira vez.
/// </param>
public sealed record ProjectSettingsResult(ProjectSettings? Settings, string? Problem);

/// <summary>
/// As configurações do projeto: a configuração do sistema (4.1) e as regras de
/// análise (4.3) juntas, como a tela do 4.4 mostra e como vão para o desenho.
///
/// Juntas porque são gravadas e lidas juntas, num registro só do desenho: o
/// projetista configura o projeto uma vez, salva o DWG, e meses depois, noutra
/// máquina, abre e encontra tudo como deixou. É a validação do passo, com
/// todas as letras: "salvar e reabrir preserva tudo".
///
/// O formato é uma lista de pares nome/valor em texto, invariante. Pares, e
/// não posição, para um campo novo não deslocar os anteriores; texto
/// invariante porque o desenho muda de máquina e "0,3" lido em cultura errada
/// vira 3. O Core define o formato e o testa; o plugin só leva a lista ao
/// dicionário do desenho e traz de volta.
/// </summary>
/// <param name="Configuration">Os limites do projeto.</param>
/// <param name="Analyses">O que se pinta, de que cor, em que camada.</param>
public sealed record ProjectSettings(SystemConfiguration Configuration, AnalysisRules Analyses)
{
    /// <summary>Marca do campo de versão, sempre o primeiro.</summary>
    public const string CampoVersao = "FORMATO";

    /// <summary>
    /// Versão do formato. Um registro de outra versão é recusado com o motivo,
    /// não lido pela metade: um formato futuro que troque a unidade de um
    /// campo faria a leitura antiga entender errado o que está escrito.
    /// </summary>
    public const int VersaoDoFormato = 1;

    // Os nomes ficam gravados no arquivo do usuário: mudar qualquer um torna
    // ilegível a configuração dos desenhos já salvos.
    private const string CampoAzimute = "AZIMUTE_RAD";
    private const string CampoPitch = "PITCH";
    private const string CampoPontaBaixaMin = "PONTA_BAIXA_MIN";
    private const string CampoPontaBaixaMax = "PONTA_BAIXA_MAX";
    private const string CampoEnterroMin = "ENTERRO_MIN";
    private const string CampoEnterroMax = "ENTERRO_MAX";
    private const string CampoDegrauMin = "DEGRAU_MIN";
    private const string CampoDegrauMax = "DEGRAU_MAX";
    private const string CampoLombo = "LOMBO_MODULOS";
    private const string CampoDeclividadeMax = "DECLIVIDADE_MAX_RAD";
    private const string CampoEspacamento = "ESPACAMENTO_QUEBRA";
    private const string CampoPilarPintarAcima = "PILAR_PINTAR_ACIMA";

    private const string SufixoLigada = "_LIGADA";
    private const string SufixoCamada = "_CAMADA";
    private const string SufixoCorAbaixo = "_ABAIXO_COR";
    private const string SufixoCorAcima = "_ACIMA_COR";
    private const string SufixoCor = "_COR";
    private const string PrefixoBorda = "BORDA";

    /// <summary>As configurações de partida: os dois padrões juntos.</summary>
    public static readonly ProjectSettings Default = new(SystemConfiguration.Default, AnalysisRules.Default);

    /// <summary>
    /// Uma configuração em que TODO campo gravado difere do padrão — inclusive
    /// cada camada, cada cor e cada "ligada". É a amostra do teste de nível 2
    /// e do teste de ida e volta: um campo esquecido na gravação ou na
    /// leitura voltaria com o padrão, e só se vê isso se o valor de partida
    /// era outro. Há teste que confere a diferença campo a campo.
    /// </summary>
    public static ProjectSettings SampleAllDifferent()
    {
        const double grau = Math.PI / 180;

        var config = new SystemConfiguration(
            FacingAzimuthRadians: 15 * grau,
            Pitch: 7.5,
            MinLowEdge: 0.35,
            MaxLowEdge: 0.95,
            MinEmbedment: 1.1,
            MaxEmbedment: 2.4,
            MinStep: 0.05,
            MaxStep: 0.65,
            BumpToleranceModules: 3,
            MaxLongitudinalSlope: 12 * grau,
            MaxGapBeforeBreak: 0.75);

        // Cores diferentes de análise para análise: com todas iguais, uma
        // leitura que trocasse o prefixo de uma análise pelo de outra passaria
        // despercebida.
        var analises = new AnalysisRules(
            LowEdge: new AnalysisRule(false, "TESTE_PONTA_BAIXA", new RgbColor(255, 200, 0), new RgbColor(0, 128, 0)),
            PillarLength: new AnalysisRule(false, "TESTE_PILAR", new RgbColor(255, 128, 0), new RgbColor(0, 200, 200)),
            Embedment: new AnalysisRule(false, "TESTE_ENTERRO", new RgbColor(128, 0, 200), new RgbColor(255, 105, 180)),
            LongitudinalSlope: new AnalysisRule(false, "TESTE_DECLIVIDADE", new RgbColor(150, 90, 40), new RgbColor(255, 255, 0)),
            EdgeRule: new EdgeRule(false, "TESTE_BORDA", new RgbColor(0, 0, 0)),
            PaintPillarsLongerThan: 2.75);

        return new ProjectSettings(config, analises);
    }

    /// <summary>Se as duas partes fecham.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de não fechar, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (Configuration is null) return "a configuração do sistema está ausente";
            if (Analyses is null) return "as regras de análise estão ausentes";

            return Configuration.WhyInvalid ?? Analyses.WhyInvalid;
        }
    }

    /// <summary>As duas linhas que descrevem as configurações para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Configuração inválida: {motivo}.";

        return $"{Configuration.Describe()}; {Analyses.Describe()}";
    }

    /// <summary>
    /// Os pares nome/valor que descrevem as configurações, com a versão do
    /// formato em primeiro.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se as configurações não fecham. Gravar um estado inválido faria a
    /// próxima abertura recusar o que o próprio plugin escreveu.
    /// </exception>
    public IReadOnlyList<KeyValuePair<string, string>> ToFields()
    {
        if (WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não pode ser gravada: {motivo}.");

        var c = Configuration;
        var a = Analyses;

        var campos = new List<KeyValuePair<string, string>>
        {
            Par(CampoVersao, VersaoDoFormato.ToString(CultureInfo.InvariantCulture)),
            Par(CampoAzimute, Numero(c.FacingAzimuthRadians)),
            Par(CampoPitch, Numero(c.Pitch)),
            Par(CampoPontaBaixaMin, Numero(c.MinLowEdge)),
            Par(CampoPontaBaixaMax, Numero(c.MaxLowEdge)),
            Par(CampoEnterroMin, Numero(c.MinEmbedment)),
            Par(CampoEnterroMax, Numero(c.MaxEmbedment)),
            Par(CampoDegrauMin, Numero(c.MinStep)),
            Par(CampoDegrauMax, Numero(c.MaxStep)),
            Par(CampoLombo, c.BumpToleranceModules.ToString(CultureInfo.InvariantCulture)),
            Par(CampoDeclividadeMax, Opcional(c.MaxLongitudinalSlope)),
            Par(CampoEspacamento, Numero(c.MaxGapBeforeBreak)),
            Par(CampoPilarPintarAcima, Opcional(a.PaintPillarsLongerThan)),
        };

        foreach (var kind in AnalysisRules.RangedKinds)
        {
            var regra = a.Rule(kind);
            var prefixo = Prefixo(kind);

            campos.Add(Par(prefixo + SufixoLigada, Booleano(regra.Enabled)));
            campos.Add(Par(prefixo + SufixoCamada, regra.Layer));
            campos.Add(Par(prefixo + SufixoCorAbaixo, regra.BelowColor.ToHex()));
            campos.Add(Par(prefixo + SufixoCorAcima, regra.AboveColor.ToHex()));
        }

        campos.Add(Par(PrefixoBorda + SufixoLigada, Booleano(a.EdgeRule.Enabled)));
        campos.Add(Par(PrefixoBorda + SufixoCamada, a.EdgeRule.Layer));
        campos.Add(Par(PrefixoBorda + SufixoCor, a.EdgeRule.Color.ToHex()));

        return campos;
    }

    /// <summary>
    /// Lê os pares de volta.
    ///
    /// Nada é preenchido com o padrão em silêncio: campo faltando ou ilegível
    /// é problema que nomeia o campo. Preencher calado faria o projetista
    /// perder um número que digitou e não saber. Campo desconhecido é
    /// ignorado, para uma versão futura que só acrescente campos não tornar o
    /// registro ilegível para esta.
    /// </summary>
    /// <param name="fields">Os pares, ou null / vazio para "nunca gravado".</param>
    public static ProjectSettingsResult Parse(IReadOnlyList<KeyValuePair<string, string>>? fields)
    {
        if (fields is null || fields.Count == 0) return new ProjectSettingsResult(null, null);

        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (chave, valor) in fields)
        {
            if (!string.IsNullOrWhiteSpace(chave)) campos[chave] = valor ?? string.Empty;
        }

        if (!campos.TryGetValue(CampoVersao, out var versaoTexto))
            return Problema("a configuração gravada não diz de que versão do formato é");

        if (!int.TryParse(versaoTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var versao)
            || versao != VersaoDoFormato)
        {
            return Problema(
                $"a configuração gravada é da versão {versaoTexto} do formato, e este plugin "
                + $"lê a versão {VersaoDoFormato}");
        }

        var leitor = new Leitor(campos);

        var configuracao = new SystemConfiguration(
            FacingAzimuthRadians: leitor.Real(CampoAzimute),
            Pitch: leitor.Real(CampoPitch),
            MinLowEdge: leitor.Real(CampoPontaBaixaMin),
            MaxLowEdge: leitor.Real(CampoPontaBaixaMax),
            MinEmbedment: leitor.Real(CampoEnterroMin),
            MaxEmbedment: leitor.Real(CampoEnterroMax),
            MinStep: leitor.Real(CampoDegrauMin),
            MaxStep: leitor.Real(CampoDegrauMax),
            BumpToleranceModules: leitor.Inteiro(CampoLombo),
            MaxLongitudinalSlope: leitor.RealOpcional(CampoDeclividadeMax),
            MaxGapBeforeBreak: leitor.Real(CampoEspacamento));

        AnalysisRule Regra(AnalysisKind kind)
        {
            var prefixo = Prefixo(kind);

            return new AnalysisRule(
                Enabled: leitor.Booleano(prefixo + SufixoLigada),
                Layer: leitor.Texto(prefixo + SufixoCamada),
                BelowColor: leitor.Cor(prefixo + SufixoCorAbaixo),
                AboveColor: leitor.Cor(prefixo + SufixoCorAcima));
        }

        var analises = new AnalysisRules(
            LowEdge: Regra(AnalysisKind.LowEdge),
            PillarLength: Regra(AnalysisKind.PillarLength),
            Embedment: Regra(AnalysisKind.Embedment),
            LongitudinalSlope: Regra(AnalysisKind.LongitudinalSlope),
            EdgeRule: new EdgeRule(
                Enabled: leitor.Booleano(PrefixoBorda + SufixoLigada),
                Layer: leitor.Texto(PrefixoBorda + SufixoCamada),
                Color: leitor.Cor(PrefixoBorda + SufixoCor)),
            PaintPillarsLongerThan: leitor.RealOpcional(CampoPilarPintarAcima));

        if (leitor.Problema is { } problema) return Problema(problema);

        var lido = new ProjectSettings(configuracao, analises);

        if (lido.WhyInvalid is { } motivo)
            return Problema($"a configuração gravada não fecha: {motivo}");

        return new ProjectSettingsResult(lido, null);
    }

    private static ProjectSettingsResult Problema(string motivo) => new(null, motivo);

    private static KeyValuePair<string, string> Par(string chave, string valor) => new(chave, valor);

    private static string Prefixo(AnalysisKind kind) => kind switch
    {
        AnalysisKind.LowEdge => "PONTA_BAIXA",
        AnalysisKind.PillarLength => "PILAR",
        AnalysisKind.Embedment => "ENTERRO",
        AnalysisKind.LongitudinalSlope => "DECLIVIDADE",
        // A borda não passa por aqui: os campos dela usam PrefixoBorda direto.
        _ => throw new ArgumentException($"A análise {kind} não é de faixa.", nameof(kind)),
    };

    /// <summary>Formato redondo e invariante: o número volta exatamente igual.</summary>
    private static string Numero(double valor) => valor.ToString("R", CultureInfo.InvariantCulture);

    private static string Opcional(double? valor) => valor is { } v ? Numero(v) : string.Empty;

    private static string Booleano(bool valor) => valor ? "1" : "0";

    /// <summary>
    /// Lê campo a campo e guarda o PRIMEIRO problema. Os campos são lidos
    /// todos de uma vez, e o construtor do record precisa de um valor para
    /// cada um mesmo quando algum está errado; por isso a leitura devolve
    /// zero no erro e anota, e quem chama confere o problema no fim.
    /// </summary>
    private sealed class Leitor(IReadOnlyDictionary<string, string> campos)
    {
        public string? Problema { get; private set; }

        public double Real(string chave)
        {
            if (!Pegar(chave, out var texto)) return 0;

            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
                return valor;

            Anotar($"o campo {chave} da configuração gravada não é um número");
            return 0;
        }

        public double? RealOpcional(string chave)
        {
            if (!Pegar(chave, out var texto)) return null;
            if (string.IsNullOrWhiteSpace(texto)) return null;

            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
                return valor;

            Anotar($"o campo {chave} da configuração gravada não é um número");
            return null;
        }

        public int Inteiro(string chave)
        {
            if (!Pegar(chave, out var texto)) return 0;

            if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
                return valor;

            Anotar($"o campo {chave} da configuração gravada não é um inteiro");
            return 0;
        }

        public bool Booleano(string chave)
        {
            if (!Pegar(chave, out var texto)) return false;

            switch (texto.Trim())
            {
                case "1": return true;
                case "0": return false;
                default:
                    Anotar($"o campo {chave} da configuração gravada não é 0 nem 1");
                    return false;
            }
        }

        public string Texto(string chave) => Pegar(chave, out var texto) ? texto : string.Empty;

        public RgbColor Cor(string chave)
        {
            if (!Pegar(chave, out var texto)) return default;

            if (RgbColor.TryParseHex(texto, out var cor)) return cor;

            Anotar($"o campo {chave} da configuração gravada não é uma cor #RRGGBB");
            return default;
        }

        private bool Pegar(string chave, out string texto)
        {
            if (campos.TryGetValue(chave, out var valor))
            {
                texto = valor;
                return true;
            }

            Anotar($"o campo {chave} está faltando na configuração gravada");
            texto = string.Empty;
            return false;
        }

        private void Anotar(string motivo) => Problema ??= motivo;
    }
}
