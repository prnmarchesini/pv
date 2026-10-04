using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As perguntas que todo comando faz do mesmo jeito.
///
/// Por enquanto é uma só — o nome —, e ela já estava escrita duas vezes,
/// palavra por palavra, no comando da área e no do alinhamento. A revisão do
/// 4.2 apontou, e vale o mesmo argumento das outras extrações: texto duplicado
/// é texto que diverge, e aí o usuário recebe duas mensagens diferentes para o
/// mesmo erro sem entender por quê.
/// </summary>
internal static class Perguntas
{
    /// <summary>
    /// Maior nome aceito.
    ///
    /// O XData corta cada texto em 255 bytes, e em UTF-8 um acento gasta dois.
    /// O limite é conferido logo depois da pergunta, e não na gravação:
    /// estourar lá dentro faria o usuário perder o que acabou de traçar, com
    /// uma mensagem que não explicaria nada.
    /// </summary>
    internal const int MaiorNome = 100;

    /// <summary>
    /// Pergunta o nome até vir um que sirva, ou até o usuário desistir.
    /// </summary>
    /// <param name="oQue">Como a coisa se chama, em minúscula: "área", "alinhamento".</param>
    /// <param name="oQueMaiusculo">
    /// A mesma coisa começando frase: "A área", "O alinhamento". Português tem
    /// gênero, e montar a frase concatenando artigo daria "A alinhamento".
    /// </param>
    internal static string? Nome(Editor editor, string oQue, string oQueMaiusculo)
    {
        ArgumentNullException.ThrowIfNull(editor);

        // Com interface, uma janela (pedido do Renan em 26/09/2026); no Core
        // Console, a linha de comando, que é o que o script alimenta.
        if (ClivusExtension.TemInterface())
        {
            var nome = NomePelaJanela(oQue);

            if (nome is null) editor.WriteMessage(Tr.F("\n{0} ficou sem nome; nada foi feito.\n", oQueMaiusculo));

            return nome;
        }

        while (true)
        {
            var resposta = editor.GetString(new PromptStringOptions(Tr.F("\nNome do {0}: ", oQue))
            {
                AllowSpaces = true,
            });

            if (resposta.Status != PromptStatus.OK)
            {
                editor.WriteMessage(Tr.F("\n{0} ficou sem nome; nada foi feito.\n", oQueMaiusculo));
                return null;
            }

            var nome = resposta.StringResult.Trim();

            if (nome.Length == 0)
            {
                editor.WriteMessage(Tr.F("\n{0} precisa de um nome.\n", oQueMaiusculo));
                continue;
            }

            if (nome.Length > MaiorNome)
            {
                editor.WriteMessage(
                    Tr.F("\nNome longo demais ({0} caracteres). O limite é {1}.\n", nome.Length, MaiorNome));
                continue;
            }

            return nome;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string? NomePelaJanela(string oQue)
    {
        var janela = new JanelaDeNome(Tr.F("Nome do {0}", oQue), Tr.F("Como se chama este {0}?", oQue));
        var resultado = Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowModalWindow(janela);

        return resultado == true ? janela.Nome : null;
    }
}
