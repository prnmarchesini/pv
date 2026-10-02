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
/// Edição compacta num menu, e as abas de Análises e Tags. O plugin só monta.
/// </summary>
public static class RibbonLayout
{
    private static RibbonButtonSpec B(string texto, string comando, string dica, string icone, bool grande = false) =>
        new(texto, comando, dica, icone, grande);

    private static RibbonPanelSpec Analise(string titulo, string nome, string icone,
        string inserir, string analisar, string apagar, string cores, string quantificar) =>
        new(titulo,
        [
            B("Inserir", inserir, $"Escreve {nome} em todas as mesas (os textos desta análise que já existiam saem antes).", icone, grande: true),
            B("Analisar", analisar, $"Abre a regra (abaixo de X uma cor, acima de Y outra; só textos ou também as peças) e pinta {nome}.", "PintarEstouros"),
            B("Apagar textos", apagar, $"Apaga os textos de {nome}; as outras análises ficam.", "Sujar"),
            B("Tirar cores", cores, $"Volta à cor da camada o que esta análise pintou ({nome}); as cores das outras ficam.", "Estado"),
            B("Quantificar", quantificar, $"Conta quantos ficaram abaixo, dentro e acima da faixa ({nome}); vai para o Excel.", "Recontar"),
        ]);

    private static RibbonPanelSpec Tag(string titulo, string nome, string inserir, string apagar) =>
        new(titulo,
        [
            B("Inserir", inserir, $"Escreve as tags de {nome} (as que já existiam saem antes).", "Numerar", grande: true),
            B("Apagar", apagar, $"Apaga as tags de {nome}; as outras tags ficam.", "Sujar"),
        ]);

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
                B("Resumo", PluginInfo.ComandoTerrenoResumo, "Qual terreno está escolhido, a área, a cidade, o país e o fuso UTM SIRGAS 2000.", "Status"),
                B("Coordenada", PluginInfo.ComandoCoordenada, "Clica num ponto e responde X, Y e Z do terreno.", "Coordenada"),
                B("Localização", PluginInfo.ComandoLocalizacao, "Informa ou corrige a latitude e a longitude do terreno.", "Coordenada"),
            ]),
            new("Implantação",
            [
                B("Área", PluginInfo.ComandoArea, "Traça a área de implantação e a assenta no terreno.", "Area", grande: true),
                B("Alinhamento", PluginInfo.ComandoAlinhamento, "Traça a linha onde as fileiras nascem e escolhe o lado delas.", "Alinhamento", grande: true),
            ]),
            new("Processar",
            [
                B("Usina", PluginInfo.ComandoUsina, "Gera a área inteira com as mesas em uso, sem análise (as análises ficam na aba UFV Análises).", "Usina", grande: true),
                B("Fileira", PluginInfo.ComandoFileira, "Gera uma fileira só, para conferir antes da usina.", "Fileira"),
                B("Refazer", PluginInfo.ComandoRefazer, "Apaga e gera de novo a área com a configuração atual.", "Refazer"),
                B("Regerar áreas", PluginInfo.ComandoRegerar, "Refaz todas as áreas do desenho com a configuração atual.", "RegerarTudo"),
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
                B("Numerar", PluginInfo.ComandoNumerar, "Numera as fileiras e as mesas (F1, F1.1...) a partir da primeira e da última que você indicar.", "Numerar", grande: true),
            ]),
            new("Saída",
            [
                B("PVsyst", PluginInfo.ComandoExportar, "Exporta as faces dos módulos escolhidos para o PVsyst (DAE).", "Exportar", grande: true),
                B("Excel", PluginInfo.ComandoExportarExcel, "Exporta para o Excel o resumo, as quantificações das análises e os pilares.", "Exportar", grande: true),
            ]),
        ]),
        new("UFV_RIBBON_ANALISES", "UFV Análises",
        [
            Analise("Ponta baixa", "a altura da ponta baixa (PB)", "Alturas",
                PluginInfo.ComandoAnPontaBaixaInserir, PluginInfo.ComandoAnPontaBaixaAnalisar, PluginInfo.ComandoAnPontaBaixaApagar,
                PluginInfo.ComandoAnPontaBaixaTirarCores, PluginInfo.ComandoAnPontaBaixaQuantificar),
            Analise("Ponta alta", "a altura da ponta alta (PA)", "RegerarAlturas",
                PluginInfo.ComandoAnPontaAltaInserir, PluginInfo.ComandoAnPontaAltaAnalisar, PluginInfo.ComandoAnPontaAltaApagar,
                PluginInfo.ComandoAnPontaAltaTirarCores, PluginInfo.ComandoAnPontaAltaQuantificar),
            Analise("Declividade", "a declividade das mesas", "Declividade",
                PluginInfo.ComandoAnDeclividadeInserir, PluginInfo.ComandoAnDeclividadeAnalisar, PluginInfo.ComandoAnDeclividadeApagar,
                PluginInfo.ComandoAnDeclividadeTirarCores, PluginInfo.ComandoAnDeclividadeQuantificar),
            Analise("Pilares", "o comprimento dos pilares acima do terreno", "Pontas",
                PluginInfo.ComandoAnPilarInserir, PluginInfo.ComandoAnPilarAnalisar, PluginInfo.ComandoAnPilarApagar,
                PluginInfo.ComandoAnPilarTirarCores, PluginInfo.ComandoAnPilarQuantificar),
            new("Quantidades",
            [
                B("Excel", PluginInfo.ComandoExportarExcel, "Exporta para o Excel as quantificações feitas e o quantitativo de módulos, mesas e pilares.", "Exportar", grande: true),
            ]),
        ]),
        new("UFV_RIBBON_TAGS", "UFV Tags",
        [
            Tag("Fileiras", "fileiras (F1, F2...)", PluginInfo.ComandoTagFileirasInserir, PluginInfo.ComandoTagFileirasApagar),
            Tag("Mesas", "mesas (F1.1, F1.2...)", PluginInfo.ComandoTagMesasInserir, PluginInfo.ComandoTagMesasApagar),
            Tag("Módulos", "módulos (o número de cada um na mesa)", PluginInfo.ComandoTagModulosInserir, PluginInfo.ComandoTagModulosApagar),
            Tag("Strings", "strings (S1, S2...; pergunta os módulos por string)", PluginInfo.ComandoTagStringsInserir, PluginInfo.ComandoTagStringsApagar),
            new("Projeto",
            [
                B("Estilos", PluginInfo.ComandoEstilos, "Escolhe os estilos (anotativos) de texto, cota e chamada que o plugin usa.", "Configuracao", grande: true),
            ]),
        ]),
    ];

    /// <summary>Todos os botões de todas as abas.</summary>
    public static IEnumerable<RibbonButtonSpec> AllButtons => Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Buttons);
}
