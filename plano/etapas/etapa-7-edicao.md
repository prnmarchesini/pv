# Etapa 7: Edição depois de gerado

### 7.1 Estado sujo
Cada mesa tem estado limpo/sujo gravado no XData. Suja: pinta de vermelho.

### 7.2 O vigia
Escuta eventos do banco (objeto modificado, apagado, adicionado) e fim de comando. Mover, apagar ou copiar mesa: só marca e pinta. Nada mais.
Testes de nível 2: MOVE via script suja a mesa; ERASE registra a remoção.

### 7.3 Recalcular mesa
Item no menu de botão direito sobre mesa. Reamostra e refaz pilares e pontas baixas daquela mesa.

### 7.4 Recalcular tudo
Botão que refaz só as mesas sujas.

### 7.5 Cópia
Detecta GUID duplicado, dá identidade nova à cópia e a cada pilar e módulo dela, marca como suja. Renomear os blocos para o padrão do plugin só pelo comando **Renomear**.

### 7.6 Apagar e recontar
Detecta remoção (inclusive por Delete puro). Botão **Recontar** refaz listas e potência. Buraco na numeração fica até o comando de numerar.

### 7.7 Validação
Ao abrir o desenho, e pelo botão **Validar**: confere se o registrado existe, se algo mudou de posição, GUID duplicado, carimbo da superfície. Indica o que achou.

### 7.8 Auto-seleção
Ao selecionar mesas (arrastando ou clicando), caixa flutuante semitransparente com o kWp da seleção.

### 7.9 Grupos e painel de informações
Formar grupo com nome a partir da seleção. Painel lista grupos com nº de mesas, módulos, pilares e kWp. Recalcular por grupo.

### 7.10 Numeração
Seção própria, botão **Gerar numeração**. Fileira = mesas contínuas na mesma reta e mesmo azimute; espaçamento acima do limite ou azimute diferente abre fileira nova. Usuário indica a F1 e a última. Letreiros F1, F2... e mesas F1.1, F1.2...; módulos e pilares numerados em consequência.
**Validação do Renan:** confere a numeração na tela antes de fechar.
