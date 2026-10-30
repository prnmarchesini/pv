using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Um módulo como geometria.
/// </summary>
/// <param name="Column">A coluna dele na mesa, contando da ponta esquerda.</param>
/// <param name="Row">A fileira: 0 é a de baixo, 1 é a de cima em 2V.</param>
/// <param name="TopFace">
/// Os quatro cantos da face superior, no sentido anti-horário visto de cima.
///
/// É esta face, e só ela, que vai para o PVsyst: ele não recebe bloco nem
/// sólido, recebe plano. Por isso ela é uma coisa separada do corpo, e não um
/// lado dele.
/// </param>
/// <param name="Solid">
/// Os oito cantos da caixa: os quatro de cima, na mesma ordem da face, e
/// depois os quatro de baixo.
/// </param>
public sealed record ModulePiece(
    int Column,
    int Row,
    IReadOnlyList<Point3> TopFace,
    IReadOnlyList<Point3> Solid);

/// <summary>
/// Um pilar como geometria, sem comprimento ainda.
///
/// O comprimento não existe aqui de propósito: ele só aparece quando a mesa
/// encontra o terreno, e inventar um valor agora seria adiantar o passo 3.5
/// com um número que pareceria calculado.
/// </summary>
/// <param name="Station">A distância dele ao longo da mesa, do zero da estrutura.</param>
/// <param name="Anchor">
/// Onde ele encosta na mesa. Em coordenadas locais todos os apoios estão no
/// mesmo plano dos módulos — é isso que a regra sagrada 2 exige.
/// </param>
/// <param name="Footprint">Os quatro cantos da seção, no plano do apoio.</param>
public sealed record PillarPiece(
    double Station,
    Point3 Anchor,
    IReadOnlyList<Point3> Footprint)
{
    /// <summary>
    /// A linha do pilar no pórtico: <see cref="PillarLine.Single"/> na
    /// monoposte; na bifila, <see cref="PillarLine.Front"/> ou
    /// <see cref="PillarLine.Rear"/> (passo 37.2).
    /// </summary>
    public PillarLine Line { get; init; } = PillarLine.Single;
}

/// <summary>Em que linha do pórtico o pilar fica.</summary>
public enum PillarLine
{
    /// <summary>O pilar único da monoposte.</summary>
    Single,

    /// <summary>O dianteiro da bifila: o do lado baixo dos módulos.</summary>
    Front,

    /// <summary>O traseiro da bifila: o do lado alto.</summary>
    Rear,
}

/// <summary>
/// A mesa como geometria, em coordenadas locais.
///
/// O sistema local, como manda a arquitetura (origem, plano horizontal,
/// azimute zero):
/// <list type="bullet">
/// <item><description><b>X</b> ao longo do comprimento da mesa, de 0 ao
/// comprimento total;</description></item>
/// <item><description><b>Y</b> ao longo da inclinação, de 0 ao M2 — a ponta
/// baixa do módulo é o zero;</description></item>
/// <item><description><b>Z</b> para cima, com a face superior dos módulos em
/// z = 0 e o corpo descendo a espessura.</description></item>
/// </list>
///
/// Pôr a face em z = 0, e não a base, tem uma razão: o plano dos módulos passa
/// a ser o plano z = 0, e a regra sagrada 2 — "todos os módulos e todos os
/// topos de pilar no mesmo plano" — vira uma conferência de uma linha, aqui e
/// depois de qualquer transformação rígida.
///
/// Aqui não há terreno, inclinação nem azimute: a mesa está deitada, pronta
/// para ser colocada por <see cref="Transformed"/> com uma matriz só.
/// </summary>
public sealed class TableGeometry
{
    private TableGeometry(
        IReadOnlyList<ModulePiece> modulos,
        IReadOnlyList<PillarPiece> pilares,
        double sobraDaTesoura,
        double fileiraDoPilar,
        double comprimento,
        double profundidade,
        double? fileiraDoTraseiro = null,
        double quedaDoTopo = 0,
        TableLayout? mesa = null,
        TableFrame? estrutura = null)
    {
        Layout = mesa;
        Frame = estrutura;
        Modules = modulos;
        Pillars = pilares;
        RafterOffset = sobraDaTesoura;
        PillarRow = fileiraDoPilar;
        Length = comprimento;
        Depth = profundidade;
        RearPillarRow = fileiraDoTraseiro;
        PillarTopDrop = quedaDoTopo;
    }

    /// <summary>Os módulos, coluna a coluna e fileira a fileira.</summary>
    public IReadOnlyList<ModulePiece> Modules { get; }

    /// <summary>
    /// Os pilares, na ordem da tabela. Na bifila são dois por estação, na
    /// ordem estação → frente → trás: o primeiro e o último continuam sendo
    /// os da primeira e da última estação, que é o que quem procura as pontas
    /// da mesa espera.
    /// </summary>
    public IReadOnlyList<PillarPiece> Pillars { get; }

    /// <summary>
    /// Na bifila, onde a fileira dos pilares traseiros fica, medida da ponta
    /// baixa do MÓDULO (sobra + T2t), como <see cref="PillarRow"/> é a dos
    /// dianteiros. Null na monoposte.
    /// </summary>
    public double? RearPillarRow { get; }

    /// <summary>
    /// Quanto o topo dos pilares fica abaixo da face dos módulos, na vertical:
    /// zero na monoposte (o pilar vai até o plano, como sempre foi), o
    /// <see cref="BifilaFrame.PillarTopDrop"/> na bifila. Quem desce o topo é o
    /// <see cref="PillarCalculator"/>; o âncora continua no plano, que é o que
    /// a regra sagrada 2 confere.
    /// </summary>
    public double PillarTopDrop { get; }

    /// <summary>
    /// A mesa (módulos, arranjo, folgas) de que esta geometria saiu, ou null
    /// na geometria montada à mão pelos testes dos verificadores.
    /// </summary>
    public TableLayout? Layout { get; }

    /// <summary>
    /// A estrutura de que esta geometria saiu, ou null como em
    /// <see cref="Layout"/>. É dela que a bifila tira as peças
    /// (<see cref="BifilaStructure"/>) sem o perfil inteiro.
    /// </summary>
    public TableFrame? Frame { get; }

    /// <summary>
    /// Quanto o módulo sobra abaixo da tesoura: <c>(M2 − T1) / 2</c>.
    ///
    /// Vem de a tesoura ser centrada nos módulos, que foi o que o Renan
    /// confirmou com o eixo do segundo desenho. É esta sobra que faz T2 e M1
    /// não partirem do mesmo lugar.
    /// </summary>
    public double RafterOffset { get; }

    /// <summary>
    /// Onde a fileira de pilares fica, medida da ponta baixa do MÓDULO:
    /// <c>sobra + T2</c>. É a distância que entra na conta da altura do pilar.
    /// </summary>
    public double PillarRow { get; }

    /// <summary>
    /// O comprimento da mesa, de ponta a ponta da estrutura.
    ///
    /// Vem guardado, e não medido nos módulos: as sobras das pontas ficam
    /// FORA deles, e quem medisse só os módulos perderia os 20 cm de
    /// estrutura — que é exatamente o que a planta baixa existe para mostrar.
    /// </summary>
    public double Length { get; }

    /// <summary>A medida da mesa na direção da inclinação: o M2.</summary>
    public double Depth { get; }

    /// <summary>
    /// Monta a mesa deitada, em coordenadas locais.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a mesa, a tabela de pilares ou a estrutura não fecharem, ou se a
    /// tesoura não couber nos módulos. Devolver geometria a partir de dado
    /// inválido seria entregar uma mesa que não existe com cara de pronta.
    /// </exception>
    public static TableGeometry Local(TableLayout mesa, PillarTable pilares, TableFrame estrutura)
    {
        ArgumentNullException.ThrowIfNull(mesa);
        ArgumentNullException.ThrowIfNull(pilares);
        ArgumentNullException.ThrowIfNull(estrutura);

        if (mesa.WhyInvalid is { } porCausaDaMesa)
            throw new InvalidOperationException(Tr.F("A mesa não fecha: {0}.", porCausaDaMesa));

        if (estrutura.WhyInvalid is { } porCausaDaEstrutura)
            throw new InvalidOperationException(Tr.F("A estrutura não fecha: {0}.", porCausaDaEstrutura));

        if (pilares.WhyDoesNotFit(mesa.Length) is { } porCausaDosPilares)
            throw new InvalidOperationException(porCausaDosPilares);

        // A exigência do plano ("tesoura menor que o módulo") mora em
        // TableFrame, para o perfil e a geometria nunca divergirem sobre ela.
        if (estrutura.WhyDoesNotFit(mesa) is { } porCausaDaTesoura)
            throw new InvalidOperationException(Tr.F("A estrutura não serve para a mesa: {0}.", porCausaDaTesoura));

        var profundidade = mesa.Depth;

        var sobra = (profundidade - estrutura.RafterLength) / 2;
        var fileiraDoPilar = sobra + estrutura.PillarAlongRafter;
        double? fileiraDoTraseiro = estrutura.Bifila is { } bifila ? sobra + bifila.RearPillarAlongRafter : null;

        return new TableGeometry(
            MontarModulos(mesa),
            MontarPilares(pilares, estrutura, fileiraDoPilar, fileiraDoTraseiro),
            sobra,
            fileiraDoPilar,
            mesa.Length,
            profundidade,
            fileiraDoTraseiro,
            estrutura.Bifila?.PillarTopDrop ?? 0,
            mesa,
            estrutura);
    }

    /// <summary>
    /// A mesma mesa, levada pela transformação.
    ///
    /// Todos os vértices passam pela mesma matriz, e é isso que mantém a mesa
    /// plana. Transformar canto a canto daria a cada vértice o seu próprio
    /// erro de arredondamento, e a mesa entortaria por décimos de milímetro —
    /// invisível no desenho, e exatamente o que a regra sagrada 2 proíbe.
    /// </summary>
    public TableGeometry Transformed(Transform colocacao)
    {
        if (!colocacao.IsFinite)
            throw new ArgumentException("A colocação da mesa tem número inválido.", nameof(colocacao));

        // Rígida, e não só finita. Uma matriz que estica levaria plano em
        // plano — o verificador de regra aprovaria — e entregaria módulos de
        // tamanho errado; uma que espelha viraria todas as faces para baixo, e
        // a usina inteira sairia com produção zero no PVsyst.
        if (!colocacao.IsRigid)
            throw new ArgumentException(
                "A colocação da mesa não é uma transformação rígida: ela estica ou espelha.",
                nameof(colocacao));

        var modulos = Modules
            .Select(m => new ModulePiece(
                m.Column,
                m.Row,
                Levar(m.TopFace, colocacao),
                Levar(m.Solid, colocacao)))
            .ToList();

        var pilares = Pillars
            .Select(p => new PillarPiece(
                p.Station,
                colocacao.Apply(p.Anchor),
                Levar(p.Footprint, colocacao))
            {
                Line = p.Line,
            })
            .ToList();

        return new TableGeometry(modulos, pilares, RafterOffset, PillarRow, Length, Depth, RearPillarRow, PillarTopDrop, Layout, Frame);
    }

    /// <summary>
    /// Monta uma geometria com peças dadas, sem passar pela validação.
    ///
    /// Existe só para os testes dos verificadores de regra sagrada: para
    /// provar que um verificador recusa mesa torta é preciso produzir uma, e
    /// <see cref="Local"/> nunca produz.
    ///
    /// É <c>internal</c> por isso mesmo. Enquanto era público, era o construtor
    /// privado exposto numa DLL de produção: qualquer código do plugin podia
    /// montar uma mesa impossível com cara de pronta, que é exatamente o que o
    /// resto desta classe existe para impedir.
    /// </summary>
    internal static TableGeometry ForTesting(
        IReadOnlyList<ModulePiece> modulos,
        IReadOnlyList<PillarPiece> pilares,
        double sobraDaTesoura,
        double fileiraDoPilar,
        double comprimento = 0,
        double profundidade = 0) =>
        new(modulos, pilares, sobraDaTesoura, fileiraDoPilar, comprimento, profundidade);

    /// <summary>
    /// Os pontos que a regra sagrada 2 manda estar no mesmo plano: as faces
    /// superiores dos módulos e os apoios dos pilares.
    ///
    /// A face, e não o sólido inteiro: o corpo do módulo tem 33 mm de
    /// espessura, e os vértices de baixo nunca estiveram no plano da mesa nem
    /// deviam estar. Misturá-los faria o verificador reprovar toda mesa
    /// perfeita — e um verificador que reprova sempre é desligado, que é a
    /// única forma de perder a regra de vez.
    /// </summary>
    public IEnumerable<Point3> PlanePoints()
    {
        foreach (var modulo in Modules)
        {
            foreach (var ponto in modulo.TopFace) yield return ponto;
        }

        foreach (var pilar in Pillars) yield return pilar.Anchor;
    }

    private static IReadOnlyList<Point3> Levar(IReadOnlyList<Point3> pontos, Transform colocacao)
    {
        var levados = new Point3[pontos.Count];

        for (var i = 0; i < pontos.Count; i++) levados[i] = colocacao.Apply(pontos[i]);

        return levados;
    }

    private static IReadOnlyList<ModulePiece> MontarModulos(TableLayout mesa)
    {
        var modulo = mesa.Module;
        var fileiras = mesa.Arrangement == TableArrangement.DoubleRow ? 2 : 1;
        var pecas = new List<ModulePiece>(mesa.ModuleCount);

        for (var coluna = 0; coluna < mesa.Columns; coluna++)
        {
            // A largura da célula é o módulo mais o espaçamento; a face, só o
            // módulo. Confundir os dois infla a área no PVsyst e a simulação
            // inteira sai otimista.
            var x0 = mesa.LeftMargin + coluna * (modulo.Width + mesa.HorizontalGap);
            var x1 = x0 + modulo.Width;

            for (var fileira = 0; fileira < fileiras; fileira++)
            {
                var y0 = fileira * (modulo.Height + mesa.VerticalGap);
                var y1 = y0 + modulo.Height;

                // Anti-horário visto de cima, para a normal apontar para cima.
                // Invertida, o PVsyst entende a face virada para o chão e a
                // produção vai a zero com o desenho parecendo perfeito.
                Point3[] face =
                [
                    new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0),
                ];

                var fundo = -modulo.Thickness;

                Point3[] caixa =
                [
                    face[0], face[1], face[2], face[3],
                    new(x0, y0, fundo), new(x1, y0, fundo),
                    new(x1, y1, fundo), new(x0, y1, fundo),
                ];

                pecas.Add(new ModulePiece(coluna, fileira, face, caixa));
            }
        }

        return pecas;
    }

    private static IReadOnlyList<PillarPiece> MontarPilares(
        PillarTable pilares, TableFrame estrutura, double fileira, double? fileiraDoTraseiro)
    {
        var meiaLargura = estrutura.PillarWidth / 2;
        var meiaProfundidade = estrutura.PillarDepth / 2;

        PillarPiece Pilar(double estacao, double y, PillarLine linha)
        {
            var x0 = estacao - meiaLargura;
            var x1 = estacao + meiaLargura;
            var y0 = y - meiaProfundidade;
            var y1 = y + meiaProfundidade;

            Point3[] pegada =
            [
                new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0),
            ];

            return new PillarPiece(estacao, new Point3(estacao, y, 0), pegada) { Line = linha };
        }

        // Na bifila, o dianteiro e o traseiro de cada estação, nesta ordem.
        return fileiraDoTraseiro is { } tras
            ? pilares.Positions
                .SelectMany(estacao => new[] { Pilar(estacao, fileira, PillarLine.Front), Pilar(estacao, tras, PillarLine.Rear) })
                .ToList()
            : pilares.Positions.Select(estacao => Pilar(estacao, fileira, PillarLine.Single)).ToList();
    }
}
