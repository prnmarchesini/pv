namespace UFV.Core;

/// <summary>
/// O que a leitura de uma tabela de registros encontrou.
/// </summary>
/// <param name="Items">Os itens que deu para ler.</param>
/// <param name="Problem">
/// O que estava errado, em português, ou null se estava inteiro.
///
/// Existe porque tabela ilegível era descartada em silêncio, e a gravação
/// seguinte a substituía por uma lista quase vazia. Perder o índice não é grave
/// — a verdade mora na entidade, e o reindexar o reconstrói —, mas o usuário
/// precisa saber que ficou um reindexar para rodar.
/// </param>
public sealed record RecordTableResult<T>(IReadOnlyList<T> Items, string? Problem);

/// <summary>
/// O formato das tabelas de registro do plugin, em texto puro.
///
/// Todo registro nosso — áreas, alinhamentos, e o que vier — é a mesma coisa:
/// um cabeçalho com versão e quantidade, seguido de N campos de texto por item.
/// As garantias também são as mesmas, e foram caras de aprender:
///
/// <list type="bullet">
/// <item><description>versão diferente é recusada, não lida pela metade;</description></item>
/// <item><description>entrada ilegível deixa rastro, nunca some calada;</description></item>
/// <item><description>a quantidade declarada é conferida contra o que foi
/// lido — é ela que denuncia registro truncado.</description></item>
/// </list>
///
/// Mora no Core, e não no plugin, porque é texto virando lista: não tem nada de
/// CAD. A revisão do 4.2 apontou o motivo prático — enquanto isso vivia no
/// plugin, as três garantias acima não tinham teste em nível nenhum, e a
/// refatoração que as moveu de lugar só pôde ser conferida por leitura.
/// Quem fala com o desenho é um adaptador fino do lado do plugin.
/// </summary>
public static class RecordTable
{
    /// <summary>Marca do campo de versão, no começo da tabela.</summary>
    public const string CampoVersao = "FORMATO";

    /// <summary>Marca do campo de quantidade, logo depois da versão.</summary>
    public const string CampoQuantidade = "QUANTIDADE";

    /// <summary>Quantos campos o cabeçalho ocupa.</summary>
    public const int CamposDoCabecalho = 4;

    /// <summary>
    /// Monta a lista de campos de texto de uma tabela.
    /// </summary>
    /// <param name="fieldsPerItem">
    /// Quantos campos cada item ocupa. Conferido item a item: quando escrita e
    /// leitura discordam disso, a tabela inteira volta como ilegível, e o
    /// índice se perde. Era um contrato que morava em dois lugares soltos.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Se algum item produzir um número de campos diferente do combinado.
    /// </exception>
    public static IReadOnlyList<string> Write<T>(
        int version,
        int fieldsPerItem,
        IReadOnlyList<T> items,
        Func<T, IReadOnlyList<string>> fields)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldsPerItem, 1);

        var texto = new List<string>(CamposDoCabecalho + items.Count * fieldsPerItem)
        {
            CampoVersao,
            version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CampoQuantidade,
            items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        foreach (var item in items)
        {
            var campos = fields(item);

            if (campos is null || campos.Count != fieldsPerItem)
            {
                throw new InvalidOperationException(
                    $"A tabela grava {fieldsPerItem} campo(s) por item, e um item produziu "
                    + $"{campos?.Count ?? 0}.");
            }

            foreach (var campo in campos) texto.Add(campo ?? string.Empty);
        }

        return texto;
    }

    /// <summary>
    /// Lê a lista de campos de texto de volta.
    /// </summary>
    /// <param name="build">
    /// Monta o item a partir dos campos dele, ou devolve null se eles não
    /// descrevem um item utilizável. Null conta como entrada ilegível, e
    /// aparece no problema.
    /// </param>
    /// <param name="whatItIs">
    /// Como chamar a tabela nas mensagens: "de áreas", "de alinhamentos".
    /// </param>
    public static RecordTableResult<T> Read<T>(
        IReadOnlyList<string>? texto,
        int version,
        int fieldsPerItem,
        Func<IReadOnlyList<string>, T?> build,
        string whatItIs)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldsPerItem, 1);

        if (texto is null) return new RecordTableResult<T>([], null);

        if (texto.Count < CamposDoCabecalho)
            return Perdido<T>($"o registro {whatItIs} está truncado");

        if (Campo(texto, 0) != CampoVersao)
            return Perdido<T>($"o registro {whatItIs} não tem o cabeçalho esperado");

        if (!int.TryParse(
                Campo(texto, 1),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var gravada)
            || gravada != version)
        {
            return Perdido<T>(
                $"o registro {whatItIs} foi gravado por outra versão do plugin "
                + $"(formato {Campo(texto, 1)}, esperado {version})");
        }

        // O cabeçalho é de tamanho fixo: se o terceiro campo não for a marca
        // de quantidade, a tabela não é o que diz ser. Antes isso passava
        // calado e a conferência de quantidade era simplesmente pulada — ou
        // seja, justamente a garantia contra truncamento sumia quando o
        // cabeçalho estava corrompido.
        if (Campo(texto, 2) != CampoQuantidade)
            return Perdido<T>($"o registro {whatItIs} não tem o cabeçalho esperado");

        var itens = new List<T>();
        var ilegiveis = 0;

        for (var i = CamposDoCabecalho; i + fieldsPerItem - 1 < texto.Count; i += fieldsPerItem)
        {
            var pedaco = new string[fieldsPerItem];

            for (var j = 0; j < fieldsPerItem; j++) pedaco[j] = Campo(texto, i + j);

            var item = build(pedaco);

            if (item is null) ilegiveis++;
            else itens.Add(item);
        }

        if (ilegiveis > 0)
        {
            return Perdido(
                $"{ilegiveis} entrada(s) do registro {whatItIs} não puderam ser lidas", itens);
        }

        // A quantidade declarada no cabeçalho existe exatamente para isto: sem
        // conferi-la, um registro cortado pela metade devolveria os itens que
        // sobraram sem ninguém notar a falta dos outros.
        if (int.TryParse(
                Campo(texto, 3),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var declarados)
            && declarados != itens.Count)
        {
            return Perdido(
                $"o registro {whatItIs} diz ter {declarados} item(ns) e só {itens.Count} "
                + "foram lidos",
                itens);
        }

        return new RecordTableResult<T>(itens, null);
    }

    private static RecordTableResult<T> Perdido<T>(string motivo, IReadOnlyList<T>? itens = null) =>
        new(itens ?? [], motivo);

    private static string Campo(IReadOnlyList<string> texto, int posicao) =>
        posicao < texto.Count ? texto[posicao] ?? string.Empty : string.Empty;
}
