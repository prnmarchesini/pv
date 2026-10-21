using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// A memória de cálculo no desenho (passo 31): junta as canaletas (seção e
/// perfil), as bacias gravadas, os cruzamentos com via e os parâmetros, e
/// entrega as tabelas, o Excel e o "aplicar a altura sugerida".
/// </summary>
internal static class MemoriaDeDrenagem
{
    /// <summary>A memória das canaletas do desenho, recalculada agora.</summary>
    internal static DrainageMemoryResult Montar(Database database, Tin terreno)
    {
        var configuracao = DrenagemStore.Ler(database);
        var bacias = DrenagemStore.LerBacias(database);
        var entradas = new List<ChannelInput>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var vias = Vias(transacao, database, configuracao.Hydrology.RoadLayers);

            foreach (var linha in LinhasDeDrenagem.Todas(transacao, database).Where(l => l.Identidade.IsChannel).OrderBy(l => l.Identidade.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var (_, perfil) = LinhasDeDrenagem.Perfil(terreno, configuracao, linha);
                if (perfil is null || perfil.Points.Count < 2) continue;

                entradas.Add(new ChannelInput(linha.Identidade.Id, linha.Identidade.Name, linha.Identidade.Section!, perfil,
                    bacias?.Of(linha.Identidade.Id), Cruzamentos(linha.Vertices, vias).Count));
            }
        }

        return DrainageMemory.Build(configuracao.Hydrology, entradas);
    }

    /// <summary>Os traçados em planta das vias (as curvas nas camadas informadas, separadas por ";").</summary>
    internal static List<IReadOnlyList<Point3>> Vias(Transaction transacao, Database database, string camadas)
    {
        var nomes = camadas.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vias = new List<IReadOnlyList<Point3>>();
        if (nomes.Count == 0) return vias;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve c || !nomes.Contains(c.Layer)) continue;
            vias.Add(LinhaCommands.Caminho(database, id));
        }

        return vias;
    }

    /// <summary>Onde a canaleta cruza as vias, em planta, com a direção da canaleta ali.</summary>
    internal static List<(double X, double Y, double DirX, double DirY)> Cruzamentos(IReadOnlyList<Point3> canaleta, List<IReadOnlyList<Point3>> vias)
    {
        var pontos = new List<(double, double, double, double)>();

        for (var i = 1; i < canaleta.Count; i++)
        {
            var (a, b) = (canaleta[i - 1], canaleta[i]);

            foreach (var via in vias)
            {
                for (var j = 1; j < via.Count; j++)
                {
                    if (!Polygons.SegmentsCross(a, b, via[j - 1], via[j])) continue;

                    // O ponto do cruzamento, pela fração no trecho da canaleta.
                    var (c, d) = (via[j - 1], via[j]);
                    var den = (b.X - a.X) * (d.Y - c.Y) - (b.Y - a.Y) * (d.X - c.X);
                    var t = Math.Abs(den) < 1e-12 ? 0.5 : ((c.X - a.X) * (d.Y - c.Y) - (c.Y - a.Y) * (d.X - c.X)) / den;
                    var n = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    pontos.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, (b.X - a.X) / n, (b.Y - a.Y) / n));
                }
            }
        }

        return pontos;
    }

    /// <summary>Grava o Excel da memória (uma aba por tabela). A frase do relato.</summary>
    internal static string Exportar(Document documento, Tin terreno, string caminho)
    {
        if (!caminho.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) caminho += ".xlsx";

        var memoria = Montar(documento.Database, terreno);
        var planilha = new XlsxWriter();
        foreach (var tabela in DrainageMemory.Tables(memoria)) QuantityReport.Tabela(planilha, tabela);

        File.WriteAllBytes(caminho, planilha.ToBytes());

        return Tr.F("MEMÓRIA {0} canaleta(s), {1} alerta(s), gravada em {2}", memoria.Channels.Count, memoria.Alerts.Count(), caminho);
    }

    /// <summary>
    /// A altura sugerida pela memória vira a altura (ou o raio) da seção das
    /// canaletas escolhidas, e o perfil e o 3D são refeitos (31.5). É ação do
    /// projetista, nunca automática (regra 5).
    /// </summary>
    internal static string AplicarAltura(Document documento, Tin terreno, IReadOnlyCollection<ObjectId> escolhidas)
    {
        var memoria = Montar(documento.Database, terreno);
        var configuracao = DrenagemStore.Ler(documento.Database);
        var feitas = new List<string>();
        var semSugestao = new List<string>();

        using var transacao = documento.Database.TransactionManager.StartTransaction();
        var indice = IndiceDaDrenagem.Montar(transacao, documento.Database);

        foreach (var id in escolhidas)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d p || LinhasDeDrenagem.Identidade(p) is not { Section: { } secao } identidade) continue;

            var calculo = memoria.Channels.FirstOrDefault(c => c.Input.Id == identidade.Id);
            if (calculo is null || !double.IsFinite(calculo.SuggestedHeight))
            {
                semSugestao.Add(identidade.Name);
                continue;
            }

            var nova = secao.Kind == ChannelKind.HalfPipe
                ? secao with { Radius = calculo.SuggestedHeight }
                : secao with { Height = calculo.SuggestedHeight };

            var polilinha = (Polyline3d)transacao.GetObject(id, OpenMode.ForWrite, false, true);
            LinhasDeDrenagem.Gravar(transacao, polilinha, identidade with { Section = nova });
            LinhasDeDrenagem.Refazer(transacao, documento.Database, terreno, configuracao, id, indice);

            feitas.Add(Tr.F("{0}: {1:0.00} → {2:0.00} m", identidade.Name, secao.UsefulHeight, nova.UsefulHeight));
        }

        transacao.Commit();

        var frase = Tr.F("MEMÓRIA altura sugerida aplicada em {0} canaleta(s): {1}.", feitas.Count, string.Join("; ", feitas));
        if (semSugestao.Count > 0) frase += " " + Tr.F("Sem sugestão (sem bacia ou sem vazão): {0}.", string.Join(", ", semSugestao));
        return frase;
    }
}
