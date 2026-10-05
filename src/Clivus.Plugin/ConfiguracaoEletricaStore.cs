using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A configuração elétrica do desenho (etapas 12 a 14) inteira de uma vez:
/// lê os registros do contrato (<see cref="ElectricalStore"/>) num
/// <see cref="ElectricalSetup"/>, aplica a mudança e grava de volta.
/// </summary>
internal static class ConfiguracaoEletricaStore
{
    /// <summary>O cadastro e as frases de problema dos registros (null se todos estavam inteiros).</summary>
    internal static (ElectricalSetup Setup, string? Problema) Ler(Database database)
    {
        var ucs = ElectricalStore.ConsumerUnits(database);
        var trafos = ElectricalStore.Transformers(database);
        var inversores = ElectricalStore.Inverters(database);

        var problemas = new[] { ucs.Problem, trafos.Problem, inversores.Problem }.Where(p => p is not null).ToList();

        return (new ElectricalSetup(trafos.Items, inversores.Items, ucs.Items), problemas.Count == 0 ? null : string.Join("; ", problemas));
    }

    internal static void Gravar(Database database, ElectricalSetup setup)
    {
        ElectricalStore.SaveConsumerUnits(database, setup.Units);
        ElectricalStore.SaveTransformers(database, setup.Transformers);
        ElectricalStore.SaveInverters(database, setup.Inverters);
    }

    /// <summary>Lê, aplica a mudança e grava. Devolve o que a mudança devolveu.</summary>
    internal static T Mudar<T>(Database database, Func<ElectricalSetup, T> mudanca)
    {
        var (setup, _) = Ler(database);
        var resultado = mudanca(setup);
        Gravar(database, setup);
        return resultado;
    }
}
