using System.Globalization;

namespace Clivus.Core;

/// <summary>Uma mesa cadastrada no desenho: o perfil, a cor com que ela aparece e se entra nesta usina.</summary>
public sealed record DrawingTable(TableProfile Profile, RgbColor Color, bool Use)
{
    /// <summary>O nome da mesa (o do perfil, aparado).</summary>
    public string Name => Profile.Name.Trim();
}

/// <summary>
/// As mesas cadastradas NO DESENHO (passo 8.5, Melhorias.docx, 01/10/2026:
/// "quero que nesse menu apareça a lista de mesas cadastradas NO DESENHO, e
/// aí eu clico em quais quero usar; se desse para usar cores diferentes
/// nelas, aí eu saberia qual é qual. É assim que o PVcase faz").
///
/// Os perfis de <c>%LOCALAPPDATA%</c> continuam sendo a biblioteca de onde
/// se traz uma mesa para o desenho; o que vale para a usina é o que está no
/// desenho.
///
/// Gravado no dicionário do desenho como texto: versão, quantas, e por mesa
/// a cor, o "usar" e o perfil em JSON partido em pedaços (um valor de texto
/// do Xrecord não comporta um perfil inteiro com folga).
/// </summary>
public static class DrawingTables
{
    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public const string StorageKey = "MESAS";

    private const string Versao = "1";

    /// <summary>Tamanho de cada pedaço do JSON.</summary>
    private const int Pedaco = 200;

    /// <summary>
    /// As cores que as mesas recebem ao entrar, na ordem: bem distintas umas
    /// das outras e de vermelho/azul (as das análises) e magenta (não cabe).
    /// </summary>
    public static readonly RgbColor[] Palette =
    [
        new(0, 160, 0),
        new(255, 128, 0),
        new(0, 200, 200),
        new(128, 0, 200),
        new(255, 255, 0),
        new(150, 90, 40),
        new(255, 105, 180),
        new(120, 120, 120),
    ];

    /// <summary>Para o registro.</summary>
    public static IReadOnlyList<string> Encode(IReadOnlyList<DrawingTable> mesas)
    {
        ArgumentNullException.ThrowIfNull(mesas);

        var campos = new List<string> { Versao, mesas.Count.ToString(CultureInfo.InvariantCulture) };

        foreach (var mesa in mesas)
        {
            var json = mesa.Profile.ToJson();
            var pedacos = Enumerable.Range(0, (json.Length + Pedaco - 1) / Pedaco)
                .Select(i => json.Substring(i * Pedaco, Math.Min(Pedaco, json.Length - i * Pedaco)))
                .ToList();

            campos.Add(mesa.Color.ToHex());
            campos.Add(mesa.Use ? "1" : "0");
            campos.Add(pedacos.Count.ToString(CultureInfo.InvariantCulture));
            campos.AddRange(pedacos);
        }

        return campos;
    }

    /// <summary>
    /// Do registro. Mesa ilegível fica de fora e entra em <paramref name="problemas"/>;
    /// registro de outra versão ou quebrado devolve a lista vazia com o motivo.
    /// </summary>
    public static IReadOnlyList<DrawingTable> Decode(IReadOnlyList<string>? campos, out IReadOnlyList<string> problemas)
    {
        var lista = new List<DrawingTable>();
        var erros = new List<string>();
        problemas = erros;

        if (campos is null || campos.Count == 0) return lista;

        if (campos[0] != Versao || campos.Count < 2 || !int.TryParse(campos[1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantas))
        {
            erros.Add(Tr.T("o registro das mesas do desenho é de outra versão ou está quebrado"));
            return lista;
        }

        var i = 2;

        for (var n = 0; n < quantas; n++)
        {
            if (i + 3 > campos.Count
                || !RgbColor.TryParseHex(campos[i], out var cor)
                || !int.TryParse(campos[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var pedacos)
                || i + 3 + pedacos > campos.Count)
            {
                erros.Add(Tr.F("a mesa {0} do registro está quebrada; as seguintes não puderam ser lidas", n + 1));
                return lista;
            }

            var usar = campos[i + 1] == "1";
            var json = string.Concat(campos.Skip(i + 3).Take(pedacos));
            i += 3 + pedacos;

            try
            {
                lista.Add(new DrawingTable(TableProfile.Parse(json), cor, usar));
            }
            catch (InvalidOperationException erro)
            {
                erros.Add(Tr.F("a mesa {0} do registro não pôde ser lida: {1}", n + 1, erro.Message));
            }
        }

        return lista;
    }

    /// <summary>Por que a lista não serve, ou null: nome em branco ou repetido.</summary>
    public static string? WhyInvalid(IReadOnlyList<DrawingTable> mesas)
    {
        ArgumentNullException.ThrowIfNull(mesas);

        if (mesas.Any(m => m.Name.Length == 0)) return Tr.T("há mesa sem nome");

        var repetido = mesas.GroupBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return repetido is null ? null : Tr.F("há duas mesas chamadas \"{0}\"", repetido.Key);
    }

    /// <summary>
    /// As mesas marcadas para a usina, na ordem da lista: é a prioridade
    /// (02/10/2026: "o primeiro da lista vai ser a prioridade").
    /// </summary>
    public static IReadOnlyList<DrawingTable> InUse(IEnumerable<DrawingTable> mesas) =>
        mesas.Where(m => m.Use).ToList();

    /// <summary>
    /// As mesas que o motor usa, na ordem da lista: as marcadas; sem
    /// nenhuma marcada, TODAS (02/10/2026, com print da lista 28, 14 sem
    /// marca e a usina toda de 14: "a prioridade são mesas de 28"). A
    /// lista é o que o usuário vê; o motor não pode ignorá-la por falta de
    /// um tique.
    /// </summary>
    public static IReadOnlyList<DrawingTable> ForEngine(IEnumerable<DrawingTable> mesas)
    {
        var lista = mesas.ToList();
        var marcadas = InUse(lista);
        return marcadas.Count > 0 ? marcadas : lista;
    }

    /// <summary>A primeira cor da paleta que nenhuma mesa usa ainda (se todas foram, recomeça).</summary>
    public static RgbColor NextColor(IEnumerable<DrawingTable> mesas)
    {
        var usadas = mesas.Select(m => m.Color).ToHashSet();
        return Palette.FirstOrDefault(c => !usadas.Contains(c), Palette[usadas.Count % Palette.Length]);
    }

    /// <summary>
    /// A mesa que volta da janela de Mesa aberta para uma mesa do desenho:
    /// a confirmada em "Usar esta mesa"; sem ela, a última gravada em
    /// "Salvar perfil" com o mesmo nome da mesa aberta; senão, null (nada
    /// muda).
    ///
    /// Origem (05/10/2026): o Renan abriu a mesa "Mesa 28 módulos" pelas
    /// Configurações, trocou para 56, clicou em "Salvar perfil", confirmou a
    /// substituição e fechou. O arquivo da biblioteca ficou com 56, mas a
    /// janela devolvia null por ter sido fechada, e a mesa DO DESENHO — que
    /// as Configurações listam antes da biblioteca e que esconde o perfil de
    /// mesmo nome — continuava com 28. Para ele, "não salva".
    ///
    /// Gravado com outro nome é cópia para a biblioteca, e não mexe na mesa
    /// aberta.
    /// </summary>
    public static TableProfile? AfterEdit(string? nomeAberto, TableProfile? confirmada, TableProfile? salva)
    {
        if (confirmada is not null) return confirmada;
        if (salva is null || string.IsNullOrWhiteSpace(nomeAberto)) return null;

        return string.Equals(salva.Name.Trim(), nomeAberto.Trim(), StringComparison.CurrentCultureIgnoreCase)
            ? salva
            : null;
    }

    /// <summary>A mesa com este nome (ignorando maiúscula e espaço), ou null.</summary>
    public static DrawingTable? Find(IEnumerable<DrawingTable> mesas, string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null
        : mesas.FirstOrDefault(m => string.Equals(m.Name, nome.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
