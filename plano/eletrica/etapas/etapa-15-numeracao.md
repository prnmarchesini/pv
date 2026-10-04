# Etapa 15: Numeração das strings

Quarta aba. Passo 3 do fluxo (gera string, aloca no inversor, numera). Botão de renumerar em cada inversor abre esta seção. Começa simples, trava uma forma, depois cresce.

### 15.1 Composição da tag em três pedaços
A tag é composição de três partes: trafo, inversor, string. Em cada pedaço o usuário escolhe o prefixo (ex. T, ou a palavra trafo, ou nada) somado ao número sequencial, e o separador entre os pedaços (ponto, risquinho ou nada colado). Ex.: T1.I1.S1, ou 1S1, conforme a escolha. A string é sequencial POR inversor e reinicia a cada inversor.
**Validação do Renan:** monta uma composição com separador ponto e outra colada, confere o formato gerado.

### 15.2 Configuração da varredura e blocos
Seção de configuração da varredura: sentido do sequencial da string (esquerda para direita, direita para esquerda, de cima para baixo, de baixo para cima). Vale para a usina inteira por padrão. Além do PVcase: dá para criar blocos de configuração. Bloco 1, seleciona as mesas dele, com sentido de varredura próprio. Bloco 2, outras mesas. Dá para picar a usina inteira em blocos.
Inversor e trafo NÃO entram na varredura: são nomeados um a um pelo Renan na alocação em campo, na ordem que ele pôs.
**Validação do Renan:** cria dois blocos com sentidos diferentes, confere que cada um varre no seu sentido.

### 15.3 Ordem dos blocos na lista
A sequência segue a ordem dos blocos na lista: numera o bloco 1 inteiro, depois o 2, e segue. Dá para reordenar os blocos na lista para mudar a sequência.
**Validação do Renan:** inverte a ordem de dois blocos na lista e confere que a numeração seguiu a nova ordem.

### 15.4 Botão Gerar
Botão gerar: faz as varreduras na ordem dos blocos e cria as tags no desenho. Depois de gerado, a cadeia fica carregada na memória do sistema (inversor 1 tem tais strings, trafo 1 tais strings), base para o resumo.
**Validação do Renan:** gera e confere as tags no desenho na ordem esperada.

### 15.5 Edição granular
Tudo independente e editável: apagar tudo, apagar só de um inversor, refazer só de um inversor, ou mudar a varredura de um bloco e regerar só aquele bloco. Como o sequencial reinicia por inversor, regerar um pedaço fica contido e não empurra os outros.
**Validação do Renan:** regera um único bloco e confere que os outros não mudaram.
