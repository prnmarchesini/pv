using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.GanchosDeTeste))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// Toda escrita do plugin feita fora de um comando (botão de janela solta,
/// abertura do desenho) passa por aqui: trava o documento e cala o vigia,
/// para o que o plugin pinta não virar "mesa pendente" vermelha na folga
/// seguinte (04/10/2026, sombras e análises pela janela).
/// </summary>
internal static class EscritaForaDeComando
{
    internal static T Fazer<T>(Document documento, Func<T> operacao)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(operacao);

        using (documento.LockDocument())
        using (LayoutWatcher.Calar(documento))
            return operacao();
    }

    internal static void Fazer(Document documento, Action operacao) =>
        Fazer(documento, () =>
        {
            operacao();
            return true;
        });
}

#if DEBUG
/// <summary>
/// Só no build de teste: o nível 2 chama, por LISP, o mesmo caminho do botão
/// da janela de sombras, fora de um comando. (clivus-sombras-pela-janela
/// "dd/mm/aaaa" "hh:mm" "fuso" ["escopo" "áreas" "mesas"]) escreve a frase e
/// devolve T; sem o escopo, o desenho inteiro.
/// </summary>
public static class GanchosDeTeste
{
    [LispFunction("CLIVUS-SOMBRAS-PELA-JANELA")]
    public static object? SombrasPelaJanela(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 3 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[0], valores[1], valores[1], "30", valores[2], out var porque);
        if (periodo is null)
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        var escopo = valores.Length >= 6 ? SombrasCommands.EscopoPelosNomes(documento.Database, valores[3], valores[4], valores[5], out porque) : ShadowScope.WholeDrawing;
        if (escopo is null)
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        documento.Editor.WriteMessage($"\n{SombrasCommands.GerarPelaJanela(documento, periodo, escopo)}\n");
        return true;
    }

    /// <summary>
    /// Os outros botões da janela de sombras, fora de comando:
    /// (clivus-sombras-acao "apagar" "escopo" "áreas" "mesas"),
    /// (clivus-sombras-acao "ocultar") e (clivus-sombras-acao "mostrar").
    /// Escreve a frase e devolve T.
    /// </summary>
    [LispFunction("CLIVUS-SOMBRAS-ACAO")]
    public static object? SombrasAcao(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 1 || documento is null) return null;

        RelatoDasSombras relato;
        switch (valores[0])
        {
            case "ocultar":
            case "mostrar":
                relato = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Ligar(documento, valores[0] == "mostrar"));
                break;

            case "apagar" when valores.Length >= 4:
                var escopo = SombrasCommands.EscopoPelosNomes(documento.Database, valores[1], valores[2], valores[3], out var porque);
                if (escopo is null)
                {
                    documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
                    return null;
                }

                relato = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Apagar(documento, escopo));
                break;

            default:
                documento.Editor.WriteMessage($"\nSOMBRAS ação recusada: {string.Join(" ", valores)}\n");
                return null;
        }

        documento.Editor.WriteMessage($"\n{relato}\n");
        return true;
    }

    /// <summary>(clivus-sombras-modulos): os handles dos blocos dos módulos com sombra no registro dos motivos, ou nil.</summary>
    [LispFunction("CLIVUS-SOMBRAS-MODULOS")]
    public static object? SombrasModulos(ResultBuffer argumentos)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return null;

        var handles = SombrasCommands.ModulosComSombra(documento.Database);
        return handles.Count == 0 ? null : new ResultBuffer(handles.Select(h => new TypedValue((int)LispDataType.Text, h)).ToArray());
    }

    private static string[] Valores(ResultBuffer? argumentos) =>
        argumentos?.AsArray().Select(v => Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray() ?? [];

    /// <summary>
    /// O Cancelar da janela de andamento das sombras (05/10/2026), sem janela:
    /// (clivus-sombras-cancelar "dd/mm/aaaa" "dd/mm/aaaa" "fuso" n) gera o
    /// período de hora em hora, das 6h às 18h, pelo caminho do botão, e
    /// "clica" em Cancelar no n-ésimo passo do andamento. Escreve a frase e
    /// devolve T.
    /// </summary>
    [LispFunction("CLIVUS-SOMBRAS-CANCELAR")]
    public static object? SombrasCancelar(ResultBuffer argumentos)
    {
        var valores = Valores(argumentos);
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 4 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[1], "06:00", "18:00", "60", valores[2], out var porque);
        if (periodo is null || !int.TryParse(valores[3], out var noPasso))
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        var passos = 0;
        var frase = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Gerar(documento, periodo, ShadowScope.WholeDrawing, (_, texto) =>
        {
            passos++;
            if (passos == 3) documento.Editor.WriteMessage($"\nSOMBRAS_ANDAMENTO {texto}\n");
            return passos < noPasso;
        }));
        documento.Editor.WriteMessage($"\n{frase}\n");
        return true;
    }
}
#endif
