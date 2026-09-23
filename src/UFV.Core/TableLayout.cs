using System.Globalization;

namespace UFV.Core;

/// <summary>
/// Como os módulos se arrumam na mesa.
/// </summary>
public enum TableArrangement
{
    /// <summary>1V: uma fileira de módulos em pé.</summary>
    SingleRow,

    /// <summary>2V: duas fileiras de módulos em pé, uma acima da outra.</summary>
    DoubleRow,
}

/// <summary>
/// A mesa em coordenadas próprias: quantos módulos, como arrumados, com que
/// folgas — e o comprimento que sai disso.
///
/// O comprimento é o número de que tudo depois depende: quantas mesas cabem na
/// área, onde ficam os pilares, quanta estrutura comprar. E é justamente por
/// ser uma soma simples que ninguém confere: errar o número de espaçamentos
/// (são 27 entre 28 módulos, não 28) some 2 cm, e o resultado continua
/// parecendo razoável.
///
/// Aqui não há terreno nem inclinação. A mesa ainda é um retângulo deitado; o
/// relevo entra depois, quando ela for assentada — e é lá que a declividade
/// longitudinal, que o Renan pediu para medir e filtrar, vai ser calculada.
/// </summary>
/// <param name="Module">O módulo usado.</param>
/// <param name="ModuleCount">Quantos módulos a mesa tem ao todo.</param>
/// <param name="Arrangement">1V ou 2V.</param>
/// <param name="HorizontalGap">Folga entre módulos vizinhos ao longo da mesa.</param>
/// <param name="VerticalGap">
/// Folga entre a fileira de baixo e a de cima. Só existe em 2V; em 1V é
/// ignorada, e de propósito: não se recusa uma mesa por causa de um campo que
/// não participa de conta nenhuma.
/// </param>
/// <param name="LeftMargin">Estrutura que passa da ponta esquerda.</param>
/// <param name="RightMargin">
/// Estrutura que passa da ponta direita. Separada da esquerda porque a
/// estrutura nem sempre é simétrica.
/// </param>
public sealed record TableLayout(
    SolarModule Module,
    int ModuleCount,
    TableArrangement Arrangement,
    double HorizontalGap,
    double VerticalGap,
    double LeftMargin,
    double RightMargin)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Maior folga aceita, em metro.
    ///
    /// Não é regra de projeto: é rede para erro de escala, como o teto de
    /// medida do módulo. Quem digitar 20 achando que o campo é em centímetro
    /// precisa descobrir agora, e não quando a mesa sair com 280 m.
    /// </summary>
    private const double MaiorFolga = 5.0;

    /// <summary>
    /// Maior número de módulos numa mesa.
    ///
    /// Mesma ideia: uma mesa de mil módulos teria quase um quilômetro e meio.
    /// Não existe, e quem digitou isso errou o campo.
    /// </summary>
    private const int MaiorContagem = 500;

    /// <summary>
    /// Quantas colunas de módulos a mesa tem.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a mesa não fecha. Sem isto, uma mesa 2V com 27 módulos devolveria 13
    /// em silêncio, por truncamento — e 13 é um número que o passo seguinte
    /// usaria para posicionar pilar sem desconfiar de nada.
    /// </exception>
    public int Columns
    {
        get
        {
            Conferir();

            return Arrangement == TableArrangement.DoubleRow
                ? ModuleCount / 2
                : ModuleCount;
        }
    }

    /// <summary>Se a mesa fecha. Quando não fecha, <see cref="WhyInvalid"/> diz por quê.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>
    /// O motivo de a mesa não fechar, em português, ou null se ela fecha.
    ///
    /// O motivo existe porque "mesa inválida" sozinho não ajuda ninguém: a
    /// janela do 3.7 precisa dizer qual campo está errado, e o usuário precisa
    /// saber o que corrigir. Por isso cada mensagem nomeia um campo só.
    /// </summary>
    public string? WhyInvalid
    {
        get
        {
            if (Module is null) return "não há módulo escolhido";

            if (!Module.IsValid)
                return $"o módulo {Module.DisplayName} tem medida impossível";

            if (ModuleCount <= 0) return "a mesa precisa de pelo menos um módulo";

            if (ModuleCount > MaiorContagem)
                return $"a mesa tem {ModuleCount} módulos, mais que os {MaiorContagem} possíveis";

            if (Arrangement == TableArrangement.DoubleRow && ModuleCount % 2 != 0)
            {
                // Uma coluna pela metade é decisão de projeto — qual ponta fica
                // vazia, se a fileira de cima ou a de baixo —, e não é o plugin
                // que escolhe.
                return $"uma mesa 2V precisa de um número par de módulos, e foram {ModuleCount}";
            }

            if (!Folga(HorizontalGap)) return "o espaçamento entre módulos não é uma medida válida";
            if (!Folga(LeftMargin)) return "a sobra da esquerda não é uma medida válida";
            if (!Folga(RightMargin)) return "a sobra da direita não é uma medida válida";

            // O espaçamento vertical só é conferido quando participa da conta.
            if (Arrangement == TableArrangement.DoubleRow && !Folga(VerticalGap))
                return "o espaçamento entre as duas fileiras não é uma medida válida";

            return null;
        }
    }

    /// <summary>
    /// O comprimento da mesa, de ponta a ponta da estrutura.
    ///
    /// São <c>Columns - 1</c> espaçamentos e não <c>Columns</c>: eles ficam
    /// entre os módulos, e entre 28 módulos há 27 vãos.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a mesa não fecha. Devolver um número aqui seria pior que lançar: quem
    /// esquecesse de olhar <see cref="IsValid"/> levaria um comprimento
    /// plausível de uma mesa que não existe.
    /// </exception>
    public double Length
    {
        get
        {
            Conferir();

            return Columns * Module.Width
                + (Columns - 1) * HorizontalGap
                + LeftMargin
                + RightMargin;
        }
    }

    /// <summary>
    /// A medida da mesa no outro sentido, ao longo da inclinação — a altura de
    /// um módulo em 1V, ou a dos dois mais a folga entre eles em 2V.
    ///
    /// É este número que precisa caber na estrutura inclinada: numa mesa 2V com
    /// o módulo do Renan dá 4,788 m, contra os 3 m de tesoura que ele
    /// informou. A incoerência está anotada em PROGRESSO.md.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a mesa não fecha, pelo mesmo motivo de <see cref="Length"/>.
    /// </exception>
    public double Depth
    {
        get
        {
            Conferir();

            return Arrangement == TableArrangement.DoubleRow
                ? 2 * Module.Height + VerticalGap
                : Module.Height;
        }
    }

    /// <summary>A linha que descreve a mesa para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Mesa inválida: {motivo}.";

        var arranjo = Arrangement == TableArrangement.DoubleRow ? "2V" : "1V";

        return $"{ModuleCount} módulos em {arranjo} ({Columns} colunas) — "
            + $"{Length.ToString("0.###", Brasil)} m de comprimento por "
            + $"{Depth.ToString("0.###", Brasil)} m";
    }

    private void Conferir()
    {
        if (WhyInvalid is { } motivo)
        {
            throw new InvalidOperationException($"A mesa não fecha: {motivo}.");
        }
    }

    /// <summary>
    /// Folga zero é projeto legítimo — estrutura encostada existe. Negativa
    /// não: seria módulo sobrepondo módulo.
    /// </summary>
    private static bool Folga(double valor) =>
        double.IsFinite(valor) && valor >= 0 && valor <= MaiorFolga;
}
