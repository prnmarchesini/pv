using System.Runtime.CompilerServices;
using Autodesk.Windows;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Monta a aba UFV na ribbon.
///
/// Tudo que toca Autodesk.Windows mora aqui, e nao em UfvExtension, por um
/// motivo concreto: um host sem interface, como o Core Console
/// (accoreconsole.exe), nao tem ribbon. Carregar um tipo cujo metodo mencione
/// RibbonItemEventArgs obriga o runtime a resolver AdWindows na hora de
/// carregar a assembly, e o NETLOAD falha inteiro com "Unable to load
/// assembly" - o plugin nao carrega nem para os comandos que funcionariam bem
/// sem interface.
///
/// Por isso TODO metodo desta classe chamado de fora leva
/// [MethodImpl(MethodImplOptions.NoInlining)]: sem isso o JIT pode trazer o
/// corpo para dentro do chamador e a resolucao de AdWindows volta a acontecer
/// cedo demais. Ao acrescentar um ponto de entrada aqui, o atributo vem junto.
///
/// A ribbon cresce por secoes conforme as etapas; na etapa 0 ela tem so o
/// botao Ola.
/// </summary>
internal static class RibbonUfv
{
    private const string IdDaAba = "UFV_RIBBON_TAB";
    private const string TituloDaAba = "UFV";

    private static readonly object Tranca = new();

    /// <summary>
    /// Se ja assinamos ItemInitialized. Eventos .NET sao multicast: assinar
    /// duas vezes (bundle mais NETLOAD por cima) faria o gancho continuar
    /// disparando a cada item da interface, porque o "-=" do callback remove
    /// uma assinatura, nao todas.
    /// </summary>
    private static bool _esperandoRibbon;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (ComponentManager.Ribbon is not null)
        {
            Montar();
            return;
        }

        // O plugin carregou antes da ribbon existir, que e o caso do bundle no
        // boot do Civil 3D. Esperamos ela ficar pronta.
        lock (Tranca)
        {
            if (_esperandoRibbon) return;

            ComponentManager.ItemInitialized += AoInicializarComponente;
            _esperandoRibbon = true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        lock (Tranca)
        {
            if (!_esperandoRibbon) return;

            ComponentManager.ItemInitialized -= AoInicializarComponente;
            _esperandoRibbon = false;
        }
    }

    private static void AoInicializarComponente(object? remetente, RibbonItemEventArgs e)
    {
        // Este callback roda dentro de um evento de interface do AutoCAD: uma
        // excecao daqui sobe para dentro dele, e nao para o try de quem nos
        // chamou.
        try
        {
            if (ComponentManager.Ribbon is null) return;

            // A ribbon existe: montamos uma vez e desligamos o gancho, que
            // senao dispara a cada item da interface.
            Desinstalar();
            Montar();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao montar a ribbon depois que ela ficou pronta.", erro);
        }
    }

    private static void Montar()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null) return;

        // NETLOAD por cima do bundle montaria a aba duas vezes.
        if (ribbon.Tabs.Any(t => t.Id == IdDaAba)) return;

        var aba = new RibbonTab
        {
            Id = IdDaAba,
            Title = TituloDaAba,
            Name = TituloDaAba,
        };

        aba.Panels.Add(MontarPainelInicio());
        ribbon.Tabs.Add(aba);
    }

    private static RibbonPanel MontarPainelInicio()
    {
        var origem = new RibbonPanelSource { Title = "Início" };

        origem.Items.Add(new RibbonButton
        {
            Text = "Olá",
            ShowText = true,
            ShowImage = false,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            // O nome vem da constante que registra o comando: renomear o
            // comando arrasta o botao junto. O espaco no fim e o Enter; sem
            // ele o texto so fica digitado na linha de comando.
            CommandParameter = PluginInfo.ComandoOla + " ",
            CommandHandler = new ComandoDaRibbon(),
            ToolTip = $"{PluginInfo.Nome}: confirma que o plugin está carregado.",
        });

        return new RibbonPanel { Source = origem };
    }
}
