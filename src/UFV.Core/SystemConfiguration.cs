using System.Globalization;

namespace UFV.Core;

/// <summary>
/// Os limites que valem para o projeto inteiro: como as mesas se orientam, que
/// altura livre é aceitável, quanto o pilar precisa entrar no chão, e o que
/// faz uma mesa ser marcada.
///
/// Este objeto quase não calcula — guarda números. E é justamente por isso que
/// ele é caro de errar: o defeito não aparece aqui, aparece três etapas
/// adiante, numa mesa marcada que não devia ser, ou pior, numa que devia e não
/// foi. O que ele faz de útil é recusar combinação incoerente antes de ela
/// virar usina.
///
/// <b>O comprimento do pilar é saída, não limite.</b> O Renan foi explícito em
/// 23/09/2026: "vc vai considerar o mínimo enterrado, o que precisa para cima,
/// e me dar o tamanho ideal; se ficar menor ou maior, problema meu — eu uso
/// filtros para selecionar". Por isso não há lista de comprimentos comerciais
/// travando nada, e o teto (<see cref="MaxPillarLength"/>) nasce desligado.
/// A divergência em relação ao plano está registrada em PROGRESSO.md.
///
/// <b>Todo ângulo aqui é radiano</b>, como manda a arquitetura. Os graus
/// aparecem só em propriedades calculadas, para a tela e para o texto.
/// </summary>
/// <param name="FacingAzimuthRadians">
/// O rumo para onde a mesa OLHA, contado do norte no sentido horário. No
/// Brasil o normal é zero: mesa olhando para o norte.
///
/// A convenção é "para onde olha", e não "para onde sobe", porque é assim que
/// o projetista fala e é assim que o PVsyst entende. Quem for girar a mesa não
/// usa este número direto — usa <see cref="UpslopeAzimuthRadians"/>, que é o
/// que o sistema local espera. Trocar os dois espelha a usina inteira, e em
/// planta isso não aparece.
/// </param>
/// <param name="Pitch">A distância fixa entre mesas vizinhas, em metro.</param>
/// <param name="MinLowEdge">Altura livre mínima na ponta baixa do módulo.</param>
/// <param name="MaxLowEdge">Altura livre máxima na ponta baixa do módulo.</param>
/// <param name="MinEmbedment">
/// O quanto o pilar precisa entrar no chão. É este número, e não o
/// comprimento, que define o pilar: o comprimento ideal é o que fica de fora
/// mais isto.
/// </param>
/// <param name="MaxEmbedment">
/// O quanto o pilar pode entrar no chão. Só morde quando o comprimento vem
/// imposto de fora — o comprimento ideal enterra exatamente o mínimo. Quem o
/// usa é <see cref="WhyEmbedmentIsWrong"/>.
/// </param>
/// <param name="MaxPillarLength">
/// Teto do comprimento do pilar, ou null para não haver teto — que é o padrão.
///
/// Ligado, ele não encurta pilar nenhum: marca. É a regra sagrada 4, "o que
/// cede é o pilar, que pode estourar e é marcado" — encurtar mudaria a altura
/// livre que o projetista pediu.
/// </param>
/// <param name="MinStep">Degrau mínimo entre mesas vizinhas.</param>
/// <param name="MaxStep">Degrau máximo entre mesas vizinhas.</param>
/// <param name="BumpToleranceFraction">
/// Que fração dos módulos de uma mesa pode estourar a faixa da ponta baixa
/// antes de a mesa inteira ser marcada.
///
/// A regra sagrada 4 fala em contagem ("5 em 20"); aqui é fração, porque
/// "cinco módulos" significa coisas diferentes numa mesa de 28 e numa de 14. É
/// divergência da regra, está registrada em PROGRESSO.md, e espera a palavra
/// do Renan.
///
/// Zero é o padrão: nenhum módulo pode invadir. Um é legítimo e desliga a
/// regra — quem puser isso está dizendo que aceita qualquer invasão.
/// </param>
/// <param name="MaxLongitudinalSlope">
/// Declividade longitudinal máxima da mesa, em radianos, ou null para não
/// haver limite. O Renan pediu 10° em 23/09/2026.
/// </param>
/// <param name="MaxGapBeforeBreak">
/// Maior espaçamento entre mesas de uma fileira antes de a fileira ser
/// quebrada em duas.
/// </param>
public sealed record SystemConfiguration(
    double FacingAzimuthRadians,
    double Pitch,
    double MinLowEdge,
    double MaxLowEdge,
    double MinEmbedment,
    double MaxEmbedment,
    double? MaxPillarLength,
    double MinStep,
    double MaxStep,
    double BumpToleranceFraction,
    double? MaxLongitudinalSlope,
    double MaxGapBeforeBreak)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Maior medida aceita em metro, como rede para erro de escala.</summary>
    private const double MaiorMedida = 50.0;

    /// <summary>
    /// Menor enterro que conta como enterro.
    ///
    /// É a tolerância de regra da arquitetura, a mesma de
    /// <see cref="Invariants.FloatingPillar"/>. Sem um piso, um enterro mínimo
    /// de um bilionésimo de metro era aceito — pilar que na prática flutua,
    /// aprovado pela configuração.
    /// </summary>
    private const double MenorEnterro = 0.001;

    private const double Grau = Math.PI / 180;

    /// <summary>
    /// A configuração de partida.
    ///
    /// Os números do Renan estão marcados; os outros são meus, escolhidos para
    /// serem conservadores, e estão listados em PROGRESSO.md esperando os
    /// dele. Um formulário em branco seria pior: ele obrigaria a digitar uma
    /// dúzia de campos antes de qualquer coisa funcionar.
    /// </summary>
    public static readonly SystemConfiguration Default = new(
        FacingAzimuthRadians: 0,            // Renan: mesa olhando para o norte
        Pitch: 6.0,                         // meu
        MinLowEdge: 0.30,                   // Renan
        MaxLowEdge: 0.80,                   // Renan
        MinEmbedment: 0.90,                 // Renan
        MaxEmbedment: 2.00,                 // meu
        MaxPillarLength: null,              // Renan: sem teto, ele filtra depois
        MinStep: 0,                         // meu
        MaxStep: 0.50,                      // meu
        BumpToleranceFraction: 0,           // meu, o mais restritivo
        MaxLongitudinalSlope: 10 * Grau,    // Renan
        MaxGapBeforeBreak: 0.50);           // meu

    /// <summary>
    /// O azimute do eixo que sobe a inclinação da mesa, em radianos.
    ///
    /// É este, e não o de "para onde a mesa olha", que gira o sistema local: o
    /// +Y local aponta da ponta baixa para a ponta alta, ou seja, para o lado
    /// OPOSTO ao que a mesa olha. Meia volta de diferença.
    ///
    /// Existe para fechar, por escrito e em código, a pendência que a etapa 3
    /// deixou aberta. Sem ele, alguém passaria o azimute de mira direto para
    /// a rotação e a usina inteira nasceria virada para o lado errado — com o
    /// desenho perfeito e a produção pela metade.
    /// </summary>
    public double UpslopeAzimuthRadians => Normalizar(FacingAzimuthRadians + Math.PI);

    /// <summary>O azimute de mira em graus, para a tela e para o texto.</summary>
    public double FacingAzimuthDegrees => FacingAzimuthRadians / Grau;

    /// <summary>A declividade longitudinal máxima em graus, para a tela e para o texto.</summary>
    public double? MaxLongitudinalSlopeDegrees =>
        MaxLongitudinalSlope is { } radianos ? radianos / Grau : null;

    /// <summary>Se os números fazem sentido entre si.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>
    /// O motivo de a configuração não fechar, em português, ou null.
    ///
    /// Cada motivo nomeia um campo só. A tela do 4.4 tem uma dúzia de campos,
    /// e "configuração inválida" manda o projetista procurar no escuro.
    /// </summary>
    public string? WhyInvalid
    {
        get
        {
            if (!double.IsFinite(FacingAzimuthRadians)
                || FacingAzimuthRadians < 0 || FacingAzimuthRadians >= 2 * Math.PI)
            {
                return "o azimute está fora de uma volta";
            }

            if (!Medida(Pitch)) return "o pitch entre mesas não é uma medida válida";

            if (Faixa(MinLowEdge, MaxLowEdge) is { } pontaBaixa)
                return $"a faixa da ponta baixa {pontaBaixa}";

            if (MinEmbedment < MenorEnterro && double.IsFinite(MinEmbedment) && MinEmbedment > 0)
                return "a faixa de enterro tem mínimo menor que um milímetro, que não é enterro";

            if (Faixa(MinEmbedment, MaxEmbedment) is { } enterro)
                return $"a faixa de enterro {enterro}";

            if (Faixa(MinStep, MaxStep, minimoPodeSerZero: true) is { } degrau)
                return $"a faixa de degrau entre mesas {degrau}";

            if (MaxPillarLength is { } teto)
            {
                if (!Medida(teto)) return "o teto de comprimento do pilar não é uma medida válida";

                // A conferência que importa não é "sobra algo acima do chão",
                // e sim "sobra o que a faixa pede". Um teto que não comporta
                // nem a ponta baixa MÍNIMA faz toda a usina nascer marcada, e
                // a configuração era aceita como coerente.
                var precisaDeNoMinimo = MinEmbedment + MinLowEdge;

                if (teto <= precisaDeNoMinimo)
                {
                    return $"o teto de pilar ({Texto(teto)} m) não comporta nem a ponta baixa "
                        + $"mínima de {Texto(MinLowEdge)} m com enterro de "
                        + $"{Texto(MinEmbedment)} m: nenhuma mesa caberia, e não sobraria nada "
                        + "acima do chão";
                }
            }

            if (!double.IsFinite(BumpToleranceFraction)
                || BumpToleranceFraction < 0 || BumpToleranceFraction > 1)
            {
                return "a tolerância de invasão por lombo precisa ser uma fração entre 0 e 1";
            }

            if (MaxLongitudinalSlope is { } inclinacao
                && (!double.IsFinite(inclinacao) || inclinacao <= 0 || inclinacao >= Math.PI / 2))
            {
                return "o limite de declividade longitudinal precisa ficar entre 0 e 90 graus";
            }

            if (!double.IsFinite(MaxGapBeforeBreak)
                || MaxGapBeforeBreak < 0 || MaxGapBeforeBreak > MaiorMedida)
            {
                return "o limite de espaçamento que quebra fileira não é uma medida válida";
            }

            return null;
        }
    }

    /// <summary>
    /// O comprimento ideal do pilar: o que fica de fora mais o enterro mínimo.
    ///
    /// É a saída que o Renan pediu. Não há arredondamento para comprimento
    /// comercial nem escolha de lista: o número é o que a geometria manda, e
    /// quem filtra é ele.
    ///
    /// A soma em si mora em <see cref="PillarSizing.Length"/>, com as redes de
    /// escala que o resto do Core tem. Repeti-la aqui criaria um segundo dono
    /// da mesma fórmula, que é o defeito que a etapa 3.5 já pagou para tirar.
    /// </summary>
    /// <param name="freeHeight">P3: o que o pilar tem acima do terreno.</param>
    /// <exception cref="InvalidOperationException">Se a configuração não fecha.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Se a altura livre não for possível.</exception>
    public double IdealPillarLength(double freeHeight)
    {
        Conferir();

        return PillarSizing.Length(freeHeight, MinEmbedment);
    }

    /// <summary>
    /// Por que este pilar passa do teto, ou null — inclusive quando não há
    /// teto, que é o padrão.
    ///
    /// Comprimento que não é número é marcado, e não aprovado: "não sei medir"
    /// virando "está bom" é a direção errada de falha.
    /// </summary>
    public string? WhyPillarIsTooLong(double pillarLength)
    {
        if (!double.IsFinite(pillarLength))
            return "o comprimento do pilar não é um número";

        if (MaxPillarLength is not { } teto) return null;
        if (pillarLength <= teto) return null;

        return $"o pilar precisa de {Texto(pillarLength)} m e o teto é {Texto(teto)} m "
            + $"(passa {Texto(pillarLength - teto)} m)";
    }

    /// <summary>
    /// Por que este enterro está fora da faixa, ou null se está dentro.
    ///
    /// Só faz diferença quando o comprimento do pilar vem imposto de fora: com
    /// o comprimento ideal, o enterro é exatamente o mínimo. É aqui que
    /// <see cref="MaxEmbedment"/> ganha uso — sem isto ele seria um campo que
    /// ninguém lê, que é o defeito que a revisão do 3.5 mandou tirar.
    /// </summary>
    public string? WhyEmbedmentIsWrong(double embedment)
    {
        if (!double.IsFinite(embedment)) return "o enterro do pilar não é um número";

        if (embedment < MinEmbedment)
        {
            return $"o pilar entra {Texto(embedment)} m no chão e o mínimo é "
                + $"{Texto(MinEmbedment)} m";
        }

        if (embedment > MaxEmbedment)
        {
            return $"o pilar entra {Texto(embedment)} m no chão e o máximo é "
                + $"{Texto(MaxEmbedment)} m";
        }

        return null;
    }

    /// <summary>
    /// Quantos módulos de uma mesa podem estourar a faixa da ponta baixa antes
    /// de a mesa ser marcada.
    ///
    /// Arredonda para baixo de propósito: a tolerância é permissão, e permissão
    /// que arredonda para cima vira permissão que ninguém pediu. O efeito
    /// colateral está registrado em PROGRESSO.md — numa mesa pequena, uma
    /// fração pequena dá zero, e a tolerância ligada não vale nada.
    /// </summary>
    public int BumpToleranceFor(int moduleCount)
    {
        // Configuração quebrada não responde número: com fração NaN o cast
        // devolvia int.MinValue, e uma tolerância de menos dois bilhões numa
        // comparação adiante é estouro esperando acontecer.
        if (moduleCount <= 0 || !IsValid) return 0;

        return (int)Math.Floor(BumpToleranceFraction * moduleCount);
    }

    /// <summary>A linha que descreve a configuração para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Configuração inválida: {motivo}.";

        var teto = MaxPillarLength is { } limite
            ? $", pilar até {Texto(limite)} m"
            : ", sem teto de pilar";

        var declividade = MaxLongitudinalSlopeDegrees is { } graus
            ? $", declividade até {Texto(graus)}°"
            : ", sem limite de declividade";

        return $"ponta baixa de {Texto(MinLowEdge)} a {Texto(MaxLowEdge)} m, enterro de "
            + $"{Texto(MinEmbedment)} a {Texto(MaxEmbedment)} m{teto}, pitch de "
            + $"{Texto(Pitch)} m{declividade}";
    }

    private void Conferir()
    {
        if (WhyInvalid is { } motivo)
        {
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");
        }
    }

    /// <summary>
    /// O que há de errado com uma faixa, ou null se ela fecha.
    ///
    /// Devolve o fim da frase, para quem chama nomear o campo: assim a
    /// mensagem sai inteira e em português sem cada caso repetir o mesmo texto.
    /// </summary>
    private static string? Faixa(double minimo, double maximo, bool minimoPodeSerZero = false)
    {
        var minimoValido = minimoPodeSerZero
            ? double.IsFinite(minimo) && minimo >= 0 && minimo <= MaiorMedida
            : Medida(minimo);

        if (!minimoValido) return "tem mínimo que não é uma medida válida";

        if (!double.IsFinite(maximo) || maximo <= 0 || maximo > MaiorMedida)
            return "tem máximo que não é uma medida válida";

        if (minimo > maximo)
            return $"está invertida: {Texto(minimo)} m de mínimo e {Texto(maximo)} m de máximo";

        return null;
    }

    /// <summary>Traz o ângulo para dentro de uma volta.</summary>
    private static double Normalizar(double radianos)
    {
        if (!double.IsFinite(radianos)) return radianos;

        var volta = 2 * Math.PI;
        var dentro = radianos % volta;

        return dentro < 0 ? dentro + volta : dentro;
    }

    private static bool Medida(double valor) =>
        double.IsFinite(valor) && valor > 0 && valor <= MaiorMedida;

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);
}
