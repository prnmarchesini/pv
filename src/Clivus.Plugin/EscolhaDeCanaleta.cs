using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>O que o usuário respondeu: a linha clicada (e o ponto do clique), uma palavra-chave, ou nada.</summary>
internal readonly record struct LinhaEscolhida(PromptStatus Status, ObjectId Id, Point3d Ponto, string Palavra);

/// <summary>
/// Escolhe uma linha ou canaleta da drenagem pelo clique num ponto, com a
/// cruz. O quadradinho de escolher objeto pegava o 3D e a pintura que ficam
/// em cima da canaleta e respondia "isso não é uma canaleta" (tela do Renan,
/// 21/10/2026). Aqui vale a linha mais perto do clique, em planta; passando o
/// mouse, o nome dela aparece ao lado da cruz.
/// </summary>
internal static class EscolhaDeCanaleta
{
    /// <summary>Até quantos pixels do clique a linha conta.</summary>
    private const double Alcance = 30;

    internal static LinhaEscolhida Pedir(Document documento, string mensagem, bool soCanaleta, params string[] palavras)
    {
        var editor = documento.Editor;

        List<LinhaNoDesenho> linhas;
        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            linhas = LinhasDeDrenagem.Todas(transacao, documento.Database).Where(l => !soCanaleta || l.Identidade.IsChannel).ToList();
        }

        void AoMover(object? _, PointMonitorEventArgs e)
        {
            try
            {
                var p = e.Context.ComputedPoint;
                if (MaisPerto(linhas, p.X, p.Y, RastreioDoPerfil.Pixels(Alcance)) is { } l) e.AppendToolTipText(l.Identidade.Name);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao mostrar a canaleta perto do mouse.", erro);
            }
        }

        editor.PointMonitor += AoMover;

        try
        {
            while (true)
            {
                var opcoes = new PromptPointOptions(mensagem) { AllowNone = true };
                foreach (var k in palavras) opcoes.Keywords.Add(k);
                opcoes.AppendKeywordsToMessage = palavras.Length > 0;

                var r = editor.GetPoint(opcoes);
                if (r.Status == PromptStatus.Keyword) return new LinhaEscolhida(r.Status, ObjectId.Null, default, r.StringResult);
                if (r.Status != PromptStatus.OK) return new LinhaEscolhida(r.Status, ObjectId.Null, default, string.Empty);

                var clique = r.Value.TransformBy(editor.CurrentUserCoordinateSystem);
                if (MaisPerto(linhas, clique.X, clique.Y, RastreioDoPerfil.Pixels(Alcance)) is { } linha)
                    return new LinhaEscolhida(PromptStatus.OK, linha.Id, clique, string.Empty);

                editor.WriteMessage(soCanaleta ? Tr.T("\nNenhuma canaleta perto do clique.\n") : Tr.T("\nNenhuma linha ou canaleta perto do clique.\n"));
            }
        }
        finally
        {
            editor.PointMonitor -= AoMover;
        }
    }

    /// <summary>A linha com algum trecho mais perto do ponto, em planta, até a tolerância.</summary>
    private static LinhaNoDesenho? MaisPerto(IEnumerable<LinhaNoDesenho> linhas, double x, double y, double tolerancia)
    {
        LinhaNoDesenho? melhor = null;
        var menor = tolerancia;

        foreach (var l in linhas)
        {
            for (var i = 1; i < l.Vertices.Count; i++)
            {
                var d = DistanciaAoTrecho(x, y, l.Vertices[i - 1].X, l.Vertices[i - 1].Y, l.Vertices[i].X, l.Vertices[i].Y);
                if (d > menor) continue;
                (menor, melhor) = (d, l);
            }
        }

        return melhor;
    }

    private static double DistanciaAoTrecho(double x, double y, double ax, double ay, double bx, double by)
    {
        var (dx, dy) = (bx - ax, by - ay);
        var l2 = dx * dx + dy * dy;
        var t = l2 > 0 ? Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1) : 0;
        var (px, py) = (ax + t * dx - x, ay + t * dy - y);
        return Math.Sqrt(px * px + py * py);
    }
}
