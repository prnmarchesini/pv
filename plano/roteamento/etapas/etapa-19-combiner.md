# Etapa 19: combiner box

A combiner box é um elo intermediário opcional. Com ela, o trecho CC quebra em dois: **string -> combiner** e **combiner -> inversor**. Sem ela, string vai direto no inversor.

A mecânica é exatamente a mesma das outras abas. Nada de novo além do elo.

### 19.1 Cadastro da combiner box

Cadastro no mesmo molde do inversor e do trafo: nome, tag, número de entradas, dimensão física (largura, comprimento, altura) para o retângulo 3D, e alocação em campo clicando na planta. Tag visível de cima.
**Validação do Renan:** cadastra duas combiners, aloca em campo e confere tag e retângulo.

### 19.2 Vínculo na cadeia

A string passa a se vincular à combiner, e a combiner ao inversor. A cadeia completa fica UC -> trafo -> inversor -> combiner -> string -> módulos. Quando não há combiner, o elo simplesmente não existe e a string se vincula direto ao inversor.
A alocação de strings na combiner segue as mesmas regras da alocação em inversor: seleção em campo só de string, trava de string já alocada, contagem ao vivo, Ctrl desseleciona, aviso de excesso de entradas.
**Validação do Renan:** aloca strings numa combiner e a combiner num inversor, confere a cadeia no resumo.

### 19.3 Aba Combiner na rota de cabos

Aba própria, com layer e cor próprias, e exatamente a mesma mecânica: profundidade, selecionar vala, escolher cabo, gerar, ver cabos, apagar.
O trecho string -> combiner se comporta igual ao CC da etapa 18 (sai da mesa, contorna, menor lado, bate na vala). O trecho combiner -> inversor se comporta igual ao CA da etapa 20 (raio virtual em volta do equipamento para achar a vala mais próxima).
**Validação do Renan:** gera os dois trechos numa usina com combiner e confere os traçados e a tabela.
