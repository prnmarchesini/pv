# Etapa 30: Memória › Bacias (zonas de influência)

Pedido do Renan: "qual pedaço de terra vai mandar água para qual canaleta?",
marcado em projeto com hatch, e daí a lista canaleta × área. No memorial isso
foi feito à mão (Figura 5); aqui sai do terreno.

### 30.1 Cálculo das bacias (Geo)

`Watershed`: grade fina sobre a área analisada (padrão 2 m). De cada célula,
`FlowTracer` desce até tocar uma canaleta → a célula é daquela canaleta.
Parou fora do TIN, num fundo, ou saiu da área → célula "sem destino", com o
motivo. Células de cada canaleta viram polígono (Clipper2, união e
simplificação). Do mesmo traçado saem, por bacia: área, `L` (maior caminho
até a canaleta), `ΔH` e `S` para o item 1 de `02-calculos-hidraulicos.md`.
Testes: rampa com uma canaleta no pé → bacia = área toda; telhado de duas
águas com uma canaleta de cada lado → divide na cumeeira; soma das áreas +
sem destino = área analisada (tolerância de uma célula).
Fecha automático.

### 30.2 Desenho das bacias

Hatch por bacia, cor da canaleta, transparente, layer `CLIVUS_DRN_BACIA`, com
rótulo "Bacia 1 · 13 289 m²". Área sem destino hachurada em
`CLIVUS_DRN_ALERTA`, listada com o motivo (regra 4).
**Validação do Renan:** roda na Valparaiso com as duas canaletas do memorial e
compara com a Figura 5.

### 30.3 Ajuste manual

O Renan pode redesenhar o contorno de uma bacia (grip no hatch) ou desenhar
uma polyline e dizer "esta área vai para esta canaleta". Ajuste manual é
respeitado no recálculo e marcado como manual na lista.
**Validação do Renan:** move um contorno e confere que a área muda e fica.

### 30.4 Lista canaleta × bacia

Tabela na janela: canaleta, bacias, área total (m² e ha), L, ΔH, S, manual
ou automática. Clicar numa linha dá zoom e destaca.
**Validação do Renan:** confere a lista contra o desenho.
