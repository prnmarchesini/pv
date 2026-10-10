# Etapa 21: rota MT

Do transformador até a subestação. Exatamente a mesma mecânica da CA, trocando os elos.

### 21.1 Raio virtual nos dois lados

O cabo sai do transformador, procura a vala MT mais próxima dentro do raio virtual, vai até ela pelo menor percurso, segue a vala e sobe na subestação. A busca por raio vale tanto em volta do trafo quanto em volta da subestação.
**Validação do Renan:** gera e confere que o cabo encosta na vala pelo caminho mais curto nos dois extremos.

### 21.2 Vínculo com a UC certa

O destino é a subestação à qual aquele trafo está vinculado na cadeia elétrica, nunca a mais próxima fisicamente. O vínculo manda, a posição é só representação.
**Validação do Renan:** monta um caso em que a subestação vinculada não é a mais próxima e confere que o cabo foi para a vinculada.

### 21.3 Vala fora do raio

Mesmo tratamento: avisa e pinta o equipamento que ficou sem rota.
**Validação do Renan:** força o caso e confere.

### 21.4 Escolha do cabo, gerar e tabela

Escolhe o cabo MT da biblioteca, gera, e o **Ver cabos MT** mostra a tabela com um lance por trecho, comprimento e totalização, recalculando antes de exibir.
**Validação do Renan:** gera e confere a tabela.
