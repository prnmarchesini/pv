namespace UFV.Geo;

/// <summary>
/// De que lado de uma linha um ponto está, olhando em planta.
/// </summary>
public enum LineSide
{
    /// <summary>Em cima da linha, dentro da tolerância.</summary>
    On,

    /// <summary>À esquerda de quem caminha do primeiro ponto para o segundo.</summary>
    Left,

    /// <summary>À direita de quem caminha do primeiro ponto para o segundo.</summary>
    Right,
}

/// <summary>
/// O lado de uma linha, em planta.
///
/// Existe por causa do passo 4.2: o usuário desenha a linha de alinhamento e
/// indica de que lado ficam as mesas. "Lado" precisa de uma definição que não
/// dependa de para onde ele desenhou a linha — e é aí que mora o cuidado.
///
/// A definição aqui é relativa ao sentido do traçado: esquerda e direita de
/// quem caminha do primeiro ponto para o segundo. Desenhar a mesma linha ao
/// contrário troca os dois lados. Por isso quem guarda um alinhamento guarda o
/// SENTIDO junto, e nunca só a reta: sem ele, reabrir o desenho põe as mesas do
/// lado errado sem nada parecer estranho.
/// </summary>
public static class LineSides
{
    /// <summary>
    /// Distância abaixo da qual o ponto conta como estando em cima da linha.
    ///
    /// Um milímetro, a tolerância de regra da arquitetura. Não é capricho: o
    /// usuário clica para indicar o lado, e um clique a meio milímetro da
    /// linha não é uma escolha — é a mão tremendo. Responder "esquerda" nesse
    /// caso seria inventar uma decisão que ele não tomou.
    /// </summary>
    public const double Tolerancia = 0.001;

    /// <summary>
    /// Menor comprimento em planta para uma linha servir de referência.
    ///
    /// É coisa diferente da <see cref="Tolerancia"/>, e a revisão do 4.2
    /// apontou que as duas estavam com o mesmo número e o mesmo nome. Uma
    /// responde "o clique foi em cima da linha?"; esta responde "isto é uma
    /// linha de referência ou foi um clique duplo desalinhado?".
    ///
    /// Dez centímetros: matematicamente uma linha de 2 mm tem direção, e como
    /// alinhamento de usina ela não é nada.
    /// </summary>
    public const double ComprimentoMinimo = 0.10;

    /// <summary>
    /// De que lado da linha (a → b) o ponto está.
    ///
    /// Só X e Y entram: a cota não participa, porque alinhamento é coisa de
    /// planta. Passar pontos com Z diferente é normal e não muda a resposta.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se algum ponto não for finito, ou se a linha for degenerada — dois
    /// pontos no mesmo lugar não definem lado nenhum, e devolver "em cima"
    /// esconderia o problema.
    /// </exception>
    public static LineSide Of(Point3 a, Point3 b, Point3 ponto)
    {
        Conferir(a, nameof(a));
        Conferir(b, nameof(b));
        Conferir(ponto, nameof(ponto));

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var comprimento = Math.Sqrt(dx * dx + dy * dy);

        if (comprimento <= Tolerancia)
        {
            throw new ArgumentOutOfRangeException(nameof(b), comprimento,
                "A linha tem comprimento zero em planta: não há lado a decidir.");
        }

        // Produto vetorial em duas dimensões, dividido pelo comprimento: dá a
        // distância com sinal do ponto até a RETA. Sem dividir, o valor
        // cresceria com o tamanho da linha, e a tolerância de um milímetro
        // significaria coisas diferentes numa linha de 10 m e numa de 500 m.
        var distancia = ((ponto.X - a.X) * dy - (ponto.Y - a.Y) * dx) / comprimento;

        if (Math.Abs(distancia) <= Tolerancia) return LineSide.On;

        // Sinal negativo é esquerda: com a linha apontando para o leste
        // (dy = 0, dx > 0), um ponto ao norte dá distância negativa.
        return distancia < 0 ? LineSide.Left : LineSide.Right;
    }

    /// <summary>
    /// Por que esta linha é curta demais para servir de referência, ou null se
    /// ela serve.
    ///
    /// Existe para o comando conferir isto UMA vez, logo depois do traçado, em
    /// vez de descobrir no clique do lado — e poupar o usuário de dois prompts
    /// inúteis antes de dizer que não vai dar.
    /// </summary>
    public static string? WhyTooShort(Point3 a, Point3 b)
    {
        if (!a.IsFinite || !b.IsFinite) return "a linha tem ponto com coordenada inválida";

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var comprimento = Math.Sqrt(dx * dx + dy * dy);

        if (comprimento < ComprimentoMinimo)
        {
            return $"a linha tem {comprimento:0.###} m em planta, e o mínimo para servir de "
                + $"referência é {ComprimentoMinimo:0.##} m";
        }

        return null;
    }

    /// <summary>
    /// A distância com sinal do ponto até a reta, em metro.
    ///
    /// Negativa à esquerda, positiva à direita, pelo mesmo critério de
    /// <see cref="Of"/>. Serve para quem precisa da grandeza, e não só do
    /// lado — por exemplo, para ordenar mesas pela distância ao alinhamento.
    /// </summary>
    public static double SignedDistance(Point3 a, Point3 b, Point3 ponto)
    {
        Conferir(a, nameof(a));
        Conferir(b, nameof(b));
        Conferir(ponto, nameof(ponto));

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var comprimento = Math.Sqrt(dx * dx + dy * dy);

        if (comprimento <= Tolerancia)
        {
            throw new ArgumentOutOfRangeException(nameof(b), comprimento,
                "A linha tem comprimento zero em planta: não há distância com sinal.");
        }

        var distancia = ((ponto.X - a.X) * dy - (ponto.Y - a.Y) * dx) / comprimento;

        if (!double.IsFinite(distancia))
        {
            // A conta estourou o double, com pontos tão distantes que a
            // subtração perdeu sentido. Devolver infinito seria entregar um
            // número em metro que não é número.
            throw new ArgumentOutOfRangeException(nameof(ponto), ponto,
                "O ponto está longe demais para a distância caber num número.");
        }

        return distancia;
    }

    /// <summary>O lado oposto. Em cima continua em cima.</summary>
    public static LineSide Opposite(this LineSide lado) => lado switch
    {
        LineSide.Left => LineSide.Right,
        LineSide.Right => LineSide.Left,
        _ => LineSide.On,
    };

    /// <summary>
    /// O lado como ele vai para arquivo e para XData.
    ///
    /// Nome, e nunca o número da enumeração: acrescentar um valor no meio dela
    /// renumeraria os seguintes, e todo alinhamento já gravado passaria a
    /// dizer o outro lado — sem erro nenhum na leitura, e com a usina inteira
    /// nascendo do lado errado.
    /// </summary>
    public static string Name(this LineSide lado) => lado switch
    {
        LineSide.Left => "Left",
        LineSide.Right => "Right",
        LineSide.On => "On",
        _ => throw new ArgumentOutOfRangeException(nameof(lado), lado, "Lado desconhecido."),
    };

    /// <summary>
    /// O lado a partir do nome gravado, ou falso se o texto não for um nome.
    ///
    /// A conferência é um mapa explícito, e não <c>Enum.TryParse</c>. Parece
    /// preciosismo e não é: <c>Enum.TryParse&lt;LineSide&gt;("1")</c> devolve
    /// <c>Left</c>, e <c>"2"</c> devolve <c>Right</c> — o número da enumeração
    /// passa como se fosse nome. Com isso, a defesa que <see cref="Name"/>
    /// descreve simplesmente não existia: um registro com "1" era aceito, e no
    /// dia da renumeração viraria "Right" em silêncio.
    /// </summary>
    public static bool TryParseName(string? texto, out LineSide lado)
    {
        lado = LineSide.On;

        switch (texto)
        {
            case "Left":
                lado = LineSide.Left;
                return true;

            case "Right":
                lado = LineSide.Right;
                return true;

            case "On":
                lado = LineSide.On;
                return true;

            default:
                return false;
        }
    }

    /// <summary>O nome do lado, em português, para mensagem ao usuário.</summary>
    public static string Describe(this LineSide lado) => lado switch
    {
        LineSide.Left => "esquerda",
        LineSide.Right => "direita",
        _ => "sobre a linha",
    };

    private static void Conferir(Point3 ponto, string nome)
    {
        if (!ponto.IsFinite)
            throw new ArgumentOutOfRangeException(nome, ponto, "O ponto tem coordenada inválida.");
    }
}
