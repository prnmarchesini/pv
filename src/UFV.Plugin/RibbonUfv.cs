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
/// O que vai na ribbon e dado do Core (RibbonLayout, 8.16).
/// </summary>
internal static class RibbonUfv
{
    /// <summary>A primeira aba do layout: se ela existe, as abas já foram montadas.</summary>
    private static string IdDaAba => RibbonLayout.Tabs[0].Id;

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

    /// <summary>
    /// Monta as abas a partir de <see cref="RibbonLayout"/> (passo 8.16): o
    /// que vai em cada aba, painel e botão é dado do Core, testado lá (todo
    /// botão com dica, comando que existe). Aqui só se traduz para a ribbon.
    /// </summary>
    private static void Montar()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null)
        {
            RegistroDeDiagnostico.Registrar("Montar chamado sem ribbon.");
            return;
        }

        // NETLOAD por cima do bundle montaria as abas duas vezes.
        if (ribbon.Tabs.Any(t => t.Id == IdDaAba))
        {
            RegistroDeDiagnostico.Registrar("Aba UFV já existia; nada a montar.");
            return;
        }

        foreach (var especificacao in RibbonLayout.Tabs)
        {
            var aba = new RibbonTab
            {
                Id = especificacao.Id,
                Title = especificacao.Title,
                Name = especificacao.Title,
            };

            foreach (var painel in especificacao.Panels) aba.Panels.Add(MontarPainel(painel));

            ribbon.Tabs.Add(aba);
        }

        var temDocumento = AcadApp.DocumentManager.MdiActiveDocument is not null;
        RegistroDeDiagnostico.Registrar($"Abas UFV montadas (documento aberto: {temDocumento}).");
    }

    /// <summary>
    /// Um painel: botões grandes um a um; os pequenos em seguida, empilhados
    /// de três em três numa coluna (é como o Civil 3D faz, e economiza a
    /// largura); o menu, grande.
    /// </summary>
    private static RibbonPanel MontarPainel(RibbonPanelSpec painel)
    {
        var origem = new RibbonPanelSource { Title = painel.Title };
        RibbonRowPanel? coluna = null;
        var naColuna = 0;

        foreach (var item in painel.Items)
        {
            if (item is RibbonButtonSpec { Large: false } pequeno)
            {
                if (coluna is null || naColuna == 3)
                {
                    coluna = new RibbonRowPanel();
                    origem.Items.Add(coluna);
                    naColuna = 0;
                }
                else
                {
                    // A quebra manda o próximo botão para a linha de baixo.
                    coluna.Items.Add(new RibbonRowBreak());
                }

                coluna.Items.Add(BotaoPequeno(pequeno));
                naColuna++;
                continue;
            }

            coluna = null;

            switch (item)
            {
                case RibbonButtonSpec grande:
                    origem.Items.Add(BotaoGrande(grande));
                    break;

                case RibbonMenuSpec menu:
                    origem.Items.Add(Menu(menu));
                    break;
            }
        }

        return new RibbonPanel { Source = origem };
    }

    /// <summary>O ícone pelo nome; nome que não existe cai no da configuração.</summary>
    private static ImageSource Icone(string nome) => nome switch
    {
        "Terreno" => IconesDaRibbon.Terreno(),
        "Coordenada" => IconesDaRibbon.Coordenada(),
        "Status" => IconesDaRibbon.Status(),
        "Area" => IconesDaRibbon.Area(),
        "Alinhamento" => IconesDaRibbon.Alinhamento(),
        "Usina" => IconesDaRibbon.Usina(),
        "Refazer" => IconesDaRibbon.Refazer(),
        "Recalcular" => IconesDaRibbon.Recalcular(),
        "Pontas" => IconesDaRibbon.Pontas(),
        "Parametros" => IconesDaRibbon.Parametros(),
        "PintarEstouros" => IconesDaRibbon.PintarEstouros(),
        "RegerarTudo" => IconesDaRibbon.RegerarTudo(),
        "RecalcularSujas" => IconesDaRibbon.RecalcularSujas(),
        "Numerar" => IconesDaRibbon.Numerar(),
        "Grupos" => IconesDaRibbon.Grupos(),
        "Validar" => IconesDaRibbon.Validar(),
        "Recontar" => IconesDaRibbon.Recontar(),
        "Renomear" => IconesDaRibbon.Renomear(),
        "Alturas" => IconesDaRibbon.Alturas(),
        "Declividade" => IconesDaRibbon.Declividade(),
        "RegerarAlturas" => IconesDaRibbon.RegerarAlturas(),
        "Exportar" => IconesDaRibbon.Exportar(),
        "Sujar" => IconesDaRibbon.Sujar(),
        "Estado" => IconesDaRibbon.Estado(),
        "Mesa" => IconesDaRibbon.Mesa(),
        _ => IconesDaRibbon.Configuracao(),
    };

    /// <summary>
    /// Botão grande: ícone em cima, rótulo embaixo. Quem guarda o comando é
    /// o handler, e não o CommandParameter — a ribbon chama CanExecute(null),
    /// e um handler que dependesse do parâmetro deixaria o botão inerte.
    /// </summary>
    private static RibbonButton BotaoGrande(RibbonButtonSpec b)
    {
        var icone = Icone(b.Icon);

        return new RibbonButton
        {
            Text = b.Text,
            ShowText = true,
            ShowImage = true,
            LargeImage = icone,
            Image = icone,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandHandler = new ComandoDaRibbon(b.Command),
            ToolTip = b.Tooltip,
        };
    }

    /// <summary>Botão pequeno: ícone à esquerda, rótulo ao lado.</summary>
    private static RibbonButton BotaoPequeno(RibbonButtonSpec b)
    {
        var icone = Icone(b.Icon);

        return new RibbonButton
        {
            Text = b.Text,
            ShowText = true,
            ShowImage = true,
            Image = icone,
            LargeImage = icone,
            Size = RibbonItemSize.Standard,
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            CommandHandler = new ComandoDaRibbon(b.Command),
            ToolTip = b.Tooltip,
        };
    }

    /// <summary>
    /// O menu (a Edição compacta, 8.16: "edição tá grande demais para poucos
    /// botões"): um botão grande que abre a lista, cada item com a dica dele.
    /// </summary>
    private static RibbonSplitButton Menu(RibbonMenuSpec menu)
    {
        var icone = Icone(menu.Icon);

        var botao = new RibbonSplitButton
        {
            Text = menu.Text,
            ShowText = true,
            ShowImage = true,
            LargeImage = icone,
            Image = icone,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            IsSplit = false,

            // Sem isto o botão passa a mostrar o último item clicado
            // ("Recalcular") no lugar de "Edição".
            IsSynchronizedWithCurrentItem = false,
            ToolTip = menu.Tooltip,
        };

        foreach (var item in menu.Items) botao.Items.Add(BotaoPequeno(item));

        return botao;
    }
}
