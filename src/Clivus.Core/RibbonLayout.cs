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
            new("Configurações",
            [
                B("Configurações", PluginInfo.ComandoConfiguracoes,
                    "Estruturas (mesas do desenho, vãos, enterro), escolha das mesas e cores, parâmetros da usina e estilos do projeto.",
                    "configuracoes", grande: true),
                B("Ativar", PluginInfo.ComandoAtivar, "Ativa o Clivus Solar neste PC com o código gerado no portal do app, e mostra a situação da licença.", "ativar"),
                B("Sobre", PluginInfo.ComandoSobre, "O Clivus Solar: versão, onde fica o registro de diagnóstico e o que ele faz.", "sobre"),
            ]),
            new("Terreno",
            [
                B("Terreno", PluginInfo.ComandoTerreno, "Escolhe ou troca a superfície do desenho que é o terreno.", "terreno", grande: true),
                B("Resumo", PluginInfo.ComandoTerrenoResumo, "Qual terreno está escolhido, a área, a cidade, o país e o fuso UTM SIRGAS 2000; ali também se corrige a localização.", "terreno_resumo"),
                B("Coordenada", PluginInfo.ComandoCoordenada, "Clica num ponto e responde X, Y e Z do terreno.", "coordenada"),
            ]),
            new("Implantação",
            [
                B("Área", PluginInfo.ComandoArea, "Traça a área de implantação e a assenta no terreno.", "area", grande: true),
                B("Alinhamento", PluginInfo.ComandoAlinhamento, "Traça a linha onde as fileiras nascem e escolhe o lado delas.", "alinhamento", grande: true),
            ]),
            new("Processar",
            [
                B("Usina", PluginInfo.ComandoUsina, "Gera a área inteira com as mesas em uso, sem análise (as análises ficam no painel Análises).", "usina", grande: true),
                B("Regerar área", PluginInfo.ComandoRefazer, "Apaga e gera de novo a área com a configuração atual.", "regerar_area"),
                B("Regerar todas as áreas", PluginInfo.ComandoRegerar, "Refaz todas as áreas do desenho com a configuração atual.", "regerar_todas"),
            ]),
            new("Edição",
            [
                new RibbonMenuSpec("Edição", "Recalcular, pontas à mão, validar, recontar e os outros ajustes de mesas já desenhadas.", "edicao",
                [
                    B("Recalcular", PluginInfo.ComandoRecalcular, "Refaz a mesa escolhida no terreno de onde ela está, com o mesmo GUID.", "recalcular"),
                    B("Recalcular pendentes", PluginInfo.ComandoRecalcularPendentes, "Refaz só as mesas marcadas como pendentes (movidas, copiadas).", "recalcular_pendentes"),
                    B("Pontas", PluginInfo.ComandoPontas, "Escolhe à mão a altura da ponta baixa no primeiro e no último pilar da mesa.", "pontas"),
                    B("Validar", PluginInfo.ComandoValidar, "Confere o desenho: registros sumidos, pendentes, duplicadas, órfãs, carimbo do terreno.", "validar"),
                    B("Recontar", PluginInfo.ComandoRecontar, "Refaz a contagem de mesas, módulos, pilares e kWp pelo que está no desenho.", "recontar"),
                    B("Renomear blocos", PluginInfo.ComandoRenomear, "Devolve aos blocos copiados o nome do padrão do plugin.", "renomear"),
                    B("Estado", PluginInfo.ComandoEstado, "Lista as mesas pendentes e o motivo.", "estado"),
                    B("Marcar pendente", PluginInfo.ComandoPendente, "Marca a mesa escolhida como pendente, para recalcular depois.", "pendente"),
                    B("Trocar mesa", PluginInfo.ComandoTrocarMesa, "Troca a mesa clicada por uma ou mais de outro tipo (por exemplo, uma de 28 por duas de 14), travando o início ou o fim dela.", "trocar"),
                    B("Regerar fileira", PluginInfo.ComandoRegerarFileira, "Apaga e gera de novo só a fileira da mesa clicada, com as mesas em uso; acerta o espaçamento depois de uma troca.", "regerar_fileira"),
                ]),
                B("Grupos", PluginInfo.ComandoGruposPainel, "Abre o painel de grupos: mesas, módulos, pilares e kWp de cada grupo.", "grupos", grande: true),
            ]),
            new("Análises",
            [
                B("Análises", PluginInfo.ComandoAnalises,
                    "Abre a janela das análises, uma aba para cada: ponta baixa, ponta alta, declividade e pilares (inserir, analisar com cores, apagar, quantificar) e as quantidades para o Excel.",
                    "analises", grande: true),
            ]),
            new("Tags",
            [
                B("Tags", PluginInfo.ComandoTags,
                    "Abre a janela das tags: numerar fileiras e mesas, e as tags de fileiras, mesas, módulos e strings (inserir e apagar).",
                    "tags", grande: true),
            ]),
            new("Sombreamento",
            [
                new RibbonMenuSpec("Objetos", "Objetos que fazem sombra nos módulos; o primeiro é a árvore.", "objetos",
                [
                    B("Árvore", PluginInfo.ComandoArvore, "Árvore como um pirulito: tronco e copa cilíndricos, com altura e largura de cada um. Clique onde pôr; o pé fica no terreno e acompanha o terreno quando a árvore é arrastada.", "arvore"),
                ]),
                B("Sombras", PluginInfo.ComandoSombras, "Escolhe o dia e o horário (ou um dia, um mês, um ano inteiro) e desenha a sombra dos objetos no terreno, marcando os módulos que ela pega; no período, o pior caso.", "sombras", grande: true),
            ]),
            new("Saída",
            [
                B("PVsyst", PluginInfo.ComandoExportar, "Exporta as faces dos módulos escolhidos para o PVsyst (DAE).", "pvsyst", grande: true),
                B("Excel", PluginInfo.ComandoExportarExcel, "Exporta para o Excel o resumo, as quantificações das análises e os pilares.", "excel", grande: true),
                B("3D", PluginInfo.ComandoVer3D, "Abre no navegador um modelo 3D para girar e dar zoom: terreno, mesas com as cores dos tipos, pilares e árvores. Funciona sem internet.", "ver3d", grande: true),
            ]),
        ]),
    ];

    /// <summary>Todos os botões de todas as abas.</summary>
    public static IEnumerable<RibbonButtonSpec> AllButtons => Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Buttons);
}
