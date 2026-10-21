# Cálculos hidráulicos

Tudo aqui mora em `Clivus.Core` (`Rainfall`, `Hydraulics`) e é teste de nível 1.
Fonte: memorial `0 - Assets/Drenagem/Memorial Drenagem R00.docx` (UFV
Valparaiso). Os números entre colchetes são os **números de ouro**: o teste
reproduz o memorial com a tolerância do arredondamento dele.

## 1. Escoamento superficial da bacia

- Comprimento do curso principal `L` (m): o maior caminho de escoamento dentro
  da bacia até a canaleta (sai do `FlowTracer`, etapa 30).
- `ΔH` (m): cota do início do caminho menos a cota da chegada na canaleta.
- `S` (%) = ΔH / L · 100.
- Kirpich: `tc = 57 · (L³ / ΔH)^0,385`, L em **km**, tc em min.
- Kerby, **na forma do memorial**: `tc = 37 · (0,5 · L / S)^0,47`, L em km,
  S em %. Conferido em 21/10/2026: reproduz a Tabela 1 (bacia 1 dá 7,05 min).
  Não é a Kerby "de livro" (1,44·(L·N/√S)^0,467); a regra 6 manda seguir o
  memorial. O coeficiente 0,5 é a rugosidade de Kerby e fica editável.
- `tc` adotado = max(5 min, min(Kirpich, Kerby)). Na Tabela 1 as três bacias
  caem no piso de 5 min.
- Velocidade superficial `V = L / (tc · 60)` (m/s), comparada com a máxima da
  cobertura (Tabela 31 do DNIT, abaixo).

Ouro (Tabela 1): bacia 1, L = 179,85 m, ΔH = 5,51 m, S = 3,06 % →
[Kerby 7,05; Kirpich 4,07; tc 5,00; V 0,60]. Bacia 2: L 166,23, ΔH 8,70,
S 5,23 → [5,28; 3,12; 5,00; 0,55]. Bacia 3: L 166,54, ΔH 6,50, S 3,90 →
[6,07; 3,50; 5,00; 0,56].

## 2. Intensidade (IDF)

`I = K · T^a / (t + b)^c`, I em mm/h, T em anos, t em min. Parâmetros do
projeto vêm do Plúvio 2.1 e são digitados pelo Renan. Padrão inicial
(Valparaiso-SP): K = 1544,886; a = 0,142; b = 24,953; c = 0,756; T = 25 anos.

Ouro (Tabela 3): T = 25, t = 5 → [186,73]. T = 15, t = 60 → [78,97].
T = 100, t = 1440 → [12,01]. O sistema mostra a tabela T × t inteira como no
memorial (T = 15, 25, 50, 100; t = 5, 10, 15, 30 min, 1, 2, 4, 8, 12, 24 h).

## 3. Vazão de projeto (método racional)

`Q = C · I · A / 3600`, Q em l/s, A em m². Válido até 50 ha: bacia maior
gera alerta. C vem da Tabela 39 do DNIT (lista editável; padrão 0,15, "prados
e campinas").

Ouro (Tabela 5): A = 13 289, C = 0,15, I = 186,73 → [103,39 l/s]; A = 30 120 → [234,35 l/s].

## 4. Seções

### Trapezoidal (Figura 7 do memorial)

Entradas: base `b`, talude `z = x / y` (horizontal por vertical), espessura `e`.
Para uma lâmina `h`: `A = (b + z·h)·h`; `P = b + 2·h·√(1 + z²)`; `Rh = A / P`.
Para a altura total `y`: `x = z·y`; `B = b + 2·x`.

### Meia-cana (circular)

Entrada: raio `r`, espessura `e`. Altura útil = r. Para uma lâmina `h ≤ r`:
`θ = 2·acos(1 − h/r)`; `A = r²·(θ − sen θ)/2`; `P = r·θ`; `B = 2·√(h·(2r − h))`.

## 5. Manning e continuidade

`v = Rh^(2/3) · I^(1/2) / n`; `Q = v · A`. A lâmina normal `h` para uma vazão
sai por bisseção (Q cresce com h nas duas seções até a altura útil). n padrão
0,014 (concreto, Porto, condição boa), editável.

Por canaleta, com o perfil do fundo (etapa 28):
- `Imin` e `Imax` do perfil (por trecho, não só média);
- `hmax` = lâmina na `Imin` (é onde a água sobe mais);
- `vmax` = velocidade na `Imax`; `vmin` = velocidade na `Imin`.

Alertas: vmax > 4,5 m/s (concreto) ou máxima do revestimento; vmin < 1,0 m/s
(DNIT, acúmulo de terra); Imin < 0,005; h + f > altura útil.

Ouro (Tabela 8): b 0,3; z 0,6; n 0,014; Q 0,1034; Imin 0,005; Imax 0,0434 →
[hmax 0,207; vmax 2,58]. Q 0,2343; Imax 0,0472 → [hmax 0,327; vmax 3,33].

## 6. Bordo livre e altura adotada

Bordo livre `f` pela Tabela 36 do DNIT (valetas revestidas), por Q em m³/s:
até 0,25 → 10 cm; 0,25–0,56 → 13; 0,56–0,84 → 14; 0,84–1,40 → 15;
1,40–2,80 → 18; acima → 20.
Altura adotada sugerida: `y = h + f` arredondada **para cima** em 0,05 m.

Ouro (Tabela 9): hmax 0,207 → [y 0,35; x 0,21; B 0,72]. Com y dado = 0,50 →
[x 0,30; B 0,90].
Atenção: na canaleta 2, 0,327 + 0,10 = 0,427, que vira 0,45 pela regra; o
memorial adotou 0,50.
**Pergunta aberta 1** (ver PROGRESSO): arredondar para 0,05 ou para 0,10?

## 7. Dissipador de energia (DNIT IPR 736, desenho 1.19, DES)

`L1 = B` (abertura da canaleta); `L2 = 3·L1`; comprimento = `2·L1`.
Ouro (Tabela 11): B 0,72 → [L2 2,16; comprimento 1,44]; B 0,90 → [2,70; 1,80].
(No memorial o cabeçalho diz cm, mas os valores estão em metro.)

## 8. Transposição (DNIT IPR 736, desenho 1.9, TSS)

Escolha do modelo TSS pela abertura `B` contra a tabela 12 do memorial
(B < 0,92 m → TSS 120). A tabela de modelos entra como lista editável no passo 31.6.

## 9. Tabelas de apoio (editáveis, com padrão)

- Velocidade máxima por cobertura (Tabela 31 do DNIT): grama comum firmemente
  implantada 1,50–1,80; tufos de grama com solo exposto 0,60–1,20; argila
  0,80–1,30; argila coloidal 1,30–1,80; lodo 0,35–0,85; areia fina 0,30–0,40;
  areia média 0,35–0,45; cascalho fino 0,50–0,80; silte 0,70–1,20; alvenaria
  de tijolos 2,50; concreto de cimento portland 4,50; aglomerados resistentes
  2,00; revestimento betuminoso 3,00–4,00.
- C do método racional (Tabela 39 do DNIT): lista com faixa e valor adotado.
- n de Manning (Porto): lista por revestimento.

## 10. Inconsistências do memorial (anotar, não copiar)

- Tabela 11 diz cm e os valores estão em m.
- O texto do 8.2 chama o C de "coeficiente de Manning"; é o de escoamento.
- Tabela 5 só tem bacias 1 e 2; a bacia 3 aparece na Tabela 1 sem canaleta.
- Texto do bordo livre cita as equações 13/14, mas o valor usado é o da Tabela 36.
- Altura adotada da canaleta 2 (ver item 6).
