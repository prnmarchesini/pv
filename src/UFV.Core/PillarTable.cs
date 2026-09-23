using System.Globalization;
using System.Text;

namespace UFV.Core;

/// <summary>
/// Onde cada pilar fica ao longo da mesa, contado do zero da estrutura.
///
/// Os vãos são desiguais de propósito: o projeto do fabricante não distribui
/// pilar igualzinho, e forçar isso seria inventar estrutura que não existe. O
/// que não pode é a soma dos vãos não bater com o comprimento da mesa — aí a
/// tabela descreve uma mesa que não é aquela, e a diferença só aparece no
/// campo, quando o ferro já está comprado.
///
/// Um pilar por posição, e as posições são as distâncias acumuladas: n vãos
/// dão n+1 pilares, um em cada ponta.
/// </summary>
public sealed record PillarTable
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Um milímetro. Diferença menor que isso entre a soma dos vãos e o
    /// comprimento é arredondamento de quem digitou, não erro de projeto — e é
    /// a mesma tolerância com que o drapeamento enxuga vértice.
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

    /// <param name="spans">Os vãos, na ordem, do zero da estrutura em diante.</param>
    public PillarTable(IReadOnlyList<double> spans)
    {
        // Copiado, e não guardado por referência: senão o chamador podia mexer
        // na lista depois e transformar uma tabela válida em inválida já
        // construída. Um record que muda sozinho não é um record.
        _vaos = spans is null ? [] : [.. spans];
    }

    /// <summary>Os vãos, na ordem, do zero da estrutura em diante.</summary>
    public IReadOnlyList<double> Spans => _vaos;

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
    /// As posições dos pilares, do zero da estrutura em diante.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se a tabela não serve.</exception>
    public IReadOnlyList<double> Positions
    {
        get
        {
            Conferir();

            var posicoes = new double[_vaos.Length + 1];
            var acumulado = 0.0;

            for (var i = 0; i < _vaos.Length; i++)
            {
                acumulado += _vaos[i];
                posicoes[i + 1] = acumulado;
            }

            return posicoes;
        }
    }

    /// <summary>A soma dos vãos: onde cai o último pilar.</summary>
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

        var total = TotalSpan;
        var diferenca = total - comprimento;

        if (Math.Abs(diferenca) <= Tolerancia) return null;

        var sinal = diferenca > 0 ? "sobra" : "falta";

        return $"a tabela de pilares não fecha com a mesa: os vãos somam {Texto(total)} m "
            + $"e a mesa tem {Texto(comprimento)} m ({sinal} {Texto(Math.Abs(diferenca))} m)";
    }

    /// <summary>
    /// Uma tabela de vãos iguais, o mais perto possível do alvo, que fecha
    /// exatamente com o comprimento.
    ///
    /// Existe porque o Renan pediu assim: "pilares com 3 m de distanciamento,
    /// não precisa ser 3 m cravado, provavelmente vai dar quebrado". O alvo é
    /// alvo, não regra — o que não pode é a soma não fechar, e por isso o vão
    /// sai do comprimento dividido, e não do alvo arredondado.
    ///
    /// É ponto de partida: a tabela continua editável vão a vão.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se o comprimento ou o alvo não forem medidas possíveis, ou se a mesa
    /// exigir mais vãos do que uma tabela comporta.
    /// </exception>
    public static PillarTable Distribute(double comprimento, double alvo)
    {
        if (!double.IsFinite(comprimento) || comprimento <= 0)
            throw new ArgumentOutOfRangeException(nameof(comprimento), comprimento,
                "O comprimento da mesa precisa ser positivo.");

        if (!double.IsFinite(alvo) || alvo <= 0)
            throw new ArgumentOutOfRangeException(nameof(alvo), alvo,
                "O vão pretendido precisa ser positivo.");

        // Quantos vãos o alvo pede, e quantos o limite de vão exige. Mandar
        // direto o número do alvo devolvia tabela que esta mesma classe
        // reprova: alvo de 60 m em mesa de 60 m dava um vão só, de 60 m, acima
        // do máximo.
        var pedidos = comprimento / alvo;
        var necessarios = comprimento / MaiorVao;

        if (pedidos > MaiorQuantidadeDeVaos || necessarios > MaiorQuantidadeDeVaos)
        {
            // Antes do cast para int, que satura em silêncio e devolveria uma
            // tabela sem relação nenhuma com o que foi pedido.
            throw new ArgumentOutOfRangeException(nameof(comprimento), comprimento,
                $"Uma mesa de {Texto(comprimento)} m com vão de {Texto(alvo)} m passaria dos "
                + $"{MaiorQuantidadeDeVaos} vãos que uma tabela comporta.");
        }

        // Pelo menos um vão: uma mesa mais curta que o alvo ainda precisa de
        // dois pilares, um em cada ponta.
        var quantos = Math.Max(1, (int)Math.Round(pedidos, MidpointRounding.AwayFromZero));

        quantos = Math.Max(quantos, (int)Math.Ceiling(necessarios));

        var vao = comprimento / quantos;
        var vaos = new double[quantos];

        Array.Fill(vaos, vao);

        return new PillarTable(vaos);
    }

    /// <summary>A linha que descreve a tabela para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Tabela de pilares inválida: {motivo}.";

        var vaos = string.Join(" + ", _vaos.Select(Texto));

        return $"{_vaos.Length + 1} pilares em {_vaos.Length} vão(s): {vaos} = {Texto(TotalSpan)} m";
    }

    /// <summary>
    /// Duas tabelas com os mesmos vãos são a mesma tabela.
    ///
    /// Sem isto valeria a igualdade que o record gera, que compara a
    /// referência do vetor — e duas tabelas idênticas sairiam diferentes.
    /// </summary>
    public bool Equals(PillarTable? outra) =>
        outra is not null && _vaos.AsSpan().SequenceEqual(outra._vaos);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var codigo = new HashCode();
        foreach (var vao in _vaos) codigo.Add(vao);
        return codigo.ToHashCode();
    }

    /// <summary>
    /// Só os vãos entram no texto do record.
    ///
    /// Sem este corte, o ToString gerado imprimiria também PillarCount,
    /// Positions e TotalSpan — que lançam em tabela inválida. Justamente a
    /// tabela que se quer ver num log, num assert que falhou ou no depurador
    /// seria a que explode.
    /// </summary>
    private bool PrintMembers(StringBuilder texto)
    {
        texto.Append("Spans = [").Append(string.Join(", ", _vaos.Select(Texto))).Append(']');
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
