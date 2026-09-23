using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Uma área registrada no desenho.
/// </summary>
/// <param name="Identity">Quem ela é.</param>
/// <param name="Handle">Onde ela está neste arquivo.</param>
internal sealed record AreaRecord(AreaIdentity Identity, string Handle);

/// <summary>
/// O que a leitura do registro encontrou.
/// </summary>
/// <param name="Areas">As áreas que deu para ler.</param>
/// <param name="Problema">
/// O que estava errado no registro, em português, ou null se estava inteiro.
///
/// Existe porque um registro ilegível era descartado em silêncio, e a gravação
/// seguinte o substituía por uma lista quase vazia. Perder o índice não é grave
/// — o XData é a verdade, e o UFV_REINDEXAR o reconstrói —, mas o usuário
/// precisa saber que ficou um reindexar para rodar.
/// </param>
internal sealed record RegistroDeAreas(IReadOnlyList<AreaRecord> Areas, string? Problema);

/// <summary>
/// O registro central das áreas do desenho.
///
/// Existe para o plugin saber o que existe sem varrer o desenho inteiro: um
/// arquivo de projeto tem milhares de entidades, e abrir todas para perguntar
/// "você é nossa?" a cada comando seria caro demais.
///
/// O registro é um índice, não a verdade. A verdade é o XData de cada
/// entidade, que viaja com ela. Quando os dois discordam — e discordam, porque
/// o registro não viaja na cópia entre desenhos — quem manda é o XData, e o
/// reindexar do passo 2.4 reconstrói o registro a partir dele.
/// </summary>
internal static class AreaStore
{
    private const string Chave = "AREAS";

    private const string CampoVersaoDoFormato = "FORMATO";
    private const string CampoQuantidade = "QUANTIDADE";
    private const int VersaoDoFormato = 1;

    /// <summary>As áreas registradas, na ordem em que foram gravadas.</summary>
    internal static IReadOnlyList<AreaRecord> Load(Database database) => Ler(database).Areas;

    /// <summary>
    /// Lê o registro e diz o que encontrou de errado nele.
    ///
    /// Nenhum descarte é silencioso: registro truncado, de outra versão, ou com
    /// entrada ilegível deixa rastro no log e devolve um
    /// <see cref="RegistroDeAreas.Problema"/> para quem tem editor avisar.
    /// </summary>
    internal static RegistroDeAreas Ler(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);

        using var dados = PluginDictionary.Load(database, Chave);
        if (dados is null) return new RegistroDeAreas([], null);

        try
        {
            var campos = dados.AsArray();
            var areas = new List<AreaRecord>();

            // Cabeçalho: versão e quantidade. Depois, quatro valores por área.
            if (campos.Length < 4) return Perdido("o registro de áreas está truncado");

            if (LerTexto(campos, 0) != CampoVersaoDoFormato)
                return Perdido("o registro de áreas não tem o cabeçalho esperado");

            if (!int.TryParse(LerTexto(campos, 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var versao)
                || versao != VersaoDoFormato)
            {
                return Perdido(
                    "o registro de áreas foi gravado por outra versão do plugin "
                    + $"(formato {LerTexto(campos, 1)}, esperado {VersaoDoFormato})");
            }

            var ilegiveis = 0;

            for (var i = 4; i + 3 < campos.Length; i += 4)
            {
                if (!Guid.TryParse(LerTexto(campos, i), out var id))
                {
                    ilegiveis++;
                    continue;
                }

                var nome = LerTexto(campos, i + 1);
                var handle = LerTexto(campos, i + 2);

                if (!DateTime.TryParse(
                        LerTexto(campos, i + 3),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var criadaEm))
                {
                    ilegiveis++;
                    continue;
                }

                var identidade = new AreaIdentity(id, nome, criadaEm);

                if (identidade.IsValid) areas.Add(new AreaRecord(identidade, handle));
                else ilegiveis++;
            }

            if (ilegiveis > 0)
            {
                return Perdido(
                    $"{ilegiveis} entrada(s) do registro de áreas não puderam ser lidas", areas);
            }

            // A quantidade declarada no cabeçalho existe exatamente para isto:
            // sem conferi-la, um registro cortado pela metade devolveria as
            // áreas que sobraram sem ninguém notar a falta das outras.
            if (LerTexto(campos, 2) == CampoQuantidade
                && int.TryParse(LerTexto(campos, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaradas)
                && declaradas != areas.Count)
            {
                return Perdido(
                    $"o registro de áreas diz ter {declaradas} área(s) e só {areas.Count} foram lidas",
                    areas);
            }

            return new RegistroDeAreas(areas, null);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler o registro de áreas do desenho.", erro);
            return Perdido("o registro de áreas está ilegível");
        }
    }

    private static RegistroDeAreas Perdido(string motivo, IReadOnlyList<AreaRecord>? areas = null)
    {
        RegistroDeDiagnostico.Registrar($"Registro de áreas com problema: {motivo}.");
        return new RegistroDeAreas(areas ?? [], motivo);
    }

    /// <summary>Grava a lista inteira, substituindo a anterior.</summary>
    internal static void Save(Database database, IReadOnlyList<AreaRecord> areas)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(areas);

        var buffer = new ResultBuffer();

        void Texto(string valor) =>
            buffer.Add(new TypedValue((int)DxfCode.Text, valor ?? string.Empty));

        Texto(CampoVersaoDoFormato);
        Texto(VersaoDoFormato.ToString(CultureInfo.InvariantCulture));
        Texto(CampoQuantidade);
        Texto(areas.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var area in areas)
        {
            Texto(area.Identity.Id.ToString("D"));
            Texto(area.Identity.Name);
            Texto(area.Handle);
            Texto(area.Identity.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        PluginDictionary.Save(database, Chave, buffer);
    }

    /// <summary>
    /// Acrescenta ou atualiza uma área no registro, pelo identificador dela.
    /// </summary>
    /// <param name="problema">
    /// O que havia de errado no registro anterior, ou null. A gravação acontece
    /// de qualquer jeito — recusar deixaria a área recém-criada fora do índice
    /// —, mas quem chama precisa contar ao usuário que ficou um reindexar para
    /// rodar.
    /// </param>
    internal static void Upsert(Database database, AreaRecord area, out string? problema)
    {
        ArgumentNullException.ThrowIfNull(area);

        var registro = Ler(database);
        problema = registro.Problema;

        var atuais = registro.Areas.ToList();
        var posicao = atuais.FindIndex(a => a.Identity.Id == area.Identity.Id);

        if (posicao >= 0) atuais[posicao] = area;
        else atuais.Add(area);

        Save(database, atuais);
    }

    private static string LerTexto(TypedValue[] campos, int posicao) =>
        posicao < campos.Length ? campos[posicao].Value as string ?? string.Empty : string.Empty;
}
