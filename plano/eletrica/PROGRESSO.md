# Progresso da parte elétrica

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO.

## Etapa 11: Corrente contínua (strings)
- 11.1 Modelo de tipo de string e biblioteca: AGUARDANDO VALIDAÇÃO
- 11.2 Seleção de mesas e representação cartesiana dos módulos: AGUARDANDO VALIDAÇÃO
- 11.3 Marcar polaridade e traçado convencional: AGUARDANDO VALIDAÇÃO
- 11.4 Leapfrog e traçado livre por cliques: AGUARDANDO VALIDAÇÃO
- 11.5 Editar, apagar, clonar, espelhar modelo: AGUARDANDO VALIDAÇÃO
- 11.6 Aba Gerar: casar config com grupos de mesas: AGUARDANDO VALIDAÇÃO
- 11.7 Desenhar traçado respeitando a declividade do módulo: AGUARDANDO VALIDAÇÃO
- 11.8 Avisar mesa que não casou com nenhuma config: AGUARDANDO VALIDAÇÃO

## Etapa 12: Subestação
- 12.1 Cadastro de UC, modo compartilhado (C1, C2) e vínculo com trafo: AGUARDANDO VALIDAÇÃO
- 12.2 Modo unitário (bloquinhos independentes): AGUARDANDO VALIDAÇÃO
- 12.3 Dimensão, tag e alocação em campo: AGUARDANDO VALIDAÇÃO

## Etapa 13: Transformador
- 13.1 Cadastro genérico do trafo e tabela de padrões: AGUARDANDO VALIDAÇÃO
- 13.2 Dimensão, apelido como tag e alocação em campo: AGUARDANDO VALIDAÇÃO

## Etapa 14: Inversor
- 14.1 Modelo de inversor (MPPT, entradas, total): AGUARDANDO VALIDAÇÃO
- 14.2 Lista de inversores por modelo e contagem: AGUARDANDO VALIDAÇÃO
- 14.3 Alocação manual de strings em campo (trava, contagem ao vivo, Ctrl desseleciona, destaque): AGUARDANDO VALIDAÇÃO
- 14.4 Aviso de excesso de capacidade: AGUARDANDO VALIDAÇÃO
- 14.5 Editar, selecionar todas, apagar todas (solta vínculo, não apaga geometria): AGUARDANDO VALIDAÇÃO
- 14.6 Dimensão, tag e alocação do inversor em campo: AGUARDANDO VALIDAÇÃO
- 14.7 Agrupamento de inversores por trafo (skid): AGUARDANDO VALIDAÇÃO

## Etapa 15: Numeração das strings
- 15.1 Composição da tag em três pedaços (trafo, inversor, string): AGUARDANDO VALIDAÇÃO
- 15.2 Configuração da varredura (sentido) e blocos: AGUARDANDO VALIDAÇÃO
- 15.3 Ordem dos blocos na lista: AGUARDANDO VALIDAÇÃO
- 15.4 Botão Gerar: varre e cria tags no desenho: AGUARDANDO VALIDAÇÃO
- 15.5 Edição granular (apagar tudo, por inversor, refazer inversor, regerar bloco): AGUARDANDO VALIDAÇÃO

- Aba Numeração, 05/10/2026 (Renan: "o botão selecionar mesas não deixa
  selecionar, e cada bloco precisa de dois sentidos..."): AGUARDANDO VALIDAÇÃO.
  - O Selecionar pedia a seleção direto do clique da janela solta, fora de
    comando: o AutoCAD devolvia a pergunta na hora, sem deixar selecionar, e
    o código saía calado. Agora o botão esconde a janela e manda
    CLIVUS_NUMERACAO_MESAS (seleção dentro de comando, mesas do bloco
    destacadas); a janela volta com a frase do resultado.
  - Dois sentidos (o que avança e o da faixa) na usina e em cada bloco, como a
    atribuição da aba Inversor; `NUMERACAO_VARREDURA` formato 2 (o 1 é lido
    com o sentido na faixa de antes: a ordem de desenho antigo não muda).
  - Cada bloco numa linha: nome, "N mesa(s), M string(s)", os dois sentidos e
    Selecionar, Mostrar, ↑, ↓, ✎ (renomear), Apagar; resumo embaixo (em
    blocos, fora de bloco). Assumido: Selecionar substitui as mesas do bloco
    (como antes); string conta no bloco da mesa do primeiro módulo (a mesma
    regra do Regerar bloco); "Regerar bloco escolhido" usa a linha clicada.

## Etapa 16: Resumo do sistema
- 16.1 Menu de resumo completo consolidado pela cadeia de vínculo: AGUARDANDO VALIDAÇÃO

## Rodada de 04/10/2026 (Renan: "pode fazer tudo até o final depois eu volto revisando")

Tudo feito de uma vez: 11.1 por mim; 11.2–11.8 (agente A), 12–14 (agente B),
15–16 (agente C), cada etapa com revisão por subagente e o que ele apontou
corrigido; juntado e testado inteiro (bateria verde, nível 2 com todos os
casos elétricos). Decisões tomadas sem perguntar estão em cada item.

### Roteiro de tela (na ordem do trabalho)

**Strings (botão String)**
1. Configuração > Adicionar tipo de string: selecione uma mesa 2V junto com
   uma curva de nível, Enter: o tipo entra na lista e o cartesiano mostra as
   duas fileiras (a curva foi ignorada). Repita com duas mesas de 14 vizinhas:
   as duas aparecem com o vão entre elas. (11.1, 11.2)
2. No cartesiano: clique no módulo da coluna 1 de baixo e no da última coluna,
   Concluir string: + na coluna 1, − na última. (11.3)
3. Leapfrog + Fileira inteira, clique na coluna 1: + e − em módulos vizinhos.
   Convencional em U (1→7 embaixo, sobe, volta à 1): − ao lado do +. (11.4)
4. Clonar, Espelhar o clone: + e − trocaram; o original igual. (11.5)
5. Aba Gerar, numa área com mesas de 28 e de 14: cada tipo só nos grupos
   iguais; orbite no 3D: o traçado acompanha os módulos a 5 cm; gerar de novo
   não duplica. Só com o tipo de 28: aviso nominal de cada mesa de 14, que fica
   selecionada. (11.6–11.8)

**Configuração elétrica (botão Configuração elétrica)**
6. Transformador: Novo trafo e Novo do padrão (2.500 kVA, 800 V / 13.800 V);
   editar e salvar. Alocar em campo dois trafos: caixa 0,80 m acima do
   terreno, tag de cima; renomear a tag muda o letreiro. (13.1, 13.2)
7. Subestação: C1 e C2 compartilhadas, C1 com T1 e T2 (em C2, T1 aparece
   travado "(em C1)"); duas unitárias com trafos diferentes; alocar em campo.
   (12.1–12.3)
8. Inversor: Novo modelo 1×2 e "Huawei 250" 5×4 (total 20); criar 4 de um e 2
   do outro (Inversor 1 a 6, 0 strings). "+" num inversor: só strings entram,
   contagem ao vivo, Shift tira (Ctrl não), Enter volta com a contagem. Num
   modelo 2×2 aloque 6: aviso vermelho (avisa, não bloqueia). Soltar todas: as
   strings continuam no desenho, livres; string de outro inversor é recusada.
   Alocar dois inversores em campo; Skid: 4 inversores no T1, 2 no T2.
   (14.1–14.7)
9. Numeração: separador Ponto (T1.I1.S1), depois colado sem trafo (1S1);
   dois blocos com sentidos diferentes; subir/descer bloco muda a sequência;
   Gerar tags; Regerar bloco só mexe nele; Apagar/Refazer inversor. (15.1–15.5)

**Resumo elétrico**
10. Numa usina montada: subestações → trafos → inversores → strings → módulos
    e kWp; conferir contra o desenho; pendências em vermelho. (16.1)

### Decisões que o Renan confirma
- Excesso de capacidade do inversor avisa e não bloqueia.
- Desselecionar na alocação é Shift (o do AutoCAD); Ctrl não foi feito.
- Número do trafo e do inversor na tag = a posição na lista do cadastro;
  inversor sem trafo sai sem o pedaço do trafo (ex. I4.S1), avisado.
- String: pelo menos 2 módulos; tipo de mais mesas tem prioridade no gerar;
  mesas "vizinhas" = mesma fileira, letreiros seguidos.
- Unitárias numeradas U1, U2 (separadas das C1, C2).
- Trafos padrão: 800 V → 13,8 kV (1250/2500/3150 kVA), 800 V → 34,5 kV
  (2500/3150 kVA), 380 V → 13,8 kV (500 kVA).

## Aba Inversor em tabela (05/10/2026): AGUARDANDO VALIDAÇÃO

Pedidos do Renan: "eu queria uma forma mais fácil de dizer 'esse inversor,
esse e esse é deste trafo'"; "falta o campo para inserir a potência"; "falta
uma coluna de kWp e quantidade de strings".

- A lista de inversores virou tabela: cor, Inversor, Modelo, Trafo (caixa que
  grava na hora), Strings (n/entradas), kWp (a mesma conta do Resumo
  elétrico), kW (do modelo), CC/CA, e os botões +, Selecionar, Soltar strings;
  total no rodapé. Várias linhas com Ctrl/Shift e "Pôr no trafo [T1]".
- O skid é o próprio vínculo inversor → trafo (`Inverter.Transformer`); o
  registro `SKIDS` guarda só o nome. A linha do skid saiu da vista: ficou num
  quadro fechado "Agrupar em campo (skid)", com a explicação. "Tirar do
  skid" saiu (é o "sem trafo" da linha).
- Modelo com "Potência (kW)" (CA nominal), `INVERSOR_MODELOS` formato 3; o 2
  e o 1 lidos com potência 0.
- Assumido: na tabela, inversor de outro trafo muda sem trava (escolha
  explícita); na seleção em campo a trava continua. Janela 1180 px de largura
  (era 980) para caber a tabela sem rolagem horizontal.
- Revisão por subagente: potência pequena gravada sem notação científica;
  CC/CA do total só com os inversores que têm kW; a caixa Trafo grava ao
  fechar a lista (a seta do teclado não grava sozinha) e volta ao gravado se
  falhar; com várias linhas escolhidas, Salvar/Alocar/Apagar inversor
  recusam e avisam.

Roteiro de tela:
1. Configuração elétrica > Inversor: escolha um modelo, digite 250 em
   Potência (kW), Salvar modelo: a lista mostra "— 250 kW" e a coluna kW
   dos inversores dele mostra 250.
2. Na coluna Trafo de um inversor, escolha T1: grava sem botão (recado
   verde no rodapé). Escolha "sem trafo": solta.
3. Ctrl+clique em três linhas, escolha T2 ao lado de "Pôr no trafo" e
   clique: as três mostram T2, inclusive a que estava em outro trafo.
4. Aloque strings num inversor ("+"): Strings mostra n/20, kWp aparece e
   bate com o Resumo elétrico; CC/CA = kWp ÷ kW; o total soma as linhas.
5. Abra "Agrupar em campo (skid)": a explicação e o agrupar pelo desenho,
   como antes.

## Aba Inversor: tudo na linha (05/10/2026): AGUARDANDO VALIDAÇÃO

Pedido do Renan sobre o bloco embaixo da tabela ("Pôr no trafo",
"Escolhido: ... Salvar inversor / Alocar em campo / Apagar inversor" e o
quadro "Agrupar em campo (skid)"): "essa parte não estou entendendo nada,
melhore MUITO ela". O bloco saiu.

- Na linha: a cor é o quadradinho (clique abre a paleta e "Mais cores...");
  o nome é editável na célula (Enter ou sair grava, Esc desfaz; vazio,
  repetido ou GUID recusados com o porquê); o Modelo é uma caixa que grava ao
  escolher e recusa o modelo com menos entradas que as strings já alocadas
  (a caixa volta ao gravado); o Trafo como antes.
- Ações com nome e dica: "+ Strings", "Ver" e "⋯" (Soltar strings, Pôr em
  campo, ou Mover em campo se o retângulo já está no desenho, e Apagar
  inversor…, com confirmação).
- Duas ou mais linhas escolhidas (Ctrl/Shift): aparece embaixo da tabela a
  barra "3 inversores escolhidos: Trafo [T1] [Aplicar] [Apagar os 3]
  [Cancelar seleção]"; com 0 ou 1, some. Escolhida a barra (e não "mudar a
  caixa de uma escolhida muda todas") porque é explícita: mudar o trafo de
  uma linha muda só ela, sempre. Embaixo, e não em cima, para não empurrar
  as linhas que estão sendo clicadas.
- "Trafo pelo desenho…" (ao lado de Criar): a lista dos trafos; escolher
  um esconde a janela e roda o CLIVUS_ELETRICA_SKID para clicar os
  retângulos dos inversores. O nome do skid saiu da tela (fica o que já tem,
  ou "Skid T1"; o registro SKIDS e o comando não mudaram).
- Topo: "Criar [1] inversor(es) do modelo [..] [Criar]" e "Distribuir
  strings livres: [sentido] e na faixa [..] [Distribuir] [Soltar todas da
  usina]"; uma linha de ajuda cinza em cima da tabela.
- Core: `RenameInverter`, `ChangeInverterModel`, `RemoveInverters` (testes
  em `InverterTableTests`); nível 2 novo `eletrica-inversor-linha.ps1`
  (modelo recusado pelas strings, nome recusado/gravado, apagar várias).
- Assumido: o apagar de várias pede uma confirmação só, com os nomes; o
  "Apagar" da linha fica no "⋯" (não à vista) para não ser clicado sem
  querer; antes a troca de modelo para um menor só avisava em vermelho,
  agora é recusada (pedido de 05/10/2026).

Roteiro de tela:
1. Configuração elétrica > Inversor: clique no nome de um inversor, digite
   outro e Enter: o recado verde diz "Inversor renomeado". Digite o nome de
   outro inversor: recusado em vermelho e o nome volta. Esc desfaz.
2. Num inversor com 5 strings, escolha na caixa Modelo um modelo de 4
   entradas: recusado ("solte 1 string(s) antes...") e a caixa volta.
3. Clique no quadradinho da cor: a paleta abre; escolha outra: as strings
   dele mudam de cor no desenho.
4. Ctrl+clique em três linhas (na parte das Strings/kWp): a barra aparece
   embaixo; Trafo T2 > Aplicar: as três vão para o T2. Cancelar seleção: a
   barra some.
5. "⋯" de uma linha: Pôr em campo (clique no desenho) e, de volta, o "⋯"
   mostra "Mover em campo". "Apagar inversor…" pergunta antes.
6. "Trafo pelo desenho…" > T1: a janela some; clique nos retângulos dos
   inversores, Enter: eles mostram T1 na coluna Trafo.
## Numeração: tag livre, fundo/moldura, gerar por bloco (05/10/2026): AGUARDANDO VALIDAÇÃO

Pedidos do Renan na aba Numeração:
- Composição da tag num modelo de texto livre (como o PVcase): `{T}`, `{I}`,
  `{S}`, com zeros ({I:00}); botões "+ Trafo", "+ Inversor", "+ String" põem
  o campo no cursor; exemplo ao vivo com o caso "inversor sem trafo" (o
  pedaço do trafo some). `NUMERACAO` formato 2; o 1 lido e convertido no
  modelo que dá as mesmas tags (teste de nível 1 com todas as combinações e
  de nível 2 com um registro gravado no formato 1).
- Fundo (máscara na cor da tela) e Moldura nas tags: propriedades do próprio
  MText, sem entidade a mais; Edição › Apagar continua achando (o caso
  `clivus-apagar` agora gera as tags com fundo e moldura).
- Seção Gerar em três linhas iguais: Usina inteira / Bloco ▼ / Inversor ▼,
  cada uma com Gerar e Apagar. "Apagar do bloco" é novo. Sem blocos (ou sem
  inversores) a linha fica desligada com a dica do porquê.
- Assumido: com mais de um inversor, {I} é obrigatório; dois campos colados
  só com zeros no primeiro ({I:00}{S:00}); o separador que sobra no começo
  quando o trafo some é a pontuação (não letra nem algarismo).

Roteiro de tela:
1. Configuração elétrica > Numeração: a caixa Modelo mostra T{T}.I{I}.S{S}
   (ou o equivalente da composição antiga do desenho).
2. Apague tudo, clique "+ Trafo", digite "-INV", "+ Inversor", "S",
   "+ String": o exemplo mostra T1-INV1S1 ... e "inversor sem trafo: INV3S1".
   Troque {I} por {I:00}: INV01. Apague o {S}: o exemplo fica vermelho.
3. Marque Fundo e Moldura e Salvar: as tags já desenhadas ganham o fundo e o
   quadro na hora (texto igual). Gerar da usina: tags no modelo novo.
4. Bloco ▼ Bloco 1 > Apagar: só as tags das mesas dele somem; Gerar: voltam.
   Inversor ▼ > Apagar/Gerar: só as dele.
5. Num desenho sem blocos, a linha Bloco fica cinza com a dica "crie um
   bloco acima".

## Inversor: botões na linha, soltar apaga tags, apagar todos, ordem por arrastar e ordenar (10/10/2026): AGUARDANDO VALIDAÇÃO

Pedidos do Renan na aba Inversor da Configuração elétrica:
- O "⋯" saiu da linha. As ações dele ficam à vista, do tamanho do "+ Strings"
  e do "Ver": **Soltar**, **Pôr em campo** (ou **Mover**, se já está em campo)
  e **Apagar** (com a mesma confirmação). Dicas iguais às de antes, com a
  frase das tags acrescentada.
- Soltar as strings apaga as tags de numeração delas: o Soltar da linha, o
  "Soltar todas da usina" e o apagar inversor (da linha, das escolhidas e o
  novo "Apagar todos"). Usa a mesma rotina do Apagar da aba Numeração
  (`NumeracaoDesenho.Aplicar` com a tag vazia), na mesma transação que solta
  o vínculo. As strings ficam no desenho, livres; o recado diz quantas tags
  saíram. A camada e o estilo da tag só são criados quando há texto a
  desenhar (só apagar não cria nada).
- "Apagar todos" ao lado de "Apagar os escolhidos": pergunta com quantos são
  e diz que as strings ficam livres; mesma rotina do apagar.
- A ordem da lista é a ordem do cadastro de inversores gravada no desenho.
  Ela já valia para a tabela, para o Distribuir (enche os inversores nessa
  ordem) e para o número {I} da tag (a posição no cadastro). A ordem nova vale
  para os três. No Core: `ElectricalSetup.MoveInverter` (leva o inversor para
  o lugar de outro) e `SortInverters` (por Nome, por Trafo, por Trafo e Nome),
  com ordem natural (`NaturalStringComparer`: Inversor 2 antes de Inversor
  10; T1, T2, ..., T10).
- Na tela: alça "⠿" à esquerda de cada linha para arrastar (um traço azul
  mostra onde cai; perto da borda a lista rola); e no quadro "Inversores da
  usina", na mesma linha do Criar: "Ordenar: [por Nome | por Trafo | por
  Trafo e Nome] [Ordenar]". Grava na hora e redesenha a lista; a escolha
  das linhas e as caixas do/ao seguem a ordem nova.
- Testes: nível 1 `InverterOrderTests` (ordem natural, o caso Inversor 21
  renomeado para Inversor 1, arrastar subindo e descendo, por trafo com T10
  cadastrado antes do T2, trafo sumido, o Distribuir e o {I} da tag na ordem
  nova, hífen comparado igual em qualquer cultura); nível 2
  `clivus-eletrica-inversor-ordem` (Soltar da linha leva 5 tags, Soltar
  todas da usina leva o resto, a ordem gravada lida de volta depois de cada
  ordenar e arrastar, Apagar todos leva as 3 tags).

Assumido:
- Arrastar leva só a linha pega pela alça, mesmo com várias escolhidas.
  Soltar sobre uma linha põe o inversor no lugar dela: descendo fica depois
  dela, subindo fica antes. O traço mostra qual.
- Por Trafo: o apelido do trafo em ordem natural; inversor de trafo que
  sumiu do cadastro vem depois dos trafos e antes dos sem trafo; dentro do
  mesmo trafo fica a ordem que já tinham (por Trafo e Nome ordena pelo nome).
- Mudar a ordem muda o {I} da tag na próxima geração; as tags já desenhadas
  não mudam sozinhas (o recado lembra de gerar de novo).
- A ordem padrão da caixa é "por Trafo e Nome".

Roteiro de tela:
1. Configuração elétrica > Inversor: cada linha tem "⠿" à esquerda e, no
   fim, + Strings, Ver, Soltar, Pôr em campo (ou Mover) e Apagar; o "⋯"
   sumiu.
2. Numeração > Gerar da usina. Volte ao Inversor e clique Soltar num
   inversor com strings: as tags dele somem do desenho, as strings ficam
   (na cor da camada); o recado diz "N string(s) soltas ... N tag(s)
   apagada(s)". Depois "Soltar todas da usina": as outras tags somem.
3. Crie um inversor (sai "Inversor 21", no fim), renomeie para
   "Inversor 1". Pegue a alça "⠿" dele e arraste até a primeira linha: o
   traço azul aparece em cima dela; solte: ele vira o primeiro.
4. Ordenar: "por Trafo e Nome" > Ordenar: os do T1 pelo nome, depois os do
   T2..., os sem trafo no fim. Teste também "por Nome" e "por Trafo".
5. Escolha duas linhas com Ctrl e troque o trafo de uma: as duas mudam; o
   "do ... ao ..." lista os nomes na ordem nova.
6. "Apagar todos": a pergunta diz quantos são; Sim: a tabela fica vazia, as
   strings livres e as tags delas somem.

## Observações
- Mesa recalculada troca os GUIDs dos módulos: as strings dela ficam soltas e
  regerar não as reconhece (desenha as novas sem apagar as velhas). Precisa de
  uma regra (reatar pela coluna/fileira, ou apagar as órfãs) — próximo passo.
