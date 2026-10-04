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

    internal static RecordTableResult<InverterModel> InverterModels(Database db) =>
        PluginRecords.Load<InverterModel>(db, "INVERSOR_MODELOS", 1, InverterModel.FieldCount, InverterModel.Parse, OQueModelos);

    internal static void SaveInverterModels(Database db, IReadOnlyList<InverterModel> itens) =>
        PluginRecords.Save(db, "INVERSOR_MODELOS", 1, InverterModel.FieldCount, itens, i => i.ToFields());

    internal static RecordTableResult<Inverter> Inverters(Database db) =>
        PluginRecords.Load<Inverter>(db, "INVERSORES", 1, Inverter.FieldCount, Inverter.Parse, OQueInversores);

    internal static void SaveInverters(Database db, IReadOnlyList<Inverter> itens) =>
        PluginRecords.Save(db, "INVERSORES", 1, Inverter.FieldCount, itens, i => i.ToFields());

    internal static RecordTableResult<Transformer> Transformers(Database db) =>
        PluginRecords.Load<Transformer>(db, "TRAFOS", 1, Transformer.FieldCount, Transformer.Parse, OQueTrafos);

    internal static void SaveTransformers(Database db, IReadOnlyList<Transformer> itens) =>
        PluginRecords.Save(db, "TRAFOS", 1, Transformer.FieldCount, itens, i => i.ToFields());

    internal static RecordTableResult<ConsumerUnit> ConsumerUnits(Database db) =>
        PluginRecords.Load<ConsumerUnit>(db, "SUBESTACOES", 1, ConsumerUnit.FieldCount, ConsumerUnit.Parse, OQueSubestacoes);

    internal static void SaveConsumerUnits(Database db, IReadOnlyList<ConsumerUnit> itens) =>
        PluginRecords.Save(db, "SUBESTACOES", 1, ConsumerUnit.FieldCount, itens, i => i.ToFields());
}
