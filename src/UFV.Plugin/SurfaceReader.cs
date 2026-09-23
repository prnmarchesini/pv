using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Uma superfície do desenho e o resumo que o usuário vê dela.
/// </summary>
/// <param name="Id">
/// Quem ela é dentro do desenho. É o que faz a escolha do usuário apontar para
/// alguma coisa: duas superfícies podem ter o mesmo nome e o mesmo número de
/// pontos, e aí só o identificador as distingue.
/// </param>
/// <param name="Summary">O que aparece na lista de escolha.</param>
internal sealed record SurfaceEntry(ObjectId Id, SurfaceSummary Summary);

/// <summary>
/// O que a leitura do desenho encontrou.
/// </summary>
/// <param name="Surfaces">As superfícies TIN, na ordem do banco.</param>
/// <param name="HasExternalReference">
/// Se o desenho tem referência externa. Importa quando não se acha superfície
/// nenhuma: a topografia costuma vir por XRef, e a superfície de dentro de uma
/// XRef não pertence a este desenho.
/// </param>
internal sealed record SurfaceScan(IReadOnlyList<SurfaceEntry> Surfaces, bool HasExternalReference);

/// <summary>
/// Lê do desenho as superfícies TIN que podem ser o terreno.
///
/// Faz parte da casca fina (02-arquitetura.md): traduz o que o Civil 3D tem
/// para os objetos simples do motor, e nada mais. Nenhuma decisão sobre o que
/// mostrar ou em que ordem mora aqui — isso é <see cref="SurfaceCatalog"/>.
/// </summary>
internal static class SurfaceReader
{
    /// <summary>
    /// Todas as superfícies TIN do desenho, na ordem em que o Civil 3D as
    /// devolve. Quem ordena é o catálogo.
    ///
    /// A lista vem de <c>GetSurfaceIds</c>, que é o caminho oficial e enxerga
    /// o documento inteiro. Varrer o ModelSpace à mão, como esta classe fazia
    /// antes, perdia em silêncio a superfície que estivesse em PaperSpace ou
    /// dentro de um bloco.
    ///
    /// Só TIN: superfície de grade e de volume existem no Civil 3D, mas não
    /// são terreno levantado, e o motor não sabe o que fazer com elas.
    /// </summary>
    internal static SurfaceScan Read(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var encontradas = new List<SurfaceEntry>();

        // Uma transação para a leitura inteira. Abrir uma por superfície faria
        // o AutoCAD engasgar num desenho com muitas, e a leitura termina antes
        // de qualquer tela aparecer: diálogo aberto com transação viva é
        // pedido de travamento.
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (ObjectId id in IdsDeSuperficie())
        {
            if (!EhTinSurface(id)) continue;

            // O GetObject fica depois do filtro de classe: abrir todo objeto
            // do desenho só para descobrir o tipo é caro, e um único objeto
            // ilegível — proxy sem object enabler, entidade corrompida —
            // derrubaria a leitura inteira e o usuário perderia a lista toda.
            TinSurface? superficie;
            try
            {
                superficie = transacao.GetObject(id, OpenMode.ForRead) as TinSurface;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui abrir uma superfície do desenho.", erro);
                continue;
            }

            if (superficie is null) continue;

            encontradas.Add(new SurfaceEntry(id, Resumir(superficie)));
        }

        var temXref = TemReferenciaExterna(database, transacao);

        transacao.Commit();
        return new SurfaceScan(encontradas, temXref);
    }

    private static ObjectIdCollection IdsDeSuperficie()
    {
        try
        {
            return CivilApplication.ActiveDocument.GetSurfaceIds();
        }
        catch (System.Exception erro)
        {
            // Acontece quando o documento ativo não é um documento do Civil 3D.
            RegistroDeDiagnostico.Registrar("Não consegui pedir as superfícies ao Civil 3D.", erro);
            return [];
        }
    }

    private static bool EhTinSurface(ObjectId id)
    {
        if (id.IsNull || id.IsErased) return false;

        // Comparar a classe registrada é o teste barato: não abre o objeto.
        return id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(TinSurface)));
    }

    /// <summary>
    /// Se o desenho tem alguma referência externa. Só é perguntado para poder
    /// explicar melhor a ausência de superfícies.
    /// </summary>
    private static bool TemReferenciaExterna(Database database, Transaction transacao)
    {
        try
        {
            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId id in tabela)
            {
                var bloco = (BlockTableRecord)transacao.GetObject(id, OpenMode.ForRead);
                if (bloco.IsFromExternalReference) return true;
            }

            return false;
        }
        catch (System.Exception erro)
        {
            // Sem isto a mensagem fica menos precisa, e só.
            RegistroDeDiagnostico.Registrar("Não consegui conferir as referências externas.", erro);
            return false;
        }
    }

    private static SurfaceSummary Resumir(TinSurface superficie) =>
        new(LerNome(superficie), LerQuantidadeDePontos(superficie));

    private static string LerNome(TinSurface superficie)
    {
        try
        {
            return superficie.Name;
        }
        catch (System.Exception erro)
        {
            // Superfície com definição quebrada some da lista se isto subir, e
            // sumir em silêncio é pior que aparecer sem nome.
            RegistroDeDiagnostico.Registrar("Não consegui ler o nome de uma superfície.", erro);
            return string.Empty;
        }
    }

    private static int LerQuantidadeDePontos(TinSurface superficie)
    {
        try
        {
            // NumberOfPoints mora nas propriedades gerais; as de TIN trazem
            // triângulos e comprimentos de aresta, que interessam ao passo 1.4.
            return superficie.GetGeneralProperties().NumberOfPoints;
        }
        catch (System.Exception erro)
        {
            // Acontece com superfície fora de data ou com definição corrompida.
            // Zero aqui significa "não serve de terreno", que é a decisão certa
            // quando não dá para saber.
            RegistroDeDiagnostico.Registrar("Não consegui ler a quantidade de pontos de uma superfície.", erro);
            return 0;
        }
    }
}
