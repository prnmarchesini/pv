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
| 3.1 | Módulo e biblioteca | AGUARDANDO VALIDAÇÃO | Risen RSM132-8-720BHDG, 2384×1303×33 mm, do datasheet |
| 3.2 | Comprimento da mesa | AGUARDANDO VALIDAÇÃO | 2V, 28 módulos: 18,702 m de comprimento e 4,788 m na inclinação |
| 3.3 | Tabela de pilares | AGUARDANDO VALIDAÇÃO | 18,702 m com alvo de 3 m dá 6 vãos de 3,117 m, 7 pilares |
| 3.4 | Geometria local da mesa | AGUARDANDO VALIDAÇÃO | Matriz única + primeiro verificador de regra sagrada (RigidTable) |
| 3.5 | Fórmula da altura livre | AGUARDANDO VALIDAÇÃO | Exemplo do plano fecha; verificador da regra sagrada 1 |
| 3.6 | Perfil nomeado | AGUARDANDO VALIDAÇÃO | JSON com versão de formato; ida e volta exata em qualquer ângulo |
| 3.7 | Modal | AGUARDANDO VALIDAÇÃO | UFV_MESA: campos, comprimento ao vivo e planta baixa com as sobras |

(As linhas das etapas seguintes são acrescentadas ao iniciar cada etapa, copiando os passos do arquivo dela.)

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
