namespace Clivus.Core;

/// <summary>
/// O que o AutoCAD aceita como nome de camada, conferido sem o AutoCAD.
///
/// Existe porque o nome vem do usuário, pela tela do 4.4, e o AutoCAD só
/// reclama na hora de criar a camada — que é dentro de uma transação, no meio
/// da pintura, quando já é tarde. Recusar aqui é recusar na tela.
///
/// A regra é a do AutoCAD com <c>EXTNAMES</c> ligado, que é o padrão desde o
/// 2000: até 255 caracteres, e nenhum destes:
/// <c>&lt; &gt; / \ " : ; ? * | , = `</c>. Espaço no meio pode; no começo ou
/// no fim, não: o AutoCAD apara espaço de nome de símbolo, e o nome gravado
/// deixaria de bater com o configurado. Isso é o que a documentação do
/// AutoCAD diz, e ainda não foi conferido em teste de nível 2. Por isso o
/// Core não deve ser o único juiz: quem criar a camada no plugin (etapa 5)
/// passa o nome também por <c>SymbolUtilityServices.ValidateSymbolName</c>.
/// </summary>
public static class LayerName
{
    /// <summary>Maior nome de camada que o AutoCAD aceita.</summary>
    public const int MaxLength = 255;

    private const string Proibidos = "<>/\\\":;?*|,=`";

    /// <summary>Por que este nome não serve, ou null se serve.</summary>
    public static string? WhyInvalid(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return Tr.T("está vazio");

        if (nome.Length > MaxLength) return Tr.F("tem mais de {0} caracteres", MaxLength);

        if (nome != nome.Trim()) return Tr.T("começa ou termina com espaço");

        foreach (var caractere in nome)
        {
            if (Proibidos.Contains(caractere) || char.IsControl(caractere))
                return Tr.F("tem o caractere '{0}', que o AutoCAD não aceita", caractere);
        }

        return null;
    }
}
