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
/// "dd/mm/aaaa" "hh:mm" "fuso") escreve a frase e devolve T.
/// </summary>
public static class GanchosDeTeste
{
    [LispFunction("CLIVUS-SOMBRAS-PELA-JANELA")]
    public static object? SombrasPelaJanela(ResultBuffer argumentos)
    {
        var valores = argumentos?.AsArray().Select(v => Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray() ?? [];
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 3 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[0], valores[1], valores[1], "30", valores[2], out var porque);
        if (periodo is null)
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        documento.Editor.WriteMessage($"\n{SombrasCommands.GerarPelaJanela(documento, periodo)}\n");
        return true;
    }

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
        var valores = argumentos?.AsArray().Select(v => Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray() ?? [];
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (valores.Length < 4 || documento is null) return null;

        var periodo = SombrasCommands.Ler(valores[0], valores[1], "06:00", "18:00", "60", valores[2], out var porque);
        if (periodo is null || !int.TryParse(valores[3], out var noPasso))
        {
            documento.Editor.WriteMessage($"\nSOMBRAS {porque}\n");
            return null;
        }

        var passos = 0;
        var frase = EscritaForaDeComando.Fazer(documento, () => SombrasCommands.Gerar(documento, periodo, (_, texto) =>
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
