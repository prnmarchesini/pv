# Etapa 32: segunda rodada (Renan, 21/10/2026, depois da primeira tela)

Pedido, nas palavras dele: "Linha: vc entendeu MUITO errado. Eu quero fazer uma
linha, e da linha, ver o perfil dela, o gráfico, X e Y, mostrando quanto corre e
quanto sobe, vendo o PERFIL do terreno. [...] quero passar o mouse no gráfico e
ver uma seta se movendo na planta, indicando o mesmo ponto do gráfico, e se eu
passar o mouse na canaleta na planta, a flecha aponta o mesmo ponto do gráfico [...]
pode ser ligado ou desligado." E: exportar os metros de cada canaleta e tipo;
dissipador como opção (o memorial tem o DES); caixa entre canaletas; o menu
Canaleta vira Desenhos (Canaleta, Caixa, Dissipador); na memória, clicar num
ponto da canaleta e ver a vazão ali; o dissipador da memória tem que ficar
nítido; o nome da canaleta marcado em campo; dá para puxar o Plúvio pelas
coordenadas?

### 32.1 Linha: o perfil do terreno no sentido em que foi desenhada

A linha de análise é lida do primeiro clique ao último (não da ponta mais alta,
que é coisa de canaleta). O gráfico é X × Y de verdade: distância desde o
início e cota, com os títulos dos eixos. Em cada trecho: quanto corre (Δx em
planta) e quanto sobe ou desce (Δy), com % e grau. Na planta, "A" no início e
"B" no fim da linha; no gráfico, "A" e "B" nas pontas.
**Validação:** desenha de baixo para cima e confere que o gráfico começa no A.

### 32.2 Rastreio planta ↔ gráfico, liga e desliga

Mouse sobre o gráfico: uma seta anda na planta, no ponto da linha que
corresponde à distância. Mouse sobre a linha ou a canaleta na planta: uma
vertical anda no gráfico, no mesmo ponto, com a dica "estaca, cota". Liga e
desliga num botão (Análise › Rastrear); o estado fica gravado no PC.
**Validação:** passa o mouse no gráfico e na canaleta e vê a seta e a vertical.

### 32.3 Nome da canaleta em campo

Cada canaleta (e linha) tem o nome escrito ao longo dela, no meio do trecho mais
comprido, deitado e sempre legível (nunca de cabeça para baixo): "Canaleta 1 ·
Trap 30/60". Refeito junto com ela.
**Validação:** confere o nome em planta, mexe na canaleta e vê o nome ir junto.

### 32.4 Menu Desenhos

O botão Canaleta vira o suspenso **Desenhos**: Canaleta, Caixa, Dissipador,
Exportar canaletas. Análise ganha Rastrear; Memória ganha Vazão no ponto.
**Validação:** confere os menus.

### 32.5 Dissipador como opção, nítido na memória

O dissipador deixa de nascer sozinho no pé de toda canaleta: Desenhos ›
Dissipador, clique perto da ponta da canaleta (a mais perto do clique) e ele é
posto ali (DES do álbum do DNIT IPR 736, desenho 1.19, pela abertura B da
seção: L1 = B, L2 = 3·L1, comprimento = 2·L1), em planta (o trapézio) e em 3D,
com o rótulo do modelo. [Remover] tira. Ele acompanha a canaleta. Na memória, a
aba Dissipadores mostra só os postos, com a conta escrita (B, L1, L2,
comprimento, modelo) e a velocidade de saída; canaleta sem dissipador com a
velocidade de saída acima da que o terreno aguenta ganha alerta "sugere
dissipador".
**Validação:** põe um dissipador, orbita, abre a memória e lê a conta.

### 32.6 Caixa entre canaletas

Desenhos › Caixa: clique no ponto (encontro de canaletas). Caixa de concreto
retangular, medidas internas e espessura gravadas no desenho (padrão 0,60 ×
0,60 m, parede 0,15 m), alinhada à canaleta mais próxima, com o fundo 10 cm
abaixo do fundo mais baixo das canaletas que chegam nela (ou a profundidade
mínima, se nenhuma chega) e o topo no terreno. Em planta, o contorno e o nome
("CX 1"); em 3D, o sólido oco.
**Validação:** põe a caixa no fim de duas canaletas e orbita.

### 32.7 Exportar canaletas

Desenhos › Exportar canaletas: Excel com cada canaleta (nome, tipo, seção,
medidas, revestimento, metros em planta e no fundo, concreto, escavação), o
resumo por tipo de seção (metros e quantidade), as caixas e os dissipadores.
**Validação:** abre o Excel.

### 32.8 Vazão no ponto

Memória › Vazão no ponto (depois de calculadas as bacias): clique na canaleta;
a linha de comando diz a estaca, a área que chega até ali e a vazão naquele
ponto (método racional com o tc da bacia), e a vazão do fim, para comparar.
Clica de novo em outro ponto; Enter sai. Cada célula da bacia guarda onde a
água dela entra na canaleta: a área até o ponto é a das células que entram
antes dele.
**Validação:** clica no começo, no meio e no fim e vê a vazão crescer.

### 32.9 Plúvio

Resposta ao Renan: o Plúvio 2.1 (GPRH/UFV) é um programa de Windows com a base
de dados dele; não há serviço público para consultar pelas coordenadas, e ele
não está instalado neste PC. Os parâmetros K, a, b, c continuam digitados na
memória. Se o Renan instalar o Plúvio, dá para investigar se a base dele pode
ser lida.
