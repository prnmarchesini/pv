# Etapa 12: Subestação (unidade consumidora)

Primeira aba da configuração elétrica. Contexto: a usina tem uma entrada de energia e depois várias medições; cada medição é como uma usina independente puxando de um trafo. Mas pode ser também várias subestações unitárias (postos separados) em vez de uma compartilhada. Os dois casos têm que caber.

### 12.1 Modo compartilhado e vínculo com trafo
Cria UCs compartilhadas: C1, C2, C3... Cada uma recebe nome da UC e se associa a um ou mais trafos numa tabela (geralmente um trafo, mas pode ter mais). O vínculo é dado, não posição.
**Validação do Renan:** cria C1 e C2, associa C1 a dois trafos, confere a tabela.

### 12.2 Modo unitário
Em vez de compartilhada, vários bloquinhos independentes, cada um com seu vínculo a trafo. Ao desenhar, o sistema pergunta: adicionar subestação compartilhada ou várias unitárias.
**Validação do Renan:** cria duas unitárias com vínculos distintos, confere.

### 12.3 Dimensão, tag e alocação em campo
Cada bloquinho (compartilhado ou unitário) tem tamanho próprio, nome próprio e vai alocado em campo ponto a ponto, criando a relação física. Retângulo com a dimensão do cadastro, tag visível de cima.
**Validação do Renan:** aloca os bloquinhos em campo, confere posição, tag e que o vínculo elétrico com o trafo continua intacto.
