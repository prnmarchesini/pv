# Etapa 29: canaleta em 3D

### 29.1 Sólido da canaleta

A seção (parede com espessura e) é varrida ao longo do eixo 3D do fundo.
Começar por um sólido por trecho reto (extrusão da região da seção entre duas
estacas, com corte em bissetriz nas quinas) em vez de `SweepOptions`, que
falha em polyline 3D com quina viva; se o teste em accoreconsole mostrar que o
sweep aguenta, trocar. Layer `CLIVUS_DRN_CANALETA_3D`, ligado ao GUID da
canaleta. Mudou a canaleta, o 3D se refaz.
Nível 2: volume do concreto e posição do fundo conferidos em JSON.
**Validação do Renan:** orbita uma canaleta trapezoidal e uma meia-cana.

### 29.2 Quantitativos

Por canaleta: comprimento 3D, volume de concreto (área da parede × comprimento),
volume de escavação (área da seção externa × comprimento, mais o aprofundamento
do 28.4). Vai para a Memória e para o Excel.
Fecha automático (número), com o nível 2 do 29.1.

### 29.3 No Ver 3D

As canaletas entram no `Publicador3D` (Ver 3D web) com cor própria.
**Validação do Renan:** abre o Ver 3D e acha as canaletas.
