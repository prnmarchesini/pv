# Regras invioláveis da parte elétrica

Valem junto com as regras sagradas do módulo de layout (pilar nunca flutua, mesa é monolito, toda entidade é única e carrega atributos, a ponta baixa manda). Estas se somam, específicas do elétrico.

## 1. O vínculo elétrico é a verdade, a posição física é representação
A cadeia UC -> trafo -> inversor -> string -> módulos é guardada como vínculo de dados (XData/GUID), nunca inferida da posição dos retângulos no desenho. Mover um retângulo de inversor no CAD não muda a que trafo ele pertence. O vínculo só muda por comando explícito.

## 2. Uma string pertence a no máximo um inversor
String já alocada a um inversor fica travada: não pode ser selecionada nem alocada por outro. Sem alocação dupla, nunca. Desalocar solta o vínculo; alocar de novo é um ato explícito.

## 3. Desalocar nunca apaga geometria
Soltar uma string de um inversor, apagar todas de um inversor, remover uma alocação: isso mexe só no vínculo. A string desenhada sobre os módulos continua existindo. Apagar o traçado da string é outro comando, separado.

## 4. Seleção em campo respeita o tipo do modo
Cada modo de seleção em campo filtra o que pode ser pego:
- selecionar mesas (config de tipo de string): só mesa.
- selecionar strings (alocação em inversor): só string, nunca mesa, porque uma mesa tem várias strings.
- selecionar inversores (agrupamento por trafo): só inversor.
Curva de nível, terreno, qualquer outra entidade: ignorados sempre.

## 5. Respeitar a declividade do módulo no traçado
O traçado da string é desenhado módulo a módulo acompanhando a inclinação real de cada módulo. Nunca uma linha plana que enterra no terreno ou flutua. Este é o defeito do PVcase que o Clivus Solar existe para não repetir.

## 6. Nunca ficar em silêncio sobre o que não casou
Mesa que não corresponde a nenhuma configuração de string não é preenchida E o sistema avisa qual. String que excede a capacidade do inversor: aviso vermelho. Nada falha calado.

## 7. Nada automático além do que foi pedido
Gerar traçado, gerar numeração, alocar: só por comando. O sistema não renumera nem realoca sozinho ao editar. Edição é granular: apagar tudo, apagar de um inversor, refazer um inversor, regerar um bloco. Cada operação é independente e isolada.

## 8. Sequencial da string reinicia em cada inversor
A numeração sequencial da string recomeça do 1 a cada inversor. Isso mantém a edição contida: regerar um inversor ou um bloco não empurra a numeração dos outros.
