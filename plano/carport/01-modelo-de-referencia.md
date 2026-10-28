# Carport biposte: o modelo de referência

Lido em 28/10/2026 de `0 - Assets/Cobertura/carport-biposte.glb` (gerador
"Soloz offline SZ3D to GLB converter v1", eixo Z para cima na origem; 604 nós,
44 malhas). O arquivo diz `physicalUnitsVerified: false`; as unidades foram
conferidas pelo módulo, que dá 2,258 × 1,131 m (medida de módulo comercial).
Eixos abaixo: X ao longo da fila, Y na descida (o alto em Y negativo), Z para cima.

## Números de ouro (os testes do Core conferem estes)

| Item | Valor | De onde |
|---|---|---|
| Módulo | 2,258 (na descida) × 1,131 (na largura) | malha 37, 15 instâncias |
| Módulos por vão | 5 na largura × 3 na descida, retrato | X -3,003…2,664; Y -1,714…5,086 |
| Folga entre colunas | 0,004 | 1,529 → 1,533 |
| Folga entre fileiras | 0,013 | 2,815 → 2,828 |
| Inclinação | 7,0° | 0,277 de queda em 2,258 |
| Borda baixa dos módulos | Z 2,345 | malha 32 |
| Borda alta dos módulos | Z 3,21 | caixa total |
| Pórticos | X = ±2,75 (5,50 entre eles) | malhas 6/7 e pilares |
| Bases | o meio delas fica 2,4 cm para o lado baixo do meio da cobertura | chapas (malha 22) |
| Pilar da frente (lado alto) | base Y 0,497; topo Y ≈ -0,26, Z 2,77 | malhas 28/29/30 |
| Pilar de trás (lado baixo) | base Y 2,922; topo Y ≈ 3,55, Z 2,31 | malhas 24/26/27 |
| Perfil do pilar | dois C de 0,05 de aba, costas com costas (0,10 total) | malhas 28+29 = 30 |
| Viga principal | dois C, Y -1,223…4,546 (5,77), Z 2,18…2,99 | malhas 6/7 |
| Terças | 6, comprimento 5,84, em Y 4,46 / 3,34 / 2,26 / 1,14 / 0,01 / -1,11 | malha 2 |
| Mão francesa da frente | Y 0,03…0,66, Z 2,15…2,67; pega a viga a 0,84 do topo do pilar | malhas 10/13/14 |
| Mão francesa de trás | Y 2,76…3,28, Z 1,70…2,40; pega a viga a 0,71 do topo do pilar | malhas 15/16/17 |
| Chapa de base | 0,31 × 0,31 × 0,006, nervuras de 0,186 | malhas 22, 19/20/21 |
| Bloco da base | 0,31 × 0,31 × 0,10 | malha 31 |
| Ponto mais baixo sobre o chão | 1,70 (mão francesa de trás, junto ao pilar) | |

Peças pequenas (parafusos, grampos, chapas de ligação: malhas 0, 1, 3, 4, 5,
8, 9, 11, 12, 18, 23, 25, 40 a 43) entram na lista de material como
contagem por vão, não como desenho.

## O que o modelo não diz

- **Vão × largura dos módulos.** Os 5 módulos ocupam 5,667 m e os pórticos
  ficam a 5,50 m; no `.glb` o vão está deslocado (balanço de 0,25 de um lado e
  -0,09 do outro). Na fila contínua o padrão é **vão = largura dos módulos do
  vão** (5,67), com os pórticos nas divisas dos vãos: a cobertura termina no
  eixo do primeiro e do último pórtico, sem balanço nas pontas da fila.
  Confirmar com o Renan na primeira tela.
- **Seção dos perfis** (altura da alma, espessura). O `.glb` dá só a caixa;
  ficam como campos editáveis do perfil, com a caixa do `.glb` como padrão.
- **Fundação.** O bloco de 0,10 é o que aparece; a fundação de verdade é do
  calculista.
