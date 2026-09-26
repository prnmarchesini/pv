using Autodesk.AutoCAD.EditorInput;

namespace UFV.Plugin;

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
        if (UfvExtension.TemInterface())
        {
            var nome = NomePelaJanela(oQue);

            if (nome is null) editor.WriteMessage($"\n{oQueMaiusculo} não foi criada.\n");

            return nome;
        }

        while (true)
        {
            var resposta = editor.GetString(new PromptStringOptions($"\nNome do {oQue}: ")
            {
                AllowSpaces = true,
            });

            if (resposta.Status != PromptStatus.OK)
            {
                editor.WriteMessage($"\n{oQueMaiusculo} não foi criada.\n");
                return null;
            }

            var nome = resposta.StringResult.Trim();

            if (nome.Length == 0)
            {
                editor.WriteMessage($"\n{oQueMaiusculo} precisa de um nome.\n");
                continue;
            }

            if (nome.Length > MaiorNome)
            {
                editor.WriteMessage(
                    $"\nNome longo demais ({nome.Length} caracteres). O limite é {MaiorNome}.\n");
                continue;
            }

            return nome;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string? NomePelaJanela(string oQue)
    {
        var janela = new JanelaDeNome($"Nome do {oQue}", $"Como se chama este {oQue}?");
        var resultado = Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowModalWindow(janela);

        return resultado == true ? janela.Nome : null;
    }
}
