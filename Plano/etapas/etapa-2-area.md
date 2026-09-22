# Etapa 2: Seção UFV, botão Área

### 2.1 Drapeamento puro (Geo)
Dada uma polilinha 2D e o `Tin`, devolve polilinha 3D: cota em cada vértice e vértices extras onde a linha cruza aresta de triângulo.
Testes: linha reta sobre um calombo (a corda não pode atravessar o morro); linha sobre plano não ganha vértices desnecessários; vértice fora do terreno é reportado.

### 2.2 Identidade da área
Grava XData na polilinha: tipo "Area", GUID, nome, parâmetros. Registra no dicionário central.
Testes: GUID sobrevive a salvar e reabrir.

### 2.3 Comando Área
Usuário desenha em planta, dá nome, o plugin drapeja e confirma OK.
**Validação do Renan:** orbita em 3D e vê a linha seguindo o terreno.

### 2.4 Reindexar
Comando que varre o desenho, acha entidades com XData `MARCHENG_UFV` e reconstrói o dicionário central. Roda sozinho ao abrir quando achar entidade sem registro.
**Validação do Renan:** copia uma área para outro desenho e vê ela reconhecida lá.
