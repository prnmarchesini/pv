using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Aplica os estilos do projeto (passo 8.13) aos textos que o plugin cria:
/// o estilo de texto escolhido e, se ele é anotativo, o texto anotativo, na
/// altura de papel do estilo pela escala de anotação corrente.
/// </summary>
internal static class EstiloDoProjeto
{
    internal static ProjectStyles Ler(Database database)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, ProjectStyles.StorageKey);
            var campos = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            return ProjectStyles.Decode(campos);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler os estilos do projeto.", erro);
            return ProjectStyles.None;
        }
    }

    internal static void Gravar(Database database, ProjectStyles estilos) =>
        PluginDictionary.Save(database, ProjectStyles.StorageKey, new ResultBuffer(
            estilos.Encode().Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));

    /// <summary>
    /// Os estilos que valem: os escolhidos; sem escolha gravada, os do Renan
    /// (<see cref="ProjectStyles.Marcheng"/>) que o desenho tiver.
    /// </summary>
    internal static ProjectStyles Efetivo(Transaction transacao, Database database)
    {
        if (PluginDictionary.Contains(database, ProjectStyles.StorageKey)) return Ler(database);

        return new ProjectStyles(
            ProjectStyles.Match(ProjectStyles.Marcheng.TextStyle, EstilosDeTexto(transacao, database)),
            ProjectStyles.Match(ProjectStyles.Marcheng.DimensionStyle, EstilosDeCota(transacao, database)),
            ProjectStyles.Match(ProjectStyles.Marcheng.LeaderStyle, EstilosDeChamada(transacao, database)));
    }

    /// <summary>Os nomes dos estilos de texto do desenho.</summary>
    internal static List<string> EstilosDeTexto(Transaction transacao, Database database) =>
        Nomes<TextStyleTableRecord>(transacao, database.TextStyleTableId).Where(n => n.Length > 0).ToList();

    /// <summary>Os nomes dos estilos de cota do desenho.</summary>
    internal static List<string> EstilosDeCota(Transaction transacao, Database database) =>
        Nomes<DimStyleTableRecord>(transacao, database.DimStyleTableId);

    /// <summary>Os nomes dos estilos de chamada (multileader) do desenho.</summary>
    internal static List<string> EstilosDeChamada(Transaction transacao, Database database)
    {
        var dicionario = (DBDictionary)transacao.GetObject(database.MLeaderStyleDictionaryId, OpenMode.ForRead);
        var nomes = new List<string>();

        // foreach tipado: o enumerador do DBDictionary não serve ao Cast<>.
        foreach (DBDictionaryEntry entrada in dicionario) nomes.Add(entrada.Key);

        return nomes;
    }

    /// <summary>
    /// O que põe nos textos o estilo do projeto, resolvido uma vez para a
    /// operação inteira (ler o registro a cada texto custaria caro numa usina
    /// grande). Sem estilo escolhido, ou se ele não existe no desenho, o
    /// texto fica como está, com a altura de reserva que já tem.
    ///
    /// Chamar com o texto JÁ no desenho (depois do AppendEntity): o texto
    /// anotativo precisa do banco para ganhar a escala de anotação corrente.
    /// </summary>
    internal static Action<MText> PrepararTexto(Transaction transacao, Database database)
    {
        var escolhido = Efetivo(transacao, database).TextStyle;
        if (escolhido is null) return _ => { };

        var tabela = (TextStyleTable)transacao.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        var nome = ProjectStyles.Match(escolhido, EstilosDeTexto(transacao, database));
        if (nome is null || !tabela.Has(nome)) return _ => { };

        var estilo = (TextStyleTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);
        var escala = database.Cannoscale?.Scale ?? 1;

        return texto => Aplicar(texto, estilo, escala);
    }

    private static void Aplicar(MText texto, TextStyleTableRecord estilo, double escala)
    {
        texto.TextStyleId = estilo.ObjectId;

        if (estilo.Annotative == AnnotativeStates.True)
        {
            texto.Annotative = AnnotativeStates.True;

            // Altura de papel do estilo pelo fator da escala corrente (1:500
            // → 0,002): é a altura no modelo que sai na prancha certa.
            if (estilo.TextSize > 0 && escala > 0) texto.TextHeight = estilo.TextSize / escala;
        }
        else if (estilo.TextSize > 0)
        {
            texto.TextHeight = estilo.TextSize;
        }
    }

    private static List<string> Nomes<T>(Transaction transacao, ObjectId tabelaId) where T : SymbolTableRecord
    {
        var tabela = (SymbolTable)transacao.GetObject(tabelaId, OpenMode.ForRead);
        var nomes = new List<string>();

        foreach (ObjectId id in tabela)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is T registro) nomes.Add(registro.Name);
        }

        return nomes;
    }
}
