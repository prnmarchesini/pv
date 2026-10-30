# Bifila: o modelo de referência

Lido em 30/10/2026 dos desenhos da Arctech (2P14, Aurora Solar, Lufussa,
Panamá), da geometria vetorial dos PDFs (PyMuPDF; a escala sai dos 3.000 mm
entre pilares e confere com o módulo de 2.384 mm). Medidas em metro; "ao longo"
é na inclinação (sobre o plano), "horizontal" é em planta.

## A mesa

| Item | Valor | De onde |
|---|---|---|
| Módulo | JA Solar JAM66D46-720/LB, 2,384 × 1,303 × 0,033, 720 W | `.PAN` da mesma pasta; corte (2.384) |
| Arranjo | retrato, 2 fileiras (2V), 14 colunas, 28 módulos | planta "2*14 PV Module plan" |
| Folgas | 0,020 entre colunas e entre fileiras | planta (20) e corte (20) |
| Sobras das pontas | 0,050 de cada lado (18,602 de mesa, 18,502 de módulos) | planta |
| Inclinação | 10° | corte |
| Tesoura (viga) | 4,030 | corte |
| Pórticos | 6 por mesa; vãos 3,2 + 3,5 + 3,5 + 3,5 + 3,2; balanço 0,851 | "2*14 Layout" (linhas rosa) |
| Pilar dianteiro na tesoura (T2) | 0,498 | corte: o ponto do plano em cima do eixo do pilar |
| Pilar traseiro na tesoura (T2t) | 3,544 | idem; 3,000 m do dianteiro na horizontal |
| Topo do pilar abaixo da face dos módulos | 0,126 (na vertical) | corte: face 1,185 e topo 1,059 no pilar da frente; 1,714 e 1,588 no de trás |
| Enterro (T3) | 2,200 | corte |
| Seção do pilar | 0,050 ao longo da fileira × 0,095 na inclinação | corte (0,095) e vista dos pilares traseiros (0,05) |
| Viga | 0,080 de altura | corte |
| Terças | 4 linhas, a 0,492 da borda de cada fileira de módulos (0,492 / 1,892 / 2,896 / 4,296 da ponta baixa) | corte |
| Altura da terça | 0,100 | corte: do fundo do módulo ao topo da viga |
| Emendas da terça | barras de 7,000 + 6,600 + 4,994 (2 emendas por linha) | "2*14 Layout" |
| Tirantes | X nos vãos 2 e 4, na linha dos pilares traseiros, presos a 0,20 e 0,60 abaixo do topo; peça de 3,790 | "2*14 POLE 2 Layout" |

A PB do Clivus é a face de cima do módulo na ponta baixa. A Arctech cota a
altura livre no **fundo** do módulo: 1,000 (GC1000) ou 0,800 (GC800). Na PB do
Clivus isso dá 1,000 + 0,033 × cos 10° = **1,032** e **0,832**.

## As duas variantes

Mesma mesa, mesmos pilares na tesoura; mudam a altura livre e as mãos francesas.

| | GC1000MM-NS 20 (rev. 2) | GC800MM-NS 10 (rev. 3) |
|---|---|---|
| Altura livre no fundo do módulo | 1,000 | 0,800 |
| Declividade norte-sul do catálogo | 20 % | 10 % |
| Mão francesa da frente: comprimento | 1,296 | 1,244 |
| Mão francesa da frente: abaixo do topo do pilar | 0,548 | 0,447 |
| Mão francesa de trás: comprimento | 1,233 | 1,209 |
| Mão francesa de trás: abaixo do topo do pilar | 0,826 | 0,773 |
| Pilar da frente (catálogo) | 3,259 | 3,059 |
| Pilar de trás (catálogo, já com a folga do declive) | 4,388 | 3,888 |

## Números de ouro (os testes do Core conferem estes)

Mesa no chão plano, PB = altura livre + 0,033 × cos 10°:

| Caso | Pilar da frente | Pilar de trás |
|---|---|---|
| GC1000, plano (PB 1,032) | acima do chão 1,059; total 3,259 | acima 1,588; total 3,788 |
| GC1000, chão caindo 20 % para trás (girando no pé do pilar da frente) | 3,259 | 3,788 + 0,600 = **4,388** (o do catálogo) |
| GC800, plano (PB 0,832) | 0,859; total 3,059 | 1,388; total 3,588 |
| GC800, chão caindo 10 % para trás (idem) | 3,059 | 3,588 + 0,300 = **3,888** (o do catálogo) |

Tolerância 2 mm (o PDF dá 1,0589 e 1,5884 medidos; a conta com 0,126 dá 1,0588
e 1,5877). As mãos francesas saem com o comprimento da peça (1,296 etc.), a
viga com 4,030, as terças com o comprimento da mesa (18,602).

## O que os desenhos não dizem (assumido; para o Renan confirmar)

- **Seções que não aparecem cotadas.** O pilar ao longo da fileira (0,05), a
  largura da viga, da terça e da mão francesa, e o diâmetro do tirante foram
  medidos no desenho ou chutados (viga 0,05 × 0,08; terça 0,05 × 0,10; mão
  francesa 0,04 × 0,06; tirante Ø 0,016). Só servem para o 3D; a lista de
  material sai em comprimento.
- **Emendas da terça.** A regra da Arctech (7,000 / 6,600 / 4,994) não é
  óbvia; o Clivus corta a terça em barras de no máximo 7,0 m e conta as
  emendas. Para a 2P14 dá as mesmas 3 barras e 2 emendas por linha.
- **Tirantes.** No desenho a peça tem 3,790 (rosca para os dois lados, serve
  até 20 % de declive ao longo da fileira). O Clivus desenha o tirante de um
  ponto de fixação ao outro e lista esse comprimento; a peça de catálogo fica
  no relatório como observação.
- **Vãos com tirante em mesas de outro tamanho.** A 2P14 tem 5 vãos e trava o
  2 e o 4. Para outra mesa, o projetista escreve a lista de vãos.
- **Peças pequenas** (parafusos M8/M12/M16, conectores, suportes de terça) não
  entram na lista; dependem do fornecedor.
