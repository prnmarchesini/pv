using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;

namespace Clivus.Plugin;

/// <summary>
/// A aba Numeração da janela da configuração elétrica (etapa 15). A janela
/// (etapas 12 a 14) só chama <see cref="Criar"/>. Ponto de encontro:
/// preenchido na etapa 15.
/// </summary>
internal static class PainelDeNumeracao
{
    internal static UIElement Criar(Document documento) => new TextBlock { Margin = new Thickness(12), Text = "Numeração (etapa 15)." };
}
