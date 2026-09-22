# Etapa 1: Seção Terreno

Objetivo: escolher a superfície TIN, processar, consultar cota rápido.

### 1.1 Modelo de TIN puro (Geo)
Classes `Tin`, `Triangle`, `Point3` em `UFV.Geo`. Consulta `TryGetZ(x, y)` por interpolação no triângulo. Devolve falso fora do terreno.
Testes: plano inclinado analítico (Z exato), ponto em vértice, em aresta, fora.

### 1.2 Índice espacial
Grade uniforme (ou quadtree) sobre os triângulos. `TryGetZ` usa o índice.
Testes: mesmo resultado com e sem índice em 10 mil pontos aleatórios; teste de desempenho com malha sintética de 2 milhões de pontos: construção < 10 s, 100 mil consultas < 1 s (limites a ajustar com o Renan).

### 1.3 Listar superfícies
Botão **Terreno** abre modal listando as `TinSurface` do desenho (nome, nº de pontos).
**Validação do Renan:** abre um desenho real com várias superfícies e vê todas listadas.

### 1.4 Processar superfície
Ao confirmar, extrai triângulos para o `Tin` do Geo, guarda em cache. Mensagem com resumo: nome, nº de triângulos, cota mín/máx, área.
**Validação do Renan:** confere o resumo contra as propriedades da superfície no Civil 3D.

### 1.5 Identidade e carimbo
Registrar app `MARCHENG_UFV`, criar dicionário nomeado central, gravar qual superfície foi processada e sua data de modificação (carimbo de proveniência).
Testes: reabrir o desenho recupera o registro; superfície modificada depois é detectada.

### 1.6 Coordenada geográfica
Obter latitude e longitude a partir do sistema de coordenadas do desenho. Se o desenho não tiver sistema definido, pedir ao usuário. Gravar no dicionário.
**Validação do Renan:** confere lat/long de um projeto conhecido.

### 1.7 Botão Obter Coordenada
Liberado só com superfície processada. Clica na tela, devolve X, Y, Z; avisa se caiu fora do terreno. Sem declividade.
**Validação do Renan:** clica em pontos conhecidos e compara com o Civil 3D.

Acervo da etapa: um terreno sintético pequeno e um trecho de terreno real, com cotas conferidas à mão em pontos escolhidos pelo Renan.
