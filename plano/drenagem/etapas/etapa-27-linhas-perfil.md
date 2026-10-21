# Etapa 27: Análise › Linhas e perfil

Pedido do Renan: "faço linha e surge gráfico". No Civil 3D isso é alinhamento +
perfil + vista de perfil + estilos + bandas; aqui é um comando só, já com layer
e um gráfico bom de ler.

### 27.1 Linha de análise

`CLIVUS_DRN_LINHA`: o Renan desenha a polyline na hora (como `PLINE`) ou
seleciona uma existente. Ela vai para `CLIVUS_DRN_LINHA`, ganha GUID e é
assentada no TIN (`Draping.Along`). Ponto fora do TIN: avisa e pinta.
**Validação do Renan:** desenha uma linha e confere que ela fica no terreno.

### 27.2 Amostragem do perfil (Geo)

`ProfileSampler`: estacas a cada X m (padrão 5 m, editável) + todo vértice +
toda aresta de triângulo cruzada (para não perder quebra do terreno). Para cada
estaca: distância acumulada, cota, declividade do trecho em % e grau.
Montante é a ponta mais alta; a distância conta dela (regra 3).
Fecha automático.

### 27.3 Gráfico do perfil (Core + Plugin)

`ProfileChart` monta o desenho em coordenadas de papel; o Plugin só desenha.
Conteúdo padrão:
- linha do terreno; grade com cota à esquerda e distância embaixo;
- exagero vertical automático (padrão 10×, editável) com a escala escrita;
- por trecho de declividade constante: texto "4,3 % | 2,5°" sobre o trecho,
  com seta do sentido da água;
- banda inferior com estaca, cota do terreno e distância;
- título com o nome da linha, comprimento, cota máxima e mínima,
  declividade média.
O Renan clica onde o gráfico fica. Layer `CLIVUS_DRN_PERFIL`. Texto horizontal.
**Validação do Renan:** desenha uma linha num morro e confere o gráfico.

### 27.4 Gráfico vivo

Gráfico ligado à linha pelo GUID. Mexeu na linha (grip, move), o gráfico se
refaz no mesmo lugar ao fim do comando do CAD. Apagou a linha, o gráfico avisa
órfão e some no próximo `Atualizar`.
**Validação do Renan:** arrasta um vértice e vê o gráfico mudar.
