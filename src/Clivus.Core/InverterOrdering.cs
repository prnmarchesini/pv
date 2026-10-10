namespace Clivus.Core;

/// <summary>
/// Como ordenar a lista de inversores do cadastro (Renan, 10/10/2026: "quero
/// ter opções de reordenação: por Trafo, por Nome do inversor, Trafo em
/// seguida nome"). A ordem do cadastro é a da tabela, a do Distribuir e o
/// número {I} da tag.
/// </summary>
public enum InverterOrder
{
    /// <summary>Pelo nome, na ordem natural (Inversor 2 antes de Inversor 10).</summary>
    Name,

    /// <summary>Pelo apelido do trafo, na ordem natural (T1, T2, ..., T10; sem trafo por último); dentro do trafo, a ordem que já tinham.</summary>
    Transformer,

    /// <summary>Pelo trafo e, dentro de cada trafo, pelo nome.</summary>
    TransformerThenName,
}

/// <summary>
/// A ordem natural de nomes: os números dentro do texto comparados pelo
/// valor ("Inversor 2" antes de "Inversor 10", "T9" antes de "T10"); o resto,
/// caractere a caractere sem olhar maiúscula (a mesma ordem em qualquer
/// máquina e cultura; revisão de 10/10/2026). Espaços nas pontas não contam.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    private NaturalStringComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var a = x.AsSpan().Trim();
        var b = y.AsSpan().Trim();
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            var numeroA = char.IsAsciiDigit(a[i]);
            var numeroB = char.IsAsciiDigit(b[j]);
            if (numeroA != numeroB) return numeroA ? -1 : 1;

            var (ia, jb) = (i, j);
            while (i < a.Length && char.IsAsciiDigit(a[i]) == numeroA) i++;
            while (j < b.Length && char.IsAsciiDigit(b[j]) == numeroB) j++;
            var pedacoA = a[ia..i];
            var pedacoB = b[jb..j];

            int c;
            if (numeroA)
            {
                // Pelo valor, sem limite de tamanho: sem os zeros da frente, o mais comprido é o maior.
                pedacoA = pedacoA.TrimStart('0');
                pedacoB = pedacoB.TrimStart('0');
                c = pedacoA.Length != pedacoB.Length ? pedacoA.Length.CompareTo(pedacoB.Length) : pedacoA.SequenceCompareTo(pedacoB);
            }
            else
            {
                c = pedacoA.CompareTo(pedacoB, StringComparison.OrdinalIgnoreCase);
            }

            if (c != 0) return Math.Sign(c);
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }
}
