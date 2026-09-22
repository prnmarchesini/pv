# Regras sagradas

Resultado que fere qualquer uma destas regras é BUG, não caso de análise. Cada regra vira um verificador automático em `UFV.Core.Invariants`, rodado sobre toda saída do motor em todo teste (ver `04-testes.md`).

## 1. Pilar nunca flutua
Todo pilar tem parte abaixo da superfície (embutimento > 0) e parte acima (altura livre > 0). Verificador: `embutimento > 0 && alturaLivre > 0` para todo pilar.

## 2. Mesa é monolito, nunca entorta
A mesa é corpo rígido. Todos os módulos e todos os topos de pilar de uma mesa estão no mesmo plano. Verificador: distância de cada vértice ao plano da mesa < 1 mm.

## 3. Toda entidade é única e carrega atributos
Pilar, módulo e mesa são objetos únicos com GUID próprio. Verificador: nenhum GUID repetido; soma das potências dos módulos = potência reportada; contagem de pilares = contagem da lista de compra.

## 4. A ponta baixa manda, o pilar é consequência
A faixa da ponta baixa do MÓDULO (não do ferro) até o terreno é respeitada. O que cede é o pilar, que pode estourar e é marcado.

**Exceção única:** tolerância de invasão por lombo. O usuário define quantos módulos por mesa podem estourar a ponta baixa (ex.: 5 em 20). Acima disso a mesa é marcada. Verificador: para cada mesa, módulos fora da faixa ≤ tolerância, ou a mesa está marcada.

## Princípios de comportamento (também invioláveis)

- O motor nunca move nem quebra fileira sozinho. Encaixa o máximo, marca o resto.
- Alinhamento entre mesas primeiro; pilar é o que sobra.
- O vigia só marca e pinta. Renomear e renumerar só por comando do usuário.
- Todo resultado carrega carimbo de proveniência (superfície + data). Superfície sumiu ou mudou: resultado desatualizado.
