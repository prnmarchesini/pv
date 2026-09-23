using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UFV.Core;

/// <summary>
/// Uma mesa inteira guardada com um nome: "Mesa do Renan 28 módulos".
///
/// Existe para o projetista não digitar dez campos a cada projeto, e para dois
/// projetos da mesma mesa saírem iguais.
///
/// O risco deste arquivo não é o JSON — é o tempo. Um perfil gravado hoje será
/// aberto daqui a um ano, por outra versão do plugin, num computador com o
/// separador decimal diferente. Se qualquer uma dessas coisas mudar um número
/// em silêncio, a usina sai com a mesa errada e ninguém desconfia, porque o
/// nome é o mesmo. Daí as decisões do formato:
///
/// <list type="bullet">
/// <item><description>número sempre com ponto decimal, invariante de
/// cultura;</description></item>
/// <item><description>arranjo gravado como texto E lido só como texto. Aceitar
/// o número da enumeração era pior que gravá-lo: um <c>7</c> no arquivo não é
/// nem 1V nem 2V, toda comparação com 2V dá falso, e a mesa sai com o dobro do
/// comprimento sem erro nenhum;</description></item>
/// <item><description>campo ausente é recusado pelo nome, e não lido como
/// zero. Zero é valor legítimo para folga e para margem, então "faltou" e
/// "vale zero" são indistinguíveis se o campo não for anulável — e um perfil
/// sem <c>horizontalGap</c> carregava "com sucesso" 26 cm mais curto;</description></item>
/// <item><description>campo desconhecido é recusado, não ignorado. É para isso
/// que existe a versão do formato: quem acrescentar campo sobe a
/// versão;</description></item>
/// <item><description>versão do formato no arquivo, e arquivo de outra versão
/// é recusado em vez de lido pela metade.</description></item>
/// </list>
///
/// A inclinação é o único número do projeto que vai para o arquivo em GRAUS.
/// Um perfil é feito para ser aberto num editor de texto e conferido de
/// relance, e "0.3490658503988659" não se confere. O motor continua em
/// radianos, como manda a arquitetura; a conversão acontece na borda, que é
/// aqui.
/// </summary>
/// <param name="Name">O nome pelo qual o projetista reconhece a mesa.</param>
/// <param name="Layout">Módulo, quantidade, arranjo e folgas.</param>
/// <param name="Frame">Tesoura e pilar.</param>
/// <param name="TiltRadians">
/// A inclinação, em RADIANOS. O nome diz a unidade de propósito: ao lado de
/// <see cref="TiltDegrees"/>, um campo chamado só "Tilt" é meio caminho andado
/// para alguém ligar nele o campo em graus da janela.
/// </param>
public sealed record TableProfile(
    string Name,
    TableLayout Layout,
    TableFrame Frame,
    double TiltRadians)
{
    /// <summary>
    /// A versão do formato do arquivo.
    ///
    /// Um arquivo de versão diferente é recusado, e não lido pelo que der:
    /// ler pela metade entrega uma mesa parecida com a que o projetista
    /// salvou, e parecida é o pior resultado possível.
    /// </summary>
    /// <remarks>
    /// Versão 2: o balanço das pontas e o vão pretendido entre pilares
    /// entraram na estrutura, quando o Renan confirmou que os dois são escolha
    /// de projeto e não constante. Perfil da versão 1 é recusado com o motivo,
    /// e não lido sem esses dois campos — ler pela metade daria uma mesa
    /// parecida com a que ele salvou, e parecida é o pior resultado possível.
    /// </remarks>
    public const int VersaoDoFormato = 2;

    /// <summary>
    /// Casas decimais do grau gravado no arquivo.
    ///
    /// Sem arredondar, a volta grau → radiano → grau não fecha: 37,5° saía
    /// como <c>37.50000000000001</c> e 69° como <c>68.99999999999999</c>. São
    /// 37 dos 901 ângulos de décimo em décimo entre 0° e 90°, e cada um deles
    /// fazia o perfil voltar diferente do que entrou.
    ///
    /// Nove casas são 1e-9 grau, ou 1,7e-11 radiano — muito abaixo de qualquer
    /// coisa que exista numa obra, e o bastante para o número no arquivo ser o
    /// que o projetista digitou.
    /// </summary>
    private const int CasasDoGrau = 9;

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        // camelCase no arquivo: é a forma que o projetista vê ao abrir o
        // perfil num editor, e a que o resto do mundo espera de JSON.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Campo que este plugin não conhece é erro, não ruído para ignorar.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // allowIntegerValues: false — ver o comentário da classe.
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    /// <summary>Se o perfil descreve uma mesa que existe.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de o perfil não servir, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "o perfil precisa de um nome";

            if (Layout is null) return "o perfil não traz a mesa";
            if (Frame is null) return "o perfil não traz a estrutura";

            if (Layout.WhyInvalid is { } porCausaDaMesa) return porCausaDaMesa;
            if (Frame.WhyInvalid is { } porCausaDaEstrutura) return porCausaDaEstrutura;

            if (!double.IsFinite(TiltRadians) || TiltRadians < 0 || TiltRadians > PillarSizing.MaiorInclinacao)
                return "a inclinação está fora da faixa de uma mesa";

            // A mesma conferência que a geometria faz, e do mesmo lugar: sem
            // ela aqui, um perfil errado só explodiria na hora de desenhar,
            // com o projetista já tendo escolhido a área. Chamar a de lá, em
            // vez de repetir a regra, é o que impede as duas divergirem.
            return Frame.WhyDoesNotFit(Layout);
        }
    }

    /// <summary>
    /// A inclinação em graus, como ela vai para o arquivo e para a tela.
    /// </summary>
    public double TiltDegrees => TiltRadians * 180 / Math.PI;

    /// <summary>
    /// O perfil como texto JSON.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se o perfil descreve uma mesa que não existe. Gravar assim guardaria
    /// uma mesa impossível com nome bonito — e o nome é exatamente o que faz
    /// ninguém desconfiar.
    /// </exception>
    public string ToJson()
    {
        if (WhyInvalid is { } motivo)
            throw new InvalidOperationException($"O perfil não pode ser salvo: {motivo}.");

        var arquivo = new Arquivo
        {
            FormatVersion = VersaoDoFormato,
            // Sem Trim: trimar só na gravação faria o perfil voltar diferente
            // do que entrou, que é exatamente o que este arquivo existe para
            // evitar. Nome só de espaço já foi recusado pela validação.
            Name = Name,
            TiltDegrees = Math.Round(TiltDegrees, CasasDoGrau),
            Layout = new LayoutNoArquivo
            {
                Brand = Layout.Module.Brand,
                Model = Layout.Module.Model,
                PowerWatts = Layout.Module.PowerWatts,
                Height = Layout.Module.Height,
                Width = Layout.Module.Width,
                Thickness = Layout.Module.Thickness,
                ModuleCount = Layout.ModuleCount,
                Arrangement = Layout.Arrangement,
                HorizontalGap = Layout.HorizontalGap,
                VerticalGap = Layout.VerticalGap,
                LeftMargin = Layout.LeftMargin,
                RightMargin = Layout.RightMargin,
            },
            Frame = new FrameNoArquivo
            {
                RafterLength = Frame.RafterLength,
                PillarAlongRafter = Frame.PillarAlongRafter,
                PillarWidth = Frame.PillarWidth,
                PillarDepth = Frame.PillarDepth,
                PillarSpanTarget = Frame.PillarSpanTarget,
                PillarCantilever = Frame.PillarCantilever,
            },
        };

        return JsonSerializer.Serialize(arquivo, Opcoes);
    }

    /// <summary>
    /// Lê um perfil do texto JSON.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se o texto não for um perfil, se for de outra versão do formato, se
    /// faltar campo, ou se descrever uma mesa que não existe.
    /// </exception>
    public static TableProfile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Arquivo? arquivo;

        try
        {
            arquivo = JsonSerializer.Deserialize<Arquivo>(json, Opcoes);
        }
        catch (JsonException erro)
        {
            // A mensagem do System.Text.Json vem em inglês e cita o nome do
            // tipo interno. Isso serve para o log, não para a janela: o texto
            // do usuário é nosso, e o detalhe fica na exceção de dentro.
            throw new InvalidOperationException(
                "O arquivo não é um perfil de mesa que este plugin saiba ler.", erro);
        }

        if (arquivo is null) throw new InvalidOperationException("O perfil de mesa está vazio.");

        if (arquivo.FormatVersion is not { } versao)
            throw new InvalidOperationException("O perfil de mesa não diz de que versão do formato é.");

        if (versao != VersaoDoFormato)
        {
            throw new InvalidOperationException(
                $"O perfil de mesa é da versão {versao} do formato, e este plugin "
                + $"lê a versão {VersaoDoFormato}.");
        }

        if (arquivo.Layout is null) throw new InvalidOperationException("O perfil de mesa não traz a mesa.");
        if (arquivo.Frame is null) throw new InvalidOperationException("O perfil de mesa não traz a estrutura.");

        var arranjo = Exigir(arquivo.Layout.Arrangement, "layout.arrangement");

        if (!Enum.IsDefined(arranjo))
        {
            throw new InvalidOperationException(
                $"O perfil de mesa traz um arranjo que este plugin não conhece: {arranjo}.");
        }

        var modulo = new SolarModule(
            arquivo.Layout.Brand ?? string.Empty,
            arquivo.Layout.Model ?? string.Empty,
            Exigir(arquivo.Layout.PowerWatts, "layout.powerWatts"),
            Exigir(arquivo.Layout.Height, "layout.height"),
            Exigir(arquivo.Layout.Width, "layout.width"),
            Exigir(arquivo.Layout.Thickness, "layout.thickness"));

        var perfil = new TableProfile(
            arquivo.Name ?? string.Empty,
            new TableLayout(
                modulo,
                Exigir(arquivo.Layout.ModuleCount, "layout.moduleCount"),
                arranjo,
                Exigir(arquivo.Layout.HorizontalGap, "layout.horizontalGap"),
                Exigir(arquivo.Layout.VerticalGap, "layout.verticalGap"),
                Exigir(arquivo.Layout.LeftMargin, "layout.leftMargin"),
                Exigir(arquivo.Layout.RightMargin, "layout.rightMargin")),
            new TableFrame(
                Exigir(arquivo.Frame.RafterLength, "frame.rafterLength"),
                Exigir(arquivo.Frame.PillarAlongRafter, "frame.pillarAlongRafter"),
                Exigir(arquivo.Frame.PillarWidth, "frame.pillarWidth"),
                Exigir(arquivo.Frame.PillarDepth, "frame.pillarDepth"),
                Exigir(arquivo.Frame.PillarSpanTarget, "frame.pillarSpanTarget"),
                Exigir(arquivo.Frame.PillarCantilever, "frame.pillarCantilever")),
            Exigir(arquivo.TiltDegrees, "tiltDegrees") * Math.PI / 180);

        if (perfil.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"O perfil de mesa não descreve uma mesa: {motivo}.");

        return perfil;
    }

    /// <summary>A linha que descreve o perfil para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Perfil inválido: {motivo}.";

        return $"{Name.Trim()} — {Layout.Describe()}, a {Texto(TiltDegrees)}°";
    }

    /// <summary>
    /// O valor, ou a recusa nomeando o campo que faltou.
    ///
    /// Sem isto, campo ausente virava zero — e zero é valor legítimo para
    /// folga e para margem. "Faltou" e "vale zero" ficavam indistinguíveis, e
    /// um perfil sem <c>horizontalGap</c> carregava sem um aviso, 26 cm mais
    /// curto.
    /// </summary>
    private static T Exigir<T>(T? valor, string campo) where T : struct =>
        valor ?? throw new InvalidOperationException(
            $"O perfil de mesa não traz o campo \"{campo}\".");

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);

    /// <summary>
    /// O perfil como ele está no arquivo.
    ///
    /// Separado dos objetos do motor de propósito: o formato do arquivo pode
    /// mudar sem arrastar o modelo, e o modelo pode mudar sem quebrar todo
    /// perfil já gravado. É a mesma razão pela qual a biblioteca de módulos
    /// tem a sua própria <c>Entrada</c>.
    ///
    /// Tudo anulável: é assim que "o campo não veio" deixa de ser a mesma
    /// coisa que "o campo vale zero".
    /// </summary>
    private sealed class Arquivo
    {
        public int? FormatVersion { get; init; }
        public string? Name { get; init; }
        public double? TiltDegrees { get; init; }
        public LayoutNoArquivo? Layout { get; init; }
        public FrameNoArquivo? Frame { get; init; }
    }

    private sealed class LayoutNoArquivo
    {
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public double? PowerWatts { get; init; }
        public double? Height { get; init; }
        public double? Width { get; init; }
        public double? Thickness { get; init; }
        public int? ModuleCount { get; init; }
        public TableArrangement? Arrangement { get; init; }
        public double? HorizontalGap { get; init; }
        public double? VerticalGap { get; init; }
        public double? LeftMargin { get; init; }
        public double? RightMargin { get; init; }
    }

    private sealed class FrameNoArquivo
    {
        public double? RafterLength { get; init; }
        public double? PillarAlongRafter { get; init; }
        public double? PillarWidth { get; init; }
        public double? PillarDepth { get; init; }
        public double? PillarSpanTarget { get; init; }
        public double? PillarCantilever { get; init; }
    }
}
