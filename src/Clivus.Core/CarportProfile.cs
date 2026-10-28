using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clivus.Core;

/// <summary>Como o módulo deita no carport: retrato tem a medida maior na descida.</summary>
public enum CarportOrientation
{
    /// <summary>A medida maior do módulo (<see cref="SolarModule.Height"/>) na descida.</summary>
    Portrait,

    /// <summary>A medida maior do módulo ao longo da fila.</summary>
    Landscape,
}

/// <summary>
/// A seção de uma barra do carport, como vai para a lista de material.
/// </summary>
/// <param name="Name">O nome da peça para o fornecedor: "2 × C 130×50".</param>
/// <param name="Depth">A altura da seção (a alma), em metro.</param>
/// <param name="Width">A largura do conjunto (dois C costas com costas contam juntos), em metro.</param>
public sealed record CarportSection(string Name, double Depth, double Width);

/// <summary>A base de cada pilar: a chapa sobre o bloco.</summary>
/// <param name="PlateSize">O lado da chapa quadrada.</param>
/// <param name="PlateThickness">A espessura da chapa.</param>
/// <param name="PedestalSize">O lado do bloco quadrado.</param>
/// <param name="PedestalHeight">Quanto o bloco sobe acima do chão; o pilar começa na chapa, em cima dele.</param>
public sealed record CarportBase(double PlateSize, double PlateThickness, double PedestalSize, double PedestalHeight);

/// <summary>
/// Os módulos de um vão: quantos na descida, quantos ao longo da fila, e as folgas.
/// </summary>
/// <param name="Module">O módulo.</param>
/// <param name="Orientation">Retrato ou paisagem.</param>
/// <param name="ModulesDown">Módulos na descida (3 no modelo de referência).</param>
/// <param name="ModulesAcross">Módulos ao longo da fila em cada vão (5 no modelo).</param>
/// <param name="GapAcross">A folga entre colunas, ao longo da fila.</param>
/// <param name="GapDown">A folga entre módulos na descida.</param>
public sealed record CarportArray(
    SolarModule Module,
    CarportOrientation Orientation,
    int ModulesDown,
    int ModulesAcross,
    double GapAcross,
    double GapDown)
{
    /// <summary>A medida do módulo na descida.</summary>
    public double ModuleDown => Orientation == CarportOrientation.Portrait ? Module.Height : Module.Width;

    /// <summary>A medida do módulo ao longo da fila.</summary>
    public double ModuleAcross => Orientation == CarportOrientation.Portrait ? Module.Width : Module.Height;

    /// <summary>O comprimento da cobertura na rampa, da borda baixa à alta.</summary>
    public double SlopeLength => ModulesDown * ModuleDown + (ModulesDown - 1) * GapDown;

    /// <summary>
    /// O vão: a distância entre dois pórticos vizinhos. Na fila contínua o vão
    /// é a largura dos módulos dele mais a folga que o separa do vizinho.
    /// </summary>
    public double BayWidth => ModulesAcross * (ModuleAcross + GapAcross);
}

/// <summary>
/// O aço do carport biposte: o pórtico (dois pilares em V, a viga e as mãos
/// francesas), as terças e as bases. Distâncias "na rampa" correm sobre a
/// viga; as outras são em planta ou ao longo do pilar, como diz cada campo.
/// </summary>
/// <param name="PillarBaseSpacing">A distância em planta entre as bases dos dois pilares do pórtico.</param>
/// <param name="PillarLeanRadians">Quanto cada pilar abre para fora a partir da vertical, em RADIANOS.</param>
/// <param name="PillarOffset">
/// Quanto o meio das bases sai do meio da cobertura, em planta; positivo para
/// o lado alto. Zero no modelo de referência.
/// </param>
/// <param name="BeamLowInset">Quanto a viga para antes da borda baixa dos módulos, na rampa.</param>
/// <param name="BeamHighInset">Quanto a viga para antes da borda alta dos módulos, na rampa.</param>
/// <param name="PurlinsPerModule">Terças sob cada módulo na descida (2 no modelo).</param>
/// <param name="PurlinEdgeOffset">
/// Com duas ou mais terças por módulo: a distância, na rampa, da ponta do
/// módulo à terça mais perto dela. Com uma terça, ela vai no meio e este campo
/// não conta.
/// </param>
/// <param name="BraceOnPillar">Onde a mão francesa pega o pilar: a distância do topo do pilar para baixo, ao longo dele.</param>
/// <param name="BraceOnBeam">Onde a mão francesa pega a viga: a distância do topo do pilar para dentro do pórtico, na rampa.</param>
/// <param name="Pillar">A seção do pilar.</param>
/// <param name="Beam">A seção da viga principal.</param>
/// <param name="Purlin">A seção da terça.</param>
/// <param name="Brace">A seção da mão francesa.</param>
/// <param name="Base">A base de cada pilar.</param>
public sealed record CarportFrame(
    double PillarBaseSpacing,
    double PillarLeanRadians,
    double PillarOffset,
    double BeamLowInset,
    double BeamHighInset,
    int PurlinsPerModule,
    double PurlinEdgeOffset,
    double BraceOnPillar,
    double BraceOnBeam,
    CarportSection Pillar,
    CarportSection Beam,
    CarportSection Purlin,
    CarportSection Brace,
    CarportBase Base)
{
    /// <summary>A abertura do pilar em graus, como vai para o arquivo e para a tela.</summary>
    public double PillarLeanDegrees => PillarLeanRadians * 180 / Math.PI;
}

/// <summary>
/// Um carport guardado com um nome (etapa 34.1, <c>plano/carport</c>).
///
/// É o segundo tipo de estrutura do Clivus, ao lado da mesa fixa, e não
/// mexe nela: tem o seu próprio arquivo (<see cref="Extensao"/>), a sua
/// própria versão de formato e a sua própria guarda no desenho. As decisões do
/// formato são as do perfil da mesa (<see cref="TableProfile"/>), pelos mesmos
/// motivos: número com ponto, enumeração só como texto, campo que falta é
/// recusado pelo nome em vez de virar zero, campo desconhecido é recusado, e
/// arquivo de outra versão também. Ângulos vão em graus no arquivo.
///
/// O padrão (<see cref="Reference"/>) são as medidas do carport biposte de
/// referência (<c>plano/carport/01-modelo-de-referencia.md</c>).
/// </summary>
/// <param name="Name">O nome pelo qual o projetista reconhece o carport.</param>
/// <param name="Array">Os módulos de um vão.</param>
/// <param name="Frame">O aço.</param>
/// <param name="TiltRadians">A inclinação dos módulos, em RADIANOS.</param>
/// <param name="ClearHeight">
/// A altura livre: da borda baixa dos módulos (por baixo do quadro) ao chão
/// do pórtico. Em terreno com desnível, vale no pilar mais alto do pórtico,
/// para nenhum ponto ficar abaixo dela.
/// </param>
public sealed record CarportProfile(
    string Name,
    CarportArray Array,
    CarportFrame Frame,
    double TiltRadians,
    double ClearHeight)
{
    /// <summary>A versão do formato do arquivo.</summary>
    public const int VersaoDoFormato = 1;

    /// <summary>Extensão dos arquivos de carport.</summary>
    public const string Extensao = ".clivuscarport.json";

    // Os tetos e pisos não são regra de projeto: são rede para erro de escala
    // (milímetro no campo de metro) e para o campo trocado.
    private const double MaiorInclinacaoGraus = 30;
    private const double MaiorAberturaGraus = 30;
    private const double MenorAlturaLivre = 1.8;
    private const double MaiorAlturaLivre = 8.0;
    private const int MaiorNaDescida = 10;
    private const int MaiorNoVao = 20;
    private const double MaiorFolga = 0.10;
    private const double MenorSecao = 0.02;
    private const double MaiorSecao = 1.0;
    private const double MaiorBase = 2.0;
    private const int MaiorTercas = 4;

    private const int CasasDoGrau = 9;

    /// <summary>
    /// O carport biposte de referência (<c>0 - Assets/Cobertura/carport-biposte.glb</c>,
    /// medido em 28/10/2026): 3 módulos em retrato na descida, 5 por vão, 7°,
    /// borda baixa a 2,345 m. O módulo é o do modelo (2,278 × 1,134 × 0,030);
    /// a potência não está no modelo e é para ser trocada pelo módulo do projeto.
    /// </summary>
    public static CarportProfile Reference { get; } = new(
        "Carport biposte",
        new CarportArray(
            new SolarModule(string.Empty, "2278×1134", 600, 2.278, 1.134, 0.030),
            CarportOrientation.Portrait,
            ModulesDown: 3,
            ModulesAcross: 5,
            GapAcross: 0.0,
            GapDown: 0.010),
        new CarportFrame(
            PillarBaseSpacing: 2.425,
            PillarLeanRadians: 15 * Math.PI / 180,
            PillarOffset: 0.0,
            BeamLowInset: 0.549,
            BeamHighInset: 0.503,
            PurlinsPerModule: 2,
            PurlinEdgeOffset: 0.57,
            BraceOnPillar: 0.62,
            BraceOnBeam: 0.77,
            Pillar: new CarportSection("2 × C 130×50", 0.130, 0.100),
            Beam: new CarportSection("2 × C 110×50", 0.110, 0.100),
            Purlin: new CarportSection("C 127×50", 0.127, 0.050),
            Brace: new CarportSection("2 × C 75×25", 0.075, 0.050),
            Base: new CarportBase(0.31, 0.006, 0.31, 0.092)),
        7 * Math.PI / 180,
        2.345);

    /// <summary>A inclinação em graus, como vai para o arquivo e para a tela.</summary>
    public double TiltDegrees => TiltRadians * 180 / Math.PI;

    /// <summary>Se o perfil descreve um carport que existe.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de o perfil não servir, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return Tr.T("o carport precisa de um nome");
            if (Array is null) return Tr.T("o carport não traz os módulos");
            if (Frame is null) return Tr.T("o carport não traz a estrutura");

            if (Array.Module is null || !Array.Module.IsValid) return Tr.T("o módulo do carport não serve");
            if (!Enum.IsDefined(Array.Orientation)) return Tr.T("a posição do módulo do carport não é retrato nem paisagem");
            if (Array.ModulesDown < 1 || Array.ModulesDown > MaiorNaDescida)
                return Faixa(Tr.T("módulos na descida"), 1, MaiorNaDescida);
            if (Array.ModulesAcross < 1 || Array.ModulesAcross > MaiorNoVao)
                return Faixa(Tr.T("módulos por vão"), 1, MaiorNoVao);
            if (!Entre(Array.GapAcross, 0, MaiorFolga)) return Faixa(Tr.T("folga entre colunas"), 0, MaiorFolga);
            if (!Entre(Array.GapDown, 0, MaiorFolga)) return Faixa(Tr.T("folga na descida"), 0, MaiorFolga);

            if (!Entre(TiltDegrees, 0, MaiorInclinacaoGraus)) return Faixa(Tr.T("inclinação"), 0, MaiorInclinacaoGraus);
            if (!Entre(ClearHeight, MenorAlturaLivre, MaiorAlturaLivre)) return Faixa(Tr.T("altura livre"), MenorAlturaLivre, MaiorAlturaLivre);

            var rampa = Array.SlopeLength;
            var planta = rampa * Math.Cos(TiltRadians);

            if (!Entre(Frame.PillarLeanDegrees, 0, MaiorAberturaGraus)) return Faixa(Tr.T("abertura do pilar"), 0, MaiorAberturaGraus);
            if (!(Frame.PillarBaseSpacing > 0) || Frame.PillarBaseSpacing >= planta)
                return Tr.T("a distância entre as bases dos pilares precisa ser maior que zero e menor que a cobertura");
            if (!double.IsFinite(Frame.PillarOffset) || Math.Abs(Frame.PillarOffset) + Frame.PillarBaseSpacing / 2 >= planta / 2)
                return Tr.T("com esse deslocamento, um pilar sai de baixo da cobertura");

            if (!Entre(Frame.BeamLowInset, 0, rampa / 2)) return Faixa(Tr.T("recuo da viga na borda baixa"), 0, rampa / 2);
            if (!Entre(Frame.BeamHighInset, 0, rampa / 2)) return Faixa(Tr.T("recuo da viga na borda alta"), 0, rampa / 2);
            // Se a viga alcança o topo dos dois pilares depende da geometria do
            // pórtico: quem confere é a CarportGeometry (34.2).

            if (Frame.PurlinsPerModule < 1 || Frame.PurlinsPerModule > MaiorTercas)
                return Faixa(Tr.T("terças por módulo"), 1, MaiorTercas);
            if (Frame.PurlinsPerModule > 1 && !(Frame.PurlinEdgeOffset >= 0 && Frame.PurlinEdgeOffset < Array.ModuleDown / 2))
                return Tr.T("a terça precisa ficar debaixo do módulo: a distância da ponta do módulo à terça vai de zero até a metade do módulo");

            if (!(Frame.BraceOnPillar > 0) || !(Frame.BraceOnBeam > 0) || !double.IsFinite(Frame.BraceOnPillar) || !double.IsFinite(Frame.BraceOnBeam))
                return Tr.T("a mão francesa precisa pegar o pilar e a viga a uma distância maior que zero");
            if (Frame.BraceOnPillar >= ClearHeight) return Tr.T("a mão francesa pega o pilar abaixo do chão: diminua a distância no pilar");

            foreach (var (secao, nome) in new[] { (Frame.Pillar, Tr.T("pilar")), (Frame.Beam, Tr.T("viga")), (Frame.Purlin, Tr.T("terça")), (Frame.Brace, Tr.T("mão francesa")) })
            {
                if (secao is null || string.IsNullOrWhiteSpace(secao.Name)) return Tr.F("a seção do(a) {0} precisa de um nome", nome);
                if (!Entre(secao.Depth, MenorSecao, MaiorSecao) || !Entre(secao.Width, MenorSecao, MaiorSecao))
                    return Tr.F("a seção do(a) {0} está fora da faixa ({1} a {2} m)", nome, MenorSecao, MaiorSecao);
            }

            var b = Frame.Base;
            if (b is null) return Tr.T("o carport não traz a base dos pilares");
            if (!Entre(b.PlateSize, MenorSecao, MaiorBase) || !Entre(b.PedestalSize, MenorSecao, MaiorBase))
                return Faixa(Tr.T("tamanho da base"), MenorSecao, MaiorBase);
            if (!Entre(b.PlateThickness, 0.001, 0.1)) return Faixa(Tr.T("espessura da chapa da base"), 0.001, 0.1);
            if (!Entre(b.PedestalHeight, 0, 1.0)) return Faixa(Tr.T("altura do bloco da base"), 0, 1.0);

            return null;
        }
    }

    /// <summary>A linha que descreve o carport para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return Tr.F("Carport inválido: {0}.", motivo);

        return Tr.F("{0} — {1} × {2} {3} por vão de {4:0.###} m, a {5:0.###}°, altura livre {6:0.###} m",
            Name.Trim(), Array.ModulesDown, Array.ModulesAcross,
            Array.Orientation == CarportOrientation.Portrait ? Tr.T("em retrato") : Tr.T("em paisagem"),
            Array.BayWidth, TiltDegrees, ClearHeight);
    }

    private static bool Entre(double valor, double menor, double maior) =>
        double.IsFinite(valor) && valor >= menor && valor <= maior;

    private static string Faixa(string campo, double menor, double maior) =>
        Tr.F("{0} fora da faixa ({1} a {2})", campo, menor, maior);

    // ------------------------------------------------------------- arquivo

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    /// <summary>O perfil como texto JSON.</summary>
    /// <exception cref="InvalidOperationException">Se o perfil descreve um carport que não existe.</exception>
    public string ToJson()
    {
        if (WhyInvalid is { } motivo)
            throw new InvalidOperationException(Tr.F("O carport não pode ser salvo: {0}.", motivo));

        static SecaoNoArquivo Secao(CarportSection s) => new() { Name = s.Name, Depth = s.Depth, Width = s.Width };

        var arquivo = new Arquivo
        {
            FormatVersion = VersaoDoFormato,
            Name = Name,
            TiltDegrees = Math.Round(TiltDegrees, CasasDoGrau),
            ClearHeight = ClearHeight,
            Array = new ArranjoNoArquivo
            {
                Brand = Array.Module.Brand,
                Model = Array.Module.Model,
                PowerWatts = Array.Module.PowerWatts,
                Height = Array.Module.Height,
                Width = Array.Module.Width,
                Thickness = Array.Module.Thickness,
                Orientation = Array.Orientation,
                ModulesDown = Array.ModulesDown,
                ModulesAcross = Array.ModulesAcross,
                GapAcross = Array.GapAcross,
                GapDown = Array.GapDown,
            },
            Frame = new AcoNoArquivo
            {
                PillarBaseSpacing = Frame.PillarBaseSpacing,
                PillarLeanDegrees = Math.Round(Frame.PillarLeanDegrees, CasasDoGrau),
                PillarOffset = Frame.PillarOffset,
                BeamLowInset = Frame.BeamLowInset,
                BeamHighInset = Frame.BeamHighInset,
                PurlinsPerModule = Frame.PurlinsPerModule,
                PurlinEdgeOffset = Frame.PurlinEdgeOffset,
                BraceOnPillar = Frame.BraceOnPillar,
                BraceOnBeam = Frame.BraceOnBeam,
                Pillar = Secao(Frame.Pillar),
                Beam = Secao(Frame.Beam),
                Purlin = Secao(Frame.Purlin),
                Brace = Secao(Frame.Brace),
                Base = new BaseNoArquivo
                {
                    PlateSize = Frame.Base.PlateSize,
                    PlateThickness = Frame.Base.PlateThickness,
                    PedestalSize = Frame.Base.PedestalSize,
                    PedestalHeight = Frame.Base.PedestalHeight,
                },
            },
        };

        return JsonSerializer.Serialize(arquivo, Opcoes);
    }

    /// <summary>Lê um carport do texto JSON.</summary>
    /// <exception cref="InvalidOperationException">
    /// Se o texto não for um carport, se for de outra versão do formato, se
    /// faltar campo, ou se descrever um carport que não existe.
    /// </exception>
    public static CarportProfile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Arquivo? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize<Arquivo>(json, Opcoes);
        }
        catch (JsonException erro)
        {
            throw new InvalidOperationException(Tr.T("O arquivo não é um carport que este plugin saiba ler."), erro);
        }

        if (arquivo is null) throw new InvalidOperationException(Tr.T("O arquivo de carport está vazio."));
        if (arquivo.FormatVersion is not { } versao)
            throw new InvalidOperationException(Tr.T("O arquivo de carport não diz de que versão do formato é."));
        if (versao != VersaoDoFormato)
            throw new InvalidOperationException(Tr.F("O arquivo de carport é da versão {0} do formato, e este plugin lê a versão {1}.", versao, VersaoDoFormato));

        var a = arquivo.Array ?? throw Faltou("array");
        var f = arquivo.Frame ?? throw Faltou("frame");
        var b = f.Base ?? throw Faltou("frame.base");

        var orientacao = Exigir(a.Orientation, "array.orientation");
        if (!Enum.IsDefined(orientacao))
            throw new InvalidOperationException(Tr.F("O arquivo de carport traz uma posição de módulo que este plugin não conhece: {0}.", orientacao));

        CarportSection Secao(SecaoNoArquivo? s, string campo) =>
            s is null
                ? throw Faltou(campo)
                : new CarportSection(s.Name ?? string.Empty, Exigir(s.Depth, campo + ".depth"), Exigir(s.Width, campo + ".width"));

        var perfil = new CarportProfile(
            arquivo.Name ?? string.Empty,
            new CarportArray(
                new SolarModule(
                    a.Brand ?? string.Empty,
                    a.Model ?? string.Empty,
                    Exigir(a.PowerWatts, "array.powerWatts"),
                    Exigir(a.Height, "array.height"),
                    Exigir(a.Width, "array.width"),
                    Exigir(a.Thickness, "array.thickness")),
                orientacao,
                Exigir(a.ModulesDown, "array.modulesDown"),
                Exigir(a.ModulesAcross, "array.modulesAcross"),
                Exigir(a.GapAcross, "array.gapAcross"),
                Exigir(a.GapDown, "array.gapDown")),
            new CarportFrame(
                Exigir(f.PillarBaseSpacing, "frame.pillarBaseSpacing"),
                Exigir(f.PillarLeanDegrees, "frame.pillarLeanDegrees") * Math.PI / 180,
                Exigir(f.PillarOffset, "frame.pillarOffset"),
                Exigir(f.BeamLowInset, "frame.beamLowInset"),
                Exigir(f.BeamHighInset, "frame.beamHighInset"),
                Exigir(f.PurlinsPerModule, "frame.purlinsPerModule"),
                Exigir(f.PurlinEdgeOffset, "frame.purlinEdgeOffset"),
                Exigir(f.BraceOnPillar, "frame.braceOnPillar"),
                Exigir(f.BraceOnBeam, "frame.braceOnBeam"),
                Secao(f.Pillar, "frame.pillar"),
                Secao(f.Beam, "frame.beam"),
                Secao(f.Purlin, "frame.purlin"),
                Secao(f.Brace, "frame.brace"),
                new CarportBase(
                    Exigir(b.PlateSize, "frame.base.plateSize"),
                    Exigir(b.PlateThickness, "frame.base.plateThickness"),
                    Exigir(b.PedestalSize, "frame.base.pedestalSize"),
                    Exigir(b.PedestalHeight, "frame.base.pedestalHeight"))),
            Exigir(arquivo.TiltDegrees, "tiltDegrees") * Math.PI / 180,
            Exigir(arquivo.ClearHeight, "clearHeight"));

        if (perfil.WhyInvalid is { } motivo)
            throw new InvalidOperationException(Tr.F("O arquivo de carport não descreve um carport: {0}.", motivo));

        return perfil;
    }

    private static InvalidOperationException Faltou(string campo) =>
        new(Tr.F("O arquivo de carport não traz o campo \"{0}\".", campo));

    private static T Exigir<T>(T? valor, string campo) where T : struct => valor ?? throw Faltou(campo);

    // Tudo anulável: "o campo não veio" não pode ser a mesma coisa que "vale zero".
    private sealed class Arquivo
    {
        public int? FormatVersion { get; init; }
        public string? Name { get; init; }
        public double? TiltDegrees { get; init; }
        public double? ClearHeight { get; init; }
        public ArranjoNoArquivo? Array { get; init; }
        public AcoNoArquivo? Frame { get; init; }
    }

    private sealed class ArranjoNoArquivo
    {
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public double? PowerWatts { get; init; }
        public double? Height { get; init; }
        public double? Width { get; init; }
        public double? Thickness { get; init; }
        public CarportOrientation? Orientation { get; init; }
        public int? ModulesDown { get; init; }
        public int? ModulesAcross { get; init; }
        public double? GapAcross { get; init; }
        public double? GapDown { get; init; }
    }

    private sealed class AcoNoArquivo
    {
        public double? PillarBaseSpacing { get; init; }
        public double? PillarLeanDegrees { get; init; }
        public double? PillarOffset { get; init; }
        public double? BeamLowInset { get; init; }
        public double? BeamHighInset { get; init; }
        public int? PurlinsPerModule { get; init; }
        public double? PurlinEdgeOffset { get; init; }
        public double? BraceOnPillar { get; init; }
        public double? BraceOnBeam { get; init; }
        public SecaoNoArquivo? Pillar { get; init; }
        public SecaoNoArquivo? Beam { get; init; }
        public SecaoNoArquivo? Purlin { get; init; }
        public SecaoNoArquivo? Brace { get; init; }
        public BaseNoArquivo? Base { get; init; }
    }

    private sealed class SecaoNoArquivo
    {
        public string? Name { get; init; }
        public double? Depth { get; init; }
        public double? Width { get; init; }
    }

    private sealed class BaseNoArquivo
    {
        public double? PlateSize { get; init; }
        public double? PlateThickness { get; init; }
        public double? PedestalSize { get; init; }
        public double? PedestalHeight { get; init; }
    }
}
