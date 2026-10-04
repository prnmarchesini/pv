# Etapa 14: Inversor

Terceira aba. O PVcase era pobre aqui (inversor sem modelo). O Clivus Solar faz melhor, com estrutura para crescer.

### 14.1 Modelo de inversor
Configurador único que serve tanto para inversor genérico do cliente quanto para modelo cadastrado (ex. Huawei 250, ~5 MPPT, 4 ou 5 entradas por MPPT, ~20 e poucas entradas totais). Por ora guarda só nº de MPPT e nº de entradas por MPPT, com o total derivado. Sem balanceamento ainda, mas a estrutura tem que permitir crescer.
**Validação do Renan:** cadastra um genérico e um modelo tipo Huawei, confere o total de entradas.

### 14.2 Lista de inversores por modelo
Escolhe o modelo, o sistema pergunta quantos inversores desse tipo, cria a lista. Cada item mostra quantas strings tem alocadas (zero no começo). A usina pode ter mais de um tipo de inversor.
**Validação do Renan:** cria 4 inversores de um modelo e 2 de outro, confere a lista.

### 14.3 Alocação manual de strings em campo
Botão de mais/alocar na frente de cada inversor. Clica, o modal some, o usuário seleciona em campo SÓ strings (arrastar nunca pega mesa, porque uma mesa tem várias strings). Caixa de texto suspensa sobre o CAD conta as strings selecionadas ao vivo. Ctrl desseleciona. String selecionada ganha destaque (opacidade, transparência ou contorno) para não se perder. Enter, o modal volta com a contagem daquele inversor.
**Validação do Renan:** aloca um punhado de strings num inversor, testa o Ctrl, confere a contagem na volta.

### 14.4 Aviso de excesso de capacidade
Se passar do que o modelo comporta (ex. 28 strings num inversor de 26 entradas), mensagem vermelha de excesso.
**Validação do Renan:** força o excesso e confere o aviso.

### 14.5 Editar, selecionar todas, apagar todas
Cada inversor: editar, selecionar todas, apagar todas. Apagar = soltar a alocação, NUNCA apagar a string do desenho. String já alocada a um inversor fica travada, não pode ser pega por outro.
**Validação do Renan:** apaga todas de um inversor, confere que as strings continuam no desenho e ficaram livres; tenta pegar uma string já alocada por outro inversor e confere que está travada.

### 14.6 Dimensão, tag e alocação do inversor em campo
Cadastro do inversor com dimensão (largura, comprimento, altura). Botão alocar posição em campo: clica, posiciona. Retângulo 3D flutuando a uns 80 cm, tag (ex. inversor 1) visível de cima.
**Validação do Renan:** aloca dois inversores em campo, confere dimensão, tag e 3D.

### 14.7 Agrupamento de inversores por trafo (skid)
Seleciona inversores (só inversor), fecha seção com nome, diz que o grupo é do trafo 1, outro do trafo 2. O skid é trafo + inversores. Quantos trafos e grupos quiser.
**Validação do Renan:** agrupa 4 inversores no trafo 1 e 2 no trafo 2, confere o vínculo na cadeia.
