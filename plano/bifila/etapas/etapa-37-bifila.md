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

## Etapa 39: o resto do Clivus

### 39.1 3D e TermoVision

O 3D do Clivus (página e servidor) mostra as peças da bifila (como as do
carport). O TermoVision recebe a bifila como "fixa", sem mudança de contrato.
**Validação do Renan:** Ver 3D numa área bifila.
