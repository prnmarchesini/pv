# Etapas 37 a 39: estrutura fixa bifila

## Etapa 37: o modelo (Core, sem CAD)

### 37.1 A bifila no perfil da mesa

`BifilaFrame`, a parte bifila do `TableFrame` (propriedade `Bifila`, null na
monoposte): pilar traseiro na tesoura (T2t), topo do pilar abaixo da face dos
módulos, as duas mãos francesas (comprimento e ponto no pilar, abaixo do
topo), as terças (afastamento da borda da fileira de módulos, barra máxima),
os tirantes (linha, vãos travados, dois pontos abaixo do topo) e as seções
(viga, terça, mão francesa, tirante). Validação com teto contra erro de escala
e T2 < T2t ≤ T1. Arquivo `.ufvmesa.json`: `frame.bifila` só existe na bifila,
que sai na versão 4; perfil sem ela continua saindo na 2 ou na 3, igual a
hoje. Os dois padrões da Arctech (GC800 e GC1000) como perfis prontos.
Fecha automático.

### 37.2 Dois pilares por pórtico

`PillarLine` (Única, Frente, Trás) em `PillarPiece` e `PillarResult`.
`TableGeometry` monta dois pilares por estação, na ordem estação → frente →
trás, com a fileira de cada um; `PillarCalculator` desce o topo do pilar pelo
"topo abaixo da face dos módulos". Números de ouro do modelo de referência
(plano e com declive, GC1000 e GC800). A monoposte sai idêntica: toda a
bateria antiga passa sem mudar.
Fecha automático.

### 37.3 As peças da estrutura

`BifilaStructure`: a partir da mesa assentada e dos pilares calculados, as
peças em 3D no mundo: pilares (do pé enterrado ao topo), vigas (uma por
pórtico, na tesoura), mãos francesas (pelo comprimento, do ponto no pilar até
encostar na viga), terças (ao longo da mesa, por cima das vigas) e tirantes (X
nos vãos escolhidos). Avisos: mão francesa que não alcança a viga, ponto da mão
francesa abaixo do chão. Ouro: mãos francesas com o comprimento da peça, viga
de 4,030, terças do comprimento da mesa.
Fecha automático.

### 37.4 Lista de material

`StructureBill`: por mesa e total, pilares por linha (frente e trás, cada um
com seu comprimento), vigas, mãos francesas por linha, terças (barras e
emendas) e tirantes. Aba "Estrutura" no Excel da quantificação, no padrão do
`QuantityReport`, e a coluna de linha (frente/trás) na aba Pilares quando a
mesa é bifila.
Fecha automático.

## Etapa 38: no Civil 3D

### 38.1 Nova estrutura: Fixa bifila

Configurações › Estruturas › Nova estrutura… ganha a terceira opção, **Fixa
bifila**: abre a janela de Mesa em modo bifila (seção "Bifila" com os campos
do 37.1 e o botão do padrão Arctech GC800/GC1000), o corte da mesa desenha os
dois pilares, a viga e as mãos francesas, a planta cota os pórticos (não os
pilares). Na lista, "(bifila)". A bifila entra na Escolha das estruturas como
qualquer mesa.
**Validação do Renan:** Configurações › Estruturas › Nova estrutura… › Fixa
bifila; conferir o corte (dois pilares, 3,00 m entre eles); Salvar no desenho
e reabrir; ela aparece na Escolha das estruturas.

### 38.2 Desenhar a bifila

O desenho da mesa (Fileira, Usina, Recalcular, Trocar) põe os dois pilares de
cada pórtico (bloco do pilar), as cotas PB/PA uma vez por pórtico e o P3 em
cada pilar; viga, mãos francesas, terças e tirantes em sólidos na camada
`CLIVUS_ESTRUTURA`, no grupo da mesa, com XData `Estrutura` (GUID, mesa, peça,
comprimento). XData do pilar versão 3 (a linha no fim; o pórtico sai da
estação). O
`LayoutScan` reconhece as peças: apagar, copiar, recontar, validar e limpar
tratam delas. Nível 2: a cota de cada peça lida em LISP, entre o fundo dos
pilares e o topo dos módulos.
**Validação do Renan:** processar uma área com a bifila marcada na Escolha
das estruturas, orbitar uma mesa e conferir os dois pilares, a viga, as mãos
francesas, as terças e o X de tirantes; apagar a mesa e ver tudo sumir junto.

### 38.3 As ferramentas que leem pilar

Alturas (PB/PA por pórtico, P3 nos dois pilares), Análises independentes
(PB/PA por pórtico; a contagem não dobra), Pontas à mão (estações das pontas),
Usina mista (enterro T3 de cada tipo, e não só do primeiro), Recalcular,
Trocar e Regerar com a bifila.
**Validação do Renan:** Análises numa área bifila (PB/PA uma vez por pórtico);
Pontas numa mesa bifila; trocar uma monoposte por bifila e voltar.

### 38.4 Lista de material na tela

Exportar Excel com a aba "Estrutura" e a linha (frente/trás) na aba Pilares.
**Validação do Renan:** exportar e conferir com uma mesa desenhada (12
pilares, 6 vigas, 12 mãos francesas, 4 linhas de terça, 4 tirantes na 2P14).

### 38.5 A janela de Mesa em abas (pedido de 30/10/2026)

Renan, olhando a janela de Mesa: *"nessa tela deveria ter a opção de escolher
Biestaca ou mono estaca, e a configuração da estrutura mudaria completamente
[...] uma guia para monoposte e outra para biposte"* e *"Uma guia do chrome
para configuração do perfil, módulo, e aí, eu escolho se é mono ou biposte, e
aí abre a aba certa para a opção escolhida, e aí sim faço a configuração"*.

- Primeira aba, **Perfil e módulo**: perfil salvo e nome, módulo (medidas e
  dados elétricos) e os cartões **Monoposte** e **Biposte**, cada um com o
  desenho do pórtico. O cartão abre a aba do tipo.
- Segunda aba, com o nome do tipo: Mesa e Estrutura e, no biposte, Pórtico,
  Mãos francesas, Terças, Tirantes e Seções das peças; à direita, a planta e
  a vista lateral. No biposte, T1 vira "Viga T1" e T2 vira "Pilar dianteiro
  na viga T2".
- Trocar de tipo guarda o que estava na aba do outro: ir e voltar não perde
  nada. A aba do outro tipo começa na última mesa desse tipo no desenho, ou
  na de referência (Arctech 2P14 GC800 no biposte, o exemplo na monoposte).
- O motivo de a mesa não fechar aparece embaixo, fora das abas.
- Configurações › Nova estrutura volta a ter só Fixa e Carport: o tipo da Fixa
  se escolhe na janela. Na tela, "bifila" vira "biposte" (a palavra do Renan);
  o código e o arquivo seguem com `Bifila`.

**Validação do Renan:** Nova estrutura › Fixa: a janela abre em "Perfil e
módulo"; clicar Biposte abre a aba Biposte com o pórtico; mudar um campo,
voltar para Monoposte e de novo para Biposte: o campo continua mudado.

### 38.6 O PAN enche o módulo e sobe para o servidor (pedido de 30/10/2026)

Renan: *"na hora de puxar o pan, já poderia configurar o modelo, marca,
altura, tudo [...] o pan tem tudo, dimensão marca modelo, e eu poderia já
escolher em cadastrar, e esse arquivo pan já subir no servidor, esse módulo já
ficaria salvo no servidor [...] é usuário subindo pan para gente"*.

- "Carregar .PAN..." enche a seção Módulo inteira: marca, modelo, altura,
  largura, espessura e potência, além dos dados elétricos.
- "Cadastrar módulo..." aceita um .PAN: os campos saem dele, e o arquivo vai
  junto para o serviço de módulos, que guarda o PAN e a parte elétrica.
- O módulo escolhido na lista traz a parte elétrica que o serviço tem.

### 39.2 Tudo funciona com biposte: a prova (pedido de 30/10/2026)

Renan: *"O cliente pode escolher biposte ou monoposte, o resto não muda nada,
TODO o sistema funciona igual, preciso de roteiro de teste provando que
independente da estrutura escolhida TUDO vai funcionar: rota de cabos, vala,
cabos, contagem, grupos, enfim, tudo"*.

- Nível 2 de ponta a ponta: a mesma área, o mesmo terreno e a mesma mesa (2P14
  a 10°), uma vez monoposte e outra biposte, passando por tudo que vem depois
  das mesas. Tudo que não é estrutura tem que sair igual nas duas; o que é
  estrutura, com a diferença esperada (o dobro de pilares e as peças).
- Roteiro de tela com a lista inteira, item por item.

## Etapa 40: mesas conforme os objetos do cliente (pedido de 30/10/2026)

Renan: *"quero ter a opção de escolher os objetos em planta e falar 'esses
objetos são mesas' [...] o cliente quer esta disposição, eu quero seguir [...]
Se tiver divergência ele deve apontar, em teoria todos objetos devem ser
iguais, estruture um processo para isso. Eu vou criar as áreas normalmente
[...] e aí na hora de gerar, ao invés de ter a opção de gerar automático como
já tem, pode ter a opção gerar conforme objetos em campo, aí eu escolho o
objeto da área, e ele segue eles e faz a alocação [...] área por área"*.

Referência: `Panama\Docs\Marcheng\PLAT A_Perfiles.dwg` — 641 polilinhas 3D
fechadas de 4 vértices nas camadas `*CELDAS*`, cada uma a área de módulos de
uma 2P14 (18,502 × 4,788 m em 3D, 10° de inclinação, seguindo a rampa da
fileira), contornos em `_CONTORNO` e o terreno em superfícies TIN.

### 40.1 Reconhecer as mesas do cliente (Core)

Cada objeto (polilinha fechada de 4 vértices, 2D ou 3D) vira uma pegada: o
centro em planta, o eixo da fileira (o lado maior), o lado da ponta baixa
(pelo Z, quando há), o tamanho em 3D e a inclinação. As pegadas se agrupam em
fileiras (mesmo eixo, uma atrás da outra). As divergências saem com o motivo:
tamanho diferente da maioria, rumo diferente, inclinação diferente, objeto
fora da área, objeto em cima de outro, e a mesa do Clivus que não bate com o
objeto.

### 40.2 Gerar conforme objetos em campo (tela)

Na geração da área, ao lado do automático: "Conforme objetos em campo". O
usuário escolhe os objetos (ou a camada); o Clivus mostra o resumo (quantos,
o tamanho da maioria, as divergências) e põe uma mesa em cada objeto, sentada
no terreno pelo motor, com tudo o que o automático grava. As divergências
ficam marcadas no desenho, numa camada própria, e listadas.

## Etapa 39: o resto do Clivus

### 39.1 3D e TermoVision

O 3D do Clivus (página e servidor) mostra as peças da bifila (como as do
carport). O TermoVision recebe a bifila como "fixa", sem mudança de contrato.
**Validação do Renan:** Ver 3D numa área bifila.
