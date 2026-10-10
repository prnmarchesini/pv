# Melhorias de 10/10/2026 (Melhorias.docx do Renan)

Rodada de tela do Renan sobre o commit adefc0a. O texto abaixo é o dele, sem
mudança; as imagens estão em `C:\Dev\pv-m\imagens` (fora do repositório).
Ele não usou o número 9.

| Item | Imagem |
|---|---|
| 1 | image1.png |
| 2 | image2.png |
| 3 e 4 | image3.png |
| 5 | image4.png |
| 10 | image5.png |
| 13 | image6.png |
| 15 | image7.png |
| 17 | image8.png |
| 18 | image9.png |

## Divisão em frentes

| Frente | Branch | Itens |
|---|---|---|
| Troca e ribbon | `m-troca` | 1, 13 |
| Aba Inversor | `m-inversor` | 2, 3, 4, 5, 6, 7, 8, 17, 19 (lado do inversor) |
| Rota e resumo | `m-rota` | 10, 11, 12, 16, 18, 19 (lado da rota) |
| Potência e PAN | `m-potencia` | 14, 15 |

## Texto do Renan

1 – Cliquei em trocar uma estrutura de 14 por uma de 28 e refiz a fileira e deu certo. Peguei a estrutura trocada de 28 cliquei em trocar por uma de 14 e refazer a fileira, ao refazer a fileira não foi calculado corretamente, e sobrou espaço em vazio, veja.

2 – Coloquei os inversores na área, e veja, ficaram um cima do outro, o motor tem que encaixar na medida do possivel dentro da área sem sobrepor tanto, senão couber, dar avisa falado que a área é pequena.

3 – Eu coloquei 9x inversores em uma área, fui colocar o ultimo inversor sozinhio, e veja, ele não é desenhado na área, mesmo eu clicando em área.

4 - Veja no print, é marcado área e também é marcado por em campo, erro grave. Quero um botão a mais na coluna, para quando já estiver em campo, o botão de chamar "ver em campo" ai seleciona o inversor, o modal sai, e mostra o inversor selecionado, se eu der esc, o modal volta.

5 - Uma coisa que me intriga: O botão: repartir pelo kW, deveria, pegar como seria a distribuição automática seguindo a regra de distribuição automática vai seguir, pegar a sequencia das strings, ver a potencia das strings, e ai então, atribuir o limite para cada inversor. Falta uma coisa, mostrar o total de strings distribuídas no limite de cada inversor, para ver se bate com o total de strings útil. é preciso separar a definição de quantidade máxima de strings com a distribuição de strings, conforme print

6 – Quero dar nome para cada área, pode ser na própria tabela dos inversores, hoje só é chamado área, mas ao criar uma área, queria uma forma, de nomear essa área.

7 – Nessa aba dos inversores, a lateral é "comida" com a parte de cadastro de inversor, transforme isso em um modal, não precisa ficar ali, e esse modal de hoje fica 100% para configuração dos inversores em campo.

8 – Quando distribuir, coloque a pre tag indicando o inversor, tipo, na distribuição eu não sei a ordem das strings na mesa, mas sei quais strings são de cada inversor, inclusive vc já marca com cor, mas a cor ta ruim de ver, marque também colocando a pre tag do inversor. Quando eu configurar as tags de verdade, ai vc apaga e coloca as tags configuradas.

10 – não esta errado do jeito que foi feito, mas seria melhor se cada mesa tivesse apenas uma "entrada" de cabo, duas retas entrando em uma fileira indicando cabo, ta estranho, veja o print. Podemos padronizar, somente entrada na parte alta do modulo para estrutura fixa, ou seja, na parte sul aqui no hemisfério sul, mas que no fundo, o software se orienta pela altura das mesas mesmo e cria o padrão da usina.

11 – nas abas de rotas de cabo tem o botão, selecionar valas, falta o botão de atualizar valas, para atualizar quando eu apago as linhas e um botão de "soltar todas valas" para soltar as linhas que eu selecionei.

12 – Aqui falta colocar a coluna com a tag das strings. Quero também, Subseparadores, indicando a quantidade de cabo por inversor, e por trafo, o trafo é nível acima do inversor, que soma todos inversores, ai os inversores ficam abaixo, e ai consigo saber a quantidade de cabo por trafo e inversor, e isso já vem nativamente agrupado do sistema. E acima do trafo, quero o agrupamento por UC, sendo que na maioridas das vezes vai ser um trafo por UC, mas não é regra, pode ter UC com dois ou mais trafos, ai tem que somar os trafos, e o cabo total da usina, fica la em baixo, como esta hoje. Ai eu quero que seja colapsavel, tudo fechado, mostra UCs, ai abro, trafo, inversores, e quero botão para abrir e fechar tudo o que esta abaixo do pai, tipo, abrir todos inversores do trafo, etc, abrir ou fechar todas strings do inversor.

13 - isso não pode acontecer, o submenu explodiu.

14 – Clicando no botão direito sobre a linha da área, no menu clivus, quero ter a opção de "Trocar potência do modulo" ai eu troco pela potencia que eu quiser, sem se preocupar com o tamanho, é apenas simulação, e isso leva para toda usina, todos cálculos, etc.

15 – Carreguei o arquivo pan de 700Wp e a estrutura esta com modulo de 720, preciso que o sistema indique divergências entre a estrutura e aqui. Quero que adicionei la na estrutura a opção de carregar o arquivo pan e ai não preciso carregar aqui, fica melhor, uma fonte única, na estrutura. Se eu quiser trocar o modulo, eu troco la na estrutura, e quero uma forma de Atualizar a potencia de todos os módulos sem alterar NADA, apenas a potencia via modulo pan. No botão direito, na linha da área, acabamos de permitir a atualização de potencia, pode manter, porem, isso, vai desabilitar todos os cálculos elétricos, vai ter apenas a soma das potencias, para ter calculo elétrico é preciso arquivo pan ou então, inserção manual dos valores desejáveis via cadastro do modulo. Se eu trocar a potencia via linha área, é preciso que o usuário saiba la na tela de calculo porque ele não ta vendo os cálculos, e ai, mostre a mensagem e mostre a opção de "utilizar configurações da mesa ao invés da potencia definida pela área" algo do tipo, ai sim, os cálculos passam a acontecer.

16 – na tabela resumo, em cc, quero o acréscimo das colunas de cálculos, calculando a tensão Voc, Vmppt, Isc, Imppt e se o cabo suporta ou não. Por enquanto não vamos considerar fatores de agrupamento, etc, vamos testar este calculo simples, ai em seguida a gente abre a norma, ve a forma que a norma pede para usar os agrupamentos e faz a parte 2 do calculo.

17 – Ao clicar nesse botão abaixo, automático pelas strings, as opções de inserir o inversor em campo são bloqueadas, porque o inversor sera inserido depois, na rota de cabos. Se eu já tiver inserido e clicar aqui, o sistema apaga os inversores em campo.

18 – Erro grave: Eu apaguei os inversores das áreas, fui em resumo na rota de cabos, atualizei, e continuou mostrando os cabos CC. Isso é errado, se eu apaguei os inversores, o sistema deve falar "Inversor não esta em campo, não é possivel mostrar resumo" algo desse tipo, veja, print.

19 – Mais um erro: que complementa a comentário 17. Eu tirei os inversroes então a aba CC é bloqueada, ok, porém, se eu cliquei em distribuição automática em campo dos inversores, a aba CC deve ser liberada, porque os inversores serão posicionados em campo através da rota de cabos, onde o sistema irá calcular a MENOR rota possivel dos cabos daquele inversor e inserir no desenho. Em seguida, eu posso ajustar a posição do inversor e clicar em recalcular a rota para um, vários ou todos inversores. Isso é por inversor, quando eu colocar que o inveersors sera inserido automático, na coluna dele deve sair o por em campo e entrar algo do tipo "Alocação automática" e bloquear o clique.
