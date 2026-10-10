# Etapa 20: rota CA

Do inversor até o transformador. Mesma mecânica da CC em tudo: profundidade, selecionar vala, escolher cabo, gerar, ver cabos, apagar, recontagem. A diferença está em como o cabo acha a vala.

### 20.1 Raio virtual de busca da vala

O cabo sai do inversor e procura a vala CA mais próxima dentro de um **raio virtual** em volta do equipamento (configurável, padrão na faixa de 5 a 10 m). Achou, vai até ela pelo **menor percurso possível**.
A busca vale nos dois lados do trecho: em volta do inversor e em volta do trafo.
**Validação do Renan:** põe a vala do ladinho do inversor e confere que o cabo encosta nela pelo caminho mais curto.

### 20.2 Percurso pela vala até o trafo

Encontrada a vala, o cabo desce à profundidade configurada, segue o traçado da polyline e sobe no transformador de destino.
**Validação do Renan:** confere no 3D que o cabo acompanha a vala e chega no trafo certo.

### 20.3 Vala fora do raio

Não achou vala dentro do raio: avisa e **pinta** o inversor (ou o trafo) que ficou sem rota, dizendo que não encontrou vala no alcance. Nunca chutar um caminho.
**Validação do Renan:** afasta uma vala além do raio e confere o aviso e a pintura.

### 20.4 Escolha do cabo, gerar e tabela

Escolhe o cabo CA da biblioteca, gera, e o **Ver cabos CA** mostra a tabela com um lance por trecho, comprimento e totalização, sempre recalculando antes de exibir.
**Validação do Renan:** gera e confere a tabela contra uma medição de amostra.
