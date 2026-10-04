using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// A estrutura que segura os módulos: a tesoura e o pilar.
///
/// Os nomes seguem o desenho cotado do Renan (23/09/2026):
/// <list type="bullet">
/// <item><description><b>T1</b> — o comprimento da tesoura;</description></item>
/// <item><description><b>T2</b> — onde o pilar encosta nela, medido da ponta
/// baixa da própria tesoura.</description></item>
/// </list>
///
/// A tesoura é mais curta que os módulos e fica <b>centrada</b> neles — foi o
/// que ele confirmou no segundo desenho, com o eixo. É dessa diferença que sai
/// a sobra que entra na conta da altura do pilar, e é por isso que T2 sozinho
/// não basta: ele não parte do mesmo lugar que o M1.
/// </summary>
/// <param name="RafterLength">T1: o comprimento da tesoura, em metro.</param>
/// <param name="PillarAlongRafter">
/// T2: a posição do pilar ao longo da tesoura, contada da ponta baixa dela.
/// </param>
/// <param name="PillarWidth">Uma largura da seção do pilar, ao longo da fileira.</param>
/// <param name="PillarDepth">
/// A outra largura da seção, na direção da inclinação. Na tela ela se chama
/// largura desde 01/10/2026 (Melhorias.docx): "profundidade" ficou reservada
/// ao enterro, o <see cref="MinEmbedment"/>.
/// </param>
/// <param name="PillarSpanTarget">
/// O vão pretendido entre pilares, em metro. É alvo, não regra: o vão de
/// verdade sai do comprimento dividido, e quase sempre dá quebrado.
/// </param>
/// <param name="PillarCantilever">
/// O balanço: quanto de estrutura sobra para fora do primeiro e do último
/// pilar.
///
/// Zero é valor legítimo — o pilar cravado na ponta —, e foi como o plugin
/// nasceu. Mas é escolha de projeto, não constante: o Renan confirmou em
/// 23/09/2026 que a estrutura dele aceita os dois.
/// </param>
public sealed record TableFrame(
    double RafterLength,
    double PillarAlongRafter,
    double PillarWidth,
    double PillarDepth,
    double PillarSpanTarget,
    double PillarCantilever)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Maior medida aceita, em metro. Rede para erro de escala, como nas
    /// outras classes: quem digitou 300 achando que o campo era em centímetro.
    /// </summary>
    private const double MaiorMedida = 20.0;

    /// <summary>Faixa aceita do T3, em metro.</summary>
    private const double MenorEnterro = 0.001;
    private const double MaiorEnterro = 5.0;

    private readonly double[]? _vaos;

    /// <summary>
    /// Os vãos escritos pelo usuário, par a par (P1-P2, P2-P3...), ou null
    /// para a tabela sair do <see cref="PillarSpanTarget"/>.
    ///
    /// Passo 8.1 (Melhorias.docx, 01/10/2026): o Renan quer digitar cada vão
    /// e ver se a soma bate com o que os pilares precisam cobrir. Com a lista,
    /// o vão-alvo deixa de valer; a soma tem que fechar com
    /// <see cref="PillarCoverage"/>, senão a estrutura não serve para a mesa.
    /// </summary>
    public IReadOnlyList<double>? PillarSpans
    {
        get => _vaos;

        // Copiado, como na PillarTable: a lista de quem chamou pode mudar
        // depois, e o record não.
        init => _vaos = value is null ? null : [.. value];
    }

    /// <summary>
    /// T3: o mínimo que o pilar fica enterrado, em metro, ou null quando a
    /// estrutura não diz (vale o da configuração do projeto).
    /// </summary>
    public double? MinEmbedment { get; init; }

    /// <summary>
    /// O comprimento que os pilares precisam cobrir, do primeiro ao último: a
    /// mesa inteira (módulos, espaçamentos e as duas sobras) menos o balanço
    /// das duas pontas.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a mesa não fecha.</exception>
    public double PillarCoverage(TableLayout mesa)
    {
        ArgumentNullException.ThrowIfNull(mesa);
        return mesa.Length - 2 * PillarCantilever;
    }

    /// <summary>
    /// A soma dos vãos escritos menos o que os pilares precisam cobrir:
    /// positivo sobra, negativo falta. Null sem vãos escritos. Até 1 mm é
    /// zero, a mesma tolerância com que a tabela de pilares fecha.
    /// </summary>
    public double? SpanDifference(TableLayout mesa)
    {
        ArgumentNullException.ThrowIfNull(mesa);
        if (_vaos is null) return null;

        var diferenca = new PillarTable(_vaos, 0).TotalSpan - PillarCoverage(mesa);
        return Math.Abs(diferenca) <= ToleranciaDoFechamento ? 0 : diferenca;
    }

    /// <summary>Um milímetro, como em <see cref="PillarTable"/>.</summary>
    private const double ToleranciaDoFechamento = 0.001;

    /// <summary>
    /// A tabela de pilares desta estrutura nesta mesa: os vãos escritos, ou,
    /// sem eles, a distribuição pelo vão-alvo.
    /// </summary>
    public PillarTable Pillars(TableLayout mesa)
    {
        ArgumentNullException.ThrowIfNull(mesa);

        return _vaos is null
            ? PillarTable.Distribute(mesa.Length, PillarSpanTarget, PillarCantilever)
            : new PillarTable(_vaos, PillarCantilever);
    }

    /// <summary>Se a estrutura fecha. Quando não fecha, <see cref="WhyInvalid"/> diz por quê.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de a estrutura não fechar, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (!Medida(RafterLength)) return "o comprimento da tesoura não é uma medida válida";
            if (!Medida(PillarWidth)) return "a largura do pilar não é uma medida válida";
            if (!Medida(PillarDepth)) return "a largura do pilar na inclinação (a antiga profundidade do pilar) não é uma medida válida";

            if (!double.IsFinite(PillarAlongRafter) || PillarAlongRafter < 0)
                return "a posição do pilar na tesoura não é uma distância válida";

            if (!Medida(PillarSpanTarget)) return "o vão pretendido entre pilares não é uma medida válida";

            if (!double.IsFinite(PillarCantilever) || PillarCantilever < 0 || PillarCantilever > MaiorMedida)
                return "o balanço das pontas não é uma medida válida";

            // De 1 mm (o piso do SystemConfiguration, senão ForTable montaria
            // uma configuração inválida) a 5 m: acima disso é quase certo
            // alguém digitando em centímetro num campo em metro.
            if (MinEmbedment is { } t3 && (!double.IsFinite(t3) || t3 < MenorEnterro || t3 > MaiorEnterro))
                return $"o enterro mínimo do pilar (T3) não é uma medida válida: precisa ficar entre 0,001 e {MaiorEnterro:0} m (o campo é em metro)";

            if (_vaos is not null)
            {
                if (new PillarTable(_vaos, 0).WhyInvalid is { } porCausaDosVaos) return porCausaDosVaos;

                // O mesmo teto do vão-alvo: vão escrito de 30 m não pode
                // passar onde o alvo de 30 m é recusado.
                for (var i = 0; i < _vaos.Length; i++)
                {
                    if (_vaos[i] > MaiorMedida)
                        return $"o vão {i + 1} tem {Texto(_vaos[i])} m, mais que os {MaiorMedida:0} m possíveis";
                }
            }

            if (PillarAlongRafter > RafterLength)
            {
                // Pilar fora da tesoura é peça pendurada no ar. Passaria em
                // qualquer conferência de sinal.
                return $"o pilar está a {Texto(PillarAlongRafter)} m de uma tesoura de "
                    + $"{Texto(RafterLength)} m, ou seja, fora dela";
            }

            return null;
        }
    }

    /// <summary>
    /// Por que esta estrutura não serve para esta mesa, ou null se serve.
    ///
    /// Hoje há uma conferência só, a que o passo 3.4 exige: a tesoura precisa
    /// ser menor que os módulos. Maior daria sobra negativa e poria o pilar
    /// fora da mesa.
    ///
    /// Mora aqui, e não repetida na geometria e no perfil, porque regra
    /// duplicada é regra que diverge: bastaria alguém trocar o sinal num dos
    /// dois lados para o perfil passar a aceitar o que a geometria recusa.
    /// </summary>
    public string? WhyDoesNotFit(TableLayout mesa)
    {
        if (mesa is null) return "não há mesa para a estrutura segurar";
        if (WhyInvalid is { } porCausaDaEstrutura) return porCausaDaEstrutura;
        if (mesa.WhyInvalid is { } porCausaDaMesa) return porCausaDaMesa;

        if (RafterLength > mesa.Depth)
        {
            return $"a tesoura tem {Texto(RafterLength)} m e os módulos ocupam "
                + $"{Texto(mesa.Depth)} m na inclinação: ela precisa ser menor que eles";
        }

        // Os dois balanços juntos não podem comer a mesa inteira: sobraria um
        // vão de comprimento zero ou negativo entre o primeiro e o último
        // pilar, que é pilar em cima de pilar.
        if (2 * PillarCantilever >= mesa.Length)
        {
            return $"os dois balanços somam {Texto(2 * PillarCantilever)} m numa mesa de "
                + $"{Texto(mesa.Length)} m: não sobra estrutura entre os pilares das pontas";
        }

        // Vãos escritos têm que fechar com a mesa: é a conferência que o
        // Renan pediu ao digitar P1-P2, P2-P3.
        if (_vaos is not null && Pillars(mesa).WhyDoesNotFit(mesa.Length) is { } naoFecha)
            return naoFecha;

        return null;
    }

    /// <summary>A linha que descreve a estrutura para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Estrutura inválida: {motivo}.";

        var vao = _vaos is null
            ? $"vão de {Texto(PillarSpanTarget)} m"
            : $"vãos escritos ({string.Join(" + ", _vaos.Select(Texto))} m)";

        var enterro = MinEmbedment is { } t3 ? $", enterro mínimo (T3) de {Texto(t3)} m" : "";

        return $"tesoura de {Texto(RafterLength)} m, pilar a {Texto(PillarAlongRafter)} m dela, "
            + $"seção {Texto(PillarWidth)} × {Texto(PillarDepth)} m, {vao} e balanço de "
            + $"{Texto(PillarCantilever)} m{enterro}";
    }

    /// <summary>
    /// Igualdade campo a campo, com os vãos comparados pelo valor.
    ///
    /// Sem isto valeria a do record, que compara a referência da lista: a
    /// mesma estrutura lida de volta do arquivo sairia diferente.
    /// </summary>
    public bool Equals(TableFrame? outra) =>
        outra is not null
        && RafterLength.Equals(outra.RafterLength)
        && PillarAlongRafter.Equals(outra.PillarAlongRafter)
        && PillarWidth.Equals(outra.PillarWidth)
        && PillarDepth.Equals(outra.PillarDepth)
        && PillarSpanTarget.Equals(outra.PillarSpanTarget)
        && PillarCantilever.Equals(outra.PillarCantilever)
        && Nullable.Equals(MinEmbedment, outra.MinEmbedment)
        && (_vaos is null
            ? outra._vaos is null
            : outra._vaos is not null && _vaos.AsSpan().SequenceEqual(outra._vaos));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var codigo = new HashCode();

        codigo.Add(RafterLength);
        codigo.Add(PillarAlongRafter);
        codigo.Add(PillarWidth);
        codigo.Add(PillarDepth);
        codigo.Add(PillarSpanTarget);
        codigo.Add(PillarCantilever);
        codigo.Add(MinEmbedment);
        codigo.Add(_vaos is null);
        if (_vaos is not null) foreach (var vao in _vaos) codigo.Add(vao);

        return codigo.ToHashCode();
    }

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);

    private static bool Medida(double valor) =>
        double.IsFinite(valor) && valor > 0 && valor <= MaiorMedida;
}
