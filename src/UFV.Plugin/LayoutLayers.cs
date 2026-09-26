using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// As camadas do desenho da usina, e como garantir cada uma.
///
/// Camada é aparência, nunca identidade (02-arquitetura.md): quem diz que um
/// bloco é um pilar nosso é o XData. As camadas existem para o usuário ligar
/// e desligar o que vê — e é por isso que cada análise tem a sua, e que as
/// alturas nascem numa camada desligada.
/// </summary>
internal static class LayoutLayers
{
    private const string Prefixo = PluginInfo.PrefixoDeDados + "_";

    /// <summary>Os contornos das mesas.</summary>
    internal const string Mesa = Prefixo + "MESA";

    /// <summary>Os pilares.</summary>
    internal const string Pilar = Prefixo + "PILAR";

    /// <summary>Os blocos dos módulos.</summary>
    internal const string Modulo = Prefixo + "MODULO";

    /// <summary>
    /// As faces superiores dos módulos: a camada que o Renan indica no PVsyst.
    /// Só faces, nada mais, para a exportação (etapa 6) e a importação lá
    /// pegarem o plano e só o plano.
    /// </summary>
    internal const string Face = Prefixo + "FACE";

    /// <summary>As alturas de pilar como texto: nasce desligada, "Mostrar alturas" liga.</summary>
    internal const string Alturas = Prefixo + "ALTURAS";

    /// <summary>O aviso das mesas marcadas (e dos pilares com problema).</summary>
    internal const string Marcada = Prefixo + "MARCADA";

    /// <summary>A marca dos grupos: contorno, hachura e número (7.9).</summary>
    internal const string Grupo = Prefixo + "GRUPO";

    /// <summary>
    /// Garante a camada e devolve o nome dela. A cor e o estado ligado só
    /// valem quando a camada é criada: se ela já existe, é do usuário, e o
    /// que ele fez com ela fica.
    /// </summary>
    internal static string Garantir(Transaction transacao, Database database, string nome, RgbColor? cor = null, bool desligada = false)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (tabela.Has(nome)) return nome;

        if (!tabela.IsWriteEnabled) tabela.UpgradeOpen();

        var camada = new LayerTableRecord { Name = nome, IsOff = desligada };

        if (cor is { } c) camada.Color = Color.FromRgb(c.R, c.G, c.B);

        tabela.Add(camada);
        transacao.AddNewlyCreatedDBObject(camada, true);

        return nome;
    }

    /// <summary>Se a camada existe e está desligada.</summary>
    internal static bool? EstaDesligada(Transaction transacao, Database database, string nome)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (!tabela.Has(nome)) return null;

        var camada = (LayerTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);

        return camada.IsOff;
    }

    /// <summary>Liga ou desliga a camada. Falso se ela não existe.</summary>
    internal static bool Ligar(Transaction transacao, Database database, string nome, bool ligada)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (!tabela.Has(nome)) return false;

        var camada = (LayerTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForWrite);
        camada.IsOff = !ligada;

        return true;
    }
}
