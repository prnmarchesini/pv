namespace UFV.Core;

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
/// Edição compacta num menu. Uma aba só, a UFV (Renan, 02/10/2026: "eu quero
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
        new("UFV_RIBBON_TAB", "UFV",
        [
            new("Configurações",
            [
                B("Configurações", PluginInfo.ComandoConfiguracoes,
                    "Estruturas (mesas do desenho, vãos, enterro), escolha das mesas e cores, parâmetros da usina e estilos do projeto.",
                    "Configuracao", grande: true),
            ]),
            new("Terreno",
            [
                B("Terreno", PluginInfo.ComandoTerreno, "Escolhe ou troca a superfície do desenho que é o terreno.", "Terreno", grande: true),
                B("Resumo", PluginInfo.ComandoTerrenoResumo, "Qual terreno está escolhido, a área, a cidade, o país e o fuso UTM SIRGAS 2000; ali também se corrige a localização.", "Status"),
                B("Coordenada", PluginInfo.ComandoCoordenada, "Clica num ponto e responde X, Y e Z do terreno.", "Coordenada"),
            ]),
            new("Implantação",
            [
                B("Área", PluginInfo.ComandoArea, "Traça a área de implantação e a assenta no terreno.", "Area", grande: true),
                B("Alinhamento", PluginInfo.ComandoAlinhamento, "Traça a linha onde as fileiras nascem e escolhe o lado delas.", "Alinhamento", grande: true),
            ]),
            new("Processar",
            [
                B("Usina", PluginInfo.ComandoUsina, "Gera a área inteira com as mesas em uso, sem análise (as análises ficam no painel Análises).", "Usina", grande: true),
                B("Regerar área", PluginInfo.ComandoRefazer, "Apaga e gera de novo a área com a configuração atual.", "Refazer"),
                B("Regerar todas as áreas", PluginInfo.ComandoRegerar, "Refaz todas as áreas do desenho com a configuração atual.", "RegerarTudo"),
            ]),
            new("Edição",
            [
                new RibbonMenuSpec("Edição", "Recalcular, pontas à mão, validar, recontar e os outros ajustes de mesas já desenhadas.", "Recalcular",
                [
                    B("Recalcular", PluginInfo.ComandoRecalcular, "Refaz a mesa escolhida no terreno de onde ela está, com o mesmo GUID.", "Recalcular"),
                    B("Recalcular sujas", PluginInfo.ComandoRecalcularSujas, "Refaz só as mesas marcadas como sujas (movidas, copiadas).", "RecalcularSujas"),
                    B("Pontas", PluginInfo.ComandoPontas, "Escolhe à mão a altura da ponta baixa no primeiro e no último pilar da mesa.", "Pontas"),
                    B("Validar", PluginInfo.ComandoValidar, "Confere o desenho: registros sumidos, sujas, duplicadas, órfãs, carimbo do terreno.", "Validar"),
                    B("Recontar", PluginInfo.ComandoRecontar, "Refaz a contagem de mesas, módulos, pilares e kWp pelo que está no desenho.", "Recontar"),
                    B("Renomear blocos", PluginInfo.ComandoRenomear, "Devolve aos blocos copiados o nome do padrão do plugin.", "Renomear"),
                    B("Estado", PluginInfo.ComandoEstado, "Lista as mesas sujas e o motivo.", "Estado"),
                    B("Sujar", PluginInfo.ComandoSujar, "Marca a mesa escolhida como suja, para recalcular depois.", "Sujar"),
                ]),
                B("Grupos", PluginInfo.ComandoGruposPainel, "Abre o painel de grupos: mesas, módulos, pilares e kWp de cada grupo.", "Grupos", grande: true),
            ]),
            new("Análises",
            [
                B("Análises", PluginInfo.ComandoAnalises,
                    "Abre a janela das análises, uma aba para cada: ponta baixa, ponta alta, declividade e pilares (inserir, analisar com cores, apagar, quantificar) e as quantidades para o Excel.",
                    "PintarEstouros", grande: true),
            ]),
            new("Tags",
            [
                B("Tags", PluginInfo.ComandoTags,
                    "Abre a janela das tags: numerar fileiras e mesas, e as tags de fileiras, mesas, módulos e strings (inserir e apagar).",
                    "Numerar", grande: true),
            ]),
            new("Saída",
            [
                B("PVsyst", PluginInfo.ComandoExportar, "Exporta as faces dos módulos escolhidos para o PVsyst (DAE).", "Exportar", grande: true),
                B("Excel", PluginInfo.ComandoExportarExcel, "Exporta para o Excel o resumo, as quantificações das análises e os pilares.", "Exportar", grande: true),
            ]),
        ]),
    ];

    /// <summary>Todos os botões de todas as abas.</summary>
    public static IEnumerable<RibbonButtonSpec> AllButtons => Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Buttons);
}
