namespace Clivus.Core;

/// <summary>
/// Identificacao do plugin. Mora no Core, e nao no Plugin, para a mensagem que
/// o usuario le poder ser testada sem abrir o AutoCAD.
/// </summary>
public static class PluginInfo
{
    /// <summary>Nome do plugin como aparece para o usuario.</summary>
    public const string Nome = "Clivus Solar";

    /// <summary>
    /// Nome do comando de prova de vida, na linha de comando do Civil 3D.
    ///
    /// E const, e nao so um texto solto, porque tres lugares precisam dizer
    /// exatamente o mesmo nome: o atributo CommandMethod que registra o
    /// comando, o botao da ribbon que o dispara e o script do Core Console.
    /// Sendo uma constante, renomear o comando arrasta o botao junto.
    /// </summary>
    public const string ComandoOla = "CLIVUS_OLA";

    /// <summary>
    /// Comando que lista as superfícies do desenho para o usuário escolher
    /// qual é o terreno. Mesma razão de ser constante que <see cref="ComandoOla"/>.
    /// </summary>
    public const string ComandoTerreno = "CLIVUS_TERRENO";

    /// <summary>
    /// Comando que processa a superfície sem perguntar nada, escolhendo a
    /// primeira aproveitável. Existe para o teste de nível 2 poder exercitar
    /// o processamento sem ninguém clicar — o comando do produto continua
    /// exigindo que o usuário confirme.
    /// </summary>
    public const string ComandoTerrenoAutomatico = "CLIVUS_TERRENO_AUTO";

    /// <summary>
    /// Comando que diz se o terreno gravado no desenho ainda corresponde à
    /// superfície como ela está agora.
    /// </summary>
    public const string ComandoTerrenoStatus = "CLIVUS_TERRENO_STATUS";

    /// <summary>
    /// Comando que responde X, Y e Z de um ponto clicado no terreno.
    /// </summary>
    public const string ComandoCoordenada = "CLIVUS_COORD";

    /// <summary>
    /// Comando que define ou corrige a latitude e a longitude do terreno.
    /// Sem ele, um sinal trocado ficaria gravado para sempre — e latitude
    /// trocada põe a usina no hemisfério errado.
    /// </summary>
    public const string ComandoLocalizacao = "CLIVUS_LOCAL";

    /// <summary>
    /// Comando que traça a área de implantação e a assenta no terreno.
    /// </summary>
    public const string ComandoArea = "CLIVUS_AREA";

    /// <summary>
    /// Comando que lista as áreas do desenho, lendo a identidade de cada
    /// polilinha. É por onde se confere que o GUID sobreviveu ao arquivo.
    /// </summary>
    public const string ComandoAreas = "CLIVUS_AREAS";

    /// <summary>
    /// Comando que reconstrói o registro central a partir do XData das
    /// entidades. É o que reconhece uma área copiada de outro desenho.
    /// </summary>
    public const string ComandoReindexar = "CLIVUS_REINDEXAR";

    /// <summary>
    /// Comando que abre a janela da mesa: os campos da estrutura, o
    /// comprimento que sai deles e a planta baixa.
    /// </summary>
    public const string ComandoMesa = "CLIVUS_MESA";

    /// <summary>
    /// Comando que traça a linha de alinhamento e guarda de que lado ficam as
    /// mesas.
    /// </summary>
    public const string ComandoAlinhamento = "CLIVUS_ALINHAMENTO";

    /// <summary>Comando que lista os alinhamentos do desenho.</summary>
    public const string ComandoAlinhamentos = "CLIVUS_ALINHAMENTOS";

    /// <summary>
    /// Comando que abre a tela única de configuração: os limites do sistema e
    /// as regras de análise, gravados no desenho.
    /// </summary>
    public const string ComandoConfig = "CLIVUS_CONFIG";

    /// <summary>
    /// Comando que escreve, campo a campo, a configuração gravada no desenho.
    /// É por onde o teste de nível 2 confere que salvar e reabrir preserva
    /// tudo.
    /// </summary>
    public const string ComandoConfigStatus = "CLIVUS_CONFIG_STATUS";

    /// <summary>
    /// Comando que grava uma configuração de teste, toda diferente do padrão,
    /// sem perguntar nada. Existe para o Core Console, que não abre janela.
    /// </summary>
    public const string ComandoConfigTeste = "CLIVUS_CONFIG_TESTE";

    /// <summary>
    /// Comando que processa e desenha uma fileira: escolhe área, alinhamento
    /// e o número da fileira, distribui, alinha, calcula os pilares e desenha.
    /// </summary>
    public const string ComandoFileira = "CLIVUS_FILEIRA";

    /// <summary>
    /// Comando que processa a fileira 1 da primeira área com o primeiro
    /// alinhamento, sem perguntar. Existe para o teste de nível 2.
    /// </summary>
    public const string ComandoFileiraAutomatico = "CLIVUS_FILEIRA_AUTO";

    /// <summary>Comando que liga e desliga os textos de altura dos pilares ("Mostrar alturas").</summary>
    public const string ComandoAlturas = "CLIVUS_ALTURAS";

    /// <summary>Comando que processa e desenha a área inteira: todas as fileiras, com o tempo medido.</summary>
    public const string ComandoUsina = "CLIVUS_USINA";

    /// <summary>Comando que processa a área inteira sem perguntar. Existe para o teste de nível 2.</summary>
    public const string ComandoUsinaAutomatico = "CLIVUS_USINA_AUTO";

    /// <summary>
    /// Comando que exporta os módulos selecionados para o PVsyst: seleção,
    /// formato, arquivo.
    /// </summary>
    public const string ComandoExportar = "CLIVUS_EXPORTAR";

    /// <summary>
    /// Comando que exporta TODOS os módulos do desenho em DAE para o caminho
    /// pedido na linha de comando, sem janela. Existe para o teste de nível 2.
    /// </summary>
    public const string ComandoExportarAutomatico = "CLIVUS_EXPORTAR_AUTO";

    /// <summary>Comando que marca uma mesa como suja (precisa de recálculo) e a pinta de vermelho.</summary>
    public const string ComandoSujar = "CLIVUS_SUJAR";

    /// <summary>Comando que suja a primeira mesa do desenho sem perguntar. Existe para o teste de nível 2.</summary>
    public const string ComandoSujarAutomatico = "CLIVUS_SUJAR_AUTO";

    /// <summary>Comando que diz quantas mesas estão limpas e quais estão sujas, e por quê.</summary>
    public const string ComandoEstado = "CLIVUS_ESTADO";

    /// <summary>Comando que apaga as mesas de uma área e as desenha de novo com a configuração atual.</summary>
    public const string ComandoRefazer = "CLIVUS_REFAZER";

    /// <summary>Comando que refaz a primeira área com o primeiro alinhamento, sem perguntar. Para o nível 2.</summary>
    public const string ComandoRefazerAutomatico = "CLIVUS_REFAZER_AUTO";

    /// <summary>Comando que recalcula uma mesa onde ela está (reamostra, refaz pilares e pontas baixas), mantendo o GUID.</summary>
    public const string ComandoRecalcular = "CLIVUS_RECALCULAR";

    /// <summary>Comando que recalcula só as mesas sujas.</summary>
    public const string ComandoRecalcularSujas = "CLIVUS_RECALCULAR_SUJAS";

    /// <summary>Comando que recalcula as sujas com a mesa de exemplo, sem perguntar. Para o nível 2.</summary>
    public const string ComandoRecalcularAutomatico = "CLIVUS_RECALCULAR_AUTO";

    /// <summary>Comando que devolve ao padrão do plugin os blocos que chegaram de outro desenho com sufixo ($0$).</summary>
    public const string ComandoRenomear = "CLIVUS_RENOMEAR";

    /// <summary>Comando que conta a usina como está no desenho (mesas, módulos, kWp, pilares) e consome as remoções registradas.</summary>
    public const string ComandoRecontar = "CLIVUS_RECONTAR";

    /// <summary>Comando que confere registros, mesas, identidades e o carimbo da superfície, e diz o que achou.</summary>
    public const string ComandoValidar = "CLIVUS_VALIDAR";

    /// <summary>Comando que diz mesas, módulos e kWp da seleção (a conta da caixa flutuante).</summary>
    public const string ComandoKwpSelecao = "CLIVUS_KWP_SELECAO";

    /// <summary>Comando que cria um grupo com nome a partir da seleção.</summary>
    public const string ComandoGrupoCriar = "CLIVUS_GRUPO_CRIAR";

    /// <summary>Comando que lista os grupos com mesas, módulos, pilares e kWp.</summary>
    public const string ComandoGrupos = "CLIVUS_GRUPOS";

    /// <summary>Comando que recalcula as mesas de um grupo.</summary>
    public const string ComandoGrupoRecalcular = "CLIVUS_GRUPO_RECALCULAR";

    /// <summary>Comando que põe as mesas de um grupo na seleção.</summary>
    public const string ComandoGrupoSelecionar = "CLIVUS_GRUPO_SELECIONAR";

    /// <summary>Comando que apaga o registro de um grupo (as mesas ficam).</summary>
    public const string ComandoGrupoApagar = "CLIVUS_GRUPO_APAGAR";

    /// <summary>Comando que abre o painel dos grupos.</summary>
    public const string ComandoGruposPainel = "CLIVUS_GRUPOS_PAINEL";

    /// <summary>Comando que gera a numeração das mesas (F1.1, F1.2…) a partir da F1.1 e da última fileira.</summary>
    public const string ComandoNumerar = "CLIVUS_NUMERAR";

    /// <summary>Comando que apaga e redesenha as cotas de altura de todas as mesas.</summary>
    public const string ComandoAlturasRegerar = "CLIVUS_ALTURAS_REGERAR";

    /// <summary>Exporta terreno, configuração, perfil e mesas para a bancada do motor (diagnóstico).</summary>
    public const string ComandoBancada = "CLIVUS_BANCADA";

    /// <summary>Muda a altura das pontas de uma mesa à mão (uma travada, ou as duas).</summary>
    public const string ComandoPontas = "CLIVUS_PONTAS";

    /// <summary>O mesmo, por letreiro e alturas na linha de comando, sem clique (nível 2).</summary>
    public const string ComandoPontasAutomatico = "CLIVUS_PONTAS_AUTO";

    /// <summary>A tela dos parâmetros das análises (faixa, lombo, degraus, declividade, cores).</summary>
    public const string ComandoAnalisesParametros = "CLIVUS_ANALISES_PARAMETROS";

    /// <summary>Repinta todas as mesas como estão, com as regras gravadas.</summary>
    public const string ComandoPintar = "CLIVUS_PINTAR";

    /// <summary>O mesmo com a mesa de exemplo (nível 2).</summary>
    public const string ComandoPintarAutomatico = "CLIVUS_PINTAR_AUTO";

    /// <summary>Refaz todas as áreas com a configuração atual.</summary>
    public const string ComandoRegerar = "CLIVUS_REGERAR";

    /// <summary>Passa o desenho feito com o nome antigo para o Clivus Solar. Roda sozinho ao abrir.</summary>
    public const string ComandoMigrar = "CLIVUS_MIGRAR";

    /// <summary>Troca a mesa clicada por uma ou mais de outro tipo, travando um lado (9.2).</summary>
    public const string ComandoTrocarMesa = "CLIVUS_TROCAR_MESA";

    /// <summary>O mesmo pela linha de comando: letreiro, tipo, quantidade e lado. Para o nível 2.</summary>
    public const string ComandoTrocarMesaAutomatico = "CLIVUS_TROCAR_MESA_AUTO";

    /// <summary>Apaga e gera de novo só a fileira da mesa clicada (9.3).</summary>
    public const string ComandoRegerarFileira = "CLIVUS_REGERAR_FILEIRA";

    /// <summary>O mesmo pelo letreiro, sem clique. Para o nível 2.</summary>
    public const string ComandoRegerarFileiraAutomatico = "CLIVUS_REGERAR_FILEIRA_AUTO";

    /// <summary>Insere árvores (tronco e copa cilíndricos) clicando no desenho; o pé no terreno (9.4).</summary>
    public const string ComandoArvore = "CLIVUS_ARVORE";

    /// <summary>O mesmo com as medidas e os pontos pela linha de comando. Para o nível 2.</summary>
    public const string ComandoArvoreAutomatico = "CLIVUS_ARVORE_AUTO";

    /// <summary>Janela das sombras: instante, dia, mês ou ano; desenha a sombra e marca os módulos (9.7, 9.8).</summary>
    public const string ComandoSombras = "CLIVUS_SOMBRAS";

    /// <summary>Sombras pela linha de comando (data, hora ou período). Para o nível 2.</summary>
    public const string ComandoSombrasAutomatico = "CLIVUS_SOMBRAS_AUTO";

    /// <summary>Apaga as sombras desenhadas e tira as marcas dos módulos.</summary>
    public const string ComandoSombrasApagar = "CLIVUS_SOMBRAS_APAGAR";

    /// <summary>Grava a página 3D (terreno, mesas, árvores) e abre no navegador (9.9).</summary>
    public const string ComandoVer3D = "CLIVUS_3D";

    /// <summary>Grava a página 3D sem abrir o navegador, no caminho dado. Para o nível 2.</summary>
    public const string ComandoVer3DAutomatico = "CLIVUS_3D_AUTO";

    /// <summary>A análise de declividade: seta e valor em cada mesa, em porcentagem ou graus, ou desligada.</summary>
    public const string ComandoDeclividade = "CLIVUS_DECLIVIDADE";

    /// <summary>Apaga tudo que o plugin desenhou dentro de uma área (a área e o alinhamento ficam).</summary>
    public const string ComandoApagarTudo = "CLIVUS_APAGAR_TUDO";

    /// <summary>O mesmo na primeira área registrada, sem clique (nível 2).</summary>
    public const string ComandoApagarTudoAutomatico = "CLIVUS_APAGAR_TUDO_AUTO";

    /// <summary>
    /// Prefixo de tudo que o plugin grava com nome próprio: XData, dicionário
    /// do desenho, dados pendurados no documento (ver 02-arquitetura.md).
    /// </summary>
    public const string PrefixoDeDados = "CLIVUS";

    /// <summary>A pasta do plugin em %LOCALAPPDATA% (perfis de mesa, log, bancada).</summary>
    public const string PastaDoUsuario = "Clivus Solar";


    /// <summary>
    /// Prefixo de todos os comandos do plugin. O vigia (7.2) usa para saber
    /// que o comando que está rodando é nosso e ficar calado.
    /// </summary>
    public const string PrefixoDeComando = "CLIVUS_";

    /// <summary>
    /// Os comandos de desfazer e refazer do AutoCAD. Durante eles o vigia não
    /// suja mesa: o banco volta a um estado que já foi decidido.
    /// </summary>
    public static readonly IReadOnlyList<string> ComandosDeDesfazer = ["U", "UNDO", "REDO", "MREDO", "OOPS"];

    /// <summary>Se é um comando de desfazer/refazer (nome global, sem distinguir maiúsculas).</summary>
    public static bool IsUndoCommand(string? globalName) =>
        globalName is not null && ComandosDeDesfazer.Contains(globalName.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Se o vigia fica calado durante este comando: os nossos (CLIVUS_*) e os de desfazer.</summary>
    public static bool IsSilencedCommand(string? globalName) =>
        globalName is not null
        && (globalName.TrimStart().StartsWith(PrefixoDeComando, StringComparison.OrdinalIgnoreCase) || IsUndoCommand(globalName));

    /// <summary>Texto usado quando a versao nao pode ser lida da assembly.</summary>
    public const string VersaoDesconhecida = "desconhecida";

    /// <summary>
    /// A linha que o comando CLIVUS_OLA escreve na linha de comando do Civil 3D.
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
    public const string ComandoAnPontaBaixaInserir = "CLIVUS_AN_PB_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaAnalisar = "CLIVUS_AN_PB_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaAnalisarAutomatico = "CLIVUS_AN_PB_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaApagar = "CLIVUS_AN_PB_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaTirarCores = "CLIVUS_AN_PB_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de ponta baixa.</summary>
    public const string ComandoAnPontaBaixaQuantificar = "CLIVUS_AN_PB_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de ponta alta.</summary>
    public const string ComandoAnPontaAltaInserir = "CLIVUS_AN_PA_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaAnalisar = "CLIVUS_AN_PA_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaAnalisarAutomatico = "CLIVUS_AN_PA_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de ponta alta.</summary>
    public const string ComandoAnPontaAltaApagar = "CLIVUS_AN_PA_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaTirarCores = "CLIVUS_AN_PA_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de ponta alta.</summary>
    public const string ComandoAnPontaAltaQuantificar = "CLIVUS_AN_PA_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de declividade.</summary>
    public const string ComandoAnDeclividadeInserir = "CLIVUS_AN_DECL_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de declividade.</summary>
    public const string ComandoAnDeclividadeAnalisar = "CLIVUS_AN_DECL_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de declividade.</summary>
    public const string ComandoAnDeclividadeAnalisarAutomatico = "CLIVUS_AN_DECL_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de declividade.</summary>
    public const string ComandoAnDeclividadeApagar = "CLIVUS_AN_DECL_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de declividade.</summary>
    public const string ComandoAnDeclividadeTirarCores = "CLIVUS_AN_DECL_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de declividade.</summary>
    public const string ComandoAnDeclividadeQuantificar = "CLIVUS_AN_DECL_QUANTIFICAR";

    /// <summary>Passo 8.9 a 8.11: insere os textos de pilar acima do terreno.</summary>
    public const string ComandoAnPilarInserir = "CLIVUS_AN_PILAR_INSERIR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra (abre a janela da regra) a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarAnalisar = "CLIVUS_AN_PILAR_ANALISAR";

    /// <summary>Passo 8.9 a 8.11: pinta pela regra gravada, sem janela (nível 2), a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarAnalisarAutomatico = "CLIVUS_AN_PILAR_ANALISAR_AUTO";

    /// <summary>Passo 8.9 a 8.11: apaga os textos de pilar acima do terreno.</summary>
    public const string ComandoAnPilarApagar = "CLIVUS_AN_PILAR_APAGAR";

    /// <summary>Passo 8.9 a 8.11: tira as cores da análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarTirarCores = "CLIVUS_AN_PILAR_CORES";

    /// <summary>Passo 8.9 a 8.11: conta, por faixa, a análise de pilar acima do terreno.</summary>
    public const string ComandoAnPilarQuantificar = "CLIVUS_AN_PILAR_QUANTIFICAR";

    /// <summary>02/10/2026: insere os textos da parte enterrada do pilar (E).</summary>
    public const string ComandoAnPilarEnterradoInserir = "CLIVUS_AN_PENT_INSERIR";

    /// <summary>02/10/2026: insere os textos do comprimento total do pilar (PT).</summary>
    public const string ComandoAnPilarTotalInserir = "CLIVUS_AN_PTOT_INSERIR";

    /// <summary>Só para o nível 2: a análise da ponta baixa com uma regra que pega tudo, pintando também os módulos.</summary>
    public const string ComandoAnTestePecas = "CLIVUS_AN_TESTE_PECAS";

    /// <summary>Passo 8.12: exporta para o Excel o resumo, as quantificações das análises e os pilares.</summary>
    public const string ComandoExportarExcel = "CLIVUS_EXCEL";

    /// <summary>O mesmo, com o caminho na linha de comando. Para o nível 2.</summary>
    public const string ComandoExportarExcelAutomatico = "CLIVUS_EXCEL_AUTO";

    /// <summary>Passo 8.15: o resumo do terreno (superfície, área, cidade, país e fuso UTM).</summary>
    public const string ComandoTerrenoResumo = "CLIVUS_TERRENO_RESUMO";

    /// <summary>Passo 8.13: escolher os estilos de texto, cota e chamada que o plugin usa.</summary>
    public const string ComandoEstilos = "CLIVUS_ESTILOS";

    /// <summary>O mesmo pela linha de comando (texto, cota, chamada; "-" é o corrente). Para o nível 2.</summary>
    public const string ComandoEstilosAutomatico = "CLIVUS_ESTILOS_AUTO";

    /// <summary>Passo 8.14: insere as tags de fileiras (F1, F2...).</summary>
    public const string ComandoTagFileirasInserir = "CLIVUS_TAG_FILEIRAS";

    /// <summary>Passo 8.14: apaga as tags de fileiras (F1, F2...).</summary>
    public const string ComandoTagFileirasApagar = "CLIVUS_TAG_FILEIRAS_APAGAR";

    /// <summary>Passo 8.14: insere as tags de mesas (F1.2).</summary>
    public const string ComandoTagMesasInserir = "CLIVUS_TAG_MESAS";

    /// <summary>Passo 8.14: apaga as tags de mesas (F1.2).</summary>
    public const string ComandoTagMesasApagar = "CLIVUS_TAG_MESAS_APAGAR";

    /// <summary>Passo 8.14: insere as tags de módulos (número na mesa).</summary>
    public const string ComandoTagModulosInserir = "CLIVUS_TAG_MODULOS";

    /// <summary>Passo 8.14: apaga as tags de módulos (número na mesa).</summary>
    public const string ComandoTagModulosApagar = "CLIVUS_TAG_MODULOS_APAGAR";

    /// <summary>Passo 8.14: insere as tags de strings (S1, S2...).</summary>
    public const string ComandoTagStringsInserir = "CLIVUS_TAG_STRINGS";

    /// <summary>Passo 8.14: apaga as tags de strings (S1, S2...).</summary>
    public const string ComandoTagStringsApagar = "CLIVUS_TAG_STRINGS_APAGAR";

    /// <summary>Só para o nível 2: cadastra no desenho a mesa de exemplo de 28 módulos e uma de 14, as duas em uso.</summary>
    public const string ComandoMesasExemploAutomatico = "CLIVUS_MESAS_EXEMPLO_AUTO";

    /// <summary>Passo 8.7: a janela de Configurações (Estruturas, Escolha das estruturas, Parâmetros, Projeto).</summary>
    public const string ComandoConfiguracoes = "CLIVUS_CONFIGURACOES";

    /// <summary>Janela das análises (abas Ponta baixa, Ponta alta, Declividade, Pilares, Quantidades).</summary>
    public const string ComandoAnalises = "CLIVUS_ANALISES";

    /// <summary>Janela das tags (abas Fileiras, Mesas, Módulos, Strings).</summary>
    public const string ComandoTags = "CLIVUS_TAGS";
}
