using System.Runtime.CompilerServices;
using System.Windows.Media;
using Autodesk.Windows;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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
        if (ribbon is null)
        {
            RegistroDeDiagnostico.Registrar("Montar chamado sem ribbon.");
            return;
        }

        // NETLOAD por cima do bundle montaria a aba duas vezes.
        if (ribbon.Tabs.Any(t => t.Id == IdDaAba))
        {
            RegistroDeDiagnostico.Registrar("Aba UFV já existia; nada a montar.");
            return;
        }

        var aba = new RibbonTab
        {
            Id = IdDaAba,
            Title = TituloDaAba,
            Name = TituloDaAba,
        };

        aba.Panels.Add(MontarPainelInicio());
        aba.Panels.Add(MontarPainelTerreno());
        aba.Panels.Add(MontarPainelUfv());
        ribbon.Tabs.Add(aba);

        var temDocumento = AcadApp.DocumentManager.MdiActiveDocument is not null;
        RegistroDeDiagnostico.Registrar($"Aba UFV montada (documento aberto: {temDocumento}).");
    }

    private static RibbonPanel MontarPainelInicio()
    {
        var origem = new RibbonPanelSource { Title = "Início" };

        origem.Items.Add(BotaoGrande(
            "Olá",
            IconesDaRibbon.Ola(),
            PluginInfo.ComandoOla,
            $"{PluginInfo.Nome}: confirma que o plugin está carregado."));

        return new RibbonPanel { Source = origem };
    }

    /// <summary>
    /// Seção Terreno: escolher a superfície e conferir o que saiu dela.
    ///
    /// O botão Terreno é o grande, porque é por onde se começa. Os outros dois
    /// dependem dele e ficam pequenos, empilhados ao lado — é o arranjo que o
    /// próprio Civil 3D usa, e economiza a largura que três botões grandes
    /// ocupariam.
    ///
    /// Eles não são desabilitados quando não há terreno: o comando avisa e diz
    /// o que fazer, que é mais útil que um botão cinza sem explicação.
    /// </summary>
    private static RibbonPanel MontarPainelTerreno()
    {
        var origem = new RibbonPanelSource { Title = "Terreno" };

        origem.Items.Add(BotaoGrande(
            "Terreno",
            IconesDaRibbon.Terreno(),
            PluginInfo.ComandoTerreno,
            "Escolhe qual superfície do desenho é o terreno."));

        var coluna = new RibbonRowPanel();

        coluna.Items.Add(BotaoPequeno(
            "Coordenada",
            IconesDaRibbon.Coordenada(),
            PluginInfo.ComandoCoordenada,
            "Clica num ponto e responde X, Y e Z do terreno."));

        // A quebra manda o próximo botão para a linha de baixo. Sem ela os
        // dois ficariam lado a lado e o painel voltaria a ficar largo.
        coluna.Items.Add(new RibbonRowBreak());

        coluna.Items.Add(BotaoPequeno(
            "Status",
            IconesDaRibbon.Status(),
            PluginInfo.ComandoTerrenoStatus,
            "Diz se o terreno processado ainda corresponde à superfície do desenho."));

        origem.Items.Add(coluna);

        return new RibbonPanel { Source = origem };
    }

    /// <summary>
    /// Seção UFV: o que é do projeto da usina, e não do terreno.
    /// </summary>
    private static RibbonPanel MontarPainelUfv()
    {
        var origem = new RibbonPanelSource { Title = "UFV" };

        origem.Items.Add(BotaoGrande(
            "Área",
            IconesDaRibbon.Area(),
            PluginInfo.ComandoArea,
            "Traça a área de implantação e a assenta no terreno."));

        return new RibbonPanel { Source = origem };
    }

    /// <summary>
    /// Botão grande: ícone em cima, rótulo embaixo.
    ///
    /// O nome do comando vem sempre de uma constante de <see cref="PluginInfo"/>:
    /// renomear o comando arrasta o botão junto. Quem guarda o nome é o
    /// handler, e não o CommandParameter — a ribbon chama CanExecute(null), e
    /// um handler que dependesse do parâmetro deixaria o botão inerte.
    /// </summary>
    private static RibbonButton BotaoGrande(
        string rotulo, ImageSource icone, string comando, string dica) =>
        new()
        {
            Text = rotulo,
            ShowText = true,
            ShowImage = true,
            LargeImage = icone,
            Image = icone,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandHandler = new ComandoDaRibbon(comando),
            ToolTip = dica,
        };

    /// <summary>Botão pequeno: ícone à esquerda, rótulo ao lado.</summary>
    private static RibbonButton BotaoPequeno(
        string rotulo, ImageSource icone, string comando, string dica) =>
        new()
        {
            Text = rotulo,
            ShowText = true,
            ShowImage = true,
            Image = icone,
            LargeImage = icone,
            Size = RibbonItemSize.Standard,
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            CommandHandler = new ComandoDaRibbon(comando),
            ToolTip = dica,
        };
}
