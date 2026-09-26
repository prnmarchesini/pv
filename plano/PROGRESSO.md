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
| 5.1 | Distribuição em planta | VALIDADO (automático) | `RowDistributor` no Core, `Polygons` no Geo; só modelo, fechado em 26/09/2026. A fileira segue a linha de alinhamento: **Renan confirma no 5.8** |
| 5.2 | Amostragem | VALIDADO (automático) | `TablePlacement` e `TerrainSampler` no Core; a ponta baixa é amostrada na aresta inteira (`Tin.TryGetMaxZAlong`); fechado em 26/09/2026 |
| 5.3 | Cotas viáveis por mesa | VALIDADO (automático) | `ViableElevations` no Core; grade de 1 cm, intervalos por varredura; fechado em 26/09/2026 |
| 5.4 | Alinhamento na fileira | VALIDADO (automático) | `RowSolver` no Core: programação dinâmica, não iterativo (divergência do plano, registrada); fechado em 26/09/2026. **Renan confirma no 5.8** o "degrau mínimo = 0 ou ≥ mínimo" e a ausência do campo de iterações |
| 5.5 | Pilares | PENDENTE | |
| 5.6 | Resultado das análises | PENDENTE | |
| 5.7 | Desenho | PENDENTE | |
| 5.8 | Uma fileira no CAD | PENDENTE | |
| 5.9 | Área inteira | PENDENTE | |

(As linhas das etapas seguintes são acrescentadas ao iniciar cada etapa, copiando os passos do arquivo dela.)

## POR ONDE CONTINUAR

Atualizado em 26/09/2026, depois de fechar o 4.3.

**Regra nova, 25/09/2026: o Renan só valida o que se vê no Civil 3D.**
Arquivos, acervo, manifesto e valores padrão são do Claude Code. Pedido a
ele só em forma de roteiro de tela: comando, o que clicar, o que deve aparecer.

Esta seção existe porque o resto do arquivo é diário de decisões, e diário não
responde "e agora?". Ela fica no topo de propósito. **Quem retomar o trabalho
lê isto primeiro, e depois `CLAUDE.md`.**

### Estado

Etapas 0, 1, 2 e 3 **validadas pelo Renan**. Etapa 4: 4.1 e 4.3 fechados
automaticamente (só modelo), 4.2 validado pelo Renan em 26/09/2026. Placar na
última execução:

```
Etapa 0   45/45    OK
Etapa 1   126/126  OK
Etapa 2   24/24    OK
Etapa 3   335/335  OK
Etapa 4   201/201  OK
Nivel 2   8/8      OK
Acervo             OK
```

### O próximo passo é o 4.4

**Modal**: tela única com a configuração do sistema (4.1) e as regras de
análise (4.3), graus e centímetros ligados onde couber. Validação do Renan:
configura um projeto real e confere que salvar e reabrir preserva tudo.

O que a tela precisa saber, já decidido:

- **cores**: cada análise tem paleta de seleção; padrão vermelho abaixo, azul
  acima (Renan, 25/09/2026). A borda tem uma cor só. Pilar e declividade não
  têm mínimo (`AnalysisRules.HasMinimum`), então a cor "abaixo" some nelas;
- **o limite de pilar da análise se chama `PaintPillarsLongerThan`** e nasce
  vazio; não é teto, é cor;
- **gravar e ler**: `RgbColor.ToHex`/`TryParseHex` e `LayerName.WhyInvalid`
  já existem para o arquivo e para a validação na tela;
- é onde o Renan vê os cinco padrões que são meus (pitch, enterro máximo,
  degrau, espaçamento, tolerância) e troca o que estiver errado.

### O que está travado no Renan

1. **Instalar o bundle novo e testar o 4.4**: o Civil 3D estava aberto quando
   tentei instalar (o instalador recusa, código 5). Fechar o Civil 3D e rodar
   `.\tools\instalar.ps1` (o bundle já está montado em `artefatos\UFV.bundle`,
   em Release). Depois: botão "Configuração" na aba UFV, ou `UFV_CONFIG`;
   trocar alguns números, "Salvar no desenho", salvar o DWG, fechar, reabrir,
   abrir a tela de novo e conferir que está tudo como deixou. `UFV_CONFIG_STATUS`
   mostra o gravado na linha de comando.
2. ~~Testar o 4.2 no CAD~~ Feito em 26/09/2026: "deu certo o alinhamento".
2. ~~`terreno-esperado.psd1`~~ Resolvido em 25/09/2026: o Renan decidiu que
   só valida o que se vê no CAD, e o arquivo foi congelado em
   `tests/acervo/etapa-1/` pelo Claude Code, com o Porto Feliz marcado como
   não conferido (trava regressão, não prova acerto).
3. **Conferir a mesa do 3.7 contra um projeto de fabricante.** Ele aprovou a
   etapa 3 sem relatar essa conferência, que é o que o plano pede como
   validação do 3.7 — é o único jeito de saber se o motor acerta o número, e
   não só a forma.

### Perguntas abertas, nenhuma delas bloqueante

- **Cinco valores da configuração são meus, não dele** (pitch 6,0 m; enterro
  máximo 2,00 m; degrau 0 a 0,50 m; espaçamento que quebra fileira 0,50 m; e a
  tolerância de invasão, que ele já respondeu que é contagem e está em zero).
  Ele não vai conferir tabela de número. A hora de ele ver isso é o 4.4, na
  tela de configuração: se um padrão estiver errado, ele troca lá;
- **relação entre degrau, espaçamento e pitch**: hoje os três são validados
  isoladamente e nada confere um contra o outro. Se existe relação real, ela
  não está escrita nem como comentário.

### Dívidas técnicas que valem lembrar

- `AlignmentStore`, `AlignmentXData` e `AlignmentScan` não têm teste de nível 1
  (são do plugin) nem de nível 2 (não há `.scr` de alinhamento);
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
- **o giro longitudinal não reamostra o terreno** (anotação do 5.2): a
  amostra sob a ponta baixa é da mesa com cota zero e sem giro; o giro
  move o pé dos pilares em planta por centímetros, e a ponta baixa não
  muda de lugar em planta (gira em torno de si). Para a ponta baixa a
  aproximação é exata; para os pilares, a decisão fica no 5.5;
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

