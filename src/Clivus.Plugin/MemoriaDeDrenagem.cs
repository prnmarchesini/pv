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
    /// <param name="parametros">Os parâmetros a usar; sem eles, os gravados no desenho.</param>
    internal static DrainageMemoryResult Montar(Database database, Tin terreno, HydrologyParameters? parametros = null)
    {
        var configuracao = DrenagemStore.Ler(database);
        if (parametros is not null) configuracao = configuracao with { Hydrology = parametros };
        var bacias = DrenagemStore.LerBacias(database);
        var entradas = new List<ChannelInput>();
        var fora = new List<string>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var vias = Vias(transacao, database, configuracao.Hydrology.RoadLayers);

            foreach (var linha in LinhasDeDrenagem.Todas(transacao, database).Where(l => l.Identidade.IsChannel).OrderBy(l => l.Identidade.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var (terrenoDaLinha, perfil) = LinhasDeDrenagem.Perfil(terreno, configuracao, linha);
                if (perfil is null || perfil.Points.Count < 2)
                {
                    // Nunca some calada da memória (regra 4).
                    fora.Add(Tr.F("{0}: sem perfil (fora do terreno ou comprimento zero); ficou fora da memória.", linha.Identidade.Name));
                    continue;
                }

                if (terrenoDaLinha.OutsideCount > 0)
                    fora.Add(Tr.F("{0}: {1} ponto(s) fora do terreno; a memória usa só o trecho com terreno.", linha.Identidade.Name, terrenoDaLinha.OutsideCount));

                entradas.Add(new ChannelInput(linha.Identidade.Id, linha.Identidade.Name, linha.Identidade.Section!, perfil,
                    bacias?.Of(linha.Identidade.Id), Cruzamentos(linha.Vertices, vias).Count));
            }
        }

        return DrainageMemory.Build(configuracao.Hydrology, entradas, fora);
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
            // Xline e raio não têm fim: não são traçado de via.
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve c || c is Xline or Ray || !nomes.Contains(c.Layer)) continue;
            vias.Add(LinhaCommands.Caminho(database, id));
        }

        return vias;
    }

    /// <summary>Onde a canaleta cruza as vias, em planta, com a direção da canaleta ali (<see cref="RoadCrossings"/>).</summary>
    internal static List<(double X, double Y, double DirX, double DirY)> Cruzamentos(IReadOnlyList<Point3> canaleta, List<IReadOnlyList<Point3>> vias) =>
        RoadCrossings.Find(canaleta, vias);

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
