using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O contrato da parte elétrica no desenho (plano/eletrica/05-contrato-interno.md):
/// a string desenhada guarda o seu vínculo no XData da polilinha do traçado;
/// modelos de inversor, inversores, trafos e subestações são registros do
/// dicionário do plugin; o retângulo de equipamento em campo guarda só o
/// tipo e o GUID do cadastro.
/// </summary>
internal static class ElectricalStore
{
    private const int VersaoDaString = 1;
    private const int VersaoDoEquipamento = 1;

    // ---------------------------------------------------------------- strings

    /// <summary>Grava o vínculo na entidade do traçado (substitui o anterior).</summary>
    internal static void SaveString(Transaction transacao, Entity entidade, ElectricalString s) =>
        PluginXData.Save(transacao, entidade, ElectricalString.Tipo, VersaoDaString, [.. s.ToFields()]);

    /// <summary>A string desta entidade, ou null se ela não é um traçado de string.</summary>
    internal static ElectricalString? LoadString(Entity entidade) =>
        PluginXData.LoadAll(entidade, ElectricalString.Tipo, VersaoDaString, ElectricalString.FixedFieldCount) is { } c ? ElectricalString.Parse(c) : null;

    /// <summary>Todas as strings do espaço do modelo, com a entidade de cada uma.</summary>
    internal static List<(ObjectId Id, ElectricalString String)> Strings(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
        var achadas = new List<(ObjectId, ElectricalString)>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || !id.ObjectClass.IsDerivedFrom(classe)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LoadString(e) is { } s) achadas.Add((id, s));
        }

        return achadas;
    }

    // ------------------------------------------------- equipamento em campo

    internal static void SavePlacement(Transaction transacao, Entity entidade, EquipmentPlacement p) =>
        PluginXData.Save(transacao, entidade, EquipmentPlacement.Tipo, VersaoDoEquipamento, [.. p.ToFields()]);

    internal static EquipmentPlacement? LoadPlacement(Entity entidade) =>
        PluginXData.Load(entidade, EquipmentPlacement.Tipo, VersaoDoEquipamento, EquipmentPlacement.FieldCount) is { } c ? EquipmentPlacement.Parse(c) : null;

    // ------------------------------------------------------------- cadastros

    private static readonly string OQueModelos = Tr.N("de modelos de inversor");
    private static readonly string OQueInversores = Tr.N("de inversores");
    private static readonly string OQueTrafos = Tr.N("de transformadores");
    private static readonly string OQueSubestacoes = Tr.N("de subestações");
    private static readonly string OQueSkids = Tr.N("de skids");
    private static readonly string OQueBlocos = Tr.N("de blocos de subestação");

    /// <summary>
    /// Os modelos de inversor. Formato 2 (05/10/2026) tem a lista das entradas
    /// de cada MPPT; o 1 (MPPT x entradas por MPPT) continua sendo lido e vira
    /// a lista com o valor repetido. Grava sempre o 2.
    /// </summary>
    internal static RecordTableResult<InverterModel> InverterModels(Database db) =>
        PluginRecords.Version(db, ChaveDosModelos) == 1
            ? PluginRecords.Load<InverterModel>(db, ChaveDosModelos, 1, InverterModel.FieldCount, InverterModel.ParseLegacy, OQueModelos)
            : PluginRecords.Load<InverterModel>(db, ChaveDosModelos, VersaoDosModelos, InverterModel.FieldCount, InverterModel.Parse, OQueModelos);

    internal static void SaveInverterModels(Database db, IReadOnlyList<InverterModel> itens) =>
        PluginRecords.Save(db, ChaveDosModelos, VersaoDosModelos, InverterModel.FieldCount, itens, i => i.ToFields());

    internal const string ChaveDosModelos = "INVERSOR_MODELOS";
    private const int VersaoDosModelos = 2;

    internal static RecordTableResult<Inverter> Inverters(Database db) =>
        PluginRecords.Load<Inverter>(db, "INVERSORES", 1, Inverter.FieldCount, Inverter.Parse, OQueInversores);

    internal static void SaveInverters(Database db, IReadOnlyList<Inverter> itens) =>
        PluginRecords.Save(db, "INVERSORES", 1, Inverter.FieldCount, itens, i => i.ToFields());

    internal static RecordTableResult<Transformer> Transformers(Database db) =>
        PluginRecords.Load<Transformer>(db, "TRAFOS", 1, Transformer.FieldCount, Transformer.Parse, OQueTrafos);

    internal static void SaveTransformers(Database db, IReadOnlyList<Transformer> itens) =>
        PluginRecords.Save(db, "TRAFOS", 1, Transformer.FieldCount, itens, i => i.ToFields());

    /// <summary>
    /// As UCs. Formato 2 (05/10/2026) tem o bloco físico da compartilhada; o
    /// formato 1 (sem bloco) continua sendo lido, e a compartilhada dele cai
    /// num bloco na leitura (<see cref="ElectricalSetup"/>). Grava sempre o 2.
    /// </summary>
    internal static RecordTableResult<ConsumerUnit> ConsumerUnits(Database db) =>
        PluginRecords.Version(db, ChaveDasUcs) == 1
            ? PluginRecords.Load<ConsumerUnit>(db, ChaveDasUcs, 1, ConsumerUnit.LegacyFieldCount, ConsumerUnit.Parse, OQueSubestacoes)
            : PluginRecords.Load<ConsumerUnit>(db, ChaveDasUcs, VersaoDasUcs, ConsumerUnit.FieldCount, ConsumerUnit.Parse, OQueSubestacoes);

    internal static void SaveConsumerUnits(Database db, IReadOnlyList<ConsumerUnit> itens) =>
        PluginRecords.Save(db, ChaveDasUcs, VersaoDasUcs, ConsumerUnit.FieldCount, itens, i => i.ToFields());

    /// <summary>O bloco físico da subestação compartilhada (o cubículo com as UCs C1, C2... dentro).</summary>
    internal static RecordTableResult<Substation> Substations(Database db) =>
        PluginRecords.Load<Substation>(db, "SUBESTACOES_BLOCOS", 1, Substation.FieldCount, Substation.Parse, OQueBlocos);

    internal static void SaveSubstations(Database db, IReadOnlyList<Substation> itens) =>
        PluginRecords.Save(db, "SUBESTACOES_BLOCOS", 1, Substation.FieldCount, itens, i => i.ToFields());

    private const string ChaveDasUcs = "SUBESTACOES";
    private const int VersaoDasUcs = 2;

    /// <summary>O nome de cada skid (14.7), um por trafo; o vínculo inversor → trafo continua em <see cref="Inverter.Transformer"/>.</summary>
    internal static RecordTableResult<Skid> Skids(Database db) =>
        PluginRecords.Load<Skid>(db, "SKIDS", 1, Skid.FieldCount, Skid.Parse, OQueSkids);

    internal static void SaveSkids(Database db, IReadOnlyList<Skid> itens) =>
        PluginRecords.Save(db, "SKIDS", 1, Skid.FieldCount, itens, i => i.ToFields());
}
