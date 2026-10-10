namespace Clivus.Core;

/// <summary>
/// O que o "Apagar" da Edição pode limpar (pedido do Renan em 05/10/2026: "um
/// botão de apagar, aí vai abrir um modal e eu escolho"). Dá para marcar
/// várias de uma vez.
/// </summary>
[Flags]
public enum CleanupOptions
{
    None = 0,

    /// <summary>1: todas as cores que o plugin pôs; as mesas ficam só com a cor original.</summary>
    Colors = 1,

    /// <summary>2: todos os textos do plugin.</summary>
    Texts = 2,

    /// <summary>3: todas as sombras (contornos, etiquetas e a marca nos módulos).</summary>
    Shadows = 4,

    /// <summary>4: todas as strings (traçado, sinais e tags).</summary>
    Strings = 8,

    /// <summary>5: toda a infra elétrica (inversores, trafos, subestações, skids, o que está em campo).</summary>
    Electrical = 16,

    All = Colors | Texts | Shadows | Strings | Electrical,
}

/// <summary>
/// Uma entidade do plugin que o Apagar pode levar, pelo tipo gravado no
/// XData dela (nunca pela camada: entidade sem o nosso XData é do usuário e
/// não é tocada).
/// </summary>
public enum CleanupTarget
{
    /// <summary>Não é nada que o Apagar leve (mesa, módulo, pilar, área, árvore, grupo...).</summary>
    None,

    /// <summary>Anotação da mesa: cota (risco e texto), seta e texto de análise, tag de fileira/mesa/módulo/string.</summary>
    Annotation,

    /// <summary>O texto da tag (numeração) desenhado sobre a string.</summary>
    StringTag,

    /// <summary>O + ou o − na ponta da string, e o círculo em volta.</summary>
    StringSign,

    /// <summary>A polilinha do traçado da string (o vínculo mora nela).</summary>
    StringPath,

    /// <summary>O contorno de uma sombra, no terreno ou sobre a mesa.</summary>
    ShadowOutline,

    /// <summary>A etiqueta (texto) de uma sombra.</summary>
    ShadowLabel,

    /// <summary>O bloco de um equipamento elétrico em campo (subestação, trafo, inversor).</summary>
    Equipment,

    /// <summary>O hatch da área das strings de um trafo (aba Transformador).</summary>
    TransformerArea,
}

/// <summary>A peça de uma mesa que tem cor própria.</summary>
public enum CleanupPiece
{
    Contour,
    Module,
    Pillar,
}

/// <summary>
/// Quantos itens cada opção do Apagar leva, para a janela e a confirmação
/// ("quantos itens de cada tipo serão apagados antes de apagar").
/// </summary>
/// <param name="Recolored">Peças (mesa, string, texto de análise) que voltam à cor original.</param>
/// <param name="Texts">Textos e anotações (com os riscos das cotas e as linhas das setas, que vão junto).</param>
/// <param name="ShadowEntities">Contornos e etiquetas de sombra.</param>
/// <param name="ShadowModules">Módulos marcados pela sombra, que voltam à cor de antes.</param>
/// <param name="Strings">Strings (as polilinhas; os sinais e as tags vão junto).</param>
/// <param name="Inverters">Inversores do cadastro.</param>
/// <param name="Transformers">Trafos do cadastro.</param>
/// <param name="Substations">Subestações: as UCs e os blocos compartilhados.</param>
/// <param name="Skids">Skids (nomes dos agrupamentos).</param>
/// <param name="Placed">Blocos de equipamento e hatches das áreas dos trafos em campo.</param>
/// <param name="FreedStrings">Strings com inversor ou tag que ficam soltas (a geometria fica).</param>
public sealed record CleanupCount(
    int Recolored,
    int Texts,
    int ShadowEntities,
    int ShadowModules,
    int Strings,
    int Inverters,
    int Transformers,
    int Substations,
    int Skids,
    int Placed,
    int FreedStrings)
{
    /// <summary>Quantos itens a opção (uma só) leva; zero é "nada a fazer".</summary>
    public int Of(CleanupOptions option) => option switch
    {
        CleanupOptions.Colors => Recolored,
        CleanupOptions.Texts => Texts,
        CleanupOptions.Shadows => ShadowEntities + ShadowModules,
        CleanupOptions.Strings => Strings,
        CleanupOptions.Electrical => Inverters + Transformers + Substations + Skids + Placed + FreedStrings,
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, "uma opção só"),
    };

    /// <summary>Se nenhuma das opções marcadas tem o que fazer.</summary>
    public bool IsEmpty(CleanupOptions options) => Cleanup.Each.Where(o => options.HasFlag(o)).All(o => Of(o) == 0);

    /// <summary>O que a opção (uma só) leva, curto, para o lado da caixa de marcar.</summary>
    public string Describe(CleanupOptions option) => option switch
    {
        CleanupOptions.Colors => Tr.F("{0} peça(s)", Recolored),
        CleanupOptions.Texts => Tr.F("{0} texto(s)", Texts),
        CleanupOptions.Shadows => Tr.F("{0} contorno(s) e etiqueta(s), {1} módulo(s)", ShadowEntities, ShadowModules),
        CleanupOptions.Strings => Tr.F("{0} string(s)", Strings),
        CleanupOptions.Electrical => Tr.F("{0} inversor(es), {1} trafo(s), {2} subestação(ões), {3} skid(s), {4} em campo; {5} string(s) soltas",
            Inverters, Transformers, Substations, Skids, Placed, FreedStrings),
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, "uma opção só"),
    };

    /// <summary>Uma linha por opção marcada: "Cores: 12 peça(s)".</summary>
    public IReadOnlyList<string> Lines(CleanupOptions options) =>
        Cleanup.Each.Where(o => options.HasFlag(o)).Select(o => Tr.F("{0}: {1}", Cleanup.Name(o), Describe(o))).ToList();
}

/// <summary>
/// As regras do Apagar da Edição, sem AutoCAD (o plugin só lê e escreve):
/// que entidade cada opção leva e qual é a cor original de cada peça de mesa.
///
/// Decisões (05/10/2026, sem perguntar; anotadas no relatório):
/// - só entidade com o nosso XData é tocada; camada nunca decide nada;
/// - a cota e a seta saem inteiras com o texto (o risco e as linhas vão junto);
/// - a marca dos grupos (contorno, hachura e número) não é texto solto: fica;
/// - a tag escrita dentro do bloco do equipamento é parte dele: sai com a infra;
/// - os catálogos ficam (tipos de string com os traçados, modelos de inversor),
///   e também as configurações (numeração, varredura da atribuição).
/// </summary>
public static class Cleanup
{
    /// <summary>O tipo do XData das sombras (contorno e etiqueta), o mesmo que SombrasCommands grava.</summary>
    public const string ShadowType = "Sombra";

    /// <summary>O magenta da mesa que não cabe (o mesmo do desenho da usina).</summary>
    public static readonly RgbColor MarkedColor = new(255, 0, 255);

    /// <summary>O roxo da mesa que não cabe depois de tentadas todas as mesas da lista.</summary>
    public static readonly RgbColor TriedAllColor = new(120, 40, 200);

    /// <summary>As opções, na ordem da janela (1 a 5).</summary>
    public static IReadOnlyList<CleanupOptions> Each { get; } =
        [CleanupOptions.Colors, CleanupOptions.Texts, CleanupOptions.Shadows, CleanupOptions.Strings, CleanupOptions.Electrical];

    /// <summary>O nome curto da opção (uma só).</summary>
    public static string Name(CleanupOptions option) => option switch
    {
        CleanupOptions.Colors => Tr.T("Cores"),
        CleanupOptions.Texts => Tr.T("Textos"),
        CleanupOptions.Shadows => Tr.T("Sombras"),
        CleanupOptions.Strings => Tr.T("Strings"),
        CleanupOptions.Electrical => Tr.T("Infra elétrica"),
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, "uma opção só"),
    };

    /// <summary>
    /// O que a entidade é para o Apagar, pelo tipo do nosso XData
    /// (<paramref name="xdataType"/>, null sem XData) e se ela é texto.
    /// </summary>
    public static CleanupTarget Classify(string? xdataType, bool isText) => xdataType switch
    {
        NoteIdentity.Tipo or AnalysisTextIdentity.Tipo or TagIdentity.Tipo => CleanupTarget.Annotation,
        StringTagText.Tipo or StringPreTag.Tipo => CleanupTarget.StringTag,   // a pré-tag do inversor (10/10/2026) sai como a tag
        StringSign.Tipo => CleanupTarget.StringSign,
        ElectricalString.Tipo => CleanupTarget.StringPath,
        ShadowType => isText ? CleanupTarget.ShadowLabel : CleanupTarget.ShadowOutline,
        EquipmentPlacement.Tipo => CleanupTarget.Equipment,
        TransformerAreaMark.Tipo => CleanupTarget.TransformerArea,
        _ => CleanupTarget.None,
    };

    /// <summary>Quais opções apagam a entidade.</summary>
    public static CleanupOptions ErasedBy(CleanupTarget target) => target switch
    {
        CleanupTarget.Annotation => CleanupOptions.Texts,

        // A tag da string é texto, é da string e é da cadeia elétrica (o
        // trafo e o inversor estão nela): sai com qualquer uma das três.
        CleanupTarget.StringTag => CleanupOptions.Texts | CleanupOptions.Strings | CleanupOptions.Electrical,
        CleanupTarget.StringSign => CleanupOptions.Texts | CleanupOptions.Strings,
        CleanupTarget.StringPath => CleanupOptions.Strings,
        CleanupTarget.ShadowOutline => CleanupOptions.Shadows,
        CleanupTarget.ShadowLabel => CleanupOptions.Shadows | CleanupOptions.Texts,
        CleanupTarget.Equipment => CleanupOptions.Electrical,

        // A área é das strings do trafo: sem o trafo ou sem as strings, não é nada.
        CleanupTarget.TransformerArea => CleanupOptions.Electrical | CleanupOptions.Strings,
        _ => CleanupOptions.None,
    };

    /// <summary>Se alguma das opções marcadas apaga a entidade.</summary>
    public static bool Erases(CleanupTarget target, CleanupOptions options) => (ErasedBy(target) & options) != CleanupOptions.None;

    /// <summary>
    /// A cor original da peça, a que o desenho da usina dá (null: ByLayer).
    /// Mesa que não cabe: inteira magenta (roxo se tentou todas). Senão o
    /// contorno e os módulos com a cor do tipo de mesa (sem tipo, ByLayer) e
    /// o pilar ByLayer.
    /// </summary>
    public static RgbColor? OriginalColor(CleanupPiece piece, bool marked, bool triedAll, RgbColor? typeColor)
    {
        if (marked) return triedAll ? TriedAllColor : MarkedColor;
        return piece == CleanupPiece.Pillar ? null : typeColor;
    }

    /// <summary>Quantos itens cada opção leva, contando as entidades classificadas e o resto.</summary>
    public static CleanupCount Count(
        IEnumerable<CleanupTarget> targets,
        int recolored,
        int shadowModules,
        int inverters,
        int transformers,
        int substations,
        int skids,
        int freedStrings)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var porTipo = targets.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        int N(CleanupTarget t) => porTipo.GetValueOrDefault(t);

        var textos = porTipo.Where(p => p.Key != CleanupTarget.None && ErasedBy(p.Key).HasFlag(CleanupOptions.Texts)).Sum(p => p.Value);

        return new CleanupCount(
            recolored,
            textos,
            N(CleanupTarget.ShadowOutline) + N(CleanupTarget.ShadowLabel),
            shadowModules,
            N(CleanupTarget.StringPath),
            inverters,
            transformers,
            substations,
            skids,
            N(CleanupTarget.Equipment) + N(CleanupTarget.TransformerArea),
            freedStrings);
    }

    /// <summary>
    /// As opções digitadas na linha de comando: os números de 1 a 5, juntos
    /// ou separados ("135", "1 3 5", "1,3,5"), ou "*" para todas. Null se
    /// vazio ou com algo que não é opção.
    /// </summary>
    public static CleanupOptions? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var limpo = text.Trim();
        if (limpo == "*") return CleanupOptions.All;

        var opcoes = CleanupOptions.None;

        foreach (var c in limpo)
        {
            if (c is ' ' or ',' or ';' or '+') continue;
            if (c is < '1' or > '5') return null;

            opcoes |= Each[c - '1'];
        }

        return opcoes == CleanupOptions.None ? null : opcoes;
    }
}
