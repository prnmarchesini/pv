using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clivus.Core;

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
    /// A versão do formato do perfil que traz os dados elétricos do módulo
    /// (Melhorias de 10/10/2026, item 15: "uma fonte única, na estrutura").
    /// O perfil sem eles continua saindo na versão 2, igual ao de antes, e
    /// abre em qualquer plugin; o que tem sai na 3, que plugin antigo recusa
    /// com o motivo em vez de abrir sem o PAN.
    /// </summary>
    public const int VersaoComEletrica = 3;

    /// <summary>
    /// A versão do formato do perfil bifila (passo 37.1): a estrutura traz o
    /// pilar traseiro, as mãos francesas, as terças e os tirantes em
    /// <c>frame.bifila</c>. Os dados elétricos são opcionais nela. Perfil sem
    /// a bifila continua saindo na 2 ou na 3, igual a antes; plugin antigo
    /// recusa a 4 com o motivo, em vez de abrir uma bifila como monoposte.
    /// </summary>
    public const int VersaoBifila = 4;

    /// <summary>
    /// A versão do formato do biposte com o P1 e o P2 (38.9): a altura passa a
    /// ser da estrutura, em <c>frame.bifila.frontPillar</c> e
    /// <c>rearPillar</c>. O biposte sem eles continua saindo na 4, igual a
    /// antes; plugin antigo recusa a 5 com o motivo, em vez de abrir a mesa
    /// sem a altura dela.
    /// </summary>
    public const int VersaoBifilaComPilares = 5;

    /// <summary>
    /// O enterro nominal e máximo, e os do pilar de trás do biposte (38.12):
    /// o perfil que tem algum deles sai na 6; os outros continuam na versão de
    /// antes, que o plugin anterior lê.
    /// </summary>
    public const int VersaoComEnterro = 6;

    /// <summary>
    /// Os dados elétricos do módulo desta estrutura (lidos do .PAN do PVsyst
    /// ou digitados no cadastro), ou null quando a estrutura ainda não os
    /// tem: aí as tensões e correntes da rota ficam em branco. É a fonte
    /// única do módulo para a parte elétrica (ver <see cref="ModuleSource"/>).
    /// </summary>
    public PanModule? ModuleElectrical { get; init; }

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
            if (string.IsNullOrWhiteSpace(Name)) return Tr.T("o perfil precisa de um nome");

            if (Layout is null) return Tr.T("o perfil não traz a mesa");
            if (Frame is null) return Tr.T("o perfil não traz a estrutura");

            if (Layout.WhyInvalid is { } porCausaDaMesa) return porCausaDaMesa;
            if (Frame.WhyInvalid is { } porCausaDaEstrutura) return porCausaDaEstrutura;

            if (!double.IsFinite(TiltRadians) || TiltRadians < 0 || TiltRadians > PillarSizing.MaiorInclinacao)
                return Tr.T("a inclinação está fora da faixa de uma mesa");

            if (ModuleElectrical?.WhyInvalid() is { } porCausaDaEletrica)
                return Tr.F("os dados elétricos do módulo não servem: {0}", porCausaDaEletrica);

            if (Frame.WhyInvalid is null && Layout.WhyInvalid is null && PorQueABifilaNaoServe() is { } porCausaDaBifila)
                return porCausaDaBifila;

            // A mesma conferência que a geometria faz, e do mesmo lugar: sem
            // ela aqui, um perfil errado só explodiria na hora de desenhar,
            // com o projetista já tendo escolhido a área. Chamar a de lá, em
            // vez de repetir a regra, é o que impede as duas divergirem.
            if (Frame.WhyDoesNotFit(Layout) is { } naoCabe) return naoCabe;

            return PorQueOsPilaresNaoServem();
        }
    }

    /// <summary>Se a estrutura tem o enterro do 38.12 (nominal, máximo ou os de trás): o perfil sai na versão 6.</summary>
    private bool TemEnterroNovo =>
        Frame.NominalEmbedment is not null || Frame.MaxEmbedment is not null
        || Frame.Bifila is { } b && (b.RearMinEmbedment is not null || b.RearNominalEmbedment is not null || b.RearMaxEmbedment is not null);

    private static bool TemEnterroNovoNoArquivo(Arquivo arquivo) =>
        arquivo.Frame is { } f && (f.NominalEmbedment is not null || f.MaxEmbedment is not null
            || f.Bifila is { } b && (b.RearMinEmbedment is not null || b.RearNominalEmbedment is not null || b.RearMaxEmbedment is not null));

    /// <summary>
    /// O P1 e o P2 do biposte (38.9) com a mesa montada: o P1 tem que deixar
    /// os módulos acima do chão, e o P2 tem que alcançar a viga no chão plano.
    /// O P1 é o pilar inteiro, com o enterro, então ele pede o T3.
    /// </summary>
    private string? PorQueOsPilaresNaoServem()
    {
        if (Frame.Bifila is not { FrontPillar: { } p1, RearPillar: { } p2 }) return null;

        // O P1 dá a altura com o enterro nominal (38.12); sem nominal, o mínimo.
        if ((Frame.NominalEmbedment ?? Frame.MinEmbedment) is not { } escrito)
            return Tr.T("o P1 é o pilar inteiro, com o enterro: escreva o enterro nominal (ou o mínimo) da estrutura");

        var t3 = Math.Max(escrito, Frame.MinEmbedment ?? escrito);

        var geometria = TableGeometry.Local(Layout, Frame.Pillars(Layout), Frame);
        var pontaBaixa = StructureHeight.LowEdge(geometria, TiltRadians)!.Value;

        // O fundo do módulo, e não a face de cima, tem que ficar acima do chão
        // (revisão do 38.9: o P1 de 2,24 com 2,20 de enterro passava com o
        // fundo 1,9 cm dentro do chão).
        if (!(pontaBaixa - Layout.Module.Thickness * Math.Cos(TiltRadians) > 0))
        {
            return Tr.F(
                "o P1 de {0} m, com {1} m de enterro, deixa a ponta baixa dos módulos a {2} m do chão: o módulo encosta no chão",
                Texto(p1), Texto(t3), Texto(pontaBaixa));
        }

        var plano = StructureHeight.FlatRearPillar(geometria, TiltRadians)!.Value;

        if (p2 < plano - 0.001)
        {
            return Tr.F(
                "o P2 de {0} m não alcança a viga: no chão plano o pilar traseiro precisa de {1} m (o P1 mais a subida da viga entre os dois pilares)",
                Texto(p2), Texto(plano));
        }

        return null;
    }

    /// <summary>
    /// A ponta baixa que o P1 da estrutura dá no chão plano (38.9), ou null
    /// quando ela não tem P1 ou o perfil não serve.
    /// </summary>
    public double? StructureLowEdge =>
        Frame?.Bifila?.FrontPillar is null || WhyInvalid is not null
            ? null
            : StructureHeight.LowEdge(TableGeometry.Local(Layout, Frame.Pillars(Layout), Frame), TiltRadians);

    private static string Texto(double valor) => valor.ToString("0.###", Tr.Culture);

    /// <summary>
    /// A inclinação em graus, como ela vai para o arquivo e para a tela.
    /// </summary>
    public double TiltDegrees => TiltRadians * 180 / Math.PI;

    /// <summary>
    /// O que só se confere na bifila com a inclinação na mão: os dois pilares
    /// separados em planta, e o topo deles entre o fundo do módulo e o fundo
    /// da viga (passo 37.1, revisão).
    /// </summary>
    private string? PorQueABifilaNaoServe()
    {
        if (Frame.Bifila is not { } b) return null;

        var cos = Math.Cos(TiltRadians);

        // Em planta: as seções dos dois pilares no chão, eixo a eixo, mais
        // longe que a largura do pilar na inclinação.
        var emPlanta = (b.RearPillarAlongRafter - Frame.PillarAlongRafter) * cos;
        if (!(emPlanta > Frame.PillarDepth))
        {
            return Tr.F(
                "o pilar traseiro fica a {0} m do dianteiro em planta, e o pilar tem {1} m na inclinação: os dois se encostam",
                emPlanta.ToString("0.###", Tr.Culture), Frame.PillarDepth.ToString("0.###", Tr.Culture));
        }

        // Na vertical, no eixo do pilar: o módulo, a terça e a viga estão
        // empilhados na perpendicular ao plano. O topo do pilar abaixo do
        // fundo do módulo, e acima do fundo da viga (senão ela fica sem apoio).
        var modulo = Layout.Module.Thickness / cos;
        var pilha = (Layout.Module.Thickness + b.Purlin.Depth + b.Beam.Depth) / cos;

        if (b.PillarTopDrop < modulo)
        {
            return Tr.F(
                "o topo do pilar a {0} m abaixo dos módulos fura o módulo, que tem {1} m na vertical",
                b.PillarTopDrop.ToString("0.###", Tr.Culture), modulo.ToString("0.###", Tr.Culture));
        }

        if (b.PillarTopDrop > pilha)
        {
            return Tr.F(
                "o topo do pilar a {0} m abaixo dos módulos fica abaixo da viga (módulo, terça e viga somam {1} m na vertical): a viga ficaria sem apoio",
                b.PillarTopDrop.ToString("0.###", Tr.Culture), pilha.ToString("0.###", Tr.Culture));
        }

        return null;
    }

    /// <summary>Se a estrutura é bifila (dois pilares por pórtico).</summary>
    public bool IsBifila => Frame?.Bifila is not null;

    /// <summary>
    /// Na bifila, a distância entre os eixos dos dois pilares em planta, que é
    /// como o fornecedor cota (3,00 m na Arctech); null na monoposte.
    /// </summary>
    public double? BifilaHorizontalSpacing =>
        Frame?.Bifila is { } b ? (b.RearPillarAlongRafter - Frame.PillarAlongRafter) * Math.Cos(TiltRadians) : null;

    /// <summary>
    /// Os pilares na altura básica (38.7; Renan, 30/10/2026: "cadê a altura
    /// básica das duas pernas da mesa? ... P1 que é o pilar menor e P2 que é o
    /// pilar maior"): no chão plano e com o enterro T3 da mesa (sem T3, o
    /// mínimo do projeto). Com o P1 da estrutura (38.9), a ponta baixa é a que
    /// ele dá, exata: o P1 sai o que o projetista escreveu. Sem P1, a ponta
    /// baixa é onde o motor assenta a mesa no plano (a mínima do projeto na
    /// grade de cotas, <see cref="ViableElevations.FlatGroundLowEdge"/>). No
    /// terreno, cada pilar sai com o seu.
    /// </summary>
    /// <param name="projeto">A configuração do projeto: a ponta baixa mínima e o enterro.</param>
    /// <exception cref="InvalidOperationException">Com o perfil inválido.</exception>
    public BasicPillars PillarsAtBasicHeight(SystemConfiguration projeto)
    {
        ArgumentNullException.ThrowIfNull(projeto);

        // O enterro nominal de cada linha (38.12): é o do desenho no chão plano.
        var comAMesa = projeto.WithEmbedmentOf(Frame);
        var frente = Frame.EmbedmentFor(PillarLine.Front, comAMesa.MinEmbedment, comAMesa.MaxEmbedment).Nominal;
        var tras = Frame.EmbedmentFor(PillarLine.Rear, comAMesa.MinEmbedment, comAMesa.MaxEmbedment).Nominal;

        if (StructureLowEdge is { } daEstrutura) return PillarsOnFlatGround(daEstrutura, frente, tras);

        return PillarsOnFlatGround(ViableElevations.FlatGroundLowEdge(projeto), frente, tras);
    }

    /// <summary>
    /// Os pilares no chão plano com esta ponta baixa e este enterro, sem a
    /// grade do motor: é a conta do catálogo do fabricante, que cota a altura
    /// livre exata (<see cref="PillarsAtBasicHeight"/> usa a da grade).
    /// </summary>
    /// <param name="lowEdge">A ponta baixa: a face de cima do módulo na ponta baixa, acima do chão.</param>
    /// <param name="embedment">O enterro do P1.</param>
    /// <param name="rearEmbedment">O enterro do P2; null, o do P1.</param>
    /// <exception cref="InvalidOperationException">Com o perfil inválido.</exception>
    public BasicPillars PillarsOnFlatGround(double lowEdge, double embedment, double? rearEmbedment = null)
    {
        if (WhyInvalid is { } motivo) throw new InvalidOperationException(motivo);

        var geometria = TableGeometry.Local(Layout, Frame.Pillars(Layout), Frame);

        // A mesma conta do pilar do motor: a subida até o pilar, menos o que
        // o topo dele fica abaixo dos módulos (zero na monoposte).
        double Livre(double ateOPilar) =>
            PillarSizing.FreeHeight(lowEdge, ateOPilar, TiltRadians) - geometria.PillarTopDrop;

        return new BasicPillars(
            lowEdge,
            embedment,
            Livre(geometria.PillarRow),
            geometria.RearPillarRow is { } tras ? Livre(tras) : null,
            geometria.RearPillarRow is null ? null : rearEmbedment);
    }

    /// <summary>
    /// O perfil com os dados elétricos do .PAN e a potência do módulo passada
    /// para a do PAN (item 15). Nada mais muda: medidas, quantidade, arranjo e
    /// estrutura ficam como estavam.
    /// </summary>
    public TableProfile WithPan(PanModule pan)
    {
        ArgumentNullException.ThrowIfNull(pan);
        return WithModulePower(pan.Pmax) with { ModuleElectrical = pan };
    }

    /// <summary>O perfil com outra potência de módulo, e só isso.</summary>
    public TableProfile WithModulePower(double watts) =>
        this with { Layout = Layout with { Module = Layout.Module with { PowerWatts = watts } } };

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
            throw new InvalidOperationException(Tr.F("O perfil não pode ser salvo: {0}.", motivo));

        var arquivo = new Arquivo
        {
            FormatVersion = TemEnterroNovo ? VersaoComEnterro
                : Frame.Bifila is { } bifila ? (bifila.FrontPillar is null ? VersaoBifila : VersaoBifilaComPilares)
                : ModuleElectrical is null ? VersaoDoFormato : VersaoComEletrica,
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
                PillarSpans = Frame.PillarSpans?.ToArray(),
                MinEmbedment = Frame.MinEmbedment,
                NominalEmbedment = Frame.NominalEmbedment,
                MaxEmbedment = Frame.MaxEmbedment,
                Bifila = Frame.Bifila is { } b
                    ? new BifilaNoArquivo
                    {
                        RearPillarAlongRafter = b.RearPillarAlongRafter,
                        PillarTopDrop = b.PillarTopDrop,
                        FrontBraceLength = b.FrontBraceLength,
                        FrontBraceBelowTop = b.FrontBraceBelowTop,
                        RearBraceLength = b.RearBraceLength,
                        RearBraceBelowTop = b.RearBraceBelowTop,
                        PurlinFromModuleEdge = b.PurlinFromModuleEdge,
                        PurlinMaxBar = b.PurlinMaxBar,
                        RodLine = b.RodLine.ToString(),
                        RodSpans = [.. b.RodSpans],
                        RodUpperBelowTop = b.RodUpperBelowTop,
                        RodLowerBelowTop = b.RodLowerBelowTop,
                        Beam = SecaoNoArquivo.De(b.Beam),
                        Purlin = SecaoNoArquivo.De(b.Purlin),
                        Brace = SecaoNoArquivo.De(b.Brace),
                        RodDiameter = b.RodDiameter,
                        FrontPillar = b.FrontPillar,
                        RearPillar = b.RearPillar,
                        RearMinEmbedment = b.RearMinEmbedment,
                        RearNominalEmbedment = b.RearNominalEmbedment,
                        RearMaxEmbedment = b.RearMaxEmbedment,
                    }
                    : null,
            },
            ModuleElectrical = ModuleElectrical is { } e
                ? new EletricaNoArquivo
                {
                    Manufacturer = e.Manufacturer,
                    Model = e.Model,
                    Pmax = e.Pmax,
                    Voc = e.Voc,
                    Isc = e.Isc,
                    Vmp = e.Vmp,
                    Imp = e.Imp,
                    VocCoefficient = e.VocCoefficient,
                    IscCoefficient = e.IscCoefficient,
                    Noct = e.Noct,
                    PowerCoefficient = e.PowerCoefficient,
                }
                : null,
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
                Tr.T("O arquivo não é um perfil de mesa que este plugin saiba ler."), erro);
        }

        if (arquivo is null) throw new InvalidOperationException(Tr.T("O perfil de mesa está vazio."));

        if (arquivo.FormatVersion is not { } versao)
            throw new InvalidOperationException(Tr.T("O perfil de mesa não diz de que versão do formato é."));

        if (versao != VersaoDoFormato && versao != VersaoComEletrica && versao != VersaoBifila && versao != VersaoBifilaComPilares && versao != VersaoComEnterro)
        {
            throw new InvalidOperationException(
                Tr.F("O perfil de mesa é da versão {0} do formato, e este plugin lê a versão {1}.", versao, VersaoComEnterro));
        }

        // A 6 (38.12) é a que traz o enterro novo; sem ele, o arquivo foi mexido à mão.
        if (versao == VersaoComEnterro && !TemEnterroNovoNoArquivo(arquivo))
            throw new InvalidOperationException(Tr.T("O perfil de mesa é da versão 6 do formato e não traz o enterro nominal, o máximo nem os do pilar de trás."));

        if (versao != VersaoComEnterro && TemEnterroNovoNoArquivo(arquivo))
            throw new InvalidOperationException(Tr.F("O perfil de mesa é da versão {0} do formato e traz o enterro nominal ou máximo, que só existem na versão 6.", versao));

        // Os dados elétricos entraram na versão 3: um perfil que diz ser da 2
        // e os traz foi mexido à mão, e não é lido pela metade.
        if (versao == VersaoDoFormato && arquivo.ModuleElectrical is not null)
            throw new InvalidOperationException(Tr.T("O perfil de mesa é da versão 2 do formato e traz os dados elétricos do módulo, que só existem na versão 3."));

        if (versao == VersaoComEletrica && arquivo.ModuleElectrical is null)
            throw new InvalidOperationException(Tr.F("O perfil de mesa não traz o campo \"{0}\".", "moduleElectrical"));

        // A bifila entrou na versão 4, e só ela sai na 4 (ou na 5, com o P1 e
        // o P2): os dois lados precisam bater, senão o arquivo foi mexido à mão.
        var ehBifila = versao is VersaoBifila or VersaoBifilaComPilares || (versao == VersaoComEnterro && arquivo.Frame?.Bifila is not null);

        if (ehBifila && arquivo.Frame?.Bifila is null)
            throw new InvalidOperationException(Tr.F("O perfil de mesa é da versão {0} do formato e não traz a estrutura do biposte (frame.bifila).", versao));

        if (!ehBifila && arquivo.Frame?.Bifila is not null)
            throw new InvalidOperationException(Tr.F("O perfil de mesa é da versão {0} do formato e traz a estrutura do biposte, que só existe na versão 4.", versao));

        if (versao == VersaoBifila && (arquivo.Frame?.Bifila?.FrontPillar is not null || arquivo.Frame?.Bifila?.RearPillar is not null))
            throw new InvalidOperationException(Tr.T("O perfil de mesa é da versão 4 do formato e traz o P1 e o P2 do biposte, que só existem na versão 5."));

        if (arquivo.Layout is null) throw new InvalidOperationException(Tr.T("O perfil de mesa não traz a mesa."));
        if (arquivo.Frame is null) throw new InvalidOperationException(Tr.T("O perfil de mesa não traz a estrutura."));

        var arranjo = Exigir(arquivo.Layout.Arrangement, "layout.arrangement");

        if (!Enum.IsDefined(arranjo))
        {
            throw new InvalidOperationException(
                Tr.F("O perfil de mesa traz um arranjo que este plugin não conhece: {0}.", arranjo));
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
                Exigir(arquivo.Frame.PillarCantilever, "frame.pillarCantilever"))
            {
                // Opcionais desde o 8.1: perfil gravado antes deles abre com
                // o vão-alvo e o enterro da configuração, como sempre abriu.
                PillarSpans = arquivo.Frame.PillarSpans,
                MinEmbedment = arquivo.Frame.MinEmbedment,

                // Opcionais desde o 38.12: o perfil de antes abre com o nominal
                // igual ao mínimo e o máximo do projeto, como sempre abriu.
                NominalEmbedment = arquivo.Frame.NominalEmbedment,
                MaxEmbedment = arquivo.Frame.MaxEmbedment,
                Bifila = arquivo.Frame.Bifila is { } b
                    ? LerBifila(b, comPilares: versao == VersaoBifilaComPilares || (versao == VersaoComEnterro && (b.FrontPillar is not null || b.RearPillar is not null)))
                    : null,
            },
            Exigir(arquivo.TiltDegrees, "tiltDegrees") * Math.PI / 180)
        {
            ModuleElectrical = arquivo.ModuleElectrical is { } e
                ? new PanModule(
                    e.Manufacturer ?? string.Empty,
                    e.Model ?? string.Empty,
                    Exigir(e.Pmax, "moduleElectrical.pmax"),
                    Exigir(e.Voc, "moduleElectrical.voc"),
                    Exigir(e.Isc, "moduleElectrical.isc"),
                    Exigir(e.Vmp, "moduleElectrical.vmp"),
                    Exigir(e.Imp, "moduleElectrical.imp"),
                    Exigir(e.VocCoefficient, "moduleElectrical.vocCoefficient"),
                    Exigir(e.IscCoefficient, "moduleElectrical.iscCoefficient"),
                    e.Noct,
                    e.PowerCoefficient)
                : null,
        };

        if (perfil.WhyInvalid is { } motivo)
            throw new InvalidOperationException(Tr.F("O perfil de mesa não descreve uma mesa: {0}.", motivo));

        return perfil;
    }

    /// <summary>
    /// A parte bifila do arquivo, com cada campo exigido pelo nome: na bifila
    /// nada é opcional, e zero é valor legítimo (mão francesa zero é "sem").
    /// O P1 e o P2 só existem na versão 5, e nela são exigidos.
    /// </summary>
    private static BifilaFrame LerBifila(BifilaNoArquivo b, bool comPilares)
    {
        // Pelo nome exato, e não pelo conversor do .NET, que aceita "1" entre
        // aspas e "Rear, Front" como se fossem nomes.
        var texto = b.RodLine ?? throw new InvalidOperationException(Tr.F("O perfil de mesa não traz o campo \"{0}\".", "frame.bifila.rodLine"));

        if (!Enum.GetNames<BifilaRodLine>().Contains(texto, StringComparer.Ordinal))
            throw new InvalidOperationException(Tr.F("O perfil de mesa traz uma linha de tirante que este plugin não conhece: {0}.", texto));

        var linha = Enum.Parse<BifilaRodLine>(texto);

        return new BifilaFrame(
            Exigir(b.RearPillarAlongRafter, "frame.bifila.rearPillarAlongRafter"),
            Exigir(b.PillarTopDrop, "frame.bifila.pillarTopDrop"),
            Exigir(b.FrontBraceLength, "frame.bifila.frontBraceLength"),
            Exigir(b.FrontBraceBelowTop, "frame.bifila.frontBraceBelowTop"),
            Exigir(b.RearBraceLength, "frame.bifila.rearBraceLength"),
            Exigir(b.RearBraceBelowTop, "frame.bifila.rearBraceBelowTop"),
            Exigir(b.PurlinFromModuleEdge, "frame.bifila.purlinFromModuleEdge"),
            Exigir(b.PurlinMaxBar, "frame.bifila.purlinMaxBar"),
            linha,
            b.RodSpans ?? throw new InvalidOperationException(Tr.F("O perfil de mesa não traz o campo \"{0}\".", "frame.bifila.rodSpans")),
            Exigir(b.RodUpperBelowTop, "frame.bifila.rodUpperBelowTop"),
            Exigir(b.RodLowerBelowTop, "frame.bifila.rodLowerBelowTop"),
            LerSecao(b.Beam, "frame.bifila.beam"),
            LerSecao(b.Purlin, "frame.bifila.purlin"),
            LerSecao(b.Brace, "frame.bifila.brace"),
            Exigir(b.RodDiameter, "frame.bifila.rodDiameter"))
        {
            FrontPillar = comPilares ? Exigir(b.FrontPillar, "frame.bifila.frontPillar") : null,
            RearPillar = comPilares ? Exigir(b.RearPillar, "frame.bifila.rearPillar") : null,
            RearMinEmbedment = b.RearMinEmbedment,
            RearNominalEmbedment = b.RearNominalEmbedment,
            RearMaxEmbedment = b.RearMaxEmbedment,
        };
    }

    private static BarSection LerSecao(SecaoNoArquivo? secao, string campo)
    {
        if (secao is null) throw new InvalidOperationException(Tr.F("O perfil de mesa não traz o campo \"{0}\".", campo));

        return new BarSection(Exigir(secao.Width, campo + ".width"), Exigir(secao.Depth, campo + ".depth"));
    }

    /// <summary>A linha que descreve o perfil para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return Tr.F("Perfil inválido: {0}.", motivo);

        return Tr.F("{0} — {1}, a {2:0.###}°", Name.Trim(), Layout.Describe(), TiltDegrees);
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
            Tr.F("O perfil de mesa não traz o campo \"{0}\".", campo));

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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EletricaNoArquivo? ModuleElectrical { get; init; }
    }

    /// <summary>Os dados elétricos do módulo como estão no arquivo (versão 3). Unidades do <see cref="PanModule"/>.</summary>
    private sealed class EletricaNoArquivo
    {
        public string? Manufacturer { get; init; }
        public string? Model { get; init; }
        public double? Pmax { get; init; }
        public double? Voc { get; init; }
        public double? Isc { get; init; }
        public double? Vmp { get; init; }
        public double? Imp { get; init; }
        public double? VocCoefficient { get; init; }
        public double? IscCoefficient { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Noct { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PowerCoefficient { get; init; }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? PillarSpans { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MinEmbedment { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? NominalEmbedment { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MaxEmbedment { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BifilaNoArquivo? Bifila { get; init; }
    }

    /// <summary>A parte bifila como está no arquivo (versão 4). Tudo anulável, pelo mesmo motivo do resto.</summary>
    private sealed class BifilaNoArquivo
    {
        public double? RearPillarAlongRafter { get; init; }
        public double? PillarTopDrop { get; init; }
        public double? FrontBraceLength { get; init; }
        public double? FrontBraceBelowTop { get; init; }
        public double? RearBraceLength { get; init; }
        public double? RearBraceBelowTop { get; init; }
        public double? PurlinFromModuleEdge { get; init; }
        public double? PurlinMaxBar { get; init; }
        public string? RodLine { get; init; }
        public int[]? RodSpans { get; init; }
        public double? RodUpperBelowTop { get; init; }
        public double? RodLowerBelowTop { get; init; }
        public SecaoNoArquivo? Beam { get; init; }
        public SecaoNoArquivo? Purlin { get; init; }
        public SecaoNoArquivo? Brace { get; init; }
        public double? RodDiameter { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FrontPillar { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RearPillar { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RearMinEmbedment { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RearNominalEmbedment { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RearMaxEmbedment { get; init; }
    }

    private sealed class SecaoNoArquivo
    {
        public double? Width { get; init; }
        public double? Depth { get; init; }

        public static SecaoNoArquivo De(BarSection secao) => new() { Width = secao.Width, Depth = secao.Depth };
    }
}
