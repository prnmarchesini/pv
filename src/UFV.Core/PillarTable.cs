using System.Globalization;
using System.Text;

namespace UFV.Core;

/// <summary>
/// Onde cada pilar fica ao longo da mesa, contado do zero da estrutura.
///
/// A tabela tem duas partes: o <b>balanço</b>, que é quanto de estrutura sobra
/// para fora do primeiro e do último pilar, e os <b>vãos</b> entre pilares.
/// Balanço zero põe o pilar cravado na ponta, que é projeto legítimo; balanço
/// positivo recua os dois pilares das extremidades. O Renan confirmou em
/// 23/09/2026 que a estrutura dele aceita os dois, e por isso ele é parâmetro
/// e não constante.
///
/// Os vãos são desiguais de propósito: o projeto do fabricante não distribui
/// pilar igualzinho, e forçar isso seria inventar estrutura que não existe. O
/// que não pode é a soma — balanço, vãos, balanço — não bater com o
/// comprimento da mesa: aí a tabela descreve uma mesa que não é aquela, e a
/// diferença só aparece no campo, com o ferro já comprado.
///
/// <c>n</c> vãos dão <c>n+1</c> pilares.
/// </summary>
public sealed record PillarTable
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Um milímetro. Diferença menor que isso entre a soma e o comprimento é
    /// arredondamento de quem digitou, não erro de projeto — e é a mesma
    /// tolerância com que o drapeamento enxuga vértice.
    /// </summary>
    private const double Tolerancia = 0.001;

    /// <summary>
    /// Maior vão aceito, em metro. Rede para erro de escala, como nas outras
    /// medidas: quem digitou 300 achando que o campo era em centímetro.
    /// </summary>
    private const double MaiorVao = 50.0;

    /// <summary>
    /// Maior número de vãos numa tabela.
    ///
    /// Existe para <see cref="Distribute"/> não tentar alocar um vetor de um
    /// bilhão de posições quando alguém pedir alvo microscópico, e para o
    /// cast para <c>int</c> não saturar em silêncio.
    /// </summary>
    private const int MaiorQuantidadeDeVaos = 1000;

    private readonly double[] _vaos;

    /// <param name="spans">Os vãos entre pilares, na ordem.</param>
    /// <param name="cantilever">
    /// O balanço de cada ponta: quanto de estrutura sobra para fora do
    /// primeiro e do último pilar. Zero deixa o pilar na ponta.
    /// </param>
    public PillarTable(IReadOnlyList<double> spans, double cantilever = 0)
    {
        // Copiado, e não guardado por referência: senão o chamador podia mexer
        // na lista depois e transformar uma tabela válida em inválida já
        // construída. Um record que muda sozinho não é um record.
        _vaos = spans is null ? [] : [.. spans];
        Cantilever = cantilever;
    }

    /// <summary>Os vãos entre pilares, na ordem.</summary>
    public IReadOnlyList<double> Spans => _vaos;

    /// <summary>O balanço de cada ponta da estrutura.</summary>
    public double Cantilever { get; }

    /// <summary>Se a tabela descreve uma sequência de pilares possível.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>
    /// O motivo de a tabela não servir, em português, ou null se ela serve.
    ///
    /// O motivo nomeia QUAL vão está errado. Numa tabela de doze, "tabela
    /// inválida" manda o usuário procurar no escuro.
    /// </summary>
    public string? WhyInvalid
    {
        get
        {
            if (!double.IsFinite(Cantilever) || Cantilever < 0)
                return "o balanço das pontas não é uma distância válida";

            if (Cantilever > MaiorVao)
                return $"o balanço tem {Texto(Cantilever)} m, mais que os {MaiorVao:0} m possíveis";

            if (_vaos.Length == 0) return "a tabela precisa de pelo menos um vão";

            for (var i = 0; i < _vaos.Length; i++)
            {
                var vao = _vaos[i];

                // Zero poria dois pilares no mesmo lugar; negativo andaria para
                // trás. Os dois passariam pela soma, se ela fosse a única
                // conferência.
                if (!double.IsFinite(vao) || vao <= 0)
                    return $"o vão {i + 1} não é uma distância válida";

                if (vao > MaiorVao)
                    return $"o vão {i + 1} tem {Texto(vao)} m, mais que os {MaiorVao:0} m possíveis";
            }

            return null;
        }
    }

    /// <summary>Quantos pilares a tabela descreve: um a mais que os vãos.</summary>
    /// <exception cref="InvalidOperationException">Se a tabela não serve.</exception>
    public int PillarCount
    {
        get
        {
            Conferir();
            return _vaos.Length + 1;
        }
    }

    /// <summary>
    /// As posições dos pilares, contadas do zero da estrutura.
    ///
    /// Com balanço, o primeiro pilar não fica em zero: fica no balanço. É essa
    /// a diferença entre "onde a estrutura começa" e "onde o primeiro pilar
    /// encosta no chão".
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a tabela não serve.</exception>
    public IReadOnlyList<double> Positions
    {
        get
        {
            Conferir();

            var posicoes = new double[_vaos.Length + 1];
            var acumulado = Cantilever;

            posicoes[0] = acumulado;

            for (var i = 0; i < _vaos.Length; i++)
            {
                acumulado += _vaos[i];
                posicoes[i + 1] = acumulado;
            }

            return posicoes;
        }
    }

    /// <summary>A soma dos vãos: a distância do primeiro ao último pilar.</summary>
    /// <exception cref="InvalidOperationException">Se a tabela não serve.</exception>
    public double TotalSpan
    {
        get
        {
            Conferir();

            // Somado na mesma ordem de Positions, para os dois nunca
            // discordarem por arredondamento.
            var total = 0.0;
            foreach (var vao in _vaos) total += vao;

            return total;
        }
    }

    /// <summary>
    /// O comprimento de estrutura que a tabela descreve: os dois balanços mais
    /// os vãos.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a tabela não serve.</exception>
    public double TotalLength => 2 * Cantilever + TotalSpan;

    /// <summary>
    /// Por que esta tabela não serve para uma mesa deste comprimento, ou null
    /// se serve.
    ///
    /// É a validação que o plano pede: a tabela fecha com o comprimento. A
    /// mensagem traz os dois números e a diferença, porque é a diferença que
    /// diz ao usuário o que corrigir — e diz se sobra ou se falta, porque o
    /// sinal é o que manda alongar ou encurtar.
    /// </summary>
    public string? WhyDoesNotFit(double comprimento)
    {
        if (WhyInvalid is { } motivo) return motivo;

        if (!double.IsFinite(comprimento) || comprimento <= 0)
            return "o comprimento da mesa não é uma medida válida";

        var total = TotalLength;
        var diferenca = total - comprimento;

        if (Math.Abs(diferenca) <= Tolerancia) return null;

        var sinal = diferenca > 0 ? "sobra" : "falta";

        return $"a tabela de pilares não fecha com a mesa: ela cobre {Texto(total)} m "
            + $"e a mesa tem {Texto(comprimento)} m ({sinal} {Texto(Math.Abs(diferenca))} m)";
    }

    /// <summary>
    /// Uma tabela de vãos iguais, o mais perto possível do alvo, que fecha
    /// exatamente com o comprimento.
    ///
    /// Existe porque o Renan pediu assim: "pilares com 3 m de distanciamento,
    /// não precisa ser 3 m cravado, provavelmente vai dar quebrado". O alvo é
    /// alvo, não regra — o que não pode é a soma não fechar, e por isso o vão
    /// sai do que sobra dividido, e não do alvo arredondado.
    ///
    /// É ponto de partida: a tabela continua editável vão a vão.
    /// </summary>
    /// <param name="comprimento">O comprimento da mesa, de ponta a ponta.</param>
    /// <param name="alvo">O vão pretendido entre pilares.</param>
    /// <param name="balanco">
    /// Quanto de estrutura sobra para fora do primeiro e do último pilar. Zero
    /// deixa o pilar cravado na ponta.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se alguma medida não for possível, ou se a mesa exigir mais vãos do que
    /// uma tabela comporta.
    /// </exception>
    public static PillarTable Distribute(double comprimento, double alvo, double balanco = 0)
    {
        if (!double.IsFinite(comprimento) || comprimento <= 0)
            throw new ArgumentOutOfRangeException(nameof(comprimento), comprimento,
                "O comprimento da mesa precisa ser positivo.");

        if (!double.IsFinite(alvo) || alvo <= 0)
            throw new ArgumentOutOfRangeException(nameof(alvo), alvo,
                "O vão pretendido precisa ser positivo.");

        if (!double.IsFinite(balanco) || balanco < 0)
            throw new ArgumentOutOfRangeException(nameof(balanco), balanco,
                "O balanço não pode ser negativo.");

        if (2 * balanco >= comprimento)
        {
            throw new ArgumentOutOfRangeException(nameof(balanco), balanco,
                $"Os dois balanços somam {Texto(2 * balanco)} m numa mesa de "
                + $"{Texto(comprimento)} m: não sobra estrutura entre os pilares das pontas.");
        }

        // O que os vãos iguais cobrem é o miolo, do primeiro ao último pilar —
        // e não a mesa inteira.
        var miolo = comprimento - 2 * balanco;

        var pedidos = miolo / alvo;
        var necessarios = miolo / MaiorVao;

        if (pedidos > MaiorQuantidadeDeVaos || necessarios > MaiorQuantidadeDeVaos)
        {
            // Antes do cast para int, que satura em silêncio e devolveria uma
            // tabela sem relação nenhuma com o que foi pedido.
            throw new ArgumentOutOfRangeException(nameof(comprimento), comprimento,
                $"Uma mesa de {Texto(comprimento)} m com vão de {Texto(alvo)} m passaria dos "
                + $"{MaiorQuantidadeDeVaos} vãos que uma tabela comporta.");
        }

        // Pelo menos um vão: uma mesa mais curta que o alvo ainda precisa de
        // dois pilares.
        var quantos = Math.Max(1, (int)Math.Round(pedidos, MidpointRounding.AwayFromZero));

        quantos = Math.Max(quantos, (int)Math.Ceiling(necessarios));

        var vao = miolo / quantos;
        var vaos = new double[quantos];

        Array.Fill(vaos, vao);

        return new PillarTable(vaos, balanco);
    }

    /// <summary>A linha que descreve a tabela para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Tabela de pilares inválida: {motivo}.";

        var vaos = string.Join(" + ", _vaos.Select(Texto));

        var balanco = Cantilever > 0
            ? $", com balanço de {Texto(Cantilever)} m em cada ponta"
            : ", com o pilar na ponta da estrutura";

        return $"{_vaos.Length + 1} pilares em {_vaos.Length} vão(s): {vaos} = "
            + $"{Texto(TotalSpan)} m{balanco}";
    }

    /// <summary>
    /// Duas tabelas com os mesmos vãos e o mesmo balanço são a mesma tabela.
    ///
    /// Sem isto valeria a igualdade que o record gera, que compara a
    /// referência do vetor — e duas tabelas idênticas sairiam diferentes.
    /// </summary>
    public bool Equals(PillarTable? outra) =>
        outra is not null
        && Cantilever.Equals(outra.Cantilever)
        && _vaos.AsSpan().SequenceEqual(outra._vaos);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var codigo = new HashCode();

        codigo.Add(Cantilever);
        foreach (var vao in _vaos) codigo.Add(vao);

        return codigo.ToHashCode();
    }

    /// <summary>
    /// Só os vãos e o balanço entram no texto do record.
    ///
    /// Sem este corte, o ToString gerado imprimiria também PillarCount,
    /// Positions e TotalSpan — que lançam em tabela inválida. Justamente a
    /// tabela que se quer ver num log, num assert que falhou ou no depurador
    /// seria a que explode.
    /// </summary>
    private bool PrintMembers(StringBuilder texto)
    {
        texto.Append("Spans = [").Append(string.Join(", ", _vaos.Select(Texto))).Append(']');
        texto.Append(", Cantilever = ").Append(Texto(Cantilever));
        return true;
    }

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);

    private void Conferir()
    {
        if (WhyInvalid is { } motivo)
        {
            throw new InvalidOperationException($"A tabela de pilares não serve: {motivo}.");
        }
    }
}
