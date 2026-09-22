# Etapa 3: Botão Desenhar Estrutura

### 3.1 Módulo e biblioteca
Modelo `Modulo` (marca, modelo, potência, altura, largura, espessura). Biblioteca de modelos padrão em JSON + opção livre.

### 3.2 Comprimento da mesa
Comprimento = nº módulos × largura + nº espaçamentos horizontais × espaçamento + sobra esquerda + sobra direita. Arranjo 1V ou 2V (2V = duas fileiras, espaçamento vertical próprio).
Testes: exemplo de 26 módulos com números do Renan.

### 3.3 Tabela de pilares
Distâncias acumuladas do zero da estrutura, vãos desiguais. Um pilar por posição. Validação: fecha com o comprimento.
Testes: vãos 3, 3, 3, 4, 3; tabela que não fecha dá erro claro.

### 3.4 Geometria local da mesa
Mesa em coordenadas locais: módulos como caixas (largura, altura, espessura), face superior de cada módulo como entidade separada, pilares como caixas retangulares (dois campos, ex.: 0,15 × 0,07 m). Posição do pilar na tesoura. Tesoura menor que o módulo.
Testes: regra sagrada 2 (tudo coplanar), contagens.

### 3.5 Fórmula da altura livre
altura livre = cota da ponta baixa do módulo + distância do pilar ao longo da tesoura × sen(tilt), com a ponta baixa medida no MÓDULO.
Testes: tesoura 4 m a 10°: 0,30 / 0,647 / 0,995 m nas posições 0, 2 e 4 m.

### 3.6 Perfil nomeado
Salva e carrega perfis ("Mesa do Renan 26 módulos") em JSON.

### 3.7 Modal
Modal com todos os campos, mostra comprimento calculado e planta baixa da mesa com as sobras.
**Validação do Renan:** monta uma mesa real que ele conhece e confere comprimento e posição dos pilares com o projeto do fabricante.
