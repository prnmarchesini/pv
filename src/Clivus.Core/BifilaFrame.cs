using System.Globalization;

namespace Clivus.Core;

/// <summary>Em que linha de pilares vai o X de tirantes da bifila.</summary>
public enum BifilaRodLine
{
    /// <summary>Sem tirantes.</summary>
    None,

    /// <summary>Na linha dos pilares traseiros (os altos), como na Arctech.</summary>
    Rear,

    /// <summary>Na linha dos pilares dianteiros.</summary>
    Front,

    /// <summary>Nas duas linhas.</summary>
    Both,
}

/// <summary>A seção de uma barra: a largura e a altura, em metro.</summary>
public sealed record BarSection(double Width, double Depth);

/// <summary>
/// A parte bifila de uma estrutura fixa (passo 37.1): o pilar traseiro de cada
/// pórtico, as mãos francesas, as terças e os tirantes.
///
/// Mora dentro do <see cref="TableFrame"/> (<see cref="TableFrame.Bifila"/>),
/// e não numa estrutura à parte como o carport, porque a bifila é uma mesa
/// fixa: entra no motor de fileiras como qualquer outra. Na monoposte ela é
/// null, e a mesa sai exatamente como antes.
///
/// Os números de referência são os desenhos da Arctech do projeto Aurora
/// (<see cref="BifilaReference"/> e plano/bifila/01-modelo-de-referencia.md).
/// </summary>
/// <param name="RearPillarAlongRafter">
/// T2t: onde o pilar traseiro encosta na tesoura, contado da ponta baixa dela,
/// como o T2 do dianteiro. Os 3,00 m "entre pilares" do desenho são na
/// horizontal; aqui é na tesoura (ver <see cref="RearAlongRafter"/>).
/// </param>
/// <param name="PillarTopDrop">
/// Quanto o topo dos pilares fica abaixo da face de cima dos módulos, na
/// vertical (módulo, terça e viga): 0,126 na Arctech. Sem isto o pilar iria
/// até o plano dos módulos, como na monoposte, e sairia mais comprido na lista
/// de compra.
/// </param>
/// <param name="FrontBraceLength">
/// O comprimento da mão francesa do pilar dianteiro (peça de catálogo). Zero:
/// sem mão francesa.
/// </param>
/// <param name="FrontBraceBelowTop">Onde ela pega no pilar dianteiro, abaixo do topo dele.</param>
/// <param name="RearBraceLength">O comprimento da mão francesa do pilar traseiro. Zero: sem.</param>
/// <param name="RearBraceBelowTop">Onde ela pega no pilar traseiro, abaixo do topo dele.</param>
/// <param name="PurlinFromModuleEdge">
/// A distância de cada terça à borda da fileira de módulos, na inclinação. São
/// duas terças por fileira de módulos, uma de cada lado (0,492 na Arctech).
/// </param>
/// <param name="PurlinMaxBar">
/// A maior barra de terça: a terça da mesa é cortada em barras de até este
/// comprimento, e cada corte é uma emenda na lista de material.
/// </param>
/// <param name="RodLine">Em que linha de pilares vai o X de tirantes.</param>
/// <param name="RodSpans">
/// Os vãos travados, contados de 1 a partir da ponta onde a mesa começa (o
/// vão entre o pórtico 1 e o 2 é o vão 1). A Arctech trava o 2 e o 4.
/// </param>
/// <param name="RodUpperBelowTop">O ponto de cima do tirante no pilar, abaixo do topo.</param>
/// <param name="RodLowerBelowTop">O ponto de baixo do tirante no pilar, abaixo do topo.</param>
/// <param name="Beam">A seção da viga (a tesoura).</param>
/// <param name="Purlin">A seção da terça.</param>
/// <param name="Brace">A seção da mão francesa.</param>
/// <param name="RodDiameter">O diâmetro do tirante.</param>
public sealed record BifilaFrame(
    double RearPillarAlongRafter,
    double PillarTopDrop,
    double FrontBraceLength,
    double FrontBraceBelowTop,
    double RearBraceLength,
    double RearBraceBelowTop,
    double PurlinFromModuleEdge,
    double PurlinMaxBar,
    BifilaRodLine RodLine,
    IReadOnlyList<int> RodSpans,
    double RodUpperBelowTop,
    double RodLowerBelowTop,
    BarSection Beam,
    BarSection Purlin,
    BarSection Brace,
    double RodDiameter)
{
    /// <summary>Maior medida aceita, em metro: rede para quem digitou em milímetro.</summary>
    private const double MaiorMedida = 20.0;

    /// <summary>O topo do pilar até 1 m abaixo dos módulos; mais que isso não é estrutura de solo.</summary>
    private const double MaiorQueda = 1.0;

    /// <summary>Mão francesa e ponto de tirante até 5 m.</summary>
    private const double MaiorPeca = 5.0;

    /// <summary>Seção de barra até 1 m; tirante até 20 cm de diâmetro.</summary>
    private const double MaiorSecao = 1.0;
    private const double MaiorDiametro = 0.2;

    /// <summary>Menor barra de terça: abaixo disso a terça vira só emenda.</summary>
    private const double MenorBarra = 1.0;

    private readonly int[] _vaos = Ordenar(RodSpans);

    /// <inheritdoc cref="BifilaFrame" path="/param[@name='RodSpans']"/>
    public IReadOnlyList<int> RodSpans
    {
        get => _vaos;

        // Copiada, porque a lista de quem chamou pode mudar depois e o record
        // não; e em ordem, porque "4; 2" e "2; 4" são a mesma estrutura.
        init => _vaos = Ordenar(value);
    }

    private static int[] Ordenar(IReadOnlyList<int>? vaos)
    {
        int[] copia = [.. vaos ?? []];
        Array.Sort(copia);
        return copia;
    }

    /// <summary>
    /// A posição do pilar traseiro na tesoura a partir da distância entre os
    /// pilares na horizontal, que é como o fornecedor cota.
    /// </summary>
    /// <param name="frontAlongRafter">T2 do pilar dianteiro.</param>
    /// <param name="horizontalSpacing">A distância entre os eixos dos dois pilares, em planta.</param>
    /// <param name="tiltRadians">A inclinação da mesa, em radianos.</param>
    /// <exception cref="ArgumentOutOfRangeException">Com a inclinação fora de [0, 90°): em pé, não há distância na tesoura.</exception>
    public static double RearAlongRafter(double frontAlongRafter, double horizontalSpacing, double tiltRadians)
    {
        if (!double.IsFinite(tiltRadians) || tiltRadians < 0 || tiltRadians >= Math.PI / 2)
            throw new ArgumentOutOfRangeException(nameof(tiltRadians), tiltRadians, "Precisa estar entre 0 e 90°, sem incluir os 90°.");

        return frontAlongRafter + horizontalSpacing / Math.Cos(tiltRadians);
    }

    /// <summary>O motivo de a parte bifila não fechar sozinha, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (!double.IsFinite(RearPillarAlongRafter) || RearPillarAlongRafter <= 0 || RearPillarAlongRafter > MaiorMedida)
                return Tr.T("a posição do pilar traseiro na tesoura não é uma distância válida");

            if (!double.IsFinite(PillarTopDrop) || PillarTopDrop < 0 || PillarTopDrop > MaiorQueda)
                return Tr.F("o topo do pilar abaixo dos módulos precisa ficar entre 0 e {0:0} m", MaiorQueda);

            if (PorQueAMaoNaoServe(FrontBraceLength, FrontBraceBelowTop, Tr.T("dianteiro")) is { } frente) return frente;
            if (PorQueAMaoNaoServe(RearBraceLength, RearBraceBelowTop, Tr.T("traseiro")) is { } tras) return tras;

            if (!double.IsFinite(PurlinFromModuleEdge) || PurlinFromModuleEdge <= 0 || PurlinFromModuleEdge > MaiorPeca)
                return Tr.T("a distância da terça à borda dos módulos não é uma medida válida");

            if (!double.IsFinite(PurlinMaxBar) || PurlinMaxBar < MenorBarra || PurlinMaxBar > MaiorMedida)
                return Tr.F("a barra máxima da terça precisa ficar entre {0:0} e {1:0} m", MenorBarra, MaiorMedida);

            if (!Enum.IsDefined(RodLine)) return Tr.T("a linha do tirante não é uma das conhecidas");

            for (var i = 0; i < _vaos.Length; i++)
            {
                if (_vaos[i] < 1) return Tr.F("o vão {0} do tirante não existe: os vãos começam no 1", _vaos[i]);
                if (Array.IndexOf(_vaos, _vaos[i]) != i) return Tr.F("o vão {0} do tirante aparece duas vezes", _vaos[i]);
            }

            // Um jeito só de dizer "sem tirante": linha nenhuma e lista vazia.
            // Linha sem vão é tirante faltando na obra; vão sem linha é um
            // número escrito que ninguém usa.
            if (RodLine != BifilaRodLine.None && _vaos.Length == 0)
                return Tr.T("o tirante tem linha mas nenhum vão: escreva os vãos ou escolha \"sem tirante\"");

            if (RodLine == BifilaRodLine.None && _vaos.Length > 0)
                return Tr.T("há vãos de tirante escritos sem linha de tirante: escolha a linha ou apague os vãos");

            // Os números dos pontos e do diâmetro valem mesmo sem tirante:
            // lixo num campo sem uso continua lixo no arquivo.
            if (!double.IsFinite(RodUpperBelowTop) || RodUpperBelowTop < 0 || RodUpperBelowTop > MaiorPeca
                || !double.IsFinite(RodLowerBelowTop) || RodLowerBelowTop < 0 || RodLowerBelowTop > MaiorPeca
                || (RodLine != BifilaRodLine.None && RodLowerBelowTop <= RodUpperBelowTop))
            {
                return Tr.T("os pontos do tirante no pilar não servem: o de baixo tem que ficar abaixo do de cima, os dois a menos de 5 m do topo");
            }

            if (!double.IsFinite(RodDiameter) || RodDiameter < 0 || RodDiameter > MaiorDiametro
                || (RodLine != BifilaRodLine.None && RodDiameter == 0))
            {
                return Tr.T("o diâmetro do tirante não é uma medida válida");
            }

            if (PorQueASecaoNaoServe(Beam, Tr.T("viga")) is { } viga) return viga;
            if (PorQueASecaoNaoServe(Purlin, Tr.T("terça")) is { } terca) return terca;
            if (PorQueASecaoNaoServe(Brace, Tr.T("mão francesa")) is { } mao) return mao;

            return null;
        }
    }

    /// <summary>
    /// A linha que descreve a parte bifila para o usuário.
    /// </summary>
    public string Describe()
    {
        string Mao(double comprimento) => comprimento > 0 ? Tr.F("{0} m", Texto(comprimento)) : Tr.T("nenhuma");

        var maos = FrontBraceLength > 0 || RearBraceLength > 0
            ? Tr.F("mão francesa dianteira {0} e traseira {1}", Mao(FrontBraceLength), Mao(RearBraceLength))
            : Tr.T("sem mão francesa");

        // Ponto e vírgula entre os vãos: com a vírgula decimal, "2, 4" se lê 2,4.
        var vaos = string.Join("; ", _vaos.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        var tirantes = RodLine switch
        {
            BifilaRodLine.Rear => Tr.F("tirantes na linha traseira, vãos {0}", vaos),
            BifilaRodLine.Front => Tr.F("tirantes na linha dianteira, vãos {0}", vaos),
            BifilaRodLine.Both => Tr.F("tirantes nas duas linhas, vãos {0}", vaos),
            _ => Tr.T("sem tirante"),
        };

        return Tr.F(
            "biposte: pilar traseiro a {0} m na tesoura, topo dos pilares {1} m abaixo dos módulos, {2}, terças a {3} m da borda dos módulos, {4}",
            Texto(RearPillarAlongRafter), Texto(PillarTopDrop), maos, Texto(PurlinFromModuleEdge), tirantes);
    }

    /// <summary>Igualdade campo a campo, com os vãos do tirante comparados pelo valor.</summary>
    public bool Equals(BifilaFrame? outra) =>
        outra is not null
        && RearPillarAlongRafter.Equals(outra.RearPillarAlongRafter)
        && PillarTopDrop.Equals(outra.PillarTopDrop)
        && FrontBraceLength.Equals(outra.FrontBraceLength)
        && FrontBraceBelowTop.Equals(outra.FrontBraceBelowTop)
        && RearBraceLength.Equals(outra.RearBraceLength)
        && RearBraceBelowTop.Equals(outra.RearBraceBelowTop)
        && PurlinFromModuleEdge.Equals(outra.PurlinFromModuleEdge)
        && PurlinMaxBar.Equals(outra.PurlinMaxBar)
        && RodLine == outra.RodLine
        && _vaos.AsSpan().SequenceEqual(outra._vaos)
        && RodUpperBelowTop.Equals(outra.RodUpperBelowTop)
        && RodLowerBelowTop.Equals(outra.RodLowerBelowTop)
        && Equals(Beam, outra.Beam)
        && Equals(Purlin, outra.Purlin)
        && Equals(Brace, outra.Brace)
        && RodDiameter.Equals(outra.RodDiameter);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var codigo = new HashCode();

        codigo.Add(RearPillarAlongRafter);
        codigo.Add(PillarTopDrop);
        codigo.Add(FrontBraceLength);
        codigo.Add(FrontBraceBelowTop);
        codigo.Add(RearBraceLength);
        codigo.Add(RearBraceBelowTop);
        codigo.Add(PurlinFromModuleEdge);
        codigo.Add(PurlinMaxBar);
        codigo.Add(RodLine);
        foreach (var vao in _vaos) codigo.Add(vao);
        codigo.Add(RodUpperBelowTop);
        codigo.Add(RodLowerBelowTop);
        codigo.Add(Beam);
        codigo.Add(Purlin);
        codigo.Add(Brace);
        codigo.Add(RodDiameter);

        return codigo.ToHashCode();
    }

    /// <summary>Zero é "sem mão francesa"; senão a peça precisa existir e pegar abaixo do topo.</summary>
    private static string? PorQueAMaoNaoServe(double comprimento, double abaixoDoTopo, string pilar)
    {
        if (!double.IsFinite(comprimento) || comprimento < 0 || comprimento > MaiorPeca)
            return Tr.F("a mão francesa do pilar {0} precisa ter de 0 (sem) a {1:0} m (o campo é em metro)", pilar, MaiorPeca);

        // Sem mão francesa, o ponto não é usado, mas lixo nele continua lixo
        // no arquivo: tem que ser um número de verdade.
        if (!double.IsFinite(abaixoDoTopo) || abaixoDoTopo < 0 || abaixoDoTopo > MaiorPeca || (comprimento > 0 && abaixoDoTopo == 0))
            return Tr.F("a mão francesa do pilar {0} precisa pegar abaixo do topo dele, a menos de {1:0} m", pilar, MaiorPeca);

        return null;
    }

    private static string? PorQueASecaoNaoServe(BarSection? secao, string peca)
    {
        if (secao is null
            || !double.IsFinite(secao.Width) || secao.Width <= 0 || secao.Width > MaiorSecao
            || !double.IsFinite(secao.Depth) || secao.Depth <= 0 || secao.Depth > MaiorSecao)
        {
            return Tr.F("a seção da {0} não é uma medida válida (até 1 m, em metro)", peca);
        }

        return null;
    }

    private static string Texto(double valor) => valor.ToString("0.###", Tr.Culture);
}

/// <summary>As duas variantes da Arctech 2P14 dos desenhos do projeto Aurora.</summary>
public enum BifilaVariant
{
    /// <summary>GC800MM-NS 10 (rev. 3, 16/05/2026): 0,80 m livres no fundo do módulo, 10 % norte-sul.</summary>
    Gc800,

    /// <summary>GC1000MM-NS 20 (rev. 2, 29/04/2026): 1,00 m livre no fundo do módulo, 20 % norte-sul.</summary>
    Gc1000,
}

/// <summary>
/// Os perfis prontos da bifila, com as medidas dos desenhos da Arctech
/// (plano/bifila/01-modelo-de-referencia.md). As duas variantes são a mesma
/// mesa; mudam as mãos francesas. A altura livre (0,80 ou 1,00 no fundo do
/// módulo) é a faixa da PB do projeto, que fica nas Configurações.
/// </summary>
public static class BifilaReference
{
    private const double Grau = Math.PI / 180;

    /// <summary>A parte bifila da variante.</summary>
    public static BifilaFrame Frame(BifilaVariant variante) => variante switch
    {
        BifilaVariant.Gc800 => Base() with
        {
            FrontBraceLength = 1.244,
            FrontBraceBelowTop = 0.447,
            RearBraceLength = 1.209,
            RearBraceBelowTop = 0.773,
        },
        BifilaVariant.Gc1000 => Base() with
        {
            FrontBraceLength = 1.296,
            FrontBraceBelowTop = 0.548,
            RearBraceLength = 1.233,
            RearBraceBelowTop = 0.826,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(variante), variante, null),
    };

    /// <summary>
    /// A mesa inteira da variante: 2P14 com o módulo JA Solar de 720 W, a 10°,
    /// 6 pórticos, enterro de 2,20 m.
    /// </summary>
    public static TableProfile Profile(BifilaVariant variante)
    {
        var modulo = new SolarModule("JA Solar", "JAM66D46-720/LB", 720, 2.384, 1.303, 0.033);

        return new TableProfile(
            Name(variante),
            new TableLayout(modulo, 28, TableArrangement.DoubleRow, 0.020, 0.020, 0.050, 0.050),
            new TableFrame(4.030, 0.498, 0.050, 0.095, 3.5, 0.851)
            {
                PillarSpans = [3.2, 3.5, 3.5, 3.5, 3.2],
                MinEmbedment = 2.200,
                Bifila = Frame(variante),
            },
            10 * Grau);
    }

    /// <summary>
    /// O biposte genérico, para montar o seu (38.7; Renan, 30/10/2026: "se for
    /// um biposte específico do fabricante, crie uma aba de fabricantes e aí um
    /// genérico, para a pessoa montar o biposte dela"). A mesa e o pórtico de
    /// referência, sem mão francesa e sem tirante, que são do fabricante (quem
    /// tiver os põe), e com o vão-alvo no lugar dos vãos escritos, que só
    /// fecham com o módulo de referência.
    /// </summary>
    public static TableProfile Generic()
    {
        var referencia = Profile(BifilaVariant.Gc800);

        return referencia with
        {
            Name = "Biposte",
            Frame = referencia.Frame with
            {
                PillarSpans = null,
                Bifila = Frame(BifilaVariant.Gc800) with
                {
                    FrontBraceLength = 0,
                    FrontBraceBelowTop = 0,
                    RearBraceLength = 0,
                    RearBraceBelowTop = 0,
                    RodLine = BifilaRodLine.None,
                    RodSpans = [],
                },
            },
        };
    }

    /// <summary>O nome do perfil pronto.</summary>
    public static string Name(BifilaVariant variante) => variante switch
    {
        BifilaVariant.Gc800 => "Biposte 2P14 (0,80 m livre)",
        BifilaVariant.Gc1000 => "Biposte 2P14 (1,00 m livre)",
        _ => throw new ArgumentOutOfRangeException(nameof(variante), variante, null),
    };

    private static BifilaFrame Base() => new(
        RearPillarAlongRafter: 3.544,
        PillarTopDrop: 0.126,
        FrontBraceLength: 0,
        FrontBraceBelowTop: 0,
        RearBraceLength: 0,
        RearBraceBelowTop: 0,
        PurlinFromModuleEdge: 0.492,
        PurlinMaxBar: 7.0,
        RodLine: BifilaRodLine.Rear,
        RodSpans: [2, 4],
        RodUpperBelowTop: 0.20,
        RodLowerBelowTop: 0.60,
        Beam: new BarSection(0.050, 0.080),
        Purlin: new BarSection(0.050, 0.100),
        Brace: new BarSection(0.040, 0.060),
        RodDiameter: 0.016);
}
