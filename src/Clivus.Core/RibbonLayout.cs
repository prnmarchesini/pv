namespace Clivus.Core;

/// <summary>Um botão da ribbon: rótulo, comando, dica, ícone (pelo nome) e se é grande.</summary>
public sealed record RibbonButtonSpec(string Text, string Command, string Tooltip, string Icon, bool Large = false);

/// <summary>Um botão de menu: abre a lista dos botões dele (a "Edição" compacta).</summary>
public sealed record RibbonMenuSpec(string Text, string Tooltip, string Icon, IReadOnlyList<RibbonButtonSpec> Items);

/// <summary>Um painel: título e os itens (botões e menus), na ordem.</summary>
public sealed record RibbonPanelSpec(string Title, IReadOnlyList<object> Items)
{
    /// <summary>Todos os botões do painel, inclusive os de dentro dos menus.</summary>
    public IEnumerable<RibbonButtonSpec> Buttons =>
        Items.SelectMany(i => i switch
        {
            RibbonButtonSpec b => [b],
            RibbonMenuSpec m => m.Items,
            _ => Enumerable.Empty<RibbonButtonSpec>(),
        });
}

/// <summary>Uma aba da ribbon.</summary>
public sealed record RibbonTabSpec(string Id, string Title, IReadOnlyList<RibbonPanelSpec> Panels);

/// <summary>
/// A ribbon (passo 8.16, Melhorias.docx, 01/10/2026), descrita como dado
/// para o Core testar o que o Word pede: todo botão com texto explicativo
/// ("isso é regra, preciso que tudo tenha isso"), sem o Olá, sem Mesa,
/// Configuração e Parâmetros soltos (viram a janela de Configurações), a
/// Edição compacta num menu. Uma aba só, a Clivus Solar (Renan, 02/10/2026: "eu quero
/// apenas o menu UFV, e aí dentro dele você coloca análises e tags"):
/// Análises e Tags são botões que abrem janelas com abas ("menu análises, e
/// aí o modal com as abas, algo muito estruturado").
/// O plugin só monta.
/// </summary>
public static class RibbonLayout
{
    private static RibbonButtonSpec B(string texto, string comando, string dica, string icone, bool grande = false) =>
        new(texto, comando, dica, icone, grande);

    /// <summary>As abas, na ordem.</summary>
    public static IReadOnlyList<RibbonTabSpec> Tabs { get; } =
    [
        new("CLIVUS_RIBBON_TAB", "Clivus Solar",
        [
            new(Tr.N("Configurações"),
            [
                B(Tr.N("Configurações"), PluginInfo.ComandoConfiguracoes,
                    Tr.N("Estruturas (mesas do desenho, vãos, enterro), escolha das mesas e cores, parâmetros da usina e estilos do projeto."),
                    "configuracoes", grande: true),
                B(Tr.N("Ativar"), PluginInfo.ComandoAtivar, Tr.N("Ativa o Clivus Solar neste PC com o código gerado no portal do app, e mostra a situação da licença."), "ativar"),
                B(Tr.N("Sobre"), PluginInfo.ComandoSobre, Tr.N("O Clivus Solar: versão, onde fica o registro de diagnóstico e o que ele faz."), "sobre"),
            ]),
            new(Tr.N("Terreno"),
            [
                B(Tr.N("Terreno"), PluginInfo.ComandoTerreno, Tr.N("Escolhe ou troca a superfície do desenho que é o terreno."), "terreno", grande: true),
                B(Tr.N("Resumo"), PluginInfo.ComandoTerrenoResumo, Tr.N("Qual terreno está escolhido, a área, a cidade, o país e o fuso UTM SIRGAS 2000; ali também se corrige a localização."), "terreno_resumo"),
                B(Tr.N("Coordenada"), PluginInfo.ComandoCoordenada, Tr.N("Clica num ponto e responde X, Y e Z do terreno."), "coordenada"),
            ]),
            new(Tr.N("Implantação"),
            [
                B(Tr.N("Área"), PluginInfo.ComandoArea, Tr.N("Traça a área de implantação e a assenta no terreno."), "area", grande: true),
                B(Tr.N("Alinhamento"), PluginInfo.ComandoAlinhamento, Tr.N("Traça a linha onde as fileiras nascem e escolhe o lado delas."), "alinhamento", grande: true),
            ]),
            new(Tr.N("Processar"),
            [
                B(Tr.N("Usina"), PluginInfo.ComandoUsina, Tr.N("Gera a área inteira com as mesas em uso, sem análise (as análises ficam no painel Análises)."), "usina", grande: true),
                B(Tr.N("Regerar área"), PluginInfo.ComandoRefazer, Tr.N("Apaga e gera de novo a área com a configuração atual."), "regerar_area"),
                B(Tr.N("Regerar todas as áreas"), PluginInfo.ComandoRegerar, Tr.N("Refaz todas as áreas do desenho com a configuração atual."), "regerar_todas"),
            ]),
            new(Tr.N("Edição"),
            [
                new RibbonMenuSpec(Tr.N("Edição"), Tr.N("Recalcular, pontas à mão, validar, recontar e os outros ajustes de mesas já desenhadas."), "edicao",
                [
                    B(Tr.N("Recalcular"), PluginInfo.ComandoRecalcular, Tr.N("Refaz a mesa escolhida no terreno de onde ela está, com o mesmo GUID."), "recalcular"),
                    B(Tr.N("Recalcular pendentes"), PluginInfo.ComandoRecalcularPendentes, Tr.N("Refaz só as mesas marcadas como pendentes (movidas, copiadas)."), "recalcular_pendentes"),
                    B(Tr.N("Pontas"), PluginInfo.ComandoPontas, Tr.N("Escolhe à mão a altura da ponta baixa no primeiro e no último pilar da mesa."), "pontas"),
                    B(Tr.N("Validar"), PluginInfo.ComandoValidar, Tr.N("Confere o desenho: registros sumidos, pendentes, duplicadas, órfãs, carimbo do terreno."), "validar"),
                    B(Tr.N("Recontar"), PluginInfo.ComandoRecontar, Tr.N("Refaz a contagem de mesas, módulos, pilares e kWp pelo que está no desenho."), "recontar"),
                    B(Tr.N("Renomear blocos"), PluginInfo.ComandoRenomear, Tr.N("Devolve aos blocos copiados o nome do padrão do plugin."), "renomear"),
                    B(Tr.N("Estado"), PluginInfo.ComandoEstado, Tr.N("Lista as mesas pendentes e o motivo."), "estado"),
                    B(Tr.N("Marcar pendente"), PluginInfo.ComandoPendente, Tr.N("Marca a mesa escolhida como pendente, para recalcular depois."), "pendente"),
                    B(Tr.N("Trocar mesa"), PluginInfo.ComandoTrocarMesa, Tr.N("Troca a mesa clicada por uma ou mais de outro tipo (por exemplo, uma de 28 por duas de 14), travando o início ou o fim dela."), "trocar"),
                    B(Tr.N("Regerar fileira"), PluginInfo.ComandoRegerarFileira, Tr.N("Apaga e gera de novo só a fileira da mesa clicada, com as mesas em uso; acerta o espaçamento depois de uma troca."), "regerar_fileira"),
                ]),
                B(Tr.N("Grupos"), PluginInfo.ComandoGruposPainel, Tr.N("Abre o painel de grupos: mesas, módulos, pilares e kWp de cada grupo."), "grupos", grande: true),
            ]),
            new(Tr.N("Análises"),
            [
                B(Tr.N("Análises"), PluginInfo.ComandoAnalises,
                    Tr.N("Abre a janela das análises, uma aba para cada: ponta baixa, ponta alta, declividade e pilares (inserir, analisar com cores, apagar, quantificar) e as quantidades para o Excel."),
                    "analises", grande: true),
            ]),
            new(Tr.N("Tags"),
            [
                B(Tr.N("Tags"), PluginInfo.ComandoTags,
                    Tr.N("Abre a janela das tags: numerar fileiras e mesas, e as tags de fileiras, mesas, módulos e strings (inserir e apagar)."),
                    "tags", grande: true),
            ]),
            new(Tr.N("Sombreamento"),
            [
                new RibbonMenuSpec(Tr.N("Objetos"), Tr.N("Objetos que fazem sombra nos módulos; o primeiro é a árvore."), "objetos",
                [
                    B(Tr.N("Árvore"), PluginInfo.ComandoArvore, Tr.N("Árvore como um pirulito: tronco e copa cilíndricos, com altura e largura de cada um. Clique onde pôr; o pé fica no terreno e acompanha o terreno quando a árvore é arrastada."), "arvore"),
                ]),
                B(Tr.N("Sombras"), PluginInfo.ComandoSombras, Tr.N("Escolhe o dia e o horário (ou um dia, um mês, um ano inteiro) e desenha a sombra dos objetos no terreno e sobre as mesas, marcando os módulos que ela pega; no período, o pior caso."), "sombras", grande: true),
                B(Tr.N("Por quê?"), PluginInfo.ComandoSombraPorQue, Tr.N("Clique num módulo marcado: diz quanto da face pega sombra, de quê (árvore, outra mesa, terreno) e em que dia e hora."), "sombra_porque", grande: true),
            ]),
            new(Tr.N("Elétrica"),
            [
                B(Tr.N("String"), PluginInfo.ComandoString, Tr.N("Os tipos de string (a biblioteca) e a geração do traçado das strings sobre os módulos."), "string", grande: true),
                B(Tr.N("Configuração elétrica"), PluginInfo.ComandoEletrica, Tr.N("Subestação, transformadores, inversores (com a alocação das strings) e a numeração das strings."), "eletrica", grande: true),
                B(Tr.N("Resumo"), PluginInfo.ComandoEletricaResumo, Tr.N("O sistema inteiro pela cadeia de vínculo: subestações, trafos, inversores, strings, módulos e potência."), "resumo_eletrico", grande: true),
            ]),
            new(Tr.N("Saída"),
            [
                B(Tr.N("PVsyst"), PluginInfo.ComandoExportar, Tr.N("Exporta as faces dos módulos escolhidos para o PVsyst (DAE)."), "pvsyst", grande: true),
                B(Tr.N("Excel"), PluginInfo.ComandoExportarExcel, Tr.N("Exporta para o Excel o resumo, as quantificações das análises e os pilares."), "excel", grande: true),
                B(Tr.N("3D"), PluginInfo.ComandoVer3D, Tr.N("Abre no navegador um modelo 3D para girar e dar zoom: terreno, mesas com as cores dos tipos, pilares e árvores. Funciona sem internet."), "ver3d", grande: true),
            ]),
        ]),
    ];

    /// <summary>Todos os botões de todas as abas.</summary>
    public static IEnumerable<RibbonButtonSpec> AllButtons => Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Buttons);
}
