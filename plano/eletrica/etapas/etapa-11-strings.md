# Etapa 11: Corrente contínua (strings)

Universo próprio, separado da configuração elétrica. Aqui só se desenha o traçado das strings sobre os módulos, SEM nome. Nomear é na etapa 15.
Botão **String** com duas abas tipo Chrome: **Configuração** e **Gerar**.

### 11.1 Modelo de tipo de string e biblioteca
Aba Configuração. "Adicionar tipo de string" cria um modelo na biblioteca, com nome (modelo 1, 2, 3). Guarda a assinatura de arranjo (quantidade e disposição de mesas que cobre) e o traçado. Core puro.
**Validação do Renan:** cria dois modelos, confere que ficam na lista com os nomes.

### 11.2 Seleção de mesas e representação cartesiana
Ao adicionar o tipo, o modal se oculta, o mouse vira quadradinho de seleção, o usuário pega uma ou mais mesas (só mesa) e dá enter. O modal volta mostrando NÃO a mesa real, mas os módulos num plano cartesiano X/Y: 2V mostra duas fileiras (ex. 28 em cima e 28 embaixo); 1V mostra uma só. Duas mesas de 14 aparecem as duas, com o espaçamento de campo.
**Validação do Renan:** seleciona uma mesa 2V e vê o cartesiano certo; seleciona duas mesas de 14 e vê as duas.

### 11.3 Marcar polaridade e traçado convencional
No cartesiano o usuário marca onde é positivo e onde é negativo. Pontas opostas = ligação convencional.
**Validação do Renan:** monta um traçado convencional e confere positivo e negativo nas pontas.

### 11.4 Leapfrog e traçado livre por cliques
Botão leapfrog monta o padrão alternado (1º módulo positivo, 2º negativo) na fileira inteira de uma vez. E o traçado é livre por cliques: letra U na metade da mesa, pegar 14 embaixo e subir, etc. Cada trecho pode ser convencional ou leapfrog.
**Validação do Renan:** gera um leapfrog de fileira inteira e um traçado livre em U, confere os dois.

### 11.5 Editar, apagar, clonar, espelhar modelo
Cada modelo: editar, apagar, clonar, e espelhar/inverter (mesma config com o positivo do outro lado, para outra parte da usina).
**Validação do Renan:** clona um modelo, espelha, confere que a polaridade inverteu.

### 11.6 Aba Gerar: casar config com grupos de mesas
Aba Gerar: escolhe quais configurações valem para aquela porção. Clica em gerar, pede seleção, arrasta e só seleciona mesas (ignora curva de nível e o resto). O sistema casa cada config com grupos de mesas de arranjo igual (config de duas mesas de 14 só preenche grupos de duas mesas de 14).
**Validação do Renan:** gera numa área com arranjos variados, confere que cada config caiu no grupo certo.

### 11.7 Desenhar traçado respeitando a declividade do módulo
Desenha o traçado módulo a módulo com os sinais mais e menos, acompanhando a inclinação real de cada módulo. Nunca linha plana que enterra ou flutua (o defeito do PVcase).
**Validação do Renan:** gera em terreno inclinado, confere no 3D que o traçado acompanha os módulos.

### 11.8 Avisar mesa que não casou
Mesa sem config correspondente não é preenchida e o sistema avisa qual (ex. mesa de 20 nunca configurada). Nunca ficar calado.
**Validação do Renan:** deixa uma mesa de arranjo não configurado na área e confere o aviso nominal.
