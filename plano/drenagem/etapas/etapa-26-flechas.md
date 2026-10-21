# Etapa 26: Análise › Flechas

Pedido do Renan: "o AutoCAD Civil pega o terreno e coloca as flechinhas
indicando o sentido da água, teria como replicar?". Sim, e já pronto, sem
configurar estilo de superfície. Referência visual: Figura 3 do memorial.

### 26.1 Janela das flechas

Janela compacta (regra de 02/10: rótulo ao lado, nada de espaço vazio):
- onde: área do projeto (polyline `CLIVUS_AREA`), janela retangular ou tudo;
- espaçamento da grade (padrão 5 m);
- faixas de cor (padrão do memorial: 0–5 % verde, 5–10 % laranja,
  10–15 % vermelho, 15–20 % roxo, > 20 % azul-escuro), editáveis;
- mostrar texto do valor em cada flecha: sim/não (padrão não);
- Gerar, Apagar.
**Validação do Renan:** abre a janela e confere os padrões.

### 26.2 Desenho das flechas

Uma flecha por ponto da grade dentro do TIN, apontando a descida (`TinSlope`),
tamanho fixo proporcional ao espaçamento, cor pela faixa, sentada na cota do
terreno. Ponto plano (declividade < 0,1 %) vira um pontinho, não flecha.
Desenhar em lote numa transação só; 100 mil flechas não podem travar o CAD
(medir e registrar o tempo no relatório).
Regerar apaga as flechas anteriores antes (XData, não layer).
**Validação do Renan:** gera no terreno da Valparaiso e compara com a Figura 3.

### 26.3 Legenda

Quadro de legenda com as faixas e cores no canto da área (reaproveitar
`DrawingTables`). Arrastável, regerado junto.
**Validação do Renan:** confere a legenda.
