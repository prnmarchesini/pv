using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

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

    /// <summary>A seta e o valor da declividade de cada mesa (análise de 29/09/2026).</summary>
    internal const string SetaDeclividade = Prefixo + "SETA_DECLIVIDADE";

    /// <summary>As áreas de implantação: laranja (pedido do Renan, 29/09/2026).</summary>
    internal const string Area = Prefixo + "AREA";

    /// <summary>Os alinhamentos: amarelo (pedido do Renan, 29/09/2026).</summary>
    internal const string Alinhamento = Prefixo + "ALINHAMENTO";

    /// <summary>As árvores e outros objetos que fazem sombra (9.4).</summary>
    internal const string Arvore = Prefixo + "ARVORE";

    /// <summary>A sombra desenhada no terreno (9.7).</summary>
    internal const string Sombra = Prefixo + "SOMBRA";

    /// <summary>Os módulos marcados pela sombra (9.7): a marca é cor na peça; a camada é a dos textos de sombra.</summary>
    internal const string SombraTexto = Prefixo + "SOMBRA_TEXTO";

    /// <summary>Laranja do AutoCAD (ACI 30).</summary>
    internal const short CorDaArea = 30;

    /// <summary>Amarelo do AutoCAD (ACI 2).</summary>
    internal const short CorDoAlinhamento = 2;

    /// <summary>
    /// Garante a camada com a cor do plugin (índice ACI). Diferente de
    /// <see cref="Garantir"/>, também acerta a cor de uma camada que já
    /// existe, mas só se ela ainda está no branco (ACI 7) com que o plugin
    /// criava as áreas e os alinhamentos até 29/09/2026: cor que o usuário
    /// escolheu fica. Devolve o nome.
    /// </summary>
    internal static string GarantirComCor(Transaction transacao, Database database, string nome, short cor)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (tabela.Has(nome))
        {
            AcertarCorSeBranca(transacao, tabela, nome, cor);
            return nome;
        }

        tabela.UpgradeOpen();

        var camada = new LayerTableRecord { Name = nome, Color = Color.FromColorIndex(ColorMethod.ByAci, cor) };

        tabela.Add(camada);
        transacao.AddNewlyCreatedDBObject(camada, true);

        return nome;
    }

    /// <summary>
    /// Acerta a cor das camadas de área e de alinhamento que já existem no
    /// desenho (desenhos feitos antes de 29/09/2026), sem criar nenhuma.
    /// Quantas mudaram.
    /// </summary>
    internal static int AcertarCoresDaUsina(Transaction transacao, Database database)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        var mudaram = 0;

        if (tabela.Has(Area) && AcertarCorSeBranca(transacao, tabela, Area, CorDaArea)) mudaram++;
        if (tabela.Has(Alinhamento) && AcertarCorSeBranca(transacao, tabela, Alinhamento, CorDoAlinhamento)) mudaram++;

        return mudaram;
    }

    private static bool AcertarCorSeBranca(Transaction transacao, LayerTable tabela, string nome, short cor)
    {
        var camada = (LayerTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);

        if (camada.Color.ColorMethod != ColorMethod.ByAci || camada.Color.ColorIndex != 7) return false;

        camada.UpgradeOpen();
        camada.Color = Color.FromColorIndex(ColorMethod.ByAci, cor);

        return true;
    }

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
