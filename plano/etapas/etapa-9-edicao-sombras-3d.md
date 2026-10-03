# Etapa 9: Trocar mesa, regerar fileira, objetos de sombra, sombras e 3D no navegador

Origem: pedidos do Renan em 03/10/2026, na tela, durante a rodada da etapa 8.
Ordem: primeiro a edição de mesas (mais perto do que já existe), depois os
objetos e as sombras (o modelo do sol e da sombra antes da tela), por último
o 3D no navegador, que só mostra o que já existe.

Regras que valem para a etapa inteira:
- **Tudo que desenha acompanha o terreno** (regra 5): árvore, sombra e mesa
  trocada pegam a cota do terreno, nunca a do clique.
- Todo botão e todo campo com texto explicativo ao passar o mouse.
- Uma aba só na ribbon; painéis novos sem espaço à toa (regra de 02/10).

## 9.1 Trocar mesa (Core)
Renan: "clicar em uma mesa e ter a opção de trocar por outra, por exemplo,
trocar uma de 28 por uma de 14, e escolher quantas colocar no lugar, uma,
duas, três ... eu só falo o lado que quero travar e o sistema refaz".
A conta: dado o trecho da mesa na fileira (estação inicial, comprimento,
rumo), o tipo novo, a quantidade N e o lado travado (início ou fim), as N
mesas novas saem encostadas no lado travado, com o espaçamento entre mesas
da configuração. Se não cabem no espaço da antiga (até a vizinha), a conta
diz quanto passa; não move as vizinhas ("é problema meu").
Validação: automática (só modelo).

## 9.2 Trocar mesa (tela)
Botão "Trocar mesa" no painel Edição: clica a mesa, janela com o tipo (as
mesas do desenho), a quantidade (1 a 5) e o lado travado; apaga a antiga e
desenha as novas assentadas no terreno, com letreiros novos na fileira. Avisa
quando passa da vizinha e oferece Regerar fileira.

## 9.3 Regerar fileira (tela)
Botão "Regerar fileira": clica uma mesa, a fileira dela inteira é apagada e
gerada de novo com as mesas em uso, na mesma área e alinhamento; as outras
fileiras ficam como estão. Resolve o espaçamento depois de uma troca.

## 9.4 Objetos de sombra: árvore
Painel novo "Sombreamento" na ribbon, botão "Objetos" com o primeiro objeto,
"Árvore". Renan: "tamanho do tronco e sua largura, e a copa da árvore, altura
e largura, considerando que ela é um cilindro ... tipo um pirulito". Janela
com as quatro medidas (lembra as últimas), depois clica onde pôr, quantas
quiser. A árvore é bloco (tronco e copa cilindros) com XData das medidas, o pé
na cota do terreno; arrastada (MOVE, grip), desce ou sobe para o terreno do
lugar novo, sempre.

## 9.5 Posição do sol (Core)
Azimute e elevação do sol para latitude, longitude, data, hora e fuso
(algoritmo do NOAA). A latitude e a longitude vêm do georreferenciamento do
desenho (o mesmo do resumo do terreno). Testes contra valores de referência
do NOAA.
Validação: automática (só modelo).

## 9.6 Sombra num instante (Core)
A sombra de cada cilindro (tronco e copa) sobre o terreno e sobre o plano dos
módulos, para a direção do sol; quais módulos ela pega e quanto (fração da
face). Sol abaixo do horizonte: sem sombra, dito.
Validação: automática (só modelo).

## 9.7 Sombras num instante (tela)
Menu "Sombras" no painel Sombreamento: escolhe dia e horário, gera a sombra
desenhada no CAD (no terreno, camada própria) e marca os módulos que ela pega
(cor própria, com Tirar marcas, como as análises).

## 9.8 Sombras por período, pior caso
Na mesma janela: rodar por dia (de hora em hora, ou no passo escolhido), por
mês ou por ano; marca cada módulo pelo PIOR caso do período (a maior fração
sombreada) e diz quando foi. A sombra desenhada é a do pior instante.

## 9.9 Ver em 3D no navegador
Renan: "o PVcase tinha uma função dessa, eu clicava, abria uma página web
com um modelo 3D que eu podia navegar, tinha o terreno, mesas". Botão "3D no
navegador": grava uma página HTML com o terreno (reduzido), as mesas, os
pilares e as árvores, e abre no navegador padrão, com órbita, zoom e cores
dos tipos. Funciona sem internet (a biblioteca de 3D vai junto no bundle).
