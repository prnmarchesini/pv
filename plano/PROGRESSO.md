# Progresso

Status: PENDENTE | EM ANDAMENTO | AGUARDANDO VALIDAÇÃO | VALIDADO | REPROVADO
Só o Renan marca VALIDADO.

| Passo | Descrição | Status | Observação |
|---|---|---|---|
| 0.1 | Solução e projetos vazios | VALIDADO | Alvo mudou de Civil 3D 2025 para 2026 |
| 0.2 | Teste de arquitetura e placar | VALIDADO | |
| 0.3 | Comando hello world | VALIDADO | Renan conferiu no Civil 3D 2026 em 22/09/2026 |
| 0.4 | Carregamento automático (bundle) | VALIDADO | Botão conferido pelo Renan; carrega após "Always Load" |
| 0.5 | Core Console fumaça | VALIDADO | |
| 0.6 | Instalador com detecção de versão | VALIDADO | Renan aprovou em 22/09/2026; máquina limpa fica pendente |
| 1.1 | Modelo de TIN puro (Geo) | VALIDADO | Validação é automática, sem CAD |
| 1.2 | Índice espacial | VALIDADO | Validação é automática, sem CAD |
| 1.3 | Listar superfícies | VALIDADO | Duas superfícies conferidas no Porto Feliz |
| 1.4 | Processar superfície | VALIDADO | Os 5 números batem com o Civil 3D no Itatiba |
| 1.5 | Identidade e carimbo | VALIDADO | Renan moveu a superfície, viu Desatualizado, reprocessou e voltou a Atual |
| 1.6 | Coordenada geográfica | VALIDADO | Renan achou o erro do ponto de referência; corrigido e conferido |
| 1.7 | Botão Obter Coordenada | VALIDADO | Z conferido no desenho, e o caso fora do terreno |
| 2.1 | Drapeamento puro (Geo) | VALIDADO | Validação é automática, sem CAD |
| 2.2 | Identidade da área | VALIDADO | GUID sobrevive a salvar e reabrir (teste de nível 2) |
| 2.3 | Comando Área | VALIDADO | Renan orbitou em 3D e a linha seguiu o terreno; pediu o rastro na tela, feito |
| 2.4 | Reindexar | VALIDADO | Renan copiou a área "teste2" para outro desenho e ela foi reconhecida lá |
| 3.1 | Módulo e biblioteca | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.2 | Comprimento da mesa | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.3 | Tabela de pilares | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.4 | Geometria local da mesa | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.5 | Fórmula da altura livre | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.6 | Perfil nomeado | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 3.7 | Modal | VALIDADO | Aprovado pelo Renan em 23/09/2026 |
| 4.1 | Modelo de configuração | VALIDADO (automático) | Sem tela; fechado em 25/09/2026 pela regra "só valido no CAD". Cinco padrões continuam meus |
| 4.2 | Linha de alinhamento | VALIDADO | Renan aprovou em 26/09/2026 ("deu certo o alinhamento"), depois de 2 reprovações em 25/09 |
| 4.3 | Regras de análise | VALIDADO (automático) | Modelo no Core, sem tela; fechado em 26/09/2026 pela regra "só valido no CAD". A tela é o 4.4, a pintura é a etapa 5 |
| 4.4 | Modal | AGUARDANDO VALIDAÇÃO | `UFV_CONFIG`: tela única, grava no desenho; nível 2 salva, reabre e compara campo a campo |
| 5.1 | Distribuição em planta | VALIDADO (automático) | `RowDistributor` no Core, `Polygons` no Geo; só modelo. **Refeito em 26/09/2026**: a linha de alinhamento é o eixo transversal, toda fileira nasce nela a cada pitch e corre a 90° (a primeira versão punha as fileiras paralelas à linha; o Renan reprovou no CAD) |
| 5.2 | Amostragem | VALIDADO (automático) | `TablePlacement` e `TerrainSampler` no Core; a ponta baixa é amostrada na aresta inteira (`Tin.TryGetMaxZAlong`); fechado em 26/09/2026 |
| 5.3 | Cotas viáveis por mesa | VALIDADO (automático) | `ViableElevations` no Core; grade de 1 cm, intervalos por varredura; fechado em 26/09/2026 |
| 5.4 | Alinhamento na fileira | VALIDADO (automático) | `RowSolver` no Core: programação dinâmica, não iterativo (divergência do plano, registrada); fechado em 26/09/2026. **Renan confirma no 5.8** o "degrau mínimo = 0 ou ≥ mínimo" e a ausência do campo de iterações |
| 5.5 | Pilares | VALIDADO (automático) | `PillarCalculator` no Core; comprimento ideal, sem arredondamento comercial (decisão do Renan no 4.1); fechado em 26/09/2026 |
| 5.6 | Resultado das análises | VALIDADO (automático) | `TableAnalysis` e `RowPipeline` no Core; fechado em 26/09/2026 |
| 5.7 | Desenho | AGUARDANDO VALIDAÇÃO | `LayoutDrawer`: pilares (bloco escalado), módulos (bloco por modelo) + face separada, contorno da mesa, alturas em camada desligada, aviso de marcada; uma transação |
| 5.8 | Uma fileira no CAD | AGUARDANDO VALIDAÇÃO | `UFV_FILEIRA`; REPROVADO em 26/09/2026 (fileira paralela à linha; alturas ilegíveis) e refeito no mesmo dia; nível 2 com 6 mesas, 42 pilares e 168 faces sobre o Itatiba |
| 5.9 | Área inteira | AGUARDANDO VALIDAÇÃO | `UFV_USINA`; nível 2 com 102 mesas em 17 fileiras sobre o Itatiba: motor 0,6 s, desenho 0,2 s |
| 6.1 | Escritor DAE puro | VALIDADO (automático) | `ColladaWriter` no Core: Collada 1.4.1, uma geometria e um nó por face, material com o nome da camada (é por ele que o PVsyst reconhece módulos); 16 testes; fechado em 26/09/2026 |
| 6.2 | Botão Exportar para PVsyst | AGUARDANDO VALIDAÇÃO | `UFV_EXPORTAR` (seleção por XData, formato, janela de arquivo, DAE com origem local); nível 2 relê o DAE: 168 faces, vértice ao milímetro |
| 6.3 | Validação no PVsyst | AGUARDANDO VALIDAÇÃO | Renan importa o DAE no PVsyst, escolhe o material `MARCHENG_UFV_FACE`, confere contagem e orientação |
| 6.4 | Estudo do formato PVC | AGUARDANDO VALIDAÇÃO | Resumo entregue em 26/09/2026 (seção "6.4: o formato PVC"); o plano manda PARAR aqui |
| 6.5 | Escritor PVC | PENDENTE | |
| 7.1 | Estado sujo | AGUARDANDO VALIDAÇÃO | `TableIdentity.Dirty` no XData (versão 2, lê a 1); `UFV_SUJAR` pinta contorno, pilares e módulos de vermelho; `UFV_ESTADO` lista; nível 2 confere cor e XData |
| 7.2 | O vigia | AGUARDANDO VALIDAÇÃO | `LayoutWatcher` por desenho: eventos do banco + fim de comando; `PendingChanges` (Core) decide; MOVE suja, ERASE do contorno registra remoção (`RemovalStore`); nível 2 com MOVE e ERASE por script |
| 7.3 | Recalcular mesa | AGUARDANDO VALIDAÇÃO | `UFV_RECALCULAR` (botão e botão direito sobre a peça): célula dos cantos do contorno (`TableCells`), reamostra, refaz pilares e pontas baixas, mesmo GUID, nasce limpa |
| 7.4 | Recalcular tudo | AGUARDANDO VALIDAÇÃO | `UFV_RECALCULAR_SUJAS` (botão "Recalcular sujas"); nível 2: suja uma, recalcula, mesmo GUID limpo, 7/28/28 peças |
| 7.5 | Cópia | AGUARDANDO VALIDAÇÃO | `CopyFixer` no fim do comando que copiou: mesa nova e peças novas (agrupadas pelo deslocamento), suja "copiada"; `UFV_RENOMEAR` para blocos com sufixo; nível 2 com COPY e -RENAME |
| 7.6 | Apagar e recontar | AGUARDANDO VALIDAÇÃO | remoção já era do vigia (7.2); `UFV_RECONTAR` (botão Recontar) conta pelo XData (`LayoutCensus`, Core, 3 testes) e consome as removidas; nível 2 com ERASE |
| 7.7 | Validação | AGUARDANDO VALIDAÇÃO | `UFV_VALIDAR` (botão Validar) e ao abrir o desenho: registros que sumiram, sujas, duplicadas (mesa e peça), órfãs, removidas não recontadas, carimbo do terreno (`LayoutValidation`, Core, 3 testes); nível 2 com quatro estragos |
| 7.8 | Auto-seleção | PENDENTE | |
| 7.9 | Grupos e painel de informações | PENDENTE | |
| 7.10 | Numeração | PENDENTE | |

(As linhas das etapas seguintes são acrescentadas ao iniciar cada etapa, copiando os passos do arquivo dela.)

## POR ONDE CONTINUAR

Atualizado em 26/09/2026, depois de fechar a etapa 5 inteira (5.7, 5.8 e
5.9 aguardando validação na tela).

**Regra nova, 25/09/2026: o Renan só valida o que se vê no Civil 3D.**
Arquivos, acervo, manifesto e valores padrão são do Claude Code. Pedido a
ele só em forma de roteiro de tela: comando, o que clicar, o que deve aparecer.

**Regra nova, 26/09/2026: seguir sem perguntar.** O Renan pediu "faça o
máximo que puder sem me pedir nada". Passo com tela fica `AGUARDANDO
VALIDAÇÃO` e o trabalho continua; passo só de modelo fecha sozinho; decisão
de regra de negócio tomada sozinha fica registrada aqui como "o Renan
confirma na tela", nunca vira pergunta bloqueante.

Esta seção existe porque o resto do arquivo é diário de decisões, e diário não
responde "e agora?". Ela fica no topo de propósito. **Quem retomar o trabalho
lê isto primeiro, e depois `CLAUDE.md`.**

### Estado

Etapas 0, 1, 2 e 3 **validadas pelo Renan**. Etapa 4: 4.1 e 4.3 fechados
automaticamente (só modelo), 4.2 validado pelo Renan em 26/09/2026, 4.4
aguardando validação na tela. Etapa 5: 5.1 a 5.6 fechados automaticamente
(só modelo), 5.7, 5.8 e 5.9 aguardando validação na tela. Etapa 6: 6.1
fechado automaticamente, 6.2 e 6.3 aguardando validação (no PVsyst), 6.4
entregue e PARADO por ordem do plano, 6.5 pendente das respostas dele.
Etapa 7: 7.1 a 7.4 aguardando validação na tela. As etapas 5, 6 e 7.1 a
7.4 foram feitas em 26/09/2026; a 5 foi reprovada duas vezes na tela no
mesmo dia e refeita (fileira pelo azimute, mesa dentro da área, mesa que
não cabe pintada, Refazer, janela de nome, terreno que não se perde).
Placar na última execução:

```
Etapa 0   45/45    OK
Etapa 1   126/126  OK
Etapa 2   24/24    OK
Etapa 3   335/335  OK
Etapa 4   242/242  OK
Etapa 5   250/250  OK
Etapa 6   19/19    OK
Etapa 7   21/21    OK
Nivel 2   16/16    OK
Acervo             OK
```

### O próximo passo é o 7.8 (auto-seleção); o 6.5 espera as respostas do 6.4

6.1 fechado, 6.2 e 6.3 aguardando validação (o DAE no PVsyst), 6.4 é o
resumo do formato PVC ("Entregar um resumo ao Renan e PARAR"). O 6.5 só
começa depois que o Renan ler o resumo e decidir as três perguntas dele. A
etapa 7 (edição) não depende da 6 e seguiu: 7.1 (estado sujo), 7.2 (o
vigia), 7.3 (recalcular mesa), 7.4 (recalcular sujas), 7.5 (cópia), 7.6
(recontar) e 7.7 (validação) feitos, aguardando validação. Fora do plano, a pedido do Renan na tela em 26/09/2026, a
etapa 5 foi refeita duas vezes no mesmo dia (fileira pelo azimute, mesa
dentro da área, mesa que não cabe pintada, Refazer, janela de nome,
terreno que não se perde): ver "Segunda reprovação do 5.8". O 7.5 é a
cópia: detectar GUID duplicado (o vigia já suja original e cópia com
"copiada"), dar identidade nova à cópia e a cada peça dela. Antes de
começar, ler `plano/etapas/etapa-7-edicao.md` e as seções 7.1 a 7.4 no
fim deste arquivo. O 7.8 é a auto-seleção: ao selecionar mesas
(arrastando ou clicando), uma caixa flutuante semitransparente com o kWp
da seleção. É tela pura (WPF sobre o editor, ou um tooltip na posição do
cursor) ligada ao evento de seleção do editor (`SelectionAdded` /
`PromptingForSelection`, ou o `Idle` conferindo `SelectImplied`); a conta
é a do `LayoutCensus` (Core) sobre as mesas cujas peças estão na seleção.
Sem tela não há como testar em nível 2 além de "a conta da seleção está
certa": separar a conta (comando `UFV_KWP_SELECAO` que imprime) da caixa.

Antes de começar, ler `plano/etapas/etapa-6-pvsyst.md`. O que a etapa 6
recebe da 5: as faces superiores dos módulos são entidades `3DFACE` na
camada `MARCHENG_UFV_FACE`, cada uma com `FaceIdentity` em XData (GUID
próprio, GUID do módulo, GUID da mesa, coluna, fileira), cantos em ordem
anti-horária vistos de cima (normal para cima, conferida no nível 2).
`LayoutXData.LoadFace` lê a identidade de volta e ainda não tem uso nem
teste: a etapa 6 é onde passa a ter.

**Decisões da etapa 5 tomadas sem o Renan, que ele confirma na tela (5.8):**

- ~~a fileira segue a DIREÇÃO da linha de alinhamento~~ **Errado, reprovado
  em 26/09/2026.** A linha de alinhamento é o eixo TRANSVERSAL: toda
  fileira nasce nela, uma a cada pitch ao longo dela, e corre a 90° para o
  lado clicado. O azimute só escolhe para que lado a mesa sobe (um dos dois
  sentidos da linha), e o comando avisa quando a linha diverge mais de 5°
  dele (a linha deve ser paralela ao azimute);
- o alinhamento na fileira é programação dinâmica, não iterativo: não há
  campo "máximo de iterações" (o plano de execução pedia; o de requisitos já
  registrava a DP como alternativa exata);
- "degrau mínimo" = ou não há degrau, ou ele tem pelo menos o mínimo;
- espaçamento entre mesas de 0,50 m e quebra de fileira em 5 m (exemplo do
  plano de requisitos) são padrões meus, editáveis na tela do 4.4;
- a mesa que não cabe fica marcada com um texto vermelho no meio (camada
  `MARCHENG_UFV_MARCADA`), podendo ficar inclinada como escada para as
  vizinhas caberem;
- a mesa não virou bloco (é o contorno + blocos de pilar e módulo em volta,
  todos com o GUID da mesa); o bloco por mesa fica para a etapa 7;
- pilar com problema (sem altura livre, fora de escala, fora do terreno) é
  desenhado com 1 m, vermelho, com o motivo no texto de altura;
- o comprimento do pilar é o ideal (P3 + enterro mínimo), sem arredondamento
  comercial, como ele decidiu no 4.1.

### O que está travado no Renan

1. **Instalar o bundle novo**: fechar o Civil 3D e rodar
   `.\tools\instalar.ps1` (o bundle está montado em `artefatos\UFV.bundle`,
   em Release, com tudo até o 5.9). Com o Civil 3D aberto o instalador recusa
   (código 5); é só fechar e rodar de novo.
2. **Testar o 4.4**: botão "Configuração" na aba UFV, ou `UFV_CONFIG`;
   trocar alguns números, "Salvar no desenho", salvar o DWG, fechar,
   reabrir, abrir a tela de novo e conferir que está tudo como deixou.
   `UFV_CONFIG_STATUS` mostra o gravado na linha de comando. Ali estão os
   padrões que são meus (pitch 6 m, enterro máximo 2 m, degrau 0 a 50 cm,
   espaçamento entre mesas 50 cm, quebra de fileira 5 m, tolerância de lombo
   0): trocar o que estiver errado.
3. **Testar o 5.8 (uma fileira)**: num desenho com terreno processado
   (botão Terreno), traçar uma área (botão Área) e um alinhamento (botão
   Alinhamento, clicando o lado das mesas). Salvar uma mesa pela janela Mesa
   se quiser outra que não a de exemplo. Botão "Fileira" (ou `UFV_FILEIRA`):
   usa a área e o alinhamento se houver um de cada, pergunta o número da
   fileira (1 é a que encosta na linha), processa e desenha. Orbitar em 3D:
   os pilares (blocos) do terreno até a mesa, os módulos (blocos) e as faces
   em cima, o contorno da mesa. Botão "Alturas" (ou `UFV_ALTURAS`) liga as
   cotas: em cada pilar, um risco vermelho na ponta baixa com "PB" (altura
   livre da ponta baixa), outro na ponta alta com "PA", e no centro "P3" (a
   altura livre do pilar), como no seu print de 26/09. P1 e P2 ficam no
   relatório e no XData. **Conferir à mão três ou quatro pilares** (é a
   validação do plano): P3 = PB + (sobra + T2) × sen(tilt); P1 = P3 + P2.
   A linha de comando diz mesa a mesa o que estourou e por quê.
4. **Testar o 5.9 (área inteira)**: botão "Usina" (ou `UFV_USINA`) na mesma
   área. Comparar com o PVcase: fileiras, mesas, módulos, kWp, pilares e a
   faixa de comprimentos de pilar (média também). O tempo do motor e do
   desenho aparecem no fim. Rodar duas vezes desenha por cima (o comando
   avisa); apagar antes de repetir.
5. **Testar o 6.2 e o 6.3 (PVsyst)**: com a usina desenhada, botão
   "Exportar" na seção PVsyst (ou `UFV_EXPORTAR`). Selecionar com uma janela
   sobre a área (só as faces de módulo entram, o resto da seleção é
   ignorado), responder DAE ao formato, escolher o arquivo. A linha de
   comando diz quantas faces foram, o material e a ORIGEM LOCAL (as
   coordenadas do arquivo são relativas a ela; está também no cabeçalho do
   arquivo). No PVsyst: Arquivo > Importar > Importar cena 3D, escolher o
   .dae; na janela de importação, marcar o material `MARCHENG_UFV_FACE` e
   "mesas fixas". Conferir: número de objetos = número de faces dito pelo
   comando; as mesas com a face para cima e inclinadas para o norte
   (azimute); e se cada face virou UM campo PV ou DOIS triângulos (se dois,
   me diga: troco `triangles` por `polylist`). O PVsyst desde a 7.x pode
   perguntar "usar a maior aresta" ou "melhor azimute" para a orientação:
   testar as duas e dizer qual ficou certa.
6. **Ler o resumo do 6.4** (seção "6.4: o formato PVC" no fim deste arquivo)
   e responder as três perguntas do fim dele. Sem isso o 6.5 não começa.
7. **Testar o 7.1 (estado sujo)**: com uma fileira desenhada, botão "Sujar"
   na seção Edição (ou `UFV_SUJAR`) e clicar num pilar, num módulo ou no
   contorno de uma mesa: a mesa inteira (contorno, pilares, módulos) fica
   vermelha; as faces superiores NÃO mudam (são o que o PVsyst recebe).
   Botão "Estado" (ou `UFV_ESTADO`): "N mesa(s), N−1 limpa(s), 1 suja(s)" e
   a linha "F1.x: SUJA (pedido do usuário)". Salvar, fechar, reabrir: a mesa
   continua suja no Estado (o estado mora no XData do contorno). Clicar
   numa entidade que não é do plugin: "Isso não é uma peça de mesa do
   plugin". Não há "limpar": quem limpa é o recálculo (7.3/7.4), que
   redesenha a mesa com as cores certas.
8. **Testar o 7.2 (o vigia)**: com uma fileira desenhada, sem usar botão
   nenhum do plugin: MOVE num pilar ou módulo (ou arrastar pelo grip) e
   confirmar: ao terminar o comando, a linha de comando diz "VIGIA F1.x
   suja (movida ou editada, comando MOVE)" e a mesa inteira fica vermelha.
   ERASE no contorno de outra mesa: "VIGIA F1.y removida (comando ERASE)";
   `UFV_ESTADO` lista "1 removida(s): F1.y removida em <data>" e conta as
   peças que sobraram como órfãs. COPY de uma mesa (janela sobre ela, base
   e destino): as duas (original e cópia, que ainda compartilham GUID até
   o 7.5) ficam vermelhas com motivo "copiada". Mudar a cor de um módulo
   pela paleta de propriedades: também suja ("movida ou editada"). Rodar
   `UFV_FILEIRA` ou `UFV_USINA` de novo: NADA fica sujo (o vigia se cala
   nos nossos comandos). Salvar, fechar, reabrir: estados e remoções ficam.
12. **Testar o 7.7 (validação)**: abra o desenho da usina: a linha de
   comando diz "AO ABRIR nada a apontar" ou lista os achados. Faça
   estragos e clique "Validar" na seção Edição: apague a polilinha da área
   (o registro fica) → "1 área registrada não está no desenho"; mova uma
   mesa → "1 mesa suja"; apague um contorno → "1 mesa só com peças" e "1
   removida não recontada"; copie uma mesa → ela entra como suja, NÃO
   como duplicada (a cópia já tem identidade própria). Cada linha diz o
   que fazer (Reindexar, Recalcular sujas, Recontar, Refazer). Reprocesse
   a superfície depois de mexer nela: a linha "terreno:" aparece quando o
   carimbo não bate.
11. **Testar o 7.6 (recontar)**: apague uma mesa inteira com o Delete
   (janela sobre ela) e outra só o contorno. Botão "Recontar" na seção
   Edição: diz mesas, módulos, kWp (com a potência do módulo do perfil
   atual), pilares com faixa e média de comprimento, e "N removida(s)
   desde a última recontagem: F1.x, F1.y. Registro limpo"; a mesa que
   ficou só com as peças aparece como "com peças órfãs". "Estado" depois
   diz 0 removidas. Recontar de novo: "nenhuma removida desde a última".
10. **Testar o 7.5 (cópia)**: COPY de uma mesa inteira (janela sobre ela)
   para outro lugar: ao terminar, "VIGIA cópia de F1.x: N peça(s) com
   identidade nova", a CÓPIA fica vermelha com motivo "copiada" e a
   original continua como estava; "Estado" conta uma suja a mais. Botão
   direito na cópia → Recalcular: ela é recalculada onde está, limpa. COPY
   com várias cópias de uma vez (opção Múltiplo): cada cópia vira uma
   mesa. Renomear: se um bloco chegou de outro desenho como
   `MARCHENG_UFV_PILAR$0$`, o botão "Renomear" o devolve ao padrão; sem
   sufixo nenhum, diz que não há o que renomear.
9. **Testar o 7.3 e o 7.4 (recalcular)**: MOVE numa mesa inteira (janela
   sobre ela) para um lugar onde o terreno é outro: fica vermelha. Botão
   direito sobre qualquer peça dela → UFV → "Recalcular esta mesa" (ou
   botão "Recalcular" na seção Edição e clicar na peça): a mesa é
   redesenhada onde está, com pilares e cotas do terreno novo, limpa (cor
   normal), mesmo letreiro. Suje duas ou três mesas (botão "Sujar") e use
   "Recalcular sujas": só elas mudam; `UFV_ESTADO` conta 0 sujas. Trocar
   a mesa na janela Mesa e recalcular: o comando recusa ("trocou de mesa?
   use o Refazer da área"). Recalcular não refaz o alinhamento com as
   vizinhas; para isso é o Refazer da área.
5. **Conferir a mesa do 3.7 contra um projeto de fabricante.** Ele aprovou a
   etapa 3 sem relatar essa conferência, que é o que o plano pede como
   validação do 3.7 — é o único jeito de saber se o motor acerta o número, e
   não só a forma.
6. ~~Testar o 4.2 no CAD~~ Feito em 26/09/2026: "deu certo o alinhamento".
7. ~~`terreno-esperado.psd1`~~ Resolvido em 25/09/2026 (congelado pelo Claude
   Code, Porto Feliz marcado como não conferido).

### Perguntas abertas, nenhuma delas bloqueante

- **a etapa 5 inteira foi feita sem o Renan ver uma mesa na tela.** As
  decisões de regra de negócio que tomei sozinho estão listadas acima. Se
  alguma estiver errada, é no 5.8 que aparece, e o conserto é no Core, com
  teste;
- **relação entre degrau, espaçamento e pitch**: a única conferência cruzada
  é quebra de fileira ≥ espaçamento entre mesas (5.1). Se existe relação real
  entre degrau e pitch, ela não está escrita;
- **o acervo do 5.8 e do 5.9**: o plano diz que o resultado, conferido pelo
  Renan, vira referência congelada. Fica para depois da conferência dele.

### Dívidas técnicas que valem lembrar

- `AlignmentStore`, `AlignmentXData` e `AlignmentScan` não têm teste de nível 1
  (são do plugin); o nível 2 os exercita desde o 4.2 (`ufv-alinhamento.scr`);
- `GarantirLayer` existe em três lugares (área, alinhamento, `LayoutLayers`) e
  `PorHandle` duplica o de `TerrenoEnvelhecido`: consolidar;
- rodar `UFV_FILEIRA` ou `UFV_USINA` duas vezes desenha por cima (o comando
  avisa); apagar e substituir é assunto da etapa 7;
- ~~os textos de altura e o aviso de marcada não carregam XData~~ Pago em
  26/09/2026: são notas (`NoteIdentity`) com o GUID da mesa;
- a análise de borda (4.3, "mesa na borda") não tem mais o que pintar
  desde que mesa não passa da área; ou some da tela de configuração, ou
  vira "mesa encostada na borda";
- `UFV_EXPORTAR` derruba a exportação inteira se uma 3DFACE nossa foi
  escalada até ficar sem área ou vertical (mensagem do Core); pular a face
  e contá-la seria melhor;
- ~~`RowSolverTests.EDeterministaERapido` oscilava perto de 2 s sob carga~~
  Resolvido em 26/09/2026: mede o menor de três execuções; o limite de 2 s
  ficou;
- o teste de nível 2 carrega a DLL de **Debug**, onde o inlining está
  desligado: ele guarda o sintoma da janela sem interface, não a regra do
  `[MethodImpl(NoInlining)]`;
- `SignedDistance` devolve **negativo à esquerda**, inverso da convenção usual.
  Documentado e testado, mas quem usar na etapa 5 vai se enganar uma vez;
- `MaiorMedida = 50` tem três donos diferentes no Core;
- o parser do carimbo (etapa 1) ainda mora no plugin, onde nenhum teste de
  nível 1 alcança. O lugar dele é o Core.

### Uma lição que se repetiu duas vezes e vale a pena não repetir uma terceira

Em dois passos seguidos (4.1 e 4.2), o achado mais grave da revisão foi **um
comentário meu afirmando uma garantia que o código não dava**:

- no 4.1, um comentário dizia que os valores padrão estavam registrados neste
  arquivo. Não estavam;
- no 4.2, dois comentários diziam que número não valia como lado. E
  `Enum.TryParse` aceitava `"1"` numa boa.

Comentário que descreve uma defesa é uma promessa. Quando ele mente, é pior que
ausência de comentário: alguém lê, acredita, e para de procurar.

## Etapa 3: os números que o Renan deu

Em 23/09/2026, para a mesa de referência:

- módulo **Risen 720 Wp**, modelo `RSM132-8-720BHDG`. As medidas vieram do
  datasheet do fabricante (2384 × 1303 × 33 mm, 37,5 kg, 132 células), não do
  Renan: ele disse "pega na net uma qualquer, só coloca o modelo";
- **28 módulos** por mesa;
- **2 cm** de espaçamento entre módulos, horizontal e vertical;
- **10 cm** de estrutura passando de cada lado da mesa;
- pilares com **cerca de 3 m** de distanciamento — "não precisa ser 3 m
  cravado, provavelmente vai dar quebrado";
- pilar de **até 2,5 m**, enterrado **no mínimo 90 cm**;
- tesoura de **3 m no máximo**, pilar na posição **2,5 m** da tesoura.

**O que ainda falta, e trava o 3.5:** a inclinação (tilt) e a altura livre na
ponta baixa do módulo. Sem os dois não há altura de pilar.

**Uma contradição a resolver com ele:** tesoura de 3 m não comporta 2V — dois
módulos em pé somam 4,79 m. O desenho de conferência foi feito em 1V, que é o
que os números descrevem, e dá mesa de 37,224 m com 12 vãos de 3,102 m. Se a
mesa for 2V mesmo, a tesoura é outra.

## A mesa não fica nivelada: ela acompanha o terreno

Decisão do Renan em 23/09/2026, depois de corrigir um erro meu de conceito.

Eu estava calculando a altura do pilar como se o terreno fosse plano — um pilar
só, um número só. Ele apontou: "e se o terreno tiver descendo? ou subindo? e se
tiver morro?". Está certo, e é o ponto inteiro do projeto.

O que vale:

- **a viga longitudinal da mesa fica na declividade do terreno.** A mesa não é
  nivelada;
- **essa declividade é uma grandeza do projeto, e tem que ser medida** mesa a
  mesa, não arbitrada;
- **haverá filtro de mínimo e de máximo** sobre ela. Uma mesa cuja declividade
  fique fora da faixa não serve, e a ferramenta tem que dizer isso.

Consequência para o modelo: a mesa não tem "uma altura livre". Ela tem um pilar
por estação, cada um com o seu comprimento, e o que os pilares precisam absorver
é a **ondulação que sobra depois de tirar a declividade média** — não o desnível
bruto do terreno.

Por que isso importa em número: com 20°, pilar a 2,5 m da tesoura, 0,30 m de
altura livre mínima, 0,90 m de enterro e 2,50 m de pilar máximo, o comprimento
de pilar só pode variar entre 2,06 m e 2,50 m. São 44 cm de folga. Se a mesa
fosse nivelada, 44 cm seria o desnível máximo de terreno que uma mesa de 37 m
aceitaria — cerca de 1,2% de caimento, menos que quase qualquer terreno real.
Acompanhando o terreno, os 44 cm passam a valer para a ondulação, que é uma
ordem de grandeza menor.

**Fechado em 23/09/2026:** estrutura **fixa**, arranjo **2V**, e filtro padrão de
declividade longitudinal em **10°** — acima disso a mesa não serve.

Com isso a mesa de referência passa a ser:

| | |
|---|---|
| Arranjo | 2V, 28 módulos em 14 colunas |
| Comprimento | 14 × 1,303 + 13 × 0,02 + 0,20 = **18,702 m** |
| Medida na inclinação | 2 × 2,384 + 0,02 = **4,788 m** |
| Vãos de ~3 m | 6 vãos de 3,117 m, 7 pilares |

## Nomenclatura do Renan (desenho de 23/09/2026)

Ele mandou um corte cotado, e é a partir dele que o código deve nomear as
coisas:

| Sigla | O que é |
|---|---|
| **M1** | altura livre da **ponta baixa do MÓDULO** até o terreno |
| **T1** | comprimento da tesoura |
| **T2** | posição do pilar ao longo da tesoura, medida da ponta baixa **dela** |
| **P1** | comprimento total do pilar |
| **P2** | parte enterrada |
| **P3** | parte acima do terreno |

Com `P1 = P2 + P3`.

**O desenho matou uma incoerência que eu insistia em levantar.** Eu vinha
apontando que "tesoura de 3 m" não comporta os 4,788 m de uma mesa 2V. No
desenho dele a tesoura é **mais curta que o módulo**, e o módulo sobra nas duas
pontas — que é exatamente o que o passo 3.4 já dizia ("Tesoura menor que o
módulo"). Uma tesoura de 3 m debaixo de 4,788 m de módulo está certa. Eu estava
comparando coisas que não se comparam, e o plano já tinha me avisado.

**O que o desenho revelou que falta.** M1 é medido na ponta baixa do MÓDULO e T2
na ponta baixa da TESOURA. Como o módulo sobra para baixo da tesoura, os dois
não partem do mesmo lugar, e essa sobra entra na conta do pilar:

```
P3 = M1 + (sobra + T2) × sen(tilt)
P1 = P3 + P2
```

A **sobra** não está cotada no desenho dele e não foi informada. Enquanto não
vier, qualquer altura de pilar que o plugin calcular está errada por um valor
constante — e plausível, que é o pior tipo de erro.

Nota sobre o plano: os números do teste do passo 3.5 ("tesoura 4 m a 10°:
0,30 / 0,647 / 0,995 m nas posições 0, 2 e 4 m") só fecham se a distância for
medida **da ponta baixa do módulo**, com sobra zero. A nomenclatura do desenho
mede da tesoura. Os dois precisam ser reconciliados antes do 3.5.

## Módulo, pilar e mesa são blocos — e a face superior é sagrada

Decisão do Renan em 23/09/2026, reforçando o plano.

- **módulo é bloco próprio;**
- **pilar é bloco próprio;**
- **mesa é bloco próprio**, contendo a estrutura e os módulos.

Isto não estava escrito no plano com estas palavras. O que está é a regra
sagrada 3 ("Pilar, módulo e mesa são objetos únicos com GUID próprio") e uma
menção de passagem na etapa 7 ("renomear os blocos para o padrão do plugin").
O passo 3.4 fala em "caixas", não em blocos. Fica registrado aqui como
especificação dele, mais forte que a regra 3 e compatível com ela.

**A face superior do módulo não é detalhe de desenho: é o produto.**

O Renan corrigiu um erro meu aqui. Eu havia proposto que a face superior fosse
uma sub-entidade aninhada na definição do bloco do módulo. Está errado, e o
plano já dizia por quê em três lugares:

- 3.4: "face superior de cada módulo como **entidade separada**";
- 6.1: o escritor DAE "recebe **lista de faces superiores de módulo**";
- 6.3: no PVsyst o Renan "**indica a layer do módulo**" na importação.

O PVsyst não recebe sólido nem bloco: recebe **plano**. Então a face superior
precisa de layer própria e identidade própria, alcançável sem explodir o bloco.
O bloco é conveniência para quem mexe no CAD; a face é o que sai pela porta.

Se a face ficar indistinguível dentro do bloco, a etapa 6 não tem o que
exportar — e isso só apareceria lá na frente, depois de toda a etapa 3 pronta.

## Uma pergunta que eu não precisava ter feito

Perguntei ao Renan se, quando o pilar estoura o limite de 2,50 m, o certo era
apertar a faixa da ponta baixa ou aceitar o pilar maior. **A regra sagrada 4 já
respondia:** "A ponta baixa manda, o pilar é consequência. O que cede é o pilar,
que pode estourar e é marcado."

Fica a lição, que vale para todo passo: reler as regras sagradas antes de
perguntar. Elas são curtas justamente para serem relidas.

## Placar na entrega da etapa 2

```
Etapa 0   45/45    OK
Etapa 1   126/126  OK
Etapa 2   24/24    OK
Nivel 2   6/6      OK
Acervo             OK
```

## Placar na entrega da etapa 0

```
Etapa 0   45/45    OK
Etapa 1   122/122  OK
Nivel 2   4/4      OK
Acervo             OK
```

## Observações

**Civil 3D 2026, não 2025.** A máquina tem Civil 3D 2026 (série R25.1, `acmgd.dll`
25.1.74.0.0). `02-arquitetura.md` e `etapa-0-fundacao.md` foram corrigidos. O
framework não mudou: 2025 e 2026 são ambos .NET 8.

**O .NET SDK não estava instalado**, só o runtime. Instalado o 8.0.425.

**SECURELOAD no teste de nível 2.** O AutoCAD só carrega código de caminho
confiável, e a pasta `bin` do build não é uma. Sem interface não há como
autorizar, então o teste baixa a guarda pelo tempo do NETLOAD e devolve depois.
Como o valor fica salvo no perfil do usuário, quem garante a devolução é o
runner, em `finally`, inclusive apagando a entrada nos perfis onde ela não
existia antes. A origem do aviso em produção é a mesma, e está logo abaixo.

**O aviso de arquivo não assinado.** Conferido abrindo o Civil 3D 2026: com o
bundle em `%APPDATA%\Autodesk\ApplicationPlugins`, toda abertura para no aviso
*Unsigned Executable File* e o plugin só carrega depois de um clique. Estar em
`ApplicationPlugins` faz o CAD **achar** o plugin, não confiar nele: a DLL não é
assinada, e o AutoCAD só dispensa a assinatura numa pasta protegida por
permissão — a pasta do usuário é gravável, e não vale.

Acrescentar a pasta ao `TRUSTEDPATHS` **não resolve**: testado, o aviso continua,
porque o AutoCAD despreza caminho gravável pelo usuário.

Duas saídas de verdade, e o instalador já traz a primeira:

1. `tools\instalar.ps1 -ParaTodaAMaquina`, que instala em
   `%PROGRAMFILES%\Autodesk\ApplicationPlugins` (pede elevação). **Ainda não
   testado**: precisa de UAC.
2. Assinar a DLL com certificado confiado pela máquina. É a resposta certa para
   distribuir a terceiros, e fica para quando houver certificado.

Sem uma das duas, o usuário aperta "Always Load" uma vez — mas isso vale para
aquela DLL, e se perde a cada nova versão.

**O acervo foi congelado em 22/09/2026** com dois desenhos do Renan: `Curvas
Itatiba.dwg` (uma superfície, 12.621 pontos) e `Porto Feliz - 2
Superficies.dwg` (duas: 342 e 7.220 pontos). O teste de nível 2 roda contra
todos os `.dwg` do acervo, então congelar mais um é só copiar o arquivo e
declarar o hash.

As linhas do `MANIFESTO.sha256` foram acrescentadas pelo Claude Code, com
autorização do Renan na conversa ("tudo o que for terminal faça vc"). A regra
de `04-testes.md` continua valendo, e `checar-acervo.ps1` avisa sempre que o
manifesto difere do git.

**Nota antiga, já resolvida:** O teste de nível 2 cai num template do
Civil 3D e diz qual usou. Quando a etapa 1 congelar um `.dwg` de referência, ele
passa a mandar — e a entrada do teste muda.

**Fora do escopo, para as etapas seguintes:**

- O instalador copia para `ApplicationPlugins` do usuário atual. Instalação para
  todos os usuários da máquina não foi feita.
- Os códigos de saída 1, 4 e 5 do instalador não têm teste automatizado: exigem
  máquina sem AutoCAD ou com o CAD aberto.
- `-ParaTodaAMaquina` ainda não foi exercitado: precisa de elevação (UAC).
- Assinar a DLL com certificado, que dispensaria a pasta protegida.
- `0 - Assets/Curvas Itatiba.dwg` é material do Renan, fora do git. Quando
  virar referência de teste, entra em `tests/acervo/` congelado por ele.
- Os testes do 1.1 usam terreno analítico (plano, quina de calombo). Terreno
  real com curvatura entra no acervo da etapa 1.
- `tests/proposto/etapa-1/terreno-esperado.psd1` espera conferência do Renan
  para ir ao acervo. Os números do Itatiba já foram conferidos na tela; os do
  Porto Feliz saíram do próprio plugin e ainda não.
- Ninguém confere a unidade do desenho (INSUNITS). Um DWG em milímetro ou em
  pé entregaria cota errada por fator constante, plausível. Vale um passo.

**Pendências técnicas da etapa 1**, anotadas por revisão de código e não
resolvidas:

- O parser do carimbo (`ProvenanceStore.Escrever`/`Ler`) é lógica pura sobre
  pares de texto, mas mora no plugin, onde nenhum teste de nível 1 alcança.
  A robustez que ele promete — versão diferente, campo faltando, duplicado,
  fora de ordem — não tem prova. O lugar dele é o Core.
- A conferência de latitude no teste de nível 2 assume UTM do hemisfério sul.
  Um desenho de outra projeção no acervo deixaria o placar vermelho sem
  defeito nenhum.
- `ProcessedAt` é gravado como hora local; quem abrir o desenho noutro fuso lê
  uma hora diferente da que o carimbador viu. O instante é preservado, o texto
  não.

**Casos que nenhum desenho do acervo exercita.** Os dois desenhos congelados
são parecidos entre si — topografia em UTM, uma ou duas superfícies, sem
talude vertical, sem XRef, e o Renan disse em 22/09/2026 que o resto do acervo
dele é do mesmo tipo. Então estes caminhos do código só têm teste analítico,
de nível 1, e nenhum teste dentro do CAD:

- triângulo descartado por não ter área em planta (faceta vertical de talude).
  Nos dois desenhos o descarte é zero;
- superfície vazia, que aparece acinzentada e não deixa escolher;
- superfície vinda de referência externa, e a mensagem que manda vincular.
  Este dá para montar no próprio teste, anexando um desenho do acervo como
  XRef a um desenho vazio — não precisa de arquivo novo;
- terreno grande de verdade (centenas de milhares de pontos). O desempenho só
  foi medido em malha sintética.

**Observações da etapa 2.**

O rastro do traçado (`RastroDoTracado`) não estava no plano: entrou porque o
Renan traçou uma área e disse que não dava para ver o que estava desenhando. O
AutoCAD sozinho só mostra o elástico do último ponto até o cursor. São gráficos
transientes, que existem só na tela e somem quando o comando termina — inclusive
por Esc ou por erro.

Os ícones da ribbon são vetor desenhado em código (`IconesDaRibbon`), e não
arquivos de imagem: não há binário para versionar nem para o bundle carregar, e
o traço não borra em tela 4K. As cores foram escolhidas para funcionar no tema
escuro e no claro da ribbon.

**O que a revisão de código da etapa 2 apontou, e o que foi feito.**

Corrigidos, todos com teste ou com a razão escrita no código:

- `Draping` anotava a posição do vértice fora do terreno **antes** de saber se
  ele seria aceito. Um ponto recusado por repetir o anterior deixava a anotação
  apontando para a vaga dele, que o próximo ocupava: o aviso acusava um vértice
  que estava sobre o terreno, e o que estava fora passava calado. Alcançável com
  dois cliques no mesmo lugar. Teste novo, conferido por mutação.
- `TriangleGrid.CandidatesAlong` prendia X e Y do segmento à caixa da malha
  **separadamente**, o que move as pontas em vez de cortar o segmento. Uma
  diagonal que entra por uma borda e sai por outra virava outra diagonal, e o
  trecho sobre o terreno saía sem vértices — a linha atravessando o morro por
  dentro, que é o defeito que esta etapa existe para impedir. Agora o recorte é
  paramétrico (Liang-Barsky). Teste novo, com malha de 3.200 triângulos: o de
  quatro triângulos cabe numa célula só e não prova nada.
- O trecho que fecha o contorno **nunca aparecia na tela**: o `using` do rastro
  ficava dentro de `Tracar`, e descartava tudo na instrução seguinte à que o
  desenhava. O rastro passou a viver por todo o comando, e agora também
  continua visível enquanto o nome é digitado.
- Três cliques no mesmo lugar criavam uma polilinha fechada **sem nenhum
  vértice**, com identidade e registro: uma área fantasma que o AUDIT reclama.
  Guarda nova, com mensagem.
- O `handle` era lido depois do `Commit`, com o objeto já fechado. Passou a ser
  lido dentro da transação.
- Registro de áreas ilegível ou de outra versão era descartado **em silêncio**,
  e a gravação seguinte o substituía por uma lista quase vazia. Agora todo
  descarte deixa rastro no log e o usuário é avisado de que ficou um
  `UFV_REINDEXAR` para rodar. O campo `QUANTIDADE`, que era gravado e nunca
  lido, passou a ser conferido — é ele que denuncia registro truncado.
- Nome de área sem limite estourava o XData (255 bytes) só na gravação, depois
  de a área inteira ter sido traçada. Conferido na hora da pergunta.
- `RastroDoTracado` vazava a `Line` se o `AddTransient` lançasse.
- `PluginDictionary.Save` assume a posse do `ResultBuffer`; agora está na
  assinatura.

Apontamento recusado, com o motivo: a revisão pediu `_viewports.Dispose()` em
`RastroDoTracado`, dizendo que `IntegerCollection` é invólucro de memória não
gerenciada. Não é — ao contrário da maioria das coleções da API do AutoCAD,
esta não herda de `DisposableWrapper` e não tem `Dispose`. Conferido no
compilador (CS1061) e anotado no código para não voltar.

**Pendências técnicas da etapa 2:**

- A ressalva das cotas no `UFV_REINDEXAR` é um aviso em texto, não uma
  verificação. O plugin não tem como saber se a topografia do desenho de
  destino é outra — ele avisa sempre. Conferir de verdade exigiria comparar o
  terreno de destino com as cotas que a área traz, e isso é trabalho de etapa
  posterior.
- O layout da ribbon (botão grande mais coluna de botões pequenos) só foi
  conferido a olho. Não há teste que o alcance: a ribbon não existe em host
  sem interface.
- A tolerância de um milímetro do `Draping` é absoluta, e só significa
  "um milímetro" se a unidade do desenho for metro. Num DWG em milímetro ela
  vira um micrômetro e o enxugue deixa de enxugar; num DWG em pé, 0,3 mm. É o
  segundo consumidor da pendência de INSUNITS anotada acima, e o mais caro.
- `AreaStore` é lógica pura sobre pares de texto morando no plugin, onde nenhum
  teste de nível 1 alcança — o mesmo problema já anotado para o carimbo. A
  conferência de `QUANTIDADE` e os descartes com aviso não têm prova
  automatizada. O lugar dele é o Core.
- Entraram neste diff três correções de vazamento que são da etapa 1
  (`GeoStore`, `ProvenanceStore`, `PluginDictionary`). São corretas, mas
  deviam ter ficado fora do escopo do passo.

**Observações da etapa 3.**

Só entra na biblioteca de módulos aquele cujo datasheet foi conferido. Medida
aproximada é pior que medida nenhuma: ela parece dado e ninguém volta a
conferir. Por isso a biblioteca começa com três módulos da mesma série Risen
(720, 730 e 740 Wp, todos 2384 × 1303 × 33 mm) e não com uma lista grande de
marcas cujas medidas eu não teria como garantir.

O JSON vai embutido na DLL. Como arquivo solto ao lado do plugin ele sumiria na
primeira instalação que copiasse só a DLL, e a lista apareceria vazia sem
explicação — que é o resultado que a etapa inteira evita.

**O que a revisão de código do 3.1 apontou, e o que foi feito.**

Corrigidos:

- `Parse("[]")` devolvia lista vazia **em silêncio** — justamente o caso que o
  método documenta como o pior resultado possível, e o mais provável de
  acontecer (alguém editando o JSON e apagando as entradas). Agora dá erro.
- `Default()` devolvia a `List` de dentro: qualquer chamador podia esvaziar a
  biblioteca para o resto da sessão do AutoCAD. Agora é `ReadOnlyCollection`.
- O cache estático virou `Lazy` com publicação protegida. `UFV.Core` é C# puro
  e também roda fora do AutoCAD, onde não vale a garantia de thread única.
- Dois comparadores diferentes no mesmo método (`Ordinal` para repetido,
  `CurrentCulture` para ordenar). O segundo fazia a ordem da lista depender do
  idioma da máquina. Unificados em `InvariantCultureIgnoreCase`.
- A potência não tinha faixa nenhuma: 0,72 (quem digitou em quilowatt) e
  500000 (quem digitou a potência da string) passavam como válidos. E as
  medidas tinham teto mas não piso — um módulo de dois milímetros passava.

**Dois testes meus não testavam nada, e a revisão pegou os dois:**

- o teste de ordenação recalculava a ordem com a mesma expressão LINQ da
  implementação, e as três entradas do JSON já estavam em ordem no arquivo:
  passaria inclusive se `Parse` não ordenasse nada. Agora a entrada está
  deliberadamente fora de ordem e a saída é comparada com uma lista escrita à
  mão;
- o teste de modelo repetido olhava o arquivo da biblioteca, e não a regra:
  apagar o `GroupBy` da implementação o deixaria verde. Agora a regra tem
  teste próprio, com `"Y"` e `" y "`.

As quatro correções foram conferidas por mutação: com as regras desligadas,
quatro testes falham.

**Pendências técnicas da etapa 3:**

- A "opção livre" do enunciado é hoje só o construtor público do record. Um
  módulo digitado à mão e inválido devolve `IsValid == false` sem dizer **qual**
  campo está errado; só `LooksSwapped` tem texto. A mensagem campo a campo fica
  para o 3.7, que é quem tem janela.
- `Find` devolve null para modelo que não existe, mas **lança** se a biblioteca
  embutida estiver quebrada. É proposital e está documentado: instalação
  corrompida tem que aparecer, não virar "não achei o modelo".
- O limite de 2000 Wp e o piso de 1 Wp são meus, não do Renan. Servem para
  pegar erro de digitação, não são regra de projeto.

**Geometria fechada em 23/09/2026, pelo segundo desenho do Renan.**

Ele acrescentou **M2** (módulos + espaçamento, ao longo da inclinação) e o
**eixo**: tesoura e módulo **alinhados pelo centro**. Com isso não falta mais
cota nenhuma:

```
sobra = (M2 - T1) / 2
P3    = M1 + (sobra + T2) x sen(tilt)
P1    = P3 + P2
```

O desenho também mostra que **o pilar não fica no eixo**: T2 é medido do começo
da tesoura, e com T1 = 3 m e T2 = 2,5 m ele cai acima do centro. O eixo serve
para ancorar o bloco da mesa; o pilar, não.

Consequência numérica com os valores de hoje (2V, M2 = 4,788, T1 = 3, T2 = 2,5,
tilt 20 graus, P2 = 0,90, P1 máximo 2,50): sobra = 0,894 m, e
**P3 = M1 + 1,161 m**. O M1 só vai até **0,44 m** antes de o pilar passar de
2,50 m, e a faixa que o Renan deu vai até 0,80 m. Pela regra sagrada 4 isso não
é erro — o pilar cede e é marcado —, mas boa parte da faixa vai gerar mesa
marcada. Avisado a ele.

**O que a revisão do 3.3 apontou, e o que foi feito.**

- `Distribute` podia devolver uma tabela que **a própria classe reprova**:
  escolhia o número de vãos só pelo alvo e nunca conferia contra o vão máximo.
  Alvo de 60 m em mesa de 60 m dava um vão de 60 m, acima do limite. Pior, uma
  razão comprimento/alvo enorme saturava o cast para `int` em silêncio, ou
  tentava alocar um bilhão de doubles.
- A tabela guardava a **lista do chamador por referência**: mexer nela depois
  transformava uma tabela válida em inválida já construída. Agora é copiada.
- `Positions` devolvia uma `List` mutável cujas alterações se perdiam no acesso
  seguinte — escrita silenciosamente ignorada, que é pior que escrita que falha.
- `ToString()` de uma tabela **inválida lançava**, porque o texto gerado pelo
  record imprime `Positions` e `TotalSpan`. Justamente a tabela que se quer ver
  num log ou num assert que falhou era a que explodia.
- A igualdade do record comparava a referência do vetor: duas tabelas idênticas
  saíam diferentes.

**Dois testes meus não mordiam, e a revisão provou mutando:**

- inverter "sobra" e "falta" na mensagem passava verde — nenhum teste olhava a
  palavra, só os números. O sinal é o que manda alongar ou encurtar a mesa;
- afrouxar a tolerância de 1 mm para 1 cm passava verde, porque o caso "não
  tolerado" do teste era 5 cm, que reprova das duas formas. O nome do teste
  prometia o que ele não fazia. Agora o caso é 5 mm.

**Etapa 3.4: a geometria local, e o primeiro verificador de regra sagrada.**

O sistema local da mesa: X ao longo do comprimento, Y ao longo da inclinação
(o M2 do desenho), Z para cima, com a **face superior dos módulos em z = 0** e
o corpo descendo a espessura. Pôr a face no zero, e não a base, faz o plano dos
módulos ser o plano z = 0 — e a regra sagrada 2 vira uma conferência de uma
linha, aqui e depois de qualquer transformação.

`UFV.Core.Invariants` passou a existir, com `RigidTable` (regra sagrada 2). As
outras três regras ainda não têm verificador: a 1 e a 4 dependem do terreno, e
a 3 depende de as peças terem GUID, que ainda não têm. Anotado abaixo.

**Dois defeitos que eu mesmo achei escrevendo:**

- o verificador estava somando os vértices de baixo do módulo. Com 33 mm de
  espessura, nenhuma mesa seria plana — ele reprovaria toda mesa perfeita. Um
  verificador que reprova sempre é desligado, e aí a regra se perde de vez;
- a sobra da tesoura entra na posição do pilar: `PillarRow = (M2 − T1)/2 + T2`,
  3,394 m na mesa do Renan. Esquecê-la erraria a altura de todo pilar da usina
  pelo mesmo valor.

**O que a revisão do 3.4 apontou, e o que foi feito.**

O achado principal: **`Transform` prometia rigidez que não garantia.** O
construtor era público e aceitava doze números quaisquer, então uma matriz de
escala era um `Transform` legítimo. O revisor rodou: com ela o módulo saía com
2,606 m de largura e o verificador da regra sagrada 2 **aprovava**, porque
transformação afim também leva plano em plano. Uma reflexão passava igual, e
virava todas as faces para baixo — usina inteira com produção zero no PVsyst e
o desenho perfeito. Agora o construtor é privado, só as fábricas constroem, e
`Transformed` exige `IsRigid` (colunas ortonormais, determinante +1).

Mais:

- o plano do verificador passava pelo primeiro vértice e tirava a direção de
  três pontos extremos. Media até três vezes mais desvio do que existia, e
  acusava vértice inocente quando o torto era o primeiro. **Pior**: o vértice
  torto costuma SER um dos extremos, e o plano se inclinava para acompanhá-lo.
  Agora é o plano dos mínimos quadrados, por covariância e Jacobi;
- `ForTesting` era `public` numa DLL de produção — o construtor privado
  exposto. Virou `internal` com `InternalsVisibleTo`;
- `1e-12` sobre o módulo de um produto vetorial (uma área) foi trocado por
  distância à reta em metro, com a tolerância geométrica de 1e-6;
- `TableFrame` não tinha arquivo de teste nenhum. Tem agora.

**Três mutantes vivos que a revisão encontrou, todos mortos:**

- trocar a sobra da esquerda pela da direita passava, porque toda mesa de teste
  usava 0,10 nos dois lados;
- a pegada do pilar podia começar na estação em vez de ser centrada nela — 7 cm
  de ferro deslocado, invisível em planta;
- a ordem dos cantos de baixo do sólido não era contrato nenhum: embaralhá-los
  passava em tudo, e quem for montar a caixa no CAD vai confiar nela.

**E um teste meu que só copiava a implementação.** O de `Place` recompunha a
mesma expressão do corpo do método. Detectava mutação, mas não validava nada —
quem trocasse a ordem e "consertasse" o teste na mesma linha não encontraria
resistência. Pior: quando o revisor inverteu tilt com azimute, esse foi o
**único teste da solução inteira** a falhar. Agora o esperado é calculado à mão
(mesa a 30°, azimute 90°, um ponto 2 m acima da ponta baixa).

**Pendências da etapa 3.4:**

- **regra sagrada 3 sem verificador.** `ModulePiece`, `PillarPiece` e
  `TableGeometry` não carregam GUID, e a regra pede um por pilar, módulo e
  mesa. Como a identidade hoje mora no XData (etapa 2) e a geometria é objeto
  puro, isso precisa ser decidido: ou a peça ganha GUID no Core, ou o
  verificador roda só do lado do plugin. Provavelmente é assunto da etapa 4;
- o comprimento do pilar não existe no 3.4 de propósito — ele nasce no 3.5,
  quando a mesa encontra o terreno.

**Etapa 3.5: a fórmula da altura livre, e o verificador da regra sagrada 1.**

O exemplo trabalhado do plano fecha na primeira: tesoura de 4 m a 10° dá
0,300 / 0,647 / 0,995 m nas posições 0, 2 e 4.

Na mesa do Renan, com a sobra da tesoura somada:

```
sobra = (4,788 - 3,000) / 2 = 0,894 m
P3    = M1 + (0,894 + 2,500) x sen 20 graus = M1 + 1,161 m
P1    = P3 + P2
```

Com M1 = 0,30 e P2 = 0,90, o pilar dá **2,361 m**. E o M1 só vai até **0,439 m**
antes de P1 passar de 2,50 m — contra os 0,80 m que a faixa dele permite. Pela
regra sagrada 4 isso não é erro (o pilar cede e é marcado), mas boa parte da
faixa vai gerar mesa marcada.

**O que a revisão do 3.5 apontou, e o que foi feito.**

Dois achados sérios:

- **a regra sagrada 1 não valia para a saída.** O revisor rodou:
  `Measure(0; 3,394; 0)` devolvia altura livre zero, comprimento 0,90 e
  `Fits = True` — mesa pousada no chão, aprovada. E com inclinação negativa a
  altura livre ficava francamente negativa, com `Fits` ainda verdadeiro. Pior:
  **não existia verificador da regra 1** em `UFV.Core.Invariants`, embora este
  seja o passo que torna os dois números calculáveis pela primeira vez. Agora
  existe `FloatingPillar`, e a inclinação tem faixa (0 a 90 graus);
- **eu tinha adiantado escopo.** `PillarLimits` (faixa da ponta baixa, enterro
  mínimo, comprimento máximo) é o conteúdo do **passo 4.1**, que o plano lista
  nominalmente; e `Measure`/`Overrun` são do **5.x**. Cravar
  `Padrao = (0,30; 0,80; 0,90; 2,50)` aqui criava um segundo dono desses
  números, que o 4.1 teria que reconciliar. Removidos. O passo entrega o que
  ele pede: a fórmula, o comprimento do pilar, e o verificador da regra.

Outros achados corrigidos:

- ponta baixa negativa passava e dava pilar de comprimento negativo sem aviso;
- `MinClearance`/`MaxClearance` eram **código morto**: os dois campos existiam,
  nenhum cálculo os usava, e a faixa da regra 4 não era conferida em lugar
  nenhum. Saíram junto com `PillarLimits`, e a conferência da faixa vira
  responsabilidade do 4.1;
- `Fits` comparava sem a tolerância de 1 mm da arquitetura, e um dos meus
  testes cravava esse comportamento — ou seja, testava a ausência da
  tolerância. Saiu junto;
- `Padrao` era nome público em português, contra o CLAUDE.md.

**A ligação que faltava.** O `3,394` estava digitado à mão em todo teste, e
nada no repositório ligava a fórmula a `TableGeometry.PillarRow`, que é quem
carrega a sobra da tesoura. Dava para apagar a sobra da geometria e a fórmula
continuaria "certa" sozinha. Agora existe um teste que monta a mesa 2V de
verdade e passa `geo.PillarRow` para a fórmula — e a mutação que zera a sobra
derruba a suíte.

Cinco mutações conferidas no passo (cosseno no lugar do seno, inclinação sem
faixa, comprimento sem o enterro, verificador cego ao embutimento, sobra
apagada): **16 testes caem**.

**Pendências da etapa 3.5:**

- a faixa da ponta baixa (0,30 a 0,80 m) não é conferida por ninguém ainda.
  Ela é a regra sagrada 4 propriamente dita, e o lugar dela é o 4.1, junto com
  os outros limites de configuração;
- falta o verificador da regra sagrada 4 em `UFV.Core.Invariants`. Ele depende
  da tolerância de invasão por lombo ("o usuário define quantos módulos por
  mesa podem estourar a ponta baixa"), que também é configuração do 4.1.

**Etapa 3.6: o perfil nomeado.**

O risco deste passo não é o JSON — é o tempo. Um perfil gravado hoje será
aberto daqui a um ano, por outra versão do plugin, num computador com o
separador decimal diferente. Se qualquer uma dessas coisas mudar um número em
silêncio, a usina sai com a mesa errada e ninguém desconfia, porque o nome é o
mesmo.

Decisões do formato, todas por causa disso:

- número com ponto decimal, invariante de cultura;
- arranjo gravado **e lido** só como texto;
- campo ausente recusado pelo nome, nunca lido como zero;
- campo desconhecido recusado, não ignorado;
- versão do formato no arquivo, e arquivo de outra versão é recusado.

A inclinação é o único número do projeto que vai para o arquivo em **graus**:
um perfil é feito para ser aberto num editor e conferido de relance, e
"0.3490658503988659" não se confere.

**O que a revisão do 3.6 apontou, e o que foi feito.**

O achado principal, e é o tipo de coisa que só aparece quando alguém procura:
**a ida e volta não era exata, e o meu teste passava por sorte.** O revisor
rodou os 901 ângulos de décimo em décimo entre 0° e 90°: **37 deles voltavam
diferentes**, por um ULP. Vinte graus — o ângulo que eu escolhi para o teste —
fecha; 37,5° saía como `37.50000000000001` no arquivo e o perfil voltava
diferente do que entrou. Num fuzz de 200 mil radianos, 25% não fechavam.

Isso quebrava duas coisas: a igualdade do perfil (qualquer "este perfil
mudou?" daria falso positivo) e a própria justificativa do formato em graus,
já que `68.99999999999999` não se confere de relance. Corrigido arredondando
na gravação, e o teste agora roda os nove ângulos que o revisor achou.

Dois outros achados graves:

- **o arranjo era gravado como texto, mas a leitura aceitava número.** Um `7`
  no arquivo não é 1V nem 2V: toda comparação com 2V dá falso e a mesa sai
  como 1V, com 37,224 m no lugar de 18,702 m. O dobro do comprimento, sem erro
  nenhum. O meu teste só olhava a gravação;
- **campo ausente virava zero**, e zero é valor legítimo para folga e para
  margem — "faltou" e "vale zero" eram indistinguíveis. Um perfil sem
  `horizontalGap` carregava "com sucesso" 26 cm mais curto. Agora todo campo
  do arquivo é anulável e a recusa nomeia o campo.

Menores, corrigidos: campo desconhecido era ignorado em silêncio; o nome era
trimado só na gravação, então voltava diferente; a mensagem de erro do
`System.Text.Json` vazava em inglês com o nome de um tipo privado interno; e o
teste de cultura rodava na cultura da máquina — passava aqui por ser pt-BR e
não provava nada. Agora ele troca a cultura de propósito.

E uma duplicação que valia corrigir: a regra "tesoura menor que o módulo"
estava escrita palavra por palavra em dois arquivos. Bastaria alguém trocar o
sinal num dos dois para o perfil passar a aceitar o que a geometria recusa.
Agora mora em `TableFrame.WhyDoesNotFit`, e os dois chamam de lá.

Renomeado `Tilt` para `TiltRadians`: ao lado de `TiltDegrees`, um campo
chamado só "Tilt" é meio caminho andado para alguém ligar nele o campo em
graus da janela do 3.7.

Cinco mutações conferidas: **15 testes caem**.

**Etapa 3.7: a janela da mesa.**

`UFV_MESA` abre um modal com todos os campos, o comprimento recalculado a cada
tecla e a planta baixa com os módulos, os pilares e as sobras das pontas. Botão
**Mesa** na ribbon, com ícone.

Junto foi a parte de arquivo do 3.6: `TableProfileStore`, que grava e lê perfil
em `%LOCALAPPDATA%\MarchEng\UFV\perfis`. Mora no Core, com a pasta vindo de
fora — assim ele é testável, e quem sabe onde guardar continua sendo o plugin.

A planta existe porque o número sozinho não denuncia erro de digitação: 28
módulos em 1V dão 37,224 m, que parece tão razoável quanto 18,702 m. O que
denuncia é a forma mudando na hora.

**Um teste de nível 2 novo, e é o que guarda a regra mais cara deste projeto.**
`ufv-mesa-sem-interface.scr` roda `UFV_OLA` e `UFV_MESA` no Core Console.
Nomear um tipo WPF num método faz o runtime resolver as assemblies de interface
ao carregá-lo, e num host sem elas isso derruba o NETLOAD inteiro — o sintoma é
o plugin sumir, não a janela falhar. Conferido por mutação: tirando a guarda de
interface, o Core Console trava e o teste falha por estouro de tempo, que é
exatamente o que aconteceria na máquina do Renan.

**O que a revisão do 3.7 apontou, e o que foi feito.**

O achado mais grave **derrubaria o Civil 3D**: `Recalcular` roda dentro de um
manipulador de evento do WPF, e exceção não tratada ali não fecha a janela —
fecha o AutoCAD, sem salvar nada. E havia caminho real: espaçamento de 5 m com
contagem alta passa na validação do perfil e faz a tabela de pilares passar dos
mil vãos, que é recusa por exceção. Bastava digitar `5` no espaçamento. Agora o
método inteiro está dentro de um try.

No `TableProfileStore`, cinco defeitos, todos medidos pelo revisor:

- **colisão por maiúscula**: o Windows não distingue caixa no nome de arquivo,
  o escape distinguia. Salvar "mesa" apagava "Mesa" em silêncio, e pedir "Mesa"
  de volta devolvia "mesa";
- **colisão por espaço nas pontas**: `Caminho` trimava e `ToJson` não, então
  " Mesa " sobrescrevia o arquivo de "Mesa";
- **o limite de 120 era conferido antes do escape**, e cada caractere
  convertido ocupa cinco: 120 barras viravam um arquivo de 600 caracteres e o
  erro ilegível do Windows;
- **pasta com barra no fim** fazia a conferência de segurança recusar todo
  nome, culpando o nome pelo erro de quem montou o caminho;
- **o arquivo provisório tinha nome fixo**: dois Civil 3D salvando o mesmo
  perfil brigavam por ele, e na pior janela um publicava o arquivo pela metade
  do outro por cima do perfil bom — justamente o que o escrever-e-trocar
  existia para impedir. Em 200 gravações concorrentes, 181 falhavam.

**A leitura de número saiu da janela para o Core**, como `NumberInput`, e ganhou
teste. Era o pedaço mais sujeito a erro silencioso do passo, e estava preso
dentro do controle de interface, sem teste nenhum. A revisão mediu: `"1.500"` no
campo de potência virava **1,5 Wp**, passava na validação, e a usina saía com a
potência dividida por mil.

Escrevendo os testes dele achei mais dois, que a revisão não tinha visto:
`NumberStyles.AllowThousands` não confere o tamanho dos grupos, então `"1.2.3"`
virava 123 e — pior — **`"28.5"` no campo de módulos virava 285**. Duzentos e
oitenta e cinco módulos é um número plausível, e o projetista digitou vinte e
oito e meio. A regra de milhar passou a ser estrita.

Outros achados corrigidos: a mensagem de erro não nomeava o campo; preencher a
janela com um perfil disparava a troca automática de módulo e **jogava fora as
medidas ajustadas à mão**; marca e modelo de módulo fora da biblioteca eram
apagados ao salvar; `Salvar` sobrescrevia perfil de mesmo nome sem perguntar;
`Exists` e `Delete` do store não tinham chamador; e `UltimoPerfil` devolvia o
primeiro em ordem alfabética.

A janela ganhou **seletor de perfil salvo**, que faltava: havia botão de salvar
e nenhuma forma de escolher qual carregar.

**Pendências da etapa 3.7:**

- o teste de nível 2 carrega a DLL de **Debug**, onde o inlining está
  desligado. Ele guarda o sintoma (a janela sem interface), não a regra
  (`NoInlining`): apagar o atributo não o deixaria vermelho. Rodar também em
  Release fecharia o buraco;
- a janela não tem teste de nível 1, por ser WPF. O que dava para extrair e
  testar foi extraído (`NumberInput`); o resto é montagem de controle;
- desenhar a mesa no CAD é da etapa 5. A janela diz isso ao fechar.

**Ajuste pedido pelo Renan depois de usar a janela (23/09/2026).**

Ele olhou a planta baixa e pediu três coisas: indicar o norte, cotar os vãos
entre pilares, e cotar o espaço livre entre o pilar e o fim da estrutura. A
terceira revelou um buraco no modelo.

**O balanço virou parâmetro.** A tabela de pilares punha um pilar cravado em
cada ponta da estrutura, sempre — "distâncias acumuladas do zero", como o plano
diz. Mas estrutura de verdade costuma ter balanço, e ele confirmou: "o pilar
pode ficar cravado em zero e pode ter distância configurada". Então:

- `PillarTable` ganhou `Cantilever`, e as posições passam a começar nele, não
  em zero. Sem isso, a cota do balanço não teria o que medir — e o desenho
  estava certo por acidente, porque o balanço era sempre zero;
- `Distribute` recebe o balanço e divide só o **miolo** entre os pilares. A
  soma balanço + vãos + balanço continua fechando com o comprimento, que é a
  única regra que a classe não abre mão;
- `TableFrame` ganhou `PillarSpanTarget` e `PillarCantilever`. O vão de 3 m
  estava cravado na janela como constante, o que já era errado;
- o perfil passou para a **versão 2** do formato. Perfil da versão 1 é recusado
  com o motivo, que é exatamente para isso que a versão existe.

**Sobre o norte.** A seta aponta para a ponta baixa, que no hemisfério sul é a
face voltada ao norte. Vale para a mesa deitada com azimute zero, que é o que a
janela mostra; o azimute de verdade entra na etapa 5.

**E uma pendência que este ajuste levanta**, anotada antes de virar defeito: a
convenção de azimute precisa ser decidida na etapa 5. Hoje `Transform.Azimuth`
gira o +Y local (a direção que sobe a inclinação, da ponta baixa para a alta) e
azimute zero o deixa apontando para o norte — ou seja, azimute zero põe a mesa
olhando para o SUL. No Brasil o normal é o contrário. Ou o azimute passa a
significar "para onde a mesa olha", ou o eixo local se inverte. Escolher errado
espelha a usina inteira, e em planta isso não aparece.

## Etapa 3 aprovada pelo Renan em 23/09/2026

Palavras dele, depois de usar a janela: "ficou bom pra um caralho, se quiser
colocar um vista lateral fica top, do resto ta tudo aprovado".

**Uma ressalva honesta sobre o que foi conferido.** O plano pede, como validação
do 3.7, que ele "monte uma mesa real que ele conhece e confira comprimento e
posição dos pilares com o projeto do fabricante". Ele aprovou sem relatar essa
conferência. A aprovação é dele e está registrada; a conferência contra projeto
de fabricante continua valendo a pena, e é o único jeito de saber se o motor
acerta o número e não só a forma.

**A vista lateral** foi acrescentada junto: o corte na direção da inclinação,
com a nomenclatura do desenho dele (M2, T1, T2) e a subida do pilar acima da
ponta baixa. Não há terreno nela, de propósito — o pilar desce até a linha da
ponta baixa e segue tracejado, porque o comprimento de verdade só existe com o
terreno, na etapa 5. Desenhar um chão qualquer seria inventar um número com
cara de calculado.

**Um teste instável, consertado na hora.** O de gravação concorrente do perfil
falhava de vez em quando no placar (em Debug, que é mais lento) e passava
sempre em Release. A causa era o reenvio da troca do arquivo, que tinha
orçamento fixo de três tentativas com 20 e 40 ms. Virou prazo de um segundo e
meio com espera crescente.

Vale registrar por que isso foi tratado como defeito e não como azar: teste que
falha às vezes é pior que teste nenhum, porque ensina a ignorar o placar — e o
placar é a única coisa que separa "está verde" de "eu acho que está verde".

# Etapa 4

## 4.1: o modelo de configuração

`SystemConfiguration` guarda os limites que valem para o projeto inteiro. Ele
quase não calcula — e é por isso que é caro de errar: o defeito não aparece
nele, aparece três etapas adiante, numa mesa marcada que não devia ser, ou
pior, numa que devia e não foi.

### Uma divergência deliberada do plano

O passo 4.1 pede "comprimentos comerciais de pilar". **Não foi feito assim**, e
a razão é o Renan, em 23/09/2026:

> "mas o tamanho é personalizavel, para vc, vc vai considerar o minimo
> enterrado, o que precisa para cima e me dar o tamanho ideal, se ficar menor
> ou maior problema meu"
>
> "sua missão é dar o tamanho do pilar, a conferencia se bate ou nao com o que
> eu preciso, eu uso filtros para selecionar"

Então o comprimento é **saída**: `IdealPillarLength = altura livre + enterro
mínimo`. Não há lista comercial travando nada. Existe um teto **opcional**,
desligado por padrão, para quem quiser marcar pilar acima de um valor — marcar,
nunca encurtar, porque encurtar mudaria a altura livre que o projetista pediu.

**Isto aposenta um número dele mesmo.** Antes, no mesmo dia, ele tinha dito
"pilar de até 2,5 m". Eu vinha tratando os 2,5 m como teto rígido, e daí saía a
conclusão de que a ponta baixa só iria até 0,439 m dos 0,80 m da faixa — metade
da faixa viraria mesa marcada. Com o comprimento sendo saída, **essa conclusão
evapora**: nada estoura, o plugin entrega o número e ele filtra. A fala mais
recente é a que vale, mas quem decidiu isso fui eu; **vale confirmar com ele**.

### Uma segunda divergência, esta de regra sagrada

A regra sagrada 4 diz, literalmente: "o usuário define **quantos módulos** por
mesa podem estourar a ponta baixa (ex.: 5 em 20)". O código guarda **fração**,
não contagem, porque "cinco módulos" significa coisas diferentes numa mesa de
28 e numa de 14.

É troca de regra sagrada por conta própria, e fica registrada como tal.
**Pergunta aberta ao Renan:** ele quer digitar contagem ou percentual?

Efeito colateral já medido e testado: a fração arredonda para baixo, então numa
mesa pequena uma fração pequena dá tolerância zero — 25% de 3 módulos é zero, e
quem ligou a tolerância não é avisado. O arredondamento para baixo é
proposital (permissão que arredonda para cima é permissão que ninguém pediu),
mas o silêncio não é confortável.

### A pendência do azimute, fechada

A etapa 3 deixou anotado que a convenção de azimute precisaria ser decidida, e
que escolher errado espelharia a usina inteira sem aparecer em planta. Decidido
e fixado em teste:

- **`FacingAzimuthRadians` é o rumo para onde a mesa OLHA**, do norte no
  sentido horário. Zero é olhando para o norte, que é o normal no Brasil;
- **`UpslopeAzimuthRadians` é o que gira o sistema local**, e vale o de mira
  mais meia volta — porque o +Y local aponta da ponta baixa para a alta, ou
  seja, para o lado oposto ao que a mesa olha.

Quem for colocar a mesa no terreno usa o segundo. Passar o primeiro direto para
a rotação põe a usina de costas, com o desenho perfeito.

**O que não foi contemplado:** o plano lista "Norte" como item separado do
azimute. Não há campo de norte do desenho, e a hipótese silenciosa é que o DWG
já esteja no norte geográfico. Se o desenho do Renan usa grid rotacionado, isso
precisa de campo — está aqui para ele dizer.

### Os padrões que são meus, não dele

| campo | valor | de quem |
|---|---|---|
| azimute de mira | 0° (norte) | Renan |
| ponta baixa | 0,30 a 0,80 m | Renan |
| enterro mínimo | 0,90 m | Renan |
| declividade longitudinal máxima | 10° | Renan |
| teto de pilar | desligado | Renan |
| **pitch entre mesas** | **6,0 m** | **meu** |
| **enterro máximo** | **2,00 m** | **meu** |
| **degrau entre mesas** | **0 a 0,50 m** | **meu** |
| **tolerância de invasão** | **0 (nenhum módulo)** | **meu** |
| **espaçamento que quebra fileira** | **0,50 m** | **meu** |

### O que a revisão do 4.1 apontou, e o que foi feito

O achado mais constrangedor: **um comentário meu mentia.** Ele dizia que os
padrões estavam listados em PROGRESSO.md, e não estavam — esta seção nasceu por
causa disso. Comentário que aponta para documentação inexistente é pior que
comentário nenhum.

Corrigidos:

- **a faixa da ponta baixa podia ser inalcançável e a configuração aprovava.**
  Um teto de 1,15 m com enterro de 0,90 e ponta baixa mínima de 0,30 fazia toda
  mesa da usina nascer marcada, e nada reclamava. A conferência agora é contra
  `enterro + ponta baixa mínima`, e não só contra o enterro;
- **graus e radianos no mesmo objeto de motor**, contra a arquitetura. Pior:
  um campo terminava em `Degrees` e o outro não dizia a unidade, e o padrão
  zero esconde o erro (0° = 0 rad). Agora tudo é radiano, com os graus em
  propriedades calculadas para tela e texto;
- **`BumpToleranceFor` com fração NaN devolvia `int.MinValue`** — menos dois
  bilhões de módulos de tolerância. Configuração quebrada agora responde zero;
- **`IdealPillarLength` duplicava `PillarSizing.Length`** com guardas mais
  fracas: aceitava altura livre de um bilhão de metros. Agora delega, e herda a
  rede de escala do resto do Core;
- **`WhyPillarIsTooLong(NaN)` devolvia null**, ou seja, "não sei medir" virava
  "está bom". Agora marca;
- **`MaxEmbedment` era campo que ninguém lia** — o mesmo defeito que a revisão
  do 3.5 mandou tirar de `PillarLimits`. Ganhou uso em `WhyEmbedmentIsWrong`;
- **enterro mínimo de um bilionésimo de metro era aceito**: pilar que flutua,
  aprovado pela configuração. Piso de um milímetro, a tolerância de regra.

Seis mutações conferidas depois das correções: **11 testes caem**.

### Pendências do 4.1

- `MinStep`/`MaxStep` e `MaxGapBeforeBreak` não têm conferência cruzada com
  nada. Se existe relação real com o pitch ou com o comprimento da mesa, ela
  não está escrita nem como comentário;
- `MaiorMedida = 50` é o terceiro dono de um limite de escala que já existe em
  `PillarSizing`. Vale centralizar antes de virar quatro.

## As três respostas do Renan, aplicadas (23/09/2026)

Perguntei as três coisas que eu tinha decidido por conta própria. Ele respondeu,
e duas das respostas **apagaram código**.

**1. O pilar é só cálculo.** Perguntei se os 2,5 m eram teto de verdade:

> "eu costumo comprar, volto a dizer, vc deve calcular o pilar ideal apenas"

O teto opcional saiu inteiro — `MaxPillarLength` e `WhyPillarIsTooLong` não
existem mais. Configuração que ninguém usa é passivo: ela vira campo na tela do
4.4, vira um segundo dono de um número que é dele, e volta como "inofensiva".
Ficou um teste guardando a ausência, para ela não voltar sem querer.

Com isso a conclusão de que "a ponta baixa só vai até 0,439 m" está
definitivamente aposentada. Ela era artefato de eu tratar os 2,5 m como
restrição.

**2. Tolerância de invasão é contagem, não fração.**

> "contagem"

Voltou a ser o que a regra sagrada 4 diz ao pé da letra. `BumpToleranceFraction`
(double) virou `BumpToleranceModules` (int), e com isso some também o efeito
colateral que eu tinha registrado: numa mesa pequena, a fração arredondada para
baixo dava tolerância zero sem avisar. A única regra que sobrou é o limite pelo
tamanho da mesa — cinco numa mesa de três valem três, senão a comparação adiante
nunca marcaria mesa nenhuma.

**Lição para os próximos passos:** eu tinha bons argumentos de engenharia para a
fração, e mesmo assim estava errado — porque o que estava em jogo não era a
engenharia, era a regra que ele escreveu. Regra sagrada não se melhora sem
perguntar.

**3. O desenho está no norte geográfico.**

> "esta com o norte"

Então não há campo de norte do desenho, e a hipótese deixa de ser silenciosa:
está escrita aqui. Se um dia entrar um DWG com grid rotacionado, é aqui que a
falta vai aparecer.

## 4.2: a linha de alinhamento

O usuário traça a linha e **clica** de que lado ficam as mesas. Clique, e não
"esquerda/direita" digitado: esquerda de quem — do traçado, da tela, do norte?
Clicar não tem ambiguidade nenhuma.

O lado só faz sentido junto com o **sentido** do traçado, e é isso que a
identidade guarda. Desenhar a mesma linha ao contrário troca os dois lados.

Comandos: `UFV_ALINHAMENTO` e `UFV_ALINHAMENTOS`. Botão na ribbon, com ícone.

### O que a revisão do 4.2 apontou, e o que foi feito

**O achado bloqueante foi um comentário meu que mentia.** Os dois arquivos que
gravam o lado diziam, por extenso, que número não valia como lado — e
`Enum.TryParse<LineSide>("1")` devolve `Left` numa boa. Ou seja, a defesa que o
comentário descrevia **não existia**: um registro com `"1"` era aceito hoje, e
no dia de renumerar a enumeração viraria `Right` em silêncio, com a usina
inteira do lado errado. Era exatamente o modo de falha que o passo diz temer.

Agora o mapa é explícito (`LineSides.Name` / `TryParseName`), no Geo, com teste
de nível 1 que recusa `"0"`, `"1"`, `"2"`, `"+1"` e `" 2 "`.

**`AlignmentXData.Load` era código morto.** A identidade era gravada na entidade
e nunca lida por caminho nenhum: um alinhamento copiado para outro desenho
ficava invisível e irrecuperável, enquanto o código prometia por escrito que "o
XData é a verdade e o reindexar o reconstrói". Verdadeiro para a área, falso
para o alinhamento. Agora existe `AlignmentScan.Varrer`, e o `UFV_REINDEXAR`
refaz os dois registros.

**A quantidade de campos por item morava em dois lugares soltos** — a constante
da leitura e o vetor da gravação —, ligados só por convenção. Desalinhados, a
tabela inteira virava "entradas ilegíveis" e o índice se perdia. Agora a
gravação confere e lança.

**Uma garantia da etapa 2 tinha um buraco que ninguém tinha visto:** a
conferência da quantidade declarada só rodava se o terceiro campo do cabeçalho
estivesse íntegro. Corrompido justamente ele, o registro voltava truncado e sem
problema relatado — a defesa contra truncamento sumia exatamente quando mais
importava.

Menores, também corrigidos: o `catch` do clique do lado culpava a linha mesmo
quando o problema era o ponto clicado, e não registrava nada no log; a mensagem
de erro dizia "não consegui criar" quando o alinhamento já estava no desenho e
só a indexação tinha falhado (o usuário redesenharia e ficaria com dois); o
`SignedDistance` devolvia infinito para coordenada absurda; e a tolerância de
"clique em cima da linha" estava com o mesmo nome e o mesmo número do
"comprimento mínimo de uma linha de referência", que são coisas diferentes.

### Duas extrações que a revisão cobrou, e estavam certas

O formato dos registros tinha sido extraído de `AreaStore` para ser
reaproveitado pelo alinhamento. A revisão apontou duas coisas:

1. **o XData devia ter sido extraído junto.** `AlignmentXData` era cópia quase
   literal de `AreaXData`, incluindo um método idêntico caractere a caractere.
   Se repetir o formato do registro central era errado, repetir o do XData
   também era. Agora existe `PluginXData`, e os dois são finos;
2. **o formato devia estar no Core, não no plugin.** As três garantias que ele
   dá — versão recusada, ilegível relatado, quantidade conferida — foram
   exigidas pela revisão da etapa 2 e **não tinham teste em nível nenhum**,
   porque moravam no plugin. A refatoração que as moveu de lugar só pôde ser
   conferida por leitura: o revisor teve que reimplementar os dois algoritmos
   fora do repositório para comparar.

   Agora o miolo é `UFV.Core.RecordTable`, que é texto virando lista e tem 17
   testes de nível 1. O plugin ficou com o adaptador de `ResultBuffer`.

E a pergunta de nome, que estava escrita palavra por palavra em dois comandos,
virou `Perguntas.Nome`.

Sete mutações conferidas depois das correções: **16 testes caem**.

### Reprovação do 4.2 em 25/09/2026, e o que ela ensinou

O Renan traçou o alinhamento em planta, orbitou, e a linha era um poste
atravessando o terreno. Causa: o comando guardava o Z bruto de cada clique.
Com OSNAP, um clique pegou a cota de uma curva de nível e o outro caiu na
elevação corrente, zero. O lado e a identidade estavam certos (usam só X e
Y), e por isso nenhum teste acusou: **não havia teste da cota**.

Ele classificou como erro grave, e é. Duas coisas mudaram:

- **regra sagrada 5** em `01-regras-sagradas.md`: tudo que o plugin desenha
  acompanha o terreno, e passo que desenha não sai para validação sem teste
  de nível 2 que leia a cota da entidade;
- **`ufv-alinhamento.scr`** e `Testar-Alinhamento` no runner: entrega cotas
  de clique absurdas (0 e 9999) e lê as pontas direto da entidade, em LISP.

A correção: `Assentar` no comando. Com terreno processado e as duas pontas
sobre ele, cada ponta ganha a cota do terreno. Sem terreno, ou com uma ponta
fora, as duas vão para Z = 0 com aviso. Nunca uma ponta de cada jeito.

### Segunda reprovação do 4.2, no mesmo dia: a linha era pobre demais

Depois da correção da cota, o Renan disse o que de fato estava errado:

> "O alinhamento não precisa ser reto, eu posso fazer vários pontos. O
> alinhamento basicamente é uma linha, não reta ou reta, que deveria ter o
> formato do terreno, quando eu desenho eu deveria ver eu fazendo o desenho,
> igual quando eu faço a área que é criada linha temporária. Aí você pede
> para clicar de que lado nasce a mesa, e sem ver a linha, é meio que difícil."

Três coisas, todas já existentes na área e que eu não reaproveitei:

- **vários pontos**: `Tracar` virou o laço do UFV_AREA (Enter termina, mínimo
  dois). A entidade é `Polyline3d`, não `Line`. O varredor e o índice não
  dependem do tipo, então o UFV_REINDEXAR segue igual;
- **acompanha o terreno**: `Draping.Along` sobre o traçado, como a área. A
  linha ganha vértices onde cruza o relevo. Exige terreno processado, como
  a área; sem terreno o comando recusa;
- **rastro**: `RastroDoTracado` vive o comando inteiro, inclusive durante o
  clique do lado e o nome. Era isso que faltava para "de que lado?" fazer
  sentido.

O lado de uma linha quebrada é o **do trecho mais próximo do clique**
(`PathSides` no Geo, 8 testes). A convenção de sentido continua a mesma:
inverter o traçado inverte todos os trechos de uma vez.

O teste de nível 2 traça três pontos em "V" com Z de clique 0 e 9999, e lê
os vértices da polilinha em LISP: tem que haver mais de três (assentou no
relevo) e todos dentro da faixa de cotas do terreno.

**Lição**: quando dois comandos fazem "traçar uma coisa em planta sobre o
terreno", o segundo copia o ritual do primeiro inteiro. Eu fiz o
alinhamento mais simples que a área porque o plano dizia "linha", e o
Renan teve que me pedir o óbvio.

### Pendências do 4.2

- `AlignmentStore`, `AlignmentXData` e `AlignmentScan` continuam sem teste de
  nível 1, por serem do plugin, e sem teste de nível 2, porque não há `.scr` de
  alinhamento. O que dava para trazer para o Core foi trazido (o formato e o
  nome do lado); o que sobrou é conversa com o AutoCAD;
- `SignedDistance` devolve **negativo à esquerda**, que é o inverso da
  convenção usual da regra da mão direita. Está documentado e testado, mas quem
  usar isso na etapa 5 para ordenar mesas vai se enganar uma vez.

## 4.3: as regras de análise

`AnalysisRules` no Core: o que se pinta, de que cor, em que camada. É modelo
puro, sem tela (a tela é o 4.4) e sem pintura (a pintura é a etapa 5, quando
houver mesa para pintar). Pela regra de 25/09/2026, fecha com os testes
automáticos, como o 4.1.

**Análise não trava nada.** A regra sagrada 4 continua mandando: a ponta baixa
manda, o pilar estoura se tiver que estourar. Isto só diz de que cor o estouro
aparece — o plano de requisitos é explícito nisso ("isso não trava o sistema:
é só análise").

### Os limites vêm da configuração, não de uma segunda cópia

O passo diz "cada análise com limites e cores configuráveis". Três das cinco
análises já têm faixa em `SystemConfiguration`: ponta baixa, enterro e
declividade. Repetir esses números nas regras de análise seria criar um
segundo dono — o defeito que a revisão do 3.5 e a do 4.1 mandaram tirar. Então
a regra de análise guarda **só ligado/desligado, camada e as duas cores**; os
limites são lidos da configuração na hora de avaliar. Mudar a faixa da ponta
baixa na configuração muda a análise junto, e há teste disso.

O único limite que é só da análise é o de **comprimento de pilar**
(`PaintPillarsLongerThan`), porque a configuração não tem teto de pilar por decisão
do Renan ("vc deve calcular o pilar ideal apenas"). O exemplo do plano é
"pintar tudo acima de 2,50 m, porque compra pilar de 2,20, 2,50 e 3,00 m".
Não contradiz a decisão dele: não há teto, só cor.

### Os padrões que são meus

| campo | valor | de quem |
|---|---|---|
| cor abaixo do limite | vermelho | Renan (25/09/2026) |
| cor acima do limite | azul | Renan (25/09/2026) |
| **cor da mesa na borda** | **magenta** | **meu** (precisa se distinguir das outras duas) |
| **pintar pilar mais comprido que** (`PaintPillarsLongerThan`) | **desligado (null)** | **meu**: o número de onde a cor começa é dele, e ele põe na tela do 4.4 |
| **todas as análises ligadas** | **sim** | **meu** |
| **camadas** | `MARCHENG_UFV_ANALISE_PONTA_BAIXA`, `_PILAR`, `_ENTERRO`, `_DECLIVIDADE`, `_BORDA` | **meu** |

### Decisões miúdas que valem registrar

- **a mesa na borda não é análise de faixa**: não tem mínimo nem máximo, só
  "caiu fora ou não", e uma cor só (`EdgeRule`). Forçá-la no mesmo molde das
  outras daria uma cor "abaixo" que nunca seria usada;
- **a declividade vale pelo módulo**: o sentido da fileira é arbitrário, e a
  mesa que desce 12° para o leste é a que sobe 12° para o oeste;
- **valor no limite é dentro**, com a tolerância geométrica de 1e-6: 0,30 m
  calculado por um seno sai 0,2999999, e isso não é "abaixo";
- **análise desligada devolve `Off`, não `Inside`**: quem lê "dentro" numa
  análise desligada acha que conferiu. Sem limite (declividade sem limite na
  configuração, pilar sem `MaxPillarLength`) também é `Off`;
- **NaN e infinito são recusados** com exceção, como no resto do Core: "não
  sei medir" não vira "está bom";
- **camada repetida entre duas análises é recusada**, sem distinguir caixa,
  porque o AutoCAD também não distingue; e o nome de camada é conferido no
  Core (`LayerName`) contra a regra do AutoCAD, para a tela do 4.4 recusar na
  hora e não dentro de uma transação, no meio da pintura;
- **cor é RGB, não índice ACI** (`RgbColor`): o índice muda de aparência com
  o fundo e a tabela do desenho, e "vermelho" precisa ser o mesmo vermelho em
  qualquer máquina. O plugin traduz com `Color.FromRgb` quando pintar.

### O que fica para a etapa 5

Quem mede é a etapa 5: a altura livre da ponta baixa **da primeira fileira**
("vale só para a primeira fileira de módulos, a que fica junto ao solo"), o
comprimento e o enterro de cada pilar, a declividade por mesa, e se a mesa cai
fora da área. `Evaluate` recebe o número pronto e devolve o veredito; aqui não
há geometria.

### O que a revisão do 4.3 apontou, e o que foi feito

Nenhum bloqueante. Três importantes, todos corrigidos:

- **um comentário meu afirmava registro que não existia** (terceira vez em
  três passos, e exatamente a lição escrita mais acima). O campo do limite de
  pilar dizia "registrado em PROGRESSO.md", e o diário ainda não tinha a
  seção. Pior: o campo se chamava `MaxPillarLength`, o mesmo nome que o 4.1
  apagou da configuração por palavra do Renan. Renomeado para
  `PaintPillarsLongerThan`, que diz "cor, não teto", e o teste duplicado que
  eu tinha copiado do 4.1 virou um que confere que **nenhum campo de dado
  das regras tem o nome de um campo da configuração**;
- **regra ausente (null) derrubava `WhyInvalid` com exceção de referência
  nula**, sem nomear campo. Importa porque o 4.4 vai ler isto de arquivo, e
  campo faltando vira null. Agora é motivo nomeado ("a regra da análise de
  enterro está ausente"), com teste para as cinco;
- **a tolerância de 1e-6 só era testada do lado "dentro"**: uma tolerância
  de um centímetro passava a suíte. Teste novo com dez micrômetros além do
  limite, dos dois lados e na declividade. Mutação conferida: cai.

Menores, também corrigidos: `TryParseHex` aceitava "#FF 000" como vermelho
(`NumberStyles.HexNumber` tolera espaço em cada par; agora só o
especificador); `AnalysisVerdict.Paints` estava sem uso e sem teste, removido;
a cor "abaixo" de pilar e declividade nunca é usada, agora documentado e
exposto em `HasMinimum` para a tela do 4.4 esconder o campo; o comentário da
tolerância chamava de "geométrica em metro" o que também vale em radiano; um
comentário de `RgbColor` descrevia tradução no plugin que ainda não existe;
`LayerName` afirmava comportamento do AutoCAD (aparar espaço) sem teste de
nível 2 possível, agora marcado como "a confirmar" com a instrução de o
plugin também validar por `SymbolUtilityServices.ValidateSymbolName` quando
criar a camada; e `MaiorNome` público virou `MaxLength`, código em inglês.
Testes acrescentados para ordem de `Layers`, motivo nomeando a análise,
`Describe` com limite ("2,5 m"), borda desligada com mesa dentro, e enum fora
da faixa.

Placar final do passo: **82 testes** em `AnalysisRulesTests`, etapa 4 com
201/201.

### Pendências do 4.3

- na etapa 5, quem criar as camadas passa o nome também por
  `SymbolUtilityServices.ValidateSymbolName`, porque a regra de nome do Core
  é a da documentação e não foi conferida no AutoCAD;
- `MaiorMedida = 50` agora tem **quatro** donos no Core (`PillarSizing`,
  `PillarTable`, `SystemConfiguration`, `AnalysisRules`). A pendência do 4.1
  dizia "centralizar antes de virar quatro". Virou;
- a ponta baixa é analisada **por módulo da primeira fileira**, e a regra
  sagrada 4 conta módulos por mesa com tolerância. A ligação entre o veredito
  por módulo e a marcação da mesa (`BumpToleranceFor`) é da etapa 5, e o
  comentário de `AnalysisKind.LowEdge` não diz isso — fica aqui.

## 4.4: a tela de configuração

`UFV_CONFIG`, botão "Configuração" na ribbon. Uma janela só: os limites do
sistema (4.1) à esquerda, as regras de análise (4.3) à direita, e "Salvar no
desenho" grava tudo no dicionário nomeado do DWG, ao lado do carimbo do
terreno. É do desenho, não do usuário: a faixa da ponta baixa é deste projeto
e viaja com o arquivo para quem o receber. O perfil de mesa (3.6) continua
sendo do usuário, em disco, porque vale para todo desenho.

### Graus e centímetros onde couber

O plano pede "graus e centímetros ligados onde couber". Ficou assim:

| campo | unidade na tela | no motor |
|---|---|---|
| azimute, declividade máxima | graus | radiano |
| ponta baixa, enterro, degrau, espaçamento que quebra | **centímetros** | metro |
| pitch, comprimento de pilar a partir do qual pintar | **metro** | metro |

Pitch e pilar em metro porque é assim que se fala deles ("pitch de 6 m",
"pilar de 2,5 m"); "600 cm" ninguém diz. A conversão acontece só nas bordas
da janela (`Preencher` e `Ler`); o registro no desenho é sempre metro e
radiano, invariante, formato redondo (`R`), para o número voltar exatamente
igual noutra máquina.

### O formato no desenho: `ProjectSettings` (Core)

Pares nome/valor em texto, com `FORMATO=1` primeiro, os 12 campos da
configuração e 4 por análise (ligada, camada, cor abaixo, cor acima; a borda
tem 3). Cor em `#RRGGBB`. Os dois opcionais (declividade máxima, pilar a
pintar) vão em branco quando não há.

Garantias, todas com teste de nível 1 (22 testes):

- **todo campo diferente do padrão vai e volta igual**, comparado por
  igualdade do record inteiro. Um campo esquecido na gravação voltaria com o
  padrão, e o teste acusa;
- versão diferente é recusada com o motivo; **campo faltando ou ilegível
  nomeia o campo** e nada é preenchido com o padrão em silêncio; campo
  desconhecido é ignorado (versão futura que só acrescente campo não torna o
  registro ilegível para esta);
- configuração que não fecha não é gravada: gravar estado inválido faria a
  próxima abertura recusar o que o próprio plugin escreveu.

### Teste de nível 2: salvar e reabrir preserva tudo

`ufv-config-gravar.scr` chama `UFV_CONFIG` (num host sem interface tem que
avisar e seguir, não derrubar o plugin), depois `UFV_CONFIG_TESTE`, que grava
uma configuração em que **todo campo difere do padrão**, e salva o DWG numa
cópia. `ufv-config-ler.scr` reabre e chama `UFV_CONFIG_STATUS`, que escreve
campo a campo. O runner compara o conjunto inteiro de `CONFIG_CAMPO` das duas
metades, e exige que o pitch gravado seja 7,5 (um plugin que perdesse o
registro e caísse no padrão, 6, passaria o resto).

Dois tropeços meus no runner, ambos do PowerShell e não do plugin: `.Count`
numa lista de um elemento (modo estrito) e `$` de regex que não casa antes do
CR nas linhas do Core Console.

### Decisões miúdas

- **a tela abre no padrão quando o registro está ilegível**, e avisa: recusar
  abrir deixaria o usuário sem como consertar. Salvar grava por cima;
- **cor fora da paleta** (gravada por outra versão, ou editada no registro)
  entra na caixa como "Outra (#RRGGBB)", para não ser trocada em silêncio
  pela primeira da lista;
- **a coluna "abaixo do mínimo" fica vazia** para pilar e declividade, que só
  têm máximo (`AnalysisRules.HasMinimum`), e a borda tem uma cor só;
- **"Restaurar padrão"** repõe os dois padrões sem gravar; só "Salvar no
  desenho" grava;
- **a configuração vai junto com o arquivo**: o comando lembra que é preciso
  salvar o desenho. Sem isso, o Renan configuraria, fecharia sem salvar e
  concluiria que a tela não guarda nada.

### O que a revisão do 4.4 apontou, e o que foi feito

Nenhum bloqueante. Quatro importantes, todos corrigidos:

- **a conversão de unidade da tela não tinha teste nenhum.** Trocar `/100`
  por `/10` na ponta baixa passava em todos os testes; só o Renan pegaria,
  se olhasse o resumo em metros enquanto digitava centímetros. A lógica do
  formulário saiu da janela e virou `ProjectSettingsForm` no Core: os campos
  em texto nas unidades da tela, `From(ProjectSettings)` para mostrar e
  `TryParse` para ler, com 17 testes (ida e volta exata, cm→m, grau→rad,
  caixa desmarcada ignora o texto ao lado, cada campo em branco nomeado). A
  janela agora só liga caixa de texto a campo. Duas mutações conferidas
  depois: `/100`→`/10` derruba 10 testes, tirar o `* Grau` do azimute
  derruba 4;
- **"todo campo difere do padrão" era mentira** na configuração de teste do
  nível 2: quatro camadas, seis cores e quatro "ligada" eram o padrão, e o
  teste não provava preservação deles. A amostra virou
  `ProjectSettings.SampleAllDifferent()` no Core, com teste que compara
  campo a campo contra o padrão e exige diferença em todos; o nível 2 usa a
  mesma amostra e exige os 32 campos, não "pelo menos 20". Quarta vez em
  quatro passos que o achado mais grave é um comentário meu prometendo mais
  do que o código faz;
- **Xrecord corrompido virava "nunca gravado"**: o dicionário devolve null
  nos dois casos, e a tela abria no padrão sem aviso — o "preencher calado"
  que o próprio comentário dizia nunca fazer. `PluginDictionary.Contains`
  separa os dois, e registro que existe e não abre é problema com aviso;
- **`Confirmar` era evento do WPF sem try.** Agora todos os três têm.

Menores, também corrigidos: `Preencher` arredondava a 4 casas, e abrir e
salvar sem tocar em nada regravava valores arredondados (o formulário usa 12
casas, com teste de que a segunda volta é idêntica à primeira); a cor
"abaixo" de pilar e declividade, que a tela não mostra, era trocada por
vermelho ao salvar (agora volta a que veio); `ResultBuffer` criado antes de
`ToFields` vazava quando ele recusava; a paleta acumulava um item "Outra" a
cada preenchimento; ramo morto em `Prefixo(EdgeTable)`; e o teste de cores
conferia valor sem chave (uma troca de prefixo entre análises passava).

Placar final do passo: Etapa 4 com 242/242, nível 2 com 9/9.

### Pendências do 4.4

- a janela em si (`JanelaDeConfiguracao`) continua sem teste, por ser WPF;
  o que restou nela é ligar caixa a campo e a paleta de cores;
- o azimute 360 é recusado ("fora de uma volta") e a tela diz "0 = norte":
  seria mais amigável normalizar. Fica para o Renan dizer se incomoda.

## 5.1: a distribuição em planta

`RowDistributor` no Core e `Polygons` no Geo. Só planta: Z entra zero e sai
zero; a cota é da amostragem (5.2). Fecha com testes automáticos.

### A fileira segue a linha, não o azimute da configuração

O passo diz "fileiras a partir da linha de alinhamento, com pitch e azimute",
e havia duas leituras: a fileira é perpendicular ao azimute de mira da
configuração e a linha só marca onde começa; ou a fileira segue a direção da
linha. Fiquei com a segunda, e o motivo está no plano de requisitos: "azimute
diferente, fileira diferente (linhas de alinhamento distintas na mesma área,
ou o terreno pedindo orientações diferentes em trechos diferentes)". Se
linhas distintas dão azimutes distintos, é a linha que orienta a fileira. É
também a única leitura em que a linha de vários pontos do 4.2 faz sentido.

O azimute da configuração continua valendo para o que ele diz: para onde a
mesa OLHA. Ele gira a mesa dentro da fileira, no desenho (5.7), e escolhe
qual dos dois lados perpendiculares à fileira é a frente. **Fica para o
Renan confirmar quando vir a fileira no CAD (5.8).**

### Como se distribui

- a fileira 1 encosta na linha, do lado clicado; a fileira k está a (k − 1)
  pitches, **medidos de borda a borda** (o pitch é a distância entre a mesma
  borda de fileiras vizinhas, que é como o projetista mede);
- cada fileira é cortada pela área em trechos; em cada trecho as mesas
  entram do início para o fim, no sentido da linha, com o espaçamento entre
  elas; a última, que passa da borda, **fica e é marcada** (`PartlyOutside`),
  como o plano manda;
- linha reta gera fileiras infinitas, cortadas só pela área. Linha quebrada
  gera uma família por trecho, limitada à faixa do trecho (as perpendiculares
  pelas pontas); onde as faixas se cruzam, a segunda família **não pisa na
  primeira**: a mesa que pisaria é pulada e contada em `SkippedForOverlap`.
  O motor não move mesa para caber, e o usuário fica sabendo que ali ficou
  vazio;
- numeração do plano de requisitos: F1, F2… no sentido do afastamento da
  linha; F1.1, F1.2… no sentido da linha. Fileira sem mesa não recebe número.

### O corte da fileira pela área: três linhas, não uma

A primeira versão cortava a faixa da fileira pela linha central e perdia a
última fileira inteira: com a área terminando em y = 50 e a faixa em
[48, 52], a linha central em 50 é tangente à borda, e tangente não entra.
Justamente a fileira que precisava ficar marcada sumia. Cortar pela borda de
cá também não serve: na fileira 1 ela está em cima da linha de alinhamento,
que quase sempre é a borda da área, e é tangente do mesmo jeito.

Agora a faixa é cortada em três linhas (1 mm para dentro de cada borda e o
meio) e os intervalos são a união. Toda fileira que toca a área por qualquer
parte ganha o seu intervalo, e a mesa que só entra por uma beirada sai
marcada.

### A borda conta como dentro

O canto de uma mesa encostada na borda da área é o caso mais comum que
existe, e pelo critério par-ímpar ele cai de qualquer lado. `Polygons.Contains`
confere a borda à parte e a considera dentro, com teste nos quatro lados e
nos vértices, horário e anti-horário.

### Um campo novo na configuração, e um padrão que mudou

A distribuição precisa do **espaçamento entre mesas** de uma fileira, e a
configuração do 4.1 não o tinha. Entrou `TableGap` (padrão 0,50 m, meu). E o
limite de quebra de fileira, que era 0,50 m (meu), passou a **5 m**, que é o
exemplo do próprio plano de requisitos ("quando o espaçamento entre duas
mesas consecutivas passa de um limite configurável (ex.: 5 m)"). Com isso
nasceu a primeira conferência cruzada da configuração, que a pendência do
4.1 cobrava: quebra menor que o espaçamento é recusada, porque toda fileira
nasceria quebrada em cada mesa. O registro do desenho ganhou o campo
`ESPACAMENTO_MESAS` (33 campos; a versão do formato continua 1 porque
nenhum desenho real tem o registro ainda), e a tela do 4.4 ganhou a caixa.

### Duas contas minhas erradas nos testes, corrigidas pelo código

Contei "9 fileiras inteiras até y = 46 + 4 = 50" com pitch 6; 46 não é
múltiplo de 6. São 8 inteiras (a oitava em [42, 46]) e a nona parcial. E
escolhi como "reta tangente por fora" uma que na verdade atravessava o
retângulo pela diagonal. Nos dois casos o código estava certo e o teste
errado; corrigi o teste, com a conta refeita no comentário.

### O que a revisão do 5.1 apontou, e o que foi feito

Um bloqueante, seis importantes, todos corrigidos. O revisor sondou o
código com um programa próprio, e os achados vieram com entradas concretas.

- **Bloqueante: mesa atravessada por um recorte da área não era marcada.**
  A conferência de "parcialmente fora" olhava só os quatro cantos; uma área
  com um dente triangular entrando 3 m pela borda, inteiro dentro de F1.1,
  dava mesa inteira. Agora são três perguntas: canto fora, vértice da área
  dentro da mesa, aresta da área atravessando aresta da mesa
  (`Polygons.SegmentsCross`). Dois testes: o dente e um canal que atravessa
  a mesa sem deixar vértice dentro;
- **a tangente do lado positivo em `Crossings` devolvia um intervalo de
  100 m.** A meia-abertura do par-ímpar agrupava "vértice em cima da reta"
  com um lado só, e com o polígono do outro lado as duas arestas vizinhas
  da colinear contavam. Agora vértice em cima da reta herda o lado do último
  vértice antes dele que não está em cima, e o cruzamento acontece só onde o
  lado muda: tangente não entra de nenhum lado, vértice atravessado conta
  uma vez, vértice encostado por fora não conta. Sete testes novos, dos dois
  lados e nos dois sentidos;
- **"toda fileira que toca a área ganha o seu intervalo" era mentira** para
  uma área fininha dentro da faixa sem tocar nenhuma das três linhas de
  corte. Agora os intervalos são a projeção da parte da área que cai na
  faixa: cruzamentos das três linhas mais **as arestas da área recortadas
  pela faixa**, em união. Testes: área fininha, topo em 49 (pega a mutação
  "só linha central"), área rasa com a linha na borda (pega a mutação "sem
  recuo");
- **mesa pulada por sobreposição sumia com só uma contagem.** Num "L" de
  90° a segunda família inteira desaparecia e o usuário via um número.
  Agora `PlanLayout.Overlapping` devolve as puladas com posição, para o 5.7
  pintá-las com cor própria; teste com contas à mão (45 colocadas, 51
  puladas, cada pulada pisa em alguma colocada);
- **o comentário dizia que o azimute da configuração "gira a mesa dentro da
  fileira"**, o que invadiria a vizinha: a célula reservada tem exatamente o
  comprimento por o fundo. Decidido e escrito: a mesa fica alinhada com a
  fileira, sempre; o azimute da configuração escolhe qual dos dois lados
  perpendiculares é a subida e avisa quando a linha diverge. `RowOrientation`
  faz isso num lugar só, com a conversão direção matemática → azimute
  topográfico testada contra `Transform.Azimuth` (7 testes);
- **medida sem piso**: mesa de 1e-15 m travava o laço (o passo somado some
  abaixo da precisão do double); mesa de 0,1 mm dava dez mil por metro.
  Piso de 10 cm (`MenorMedida`, o mesmo da linha de referência) em
  comprimento, fundo e pitch;
- **custo quadrático sempre**, inclusive com um trecho só. Agora a
  conferência de sobreposição só roda com mais de um trecho, contra as
  famílias anteriores, com prefiltro por distância entre centros.

Menores, também corrigidos: trecho de 5 mm (clique duplo com tremor) gerava
uma família com o rumo do tremor, agora o piso é `ComprimentoMinimo`;
`PlanLayout` era record com campo mutável (quebrava a igualdade), virou
classe; `SignedArea` sem consumidor, removida; doc de `Row` dizia "a partir
de 1 na linha", e é na primeira fileira com mesa; `Overlap` ganhou testes
diretos e um verificador independente por pontos, para um `Overlap` frouxo
não esconder sobreposição de todos os testes; teste do "L" aberto agora
afirma faixa, puladas e sobreposição.

Comportamentos registrados como esperados, com teste: linha traçada 2 mm
fora da borda (clique à mão, sem OSNAP) marca a fileira 1 inteira, e é
assim que se percebe que o clique não pegou a borda; coordenadas UTM
giradas 30° dão a mesma contagem do retângulo na origem.

Não feito, anotado: área auto-intersectante (gravata) é aceita em silêncio
pelo par-ímpar. A área vem de polilinha fechada pelo usuário; a conferência
cabe no comando de área, não aqui. E o formato do registro de configuração
continua na versão 1 com o campo novo obrigatório: nenhum DWG real tem o
registro (o 4.4 ainda não foi instalado), então não há o que migrar.

Placar do passo: Geo 41 testes de polígono; Core 47 de distribuição e
orientação; etapa 5 com 93/93.

## 5.2: a amostragem do terreno

Duas coisas, as duas no Core, as duas só modelo.

**`TablePlacement.Plan`**: a matriz que leva a mesa local (deitada, origem
na ponta baixa, X ao longo do comprimento, Y subindo a inclinação) para a
célula que a distribuição reservou em planta. A célula é um retângulo
alinhado com a fileira, de comprimento por fundo × cos(inclinação). O que
decide onde a origem local cai é a `RowOrientation` do 5.1: com a ponta
baixa na borda de cá, a origem é um canto da borda de cá; senão, um da
borda de lá — e, em cada caso, o canto de onde o eixo +X local parte, que é
a subida girada 90° no sentido horário (é o que `Transform.Azimuth` faz). O
teste leva os quatro cantos da mesa inclinada pela matriz e exige que caiam,
em planta, nos quatro cantos da célula, em seis combinações de lado, rumo e
azimute pedido.

Uma consequência que engana: com a mesa olhando para o norte (o padrão), a
subida é para o sul e o +X local aponta para OESTE. A mesa "cresce" da
origem para a esquerda de quem olha o norte. Eu mesmo tropecei nisso num
teste (pus a mesa na borda leste do terreno esperando que saísse por lá).

**`TerrainSampler.Sample`**: para cada mesa, a cota do terreno no pé de cada
pilar (a projeção do apoio levada pela matriz; o pilar é vertical) e sob a
ponta baixa de cada módulo da fileira de baixo. Sob a ponta baixa vale a
**cota mais alta** entre as duas pontas e o meio da aresta: a regra sagrada
4 mede da ponta baixa até o terreno, e o terreno que importa é o que chega
mais perto. Ponto fora do terreno é null e contado, nunca zero. A amostra
não depende da cota da mesa (só de X e Y), e por isso se amostra uma vez
com cota zero e a mesma amostra serve para toda cota que a otimização
experimentar — é a arquitetura ("terreno amostrado uma vez e guardado; o
motor não volta à superfície durante a otimização") virando objeto imutável.

O terreno dos testes é um plano, z = 0,02x + 0,03y + 700, onde a cota certa
em qualquer ponto se calcula à mão; há um teste de ponta a ponta, da célula
do distribuidor à amostra, e um de mesa na borda do terreno com amostras
dos dois tipos.

### O que a revisão do 5.2 apontou, e o que foi feito

Três importantes, todos corrigidos:

- **três pontos por aresta não davam "a cota mais alta sob a ponta baixa"**:
  um TIN com uma crista cruzando a aresta a um quarto do módulo passava
  entre a ponta e o meio, e a altura livre sairia otimista — a regra
  sagrada 4 violada sem marca. O comentário prometia o que só a aresta
  inteira dá. Agora o `Tin` responde `TryGetMaxZAlong`: a cota é linear
  dentro de cada triângulo, então o máximo ao longo do segmento está numa
  ponta de cada pedaço do segmento dentro de cada triângulo, e é isso que
  se avalia, com os candidatos do índice. Segmento com qualquer parte fora
  do terreno (ou sobre um buraco da triangulação) é sem resposta, não "o
  máximo do pedaço que existe". Dez testes no Geo (crista no meio, a um
  quarto, na diagonal, sobre uma aresta da malha, buraco, ponta fora, e
  concordância com amostragem fina de mil pontos em 200 segmentos
  aleatórios), e o teste da crista no Core;
- **a matriz aceitava orientação de outra fileira**: com a subida a 30° da
  perpendicular, saía uma matriz rígida e finita com a mesa girada dentro
  da célula, amostrando terreno sob a vizinha. Agora `Plan` exige subida
  perpendicular à célula e recusa célula sem quatro cantos ou curta;
- **"uma ponta dentro, outra fora" não estava pinado**: testes para o
  módulo que atravessa a borda do terreno (null) e para o buraco de 10 cm
  sob a ponta baixa (null), com os vizinhos inteiros continuando com cota.

Um detalhe do próprio `TryGetMaxZAlong`, pego pelo teste: a folga
baricêntrica que evita fresta entre triângulos vizinhos deixava o corte ir
um nada além da aresta, e o plano do triângulo extrapolava a crista em
1e-8 m. O corte de cobertura (com folga) e o de avaliação (exato) são
separados.

Menores, corrigidos: `HighestGround`/`LowestGround` duplicavam a cadeia
LINQ (agora `CotasConhecidas`, com teste do mínimo); `IsFinite` redundante
ao lado de `IsRigid`; teste renomeado para o que testa (matriz não finita,
já que uma não rígida não se monta); testes de mesa 1V e de inclinação
zero; `LowEdgeSample` ganhou `Station` (a estação local do meio do módulo),
que o 5.3 precisa para a cota da ponta baixa variar ao longo da mesa.

### Duas anotações para o 5.3

- **um giro longitudinal move o pé do pilar em planta.** A amostra vale
  para toda cota da mesa com inclinação e azimute fixos; se o 5.3 inclinar
  a mesa no sentido da fileira (início e fim em cotas diferentes), o apoio
  de cada pilar se desloca alguns centímetros em planta (5° e um pilar a
  10 m da origem: 5 a 8 cm; num terreno a 20%, 1 a 2 cm de cota). Decidir
  lá se isso vale uma reamostragem ou se fica como aproximação declarada;
- **coluna 0 não é "a esquerda de quem olha a mesa de frente"** na
  configuração padrão: fileira para leste, mesa olhando para o norte, o +X
  local corre para oeste, e a coluna 0 fica na ponta LESTE. `ModulePiece.Column`
  diz "contando da ponta esquerda", que é a esquerda do sistema local. Não
  afeta a amostragem; afeta rótulo e a tolerância "5 em 20" se alguém
  assumir que coluna 0 é a esquerda vista de frente.

## 5.3: as cotas viáveis por mesa

`ViableElevations` no Core, só modelo. Para uma mesa sobre o terreno
amostrado (5.2), o conjunto de pares (cota da ponta baixa no início, cota
no fim) em que a mesa é viável: no máximo a tolerância de lombo de módulos
fora da faixa da ponta baixa, e a declividade longitudinal dentro do limite,
se houver. A regra sagrada 4 ao pé da letra, com a exceção única dela.

### Como é calculado

A mesa é rígida (regra 2): a cota da ponta baixa varia linearmente da
estação 0 à estação L, e a altura livre de cada módulo da fileira de baixo
é a cota na estação dele menos o terreno mais alto sob a ponta baixa dele.
"Fora da faixa" conta abaixo do mínimo E acima do máximo — a regra diz
"faixa respeitada", não "mínimo respeitado".

Não se testa par a par. A cota inicial anda numa **grade de 1 cm alinhada
ao múltiplo do passo** (e não ao terreno), para mesas vizinhas de uma
fileira falarem da mesma grade quando a otimização (5.4) casar as juntas.
Para cada cota inicial, a cota final viável sai como **intervalos
contínuos**: cada módulo, com a cota inicial fixa, aceita a cota final num
intervalo (a cota dele é afim na cota final), e "no máximo k fora" é
"coberto por pelo menos n − k intervalos", que uma varredura pelas pontas
resolve. O limite de declividade é mais um intervalo, esse obrigatório.

`IsViable` e `Violations` são a definição direta, módulo a módulo, e o
teste de propriedade compara os intervalos com a definição numa malha fina
de cotas finais, em 30 terrenos aleatórios com e sem tolerância, com e sem
limite — e confere também que nenhuma cota inicial fora de `Starts` tem
cota final viável. É esse teste que dá confiança na varredura.

### Números conferidos à mão

- plano em 700, faixa 0,30 a 0,80: mesa nivelada viável de 700,30 a 700,80;
  com a cota inicial em 700,50, a final vai de onde o último módulo
  (estação 17,9 de 18,7) chega a 0,30 até onde chega a 0,80;
- limite de 0,5° sobre 18,7 m: a final fica a no máximo 0,163 m da inicial;
- rampa de 5 cm/m: a mesa paralela ao terreno é viável, a nivelada não;
  com limite de 1° não há cota nenhuma (0,50 m de faixa não absorve 0,935 m
  de desnível com 0,33 m de giro);
- calombo de 0,60 m sob a coluna 7: sem tolerância não há cota (a coluna 7
  pede ≥ 700,90 e as outras ≤ 700,80; a mesa é rígida); com tolerância de
  um módulo, a nivelada volta. Dois calombos com tolerância um: não;
- rampa com a mesa nivelada em 700,80: estouram exatamente as colunas 7 a
  13 (sete); tolerância 7 aceita, 6 não.

### O que fica declarado

- **o pilar não entra aqui.** A ponta baixa manda, o pilar é consequência
  (5.5), e estoura se tiver que estourar;
- **o giro longitudinal não reamostra o terreno para decidir** (anotação
  do 5.2): a amostra sob a ponta baixa é da mesa sem giro. ~~Para a ponta
  baixa a aproximação é exata~~ **Retificado no 5.5:** não é. A mesa é
  rígida e gira por seno, então a estação local s fica em planta em
  s·cos(giro), até 28 cm antes da posição sem giro a 10°. A decisão (5.3 e
  5.4) usa a amostra sem giro, declarada como aproximação; o relatório
  (5.6, via a orquestração) reamostra na posição final, e é ele que pinta;
- módulo sem terreno embaixo é `Problem`, nunca vazio calado.

### O que a revisão do 5.3 apontou, e o que foi feito

Sem bloqueante. Três importantes, corrigidos:

- **sem limite de declividade, a grade de cotas iniciais era heurística**
  e o comentário de `Starts` prometia "todas as cotas iniciais viáveis".
  Contraexemplo do revisor: plano, sem limite, tolerância 12 em 14, a mesa
  íngreme com a inicial em 706 e a final em 700,109 (só as colunas 12 e 13
  dentro) era viável e não estava na grade — a otimização perderia
  soluções em silêncio. Agora o giro máximo sem limite é derivado: com
  m = n − k módulos exigidos dentro, o mais íngreme possível é o desnível
  máximo da faixa sobre o menor vão que m estações consecutivas cobrem.
  Com m ≤ 1 nada limita o giro: o conjunto é **ilimitado**, `IsUnbounded`
  diz isso, e a grade cobre uma janela declarada. Teste do contraexemplo,
  teste do ilimitado, e um teste de propriedade sem limite varrendo 20 m
  de cota inicial;
- **NaN no terreno passava como cota** (comparar com NaN é sempre falso,
  e o módulo nunca contava como violação). Agora é "sem terreno", com
  `Problem`;
- **o ramo do módulo na estação zero e a ordem de empate das pontas não
  tinham teste.** Agora têm: módulo na estação zero como violação fixa
  (com e sem tolerância, e todos fixos → cota final livre no giro), e o
  empate (estações 1 e 6 numa mesa de 10 m, inicial 700,20: um único z1
  em 701,20, preservado porque abrir vem antes de fechar).

Menores, corrigidos: a união de intervalos era código morto (a ordem de
empate garante que dois intervalos nunca se tocam), removida com o motivo
escrito; `Contains` dizia "um milímetro" e usa um nanômetro; o comentário
de um teste dizia 2° e o código usava 1° (com 2° a mesa cabe; a conta
refeita usa o vão dos módulos, 17,2 m, não o da mesa); intervalo invertido
por 1e-9 não passa mais; `ModuleCount` conta a mesa inteira mesmo com
`Problem`; `fixos` contado no laço; `MaiorMedida` compartilhada com o
distribuidor; `ViableStart.Key(step)` expõe a chave inteira da grade para
o 5.4 casar juntas por inteiro, nunca por igualdade de double; `Width`,
`LongitudinalSlope` e tolerância maior que a mesa com teste.

Sobre reamostrar quando a mesa gira (pendência do 5.2): decidido que **não**
para a ponta baixa, porque ela gira em torno de si mesma e não muda de
lugar em planta — a amostra sob ela é exata para qualquer giro. Para os
pilares o giro desloca o pé em planta por centímetros; a decisão de
reamostrar fica para o 5.5, onde o pilar é calculado.

Placar do passo: 32 testes; etapa 5 com 166/166.

## 5.4: o alinhamento na fileira

`RowSolver` no Core, só modelo. Dada uma fileira (as mesas na ordem, com o
vão entre elas e as cotas viáveis de cada uma, do 5.3), escolhe a cota de
cada mesa de modo que os degraus entre vizinhas respeitem a configuração e
o estouro seja o menor possível — a fileira inteira de uma vez.

### Uma divergência deliberada do plano de execução

O passo pede "iterativo com máximo de iterações configurável, visível na
interface", e deixa "programação dinâmica registrada como alternativa a
avaliar". **Fiz programação dinâmica**, e o motivo está no próprio plano
de requisitos: "programação dinâmica resolve isso de forma exata e rápida,
sem depender de convergência: cada mesa tem seu conjunto de cotas viáveis
(discretizadas em passo fino, ex.: 1 cm), cada junta entre mesas tem o
degrau permitido como custo, e o algoritmo varre a fileira inteira achando
o ótimo global de uma vez". O 5.3 entregou exatamente esse conjunto de
cotas viáveis, em intervalos, numa grade de 1 cm; a DP sobre as chaves da
grade é o passo natural, e 200 mesas resolvem em menos de um segundo.

O que se perde: o campo "máximo de iterações" na tela. Não há iteração,
então não há o que limitar; a tela do 5.8 não terá esse campo. **Se o Renan
quiser o iterativo do jeito que descreveu, o lugar de dizer é o 5.8**,
quando vir a fileira; a DP fica como está até lá.

### O que o resultado garante

- **todo degrau está em {0} ∪ [degrau mínimo, degrau máximo]**: um degrau
  que a estrutura não consegue fazer não aparece, nunca. Esta é a minha
  leitura de "degrau mínimo": ou não há degrau, ou ele tem pelo menos o
  mínimo (um degrau de 5 cm não se constrói). Fica para o Renan confirmar;
- **toda mesa tem cota**, mesmo a que não cabe: ela fica nivelada e marcada
  com o motivo (nenhuma cota respeita a faixa; declividade passa do limite;
  n módulos fora e a tolerância é k; sem terreno). O motor não move mesa,
  não apaga mesa;
- **só quebra onde o vão passa do limite** (`MaxGapBeforeBreak`, 5 m), que
  é a definição de fileira do plano; cada trecho é resolvido em separado.

### A ordem do que se minimiza

Primeiro mesas marcadas, depois módulos fora da faixa (dentro da
tolerância), depois a soma dos degraus, e por fim a inclinação longitudinal
de cada mesa. Cada critério vale mais que todos os seguintes juntos (pesos
1e9, 1e5, 1 por metro, 1e-3 por metro). É isso que faz o terreno plano dar
degrau zero: nada estoura, e o degrau é o que sobra para minimizar.

### Os testes do plano, e os outros

Terreno plano: nenhuma marcada, degrau zero em toda junta, mesas
niveladas. Rampa de 3 cm/m: nenhuma marcada, as mesas inclinam com a rampa
e sobem 3 m ao longo de 8 mesas. Rampa que a mesa não pode acompanhar
(limite de 1°): a fileira sobe por degraus, sem marcar — "alinhamento entre
mesas primeiro". Calombo sob um módulo: com tolerância um, a mesa fica com
um módulo fora e não é marcada; com tolerância zero, só ela é marcada,
nivelada, e as quatro vizinhas seguem inteiras. Paredão de 2 m com degrau
máximo de 0,5: alguma mesa é marcada, mas nenhum degrau proibido aparece.
Vão de 20 m quebra em dois trechos. Mesa sem terreno no meio: marcada, e a
fileira segue por ela. Determinismo e velocidade (200 mesas, < 2 s). E o
teste que confere a marca contra a definição do 5.3, em terrenos aleatórios.

### Duas contas minhas erradas nos testes

Escolhi rampas de 1,2 e 1,5 cm/m para "obrigar degraus" com limite de 1°, e
tan(1°) é 1,75 cm/m: a mesa acompanhava sem degrau nenhum, e o código
estava certo. Com 3 cm/m os degraus aparecem.

### O que a revisão do 5.4 apontou, e o que foi feito

Dois bloqueantes, um importante de modelagem, todos corrigidos. O revisor
compilou o Core e rodou o solver em entradas que os testes não cobriam.

- **Bloqueante: as cotas de fim eram cortadas pela janela de início.** A
  janela de cada mesa era só as cotas iniciais viáveis mais um degrau, e a
  junta só percorria essa janela; mas o fim viável de uma mesa de 18,7 m
  fica até 3,3 m acima do início (10°). Resultado medido pelo revisor: uma
  mesa sozinha numa rampa de 6 % (3,4°, bem dentro dos 10°) saía marcada, e
  duas mesas em rampa de 6 % ou mais lançavam exceção. **É o caso de uso
  central do plugin, terreno inclinado, e eu não tinha nenhum teste com
  rampa acima de 3 %.** Agora as janelas de início e fim são separadas, a
  grade cobre as duas, e há teste com rampas de 6, 8 e 10 %, subindo e
  descendo, com uma e com seis mesas: nenhuma marcada;
- **Bloqueante: a opção marcada só existia na janela estreita**, então um
  paredão maior que dois degraus entre mesas vizinhas rompia a cadeia e
  lançava a exceção "não deveria acontecer". Agora a marcada existe em
  qualquer cota alcançável, e a cadeia de marcadas nunca se rompe. Testes
  com paredão de 2, 2,1 e 4 m: sem exceção;
- **Importante: a marcada só nivelada marcava mesa que cabia.** No paredão
  de 2 m, a terceira mesa (terreno plano, cabia perfeitamente) era marcada e
  posta 1,28 m acima do chão para servir de escada para a quarta. Fere
  "encaixa o máximo, marca o resto". Agora uma marcada pode inclinar (cinco
  giros: nivelada, meio giro e giro inteiro para cada lado, até o limite de
  declividade), e no paredão só a quarta mesa é marcada, inclinada como
  escada, com as outras cinco inteiras. O teste exige exatamente isso, e não
  mais "até três marcadas";
- **os pesos dos critérios tinham teto**: marca 1e9 contra estouro 1e5 só
  valia até 10 mil módulos fora somados. Agora o custo da marca é escalado
  pelo total de módulos do trecho;
- **desempenho**: a folga da grade crescia com o número de mesas e o laço
  varria a grade inteira por mesa (25 s para 200 mesas depois da primeira
  correção). Folga de um giro e um degrau (uma marcada longe de qualquer
  cota viável nunca ajuda), e o laço varre só o intervalo com custo finito:
  200 mesas em menos de dois segundos;
- **testes que faltavam**: força bruta contra a DP em duas mesas com as
  mesmas opções (pega janela, ordem de critérios e carimbo de uma vez);
  estouro tolerado antes de degrau (rampa de 3 % com limite de 1° e
  tolerância 2 sobe por degraus sem gastar a tolerância); degrau mínimo
  determinístico (salto de 0,60: sem mínimo o ótimo usa um degrau de 6 cm,
  com mínimo de 0,20 não há degrau em (0, 0,20)); `EndRanges(z0, k)` e
  `HighestGroundOrNull` do 5.3.

Os comentários que prometiam "ótimo global" e "toda mesa tem cota" foram
reescritos para dizer o que é garantido: ótimo dentro do espaço de estados
(grade de 1 cm, alcance limitado, cinco giros para marcada), todo degrau
permitido, toda mesa com cota, a que não cabe marcada.

Registrado como limitação conhecida: `IsUnbounded` do 5.3 é ignorado; a DP
só explora a janela declarada.

Placar do passo: 31 testes de solver (65 com os do 5.3); etapa 5 com 223/223 na árvore, já contando os testes do 5.5 e do 5.6, começados antes deste commit.

## 5.5: os pilares

`PillarCalculator` no Core, só modelo. Para uma mesa resolvida (5.4), por
pilar: onde fura o chão (a posição real, em planta), a cota do terreno ali,
a cota de topo, a altura livre, o enterro e o comprimento.

### A divergência do plano, já decidida no 4.1

O passo pede "comprimento arredondado para o comercial". **Não há
arredondamento nem lista comercial**: o Renan disse em 23/09/2026 que o
plugin "deve calcular o pilar ideal apenas", e o 4.1 tirou o teto e a lista
por isso. O comprimento é a altura livre mais o enterro mínimo
(`PillarSizing.Length`), e o enterro é sempre o mínimo — `MaxEmbedment` só
morde com comprimento imposto de fora, que não acontece aqui. Teste guarda
a ausência de arredondamento.

### O giro longitudinal na matriz

A fileira (5.4) escolhe início e fim da ponta baixa em cotas diferentes: a
mesa inclina no sentido da fileira. Isso entrou na matriz como uma rotação
a mais (`Transform.LongitudinalTilt`, em torno do eixo Y local), aplicada
DEPOIS da inclinação transversal e ANTES do azimute (`Transform.PlaceSolved`).
A ordem importa: assim a ponta baixa sobe ao longo do comprimento sem sair
da direção da fileira em planta, e é a ponta alta que se desloca um pouco
ao longo da fileira (fundo × sen(tilt) × sen(giro): 8 cm para 20° e 3°).
Na ordem inversa a ponta baixa entortaria em planta e sairia da célula.

O giro sai do desnível sobre o comprimento LOCAL, por seno e não por
tangente: `giro = asin((z1 − z0)/L)`. Assim a cota da ponta baixa na
estação local s é exatamente z0 + (z1 − z0)·s/L, que é o que o 5.3 supôs. O
preço é que o comprimento em planta encurta para L·cos(giro) (0,3 m a 10°
numa mesa de 18,7), o que é a mesa ser rígida e não uma sanfona. Declarado.

### O pé do pilar é reamostrado na posição real

O giro desloca o pé de cada pilar em planta por centímetros (a pendência do
5.2). Aqui a mesa é colocada com a matriz de verdade e o terreno é
consultado onde o pé realmente cai. Teste: num plano inclinado, a cota do
terreno do pilar é a do plano no X e Y reportados, e o último pilar anda
mais de 5 cm com um giro de 3 m em 18,7.

### O que estoura é marcado, nunca escondido

Pilar sem altura livre (a mesa desce até o chão naquele pé, ou abaixo) é a
regra sagrada 1 violada: `FloatingPillar.Check` diz o motivo, o pilar fica
sem comprimento e com o problema escrito, e a mesa não é movida por causa
dele — a ponta baixa manda, o pilar é consequência. Pé fora do terreno é
problema distinto, também marcado. Teste de propriedade: em cotas e
terrenos aleatórios, todo pilar sem problema passa no verificador da regra
1, e todo pilar com altura livre abaixo da tolerância tem problema.

## 5.6: o resultado das análises

`TableAnalysis.Evaluate` no Core, só modelo. Para uma mesa resolvida, com
os pilares calculados: cada módulo da fileira de baixo carrega a altura
livre da ponta baixa (cota na estação dele menos o terreno mais alto sob a
aresta) e o veredito da análise de ponta baixa; cada pilar carrega o
`PillarResult` do 5.5 e os vereditos de comprimento e de enterro; a mesa
carrega a declividade longitudinal com o veredito, o veredito de borda (da
célula do 5.1) e a marca da fileira (5.4) com o motivo. Nada aqui decide
geometria: só se pergunta às regras do 4.3 o que pintar e se guarda a
resposta ao lado do valor, que é o que o desenho (5.7) lê.

Peça sem o que medir sai **Off, nunca Inside**: módulo sem terreno, pilar
sem comprimento. "Dentro" numa peça sem medida seria dizer que conferiu.

Testes: dentro da faixa nada pintado; abaixo do mínimo vermelho na camada
da análise; mesa girada com vermelho num lado e azul no outro, com o valor
de cada módulo sendo o da estação dele; comprimento de pilar Off sem
limite e azul com limite de 1,50; pilar sem comprimento Off com o problema
junto; declividade pintada só acima do limite, com os graus no texto; borda
com a cor da borda; análise desligada Off mesmo com valor fora; marca da
fileira passando com o motivo.

### O que a revisão do 5.5 apontou, e o que foi feito

Nenhum bloqueante; três importantes, corrigidos, e um deles mudou o 5.3 e o
5.4 também.

- **altura livre acima de 20 m virava exceção**, não marca: um vértice
  espúrio da superfície em z = 0 sob um pilar derrubava a mesa, a fileira e
  a área. "Estouro marcado, nunca escondido" vale também para o absurdo:
  agora é problema por pilar ("fora de escala, o terreno sob este pé não é
  confiável"), sem comprimento. O mesmo para desnível maior que o
  comprimento (só possível sem limite de declividade): a mesa sai com todo
  pilar marcado e a matriz nivelada, sem exceção;
- **seno de um lado, tangente do outro.** O giro da matriz é asen(Δz/L),
  mas o limite de declividade era conferido com tangente no 5.3 e no 5.4:
  10° configurados viravam 10,15° de mesa, reportados como 10,0°. Agora é
  seno em todo lugar (`ViableElevations`, `RowSolver`, `TableAnalysis`, e
  `LongitudinalSlope` é asen). E a nota do 5.3 dizia que a amostra da ponta
  baixa era exata para qualquer giro; **era falsa**: a estação local s fica
  em planta em s·cos(giro), até 28 cm antes da posição sem giro a 10°. A
  nota foi retificada; a decisão (5.3/5.4) continua usando a amostra sem
  giro, declarada como aproximação, e o relatório (5.6) reamostra a ponta
  baixa na posição final — é ele que pinta;
- **a ponta alta avança sobre a vizinha.** O deslocamento da ponta alta ao
  longo da fileira é fundo × sen(tilt) × sen(giro): 0,30 m com os padrões,
  e não "centímetros" como o comentário dizia. Com espaçamento zero entre
  mesas e giro forte, duas mesas se cruzam pela borda alta em 3D. A
  orquestração agora confere junta a junta o avanço contra o vão e avisa
  (`ProcessedRow.Warnings`), sem marcar; com o espaçamento padrão de 0,50 m
  o aviso não aparece. Comentário corrigido.

Menores, corrigidos: `LongestPillar` com teste de comprimentos distintos;
enterro testado com mínimo fora do padrão (um literal 0,90 passava);
célula de outro comprimento que a geometria recusada; cota inicial não
finita conferida antes do desnível; desnível igual ao comprimento (mesa
em pé) recusado com folga; `AllSound` falso para mesa sem pilar;
`Embedment` de pilar com problema declarado como "o mínimo que ele
teria", não medida.

### O que a revisão do 5.6 apontou, e o que foi feito

Nenhum bloqueante. O que mudou:

- **o veredito de enterro nunca pinta por construção** (o comprimento é o
  ideal, o enterro é o mínimo, e a configuração válida garante mínimo ≤
  máximo), e a doc apresentava a análise como viva. Declarado, com teste
  em cotas aleatórias (Inside ou Off, nunca cor). A análise fica para o dia
  em que houver comprimento imposto de fora; a camada `_ENTERRO` vai
  existir vazia até lá;
- **módulo sem terreno sai Off** estava escrito em três lugares e sem
  teste. Agora tem, com o terreno recortado no meio da mesa;
- **um pilar é uma entidade com uma cor**: `PillarReport.PaintVerdict` é o
  veredito único de pintura (comprimento vence; enterro só se o
  comprimento não pintou), e `Painted` conta uma peça uma vez;
- testes que faltavam: `PaintedByKind` da declividade; `ModulesOutsideBand`
  contando os dois lados; tudo pintado ao mesmo tempo com contagem exata;
  estação e coluna vindas da amostra; configuração quebrada chegando
  direto ao `Evaluate`; comprimento de pilar conferido no valor (2,56 m), não
  só "> 1,50"; amostra de outra mesa (estação além do comprimento) recusada.

**Como a mesa marcada aparece na tela é decisão do 5.7.** No plano, a "cor
própria" é da mesa na borda; a marcada "é estudada à mão", sem cor nem
camada. O relatório traz `Marked` e o motivo; rótulo, hachura ou cor é o
desenho que escolhe, e o Renan vê no 5.8.

Observação para o 5.9: `AnalysisRules.Evaluate` reconfere `WhyInvalid`
(cinco nomes de camada e um agrupamento) a cada chamada, umas vinte vezes
por mesa. É desenho do 4.3; vai aparecer na medição de tempo.

### A orquestração da fileira (`RowPipeline`)

Entrou junto com o 5.6, porque é o que o desenho (5.7) e o comando de uma
fileira (5.8) consomem: célula → amostra → cotas viáveis → alinhamento →
pilares → reamostragem final → análises, na ordem, sem decidir nada por
conta própria. Um erro que só ela podia pegar, e pegou: na configuração
padrão (fileira para o leste, mesa olhando para o norte) o comprimento
local corre para OESTE, então a junta entre mesas vizinhas é entre o
início local de uma e o fim local da outra — e o solver encadeia fim →
início. Mesa sim, mesa não saía marcada numa rampa. `RowOrientation` ganhou
`LengthRunsWithRow`, e a orquestração entrega as mesas ao solver na ordem
das estações locais (invertida quando o comprimento corre contra a
fileira), devolvendo na ordem da fileira. Teste de ponta a ponta com rampa
longitudinal: nenhuma marcada, e as juntas em planta fecham dentro de um
degrau.

## 5.7 e 5.8: o desenho e a primeira fileira no CAD

Feitos juntos, porque um sem o outro não se vê: `LayoutDrawer` desenha uma
fileira processada e `UFV_FILEIRA` é o comando que a processa e chama o
desenho. Botão "Fileira" na aba UFV, seção Processar, com o botão pequeno
"Alturas" ao lado.

### O que é desenhado, e como

Tudo numa transação só, na ordem do plano: pilares, depois módulos, depois
o resto.

- **pilar**: um bloco só, `MARCHENG_UFV_PILAR`, caixa unitária com o topo na
  origem; cada instância é escalada para a largura e a profundidade da
  seção e o comprimento do pilar, inserida no topo (onde encosta na mesa) e
  girada com a fileira. O comprimento varia pilar a pilar, e um bloco por
  comprimento seria um bloco por pilar. Pilar com problema (regra sagrada 1,
  fora de escala, fora do terreno) é desenhado com 1 m, vermelho, na camada
  de marcadas, com o motivo no texto de altura;
- **módulo**: um bloco por modelo (`MARCHENG_UFV_MODULO_<modelo>`), caixa de
  largura × altura × espessura com a face superior em z = 0 e o canto da
  ponta baixa esquerda na origem, como a geometria local; cada instância
  leva a matriz da mesa. **A face superior é entidade separada** (`3DFACE`),
  na camada `MARCHENG_UFV_FACE`, com a mesma identidade do módulo — é a
  decisão do Renan sobre o PVsyst: a camada de face tem só faces, e a face
  nunca é pintada por análise;
- **mesa**: o contorno do plano dos módulos (`Polyline3d` fechada) na camada
  de mesa, carregando a identidade da mesa (GUID, letreiro, cotas,
  inclinação, marca e motivo). **Divergência declarada:** a mesa não virou
  bloco neste passo. O Renan pediu "mesa é bloco próprio, contendo a
  estrutura e os módulos"; o bloco por mesa exige decidir o que acontece ao
  copiar, mover e explodir, que é a etapa 7, e foi deixado para lá. Hoje a
  mesa é o contorno mais os blocos de módulo e pilar em volta, todos com o
  GUID da mesa no XData;
- **cores e camadas das análises**: peça pintada vai para a camada da
  análise com a cor do veredito (5.6); o resto fica nas camadas fixas
  (`_MESA`, `_PILAR`, `_MODULO`, `_FACE`), cor por camada. As cinco camadas
  de análise são criadas mesmo quando nada as pinta, para o usuário ligar e
  desligar;
- **"Mostrar alturas"**: um texto por pilar (P1, e entre parênteses P3 + P2,
  a nomenclatura do desenho do Renan) na camada `MARCHENG_UFV_ALTURAS`, que
  **nasce desligada**. `UFV_ALTURAS` (botão "Alturas") liga e desliga;
- **mesa marcada**: um texto no meio dela, na camada `MARCHENG_UFV_MARCADA`,
  com o letreiro e o motivo. Era a decisão pendente do 5.6: sem cor de
  análise (no plano a "cor própria" é da borda), o aviso é o que se vê;
- **identidade** (regra sagrada 3): GUID próprio por mesa, pilar e módulo,
  em XData, com os números que o plano de requisitos pede na mesa (cotas,
  tilt) e no pilar (estação, P1, P2, P3, problema). A face carrega o GUID do
  módulo dela, com outro tipo.

### O comando

`UFV_FILEIRA` pede o mínimo: a área (se houver uma só, nem pergunta), o
alinhamento (idem) e o número da fileira (1 é a que encosta na linha). A
mesa é o primeiro perfil salvo, ou a de exemplo; a configuração e as regras
vêm do desenho (4.4) ou do padrão. Tudo isso é dito na linha de comando,
com a distribuição inteira (quantas fileiras, quantas mesas, quantas na
borda), o relatório por mesa e a faixa de cotas de topo dos pilares. Avisa
quando a linha diverge mais de 5° do azimute configurado.

`UFV_FILEIRA_AUTO` (primeira área, primeiro alinhamento, fileira 1, sem
perguntar) existe para o nível 2.

### O que o nível 2 pegou antes do Renan

A polilinha de alinhamento gravada no desenho é a **drapejada**, com um
vértice em cada aresta do terreno: 43 vértices num traçado de dois pontos.
O distribuidor tratou cada trechinho como um trecho com família própria de
fileiras: **85 fileiras, 85 mesas, 595 sobreposições**, uma mesa desenhada.
Os vértices do drapeamento estão exatamente na reta do traçado em planta,
e `PlanPaths.SimplifyCollinear` (Geo, 9 testes) os tira antes da
distribuição. Vale para a área também (180 vértices → 4, com a volta do
polígono fechado tratada).

Com isso, no terreno de Itatiba, área de 100 m em volta do centro,
alinhamento na borda sul e mesas ao norte:

```
FILEIRA F1: 6 mesa(s) em 1 trecho(s): 1 marcada(s), maior degrau 0,41 m
  distribuição: 17 fileira(s), 102 mesa(s), 22 na borda
  desenhado: 6 mesa(s), 42 pilar(es), 168 módulo(s) com face, 9 pintada(s)
  topo dos pilares: 719,37 a 732,58 m; tempo: 0,1 s
  F1.1 MARCADA (nenhuma cota da ponta baixa respeita a faixa), 5 pontas fora
  F1.6 na borda da área; pilares de 1,88 a 2,49 m
```

O LISP contou 42 blocos de pilar, 168 blocos de módulo, 168 faces e 42
textos de altura, com o topo dos pilares entre 719,4 e 732,6 m — dentro da
faixa do terreno. A prova fina da regra sagrada 5 é outra: em cada pilar,
topo − terreno = P3, lidos do XData e da geometria, pilar a pilar (o Z =
9999 no alinhamento é prova do 4.2, que o drapeia antes de gravar; fica
como segunda linha de defesa).

A F1.1 marcada é o terreno de verdade: a mesa cai num trecho em que
nenhuma cota da ponta baixa respeita a faixa com a inclinação máxima de
10°. É o que o plano quer: encaixa o máximo, marca o resto.

## 5.9: a área inteira

`PlantPipeline.ProcessAll` no Core (todas as fileiras da distribuição, uma
a uma, com o tempo medido e os totais: mesas, módulos, potência como soma
dos módulos — regra sagrada 3 —, pilares, marcadas, pilares com problema)
e `UFV_USINA` no plugin, botão "Usina" na seção Processar. O comando mede
o motor e o desenho em separado e diz os dois.

As fileiras são independentes (o plano diz que as pontas baixas de
fileiras vizinhas não precisam casar), então a área inteira é o 5.8
repetido. Uma exceção numa fileira derruba o processamento inteiro de
propósito: o que o terreno tem de errado vira marca; exceção é defeito
nosso, e esconder defeito atrás de "marcada" seria mentir.

### Os números no Core Console (terreno de Itatiba, área de 100 × 100 m)

```
USINA 17 fileira(s), 102 mesa(s), 2856 módulo(s), 2056,3 kWp, 714 pilar(es);
      14 mesa(s) marcada(s), 23 pilar(es) com problema, 22 na borda; 0,6 s
  desenhado: 102 mesa(s), 714 pilar(es), 2856 módulo(s) com face, 140 pintada(s)
  comprimento de pilar: de 1,41 a 3,28 m, média 2,20 m
  tempo: motor 0,6 s, desenho 0,2 s
```

O LISP contou 714 blocos de pilar, 2856 faces e 102 contornos, com o topo
dos pilares entre 714,3 e 732,6 m, dentro da faixa do terreno. Do lado do
motor, o teste de nível 1 processa 1300 mesas (36 mil módulos) sobre
relevo ondulado em poucos segundos.

Os 23 pilares com problema estão nas fileiras 11 a 17, na ponta norte da
área, onde um vértice da área caiu fora do terreno: são pés fora do terreno,
marcados como tal, e não defeito de conta. As 14 mesas marcadas são o
terreno real com 10° de limite: encaixou o máximo, marcou o resto.

**O que fica para o Renan (validação do 5.9 pelo plano):** rodar numa usina
que ele conhece do PVcase e comparar contagens e alturas. O relatório dá
mesas, módulos, kWp, pilares e a faixa de comprimentos; os textos de altura
(`UFV_ALTURAS`) dão P1, P2 e P3 pilar a pilar.

**Acervo:** o plano diz que o resultado da 5.8 e da 5.9, conferido pelo
Renan, vira referência congelada. Fica para depois da conferência dele.

### O que a revisão do 5.7/5.8 apontou, e o que foi feito

Um bloqueante, três importantes, todos corrigidos.

- **Bloqueante: a cor de análise não aparecia nos blocos.** A caixa dentro
  da definição do bloco estava na camada 0 com cor "por camada", e a regra
  do AutoCAD é que entidade aninhada por camada na camada 0 toma a cor da
  CAMADA da instância, não a cor posta na instância. Módulo com ponta baixa
  fora e pilar com problema sairiam brancos; o nível 2 dizia "9 pintadas" e
  o Renan veria nada. Agora a caixa é "por bloco" (índice 0), e o LISP lê o
  62 da caixa da definição;
- **o nível 2 dependia do estado da máquina**: `UFV_FILEIRA_AUTO` usava o
  primeiro perfil salvo, e o teste fixava 7 pilares e 28 módulos. Com um
  perfil salvo do 3.6, quebrava ou passava por sorte. Os comandos
  automáticos usam sempre a mesa de exemplo; os do produto continuam com o
  perfil salvo;
- **a face tinha o mesmo GUID do módulo**, contra o "identidade própria" que
  o próprio diário registrava, e um verificador literal da regra sagrada 3
  ("nenhum GUID repetido") reprovaria. Agora `FaceIdentity` tem GUID próprio
  e o GUID do módulo como campo;
- **o nível 2 não provava a matriz nem a orientação das faces**: só as faces
  (que usam a `Transform` do Geo) eram contadas; o bloco do módulo (que usa
  a `Matrix3d` do AutoCAD) podia estar transposto e o teste passava. Agora o
  LISP leva o ponto de inserção de cada bloco de módulo ao WCS (`trans`) e
  exige que coincida com um vértice de face a 1 mm; confere que toda face
  aponta para cima; a seção e o comprimento de cada pilar; XData em pilar,
  módulo, face e contorno; **topo − terreno = P3 em cada pilar** (o
  `GroundZ` entrou no XData do pilar para isso); a camada de alturas
  desligada; um aviso por marcada.

Menores, corrigidos: `PlanPaths` comparava tolerância em metro com um
parâmetro adimensional (um ponto um metro além da ponta de um trecho de um
quilômetro contava como em cima); a área é gravada fechada sem repetir o
primeiro vértice e o último vértice do drapeamento ficava (parâmetro
`closed`, com a volta tratada; 180 → 4 de verdade); o prompt do número da
fileira mostraria "<1>: <1>"; `UpgradeOpen` sem guarda; a inclinação
gravada na identidade da mesa vinha recomposta da matriz quando o chamador
a tinha na mão; o comando avisa quando o desenho já tem mesas nossas (ele
desenha por cima; limpar é assunto da etapa 7).

Anotado, não feito: `GarantirLayer` existe em três lugares (área,
alinhamento, layout) e `PorHandle` duplica o do terreno envelhecido — vale
consolidar; `LayoutXData.Load*` não tem uso nem teste ainda (é o par
simétrico, para as etapas 6 e 7).

## 6.1: o escritor DAE

Fechado em 26/09/2026, só modelo, `VALIDADO (automático)`. `ColladaWriter`
em `UFV.Core` recebe `ModuleFace` (GUID da face + quatro cantos em metro) e
devolve um `XDocument` Collada 1.4.1: unidade metro, `Z_UP`, uma geometria
(quatro vértices, dois triângulos, uma normal) e um nó por face. Dezesseis
testes: XML bem formado e relido, contagem, os doze números de cada face
voltando iguais com ponto decimal, normal unitária para cima, material,
recusas.

### O que o PVsyst reconhece: o MATERIAL, não a camada

O plano dizia "indica a layer do módulo" na importação. O help do PVsyst 7
e 8 diz outra coisa: "pick up one or more materials used in the imported
scene and convert the faces which use them to PV fields". Não há camada em
Collada; o que o PVsyst lista é o material. Então todas as faces usam um
único material cujo NOME é o nome da camada de faces (`MARCHENG_UFV_FACE`
hoje), e o mesmo nome vai nos nós para quem abrir o arquivo noutro
programa. É esse nome que o Renan escolhe no PVsyst no 6.3. O exemplo
oficial da especificação PVCollada (`SampleFixedPVC.pvc`, do repositório
pvlib/pvcollada) usa `triangles`, não `polylist`, e é isso que o escritor
usa também.

### Decisões que são minhas

- sem face é erro (`ArgumentException`), porque o esquema exige geometria e
  nó e um arquivo vazio não serve; o comando do 6.2 diz "nenhum módulo"
  antes de chamar;
- cantos em ordem horária são invertidos em silêncio (a normal sai para
  cima de qualquer jeito); face vertical ou sem área é recusada; quatro
  cantos tomados como coplanares (mesa rígida, regra sagrada 2);
- id do material com prefixo `material-` para não colidir com o id da cena
  nem com os das faces; o nome fica com a camada tal como é;
- cor do material azul escuro fixa (0.1 0.2 0.5); só aparece na
  visualização do PVsyst.

### O que a revisão do 6.1 apontou, e o que foi feito

Nenhum bloqueante. Dois importantes, corrigidos: o documento com zero faces
era inválido pelo esquema e o teste dizia "válido" (agora recusa); cantos
horários davam face virada para baixo sem aviso (agora inverte, com teste).
Menores corrigidos: colisão de id se a camada se chamasse `cena`; testes
para NaN, `Guid.Empty` e face vertical; teste de tempo com folga (5 s).
Anotado para o 6.2: `XDocument.ToString()` descarta a declaração XML, gravar
com `Save`; **subtrair uma origem local das coordenadas UTM** antes de
escrever (E ≈ 300 000 e N ≈ 7 400 000 em float32 perdem o milímetro) e
registrar a origem; conferir no PVsyst (6.3) se os dois triângulos de cada
face viram um campo PV só; e que um nó por módulo numa usina grande são
dezenas de milhares de objetos, que o PVsyst importa devagar.

## 6.2: o botão Exportar

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO` (é tela: seleção, janela de
arquivo, e o resultado só se vê no PVsyst, que é o 6.3). Nível 1: 19 testes
na etapa 6. Nível 2: `ufv-exportar.scr` processa a fileira do 5.8, exporta
com `UFV_EXPORTAR_AUTO` e o PowerShell RELÊ O ARQUIVO: XML válido, uma
geometria e um nó por face contada em LISP (168), material único chamado
`MARCHENG_UFV_FACE`, unidade metro, e o primeiro vértice da primeira face
do desenho (menos a origem dita pelo comando) achado no DAE ao milímetro.

### O que o comando faz

- `UFV_EXPORTAR` (botão "Exportar", seção PVsyst): pede uma seleção com um
  filtro de 3DFACE que tenham o nosso XData (código 1001, nome de
  aplicativo `MARCHENG_UFV`); aceita seleção prévia (`UsePickSet`). Cada
  face é lida pelo `LayoutXData.LoadFace` (que passa a ter uso e teste de
  nível 2); face sem identidade é ignorada e contada. Pergunta o formato
  (`DAE`/`PVC`; PVC responde "ainda não, é o 6.5"), abre a janela padrão de
  salvar do AutoCAD (`GetFileNameForSave`, sugere o nome do desenho), grava
  sem BOM e diz: faces, material, origem local;
- `UFV_EXPORTAR_AUTO`: todas as faces do espaço do modelo, caminho pedido na
  linha de comando, sem janela. Só para o nível 2.

### Decisões que são minhas

- **origem local**: as coordenadas UTM do desenho (E ≈ 314 000, N ≈
  7 456 000) perdem o milímetro em float32, que é o que leitores de cena 3D
  usam. O arquivo vai relativo ao menor X, menor Y e menor Z das faces
  arredondados ao metro inteiro (E=314018 N=7456163 Z=718 no Itatiba); a
  origem está no `<comments>` do cabeçalho, legível de volta
  (`ColladaWriter.ParseOriginComment`), e na linha de comando. Se o Renan
  precisar do arquivo nas coordenadas do desenho (PVsyst não precisa: a
  cena dele é local), é um campo a mais na tela;
- o GUID da face é o id da geometria e do nó no arquivo: a regra sagrada 3
  atravessa para o PVsyst;
- o material leva o nome da camada das faces (`MARCHENG_UFV_FACE`) como
  RÓTULO; quem diz que a face é nossa é o XData, e a seleção filtra por ele;
- o formato é perguntado mesmo só havendo DAE, para o botão não mudar de
  comportamento quando o PVC chegar.

### O que a revisão do 6.2 apontou, e o que foi feito

Nenhum bloqueante. Três importantes, corrigidos: (1) mesa copiada e colada
dá duas faces com o mesmo GUID e a exportação caía no catch genérico com a
mensagem do Core; agora `Recolher` conta as repetidas e o comando diz
"apague as cópias ou reprocesse a fileira" e não grava; (2) o nível 2 lia a
origem só da linha de comando; agora lê do `<comments>` do arquivo, exige
que seja igual à dita, e usa a do arquivo; (3) o vértice era procurado em
qualquer geometria; agora o LISP imprime o GUID da face (XData) e o
PowerShell procura o vértice na geometria `face-<guid>` daquela face.
Menores corrigidos: `Path.HasExtension` aceitava "usina.v2" (agora só
`.dae` conta); caminho resolvido com `GetFullPath` uma vez e usado em tudo;
`InitialDirectory` na pasta do desenho quando ele tem nome; o `<summary>`
de `Geometria` estava em cima de `LocalOrigin`; o prompt do formato deixava
o AutoCAD repetir "[DAE/PVC]" (agora só "Formato" e o AutoCAD monta o
resto); a linha `ORIGEM` invariante só sai no comando automático. Anotado,
não feito: uma 3DFACE nossa que o usuário escalou até ficar sem área ou
vertical derruba a exportação inteira com a mensagem do Core; pular e
contar seria melhor (Observações).

## 6.4: o formato PVC (estudo, 26/09/2026)

O plano manda "levantar a especificação do PVC, entregar um resumo ao Renan
e PARAR". Este é o resumo. O 6.5 não começou.

### Fontes

- Help do PVsyst: "PV Collada file format" e "Sketchup and other CAD
  software" (pvsyst.com/help, versões 7 e 8);
- repositório `pvlib/pvcollada` no GitHub, onde a especificação vive:
  README, `docs/dev_guide.md`, `docs/pvcollada_coordinate_transformation…`,
  `schema/PVCollada_2.0/pvcollada_schema_2.0.xsd`, os schematron de
  estrutura/referências/negócio, a extensão do PVsyst
  (`extensions/pvsyst/*.xsd`), e os exemplos `Examples/PVCollada_1.0/
  SampleFixedPVC.pvc` e `Examples/PVCollada_2.0/01…07.pvc2` (mais dois com a
  extensão do PVsyst);
- PVcase, "PVC 2.0: the open-source solar data exchange standard" (blog).
  A página de ajuda do PVcase sobre exportar para o PVsyst responde 403 a
  quem não está logado; **não tenho um arquivo exportado pelo PVcase** (o
  plano pedia; ver pergunta 2).

### Há dois PVC, e não são compatíveis entre si

**PVC 1.4.1 (`.pvc`)**, o antigo. Collada 1.4.1 "modificado" (o próprio
pvlib chama de *non-compliant*): `<created>`/`<modified>` opcionais, ids
de cena como texto livre, e um filho a mais dentro de cada `<mesh>`:
`<frame_parameters>` (mesa fixa: `module_width`, `module_height`,
`module_x_spacing`, `module_y_spacing`, `module_manufacturer`,
`module_name`) ou `<tracker_parameters>` (os mesmos mais `tracker_type`,
`axis_vertices`, `min_phi`, `max_phi`, `min_theta`, `max_theta`). Uma
geometria por mesa: o plano da mesa como um paralelepípedo fino (8
vértices no exemplo), material "Frames"; árvores e terreno vão como malhas
comuns com outros materiais. O PVsyst importa desde a 7.0. Não há esquema
formal: o `pvcollada_schema_1.0.xsd` do repositório é uma cópia byte a byte
do Collada 1.4.1 (mesmo tamanho), ou seja, o formato é o que o exemplo
mostra. O exemplo tem `<triangles count="0">` com índices dentro, e o
PVsyst lê mesmo assim: engenharia reversa, com o risco que isso tem.

**PVC 2.0 (`.pvc2`, ou `.pvz2` zipado)**, o atual. Collada 1.5 VÁLIDO, com
tudo que é fotovoltaico em `<extra><technique profile="PVCollada-2.0">`,
namespace `https://pvcollada.org/2026/XMLSchema` (prefixo `pv`). Mantido
no pvlib com PVcase, PVsyst, Sandia e DNV. **PVsyst lê desde a 8.1.5;
PVcase escreve desde a 2.60.** É o que o plano chama de "carrega também
dados de módulos e mesas" e o que o 6.5 deve escrever, se a versão do
PVsyst do Renan permitir (pergunta 1).

### A estrutura de um `.pvc2` de mesas fixas (do exemplo 01 e do esquema)

1. `<asset>`: `contributor`, `coverage/geographic_location` com
   `longitude`, `latitude`, `altitude` (é a ORIGEM do sistema local
   leste-norte-cima, em graus e metro), `created`, `modified`, `unit`
   (o exemplo usa cm; metro é permitido), `up_axis` Z_UP, e o `<extra>`
   com `pv:software` (`source`, `target`), `pv:project` (`name` e,
   opcionais, `drawing`, `company`, `country`, `timezone`,
   `local_projection`, `module_count`, `table_count`, `capacity_dc`…) e
   `pv:components/pv:modules/pv:module id=…`.
2. `pv:module` exige 23 campos: `manufacturer`, `name`, `module_type`,
   `module_architecture`, `nom_power`, `length`, `width`, `depth`,
   `num_cells`, `num_cells_length`, `num_cells_width`, `num_cells_series`,
   `num_strings`, `cell_material`, `cell_architecture`, `bifacial_factor`,
   `t_coef_power`, `t_coef_isc`, `t_coef_voc`, `i_sc`, `i_mpp`, `v_oc`,
   `v_mpp`. **Temos seis** (`SolarModule`: marca, modelo, potência, altura,
   largura, espessura). A extensão do PVsyst (`technique
   profile="PVCollada-2.0-PVsyst"`, namespace
   `https://www.pvsyst.com/pvcollada-2.0-extensions`) acrescenta
   `pvsyst:module module_id=… > pan_file_name` (e, opcional,
   `pan_file_content` em CDATA): "the PAN data or the data from the PVsyst
   database will be used for that module instead of the data defined in
   the PVCollada extension". Ou seja: com o nome do PAN, o PVsyst ignora os
   23 campos, mas o esquema continua a exigi-los no arquivo (pergunta 2).
3. `library_geometries`: por modelo de mesa, uma `geometry` com o PLANO da
   mesa (4 vértices, 2 `triangles`, no exemplo o retângulo inteiro com os
   módulos implícitos) e `<extra>` `pv:rack`: `rack_type` fixed_tilt,
   `module_rows`, `module_columns`, `module_orientation`
   (landscape/portrait), `row_spacing`, `column_spacing`, `inset_*`,
   `tilt` e `azimuth` em graus (obrigatórios para mesa fixa, pelo
   schematron), `slope` (o nosso giro longitudinal; permitido em mesa fixa,
   proibido em tracker), `height_above_ground`, `module_id`. A mesa é
   descrita, não desenhada módulo a módulo: as faces do 6.1 não servem
   aqui, o que serve é a `TableGeometry` + a `SolvedTable`.
4. `library_nodes`: um `node` "modelo de mesa" com `instance_geometry` do
   rack, `bind_material`, `<extra>` `pv:instance_rack id` e `pv:table`
   (`type` fixed; para tracker, `tracker_type`, curso etc.).
5. `library_visual_scenes`: um `node` por mesa instalada, com
   `<translate>`/`<rotate>`, `instance_node` do modelo e `<extra>`
   `pv:instance_table id` com `instance_racks_array/instance_rack_ref`.
   Opcionalmente um nó de terreno (`geometry` + `pv:terrain` com contagens
   e caixa) instanciado com `pv:instance_terrain`.
6. Coordenadas: sistema LOCAL leste-norte-cima (x = leste, y = norte, z =
   cima) cuja origem é a `geographic_location`. O doc do pvlib mostra a
   conversão (pymap3d/pyproj); o PVsyst "usa a localização do arquivo para
   verificação de consistência com o sítio do projeto" (distância ao
   sítio).

### O que já temos, o que falta, e como o 6.5 ficaria

- **Temos**: geometria por mesa (cantos do plano, tilt, giro longitudinal,
  direção → azimute), linhas e colunas de módulos e vãos
  (`TableLayout`: arranjo, `HorizontalGap`, `VerticalGap`, margens →
  `inset_*`), GUIDs de mesa (viram os ids de `instance_table`), potência e
  medidas do módulo, e a `GeoLocation` (lat/long) do desenho.
- **Falta**: (a) os 17 campos elétricos do módulo, ou o nome do PAN; (b) a
  altitude e a lat/long DA ORIGEM LOCAL (a `GeoLocation` é do sítio; a
  origem tem que ser convertida do UTM do desenho para lat/long, o que
  pede o sistema de coordenadas do desenho no Civil 3D, ou um ponto de
  referência digitado); (c) a convenção de azimute do PVCollada (o exemplo
  põe 180 numa mesa a 46° N; é bússola, 180 = sul? o esquema não diz; o
  PVsyst usa 0 = sul no hemisfério norte e 0 = norte no sul); (d) um
  arquivo do PVcase para comparar.
- **Forma do 6.5**: `PvColladaWriter` puro no Core, uma `geometry` POR
  MESA (cada mesa tem giro e cota próprios; um modelo por mesa evita a
  matriz de rotação no nó e é válido pelo esquema), origem local do 6.2
  convertida para lat/long/alt na `geographic_location`, unidade metro,
  extensão do PVsyst com o nome do PAN. Teste de nível 1 contra o XSD do
  Collada 1.5 + PVCollada 2.0 com `XmlSchemaSet` do .NET (os XSD vão para
  `tests/` como referência), e as regras do schematron de mesa fixa
  (azimute e tilt presentes, sem campos de tracker) como asserções. Um
  `validate.py` com lxml existe no repositório se quisermos a conferência
  completa fora do .NET.

### As três perguntas para o Renan (o 6.5 não começa sem elas)

1. **Qual versão do PVsyst?** 8.1.5 ou mais: PVC 2.0, formato documentado e
   validável. Menos que isso: PVC 1.4.1, por engenharia reversa do exemplo,
   e sem esquema. (Ou atualizar o PVsyst, que é a saída limpa.)
2. **Dados elétricos do módulo.** Prefere (a) acrescentar à biblioteca de
   módulos (`modulos.json`, janela da Mesa) o nome do arquivo PAN e deixar
   o PVsyst buscar o resto na base dele, com os 17 campos elétricos
   preenchidos com o que o PAN diz (você me manda o PAN); ou (b) digitar os
   17 campos na biblioteca? A (a) é o que a extensão do PVsyst foi feita
   para fazer. E: **consegue exportar um `.pvc2` do PVcase** do mesmo projeto
   (Itatiba) e me mandar? É a referência que falta para comparar campo a
   campo.
3. **Georreferência do desenho.** O DWG do Itatiba tem sistema de
   coordenadas atribuído no Civil 3D (Configurações do desenho > Unidades
   e zona; algo como SIRGAS2000.UTM-23S)? Se tem, converto a origem local
   para lat/long/alt por ele. Se não, você digita lat/long de um ponto
   conhecido do desenho (e eu registro qual) — ou aceita que a cena vá
   com a `GeoLocation` do sítio como origem, sem a conversão exata.

## 7.1: o estado sujo

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO` (a cor na tela). Nível 1:
cinco testes na etapa 7 (`LayoutIdentitiesTests`). Nível 2: `ufv-sujo.scr`
processa a fileira do 5.8, suja a primeira mesa com `UFV_SUJAR_AUTO` e lê
em LISP, pelo XData: exatamente um contorno com suja=1 e motivo; as 36
peças pintáveis dessa mesa (contorno, 7 pilares, 28 módulos) vermelhas; as
28 faces dela intactas; `UFV_ESTADO` contando 1 suja de 6.

### Onde o estado mora, e por quê

`TableIdentity` ganhou `Dirty` e `DirtyReason` (Core), com `AsDirty(motivo)`,
`AsClean()` e `DescribeState()`. Suja sem motivo, ou limpa com motivo, não
é válida: é XData corrompido, e a mesa deixa de ser reconhecida (como
qualquer XData ilegível). O estado é gravado no XData do CONTORNO da mesa,
que é a entidade que carrega a identidade dela desde o 5.7; pilares e
módulos só apontam para a mesa. XData da mesa passou à **versão 2** (nove
campos); a versão 1 (sete campos, etapa 5) continua a ser lida, como limpa,
para os desenhos de antes não perderem as mesas.

### As peças de uma mesa: `LayoutScan`

`LayoutScan.Tables` varre o espaço do modelo uma vez e agrupa as nossas
entidades por GUID de mesa, lido do XData (contorno → id; pilar, módulo e
face → campo "mesa"). Devolve `TableParts` (identidade, contorno, pilares,
módulos, faces), inclusive mesas cujo contorno sumiu (peças órfãs, que o
`UFV_ESTADO` conta). Nunca pela camada. É a base do resto da etapa 7:
vigia, recalcular, cópia, validar e numerar precisam de "quais peças são
desta mesa". É varredura inteira a cada chamada; um índice fica para
quando medir mostrar que precisa.

### A pintura

`TableState.MarkDirty` grava o XData e põe vermelho (cor na instância) no
contorno, nos pilares e nos módulos, por cima da cor de análise que a peça
tinha. **A face nunca é pintada**: a camada de faces é o que o PVsyst
recebe. Não há "despintar" nem "limpar" neste passo: quem limpa é o
recálculo (7.3/7.4), que redesenha a mesa inteira com as cores certas.
Sujar de novo uma mesa suja só troca o motivo.

### Decisões que são minhas

- os comandos chamam-se `UFV_SUJAR` e `UFV_ESTADO` (botões "Sujar" e
  "Estado" numa seção Edição): "sujo" é a palavra do plano, e "marcar" já
  significa outra coisa (a mesa que o alinhamento marcou);
- o motivo do sujar manual é "pedido do usuário"; o vigia (7.2) vai gravar
  "movida", "copiada", "editada";
- o vermelho é o `RgbColor.Red` do Core, o mesmo da camada de marcadas;
- os textos de altura e o aviso de marcada não têm identidade (não
  carregam XData) e por isso não fazem parte da mesa para o `LayoutScan`;
  não são pintados nem serão apagados por mesa até ganharem identidade
  (anotado em Dívidas).

### O que a revisão do 7.1 apontou, e o que foi feito

Nenhum bloqueante. Um importante, corrigido: `LoadTable` rejeitava a mesa
inteira quando o par suja/motivo do XData vinha incoerente (dois caracteres
errados apagavam a identidade que a regra sagrada 3 protege); a leitura do
desenho agora é tolerante (suja é "1"; suja sem motivo ganha "motivo
perdido"; limpa ignora o motivo) e a coerência estrita fica só no `IsValid`
do Core. Menores corrigidos: o nível 2 não comparava o GUID que o comando
disse ter sujado com o que o XData mostra (agora compara) nem provava que
as outras mesas ficaram como estavam (agora guarda os handles do que já
era vermelho antes de sujar, e exige zero vermelho novo fora da mesa
suja: pilares com problema já nascem vermelhos no 5.7 e enganavam a
primeira versão); o vermelho é conferido pela cor verdadeira (420) além do
índice; `LayoutScan` filtra pela classe do `ObjectId` antes de abrir
(bloco, polilinha 3D, face), sem abrir a superfície nem os textos;
`TableParts.Count` sem uso, apagado; camada bloqueada tem mensagem
própria. Anotado para os próximos passos: `PluginXData.Load` é chamado até
quatro vezes por entidade na varredura (um leitor único que despache pelo
tipo resolve); o 7.2 não pode chamar `LayoutScan.Tables` a cada evento
(usar `TableOf` do objeto modificado, ou um índice em memória); a cópia
(7.5) hoje agrupa as peças de dois contornos sob um GUID; e os textos de
altura e de marcada precisam de identidade antes do 7.3 (Dívidas).

## 7.2: o vigia

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO` (é comportamento de tela:
mover, apagar, copiar). Nível 1: sete testes (`TableChangesTests`). Nível 2:
`ufv-vigia.scr` processa a fileira do 5.8 e, com comandos do PRÓPRIO
AutoCAD (`_.MOVE` num pilar da mesa A, `_.ERASE` no contorno da mesa B),
lê pelo XData: A suja com motivo "movida ou editada" e as 36 peças
vermelhas; nenhuma outra mesa suja; um contorno a menos; o vigia anunciou
as duas coisas; `UFV_ESTADO` conta 1 suja, 1 órfã e lista B como removida.
Os eventos do banco e de comando disparam no Core Console, o que não era
garantido antes de rodar.

### A divisão: o Core decide, o plugin escuta

`PendingChanges` (Core, puro) é o livro de mudanças: durante um comando,
recebe "peça da mesa X foi modificada / acrescentada / apagada, e é ou não
o contorno"; ao fim, `Resolve` devolve mesa → motivo (copiada > peça
apagada > movida ou editada, o mais grave vence) e a lista de removidas
(contorno apagado; removida não fica suja, porque não há onde gravar). O
plugin (`LayoutWatcher`) só escuta `ObjectModified`, `ObjectAppended` e
`ObjectErased` do banco e `CommandWillStart`/`CommandEnded`/`Cancelled`/
`Failed` do documento, lendo o XData da peça dentro do evento (só leitura)
e executando a decisão no fim do comando, numa transação: `MarkDirty` do
7.1 nas sujas e `RemovalStore.Add` nas removidas, com uma linha "VIGIA …"
por mesa. Um vigia por documento, ligado no `Initialize` (todo host,
inclusive sem interface) e nos que abrirem depois.

### Os dois silêncios

Sem eles o vigia se morde: (1) durante um comando NOSSO (nome começa com
`UFV_`, `PluginInfo.PrefixoDeComando`) nada é anotado, e o livro é
esvaziado no fim, senão `UFV_FILEIRA` sujaria tudo que desenha; (2)
enquanto o próprio vigia pinta nada é anotado, porque pintar é modificar.

### Decisões que são minhas

- "apagar mesa" = apagar o CONTORNO. Apagar só pilares ou módulos suja a
  mesa com "peça apagada"; apagar o contorno registra a remoção e deixa as
  peças que sobraram como órfãs (o `UFV_ESTADO` as conta; o 7.6 reconta);
- a remoção mora no dicionário do desenho (`REMOVIDAS`, GUID, letreiro,
  data), no mesmo formato de registro das áreas e alinhamentos, sem
  repetir mesa. Consumir é do 7.6;
- "desapagar" (UNDO de um apagar) conta como modificada; UNDO de um MOVE
  deixa a mesa suja (ela foi tocada duas vezes; recalcular resolve);
- cópia (7.5 ainda não) suja original e cópia com "copiada", porque as
  duas têm o mesmo GUID e o `LayoutScan` as agrupa;
- o motivo do vigia é curto e fixo; o nome do comando vai na linha de
  comando, não no XData.
- **o que o vigia grava no fim do comando NÃO entra no grupo de undo do
  comando** (visto no nível 2: depois do `_.U` do ERASE o contorno voltou
  mas o registro de remoção ficou, e foi o vigia quem o tirou). Logo um U
  depois de um MOVE devolve a mesa ao lugar e ela CONTINUA suja e
  vermelha; é conservador e o recálculo resolve. O Renan confere na tela.

### O que a revisão do 7.2 apontou, e o que foi feito

Nenhum bloqueante. Quatro importantes, corrigidos: (1) desfazer e refazer
(U, UNDO, REDO, MREDO, OOPS) sujavam a mesa que o banco só devolvia a um
estado já decidido; agora são comandos calados (`PluginInfo.IsSilencedCommand`,
com teste), em que nada suja e só o registro de remoções acompanha o
contorno que some ou volta; (2) UNDO de um ERASE deixava a mesa registrada
como removida e presente ao mesmo tempo; agora `ObjectErased` com
`Erased=false` é `ChangeKind.Restored`, o livro devolve `Restored`, e o
vigia tira a mesa do registro (`RemovalStore.Remove`), com o nível 2
provando com um `_.U` depois do ERASE (contorno de volta, 0 removidas, B
limpa); (3) o bool "comando nosso" virou contador de profundidade, para
comando nosso que chame outro comando não destravar o vigia no meio; (4) a
paleta de Propriedades altera entidade sem comando e a anotação ficava
esperando o próximo `CommandEnded` (podia ser o salvar); agora, sem comando
em andamento, o descarregamento é agendado para a folga do AutoCAD
(`Application.Idle`, com a trava do documento). Menores corrigidos:
`ResultBuffer` do XData com `using`; `RemovalStore.Load` e
`PendingChanges.Count` sem uso, apagados; `Anotar` reaproveita
`LayoutScan.TableOf`; os handlers do `DocumentManager` são guardados e
desassinados. Descoberto no nível 2: `UFV_ESTADO` deixava marca de undo e
um `U` depois dele desfazia o ESTADO em vez do ERASE; comando que só lê
agora leva `NoUndoMarker`. Anotado, não feito: cancelar um comando no meio
deixa a mesa suja (conservador); o 7.4 é o lugar de medir a varredura
(`LayoutScan.Tables`) numa usina inteira e decidir sobre um índice em
memória; o COPY suja original e cópia sob o mesmo GUID até o 7.5 (está no
roteiro de tela).

## Reprovação do 5.8 em 26/09/2026: a fileira estava invertida

O Renan instalou o bundle, pôs a área e o alinhamento e disse: "vc fez
invertido, o alinhamento é uma linha perpendicular às fileiras, toda
fileira nasce nele, e vai a 90 graus, vc fez a fileira seguindo o
alinhamento". Eu tinha lido "as mesas começam aqui e seguem para aquele
lado" como fileiras paralelas à linha, afastadas a cada pitch. O modelo
certo: a linha é o eixo transversal da usina; as fileiras nascem nas
estações da linha (0, pitch, 2·pitch…) e correm perpendiculares a ela,
para o lado clicado; a mesa 1 de cada fileira começa na linha (ou onde a
área começa) e as seguintes vêm depois do espaçamento; o fundo da célula
corre ao longo da linha; e a linha deve ser traçada paralela ao azimute
(norte-sul numa usina que olha para o norte).

### O que mudou

- `RowDistributor.Distribute`: uma fileira por estação da linha enquanto a
  estação estiver no trecho traçado (a linha define até onde vão as
  fileiras; a área define até onde vai cada fileira); direção da fileira =
  perpendicular ao trecho para o lado clicado; célula com comprimento ao
  longo da fileira e fundo ao longo da linha; as mesas nunca começam atrás
  da linha. A conferência de sobreposição entre famílias de uma linha
  quebrada ficou igual;
- `RowOrientation.Resolve`: a normal da célula passou a ser o eixo da
  linha (esquerda da fileira quando as fileiras vão para a direita da
  linha, e vice-versa). O resto do pipeline (5.2 a 5.9) não mudou: só
  recebe células e orientações;
- os testes do 5.1 foram reescritos para o modelo certo (retângulo de
  100 × 50 com a linha na borda de baixo dá 17 fileiras de 3 mesas, 51
  mesas, 17 parciais); os testes das etapas seguintes que montavam uma
  fileira para o leste passaram a traçar a linha norte-sul na borda oeste
  com as fileiras à direita, e ficaram com as mesmas células e as mesmas
  expectativas; o nível 2 idem (a linha do Itatiba virou norte-sul);
- textos dos comandos: "1 nasce no início da linha"; o aviso de
  divergência diz que a linha deve ser paralela ela ao azimute.

### Decisões minhas nesse conserto (Renan confirma na tela)

- fileiras só nas estações do comprimento traçado da linha: linha curta,
  poucas fileiras. Se ele preferir que a linha reta gere fileiras até a
  área acabar, é uma linha no distribuidor;
- a fileira 1 é a do INÍCIO da linha (primeiro clique). O 7.10 é onde ele
  vai indicar a F1 à mão;
- linha 2 mm atrás da borda não marca mais nada: as mesas começam onde a
  área começa.

### As cotas, como no print do Renan (26/09/2026)

Na mesma reprovação do 5.8 o Renan mandou um print: "as alturas também
ficaram estranhas, vc precisa colocar igual no print, os riscos vermelhos,
a ponta baixa, ponta alta, e no centro a altura livre do pilar". O texto
empilhado "P1 … (P3 … + P2 …)" em cima de cada pilar saiu. Em cada pilar
o desenho passa a ter três cotas na camada de alturas: um risco vermelho
de 1 m (no plano da mesa, ao longo da fileira) na ponta baixa com o texto
"PB 0,45", outro na ponta alta com "PA 1,23", e no centro (o pilar) o texto
"P3 0,85"; texto girado com a fileira e centrado. Para isso o Core passou a
calcular, por pilar, `LowEdgeClearance` e `HighEdgeClearance` (o plano dos
módulos em y = 0 e y = fundo na estação do pilar, menos o terreno ali),
com teste. Pilar com problema leva só o motivo, no centro. O nível 2
conta três textos por pilar. Siglas PB/PA/P3 são minhas; se ele preferir
M1 (a nomenclatura do desenho de 23/09) para a ponta baixa, é uma string.

## Segunda reprovação do 5.8 em 26/09/2026: seis pedidos do Renan

Depois do primeiro conserto (fileira nascendo na linha), o Renan olhou de
novo e mandou dois prints e seis pedidos. Tudo feito no mesmo dia, nível 1
e 2 verdes.

1. **"A linha de alinhamento é mestra apenas do alinhamento lateral dos
   módulos. O azimute é definido nas configurações."** A fileira corre
   perpendicular ao AZIMUTE da configuração, sempre; a linha só diz onde
   cada fileira começa (a mesa 1 encosta nela; uma linha quebrada dá um
   começo escalonado). As fileiras se sucedem a cada pitch no sentido do
   azimute; a fileira 1 encosta no início da linha e a célula cresce no
   sentido em que a linha caminha. Linha paralela às fileiras é recusada
   com explicação. `RowDistributor.Distribute` ganhou o azimute como
   parâmetro; `RowOrientation.Resolve` passou a ler a normal da própria
   célula (os cantos), e o lado da linha saiu do pipeline. Terceira
   versão do 5.1 no dia; os testes foram reescritos de novo (retângulo de
   100 × 50 com a linha na borda oeste: 8 fileiras de 5 mesas).
2. **"As mesas não podem passar da área."** Mesa que não cabe inteira, ou
   que um recorte da área invade, não é colocada: `PlanLayout.DroppedOutside`
   conta e o relatório diz. `PartlyOutside` ficou no modelo, sempre falso;
   a análise de borda (4.3) não tem mais o que pintar (anotado em Dívidas).
3. **"Mesa socada na terra: já poderia vir pintada de uma forma que diz
   'olha projetista, ali não tem como fazer milagre'."** Mesa marcada pelo
   alinhamento, ou com pilar sem altura livre / fora do terreno, é pintada
   INTEIRA de magenta na camada de marcadas (contorno, pilares, módulos;
   a face não), com o aviso "F1.x NÃO CABE NO TERRENO" e o motivo no meio.
4. **"Botão direito na área → Refazer."** `UFV_REFAZER` (botão Refazer na
   seção Processar, e o item "Refazer as mesas desta área" no menu de
   botão direito sobre a polilinha da área, via `MenuDeContexto`): apaga
   tudo que o plugin desenhou dentro da área (pelo XData: mesa, pilares,
   módulos, faces e notas) e desenha de novo com a configuração ATUAL. As
   cotas e os avisos ganharam identidade (`NoteIdentity`, XData "Nota"
   com o GUID da mesa) para serem apagados junto: era a dívida anotada
   antes do 7.3, paga aqui. Nível 2 (`ufv-refazer.scr`): fileira, refazer,
   80 mesas, 7 pilares e 28 faces por contorno, nenhuma nota órfã.
5. **"Quando o sistema pede o nome, poderia aparecer uma janela."**
   `JanelaDeNome` (WPF) para área e alinhamento quando há interface; no
   Core Console continua a linha de comando (é o que o script alimenta).
6. **"Ele perde o terreno quando fecho e reabro."** A malha era só memória;
   o carimbo no desenho diz qual superfície foi processada. Agora, se não
   há terreno na memória, `TerrainCommands.Reprocessar` reprocessa sozinho
   a superfície do carimbo (pelo handle, senão pelo nome; sem carimbo, a
   única superfície do desenho), avisando na linha de comando.

Também respondi uma dúvida dele, sem fazer nada: terraplanagem sugerida é
possível (uma segunda superfície gerada pelo plugin, mesas calculadas sobre
ela, corte e aterro contra a original); fica para depois.

### Decisões minhas neste lote (Renan confirma na tela)

- a fileira 1 é a do início da linha (primeiro clique) e a célula cresce
  no sentido da linha; o 7.10 é onde ele indica F1 à mão;
- só nascem fileiras cujas faixas cruzam a linha: a linha deve atravessar
  todas as fileiras que ele quer;
- a mesa 1 começa no ponto mais adiantado em que a faixa cruza a linha
  (nenhum canto fica atrás da linha);
- magenta para "não cabe no terreno"; o vermelho continua sendo "suja"
  (7.1) e o de análise é o da configuração;
- o Refazer redesenha a área inteira (todas as fileiras), não só uma.

## 7.3 e 7.4: recalcular uma mesa, recalcular as sujas

Feitos em 26/09/2026, `AGUARDANDO VALIDAÇÃO`. Nível 1: sete testes
(`TableCellsTests`). Nível 2: `ufv-recalcular.scr` processa a fileira do
5.8, suja a primeira mesa e manda recalcular as sujas: a mesa com o MESMO
GUID continua existindo, limpa, com 7 pilares, 28 módulos e 28 faces, o
total de contornos não muda, `UFV_ESTADO` conta 0 sujas.

### O que "recalcular" significa

A mesa é recalculada ONDE ESTÁ: a célula em planta vem dos quatro cantos
do contorno desenhado (`TableCells.FromCorners`, Core): origem no canto da
borda baixa, direção da borda baixa, normal para o lado da borda alta, e
comprimento e fundo do PERFIL ATUAL (o contorno em planta não é retângulo
quando há giro longitudinal, é paralelogramo com a borda baixa encurtada;
a célula é a nominal e o giro o pipeline recalcula das cotas). Se a borda
baixa desenhada não fecha com o comprimento do perfil (trocou de mesa), o
comando recusa e manda usar o Refazer. Depois, `RowPipeline.ProcessRow`
com uma fileira de uma mesa só (o alinhamento com as vizinhas não é
refeito: isso é o Refazer), tudo dela é apagado (contorno, pilares,
módulos, faces, notas) e desenhado de novo pelo `LayoutDrawer` com o mesmo
GUID (`idDaMesa`), nascendo limpa. Uma mesa movida é recalculada na posição
nova, com a cota do terreno (regra sagrada 5): a posição é vontade do
usuário.

### Onde está

- `UFV_RECALCULAR` (botão "Recalcular" na seção Edição, e o item
  "Recalcular esta mesa" no botão direito sobre contorno, pilar, módulo ou
  face); aceita a seleção prévia ou pede um clique;
- `UFV_RECALCULAR_SUJAS` (botão "Recalcular sujas"): as sujas, em ordem de
  letreiro;
- `UFV_RECALCULAR_AUTO`: as sujas com a mesa de exemplo, para o nível 2.

### Decisões que são minhas

- recalcular usa o perfil de mesa e a configuração ATUAIS (é o que o
  Renan pediu para o Refazer: "sempre baseadas nas configurações");
- o letreiro é mantido (F1.3 continua F1.3), mesmo que a mesa tenha sido
  movida para outra fileira; renumerar é o 7.10;
- mesa sem contorno não se recalcula (não há onde ler a posição): o
  comando diz e manda usar o Refazer.

### O que a revisão (dos dois lotes de 26/09) apontou, e o que foi feito

Um revisor só para o lote dos seis pedidos e o do 7.3/7.4. Nenhum
bloqueante. Quatro importantes, corrigidos: (1) mesa copiada (dois
contornos com o mesmo GUID) entrava no "recalcular sujas" e perdia as
peças da cópia; `LayoutScan` passou a guardar TODOS os contornos do GUID
(`TableParts.Contours`, `IsDuplicated`), o Recalcular recusa a duplicada
("apague a cópia ou use o Refazer; dar identidade à cópia é o 7.5") e o
Refazer apaga as duas cópias; (2) o Refazer apagava e commitava antes de
distribuir: "nenhuma fileira cabe" deixava a área vazia; `UsinaCommands`
foi dividido em `Planejar` e `Desenhar`, e o Refazer só apaga depois de
planejar (e diz que U devolve as apagadas); (3) `Reprocessar` caía para o
NOME da superfície e regravava o carimbo, anulando o aviso de terreno
velho; agora só pelo handle, e sem o handle avisa "a superfície processada
em <data> não está mais no desenho (há outra com o mesmo nome); use o
botão Terreno"; (4) os níveis 2 do Refazer e do Recalcular não provavam o
que prometiam: o do Refazer agora compara o desenho com o resumo da usina
(nada da fileira antiga sobrou), e o do Recalcular guarda o GUID sujo antes
e confere depois: mesmo GUID, limpo, 7 pilares, 28 módulos, 28 faces,
nenhuma peça vermelha. Menores corrigidos: aviso de divergência quando a
mesa recalculada foi girada à mão; o Refazer decide por qualquer vértice do
contorno dentro da área e tira as apagadas do registro de removidas; uma
varredura só no "recalcular sujas"; `Loaded` da janela protegido;
zigue-zague documentado no distribuidor. Anotado, não feito:
`PartlyOutside` e a análise de borda estão mortos; `Overlap` só os testes
usam; `LayoutScan` abre toda `Line` e `MText` do desenho (medir numa usina
grande); linha que recua e depois avança faz F1 não ser a do primeiro
clique (sem teste).

## 7.5: a cópia

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO`. Nível 2: `ufv-copia.scr`
copia a mesa F1.x inteira (106 entidades: contorno, 7 pilares, 28 módulos,
28 faces, cotas e riscos) 300 m para o norte com o COPY do AutoCAD e lê
pelo XData: a original continua única e limpa; a cópia tem contorno com
GUID novo, suja "copiada", com 7/28/28 peças apontando para ela; nenhum
GUID de peça se repete no desenho (regra sagrada 3 conferida
literalmente). Depois renomeia o bloco do pilar para `…$0$` com o -RENAME
e o `UFV_RENOMEAR` o devolve ao padrão.

### Como a cópia ganha identidade

O vigia (7.2) guarda toda entidade acrescentada durante um comando que não
é nosso. No fim do comando, `CopyFixer.Reidentify` pega as que têm XData
nosso e as agrupa por (mesa de origem, deslocamento em relação à original
ao milímetro): cada grupo é uma cópia (o COPY múltiplo faz várias de uma
vez). Cada grupo ganha um GUID novo de mesa; cada pilar, módulo, face e
nota ganha GUID próprio novo, com a mesa apontando para a nova e a face
apontando para o módulo novo. O contorno da cópia nasce sujo "copiada"; a
original não é tocada. Uma peça acrescentada que dispara "modificado" no
mesmo comando (a cópia ganhando o XData) não suja a original: o vigia
ignora modificações em peças que ele viu nascer no comando.

### Decisões que são minhas

- cópia sem original no desenho (colada de outro desenho) forma uma cópia
  só por mesa de origem, com deslocamento zero;
- cópia espelhada ou girada não tem deslocamento único: cada peça vira o
  seu grupo, sem contorno, e aparece como "peças órfãs" no Estado. É
  raro, e o Refazer da área resolve;
- o letreiro da cópia é o da original (F1.3 e F1.3); renumerar é o 7.10;
- `UFV_RENOMEAR` age só em definições com o nosso prefixo e sufixo
  `$n$`: renomeia se o padrão não existe, senão passa as referências para
  o padrão e apaga a definição. Nunca sozinho: só pelo botão.

### O que a revisão do 7.5 apontou, e o que foi feito

Um bloqueante, corrigido: desfazer a cópia (Ctrl+Z) registrava a mesa
ORIGINAL como removida, porque o undo devolve o XData da cópia ao GUID da
original antes de apagá-la, e o evento de apagar chegava com esse GUID. O
vigia agora só registra remoção de mesa que de fato não tem mais contorno.
Conferido no nível 2 com `_.U` e `_.REDO` depois do COPY: o U desfaz a
cópia e a identidade nova num passo só (a gravação do vigia no fim do
comando entra no grupo de undo do comando), a original fica única e nada
é registrado como removida; o REDO devolve a cópia ainda sem GUID
repetido. Importantes, corrigidos: (1) MIRROR/ROTATE com cópia, e a
colagem de uma mesa recalculada, viravam dezenas de "mesas" de uma peça
porque o agrupamento era só por deslocamento; agora, se nenhum GUID de
peça se repete entre as acrescentadas de uma mesa, é UMA cópia seja qual
for a transformação, e o deslocamento só separa cópias múltiplas (COPY
múltiplo, ARRAY); (2) o vigia varria o desenho inteiro ao fim de qualquer
comando que acrescentasse qualquer coisa (uma linha do usuário); agora,
sem peça nossa tocada, sai antes da varredura, e a busca dos originais
usa o filtro de classe do `LayoutScan`; (3) o caminho "trocar as
referências para o bloco padrão e apagar a definição" do Renomear não
tinha teste; o nível 2 agora desenha de novo depois do -RENAME (o padrão
renasce) e o comando troca 42 referências e apaga a definição; (4) falha
na reidentificação avisava só "não consegui marcar as mesas"; agora diz
que a cópia ficou com a identidade da original e o que fazer. Menores:
`Has` com registro apagado e referências apagadas no Renomear; `COPYMODE`
forçado no script; mensagem "cópia de F1.x suja" em vez de "F1.x suja";
textos "até o 7.5" atualizados. Anotado, não feito: duas cópias múltiplas
no mesmo ponto dividem um GUID de mesa; o nome que o AutoCAD 2026 dá a um
bloco colado de outro desenho com conflito (`$0$`? `A$C$…`?) não foi
visto no CAD — o Renomear só trata `NOME$n$`, e o Renan confere ao colar;
ARRAY associativo e INSERT de bloco com mesa dentro não são cobertos;
`ChangeKind.Appended` no livro do Core virou ramo só de teste (a cópia
é decidida pelo `CopyFixer`).

## 7.6: apagar e recontar

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO`. A metade "apagar" já era do
vigia (7.2): apagar o contorno registra a remoção, apagar peças suja a
mesa, desfazer devolve. A metade nova é o botão **Recontar**
(`UFV_RECONTAR`): conta a usina como ela ESTÁ no desenho, pelo XData, e
não pelo que o motor calculou. `LayoutCensus` (Core, 3 testes) recebe uma
lista de mesas contadas (identidade ou null se órfã, módulos, comprimentos
de pilar, pilares sem comprimento) e devolve mesas, órfãs, sujas, que não
cabem, módulos, kWp, pilares com faixa e média, em linhas prontas. O
comando lista as removidas registradas desde a última recontagem e limpa o
registro. Nível 2 (`ufv-recontar.scr`): apaga o contorno de uma mesa com
ERASE e reconta: os totais batem com o que o LISP conta pelo XData (uma
mesa a menos, mesma quantidade de módulos e pilares, uma órfã), a removida
é listada e o `UFV_ESTADO` depois diz 0.

### Decisões que são minhas

- a potência é a de cada mesa (gravada na identidade dela desde o 7.6);
  mesa desenhada antes usa a do perfil atual, com aviso;
- "recontar" não renumera (7.10) nem apaga peças órfãs: só conta e diz;
- o registro de removidas é consumido pelo Recontar (é o que "refaz as
  listas" significa aqui); o `UFV_ESTADO` continua mostrando as removidas
  enquanto ninguém recontar.

### O que a revisão do 7.6 apontou, e o que foi feito

Nenhum bloqueante. Quatro importantes, corrigidos: (1) o comando tinha
`NoUndoMarker` e grava (limpa o registro de removidas): um U depois dele
desfaria o comando anterior do usuário junto com a limpeza; a marca saiu;
(2) o kWp usava a potência do módulo do PERFIL ATUAL para todo módulo do
desenho; agora a potência do módulo vai gravada na identidade da mesa
(`TableIdentity.ModulePowerWatts`, XData da mesa na versão 3, lendo a 2 e
a 1), a contagem soma mesa a mesa, e a mesa sem potência gravada
(desenhada antes) usa a do perfil atual com um aviso dizendo quantas; (3)
mesa duplicada (dois contornos, um GUID) era contada como uma mesa com o
dobro de peças; agora conta pelos contornos e o relatório avisa; (4) o
nível 2 só apagava o contorno, e módulos e pilares não mudavam; agora
apaga também uma mesa inteira e exige duas mesas, 28 módulos e 7 pilares a
menos, e duas removidas listadas. Menores corrigidos: kWp com o mesmo
formato do relatório da usina; comprimento não finito conta como "sem
comprimento" em vez de sumir; registro de removidas ilegível é descartado
com aviso, em vez de avisar para sempre; `Lines()` chamado uma vez; a
linha das órfãs diz quantos módulos delas entraram no total.

## 7.7: validação

Feito em 26/09/2026, `AGUARDANDO VALIDAÇÃO`. `LayoutValidation` (Core, 3
testes) recebe os achados e monta o relatório, uma linha por tipo, cada
uma com o que fazer. `UFV_VALIDAR` (botão Validar) coleta: áreas e
alinhamentos registrados cujo handle não aponta mais para uma entidade
com a identidade registrada; mesas sujas (é assim que "algo mudou de
posição" chega: pelo vigia); mesas com mais de um contorno na mesma
identidade; peças (pilar, módulo, face, nota) com GUID repetido; mesas só
com peças; removidas não recontadas; e o carimbo da superfície
(`TerrenoEnvelhecido`, que já existia). `ValidacaoAoAbrir` roda a mesma
conferência em `DocumentCreated`, só em desenho que tem área, alinhamento
ou registro de removidas nosso, e escreve "AO ABRIR …". Nível 2
(`ufv-validar.scr`): valida limpo, apaga a polilinha da área, suja uma
mesa, apaga um contorno e copia uma mesa; a segunda validação diz 1 área
faltando, 2 sujas (a sujada e a cópia), 0 duplicadas, 0 peças repetidas, 1
órfã, 1 removida, terreno ok.

### Decisões que são minhas

- "algo mudou de posição" tem duas fontes: o estado sujo do vigia e a
  âncora gravada na identidade (o primeiro vértice do contorno): mesa
  limpa fora da âncora foi movida sem o plugin ver;
- a validação nunca conserta: só diz e aponta o comando certo;
- ao abrir, desenho sem nada nosso não recebe linha nenhuma.

### O que a revisão do 7.7 apontou, e o que foi feito

Nenhum bloqueante. Três importantes, corrigidos: (1) "algo mudou de
posição" era só o estado sujo do vigia: uma mesa movida com o plugin
descarregado (ou noutra máquina) passava como limpa. Agora a identidade
da mesa guarda ONDE ela foi desenhada (`TableIdentity.Anchor`, o primeiro
vértice do contorno; XData da mesa na versão 4, lendo 3, 2 e 1), o
`LayoutDrawer` e o Recalcular a gravam, e a validação compara com o
vértice atual: mesa limpa fora do lugar sai como "movida sem o vigia
ver". O nível 2 move uma mesa e apaga a marca de suja no XData com
`entmod`, simulando o plugin descarregado, e a validação a pega; (2) a
validação "ao abrir" não cobria o desenho já aberto na hora do
carregamento (NETLOAD, Core Console com /i): `Instalar` passa a validar os
documentos abertos, como o vigia faz; o nível 2 confere que num desenho
sem nada nosso ela fica calada; (3) área ou alinhamento copiado (mesma
identidade em duas polilinhas) não era detectado, porque o registro guarda
um handle por GUID; agora as varreduras do Reindexar são reaproveitadas
e o relatório conta identidades duplicadas de área e de alinhamento
(nível 2 copia o alinhamento). Menores corrigidos: handle que sumiu
(objeto apagado e desenho salvo) não vira mais exceção no diagnóstico
(`TryGetObjectId`, num só lugar, `TerrenoEnvelhecido.AcharPorHandle`);
`CopyFixer.PieceId` reaproveitado; o filtro do "ao abrir" olha também o
carimbo do terreno; a frase da linha das sujas diz "marcada suja pelo
vigia". Anotado, não feito: a validação faz duas passadas pelas peças
(`LayoutScan` e a busca de GUID repetido); `FingerprintReader` dentro do
`DocumentCreated` pode reconstruir uma superfície marcada para
reconstruir, e se travar ao abrir o remédio é adiar para `Idle`.

