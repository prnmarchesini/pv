using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>A última quantificação de cada análise, no dicionário do desenho (passo 8.12).</summary>
internal static class QuantificacaoGravada
{
    internal static void Gravar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade, BandCount pontos, BandCount? modulos)
    {
        var registro = new AnalysisTally(tipo, regra, unidade, pontos, modulos, DateTime.Now);

        PluginDictionary.Save(database, AnalysisTally.StorageKey(tipo), new ResultBuffer(
            registro.Encode().Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));
    }

    internal static AnalysisTally? Ler(Database database, IndependentKind tipo)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, AnalysisTally.StorageKey(tipo));
            var campos = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            return AnalysisTally.Decode(tipo, campos);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Não consegui ler a quantificação de {tipo}.", erro);
            return null;
        }
    }
}
