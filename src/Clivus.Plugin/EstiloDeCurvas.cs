using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O estilo de superfície com curvas de nível que o Clivus dá às superfícies
/// que ele cria (28/10/2026: a área aberta do TermoVision chegava sem curvas,
/// porque a TIN nascia com o estilo padrão do desenho, que num desenho fora do
/// modelo do Civil mostra só a borda). Mostra borda, curva mestra e
/// secundária; esconde pontos e triângulos. Um estilo por par de intervalos,
/// criado na primeira vez e reaproveitado depois.
/// </summary>
internal static class EstiloDeCurvas
{
    /// <summary>O estilo "CLIVUS Curvas {secundária} e {mestra} m", criado se faltar.</summary>
    internal static ObjectId Garantir(Database database, Transaction transacao, double secundaria, double mestra)
    {
        var nome = string.Format(CultureInfo.InvariantCulture, "CLIVUS Curvas {0:0.##} e {1:0.##} m", secundaria, mestra);
        var estilos = CivilDocument.GetCivilDocument(database).Styles.SurfaceStyles;
        if (estilos.Contains(nome)) return estilos[nome];

        var id = estilos.Add(nome);
        var estilo = (SurfaceStyle)transacao.GetObject(id, OpenMode.ForWrite);
        estilo.ContourStyle.MinorContourInterval = secundaria;
        estilo.ContourStyle.MajorContourInterval = mestra;

        foreach (SurfaceDisplayStyleType tipo in Enum.GetValues(typeof(SurfaceDisplayStyleType)))
        {
            var ver = tipo is SurfaceDisplayStyleType.Boundary or SurfaceDisplayStyleType.MajorContour or SurfaceDisplayStyleType.MinorContour;
            estilo.GetDisplayStylePlan(tipo).Visible = ver;
            estilo.GetDisplayStyleModel(tipo).Visible = ver;
        }

        return id;
    }
}
