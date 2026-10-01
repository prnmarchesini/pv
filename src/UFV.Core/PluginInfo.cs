namespace UFV.Core;

/// <summary>
/// Identificacao do plugin. Mora no Core, e nao no Plugin, para a mensagem que
/// o usuario le poder ser testada sem abrir o AutoCAD.
/// </summary>
public static class PluginInfo
{
    /// <summary>Nome do plugin como aparece para o usuario.</summary>
    public const string Nome = "Plugin UFV";

    /// <summary>
    /// Nome do comando de prova de vida, na linha de comando do Civil 3D.
    ///
    /// E const, e nao so um texto solto, porque tres lugares precisam dizer
    /// exatamente o mesmo nome: o atributo CommandMethod que registra o
    /// comando, o botao da ribbon que o dispara e o script do Core Console.
    /// Sendo uma constante, renomear o comando arrasta o botao junto.
    /// </summary>
    public const string ComandoOla = "UFV_OLA";

    /// <summary>
    /// Comando que lista as superfícies do desenho para o usuário escolher
    /// qual é o terreno. Mesma razão de ser constante que <see cref="ComandoOla"/>.
    /// </summary>
    public const string ComandoTerreno = "UFV_TERRENO";

    /// <summary>
    /// Comando que processa a superfície sem perguntar nada, escolhendo a
    /// primeira aproveitável. Existe para o teste de nível 2 poder exercitar
    /// o processamento sem ninguém clicar — o comando do produto continua
    /// exigindo que o usuário confirme.
    /// </summary>
    public const string ComandoTerrenoAutomatico = "UFV_TERRENO_AUTO";

    /// <summary>
    /// Comando que diz se o terreno gravado no desenho ainda corresponde à
    /// superfície como ela está agora.
    /// </summary>
    public const string ComandoTerrenoStatus = "UFV_TERRENO_STATUS";

    /// <summary>
    /// Comando que responde X, Y e Z de um ponto clicado no terreno.
    /// </summary>
    public const string ComandoCoordenada = "UFV_COORD";

    /// <summary>
    /// Comando que define ou corrige a latitude e a longitude do terreno.
    /// Sem ele, um sinal trocado ficaria gravado para sempre — e latitude
    /// trocada põe a usina no hemisfério errado.
    /// </summary>
    public const string ComandoLocalizacao = "UFV_LOCAL";

    /// <summary>
    /// Comando que traça a área de implantação e a assenta no terreno.
    /// </summary>
    public const string ComandoArea = "UFV_AREA";

    /// <summary>
    /// Comando que lista as áreas do desenho, lendo a identidade de cada
    /// polilinha. É por onde se confere que o GUID sobreviveu ao arquivo.
    /// </summary>
    public const string ComandoAreas = "UFV_AREAS";

    /// <summary>
    /// Comando que reconstrói o registro central a partir do XData das
    /// entidades. É o que reconhece uma área copiada de outro desenho.
    /// </summary>
    public const string ComandoReindexar = "UFV_REINDEXAR";

    /// <summary>
    /// Comando que abre a janela da mesa: os campos da estrutura, o
    /// comprimento que sai deles e a planta baixa.
    /// </summary>
    public const string ComandoMesa = "UFV_MESA";

    /// <summary>
    /// Comando que traça a linha de alinhamento e guarda de que lado ficam as
    /// mesas.
    /// </summary>
    public const string ComandoAlinhamento = "UFV_ALINHAMENTO";

    /// <summary>Comando que lista os alinhamentos do desenho.</summary>
    public const string ComandoAlinhamentos = "UFV_ALINHAMENTOS";

    /// <summary>
    /// Comando que abre a tela única de configuração: os limites do sistema e
    /// as regras de análise, gravados no desenho.
    /// </summary>
    public const string ComandoConfig = "UFV_CONFIG";

    /// <summary>
    /// Comando que escreve, campo a campo, a configuração gravada no desenho.
    /// É por onde o teste de nível 2 confere que salvar e reabrir preserva
    /// tudo.
    /// </summary>
    public const string ComandoConfigStatus = "UFV_CONFIG_STATUS";

    /// <summary>
    /// Comando que grava uma configuração de teste, toda diferente do padrão,
    /// sem perguntar nada. Existe para o Core Console, que não abre janela.
    /// </summary>
    public const string ComandoConfigTeste = "UFV_CONFIG_TESTE";

    /// <summary>
    /// Comando que processa e desenha uma fileira: escolhe área, alinhamento
    /// e o número da fileira, distribui, alinha, calcula os pilares e desenha.
    /// </summary>
    public const string ComandoFileira = "UFV_FILEIRA";

    /// <summary>
    /// Comando que processa a fileira 1 da primeira área com o primeiro
    /// alinhamento, sem perguntar. Existe para o teste de nível 2.
    /// </summary>
    public const string ComandoFileiraAutomatico = "UFV_FILEIRA_AUTO";

    /// <summary>Comando que liga e desliga os textos de altura dos pilares ("Mostrar alturas").</summary>
    public const string ComandoAlturas = "UFV_ALTURAS";

    /// <summary>Comando que processa e desenha a área inteira: todas as fileiras, com o tempo medido.</summary>
    public const string ComandoUsina = "UFV_USINA";

    /// <summary>Comando que processa a área inteira sem perguntar. Existe para o teste de nível 2.</summary>
    public const string ComandoUsinaAutomatico = "UFV_USINA_AUTO";

    /// <summary>
    /// Comando que exporta os módulos selecionados para o PVsyst: seleção,
    /// formato, arquivo.
    /// </summary>
    public const string ComandoExportar = "UFV_EXPORTAR";

    /// <summary>
    /// Comando que exporta TODOS os módulos do desenho em DAE para o caminho
    /// pedido na linha de comando, sem janela. Existe para o teste de nível 2.
    /// </summary>
    public const string ComandoExportarAutomatico = "UFV_EXPORTAR_AUTO";

    /// <summary>Comando que marca uma mesa como suja (precisa de recálculo) e a pinta de vermelho.</summary>
    public const string ComandoSujar = "UFV_SUJAR";

    /// <summary>Comando que suja a primeira mesa do desenho sem perguntar. Existe para o teste de nível 2.</summary>
    public const string ComandoSujarAutomatico = "UFV_SUJAR_AUTO";

    /// <summary>Comando que diz quantas mesas estão limpas e quais estão sujas, e por quê.</summary>
    public const string ComandoEstado = "UFV_ESTADO";

    /// <summary>Comando que apaga as mesas de uma área e as desenha de novo com a configuração atual.</summary>
    public const string ComandoRefazer = "UFV_REFAZER";

    /// <summary>Comando que refaz a primeira área com o primeiro alinhamento, sem perguntar. Para o nível 2.</summary>
    public const string ComandoRefazerAutomatico = "UFV_REFAZER_AUTO";

    /// <summary>Comando que recalcula uma mesa onde ela está (reamostra, refaz pilares e pontas baixas), mantendo o GUID.</summary>
    public const string ComandoRecalcular = "UFV_RECALCULAR";

    /// <summary>Comando que recalcula só as mesas sujas.</summary>
    public const string ComandoRecalcularSujas = "UFV_RECALCULAR_SUJAS";

    /// <summary>Comando que recalcula as sujas com a mesa de exemplo, sem perguntar. Para o nível 2.</summary>
    public const string ComandoRecalcularAutomatico = "UFV_RECALCULAR_AUTO";

    /// <summary>Comando que devolve ao padrão do plugin os blocos que chegaram de outro desenho com sufixo ($0$).</summary>
    public const string ComandoRenomear = "UFV_RENOMEAR";

    /// <summary>Comando que conta a usina como está no desenho (mesas, módulos, kWp, pilares) e consome as remoções registradas.</summary>
    public const string ComandoRecontar = "UFV_RECONTAR";

    /// <summary>Comando que confere registros, mesas, identidades e o carimbo da superfície, e diz o que achou.</summary>
    public const string ComandoValidar = "UFV_VALIDAR";

    /// <summary>Comando que diz mesas, módulos e kWp da seleção (a conta da caixa flutuante).</summary>
    public const string ComandoKwpSelecao = "UFV_KWP_SELECAO";

    /// <summary>Comando que cria um grupo com nome a partir da seleção.</summary>
    public const string ComandoGrupoCriar = "UFV_GRUPO_CRIAR";

    /// <summary>Comando que lista os grupos com mesas, módulos, pilares e kWp.</summary>
    public const string ComandoGrupos = "UFV_GRUPOS";

    /// <summary>Comando que recalcula as mesas de um grupo.</summary>
    public const string ComandoGrupoRecalcular = "UFV_GRUPO_RECALCULAR";

    /// <summary>Comando que põe as mesas de um grupo na seleção.</summary>
    public const string ComandoGrupoSelecionar = "UFV_GRUPO_SELECIONAR";

    /// <summary>Comando que apaga o registro de um grupo (as mesas ficam).</summary>
    public const string ComandoGrupoApagar = "UFV_GRUPO_APAGAR";

    /// <summary>Comando que abre o painel dos grupos.</summary>
    public const string ComandoGruposPainel = "UFV_GRUPOS_PAINEL";

    /// <summary>Comando que gera a numeração das mesas (F1.1, F1.2…) a partir da F1.1 e da última fileira.</summary>
    public const string ComandoNumerar = "UFV_NUMERAR";

    /// <summary>Comando que apaga e redesenha as cotas de altura de todas as mesas.</summary>
    public const string ComandoAlturasRegerar = "UFV_ALTURAS_REGERAR";

    /// <summary>Exporta terreno, configuração, perfil e mesas para a bancada do motor (diagnóstico).</summary>
    public const string ComandoBancada = "UFV_BANCADA";

    /// <summary>Muda a altura das pontas de uma mesa à mão (uma travada, ou as duas).</summary>
    public const string ComandoPontas = "UFV_PONTAS";

    /// <summary>O mesmo, por letreiro e alturas na linha de comando, sem clique (nível 2).</summary>
    public const string ComandoPontasAutomatico = "UFV_PONTAS_AUTO";

    /// <summary>A tela dos parâmetros das análises (faixa, lombo, degraus, declividade, cores).</summary>
    public const string ComandoAnalisesParametros = "UFV_ANALISES_PARAMETROS";

    /// <summary>Repinta todas as mesas como estão, com as regras gravadas.</summary>
    public const string ComandoPintar = "UFV_PINTAR";

    /// <summary>O mesmo com a mesa de exemplo (nível 2).</summary>
    public const string ComandoPintarAutomatico = "UFV_PINTAR_AUTO";

    /// <summary>Refaz todas as áreas com a configuração atual.</summary>
    public const string ComandoRegerar = "UFV_REGERAR";

    /// <summary>A análise de declividade: seta e valor em cada mesa, em porcentagem ou graus, ou desligada.</summary>
    public const string ComandoDeclividade = "UFV_DECLIVIDADE";

    /// <summary>Apaga tudo que o plugin desenhou dentro de uma área (a área e o alinhamento ficam).</summary>
    public const string ComandoApagarTudo = "UFV_APAGAR_TUDO";

    /// <summary>O mesmo na primeira área registrada, sem clique (nível 2).</summary>
    public const string ComandoApagarTudoAutomatico = "UFV_APAGAR_TUDO_AUTO";

    /// <summary>
    /// Prefixo de tudo que o plugin grava com nome próprio: XData, dicionário
    /// do desenho, dados pendurados no documento (ver 02-arquitetura.md).
    /// </summary>
    public const string PrefixoDeDados = "MARCHENG_UFV";

    /// <summary>
    /// Prefixo de todos os comandos do plugin. O vigia (7.2) usa para saber
    /// que o comando que está rodando é nosso e ficar calado.
    /// </summary>
    public const string PrefixoDeComando = "UFV_";

    /// <summary>
    /// Os comandos de desfazer e refazer do AutoCAD. Durante eles o vigia não
    /// suja mesa: o banco volta a um estado que já foi decidido.
    /// </summary>
    public static readonly IReadOnlyList<string> ComandosDeDesfazer = ["U", "UNDO", "REDO", "MREDO", "OOPS"];

    /// <summary>Se é um comando de desfazer/refazer (nome global, sem distinguir maiúsculas).</summary>
    public static bool IsUndoCommand(string? globalName) =>
        globalName is not null && ComandosDeDesfazer.Contains(globalName.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Se o vigia fica calado durante este comando: os nossos (UFV_*) e os de desfazer.</summary>
    public static bool IsSilencedCommand(string? globalName) =>
        globalName is not null
        && (globalName.TrimStart().StartsWith(PrefixoDeComando, StringComparison.OrdinalIgnoreCase) || IsUndoCommand(globalName));

    /// <summary>Texto usado quando a versao nao pode ser lida da assembly.</summary>
    public const string VersaoDesconhecida = "desconhecida";

    /// <summary>
    /// A linha que o comando UFV_OLA escreve na linha de comando do Civil 3D.
    /// </summary>
    public static string MensagemDeApresentacao(string? versao) =>
        $"{Nome} carregado, versão {VersaoLegivel(versao)}";

    /// <summary>
    /// A versão como o usuário lê: sem o sufixo de build que o SDK acrescenta.
    ///
    /// O sufixo continua valendo onde ele serve — gravado no carimbo do
    /// desenho, ele identifica exatamente qual build processou o terreno.
    /// Numa mensagem de tela, é ruído.
    /// </summary>
    public static string VersaoLegivel(string? versao) => Limpar(versao);

    /// <summary>
    /// Tira o sufixo de build que o SDK acrescenta a versao informativa
    /// ("0.1.0+3f2a1c9" vira "0.1.0") e apara espaco em volta.
    ///
    /// Versao ausente, em branco ou que se resume ao sufixo (um
    /// AssemblyInformationalVersion malformado como "+abc") vira
    /// "desconhecida". Melhor a palavra do que a frase terminando no vazio.
    /// </summary>
    private static string Limpar(string? versao)
    {
        if (string.IsNullOrWhiteSpace(versao)) return VersaoDesconhecida;

        var texto = versao.Trim();

        var mais = texto.IndexOf('+');
        if (mais >= 0) texto = texto[..mais].Trim();

        return texto.Length == 0 ? VersaoDesconhecida : texto;
    }

    /// <summary>Passo 8.9 a 8.11: insere os textos de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaInserir = "UFV_AN_PB_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaAnalisar = "UFV_AN_PB_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaAnalisarAutomatico = "UFV_AN_PB_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaApagar = "UFV_AN_PB_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaTirarCores = "UFV_AN_PB_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaQuantificar = "UFV_AN_PB_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de ponta alta.</summary>
    public const string ComandoAnPontaAltaInserir = "UFV_AN_PA_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaAnalisar = "UFV_AN_PA_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaAnalisarAutomatico = "UFV_AN_PA_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de ponta alta.</summary>
    public const string ComandoAnPontaAltaApagar = "UFV_AN_PA_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaTirarCores = "UFV_AN_PA_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaQuantificar = "UFV_AN_PA_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de declividade.</summary>
    public const string ComandoAnDeclividadeInserir = "UFV_AN_DECL_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de declividade.</summary>
    public const string ComandoAnDeclividadeAnalisar = "UFV_AN_DECL_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de declividade.</summary>
    public const string ComandoAnDeclividadeAnalisarAutomatico = "UFV_AN_DECL_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de declividade.</summary>
    public const string ComandoAnDeclividadeApagar = "UFV_AN_DECL_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de declividade.</summary>
    public const string ComandoAnDeclividadeTirarCores = "UFV_AN_DECL_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de declividade.</summary>
    public const string ComandoAnDeclividadeQuantificar = "UFV_AN_DECL_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de pilar acima do terreno.</summary>
    public const string ComandoAnPilarInserir = "UFV_AN_PILAR_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarAnalisar = "UFV_AN_PILAR_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarAnalisarAutomatico = "UFV_AN_PILAR_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de pilar acima do terreno.</summary>
    public const string ComandoAnPilarApagar = "UFV_AN_PILAR_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarTirarCores = "UFV_AN_PILAR_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarQuantificar = "UFV_AN_PILAR_QUANTIFICAR";

    /// <summary>Só para o nível 2: a análise da ponta baixa com uma regra que pega tudo, pintando também os módulos.</summary>
    public const string ComandoAnTestePecas = "UFV_AN_TESTE_PECAS";

    /// <summary>Passo 8.12: exporta para o Excel o resumo, as quantificações das análises e os pilares.</summary>
    public const string ComandoExportarExcel = "UFV_EXCEL";

    /// <summary>O mesmo, com o caminho na linha de comando. Para o nível 2.</summary>
    public const string ComandoExportarExcelAutomatico = "UFV_EXCEL_AUTO";

    /// <summary>Passo 8.15: o resumo do terreno (superfície, área, cidade, país e fuso UTM).</summary>
    public const string ComandoTerrenoResumo = "UFV_TERRENO_RESUMO";

    /// <summary>Passo 8.13: escolher os estilos de texto, cota e chamada que o plugin usa.</summary>
    public const string ComandoEstilos = "UFV_ESTILOS";

    /// <summary>O mesmo pela linha de comando (texto, cota, chamada; "-" é o corrente). Para o nível 2.</summary>
    public const string ComandoEstilosAutomatico = "UFV_ESTILOS_AUTO";
}
