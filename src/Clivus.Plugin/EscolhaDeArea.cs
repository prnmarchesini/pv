using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// Que área o comando usa (Usina, Fileira, Refazer, Apagar tudo, Conforme
/// objetos, a janela Áreas). Renan, 30/10/2026: "eu to clicando na área, e a
/// ferramenta 'conforme objetos' não reconhece a área [...] tinha VÁRIAS
/// linhas sobrepostas e aí buga a ferramenta [...] isso deveria ser
/// percebido pela ferramenta".
/// <para>
/// As áreas vêm do desenho, pelo XData (<see cref="AreasDoDesenho.Varrer"/>),
/// nunca do registro: o registro com área apagada à mão contava áreas que não
/// existem. A ordem: a área da seleção implícita (o botão direito, a janela
/// Áreas); a única do desenho; ou o clique, na linha dela ou dentro dela. O
/// clique que cai noutra linha por cima (a do cliente, uma cópia) vale a área
/// cuja borda passa mais perto, até a folga; longe de toda borda, a menor
/// área que contém o ponto. Duas áreas com o mesmo contorno (uma em cima da
/// outra) são avisadas, e vale a do registro.
/// </para>
/// </summary>
internal static class EscolhaDeArea
{
    /// <summary>Até isto do contorno, em planta, duas áreas são a mesma linha.</summary>
    internal const double MesmaLinha = 0.05;

    /// <summary>Quanto tempo o pedido da janela Áreas vale (o comando corre logo depois do clique).</summary>
    private static readonly TimeSpan ValidadeDoPedido = TimeSpan.FromSeconds(15);

    /// <summary>A área pedida pela janela Áreas para o próximo comando, e quando.</summary>
    private static (ObjectId Id, DateTime Quando)? _pedida;

    /// <summary>
    /// A janela Áreas escolhe a área do comando que ela manda para a linha de
    /// comando (Apagar, Limpar, Regerar, Recalcular). Pela seleção implícita
    /// não dá: os ESC que cancelam um comando no meio a apagam. Vale uma vez,
    /// por pouco tempo: um comando que falhou antes de ler o pedido não deixa
    /// a área valendo para o próximo que a pessoa digitar.
    /// </summary>
    internal static void PedirPelaJanela(ObjectId area) => _pedida = (area, DateTime.UtcNow);

    private static AreaNoDesenho? DaJanela(IReadOnlyList<AreaNoDesenho> areas)
    {
        if (_pedida is not { } pedida) return null;

        _pedida = null;
        if (DateTime.UtcNow - pedida.Quando > ValidadeDoPedido) return null;

        return areas.FirstOrDefault(a => a.Id == pedida.Id);
    }

    /// <summary>A área da seleção implícita (sem tirar a seleção se não houver área nela), ou null.</summary>
    internal static AreaNoDesenho? DaSelecao(Editor editor, IReadOnlyList<AreaNoDesenho> areas)
    {
        var implicita = editor.SelectImplied();
        if (implicita.Status != PromptStatus.OK) return null;

        var ids = implicita.Value.GetObjectIds().ToHashSet();
        if (areas.FirstOrDefault(a => ids.Contains(a.Id)) is not { } area) return null;

        editor.SetImpliedSelection([]);
        return area;
    }

    /// <summary>
    /// A área do comando: da seleção, a única, ou a do clique. Null se não há
    /// área no desenho ou a pessoa desistiu (a mensagem já foi escrita).
    /// </summary>
    internal static AreaNoDesenho? Pedir(Editor editor, Document documento)
    {
        var areas = AreasDoDesenho.Varrer(documento.Database);

        if (areas.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nNenhuma área neste desenho. Use o botão Área primeiro.\n"));
            return null;
        }

        var escolhida = DaJanela(areas) ?? DaSelecao(editor, areas);

        if (escolhida is null && Distintas(areas).Count == 1)
            escolhida = areas.OrderByDescending(a => a.Registrada).First();

        while (escolhida is null)
        {
            var opcoes = new PromptPointOptions(Tr.T("\nClique na área de implantação (na linha dela ou dentro dela): ")) { AllowNone = false };
            var clique = editor.GetPoint(opcoes);
            if (clique.Status != PromptStatus.OK) return null;

            escolhida = PeloPonto(areas, clique.Value.X, clique.Value.Y, Folga());
            if (escolhida is null) editor.WriteMessage(Tr.T("\n  Esse ponto não está numa área nem perto da linha de uma. Clique de novo.\n"));
        }

        AvisarRepetidas(editor, areas, escolhida);
        editor.WriteMessage(Tr.F("\nÁrea: {0}.\n", escolhida.Identity.DisplayName));
        return escolhida;
    }

    /// <summary>
    /// A área do ponto: a de borda mais perto, até a folga; senão a menor que
    /// contém o ponto. Entre as repetidas (mesmo contorno), a do registro.
    /// </summary>
    internal static AreaNoDesenho? PeloPonto(IReadOnlyList<AreaNoDesenho> areas, double x, double y, double folga)
    {
        var validas = areas.Where(a => a.Contorno.Count >= 3).ToList();

        var perto = validas
            .Select(a => (Area: a, Distancia: Polygons.DistanceToEdges(a.Contorno, x, y)))
            .Where(c => c.Distancia <= folga)
            .OrderBy(c => c.Distancia)
            .Select(c => c.Area)
            .FirstOrDefault();

        var achada = perto ?? validas
            .Where(a => Polygons.Contains(a.Contorno, x, y))
            .OrderBy(a => InverterSites.PlanArea(a.Contorno))
            .FirstOrDefault();

        if (achada is null) return null;

        // Das repetidas, a que o registro conhece (o Regerar acha ela).
        return Repetidas(validas, achada).OrderByDescending(a => a.Registrada).First();
    }

    /// <summary>Se as duas áreas são a mesma linha em planta.</summary>
    internal static bool MesmoContorno(AreaNoDesenho a, AreaNoDesenho b) =>
        a.Contorno.Count >= 3 && b.Contorno.Count >= 3 && Polygons.SameOutline(a.Contorno, b.Contorno, MesmaLinha);

    /// <summary>A área e as que têm o mesmo contorno que ela (ela inclusive).</summary>
    internal static List<AreaNoDesenho> Repetidas(IReadOnlyList<AreaNoDesenho> areas, AreaNoDesenho area) =>
        areas.Where(a => a.Id == area.Id || MesmoContorno(a, area)).ToList();

    /// <summary>As áreas sem as repetidas (uma por contorno).</summary>
    internal static List<AreaNoDesenho> Distintas(IReadOnlyList<AreaNoDesenho> areas)
    {
        var distintas = new List<AreaNoDesenho>();

        foreach (var a in areas)
            if (!distintas.Any(d => MesmoContorno(d, a))) distintas.Add(a);

        return distintas;
    }

    private static void AvisarRepetidas(Editor editor, IReadOnlyList<AreaNoDesenho> areas, AreaNoDesenho area)
    {
        var repetidas = Repetidas(areas, area);
        if (repetidas.Count < 2) return;

        editor.WriteMessage(Tr.F(
            "\n  ATENÇÃO: há {0} áreas com este mesmo contorno, uma em cima da outra ({1}). Usei {2}; apague as que sobram na janela Áreas (botão Apagar área).\n",
            repetidas.Count, string.Join(", ", repetidas.Select(a => a.Identity.DisplayName)), area.Identity.DisplayName));
    }

    /// <summary>Quanto o clique pode ficar longe da linha: 15 pixels na tela, nunca menos de 0,5 m.</summary>
    private static double Folga()
    {
        try
        {
            return Math.Max(0.5, RastreioDoPerfil.Pixels(15));
        }
        catch (System.Exception)
        {
            return 2.0;
        }
    }
}
