# Etapa 5: Processamento

A etapa mais longa. Passos pequenos, muitos testes de propriedade.

### 5.1 Distribuição em planta
Fileiras a partir da linha de alinhamento, com pitch e azimute. Mesas parcialmente fora da área são mantidas e marcadas.
Testes: área retangular dá a contagem exata esperada; área irregular; nenhuma mesa sobreposta.

### 5.2 Amostragem
Para cada mesa: cota do terreno no pé de cada pilar e sob a ponta baixa de cada módulo da fileira de baixo. Uma vez só.

### 5.3 Cotas viáveis por mesa
Conjunto de pares (ponta baixa início, ponta baixa fim) que respeitam a faixa, com a tolerância de lombo e o limite de inclinação longitudinal.

### 5.4 Alinhamento na fileira
Escolha da sequência que respeita o degrau entre mesas e minimiza estouro. Implementar iterativo com máximo de iterações configurável, visível na interface. (Programação dinâmica fica registrada como alternativa a avaliar.)
Testes: terreno plano dá degrau zero; rampa uniforme; calombo dentro e fora da tolerância.

### 5.5 Pilares
Por pilar: cota do terreno, cota de topo, altura livre, embutimento, comprimento arredondado para o comercial. Estouro marcado, nunca escondido.
Testes: regra sagrada 1 em toda saída.

### 5.6 Resultado das análises
Cada módulo e pilar carrega seus valores; as análises configuradas marcam o que passou do limite.

### 5.7 Desenho
Pilares primeiro (caixas), depois módulos (caixas + face superior na layer de módulo), numa transação só. Cores e camadas das análises. Labels "Mostrar alturas".

### 5.8 Uma fileira no CAD
Comando que processa uma fileira escolhida.
**Validação do Renan:** confere à mão três ou quatro pilares de uma fileira real.

### 5.9 Área inteira
Processa a área toda. Medir tempo.
**Validação do Renan:** roda numa usina que ele conhece do PVcase e compara contagens e alturas.

Acervo: o resultado da 5.8 e da 5.9, conferido pelo Renan, vira referência congelada.
