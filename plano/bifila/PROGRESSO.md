# Progresso da bifila

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

**Plano escrito em 30/10/2026 a partir do pedido do Renan ("crie TODA
estrutura para este novo tipo, TUDO deve funcionar com ela"). As decisões do
`LEIA-PRIMEIRO.md` foram tomadas por mim e ficam para ele confirmar.**

## Etapa 37: o modelo

- 37.1 A bifila no perfil da mesa: VALIDADO (automático) — `BifilaFrame`, `.ufvmesa.json` v4 (724d7ad)
- 37.2 Dois pilares por pórtico: VALIDADO (automático) — `PillarLine`, números de ouro da Arctech (ef2c30a)
- 37.3 As peças da estrutura: VALIDADO (automático) — `BifilaStructure` (aeb2f5c)
- 37.4 Lista de material: VALIDADO (automático) — `StructureBill`, aba Estrutura (b2d1733)

## Etapa 38: no Civil 3D

- 38.1 Nova estrutura: Fixa bifila: AGUARDANDO VALIDAÇÃO
- 38.2 Desenhar a bifila: AGUARDANDO VALIDAÇÃO — nível 2 `clivus-bifila` (rodar.ps1 -SoBifila)
- 38.3 As ferramentas que leem pilar: AGUARDANDO VALIDAÇÃO
- 38.4 Lista de material na tela: AGUARDANDO VALIDAÇÃO
- 38.5 A janela de Mesa em abas: AGUARDANDO VALIDAÇÃO
- 38.6 O PAN enche o módulo e sobe para o servidor: AGUARDANDO VALIDAÇÃO — serviço: `servidor/tests/test_pan.py`
- 38.7 Fabricantes, biposte genérico e a altura básica (P1 e P2): REPROVADO (30/10/2026, na tela: o P1 e o P2 não se mudavam, as medidas das mãos francesas não diziam nada, o desenho ruim, sem genérico em Fabricantes nem onde dar outro nome) — refeito no 38.9
- 38.8 Os módulos no servidor do Clivus, com a validação da gestão: AGUARDANDO VALIDAÇÃO — servidor: clivussolar 2cc6561 (gate `validate_modulos`); plugin: `ModuleServiceReply` e `ModuleServer`, 37 testes
- 38.9 A altura é da estrutura (P1 e P2 digitados), o pórtico peça por peça (P1, MF1, P2, MF2, tesoura, terças), o genérico em Fabricantes, a vista lateral nova, Norte/Sul e a lista de estruturas que voltava: AGUARDANDO VALIDAÇÃO — `StructureHeight`, `ForStructure`, perfil v5; nível 2 `clivus-bifila-p1` (rodar.ps1 -SoBifila)
- 38.11 O terreno volta à memória ao abrir o desenho, sem modificá-lo, e Área, Alinhamento, Coordenada e as janelas da drenagem carregam sozinhos: AGUARDANDO VALIDAÇÃO — `TerrainCommands.CarregarDoCarimbo`; nível 2 `clivus-carimbo` (rodar.ps1 -Caso Testar-Carimbo)
- 38.10 Salvar não suja mais o biposte (e o desenho que ficou vermelho se conserta ao abrir), o registro de tudo, a aba Modelos com seções por fabricante e Apagar, e a área pela polilinha 3D: AGUARDANDO VALIDAÇÃO — `PendingChanges.MayBeFromSave`, `PendenciasDoSalvar`, `RegistroDeTudo`, `VendorStructures.ByManufacturer`; nível 2 `clivus-salvar` (rodar.ps1 -SoBifila) e `clivus-area-selecionar` (rodar.ps1 -Caso Testar-AreaSelecionar)
- 38.12 O enterro mínimo, nominal e máximo, por linha de pilar no biposte, e a estaca do catálogo (31/10/2026, tarde: "aqui é o enterro mínimo, não é para levantar a estrutura [...] enterro nominal, para usar no desenho"; "no biposte, o enterro mínimo e máximo é POR PILAR, o da frente e de traz"; a escolha "Estaca do catálogo"): AGUARDANDO VALIDAÇÃO — `EmbedmentRange` (Stake, Of), `TableFrame.NominalEmbedment/MaxEmbedment/EmbedmentFor/CatalogStake`, `BifilaFrame.Rear*Embedment`, o motor (`PillarCalculator`) e a altura (`StructureHeight`) pelo nominal; 13 testes; nível 2 -SoBifila (com a janela da Mesa num PNG), -SoEstruturas, -SoCampo

## Etapa 39: o resto do Clivus

- 39.1 3D e TermoVision: AGUARDANDO VALIDAÇÃO
- 39.2 Tudo funciona com biposte (a prova): AGUARDANDO VALIDAÇÃO — nível 2 `clivus-estruturas` (rodar.ps1 -SoEstruturas)
- 39.3 O 3D avisa a mesa assentada num terreno diferente do de hoje (o chão por cima das mesas no navegador): AGUARDANDO VALIDAÇÃO — `TerrainDrift`, 2 testes; conferido na cópia do Aurora (118 mesas)

## Etapa 40: mesas conforme os objetos do cliente

- 40.1 Reconhecer as mesas do cliente: VALIDADO (automático) — `FieldObjects`, 21 testes; nos 641 objetos do Aurora: 609 mesas em 101 fileiras, 18 repetidos e 14 sobrepostos
- 40.2 Gerar conforme objetos em campo: AGUARDANDO VALIDAÇÃO — reprovado na tela em 30/10/2026 (nada era gerado: pelo Z, os objetos olhavam para o sul e o projeto para o norte) e refeito: só X e Y, alinhamento Direita/Centro/Esquerda, o lado da ponta baixa do projeto; nível 2 `clivus-campo` (rodar.ps1 -SoCampo)

## Etapa 41: as áreas da usina

- 41.1 A janela Áreas (Implantação): a lista das áreas com o resumo de cada uma, zoom, hachura, renomear e o Excel por área: AGUARDANDO VALIDAÇÃO — `AreaSummary`, `AreaHatch`, `AreaReport`, 34 testes; nível 2 `clivus-areas` (rodar.ps1 -SoAreas)
- 41.2 As ações por área (Apagar área, Limpar mesas, Regerar, Recalcular), a área repetida recusada e avisada, e a área achada pelo clique mesmo com a linha do cliente por cima: AGUARDANDO VALIDAÇÃO — `EscolhaDeArea`, `CLIVUS_AREA_APAGAR`, `CLIVUS_AREA_RECALCULAR`, `Polygons.SameOutline`; nível 2 `clivus-areas-acoes` (rodar.ps1 -SoAreas)

## Etapa 42: as sombras

- 42.1 A revisão das sombras: hachura magenta no chão (acompanhando o terreno) e sobre os módulos, a sombra do relevo, o escopo (seleção, uma área, área + seleção, várias áreas, desenho inteiro), Gerar e Apagar no escopo, Ocultar e Mostrar, Ver análises em tópicos, o módulo sem pintura: AGUARDANDO VALIDAÇÃO — `TerrainShadow`, `GroundPatches`, `ShadowScopePlan`, `ShadowReasons` V2, 23 testes; nível 2 `clivus-sombras*` (rodar.ps1 -SoSombras), o novo `clivus-sombras-escopo`; a bateria inteira de 31/10 deu 104/105: o `clivus-apagar` ainda esperava a sombra pintando módulo e passou a conferir o desenho antigo, montado pelo gancho `clivus-sombra-antiga` (30 módulos roxos voltam à cor de antes; b5d48f0)
- 42.2 O 3D com o sol (31/10/2026: "leve a simulacao de sombra para o 3D no navegador"): o navegador calcula o sol e faz a sombra em tempo real (data, hora, animar o dia, equinócios e solstícios, ligar a sombra), os pilares modelados (a seção e o giro do bloco), o item Resumo das camadas (mesas, módulos e kWp, por área e por tipo) e o logo do Clivus; na página pública, a propaganda que leva à landing: AGUARDANDO VALIDAÇÃO — `sol.js` e `resumo.js` (os mesmos no servidor), `Scene3DLocation`, `Scene3DPillarSection`, `Scene3DSummary`, 7 testes da etapa 42 e `tools/conferir-sol.js` (linha "Sol (JS)" do placar); servidor: clivussolar branch `sombras-3d`
- 42.1 e 42.2 na tela (31/10/2026, 10h, Aurora): REPROVADOS. "ocultei e nao tem ver sombra. A sombra fica marcada no modulo mas nao fica no solo. Eu fui claro e vc errou"; no 3D, "ta roxo, nao vejo a sombra passar, deveria no minimo ter um botão 'ver momento que causa sombras' eu nao estou acreditando que faz sombra" e "comeu modulo, é mentira, nao comeu no cad". Refeitos no 42.3.
- 42.3 As correções da tela: o Mostrar à vista (um botão só, Ocultar ou Mostrar sombras, e as linhas da janela quebram em vez de cortar); a área leva as que estão dentro dela (a "Area 1" do Aurora dava "Nenhuma mesa"); a lista diz quantas mesas cada área leva e abre na primeira que tem; o roxo da versão antiga sai ao abrir a janela; o 3D com o terreno de verdade (o TIN), sem o roxo antigo, com a sombra do CAD em magenta e o botão "Ver o momento que causa sombra": AGUARDANDO VALIDAÇÃO
- 42.3, a segunda rodada (31/10/2026, tarde; 0.1.4): "Solsticio de inverno para quem? [...] o projeto é no panama" virou **Pior dia do ano (dd/mm)**, o dia do sol mais baixo ao meio-dia no lugar do desenho (`SolarCalculator.WorstShadowDay`: dezembro no norte, junho no sul); "uma area pintou e na outra nao [...] cade o botão de escolher area ou escolher o que eu quero (mesas, arvores, etc)" virou as áreas em caixas de marcar (com "· N mesas" e "Desenho inteiro"), "Escolher área no desenho…" (`CLIVUS_SOMBRAS_AREA`, clique dentro da área), "Escolher mesas no desenho…" (`CLIVUS_SOMBRAS_MESAS`), áreas e mesas somando, e a linha **Fazem sombra** (Mesas, Árvores e objetos, Relevo; `ShadowCasters`); "Nao quero que leve o magenta no 3d web, a web ja tem sombra propria": o 3D (servidor clivussolar 8fc8fb0 e a página local) não desenha mais as hachuras do CAD nem pinta os módulos do momento, e o plugin não as manda (o botão do momento só leva o sol à hora da análise e diz quantos módulos): AGUARDANDO VALIDAÇÃO
- 42.4 A exportação para o PVsyst por escopo (31/10/2026: "tenho que ter a liberdade de exportar uma area, varias, todas, e pode selecionar as mesas que eu quero se for o caso"): a mesma escolha da janela das sombras (`SeletorDeEscopo`), numa janela antes de pedir o arquivo: AGUARDANDO VALIDAÇÃO
- 42.5 A janela Áreas com as ações na linha (31/10/2026, tarde: "eu clico em apagar area e nao apaga. As ações precisam estar na linha"): dois cliques dá zoom, a lixeira (ou Del) apaga com aviso, F2 ou o segundo clique devagar renomeia, o botão direito tem o resto; embaixo, "Todas as áreas" (Hachurar, Tirar hachura, Limpar, Regerar, Apagar); o Apagar área, o Limpar e o Recalcular só com as mesas DA área. E o CLIVUS_3D com a janela de andamento e o Cancelar ("trava gostoso [...] sem MATAR MEU CAD"): AGUARDANDO VALIDAÇÃO — bc9b912; nível 2 `clivus-areas-acoes` (rodar.ps1 -SoAreas)
- 42.6 As análises por escopo, o fundo e a borda dos textos e as colunas do biposte (31/10/2026, tarde: "Em analise eu tbm quero poder escolher o que analisar, uma area ou todas, isso para TODAS analises"; "texto ruim de ver, falta opção do texto de colocar o fundo e borda, isso para TODAS analises"; "em pilar, deveria ter a opção de analisar AMBOS pilares, cada um tem uma altura [...] uma coluna para cada pilar o da frente e o de traz, a cor pode ser o mesmo para ambos"): AGUARDANDO VALIDAÇÃO — `LineLimits`, `ThresholdRule.ForLine`, `AnalysisTally` V2 (o lugar e as linhas), `AnalysisTextLook`, `ThresholdForm`, 26 testes da etapa 42; nível 2 `clivus-analises-escopo` (rodar.ps1 -Caso Testar-AnalisesEscopo)
- 42.5 e 42.6, a segunda rodada (31/10/2026, 14h40, na tela com a 0.1.5): "nao funciona o renomear. o F2 nao funciona tbm" (a coluna do nome, ligada só de ida, era só leitura para o DataGrid); "o texto fica por traz" (nas vistas sombreadas os módulos cobriam o texto: agora ele fica deitado no plano dos módulos, 0,15 m acima, como a seta da declividade); "TEM QUE EXISTIR ALINHAMENTO" (as áreas do seletor em colunas da mesma largura); "o botão direito anda muito devagar, o zoom, muito devagar" (a caixa do kWp e o menu do botão direito liam toda a seleção a cada pan e a cada clique): AGUARDANDO VALIDAÇÃO — nível 2 `clivus-areas` pelo F2 e pelo menu da linha (conferido que o teste pega o bug antigo)

## Observações

- 37.1 (30/10/2026), o que assumi:
  - Medidas tiradas da geometria vetorial dos PDFs (ver `01-modelo-de-referencia.md`):
    T1 4,030 centrada nos módulos; T2 0,498 e T2t 3,544 (o ponto do plano em cima do eixo
    de cada pilar), 3,000 m entre eles na horizontal; topo do pilar 0,126 abaixo da face
    dos módulos; enterro 2,20; vãos 3,2 + 3,5 × 3 + 3,2 com balanço 0,851.
  - A Arctech cota a altura livre no fundo do módulo; a PB do Clivus é a face de cima. A
    GC800 pede PB ≥ 0,832 e a GC1000 PB ≥ 1,032 na faixa da PB do projeto (Configurações).
  - Seções não cotadas (só 3D): pilar 0,050 × 0,095, viga 0,05 × 0,08, terça 0,05 × 0,10,
    mão francesa 0,04 × 0,06, tirante Ø 0,016.
  - "Sem tirante" é um jeito só: linha nenhuma e lista vazia; os vãos ficam em ordem.
  - Revisão (subagente): 7 defeitos, todos corrigidos com teste — validação que lançava
    exceção com vão-alvo absurdo; terça fora da tesoura; pilares encostados em planta
    (conta em planta, no perfil, que conhece a inclinação); `"rodLine": "1"` lido calado
    (agora só pelo nome exato); topo do pilar entre o módulo e o fundo da viga; dois jeitos
    de dizer "sem tirante"; NaN em campo sem uso.
  - Achado fora do escopo: o `arrangement` do perfil (antigo, de 2026-09) aceita `"1"`
    entre aspas como `DoubleRow`, pelo conversor do .NET. Não mexi (não é da bifila).
- 37.2: a PB e a PA são da estação (iguais nos dois pilares do pórtico); o traseiro conta
  como pilar com problema na distribuição, como qualquer pilar.
- 37.3, o que assumi:
  - Os pilares NÃO entram como barras: continuam blocos do pilar (o desenho, o XData, as
    análises e a lista de compra já tratam deles). As barras são viga, mão francesa, terça
    e tirante.
  - Pilha debaixo dos módulos: terça encostada no módulo, viga encostada na terça (eixo da
    viga 0,173 abaixo da face na Arctech, ~5 cm abaixo do topo do pilar, como no corte).
  - A mão francesa sai do eixo do pilar (no desenho ela pega na face, 9 cm ao lado): a
    cabeça encosta ~1,10 m à frente do pilar, contra 1,08 no desenho.
  - Revisão: cabeça colada no pilar ou além do outro pilar, pé acima da viga, aviso de
    "aumente" para peça comprida, vazio calado, pilar sem terreno calado, tirante sem a
    linha no aviso, NaN — todos corrigidos com teste.
- 37.4, o que assumi:
  - Corte arredondado para cima no milímetro (0,1 mm de folga): a peça de catálogo de
    1,244 continua 1,244 e nenhum corte sai curto. Terça em barras de até 7,0 m (dá as 3
    barras e 2 emendas por linha da Arctech).
  - Revisão: corte curto pelo arredondamento ao centímetro, emenda negativa com barra
    inválida, emendas sem a barra no nome, faltava a lista por mesa e a nota de "nenhuma
    peça" — corrigidos com teste.
- 38.x (30/10/2026), o que assumi:
  - Na janela de Mesa, a bifila mostra "Entre pilares, na horizontal" (3,00): o T2t é
    calculado com a inclinação do campo ao ler. Os botões GC800/GC1000 só trocam as mãos
    francesas (é o que muda entre as duas). As seções são texto "0,05 × 0,08".
  - Nova estrutura › Fixa bifila parte da última bifila do desenho ou da GC800.
  - O pilar da bifila grava o XData na versão 3 (a linha no 12º campo); o da monoposte
    continua na 2. As peças vão em sólidos na camada CLIVUS_ESTRUTURA (nasce só quando há
    bifila), XData "Estrutura" v1, no grupo da mesa; mesa que não cabe deixa as peças
    magenta na camada das marcadas, como o resto dela.
  - Na Usina mista, cada mesa usa o T3 da própria estrutura (antes valia o do primeiro tipo
    para todos). Tipo sem T3 continua com o enterro da configuração da usina.
  - O 3D lê as faces de cada sólido (Brep) e manda em triângulos, na lista das canaletas
    (sem mudança de contrato com o servidor).
  - O teste de nível 2 da cota lê os sólidos por XEDGES (o Core Console não tem
    getpropertyvalue nem ActiveX).
- Revisão do 38.1/38.2 (subagente), corrigido:
  - Nova › Fixa abria em modo bifila quando a última mesa era bifila.
  - Os avisos da bifila sumiam no Recalcular, no Trocar, nas Pontas e nas Análises; agora
    saem em todos, com as mesas de cada um.
  - PB/PA ficavam faltando quando o dianteiro tinha problema: agora é o primeiro pilar sem
    problema da estação (`PillarStations`), a mesma regra no desenho, nas Alturas e nas
    Análises.
  - Peça órfã no Refazer.
  - A janela recalcula ao mudar as seções e recusa inclinação negativa.
  - XData só pelo nome exato.
  - Sólido lido primeiro no LayoutScan.
  - A mesa suja pinta também a estrutura.
  - O corte aplica as regras do Core às mãos francesas.
  - O sólido da barra é um só para carport e bifila (`SolidoDeBarra`).
  - O teste de nível 2 ficou mais forte: sólidos pelo XData, cota de cada sólido sozinho
    (PICKSTYLE 0), 12 arestas exigidas, peça junto dos pilares, 3,00 m entre pilares,
    nada sobrando depois do Apagar tudo.
- Revisão do 38.3/38.4/39.1 (subagente), corrigido:
  - A regra do T3 mora num lugar só (`SystemConfiguration.WithEmbedmentOf`): a fileira e a
    mesa avulsa (Pontas, Recalcular com pontas) aplicam o T3 de cada mesa. Antes a mesma
    bifila saía com 2,20 m na Usina e com o enterro da configuração nas Pontas.
  - O plugin entrega ao motor a configuração do projeto: o tipo sem T3 numa usina mista não
    herda mais o T3 do primeiro tipo.
  - A ponta da mesa usa o pilar que tem PB (`PillarStations.EndCarrier`).
  - O módulo da análise usa o pilar da estação.
  - O Excel só escreve a linha da estrutura com a aba marcada.
  - Os avisos ordenam F1.2 antes de F1.10.
  - Uma cor de aço só.
  - O 3D não explode mais os sólidos: o XData de cada peça guarda o eixo e a seção, e o
    Core monta a barra (`BarMesh`, a mesma do carport) em 8 triângulos (sem as tampas). Peça
    movida ou copiada aparece onde está.
- Fica para o Renan ver na tela (riscos conhecidos):
  - Peso: são 26 sólidos por mesa 2P14; numa usina de 500 mesas, uns 13 mil. Medir o tempo
    do Processar e o tamanho do DWG no projeto do Panamá.
  - 3D: as peças vão na camada das canaletas da página (a caixa "Canaletas e carport" liga
    e desliga a bifila também; formato do servidor igual, só mais faces). Conferir de longe
    se a terça, 3,3 cm abaixo do módulo, não aparece por cima dele.
  - Commits: o 38.1 a 39.1 saíram em dois commits (o núcleo no Core e o plugin), e não um
    por passo, porque as correções das revisões cruzaram os passos nos mesmos arquivos.
- 38.5 (30/10/2026), o que assumi:
  - A janela abre sempre na aba "Perfil e módulo", com o tipo da mesa aberta marcado; para
    uma mesa nova, o tipo da última mesa do desenho.
  - Só a aba do tipo escolhido aparece; a do outro fica guardada (os textos dos campos,
    o arranjo e os vãos escritos), e o perfil salvo de outro tipo também guarda a atual.
  - Na tela, "bifila" virou "biposte" (a palavra do Renan nos pedidos de 30/10); o nome
    técnico (`Bifila`, `frame.bifila`) não muda. Dois testes do 37.1 que procuravam a
    palavra na mensagem passaram a procurar "biposte", com a mesma força.
  - Nova estrutura voltou a Fixa e Carport: a Fixa bifila virou o cartão Biposte.
  - Revisão (subagente, que rodou a janela fora do Civil 3D e clicou nos cartões): nada grave;
    corrigido o nível 2 da bifila, que procurava "ATENÇÃO (bifila)"; a semente do biposte com
    outro módulo vai pelo vão-alvo (os vãos escritos da GC800 só fecham com o módulo de
    1,303 m); "Seções das peças" sem o "(3D)", que o layout compacto escrevia "(3d)"; "Sem
    tirante" não apaga mais os vãos escritos no campo; o LEIA-PRIMEIRO.
  - Fica como está: o nome é um só para os dois tipos (fica na primeira aba); trocar o tipo
    não muda o nome.
- 40.1 (30/10/2026), o que assumi:
  - O objeto é a área dos módulos ou a mesa inteira (com as sobras): os dois casam.
  - Tamanho e ângulo reto medidos em 3D (o retângulo do cliente vira paralelogramo em
    planta); o eixo e o lado da ponta baixa, em planta. A inclinação é o giro em torno do eixo
    (nos 641 objetos: 9,999° a 10,001°).
  - A referência é o que a maioria tem; tamanho, rumo, inclinação e ponta baixa diferentes
    são apontados, e a mesa sai mesmo assim. Não viram mesa: o que não é retângulo de 4
    vértices, o repetido (fica o primeiro), o que está em cima de parte de outro, o que está
    com o centro fora da área. Atravessar o contorno vira mesa, com aviso.
  - A fileira se parte onde a folga entre duas mesas passa da do motor (5 m no padrão),
    para a fileira do Clivus ser a corrente que o motor resolve.
- 40.2, núcleo (30/10/2026): `FieldPlacement` casa cada objeto com a primeira mesa em uso do
  tamanho e da inclinação dele, recusa o projeto que olha para o outro lado (o motor viraria
  as mesas) e roda o motor duas vezes: a primeira com a rampa do objeto, a segunda com a célula
  corrigida pelo que a primeira mediu (o motor ancora a mesa no canto da ponta baixa e encurta
  a planta pelo giro). 8 testes, um deles conferido desligando a segunda passada (ele falha).
  Mono e biposte nos mesmos objetos dão os módulos no mesmo lugar (a menos de 1 cm).
- 40.2, na tela (30/10/2026), o que assumi:
  - Botão "Conforme objetos" no painel Processar, ao lado de Usina, e "Gerar conforme objetos
    em campo…" no clique direito da área. Pergunta "[Todos/Camada/Escolher]": Todos são as
    polilinhas fechadas de 4 vértices com o centro dentro da área; Camada, as de uma camada
    (clica-se um objeto); Escolher, os objetos selecionados.
  - Processa antes de apagar: se o motor falhar, as mesas da área ficam. Depois troca as
    mesas que a área tinha (pergunta antes, com quantas saem e quantas entram).
  - As marcas (círculo e motivo, na cota do terreno) ficam na camada CLIVUS_DIVERGENCIAS; a
    rodada seguinte na mesma área apaga as da vez anterior.
  - A área guarda, no dicionário dela, que segue os objetos e como foram escolhidos: o
    Regerar área, o Regerar todas e o automático refazem pelos objetos, sem alinhamento. O
    Regerar fileira não mexe nessa área, e o Trocar não reespaça as vizinhas.
  - Nível 2: 8 objetos (6 + 1 repetido + 1 curto) no Z 9999 → 6 mesas a 0,000 m do centro
    dos objetos, 72 pilares na faixa do terreno, os 8 objetos intactos, 2 marcas; o Regerar
    área refaz igual.
  - Revisão (subagente), corrigido com teste: o eixo perto de 180° (ruído de coordenada ou
    vértices ao contrário) virava a mesa e abria a junta — cada pegada passa para o sentido da
    referência (o teste falha sem isso); um retângulo que não é mesa (cerca) tirava o lugar das
    mesas — os do tamanho da maioria disputam primeiro; a sobreposição pequena na mesma
    fileira passava — o alcance cobre a mesa inteira; o objeto 2D desenhado com a largura em
    planta não casava; a fileira a 3° da maioria virava mesas soltas — as fileiras se agrupam
    pelo eixo; a polilinha com o primeiro vértice repetido no fim (GIS, DXF) era descartada;
    camadas congeladas, desligadas e do Clivus ficam fora do Todos; a área esquece os objetos
    na Usina pelo alinhamento e no Apagar tudo; camada travada não derruba; as marcas da vez
    anterior saem também a até 30 m da área; o objeto de outro tamanho que casa com outra mesa
    em uso não é divergência; o "sem mesa do tamanho" saía duas vezes; o Regerar todas conta
    só a área que gerou. Nível 2: mais um objeto 2D (7 mesas) e o Apagar tudo esquecendo.
- 39.2 (30/10/2026), a prova:
  - O nível 2 `clivus-estruturas` roda a mesma cadeia duas vezes no mesmo terreno, com a
    mesma mesa (a 2P14 GC800): uma vez com estrutura monoposte (`CLIVUS_MONO_2P14_AUTO`,
    só de teste) e outra com o biposte. A cadeia: usina, alturas, tag de mesas e de
    módulos, grupo, strings, configuração elétrica (UC, trafo, modelo, inversores, alocação,
    skid), local dos inversores pelas strings, cabos da rota, vala, Gerar CC, conferência e
    resumo da rota, resumo elétrico, numeração, tag das strings, análises (PB, pilar,
    declividade), árvore e sombras, PVsyst, Excel com todas as abas (`CLIVUS_EXCEL_TUDO_AUTO`,
    só de teste), 3D, TermoVision, Validar e Recontar.
  - Resultado (10 fileiras, 30 mesas, 840 módulos): as 22 camadas do desenho que não são
    estrutura saem iguais nas duas (quantas entidades e a soma de X + Y de cada uma): área,
    alinhamento, mesas, módulos e faces, tags de mesa, de módulo e de string, strings, cabos
    CC + e −, vala, inversores, grupo, sombras e textos, árvore, declividade. Iguais também:
    o resumo da usina, o resumo da rota (1.028,59 m de cabo CC, por inversor), o resumo
    elétrico, as abas do Excel que não são de pilar, o DAE do PVsyst, a cena do 3D (menos
    pilares e peças) e o retorno do TermoVision. Muda só a estrutura: 180 × 360 pilares,
    0 × 780 peças, 540 × 720 cotas das alturas (PB/PA uma por pórtico, P3 em cada pilar).
  - O levantamento do "tudo" (subagente) achou quatro lugares onde o biposte escorregava,
    corrigidos com teste: a contagem da PB/PA dizia "pilares" e conta pórticos (agora diz
    "pórticos" quando o desenho tem biposte; o nível 2 da bifila confere); o Apagar › Cores
    deixava as peças do biposte vermelhas; a regra das pontas iguais olhava o último pilar da
    lista (o traseiro) e não o que leva a PB (o teste falha com a regra antiga); o palpite do
    perfil pelo comprimento (mesa antiga sem o nome do perfil) não olhava o tipo, e a 2P14
    monoposte e a GC800 têm o mesmo comprimento. A caixa do 3D diz "Canaletas, carport e
    estrutura do biposte".
  - Revisão (subagente), corrigido: o palpite do perfil pelo comprimento ainda podia trocar
    o tipo da estrutura quando não achava perfil do mesmo tipo — agora Recalcular, Pontas,
    Regerar fileira e Repintar recusam e mandam usar o Regerar área (`GuessByLength`, com
    teste); a contagem em pórticos sai dos pilares que o Quantificar já lê. A prova ficou
    mais apertada: X, Y e Z separados e o texto na impressão digital (5 cm de folga), as cotas
    de PB e PA comparadas (só o P3 muda), as 21 camadas exigidas, os resumos elétrico e de
    cabos no Excel, o rótulo "pórticos" conferido, Validar e Recontar conferidos, as linhas de
    batimento do console fora. Passa: 24 camadas e 5 abas iguais; as mesas ficam na mesma
    cota nas duas estruturas.
  - Ficam fora do nível 2 (sem comando automático ou só de tela): a rota CA e MT (precisam
    do trafo e da subestação posicionados), a drenagem (não lê mesa), o carport (estrutura à
    parte), salvar e reabrir o DWG, e as janelas. Estão no roteiro de tela.
- 38.6 (30/10/2026), o que assumi:
  - O PAN do PVsyst traz as medidas em metro (Height, Width, Depth; o do Aurora, 2,384 ×
    1,303 × 0,033). O lado maior vira a altura. PAN sem as três medidas: as medidas ficam
    como estavam, e o recado diz.
  - "Carregar .PAN..." põe o módulo do PAN na lista de modelos (o da biblioteca com o mesmo
    modelo, ou ele mesmo, só nesta janela) e enche marca, modelo, medidas, potência e a parte
    elétrica. Se a biblioteca do serviço não tem o modelo, pergunta se cadastra, com o PAN;
    com o serviço fora do ar, diz que o "Cadastrar módulo..." leva depois.
  - "Cadastrar módulo..." tem "Ler .PAN..."; com o PAN já lido na janela de Mesa, abre com ele.
    O PAN só vai junto se o modelo não foi trocado à mão.
  - O serviço (`servidor/`, FastAPI) guarda a parte elétrica (V, A, V/°C, A/°C, %/°C) e o
    arquivo como veio (até 500 mil caracteres); a lista devolve a parte elétrica e se tem PAN,
    sem o texto; o arquivo sai em `GET /modulos/{id}/pan`. Migração `3c1d2e7f9a10` (o
    `tools/servico-local.ps1` já roda o `alembic upgrade head`). Escolher na lista um módulo
    que tem a parte elétrica no serviço enche os dados elétricos.
  - **O "servidor" hoje é o serviço de módulos local** (`tools\servico-local.ps1`, ou o
    endereço de `CLIVUS_SERVICO`). O servidor de produção (Coolify: 3D, licenças, portal) não
    hospeda o serviço de módulos; para os usuários subirem PAN para todos, o serviço precisa
    ir para lá — decisão do Renan (e da sessão do servidor, pelo CANAL.md), fora deste passo.
  - Testes: 8 no serviço (pytest, 32 ao todo) e 8 no Core (medidas do PAN, o lado maior, a
    parte elétrica indo e voltando). As janelas são de tela.
  - Revisão (subagente), corrigido com teste: um módulo com coeficiente NaN ou infinito,
    que o serviço aceitava, derrubava a lista de todos para a biblioteca embutida — o
    serviço recusa (422 limpo) e o plugin lê a parte elétrica sem depender dela; o serviço
    antigo jogava o PAN fora e o recado dizia "com o PAN" — o plugin confere `tem_pan` na
    resposta; a parte elétrica e o arquivo passam a acompanhar o módulo escolhido (a do PAN
    lido, a do serviço, ou nenhuma, nunca a de outro módulo); o cadastro fica com as medidas
    da janela de Mesa; o PUT sem PAN mantém o PAN; nome de arquivo com qualquer caractere
    no download; o NUL sai do texto; o PAN do mesmo modelo lido de novo troca a entrada
    provisória; a lista que não recarrega não fica em branco. O contrato da lista com PAN
    (`tests/dados/servico-modulos-com-pan.json`) é conferido pelos dois lados (pytest e
    Core). Serviço: 53 testes.
  - Fica como está: o texto do PAN antigo (ANSI) é guardado como lido (Latin-1), não os bytes
    exatos; os números, que são o que conta, são ASCII.
- 38.7 (30/10/2026), o que assumi:
  - Pedidos do Renan: "se for um biposte específico do fabricante, crie uma aba de
    fabricantes e aí um genérico, para a pessoa montar o biposte dela" ("esse biposte eu vi
    que tem umas mãos francesas, etc"), e, com o desenho da Arctech ao lado (3059 e 3888),
    "cadê a altura básica das duas pernas da mesa? Vc colocou T3 somente. Ao meu ver seria
    P1 que é o pilar menor e P2 que é o pilar maior, e aí o resto".
  - Aba **Fabricantes** na janela de Mesa: um cartão por estrutura (`VendorStructures`, Core),
    com as peças, a altura livre, os pilares de catálogo e a fonte. Hoje são as duas da
    Arctech (2P14 GC800MM-NS 10 e GC1000MM-NS 20), com o nome do fabricante (a mesa vai para
    o desenho como "Arctech 2P14 GC800"). "Usar esta estrutura" enche a aba do biposte
    (mesa, pórtico, mãos francesas, terças, tirantes, seções) e a abre; a monoposte fica
    guardada. Não muda o módulo nem o nome da mesa (renomear mesa do desenho é do usuário);
    os vãos escritos do fabricante só ficam com a largura do módulo dele, senão vai o vão-alvo.
  - O **biposte genérico** (`BifilaReference.Generic`, sem nome de fabricante) é a semente do
    cartão Biposte: o pórtico de referência sem mão francesa e sem tirante, pelo vão-alvo.
    Quem tiver mãos francesas e tirantes põe nas seções deles. Antes a semente era a GC800
    inteira, com as mãos francesas dela.
  - **P1 e P2 são calculados, não digitados.** A altura da mesa vem da faixa da ponta baixa do
    projeto, igual para monoposte e biposte (o motor assenta a mesa na mínima no chão plano);
    o P2 sai do P1, da distância entre os pilares e da inclinação. Digitar os dois daria três
    donos para a mesma altura. Então o Pórtico abre com P1 (pilar menor, o dianteiro) e P2
    (pilar maior, o traseiro), inteiros com o enterro, na **altura básica**: chão plano, ponta
    baixa na mínima do projeto subida para a grade de 1 cm do motor (a de Configurações ›
    Parâmetros › Ponta baixa do módulo, mesmo sem gravar) e o T3 da mesa (sem T3, o enterro
    do projeto). Embaixo, quanto cada um tem acima do chão. O corte desenha o chão nessa
    altura e cota P1, P2 e T3.
  - O P2 do catálogo da Arctech (3,888) é maior que o da altura básica (3,588): tem a folga do
    chão caindo 10 % para trás (3,00 × 10 % = 0,300). O cartão do fabricante diz isso, e o
    teste confere que os números do catálogo fecham com a conta (P1 3,059 com a ponta baixa
    de 0,832; P2 de catálogo = P2 + 0,300).
  - Com uma estrutura de fabricante e a ponta baixa do projeto diferente da que dá a altura
    livre dela, a janela avisa embaixo do P1 e do P2, em cm e na grade (84 cm na GC800 e 104
    na GC1000, com o módulo e a inclinação da mesa); não muda a configuração do projeto.
  - Testes: 22 no Core (o P1 e o P2 iguais aos do motor inteiro, `RowPipeline.ProcessRow` num
    terreno plano, com a ponta baixa dentro e fora da grade, no biposte e na monoposte, com e
    sem T3; a grade; o enterro; os números de catálogo; o genérico). Com a conta antiga (a
    mínima crua), 9 reprovam. As abas e o corte são de tela.
  - Revisão (subagente, numa worktree isolada), corrigido:
    - O motor assenta a mesa na grade de 1 cm: com a mínima de 0,8325 ele usa 0,84, e a
      janela mostrava o P1 7,5 mm mais curto que o desenhado. A altura básica agora usa a
      mesma grade (`ViableElevations.FlatGroundLowEdge`), e os testes passam pelo motor
      inteiro (os primeiros montavam a cota à mão e não viam a grade).
    - O biposte genérico herdava o T3 de 2,20 m da Arctech, que é do solo do Aurora: agora
      vem sem T3, e vale o enterro do projeto.
    - O aviso apontava para a aba Projeto (a dos estilos), em metro: agora é Parâmetros ›
      Ponta baixa do módulo, em cm, com o módulo e a inclinação da mesa atual.
    - Carregar um perfil monoposte esquecia o fabricante do rascunho do biposte: só um
      biposte carregado o esquece agora.
    - O corte explica o tracejado e escreve "enterro 0,9 (projeto)" quando a mesa não tem T3;
      a fonte do cartão do fabricante passa pela tradução.
    - Quatro eventos da janela (Salvar perfil, Usar esta mesa, trocar o módulo, Limpar) ganham
      o try que os outros já tinham: exceção solta num evento fecha o Civil 3D.
- 38.8 (30/10/2026), o que assumi:
  - Pedidos do Renan: "então suba para o servidor de produção" e "os arquivos pan do cliente
    vão para a página de gestão do clivus, onde uma pessoa valida os pans e vai integrando a
    plataforma de forma pública após validação".
  - Servidor (clivussolar, 2cc6561, no ar desde 30/10 às ~17h30; o trabalho foi de um fork e
    eu revisei antes do push): `GET /api/v1/modulos` só com os aprovados, `POST` com a chave
    do plugin cria **pendente**, a página `/gestao/modulos` aprova ou recusa (com motivo) e
    baixa o .PAN. Quem entra na gestão: as contas de `CLIVUS_ADM_EMAILS` (sem ela, a Develop).
    Os três módulos embutidos no plugin entram aprovados na tabela vazia. Conferido no ar: a
    lista com os 3, a gestão sem login vai para `/entrar`, o `POST` sem chave dá 401 com o
    `{"erro"}`. O contrato está em `plano/contrato-servidor-3d.md`.
  - Plugin: sem `CLIVUS_SERVICO`, a janela de Mesa lê os módulos do servidor do Clivus (o do
    Publicar 3D, com `/api/v1`); com ela, do serviço local, como antes. O cadastro leva a
    `CLIVUS_SERVIDOR_CHAVE` (só por HTTPS) e o idioma; a resposta "pendente" deixa o módulo só
    na janela que o mandou, escolhido, com o .PAN, e o recado diz que ele espera a validação.
    Sem a chave, o "Carregar .PAN" não pergunta se cadastra (o servidor recusaria); o
    "Cadastrar módulo..." diz que falta a chave.
  - Hoje a chave do plugin é uma só (a sua) e publica na conta Develop: até cada conta ter o
    seu token (etapa das licenças), todo envio aparece como "Enviado por Develop", e cliente
    sem a chave não manda módulo.
  - Fica de fora: tirar da lista um módulo já aprovado e avisar o cliente da recusa.
  - Testes: 37 no Core (`ModuleServiceReply`: o pendente, o `{"erro"}` do servidor e o
    `detail` do serviço local; `ModuleServer`: o endereço, a chave só por HTTPS, o caminho
    sem chave, as variáveis que não prestam; o .PAN grande demais) e o gate do servidor (70
    conferências, que reprova nos sete defeitos plantados). O HTTP do plugin é de tela.
  - Revisão (subagente, worktree isolada), corrigido:
    - O módulo pendente sumia da janela ao carregar outro .PAN, e mandá-lo de novo dava
      "já existe" (o servidor recusa modelo pendente). Agora os mandados na sessão do Civil
      3D ficam na lista de toda janela de Mesa, marcados "(esperando a validação)", com a
      parte elétrica, até a lista do servidor trazê-los aprovados; e o "já existe" do
      servidor do Clivus é tratado como "já enviado".
    - A janela esperava a internet (até 3 s) em cada abertura: a lista boa vale 10 min e o
      "fora do ar", 5. O envio mostra o cursor de espera.
    - Sem a chave, o recado do .PAN diz que o módulo vale só nesta janela, e o "Cadastrar
      módulo..." diz o motivo antes de abrir o diálogo; os textos do diálogo falam do
      servidor do Clivus e do pendente.
    - O recado do servidor aparece também quando o cadastro vai pelo botão (o "sem o PAN"
      do serviço antigo se perdia).
    - `CLIVUS_SERVIDOR` malformado não cai mais calado no localhost: a lista embutida diz
      que é ele. A decisão de endereço e chave saiu para o Core (`ModuleServer`), com teste.
    - O .PAN acima de 500 mil caracteres é recusado antes de subir (chegava ao 413 no meio
      do envio).
    - O README e o docker-compose do `servidor/` e o `tools\servico-local.ps1` diziam que o
      plugin procura o localhost: agora dizem que é preciso `CLIVUS_SERVICO`, senão o
      cadastro de teste vai para a produção.
    - Notado e deixado como está: a rota de cadastro do servidor é `async` com o banco
      síncrono (trava o laço por um instante a cada cadastro; o tráfego é baixo); sem
      `CLIVUS_DEV_SENHA` a conta Develop não existe e a chave certa leva 401 (a produção tem
      a senha).
- Incidente de 30/10/2026: a revisão do 38.7 (subagente) rodou `tools/traducoes.py --faltam`
  passando os fontes como destino, e nove .cs viraram "[]". Recuperei os versionados do git e
  refiz as edições a partir da conversa (o diff saiu idêntico, linha por linha); os dois novos
  foram reescritos. O `--faltam` agora só grava em .json fora de src/, tests/ e plano/
  (ba8874c), e a revisão foi refeita numa worktree isolada.
- Bateria completa de 30/10/2026 (18:06 a 18:20), no a9220e7, o mesmo instalado às 18:00:
  tudo verde. Nível 1: Core 2365 e Geo 264; nível 2 100/100; serviço 53/53; instalador e
  acervo ok. O SECURELOAD voltou a 1 em todos os perfis.
- 38.9 (30/10/2026), o 38.7 refeito depois da tela. Pedidos do Renan: "Porque raios eu não
  consigo alterar as alturas? A altura está no print, 3059 P1 e 3888 P2, em fabricante, não
  tem genérico, ou então eu escolho um modelo e edito ela, onde posso dar outro nome", "é
  PILAR P1, Mão francesa MF1, pilar P2, Mão francesa MF2, aí tem as terças, as tesouras",
  "não tenho a menor ideia o que são essas medidas" (onde a mão francesa pega), o desenho
  de legenda ruim, "eu já apaguei esses [perfis] umas 10x, salvo no desenho e volta" e "a
  inclinação dos módulos para o norte ou sul, depende do local". O que assumi:
  - **A altura é da estrutura.** O P1 e o P2 são campos do biposte (`BifilaFrame.FrontPillar`
    e `RearPillar`; o perfil vai na versão 5). O P1 é o pilar dianteiro inteiro no chão
    plano: com o T3 (que ele exige), dá a ponta baixa da estrutura (`StructureHeight.LowEdge`;
    a GC800 dá 0,833, os 0,800 livres do catálogo), e essa é a ponta baixa mínima das mesas
    dela na usina, no lugar da mínima do projeto (`SystemConfiguration.ForStructure`, como o
    T3 manda no enterro). A máxima é a do projeto, mas com pelo menos a folga que a faixa
    do projeto tem: com 0,30 a 0,80, a GC800 vai de 0,833 a 1,333; com 0,84 a 1,60, de 0,833
    a 1,60. A fileira resolve com a faixa mais exigente das suas mesas (numa fileira de um
    tipo só, a dele). Os comandos dizem a faixa ("Altura da estrutura: ...").
  - O **P2** é o de catálogo: no chão plano o traseiro precisa do P1 mais a subida da tesoura
    entre os dois (3,588 na GC800); o que passa disso é a folga para o chão caindo para trás
    (0,300 = 10 % em 3,00 m; 0,600 = 20 % na GC1000), dita embaixo do pórtico e desenhada. O
    P2 menor que o de chão plano é recusado com o motivo. O motor continua calculando cada
    pilar pelo terreno (o comprimento é saída): o P2 não limita a mesa.
  - Os dois em branco: a altura vem da faixa do projeto, como no 38.7 (os perfis gravados
    antes abrem assim); um só escrito não fecha.
  - No terreno, o P1 é o dianteiro mais curto onde o motor deixa a mesa dentro da faixa; onde
    o terreno a força para fora (o relatório diz "ponta(s) baixa(s) fora"), um pilar pode
    sair abaixo dele, como sempre foi com a faixa. A grade de 1 cm do motor põe os pilares
    até 1 cm acima do P1 no plano.
  - **O pórtico peça por peça**: a seção "Pórtico: pilares e mãos francesas" tem P1, MF1 (e
    onde ela prende no P1, abaixo do topo), P2, MF2 (idem), a distância entre P1 e P2 e o
    topo abaixo dos módulos; tesoura e terças nas seções delas. O nome também na aba
    Biposte (o mesmo da primeira).
  - **Fabricantes**: o primeiro cartão é o biposte genérico, agora com as peças do pórtico de
    referência (P1 3,059, MF1 1,244, P2 3,588, MF2 1,209, tesoura 4,03, T3 2,20), sem
    tirante. "Usar" num fabricante põe o nome dele na mesa ("Arctech 2P14 GC800"), que se
    muda na aba; o genérico mantém o nome. As estruturas de fabricante trazem o P1 e o P2 do
    catálogo (3,059/3,888 e 3,259/4,388). O aviso "no projeto, altura livre mínima de 104
    cm" saiu: a altura vem do P1.
  - **A vista lateral** foi redesenhada como o desenho do fabricante: os pilares inteiros, do
    topo ao pé, com o nome e o comprimento (P1 à esquerda, P2 à direita); as mãos francesas
    com o nome, o comprimento e a cota de onde prendem; a tesoura e as terças; o chão plano
    na altura do P1 e, com a folga do P2, o chão caindo para trás que ele alcança; a altura
    livre sob o módulo; a distância entre os pilares; T1 e T2 em cima. O enterro sai cortado
    (a quebra), com a cota do valor inteiro: os 2,20 m achatavam o desenho. A vista lateral
    ganhou altura na janela (a planta cabe em menos). Conferida renderizada fora do Civil 3D
    (GC800, GC1000, genérico e o perfil sem P1).
  - **Norte ou sul**: é do projeto (o mesmo para toda a usina), em Configurações › Parâmetros,
    agora com os botões Norte e Sul ao lado do azimute; a janela de Mesa diz para onde os
    módulos olham, embaixo da inclinação. Com a latitude do terreno (a gravada ou a da
    geolocalização do desenho), Parâmetros e o Conforme objetos avisam quando a mesa olha
    para o lado errado do hemisfério (o Panamá olhando para o norte).
  - **A lista que voltava**: a lista das Configurações junta as mesas do desenho com os perfis
    da pasta do usuário (a biblioteca); o Remover tirava só do desenho, e o perfil voltava da
    pasta. Agora, ao salvar, a mesa removida sai também da biblioteca: o arquivo vai para a
    subpasta "removidos" (`TableProfileStore.MoveToRemoved`), não some.
  - Testes: no Core, o P1 que dá a ponta baixa e a volta, a folga do P2 igual ao declive do
    catálogo (10 % e 20 %), a faixa da mesa com P1 em quatro faixas de projeto, o motor
    inteiro assentando a mesa na altura do P1 com a faixa do projeto abaixo dela, os P1/P2
    que não fecham (cada um com o motivo), o perfil v5 de ida e volta (e o v4 sem eles), o
    genérico com as peças, a biblioteca que guarda o removido e o aviso do hemisfério. No
    nível 2, `clivus-bifila-p1`: a GC800 com P1 num projeto de 0,30 a 0,80; os dianteiros
    em volta do P1 (a média; com a faixa do projeto seriam uns 2,5 m), e a linha da altura
    da estrutura.
  - Pedidos durante a tela, no mesmo dia: "não consigo chegar nesses 80cm" (ele estava na
    GC1000, cujo P1 dá 1,000 livre) — o campo **Livre sob o módulo** no Pórtico, ligado ao
    P1: digitar 0,8 dá o P1 de 3,059; mudar o P1 muda o livre; o P2 fica. E "quero apagar as
    mesas da biblioteca, tem duas inúteis lá" — o botão **Apagar da biblioteca** ao lado de
    "Perfil salvo" (o arquivo vai para a subpasta "removidos").
  - Revisão (subagente, worktree isolada), corrigido:
    - O objeto mais curto que a mesa fazia a mesa entrar na vizinha sem aviso (três objetos
      de 17,5 m com 0,35 m entre eles: 0,752 m de uma em cima da outra). Agora sai a
      divergência "mesa em cima da vizinha", marcada no desenho, com o quanto e o conselho
      de trocar o alinhamento.
    - A fileira resolvia com a faixa mais exigente das suas mesas, e o Pintar e o Recalcular
      com a da mesa: numa fileira mista (monoposte ao lado de biposte com P1) a monoposte
      saía certa na fileira e com 14 módulos fora no Pintar. Agora cada mesa da corrente tem
      a sua faixa (`ChainTable.MinLowEdge/MaxLowEdge`), e a junta é julgada pela mais
      exigente das duas: a fileira e a mesa sozinha contam os mesmos módulos fora. Na
      fileira de um tipo só, nada muda.
    - Na fileira norte-sul, o ruído de desenho no eixo (89,99° e 90,01°) trocava a "direita"
      de ponta entre as mesas: a direita agora sai uma só, do eixo da maioria.
    - "O P1 é o dianteiro mais curto" era falso: o motor segura a ponta baixa na borda do
      módulo, e com o chão subindo para trás o dianteiro sai menor que o P1 (2,98 com 10 %).
      As frases dizem isso agora, e o nível 2 confere a média dos dianteiros, e não pilar por
      pilar.
    - "Os 0,300 cobrem o chão caindo até 10 %" é o critério do catálogo, medido do P1 ao P2;
      na usina, com a ponta baixa presa na borda do módulo, a folga cobre um pouco menos (uns
      7,8 %). As frases dizem que é a folga do catálogo.
    - O ângulo reto em planta com 3° de folga recusava retângulo 3D perfeito inclinado na
      rampa (20° com 15 % dava 86,9°): a folga é 8°, e o losango continua recusado.
    - "A mesa tem 0,053 m a mais que o objeto" saía para o objeto 3D na rampa (o D6D4), que é
      a mesma mesa: a diferença só aparece fora da folga da rampa.
    - O P1 que deixava o fundo do módulo dentro do chão passava (2,24 com 2,20 de enterro): a
      conferência é pelo fundo do módulo.
    - A latitude no Conforme objetos ganhou a proteção que tem nas Configurações; o registro
      da área sai na versão 1 com o centro (o plugin de antes lê); sem a MF1, a cota do P1 e
      a do T3 caíam na mesma linha do corte.
    - "Usar esta estrutura" não troca mais o nome da mesa sozinho (a mesa do desenho é achada
      pelo nome do perfil): o recado diz onde mudar.
    - Notado e mantido: o genérico com T3 de 2,20 (o P1 inclui o enterro; quem tem outro
      solo muda o T3 e o P1) e o Remover tirando o perfil da biblioteca (é o que o Renan
      pediu; o recado avisa).
  - Bateria completa de 30/10/2026 (por volta das 22h), no c2579ee, com o dc06ca0 instalado
    às 21:37: tudo verde. Nível 1: Core 2444 e Geo 264 (o placar passou a contar a etapa
    41); nível 2 102/102; serviço 53/53; instalador e acervo ok. O SECURELOAD voltou a 1.
- 40.2, refeito depois da tela (30/10/2026). O Renan escolheu 80 objetos: 78 viravam mesa e
  "Nada foi gerado", porque pelo Z deles os objetos olhavam para o sul e o projeto para o
  norte. Pedidos: "o objeto do cliente está em 2D, o eixo Z deve estar cagado, então gere a
  mesa respeitando apenas X e Y e encaixe a Z no terreno" e "no máximo o sistema tem que
  pedir 'respeitar qual eixo: Direita, centro ou esquerda' [...] se eu escolher direita, ele
  alinha na direita e estoura a esquerda". O que assumi:
  - **Só X e Y**: o objeto vira pegada em planta; o Z dos vértices não entra em nada (pode vir
    0, 9999 ou torto). O comprimento é o de planta, a largura é a distância entre os lados
    maiores (4,715 na GC800 a 10°), e o retângulo inclinado na rampa, que de cima é um
    paralelogramo, passa com 3° de folga no ângulo reto. O comprimento em planta tem 1,5 % de
    folga para "tamanho diferente" (a rampa encurta o objeto 3D visto de cima). Saíram a
    inclinação e o lado da ponta baixa do objeto, e com eles as divergências de inclinação,
    de ponta baixa trocada e de projeto virado.
  - **O lado da ponta baixa é do projeto** (o azimute em Parâmetros), e nada é recusado por
    ele; o relatório diz para onde as mesas olham e de onde isso vem.
  - **A única pergunta** depois dos objetos: "Alinhar a mesa no objeto pela
    [Direita/Centro/Esquerda] <Centro>". Direita é o leste na planta com o norte para cima (na
    fileira norte-sul, o norte). A ponta escolhida dos módulos (ou da mesa inteira, quando o
    objeto é ela) cai na do objeto, e a diferença sobra do outro lado; no centro, metade para
    cada lado. A área guarda o alinhamento (registro v2; o v1 vale centro), e o Regerar área
    usa o mesmo. A confirmação "Continuar? [Sim/Não]" saiu (o U desfaz).
  - **A mesa de tamanho mais perto**: o objeto vira a mesa em uso de tamanho mais perto (a área
    dos módulos ou a mesa inteira; a largura de planta ou da inclinação), até 15 %; mais
    longe que isso não é mesa (cerca, lote) e sai como "sem mesa do tamanho". O relatório diz
    quanto a mesa tem a mais ou a menos que o objeto.
  - Testes: no Core, o Z que não muda nada (0, 9999, cada vértice numa cota, NaN), o
    paralelogramo do D6D4 em planta, a rampa que não vira divergência, o lado da ponta baixa
    do projeto nos dois sentidos, o alinhamento pela direita, centro e esquerda com o projeto
    olhando para o sul e para o norte, a direita no leste, a mesa de tamanho mais perto. No
    nível 2, `clivus-campo` de novo (os objetos com Z 9999).
  - O reconhecimento do 40.1 também passou a ser em planta: os números dele nos 641 objetos
    do Aurora (609 mesas, 18 repetidos, 14 sobrepostos) eram do 3D e se conferem de novo na
    tela (roteiro 6).
- 41.1 (30/10/2026), o que assumi. Pedido do Renan: "em implantação, eu quero ter um botão,
  pequeno, de lista de áreas, pq nesse caso real vou ter 7 áreas [...] quero ver elas, quero
  ter a opção do sistema listar o resumo de cada área, de marcar cada área com hachura, do
  resumo sair por área, enfim, algo completo". Feito por um subagente numa worktree isolada
  (branch `areas`) e juntado aqui.
  - O botão pequeno **Áreas** em Implantação é o `CLIVUS_AREAS`, que já existia (a lista na
    linha de comando): com a interface ele abre a janela; no Core Console, a lista continua
    igual (os três casos `clivus-area-*` do nível 2 leem ela).
  - A janela é solta, uma por desenho, e fecha com ele. Uma linha por área (a cor, o nome,
    ha, mesas, módulos, kWp, pilares, os tipos de mesa, se segue os objetos, se está
    hachurada), a linha "Fora das áreas" quando houver e o Total. A área escolhida mostra o
    resumo: por tipo de mesa, os pilares (quantidade, metros, pendentes, na divisa) e a
    ocupação (área dos módulos sobre a área em planta). Botões: Zoom (e duplo clique),
    Hachurar e Tirar hachura, Hachurar todas e Tirar todas, Renomear (recusa o nome de outra
    área), Exportar Excel (a aba Resumo, uma linha por área e o Total, e uma aba por área) e
    Atualizar.
  - A mesa que atravessa duas áreas conta uma vez, na área do centro dela (na menor, se o
    centro cai em duas, como os inversores); com canto fora, aparece "na divisa". Centro fora
    de toda área vai para "Fora das áreas", e o Total bate com o Recontar.
  - A hachura é sólida, 70 % transparente, na camada `CLIVUS_AREAS_HACHURA`, com o nome no
    meio, na cor da paleta dos inversores pela ordem de criação da área. Fica 0,30 m acima do
    ponto mais alto do terreno e das mesas da área (plana, por cima de tudo, como a marca do
    grupo), nunca no Z de clique; não acompanha a área se ela for editada (hachurar de novo).
  - Fica de fora: a janela não se atualiza sozinha (botão Atualizar); apagar uma área muda a
    cor das seguintes no próximo "Hachurar todas"; o Exportar geral da Saída não tem as abas
    por área.
  - Testes: 34 no Core (`AreaSummary`, `AreaHatch`, `AreaReport`: contagens por tipo, kWp,
    pilares, ocupação, a mesa na divisa, a área vazia, o total de várias, a cota da hachura)
    e o nível 2 `clivus-areas` (duas áreas, o total igual ao Recontar, as hachuras acima das
    mesas, renomear, tirar e o Excel).
- 38.10 (30/10/2026, à noite), pedidos da tela: "eu cliquei em salvar e chega a dar
  uma travada no cad [...] VIGIA Fx.y pendente (movida ou editada, comando QSAVE) [...] isso é
  bug, corrija", "depois de salvar as mesas ficaram todas vermelhas, bug dos bravos", "quero
  também que você gere logs de tudo", "eu falei que quero APAGAR os fabricantes, cadê? E não
  chama fabricante, chama modelos a aba, e aí, quero seções POR fabricantes" e "em selecionar
  área, poligonal fechada, quero que seja possível usar 3D polilinha". O que achei e assumi:
  - **A causa do salvar:** reproduzido no Core Console (Itatiba, 5 mesas GC800): cada QSAVE
    marcava as 5. Entre o começo e o fim do salvamento o AutoCAD avisa "modificado" de cada
    sólido (130 peças da estrutura com o nosso XData nas 5 mesas; mais nenhuma entidade
    nossa), e o vigia lia isso como edição. Desde o 38.2 (as peças da estrutura são sólidos),
    todo salvar deixava todas as mesas biposte pendentes e vermelhas; a travada era o vigia
    pintando centenas de mesas.
  - **A correção:** o vigia não anota nada entre `BeginSave` e `SaveComplete`/`AbortSave` do
    banco (QSAVE, SAVE, SAVEAS, o salvamento automático, fechar salvando). Árvores e drenagem
    só olham blocos e polilinhas, que o salvar não mexe; ficaram como estavam.
  - **O desenho que já ficou vermelho:** ao abrir (ou ao carregar o plugin com ele aberto), a
    mesa pendente por "movida ou editada", com sólido, que continua onde foi desenhada (a
    âncora do contorno no lugar) volta a limpa, com a cor de antes (a do Apagar › Cores: a do
    tipo de mesa, a da camada, ou magenta na marcada), sem recalcular nem mover nada. A linha
    de comando lista as mesas e lembra de usar Recalcular se alguma peça tinha sido mexida de
    verdade. Uma vez por desenho: a chave `VIGIA_SALVAR` no dicionário, gravada no conserto e
    na primeira pendência que o vigia corrigido marcar; com ela, nada mais é desfeito. Ficam
    pendentes: copiada, peça apagada, pedido do usuário, monoposte e mesa sem âncora. Fica de
    fora: a cor de análise que a mesa tinha (Pintar estouros) volta à do tipo; pintar de novo.
  - **Logs de tudo** (só no Civil 3D com interface; o Core Console dos testes não escreve):
    - a linha de comando inteira, pelo log do próprio AutoCAD (`LOGFILEMODE`), ligado na
      pasta `%LOCALAPPDATA%\Clivus Solar\linha-de-comando` se o usuário não tinha ligado o
      dele; arquivo com mais de 30 dias sai;
    - no `clivus.log`: o Civil 3D, o Windows e a cultura ao carregar; cada desenho aberto e
      fechado; cada comando, quando começou, quanto durou e como acabou (terminou, cancelado,
      FALHOU), e o comando que começou e não terminou quando o desenho fechou; o erro que
      escapa de uma janela, de uma tarefa ou de tudo, com a pilha;
    - o `clivus.log` cheio (4 MB) vira `clivus.1.log` em vez de ser apagado.
  - **A aba Modelos** (era Fabricantes): uma seção "Genérico" e uma por fabricante (hoje,
    Arctech com a GC800 e a GC1000). Cada cartão tem Usar e **Apagar**. Os prontos vêm com o
    plugin, então apagar é esconder: a lista fica em `preferencias.json`
    (`modelosApagados`), vale para o usuário em qualquer desenho, e "Mostrar os apagados", no
    fim da aba, traz todos de volta. A mesa que já usa as medidas de um modelo apagado não muda.
  - **A área pela polilinha 3D:** a 3D já era aceita, mas só fechada pelo AutoCAD ou com a
    ponta exatamente no começo, em 3D. A do cliente vem com a cota estragada: agora vale
    fechada **em planta**, com a ponta a até 1 cm do começo e qualquer cota (o mesmo vale para
    a área dos inversores, que usa a mesma leitura). A que não fecha pergunta "a ponta está a
    X m do começo [...] [Fechar/Outra] <Fechar>": Fechar fecha com uma reta da ponta ao
    começo (a polilinha do usuário não muda; a área é nova).
  - Testes: no Core, a pendência que pode ser do salvar (só "movida ou editada", com sólido,
    no lugar) e a aba Modelos (seções, apagados, preferências de antes sem a lista). No nível
    2, `clivus-salvar` em duas metades (SAVEAS e dois QSAVE sem pendência; um CHPROP ainda
    marca; o desenho sem a chave abre e a mesa volta limpa e na cor da camada; um QSAVE
    depois não marca nada) e `clivus-area-selecionar` com as duas 3D (a com a ponta no começo
    em outra cota, e a aberta, que pergunta e fecha com o Enter). O `rodar.ps1` ganhou
    `-Caso <função>`, para rodar um caso da lista principal sozinho.
  - Revisão (subagente, numa worktree isolada), 8 achados, todos corrigidos:
    - **o conserto ao abrir confiava no motivo gravado**, e o salvar antigo o regravava
      por cima (uma peça apagada virava "movida ou editada"). Agora a mesa só volta se
      também estiver **inteira** (as peças da mesa mais comum do tipo dela,
      `PendingChanges.TypicalPieces`) e **sozinha no lugar** (a cópia colada em cima tem
      a âncora da original). O que não se distingue (a mesa marcada à mão, a peça mexida
      sem sair do lugar) a mensagem diz;
    - camada travada derrubava o conserto inteiro: cada mesa numa transação, aberta mesmo
      em camada travada; a que falhar continua pendente e é listada;
    - o silêncio do salvamento é zerado no começo e no fim de todo comando (um
      salvamento sem `SaveComplete` deixaria o vigia surdo);
    - a polilinha 2D antiga aberta: a ponta entra, e a que não cerca área é recusada pela
      área, não pela contagem de vértices;
    - o log da linha de comando volta a desligado no Terminate, com o caminho que o
      usuário tinha (guardado em arquivo, para valer depois de uma queda);
    - abrir e fechar desenho não aparecem mais como comando "sem terminar";
    - a virada do `clivus.log` que falha não para o log;
    - a mesa marcada que tentou todas volta roxa, não magenta.
  - O `clivus-salvar` ganhou as mesas que têm de continuar pendentes: uma com um módulo
    apagado e depois a cor mudada (o motivo vira "movida ou editada") e uma movida; ao
    abrir, só a inteira e no lugar volta.
  - Fica para a tela: o salvamento automático (SAVETIME) e o SAVEAS em DXF não passam no
    Core Console; o roteiro 14 pede para conferir.
- 38.12 (31/10/2026, tarde), o enterro. Pedidos do Renan, com o print da aba Biposte (o campo
  "Enterro mínimo T3" e o "livre 2,000" da vista lateral): "aqui é o enterro mínimo, não é para
  levantar a estrutura, aí coloca então: enterro nominal, para usar no desenho, tendo o enterro
  mínimo e máximo, vc vai saber até onde pode chegar, e o nominal, para gerar o desenho. veja isso
  na estrutura monoposte"; e depois: "no biposte, o enterro mínimo e máximo é POR PILAR, o da
  frente e de traz e vc vai seguir isso para projetar, aí o tamanho da estaca, essa pode
  explodir [...] o pilar fica com 7m por exemplo, foda-se eu tenho que respeitar o enterrado, o
  que ficar livre pra cima da terra é consequência". Perguntei como cada pilar é dimensionado e
  ele escolheu **Estaca do catálogo**. O que ficou:
  - Na janela da Mesa, a linha "Enterro mín. · nominal · máx. (m)" na Estrutura (no biposte, a
    do P1) e "P2: enterro mín. · nominal · máx. (m)" no pórtico. Cada campo pode ficar em branco:
    o mínimo e o máximo vêm do projeto; o nominal é o mínimo (o enterro de antes); os do P2 são
    os do P1. A monoposte tem a mesma linha.
  - A estaca (`EmbedmentRange.Stake`): no biposte, a do catálogo (P1 na frente, P2 atrás) com o
    enterro que o terreno der; se ficaria abaixo do mínimo, ela cresce com o mínimo enterrado;
    se passaria do máximo, é cortada com o máximo. Sem catálogo (a monoposte, ou o biposte sem
    P1/P2), a estaca sai com o nominal. A análise do enterro usa a faixa da linha do pilar.
  - A altura da estrutura (o P1 no chão plano) vem do enterro nominal da frente, não do mínimo;
    o P2 de chão plano leva o nominal de trás.
  - Assumi: no chão plano, o pilar de trás sai com a estaca inteira do P2 e a folga dele enterrada
    (na GC800, 3,888 m com 2,49 m enterrados, dentro do máximo de 2,5 m do projeto); antes saía
    cortado no P2 de chão plano (3,59 m com 2,2 m). A prova do 38.9
    (`ComP1OMotorAssentaAMesaNaAlturaDaEstrutura`) passou a conferir a estaca de trás pela regra
    nova (a do catálogo, cortada com o máximo ou crescida com o mínimo), e nunca menor que a do
    chão plano.
  - O perfil de antes abre igual: os campos novos são opcionais no arquivo, e o perfil que tem
    algum deles sai na versão 6 do formato (os outros continuam na 4 ou na 5, que o plugin anterior
    lê). Na janela, o perfil sem nominal abre com o nominal escrito igual ao mínimo.
  - A revisão (subagente em worktree, no 5898cc3) achou e eu corrigi: a herança de trás não era
    conferida e a regra morava em três lugares (agora um dono só, `TableFrame.EmbedmentFor`, e o
    `WhyInvalid` confere o de trás com o que herda da frente); com o nominal em branco, mudar o
    mínimo ainda levantava a estrutura (a janela escreve o nominal ao abrir); o biposte com P1/P2
    e só o nominal era recusado pedindo o "T3"; o `Equals` das estruturas ignorava os campos
    novos; o arquivo não subia de versão; as dicas do P2 diziam "o do projeto" (em branco é o da
    frente); o P2 calculado pelo livre e o resumo ignoravam o nominal de trás; a vista lateral
    desenhava o pé de trás com o enterro da frente. Testes novos: a estaca de 3,888 da GC800 e a
    cortada pelo máximo de trás, a monoposte com nominal, os conflitos com a frente, só o nominal,
    a igualdade e a versão 6.
- 38.11 (31/10/2026), pedido da tela: "quando o cad abre, ele não tá carregando o terreno,
  isso é erro". O terreno processado só vive na memória. Ao reabrir, os comandos que passam
  pelo `ExigirTerreno` reprocessavam sozinhos. Já Área, Alinhamento, Coordenada, Flechas e as
  janelas Bacias, Canaleta e Memória olhavam só a memória e diziam "Nenhum terreno
  processado". O que fiz:
  - `TerrainCommands.CarregarDoCarimbo` põe na memória a superfície do carimbo (pelo
    handle), sem gravar nada no desenho: abrir não o deixa "modificado". O Reprocessar de
    antes regravava o carimbo e apagava, por baixo dos panos, o aviso de superfície que
    mudou depois do processamento. Agora o aviso continua até o botão Terreno processar de
    novo.
  - Ao abrir o desenho, com interface, ele roda na primeira folga do AutoCAD, para não
    segurar a abertura, e escreve uma linha: "CLIVUS Terreno carregado: ...".
  - O `ExigirTerreno` usa o mesmo carregamento quando há carimbo (sem carimbo, o
    Reprocessar acha a única superfície, como antes). Área, Alinhamento e Coordenada passam
    pelo `ExigirTerreno` ou pelo `TerrenoDoDesenho.Garantir`, e as janelas da drenagem pelo
    `Garantir`.
  - Nível 2: a segunda metade do `clivus-carimbo` chama, pelo gancho de teste, o mesmo
    carregamento (no Core Console não há folga). Confere "CLIVUS Terreno carregado" e o
    DBMOD igual antes e depois.
- 39.3 e 41.2 (30/10/2026, à noite), pedidos da tela: "o 3d no navegador cagou forte, no 3d do
  cad não tem isso"; "a ferramenta 'conforme objetos' não reconhece a área [...] tinha VÁRIAS
  linhas sobrepostas e aí buga a ferramenta [...] isso deveria ser percebido pela
  ferramenta"; "quero um botão para APAGAR área, desde que esteja vazia, se não estiver, o
  apagar vai apagar as mesas, aí quando tem mesas, quero as opções de limpar, regerar,
  recalcular". O que achei e assumi:
  - **O 3D não errou.** Numa cópia do desenho do Aurora (PLAT A_Perfiles, 7 superfícies), o
    terreno de hoje é o "202605 TN LEV MAYO", reprocessado às 23:24. Na maior parte da usina,
    a cota que cada pilar gravou ao ser desenhado bate com ele (mediana de 1 mm). Numa
    região, porém, o terreno de hoje está até 5,3 m acima e 2,6 m abaixo dessa cota: 118
    mesas foram geradas antes de o terreno mudar ali. O navegador mostra o terreno de hoje,
    então o chão aparece por cima delas.
  - O 3D (Ver e Publicar) passa a avisar: quantas mesas, quanto o terreno subiu e desceu,
    quais, e que se recalculam pela janela Áreas › Recalcular (ou Regerar). A conta é
    `TerrainDrift`: a cota gravada no pilar (`GroundZ`) contra o terreno de hoje no mesmo
    X/Y, com 30 cm de tolerância.
  - **As linhas sobrepostas:** o Conforme objetos, o Regerar, a Usina, a Fileira e o Apagar
    tudo contavam as áreas pelo **registro** e só aceitavam o clique na polilinha da área. O
    registro trazia as áreas apagadas à mão (o Validar do Aurora: "4 áreas registradas não
    estão no desenho"), e o clique na linha do cliente, por cima, era recusado. Agora
    (`EscolhaDeArea`):
    - as áreas vêm do desenho, pelo XData;
    - o clique vale na linha ou dentro da área: a borda mais perto, até 15 pixels (no
      mínimo 0,5 m), ou a menor área que contém o ponto;
    - entre áreas repetidas, vale a do registro, com aviso;
    - a área repetida não nasce: o Área recusa, antes de pedir o nome, um contorno igual
      ao de uma área que já existe (`Polygons.SameOutline`, 5 cm);
    - a janela Áreas avisa as repetidas e as do registro que não existem mais (com o
      Reindexar).
  - **As ações por área** (janela Áreas, segunda linha de botões):
    - **Limpar mesas** é o Apagar tudo;
    - **Regerar** é o Refazer, que pode perguntar o alinhamento;
    - **Recalcular** (novo, `CLIVUS_AREA_RECALCULAR`) refaz onde estão as mesas com o
      centro na área;
    - **Apagar área…** (novo, `CLIVUS_AREA_APAGAR`) apaga direto a área vazia. Com mesas, a
      janela pergunta e apaga as mesas junto. A repetida sai sozinha (as mesas são da
      outra). Também saem a hachura, o registro e o "segue os objetos".
    - Os botões mandam o comando para a linha de comando, com a área pedida pela janela,
      que vale uma vez, por 15 s; a seleção do AutoCAD não serve porque os ESC a apagam. U
      desfaz. A lista se lê de novo quando o comando termina.
  - Testes: no Geo, a distância até a borda e a mesma linha (com os vértices do drapeado, em
    outro sentido); no Core, `TerrainDrift`. No nível 2, `clivus-areas-acoes`:
    - a repetida é recusada;
    - o clique na linha do cliente por cima da Oeste acha a Oeste e recalcula as mesas dela;
    - a Leste vazia é apagada sem pergunta;
    - na Oeste com mesas, o Enter não apaga e o Sim apaga;
    - o U devolve a área e as mesas;
    - depois o Validar não acha registro sobrando.
  - Não deu para renderizar a janela Áreas fora do Civil 3D (ela nasce de um Document): os
    botões novos ficam para a tela.
- 42.1 (31/10/2026), a revisão das sombras. Pedido do Renan: "Esta marcando que sombreou o
  modulo, mas nao marca a sombra no chao [...] QUERO VER o desenho das sombras no chao,
  rachurada [...] Falta o botão apagar sombras, ocultar sombras e mostrar sombras [...] o
  botão para escolher o que quero que seja calculado [...] tudo gera sombra, as mesas geram
  sombras e eu nao estou vendo, o terreno gera sombra se tiver barranco [...] O PVcase pinta
  de magenta as sombras". O que fiz e assumi:
  - **A sombra é hachura SOLID magenta (255,0,255), 50 % transparente, com a borda magenta**,
    na camada `CLIVUS_SOMBRA` (nasce magenta; a que ainda tinha o cinza de antes vira
    magenta, a cor escolhida pelo usuário fica). A etiqueta vai na `CLIVUS_SOMBRA_TEXTO`, em
    magenta.
  - **No chão:** a mancha é cortada nos ladrilhos de 8 m de uma grade fixa do desenho
    (múltiplos de 8 m), e cada pedaço ganha o plano de mínimos quadrados do terreno debaixo
    dele, levantado até 4 cm acima do ponto mais alto; se o terreno do pedaço não cabe num
    plano (a folga variando mais de 5 cm), o pedaço se divide em quatro, até 1 m
    (`GroundPatches`). Os pontos conferidos: 9 × 9 na caixa do pedaço, o contorno a cada
    oitavo do lado e os vértices do TIN que esses pontos tocam. A borda segue o terreno a
    4 cm, cortada nos mesmos ladrilhos (para o escopo da seleção apagar ladrilho inteiro).
  - **O chão debaixo das mesas não é desenhado**: em planta, ali se vê a mesa (o módulo é
    caixa em wireframe; a hachura do chão por baixo dele parecia sombra no módulo). Decisão
    minha.
  - **Sobre a mesa:** a parte do plano dos módulos na sombra, 3 cm acima dele (a caixa do
    módulo sobe 2 cm), numa hachura por mesa, no plano dela.
  - **Ordem de desenho:** a sombra sobre as mesas vai para a frente; a do chão fica logo
    abaixo da primeira peça de mesa (módulo, face ou contorno).
  - **O relevo faz sombra** (`TerrainShadow`): o terreno vira uma grade de cotas de 1 m (2 m
    acima de 25 ha), com até 500 m em volta de quem recebe; a cada passo, linhas paralelas
    ao sol varrem a grade guardando o maior "z + s·tan(elevação)": o ponto está na sombra se
    esse máximo, até meio passo antes dele, passa da cota dele (com 5 cm de folga). Recebem
    as células de 1 a 2 m do chão do escopo e os pontos de 0,5 m do plano de cada mesa do
    escopo. Saída cedo: se o sol mais baixo do período está acima do chão mais íngreme em
    volta (a maior declividade dos triângulos), o relevo nem é montado; e em cada passo,
    se o sol está acima da rampa mais íngreme da grade na direção dele, o passo não varre.
  - **A fração do módulo pelo relevo agora é ponto a ponto, na mesma varredura** (os 8
    pontos de cada face), e não mais a face inteira pelo horizonte do centro da mesa: o
    número bate com o desenho. O horizonte continua no `ShadingModel` para quem não passa
    a varredura (os testes antigos).
  - **O módulo não é mais pintado.** O registro `SOMBRA_MOTIVOS` passou a V2, por módulo:
    `T|mesa|período` para cada mesa calculada e `M|handle|mesa|fração|causa|quando|período`
    para cada módulo com sombra. Um escopo novo troca só as mesas dele. O V1 é lido (a mesa
    de cada módulo sai do bloco) e a primeira troca grava em V2. Os módulos pintados por
    desenho antigo (`SOMBRA_PINTADAS`) voltam à cor no Apagar, no cálculo do escopo deles e
    na Limpeza.
  - **O escopo** (`ShadowScopePlan`): recebem as mesas do escopo (na área, pelo centro do
    contorno, dentro de duas a menor, a regra do resumo por área). O chão de cada área é o
    território dela (a área menos as menores dentro dela); na seleção, os ladrilhos de 8 m em
    volta das mesas escolhidas (8 m de folga, fixos, para o Apagar achar os mesmos),
    repartidos pelas áreas; no desenho inteiro, as áreas e, fora delas, os ladrilhos em
    volta das mesas de fora. Fazem sombra as mesas e as árvores até a margem em que alcançam
    quem recebe com o sol mais baixo do período (no máximo 200 m) e o relevo (até 500 m).
    Cada entidade de sombra leva no XData ("Sombra", versão 1, os dois primeiros campos como
    antes): o primeiro instante, onde (Chao, Mesa, Texto), a área (GUID ou `SEM_AREA`), o
    período e o ladrilho ("x;y") ou a mesa (GUID). Gerar ou Apagar num escopo leva as
    entidades com uma área dele, as das mesas escolhidas e as dos ladrilhos da seleção; as
    de antes de 31/10/2026 (sem área) são julgadas pela posição.
  - **Comandos novos:** `CLIVUS_SOMBRAS_OCULTAR`, `CLIVUS_SOMBRAS_MOSTRAR`,
    `CLIVUS_SOMBRAS_ANALISES` (com inglês e espanhol) e, para o nível 2,
    `CLIVUS_SOMBRAS_ESCOPO_AUTO`, `CLIVUS_SOMBRAS_APAGAR_AUTO` e
    `CLIVUS_SOMBRAS_ANALISES_AUTO`. O `CLIVUS_SOMBRAS_APAGAR` digitado pergunta
    [Tudo/Area/Selecao] (Enter: tudo). O `CLIVUS_SOMBRAS_AUTO` continua igual (desenho
    inteiro). Ganchos de teste: `clivus-sombras-pela-janela` aceita o escopo,
    `clivus-sombras-acao` (apagar, ocultar, mostrar) e `clivus-sombras-modulos`.
  - **A janela:** "Calcular" (o escopo; abre em Uma área, ou no Desenho inteiro sem área), a
    área, as caixas das várias áreas, "Pegar seleção" (as mesas selecionadas no desenho); o
    resultado numa ou duas linhas (o detalhe vai para a linha de comando); Gerar, Apagar
    (no desenho inteiro, pergunta), Ocultar, Mostrar, Ver análises…, Por que essa sombra?.
    Gerar liga as camadas das sombras se estavam escondidas.
  - **Ver análises:** janela solta com a árvore área › mesa › módulo, do pior para o melhor,
    com %, causa e quando; o clique dá zoom (pelo menos uns 25 m na tela) e seleciona a
    mesa ou o módulo. Lê de novo sozinha quando a janela de sombras gera ou apaga.
  - **Ficou para conferir na tela ou fora do escopo:** o 3D no navegador continua lendo só
    as bordas (Polyline3d "Sombra"); a borda do chão agora vem em trechos abertos por
    ladrilho, e a hachura é entidade nova (o outro trabalho, o do 3D, precisa ler as
    hachuras). As frases novas estão sem tradução (os testes de idioma ficam vermelhos até
    o catálogo ser atualizado).
  - **Desempenho, medido fora do CAD** (Core em Release, num terreno ondulado de ±15 m com
    um barranco de 8 m, uma área de 300 × 300 m com 300 mesas e 12 mil módulos, um ano com
    um dia por semana de 30 em 30 min, 1247 passos com sol):
    - o relevo: 0,4 s para montar a grade, 1,7 ms por passo (934 dos 1247 varreram), 2 s no
      ano; os 3132 pedaços do chão em 0,15 s;
    - a sombra mesa a mesa nos módulos (`ShadingModel`, já existia): 447 ms por passo
      antes, 140 ms agora (o bloco de 8 × 8 células todo abaixo do raio é pulado, e o raio
      para ao sair da caixa das faces; a resposta é a mesma, conferida contra a força
      bruta): no ano inteiro dessa área, uns 3 minutos;
    - a mancha das mesas no chão (`ShadowFootprint`, já existia): 7 ms por passo, mas a
      união dos polígonos de todos os passos pesa (4 s para 63 passos de 300 mesas): no
      ano, perto de um minuto e meio.
    - Numa área de uns 3 mil módulos, o ano fica por volta de um minuto; o Solstício de
      inverno, em segundos. O desenho inteiro de 16 mil módulos no ano inteiro passa de dez
      minutos: área por área, como o Renan disse que vai fazer.
- 42.2 (31/10/2026), o que assumi. Pedidos do Renan no dia: a sombra no 3D do navegador
  ("talvez fazer o navegador calcular"), os pilares modelados, o logo e a propaganda no link
  público, e o item Resumo nas camadas.
  - O navegador calcula: a cena leva `local` (latitude, longitude e o fuso de
    `SombrasCommands.FusoDoLugar`) e o `sol.js` é o `SolarCalculator` portado, conferido com o
    node contra o C# em 42.592 instantes (16 lugares, 5 anos, 11 horas por dia): no máximo
    4e-9° no azimute e 6e-13° na elevação. O teste `OSolDoNavegadorUsaAsConstantesDoNoaa` pega
    a constante trocada de um lado só.
  - Privacidade (plano/seguranca.md, como a origem zerada): no envio ao servidor a latitude e
    a longitude vão a 0,1° (uns 11 km; o sol muda menos de 0,05°). A página local leva 7 casas.
  - Os eixos: desenho (x leste, y norte, z cima) vira three (x, z, −y); o vetor do sol (leste,
    norte, cima) vira (leste, cima × exagero, −norte): com o exagero vertical a sombra cai no
    chão onde cairia sem ele. Conferido de cima, com um poste de 10 m em Itatiba, 21/06: às 9h
    a sombra vai para sudoeste com 21,4 m (calculado 21,7), às 15h para sudeste com 18,8
    (19,05) e ao meio-dia para o sul com 10,6 (10,5).
  - A sombra: a câmera da sombra cobre a usina vista do sol (mesas, pilares, árvores,
    canaletas e estrutura, com folga de 5 % ou 10 m) e, em profundidade, o mundo todo (o morro
    entre o sol e a usina faz sombra nela). Mapa quadrado de 1024 a 4096 (2048 no celular),
    uns 10 cm por texel, sempre do mesmo tamanho; só é refeito quando muda o sol, uma camada ou
    o exagero, não a cada quadro. Viés de um texel ao longo da normal: as faces dos módulos e do
    terreno passaram a ir com a normal para cima (o terreno ia para baixo), sem mudar a cor.
  - A luz com o sol: céu 0,6 e sol 0,75 (a de sempre, sem o lugar, continua 0,75 e 0,65); a
    luz direta cresce até 6° de altura; abaixo do horizonte, só o céu e o texto "Sol abaixo do
    horizonte".
  - Abre hoje, na hora de agora do relógio da usina (o fuso da cena, não o do navegador); de
    noite, às 10h. Os atalhos são os dias do equinócio e do solstício calculados para o ano
    (2026: 20/03, 21/06, 22/09, 21/12; em 2027 o de setembro é 23/09). "Animar o dia": do nascer
    ao pôr do sol, uma hora por segundo, e de novo.
  - Os pilares: uma caixa por pilar numa malha só (InstancedMesh), do pé ao topo, na largura
    (escala X do bloco), na profundidade (escala Y) e no giro (rotação); a cor do aço da
    estrutura. Cena antiga, sem a seção: 0,10 × 0,10. Antes eram um traço, que não recebia
    sombra.
  - O resumo é o de `AreasDoDesenho.Ler(documento).Resumo`; por área só quando o desenho tem
    área, com a linha de fora das áreas marcada (cada visualizador escreve o nome dela no
    idioma da página). Liga no PC e desliga no celular.
  - O logo é o horizontal escuro do plugin (cópia em `Visualizador3D/clivus-logo.svg`, que o
    Core embute): 92 px no canto da página local. Na pública, 132 px (108 no celular), com a
    propaganda, que recolhe no × (e, no celular, sozinha no primeiro toque no 3D) e volta no
    logo. O botão leva a `CLIVUS_SITE_URL/{pt|en|es}/`, por padrão https://clivussolar.com, que
    ainda não tem DNS: no Coolify, pôr o domínio temporário do site até lá.
  - Traduções: 14 frases novas sem inglês e espanhol (o Renan traduz depois); até lá, os dois
    casos de `TranslationCatalogTests` falham.
  - Revisão (subagente), corrigido:
    - a face dupla de costas para a própria normal (parede de canaleta com os triângulos ao
      contrário, verso do módulo) fazia sombra nela mesma de frente para o sol: o afastamento
      da consulta à sombra vai agora para o lado do sol (`onBeforeCompile` nos materiais
      Lambert); conferido com duas paredes de enrolamentos opostos;
    - a guarda permanente do JS contra o C#: `tests/dados/sol-referencia.json` (48 instantes
      do C#), o teste que garante que ela é o C# de hoje e `tools/conferir-sol.js`, que o
      placar roda quando há node (linha "Sol (JS)");
    - a seção do pilar abaixo de 1 mm ia como 0 e o servidor recusaria a publicação;
    - o aviso sai também com o lugar inválido e quando o resumo falha;
    - o dia claro que passa da meia-noite do relógio (fuso longe da longitude) e a data
      apagada no campo;
    - a caixa com NaN não estraga mais a sombra da cena inteira; no servidor, número
      gigante dá 400 (não 500) e o resumo vai até 10^9, com mesas e módulos inteiros;
    - o nível 2 da 39.2 exige o resumo nas duas cenas (passava com os dois nulos).
  - Fica para o Renan (privacidade): o 0,1° é arredondado no plugin; o servidor guarda o que
    chegar. E dentro de 11 km a forma do terreno dá para casar com o relevo público (SRTM):
    o lugar exato não sai, mas a região sim.
  - No servidor, os scripts do 3D levam o hash do conteúdo na URL (`?v=`): sem isso, quem já
    tinha aberto um 3D juntava o JS velho do cache com a página nova.
  - Desempenho: aqui não há placa de vídeo (o Chrome sem cabeça desenha na CPU). Numa usina de
    16.016 módulos e 6.864 pilares: 7,7 quadros/s com a sombra parada, 4,5 animando e 9,2 sem
    sombra. Só a proporção vale; o número de verdade é na tela.

- 42.3 (31/10/2026, manhã), o que achei e o que fiz. Na sessão das 09:56 o Renan abriu o
  Aurora (`Marcheng\PLAT A_Perfiles.dwg`) na 0.1.0 com o atualizador e:
  - **Gerar sombras deu "Nenhuma mesa de Area 1: nada a calcular"** (a linha de comando
    registrada mostra). O desenho tem a "Area 1" (das 20:04 de 30/10, camada
    `CLIVUS_AREA959`) e a "area1" (das 22:21) quase iguais: a mesma caixa, 214,0 × 75,2 m
    contra 214,3 × 75,2. A mesa conta na menor (a regra do resumo), a "area1", e a janela abria
    com a "Area 1" escolhida. Nada foi desenhado no chão por isso.
  - **O roxo dos módulos era a pintura da versão antiga** (a sessão das 00:24, na 632996d):
    a 42.1 só a devolvia num cálculo do escopo dela ou no Apagar, o Ocultar não a escondia
    (ela é a cor do bloco, não camada) e o 3D a mostrava como sombra.
  - **O Mostrar existia, mas ficava cortado**: a linha de botões era um DockPanel com o grupo
    da direita (Ver análises, Por que, Fechar) por cima do fim do grupo da esquerda.
  - **O módulo "comido" no 3D** era o terreno: a grade de 2,8 m da página ficava acima do TIN
    no pé do talude. Na cena publicada às 09:59 (16.492 faces), 3 faces tinham a grade por
    cima: 0,43 m, 0,17 m e 0,03 m (as duas primeiras roxas, as que a imagem mostra).
  - O que mudou:
    - `ShadowScopePlan`: a área escolhida leva as que estão dentro dela (99 % da área de
      dentro na de fora; duas quase iguais vão uma com a outra). O teste
      `AAreaGrandeNaoLevaOChaoNemAsMesasDasMenoresDentroDela` dizia o contrário (a Usina com a
      Oeste e a Leste dentro não levava nada) e virou `AAreaGrandeLevaAsMenoresDentroDela`:
      a regra mudou pela tela, não para o teste passar. Mais `DuasAreasQuaseIguaisVaoUmaComAOutra`
      e `AAreaQueSoEncostaNaoVaiJunto`.
    - A janela: Ocultar e Mostrar num botão que diz o que faz ("Ocultar sombras" com elas à
      vista, "Mostrar sombras" com elas escondidas); as linhas e os botões em WrapPanel; a
      lista de áreas com "· N mesa(s)" e aberta na primeira que tem; ao abrir, o roxo da versão
      antiga volta à cor de antes em todo o desenho (`TirarPinturaAntiga`, fora de comando,
      registro `SOMBRA_PINTADAS` zerado) e a janela diz quantos.
    - O escopo saiu da janela para o `SeletorDeEscopo`, que a exportação para o PVsyst usa.
    - O 3D: a cena leva o TIN (`terreno.tin`, vértices juntados e triângulos; até 1 milhão de
      triângulos, acima disso só a grade, que continua indo); a cor de antes nos módulos ainda
      pintados pela versão antiga (o desenho não muda); `momentos` (os instantes, no relógio da
      usina, da pior sombra de cada módulo no registro dos motivos, com quantos módulos) e,
      nas faces desses módulos, o momento e a fração; `sombrasGrupo` (a hachura de cada anel,
      para preencher com os furos). Contrato no `clivussolar` (branch `momentos-3d`).
  - Nível 2: o `clivus-sombras-escopo` calcula a Usina (a de fora) e confere sombra na Oeste e
    na Leste; pinta 5 módulos como a versão antiga e confere que o caminho da janela os
    devolve (e que a segunda vez não acha nada); o `clivus-3d` confere o TIN, o `sombrasGrupo`
    e o momento de cada face com sombra. Gancho novo: `clivus-sombras-acao "tirar-pintura"` e
    `clivus-renderizar-janela` (a janela num PNG, sem abrir).
- 42.4 (31/10/2026), o PVsyst por escopo. O CLIVUS_EXPORTAR com interface abre a janela "Exportar
  para o PVsyst" (o `SeletorDeEscopo`: Seleção, Uma área, Área + seleção, Várias áreas,
  Desenho inteiro; a seleção feita antes do botão abre em Seleção) e só depois a janela do
  Windows do arquivo. Vão as faces dos módulos das mesas do escopo (a mesma conta das
  sombras: a área com as de dentro). Sem interface, a seleção de sempre. Para o nível 2,
  `CLIVUS_EXPORTAR_ESCOPO_AUTO` (escopo, áreas, letreiros e o arquivo), conferido no
  `clivus-sombras-escopo`: a Oeste, as duas, a F1.1, tudo e a Usina (a Usina = as duas, a F1.1
  = 1 mesa).
- 42.5 (31/10/2026, tarde), as áreas e o 3D. O que achei: o Apagar área da janela mandava o
  comando, que perguntava "Apagar? [Sim/Não]" na linha de comando, que o Renan não via; e a
  pergunta dizia que a "Area 1" (1 m maior que a "area1", por fora) ia levar as 78 mesas da
  outra. Agora o Apagar área, o Limpar mesas e o Recalcular usam as mesas DA área, pela regra
  da própria janela (o centro dentro; dentro de duas, a menor: `AreaAcoesCommands.MesasDaArea`),
  e a pergunta é da janela. O "Walk and Fly" que aparecia no clique do meio não é do Clivus: é
  o modo de navegação do AutoCAD que ficou gravado (teclas 6 e 7 dentro do 3DORBIT); a saída é
  3DORBIT, botão direito, Other Navigation Modes, Constrained Orbit (ou a tecla 1), Esc. Não
  mexi no registro. O 3D: a leitura do desenho fica na linha do AutoCAD (é a regra da API),
  avisando a janela a cada 4000 objetos; a cena em JSON, a compressão e o envio em pedaços de
  64 kB correm em outra linha, e o Cancelar para o envio (nada é publicado).
- 42.6 (31/10/2026, tarde), as análises. O que assumi:
  - O escopo fica em cima da janela, um só para as seis abas, e abre no **desenho inteiro**
    (as análises sempre foram do desenho inteiro); é o seletor das sombras (as áreas com
    "· N mesas", Desenho inteiro, as mesas escolhidas, os botões de escolher no desenho).
    Cada botão mexe só nas mesas do escopo: inserir apaga e escreve os textos delas, apagar e
    tirar cores só nelas, analisar refaz a pintura só nelas (a pintura de fora continua
    gravada e o Tirar cores dela continua funcionando), quantificar conta só elas. O texto
    sem mesa (a mesa foi apagada) só sai no desenho inteiro.
  - Os "Escolher área/mesas no desenho…" viraram dois comandos que servem às duas janelas
    soltas: `CLIVUS_ESCOLHER_AREA` e `CLIVUS_ESCOLHER_MESAS` (inglês CLIVUS_PICK_AREA e
    _TABLES, espanhol CLIVUS_ELEGIR_AREA e _MESAS); o seletor que pediu recebe a escolha. Os
    `CLIVUS_SOMBRAS_AREA` e `_MESAS` da 0.1.4 saíram.
  - A quantificação grava o lugar ("desenho inteiro", "Area Oeste", "12 mesa(s)
    escolhida(s)"): a aba Quantidades e a coluna nova **Onde** do Excel dizem de onde é cada
    número. As gravadas antes são do desenho inteiro.
  - Fundo e borda: duas caixas na seção Textos de cada aba ("Texto com Fundo Borda"), cada
    análise com as suas. Fundo é a máscara na cor da tela (a mesma das tags, 05/10); borda é
    o quadro do próprio texto, na cor dele. Marcar ou desmarcar muda já os textos dessa
    análise no desenho todo (não só no escopo) e os próximos. O padrão é com fundo e sem
    borda; os textos que já estavam no desenho ganham o fundo na primeira vez que a janela
    abre (a caixa nasce marcada e o desenho fica igual a ela). Com fundo, os textos vão para
    a frente na ordem de desenho, senão a máscara não esconde o módulo redesenhado depois.
  - As colunas do biposte: nas três análises de pilar (livre, enterrado, total), quando o
    desenho tem biposte, os limites viram uma grade com uma coluna por linha de pilar
    (Pilar da frente, Pilar de trás e, se o desenho também tem monoposte, Monoposte); a cor
    de cada lado é uma só ("a cor pode ser o mesmo para ambos"). Com colunas, a caixa vazia é
    "sem limite nessa linha"; precisa de pelo menos um limite numa coluna. Cada texto de pilar
    guarda a linha dele (XData `TextoAnalise` versão 2, só no biposte) e cada pilar é pintado
    pelo limite da linha dele. A quantificação conta cada linha pelos limites dela; na janela,
    no Excel (uma linha por linha de pilar: "pilares da frente", "pilares de trás") e na
    linha de comando. A ponta baixa, a ponta alta e a declividade não têm linha: no biposte
    elas já são uma por pórtico.
  - A unidade da declividade trocada reinsere só nas mesas que já tinham texto (antes
    reinseria no desenho inteiro, o que espalharia a análise de uma área).
  - O nível 2 `clivus-analises-escopo`: a usina biposte com a Usina, a Area Oeste e a Area
    Leste; inserir na Oeste (só lá, um texto por pilar, com fundo), na Leste (a Oeste igual),
    fundo e borda em todos, analisar a Oeste com a frente acima de 100 m e a trás acima de
    0 m (só os 288 textos e pilares de trás da Oeste), quantificar a Oeste por linha, tirar
    as cores da Leste (a Oeste fica) e da Oeste, apagar a Oeste (a Leste fica), a declividade
    só na Leste e a janela desenhada num PNG (conferida: a grade com Pilar da frente e Pilar
    de trás).
  - A revisão (subagente em worktree, no 1002064) achou e eu corrigi:
    - **bug:** o texto de pilar inserido antes (XData versão 1, sem a linha) era pintado pela
      regra da monoposte, que num desenho só de biposte não tem coluna na janela. Agora ele
      pinta pela linha do pilar mais perto em planta (o texto nasce no topo do pilar); o
      nível 2 regrava os textos da Leste na versão 1 (gancho `clivus-analises-texto-antigo`)
      e confere que só os de trás pintam;
    - com "Desenho inteiro" marcado, as caixas das áreas ficavam desligadas: agora ficam
      sempre ligadas; marcar uma área desmarca o desenho inteiro e vice-versa (vale também
      na janela das sombras);
    - a ordem de desenho do fundo era pedida com o model space só para leitura: agora para
      escrita (num desenho sem a tabela da ordem, ela nasce ali);
    - com escopo, a pintura velha de mesa com contorno deformado não saía: agora todas as
      peças das mesas do escopo entram na conta;
    - o pedido ao desenho ficava preso à janela fechada: a janela solta o pedido ao fechar, e
      o comando vai depois de dois Esc (um comando no meio levaria o nome como resposta);
    - a montagem da regra pelas caixas foi para o Core (`ThresholdForm`, 6 testes), com o
      lado desmarcado desligando também as linhas sem coluna; o nível 2 analisa a Oeste por
      ela (`analisar-como-janela`); o erro da regra sai na seção que pediu;
    - a linha de comando do biposte não mostra mais a soma com o limite da monoposte; o
      "desenho inteiro" vai sem nome para o registro (a tela e o Excel o dizem no idioma da
      hora); o fundo dos textos antigos ao abrir a janela é uma varredura só.
  - A segunda rodada (14h40, a 0.1.5 na tela):
    - Renomear: a coluna do nome tinha a ligação só de ida, e o DataGrid do WPF força coluna
      assim a só leitura: o F2, o menu e o clique devagar não abriam a edição. Agora a
      ligação é de ida e volta, mas a volta não grava (`SoDeIda`); o menu abre a edição
      depois de fechar (senão o foco que volta fecha a caixa). O nível 2 renomeia pelo F2 e
      pelo menu na janela de verdade (`RenomearComoNaTela`); com a ligação antiga ele falha.
    - O texto atrás dos módulos: o print mostrava o fundo cortado na diagonal, que é a vista
      sombreada escondendo pela cota (a face inclinada dos módulos cruzando o texto em pé na
      cota do topo do pilar). Os textos de ponta e de pilar agora ficam deitados no plano dos
      módulos, 0,15 m acima na normal, como a seta da declividade sempre ficou; os antigos
      se deitam na primeira abertura da janela (marca `ANALISE_TEXTOS_NO_PLANO`).
    - O alinhamento: as caixas do seletor (Desenho inteiro e as áreas) na mesma grade, todas
      da largura da maior.
    - A navegação: o log mostra cada pan do botão do meio como `3DPAN2`, o pan 3D do
      AutoCAD (vista em perspectiva ou estilo visual 3D), em que o zoom e o pan ficam
      lentos perto do alvo; isso é da vista, não do Clivus. O que era do Clivus: a caixa
      do kWp da seleção relia todos os objetos selecionados no fim de cada pan (agora
      reaproveita a conta se a seleção é a mesma), o menu do botão direito abria cada
      objeto selecionado (agora, achada a mesa, só abre curvas), e cada leitura das áreas
      lançava uma exceção por área do registro sem polilinha (agora `TryGetObjectId`).
    - A revisão da segunda rodada (subagente em worktree, no 1cf0072) achou e eu corrigi: o
      seletor das sombras e do PVsyst não abria mais no desenho inteiro num desenho sem área (o
      Desenho inteiro mora na grade das áreas: a conta de "sem área" olhava as filhas da grade);
      a migração dos textos estendia o plano da mesa sem limite (agora só o texto a até 1,5 m do
      contorno, em planta; o que foi afastado à mão, ou ficou para trás num MOVE da mesa, fica
      como estava, com uma linha no log), o "já deitado" passou a ser a distância ao plano na
      normal (0,15 m, exato em qualquer inclinação), a camada travada é respeitada, a marca só é
      gravada quando o desenho tem texto de ponta ou de pilar, e a tela é redesenhada; com a coluna
      do nome editável, o DataGrid abria a edição com a primeira letra digitada e no primeiro
      clique (agora só o F2, o menu e o clique devagar de verdade, com o tempo do duplo clique do
      Windows; o Enter grava e fica na linha); a caixa do kWp guardava a seleção antes da conta e
      mostraria a conta velha depois de uma falha. O nível 2 confere, pelo DXF (210 e 10) contra o
      plano dos quatro cantos do contorno, o texto de pilar deitado 0,15 m acima; e a migração de
      textos postos em pé (o afastado 30 m fica; a segunda migração não mexe em nada).
  - Ficaram como estão, de propósito: abrir a janela pela primeira vez grava o fundo nos
    textos antigos (o U desfaz); o Excel guarda a última quantificação de cada análise, e a
    coluna Onde diz de onde ela é (quantificar a Leste depois da Oeste troca o número); o
    desenho salvo nesta versão, aberto numa anterior, perde a regra por linha, a quantificação
    nova e o reconhecimento do texto de pilar do biposte (a versão antiga não os lê).

## Roteiro de tela (38.1 a 42.6)

1. **Nova estrutura (38.1 e 38.5):** Configurações › Estruturas › Nova estrutura… › **Fixa**.
   A janela de Mesa abre na aba "Perfil e módulo"; clicar no cartão **Biposte** abre a aba
   Biposte, no biposte genérico (sem mão francesa e sem tirante), com as seções Pórtico, Mãos
   francesas, Terças, Tirantes e Seções das peças. Mudar um campo, clicar Monoposte e voltar
   para Biposte: o campo continua mudado. No corte, conferir os dois pilares, a viga e as
   cotas T2, T2t e 3,000 m entre os pilares. "Usar esta mesa", Salvar no desenho, fechar e
   reabrir: ela está na lista com "(biposte)" e na Escolha das estruturas.
2. **Desenhar (38.2):** marcar só a bifila na Escolha, pôr a faixa da PB do projeto em
   0,84 a 1,60 (a GC800 pede 0,832 na face do módulo) e processar uma área. Orbitar uma
   mesa: 12 pilares (6 pórticos com dianteiro e traseiro), viga em cada pórtico, mãos
   francesas, 4 terças e o X de tirantes nos vãos 2 e 4 da linha de trás. Apagar uma mesa:
   tudo some junto.
3. **Ferramentas (38.3):** Análises › Ponta baixa › Inserir: um texto por pórtico, não dois.
   Pontas numa mesa bifila: os pilares continuam com 2,20 m de enterro. Trocar uma
   monoposte por bifila e voltar.
4. **Excel (38.4):** Exportar Excel com "Estrutura (biposte)" marcada: aba Estrutura com
   pilares por linha, vigas, mãos francesas, terças (barras e emendas) e tirantes; aba
   Pilares com a coluna Linha.
5. **3D (39.1):** Ver 3D numa área bifila: a estrutura aparece em cinza aço embaixo dos
   módulos.
6. **Mesas conforme os objetos (40.2):** no `PLAT A_Perfiles.dwg` (Panamá), criar a área
   pelo contorno de `_CONTORNO`, marcar só o biposte (GC800) na Escolha das estruturas e
   pôr o azimute no **Sul** (Configurações › Parâmetros, botão Sul; com Norte aparece o aviso
   do hemisfério). Processar › Conforme objetos › Escolher (os objetos) › **Direita**, Centro
   ou Esquerda: é a única pergunta. As mesas saem em cima dos objetos, com a cota do terreno
   (o Z dos objetos não conta); com Direita, a ponta leste de cada mesa na ponta leste do
   objeto. O relatório diz quantos objetos viram mesa, as divergências (os repetidos), para
   onde as mesas olham e quanto a mesa tem a mais que o objeto. Depois Regerar área: refaz
   pelos objetos, com o mesmo alinhamento.
7. **Tudo com biposte (39.2):** numa área com o biposte em uso (faixa da PB 0,84 a 1,60),
   passar por tudo e comparar com uma área monoposte:
   - Processar: Usina, Regerar área, Regerar todas;
   - Edição: Recalcular, Recalcular pendentes, Pontas, Validar, Recontar, Renomear, Estado,
     Marcar pendente, Trocar mesa, Regerar fileira, Apagar (cores, textos, sombras, strings),
     Apagar tudo, Grupos; copiar, mover e apagar uma mesa com o AutoCAD (o vigia);
   - Análises: PB e PA (um por pórtico, "Contado em: pórticos"), parte livre, enterrada e total
     do pilar (cada pilar), declividade; cores e quantificar;
   - Tags: fileiras, mesas, módulos, strings;
   - Sombras: árvore, sombras, por quê;
   - Elétrica: String, Configuração elétrica (UC, trafo, inversores, alocar, skid,
     numeração), Resumo, Rota de cabos (CC, CA, MT, valas, recolocar, recalcular, cabos,
     resumo);
   - Saídas: PVsyst, Excel (todas as abas; a Estrutura só no biposte), 3D (a caixa
     "Canaletas, carport e estrutura do biposte" liga e desliga as peças), Publicar 3D,
     TermoVision;
   - Salvar, fechar e abrir o DWG: as mesas, os pilares e as peças voltam.
8. **PAN (38.6):** com o serviço local no ar (`tools\servico-local.ps1`) e
   `CLIVUS_SERVICO=http://localhost:8765` (sem ela, desde o 38.8, vale o item 10), janela de Mesa › Perfil e
   módulo › Carregar .PAN... › `Panama\Docs\JAM66D46-720_LB.PAN`: o modelo vira "JA Solar
   JAM66D46-720/LB", as medidas 2,384 × 1,303 × 0,033, a potência 720 e os dados elétricos
   enchem; a pergunta "Cadastrar agora, com o PAN?" › Sim: o módulo entra na lista. Fechar e
   abrir a janela, escolher esse módulo na lista: os dados elétricos vêm junto.
9. **Modelos, P1 e P2 (38.9):** janela de Mesa › aba **Modelos** (era Fabricantes): o
   primeiro cartão é o biposte genérico, depois as duas da Arctech. "Usar esta estrutura" na GC800: a
   aba Biposte abre (o nome não muda sozinho; o campo Nome está no alto da aba) e, no
   Pórtico, **P1 3,059**, **Livre sob o módulo 0,800**, MF1 1,244 (prende a 0,447 do topo do
   P1), **P2 3,888**, MF2 1,209 (a 0,773). Embaixo, a faixa que vale na usina. A vista lateral
   mostra cada peça com o nome e a medida, a altura livre, o chão caindo da folga do P2 e o
   enterro cortado. Na GC1000 (livre 1,000), digitar 0,8 no Livre: o P1 vira 3,059. Mudar o
   P1 para 3,2: o Livre sobe junto, e a vista acompanha. Apagar só o P1: o recado pede os
   dois juntos. "Usar este" no genérico: as peças de referência, sem tirante. Salvar no
   desenho, fechar e reabrir: o P1 e o P2 continuam. Processar uma área: a linha "Altura da
   estrutura" diz a faixa pelo P1.
10. **Módulos no servidor do Clivus (38.8):** sem `CLIVUS_SERVICO` e com a
    `CLIVUS_SERVIDOR_CHAVE`, abrir a janela de Mesa: embaixo do modelo, "Módulos do servidor
    do Clivus: 3". Carregar .PAN › `JAM66D46-720_LB.PAN` › "Cadastrar agora?" › Sim: o recado
    diz que ele foi para o servidor e espera a validação; o módulo fica escolhido, com os
    dados elétricos, e na lista aparece "(esperando a validação)". Carregar outro .PAN: o
    primeiro continua na lista. Fechar e abrir a janela: ele ainda está lá. No site, entrar
    com a conta Develop › cartão **Gestão**: o módulo pendente, com o .PAN para baixar.
    Aprovar. Reabrir o Civil 3D e a janela de Mesa: ele está na lista sem a marca
    ("Módulos do servidor do Clivus: 4"), e escolher ele traz os dados elétricos. Sem a
    chave (outro usuário do Windows): o recado do .PAN diz que o módulo vale só na janela, e
    "Cadastrar módulo..." diz que falta a `CLIVUS_SERVIDOR_CHAVE`.
11. **Norte ou sul (38.9):** Configurações › Parâmetros: os botões Norte e Sul ao lado do
    azimute. No desenho do Panamá, com Norte, aparece em vermelho o aviso do hemisfério
    norte; com Sul, some. Na janela de Mesa, embaixo da inclinação, a frase diz para onde os
    módulos olham.
12. **A lista de estruturas e a biblioteca (38.9):** Configurações › Estruturas: Remover
    "Mesa 28 módulos 2 2" (e as outras que voltavam), Salvar no desenho. A linha de comando
    diz que ela saiu também da biblioteca. Reabrir as Configurações: não volta. Na janela de
    Mesa, primeira aba: escolher um perfil em "Perfil salvo" › **Apagar da biblioteca**: ele
    some da lista.
13. **Áreas (41.1):** num desenho com várias áreas e mesas (o Aurora com as 7), Implantação ›
    botão pequeno **Áreas**: uma linha por área e o Total, que bate com Edição › Recontar.
    Clicar numa área: o resumo embaixo (tipos de mesa, pilares e metros, ocupação). Zoom (ou
    duplo clique). Hachurar todas: cada área com a cor da amostra, as mesas visíveis por
    baixo, o nome no meio; Tirar hachura numa só; Renomear (o nome de outra área é recusado);
    Exportar Excel: a aba Resumo e uma aba por área; Tirar todas.
14. **Salvar (38.10):** abrir o desenho do Aurora que ficou com as mesas vermelhas: a linha
    de comando diz "N mesa(s) estavam pendentes só porque o desenho foi salvo [...] voltaram ao
    normal, sem recalcular" e as mesas voltam às cores de antes (a do tipo de mesa). Salvar
    (Ctrl+S) duas ou três vezes: nenhuma linha VIGIA, sem travada, nada vermelho. Mover uma
    mesa com o MOVE: ela fica pendente e vermelha (o vigia continua valendo); Recalcular
    nela. Salvar, fechar e abrir: só ela tinha ficado pendente, e o conserto não a desfaz.
15. **Modelos (38.10):** janela de Mesa › aba **Modelos**: a seção "Genérico" e a seção
    "Arctech" (GC800 e GC1000). Apagar na GC1000: some, e no fim da aba aparece "1 modelo(s)
    apagado(s)". Fechar e abrir a janela: continua apagada. **Mostrar os apagados**: volta.
    Apagar as duas da Arctech: a seção some junto.
16. **Área pela polilinha 3D (38.10):** Implantação › Área › **Selecionar** › clicar numa
    polilinha 3D do cliente (fechada, ou com o último vértice em cima do primeiro em planta,
    em qualquer cota): a área nasce no terreno. Numa 3D aberta, a pergunta diz a quantos
    metros a ponta está do começo; Enter fecha, **Outra** deixa escolher outra.
17. **Logs (38.10):** nada a fazer na tela. Quando aparecer um erro, diga a hora: o
    `%LOCALAPPDATA%\Clivus Solar\clivus.log` tem cada comando com o tempo e os erros com a
    pilha, e a pasta `linha-de-comando`, ao lado, tem a linha de comando inteira.
18. **Salvamento automático (38.10):** `SAVETIME` 1, deixar o Civil 3D parado dois
    minutos com o desenho do Aurora aberto: nenhuma linha VIGIA, nada vermelho.
19. **3D com o terreno mudado (39.3):** no Aurora, Publicar 3D (ou Ver 3D): antes do link, a
    linha "ATENÇÃO: N mesa(s) foram assentadas num terreno diferente do de hoje", com as
    mesas. Áreas › a área delas › **Recalcular**; publicar de novo: o aviso some, e no
    navegador o chão não passa mais por cima das mesas.
20. **Áreas: apagar, limpar, regerar, recalcular (41.2):** no Aurora, Implantação › Áreas:
    os avisos embaixo da lista dizem as áreas do registro que não existem mais (rodar o
    Reindexar) e as repetidas. Numa área vazia, **Apagar área…**: some da lista sem
    pergunta. Numa com mesas: a pergunta, Não não apaga; Sim apaga a área e as mesas; U
    devolve. **Limpar mesas**, **Regerar** e **Recalcular** numa área: a linha de comando
    diz o que fez, e a lista se atualiza sozinha.
21. **A área pelo clique (41.2):** com a linha do cliente em cima da área, Processar ›
    Conforme objetos › clicar na linha (ou dentro da área): "Área: ..." e segue. Área › os
    mesmos vértices de uma área que já existe: "Já existe a área ..." antes de pedir o
    nome.
22. **O terreno ao abrir (38.11):** fechar e abrir o Civil 3D com o desenho do Aurora: logo
    depois de abrir, a linha "CLIVUS Terreno carregado: 202605 TN LEV MAYO (...)". Área, sem
    apertar Terreno: pede os vértices na hora. Fechar sem mexer em nada: o Civil 3D não
    pergunta se quer salvar.
23. **Sombras (42.1):** num desenho com duas ou mais áreas com mesas (o terreno girante),
    Sombreamento › Sombras:
    - Calcular **Uma área** (a 1), Solstício de inverno, Gerar sombras: no 2D Wireframe, a
      sombra das mesas no chão (entre as fileiras) e sobre os módulos aparece cheia, em
      magenta transparente, com a borda magenta; os módulos não mudam de cor; o resultado na
      janela cabe numa ou duas linhas ("N módulo(s) com sombra em M mesa(s) de Área 1; o
      pior: ...").
    - Orbitar: no chão, a hachura acompanha o terreno, uns 4 cm acima (nem afundada nem
      voando); sobre a mesa, logo acima dos módulos, só até onde a sombra pega.
    - Um barranco (talude íngreme virado para longe do sol, de manhã ou à tarde): a sombra
      dele no pé do talude; o detalhe na linha de comando diz se o relevo fez sombra.
    - Calcular a área 2: as sombras da 1 continuam. Apagar sombras com a área 1: só as da 1
      somem. **Várias áreas**, **Seleção** (clicar nas mesas, Pegar seleção) e **Área +
      seleção**: cada um troca só as sombras dele.
    - Ocultar e Mostrar: as sombras somem e voltam (nada é apagado).
    - **Ver análises…**: a árvore área › mesa › módulo, do pior para o melhor; clicar num
      módulo: zoom nele e ele fica selecionado.
    - Num desenho antigo, com módulos roxos da sombra de antes: Gerar na área deles (ou
      Apagar) devolve a cor de antes.
    - O tempo: uma área inteira no "Ano, sol acima de 20°" (o detalhe diz "Conta em ...").
24. **O sol no 3D (42.2):** num desenho com localização, Ver 3D (a página local): o painel
    tem "Sol", com a data de hoje e a hora de agora (UTC−3). Mexer na hora: a sombra das mesas
    anda no chão e nas mesas de trás. Clicar 21/06 e pôr 9h: sombras longas para sudoeste. ▶
    anima o dia; desligar "Sombras do sol": somem; exagero 3×: a sombra no chão fica no mesmo
    lugar. Num desenho sem localização, a linha de comando avisa e a página não tem o sol.
25. **Os pilares modelados (42.2):** chegar perto de uma mesa: os pilares são caixas na seção
    do pilar, giradas com a mesa, do chão até debaixo do módulo; no biposte, os dois pilares
    de cada pórtico. A camada Pilares liga e desliga.
26. **O resumo (42.2):** o quadro no canto de cima bate com a janela Áreas (o total, cada área,
    fora das áreas e cada tipo de mesa: mesas, módulos e kWp). O item Resumo das camadas o
    desliga.
27. **A página pública (42.2):** Publicar 3D e abrir no PC e no celular: o logo do Clivus no
    canto de baixo; o cartão "Usina projetada com o Clivus Solar" recolhe no × e volta no logo
    (no celular, recolhe sozinho ao tocar no 3D); "Conheça o Clivus Solar" abre a landing. O sol
    e o resumo como na local. No Aurora (usina grande), girar e animar o dia com a sombra e ver
    se fica liso.
28. **Sombras de novo (42.3), no Aurora:** abrir Sombreamento › Sombras:
    - a janela diz "A pintura roxa da versão antiga saiu de N módulo(s)..." e os módulos roxos
      voltam ao azul (uma vez só: abrir de novo não diz mais);
    - a lista de áreas mostra "area1 · N mesas" e a "Area 1" também com mesas (ela leva a
      "area1", que está dentro dela); abre numa área com mesa;
    - Gerar sombras (Instante, 09:00): a sombra magenta aparece no chão entre as fileiras e
      sobre os módulos;
    - o botão diz "Ocultar sombras": clicar esconde, e ele passa a dizer "Mostrar sombras";
      clicar de novo traz de volta. Nenhum botão cortado na borda da janela.
29. **O 3D com a análise (42.3):** depois do Gerar, Publicar 3D:
    - os módulos azuis (sem o roxo antigo), a sombra do CAD em magenta no chão e nos módulos;
    - no painel do sol, "Ver o momento que causa sombra": o sol vai para a hora da análise, os
      módulos com sombra ficam destacados e a sombra do sol cai sobre eles; ◀ ▶ passam pelos
      momentos; ▶ Animar o dia mostra a sombra passando;
    - o canto da mesa no pé do talude (o "comeu módulo" da imagem de 31/10) inteiro, como no
      CAD.
30. **O PVsyst por escopo (42.4):** Exportar para o PVsyst: a janela pergunta o que exportar
    (Uma área com "· N mesas", Várias áreas, Seleção, Desenho inteiro); Exportar… pede o
    arquivo; a linha de comando diz "EXPORTAR N mesa(s) de ..." e o arquivo abre no PVsyst só
    com aquelas mesas. Selecionar umas mesas antes de clicar o botão: a janela abre em Seleção
    com elas.
31. **A janela Áreas (42.5):** na linha de uma área, dois cliques dá zoom nela; F2 (ou o
    segundo clique devagar no nome) renomeia ali mesmo; o botão direito tem Zoom, Renomear,
    Hachurar, Tirar hachura, Limpar mesas, Regerar, Recalcular, Apagar área; a lixeira (ou
    Del) pergunta e apaga. No Aurora, apagar a "Area 1" (a de fora, quase igual à "area1"):
    a pergunta diz que ela não tem mesa dela, e as mesas da "area1" ficam. Embaixo, "Todas as
    áreas": Limpar, Regerar e Apagar perguntam antes; U desfaz.
32. **O 3D com andamento (42.5):** Publicar 3D no Aurora: a janela "Exportando o 3D para a
    web" com a barra e o texto (lendo o desenho, comprimindo, enviando X de Y MB); o CAD não
    congela. Cancelar no meio: a linha de comando diz "3D cancelado: nada foi publicado.".
33. **As análises por escopo (42.6), no Aurora:** abrir Análises:
    - em cima, "O que analisar", com Desenho inteiro marcado e as áreas com "· N mesa(s)";
      marcar só uma área (ou "Escolher área no desenho…" e clicar dentro dela) e, em Pilar
      livre, Inserir textos: só as mesas dessa área ganham texto; a frase diz o nome da área;
    - os textos já têm fundo (o módulo atrás não aparece); marcar Borda põe o quadro em todos
      os textos dessa análise; desmarcar Fundo tira a máscara;
    - Pilar livre, Pilar enterrado e Pilar total mostram duas colunas, Pilar da frente e Pilar
      de trás; pôr Maior que 1,5 na frente e 2,5 atrás, Analisar: cada pilar pinta pelo
      limite da linha dele; Quantificar diz "pilares da frente: ..." e "pilares de trás: ...";
    - marcar outra área e Tirar cores: a primeira continua pintada; Apagar textos só na
      área marcada;
    - aba Quantidades e o Excel (aba Análises): a coluna Onde diz a área.
34. **A segunda rodada (42.5/42.6):** na janela Áreas, F2 numa área, e também botão direito ›
    Renomear: o nome abre para editar na linha; Enter grava. Em Análises, as áreas em colunas
    alinhadas. Numa vista sombreada, os textos de Pilar livre e de PB/PA por cima dos módulos
    (os antigos se ajeitam ao abrir a janela). Com muitas mesas selecionadas, o botão direito
    e o pan sem demora do Clivus.
35. **O enterro (38.12):** Mesa › aba Biposte: na Estrutura, "P1: enterro mín. · nominal · máx.";
    no pórtico, "P2: enterro mín. · nominal · máx.". Mudar o mínimo não muda a altura da estrutura
    (o "livre" da vista lateral); mudar o nominal muda. Gerar uma fileira no Aurora: em Análises ›
    Pilar enterrado, cada pilar com o enterro que o terreno deu, entre o mínimo e o máximo da
    linha dele; onde nem com o mínimo a estaca alcança, ela cresce (Pilar total maior que o P1/P2).
    Atenção: com o máximo do projeto em 2,0 m (o padrão) e o mínimo da GC800 em 2,2 m, a faixa
    fica 2,2/2,2/2,2 e a estaca do catálogo não aparece; escreva o máximo de cada pilar (ou suba o
    do projeto) para ver o enterro variar.
