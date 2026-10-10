# Regras invioláveis do roteamento

Somam-se às regras sagradas do layout e às regras elétricas do plano anterior.

## 1. A vala é input do usuário, nunca invenção do sistema

A vala é uma polyline comum que o Renan desenha à mão antes de entrar no menu. O sistema não cria, não move e não completa vala. Ele só recebe a seleção, atribui a layer e usa aquele traçado como caminho.

## 2. O cabo nunca corta o meio da mesa

O cabo sai da string pelo sentido de saída da mesa, contorna por fora dela e segue acompanhando a fileira. Cortar o miolo da mesa vira rabisqueira ilegível e é proibido.

## 3. Menor percurso sempre

Quando há mais de um caminho possível, o sistema escolhe o de menor comprimento. Mesa de 100 m com vala dos dois lados e a string no metro 40: vai pelo lado dos 40, não dos 60. Vale para o lado de saída da string e para a escolha da vala mais próxima nos trechos CA e MT.

## 4. Nunca falhar calado

Não achou vala no alcance, a vala não passa da mesa, trecho sem par na cadeia: o sistema avisa e **pinta** as entidades que não conseguiu rotear, dizendo o motivo. Nenhum cabo some sem explicação.

## 5. Cada lance é uma entidade contável

Cada linha de cabo desenhada é um lance, com identidade própria, comprimento próprio e vínculo com o elo que ela liga. É isso que alimenta a tabela e o resumo.

## 6. No CC sempre dois cabos por string

Positivo e negativo, sempre os dois, cada um com seu lance e seu comprimento. No traçado convencional os dois têm comprimentos diferentes. No leapfrog a diferença é de um módulo.

## 7. Recontagem antes de qualquer contagem

O Renan pode apagar cabo com Delete puro, por fora do plugin. Por isso, toda vez que o sistema for contar, inclusive no simples ato de abrir o **Ver cabos**, ele **recalcula tudo** antes de mostrar, acerta os números e pinta o que foi apagado manualmente. Nunca exibir número vindo de cache sem reconferir o desenho.

## 8. O sistema calcula, não dimensiona

Nenhum cálculo escolhe bitola, aprova cabo ou emite veredito. O sistema aplica as fórmulas sobre os inputs que o Renan deu e mostra os números. A decisão é dele.

## 9. O cabo é 3D

O cabo é desenhado no 3D, descendo até a profundidade da vala configurada naquela aba. O comprimento contado é o do percurso 3D real, incluindo as descidas e subidas de vala.
