# Etapa 18: rota CC

Da string até o inversor (ou até o combiner, quando houver). Usa toda a base da etapa 17.

### 18.1 Saída da string e contorno da mesa

O cabo sai da ponta da string (positivo e negativo) no sentido de **sair da mesa**, nunca cortando o miolo dela. Contorna por fora e segue acompanhando a fileira até o fim dela.
Este é o ponto que o PVcase acertava e que não pode regredir: linha cortando o meio da mesa vira rabisqueira ilegível.
**Validação do Renan:** gera numa fileira e confere na tela que nenhuma linha atravessa o meio de mesa nenhuma.

### 18.2 Escolha do lado de menor comprimento

Quando há vala dos dois lados, a string sai pelo lado de **menor comprimento**. Mesa de 100 m com vala nos dois lados e a string no metro 40: vai pelos 40, não pelos 60.
**Validação do Renan:** monta esse caso e confere que todas as strings foram para o lado mais perto.

### 18.3 Atribuição manual de lado

Já previsto nesta etapa, mas como ação opcional: o Renan seleciona strings em campo e manda ir para a direita ou para a esquerda, sobrescrevendo a escolha automática. String com lado atribuído guarda isso e não volta ao automático numa regeração.
**Validação do Renan:** força um punhado de strings para o lado "errado" e confere que o sistema respeitou.

### 18.4 Chegada na vala

Do fim da fileira, o cabo vai reto até bater na vala. Ao bater, passa a seguir o traçado da polyline até o destino.
**Validação do Renan:** confere que o cabo entra na vala e acompanha ela até o inversor.

### 18.5 Vala que não passa da mesa

Se a vala não passar da mesa, o cabo não tem onde bater. O sistema **não chuta**: avisa quais mesas ficaram sem rota e **pinta** essas mesas, dizendo que faltou a referência da vala.
**Validação do Renan:** deixa uma fileira sem vala alcançável e confere o aviso e a pintura.

### 18.6 Escolha do cabo e gerar

Seção de cabo: escolhe da biblioteca qual cabo vale para essa rota (ex. 6 mm²). Botão **Gerar** desenha os cabos na layer da rota.
**Validação do Renan:** gera com 6 mm², confere o desenho.

### 18.7 Dois cabos por string, sempre

Para cada string, sempre dois lances: positivo e negativo, até o inversor. Podem ir em layers separadas (mais e menos) para facilitar a leitura, mas a identificação real é pelo XData.
No traçado convencional os dois lances têm comprimentos diferentes. No leapfrog a diferença é de um módulo.
**Validação do Renan:** confere numa string convencional e numa leapfrog que os comprimentos batem com o esperado.

### 18.8 Ver cabos CC

Botão **Ver cabos CC**. Recalcula tudo antes de exibir (regra 7) e mostra a tabela: cada lance com seu comprimento, separado por polaridade, e a totalização.
**Validação do Renan:** abre a tabela e confere a totalização contra uma medição manual de amostra.
