# Etapa 25: base da drenagem

### 25.1 Painel Drenagem na ribbon

Painel novo **Drenagem** na aba Clivus (`RibbonLayout.Tabs`), três grupos:
Análise (Flechas, Linhas), Desenho (Canaleta), Memória (Bacias, Memória).
Comandos `CLIVUS_DRN_FLECHAS`, `CLIVUS_DRN_LINHA`, `CLIVUS_DRN_CANALETA`,
`CLIVUS_DRN_BACIAS`, `CLIVUS_DRN_MEMORIA`. Ícones novos. Botão de passo ainda
não feito avisa "em construção". Teste em `RibbonLayoutTests`.
**Validação do Renan:** abre o Civil 3D e confere o painel, a ordem e os ícones.

### 25.2 Layers da drenagem

`CLIVUS_DRN_FLECHA`, `CLIVUS_DRN_LINHA`, `CLIVUS_DRN_PERFIL`,
`CLIVUS_DRN_CANALETA`, `CLIVUS_DRN_CANALETA_3D`, `CLIVUS_DRN_BACIA`,
`CLIVUS_DRN_ALERTA`. Cor de cada uma no padrão do `LayoutLayers`.
Fecha automático (só modelo).

### 25.3 Declividade e aspecto do TIN (Geo)

`TinSlope`: para cada triângulo, normal, declividade (m/m, %, grau) e direção de
descida em planta. `Tin` ganha `TryGetSlope(x, y)`. Testes nos terrenos
analíticos: plano = 0 e sem direção; rampa de 5 % = 5 % na direção certa;
gaussiana = direção radial para fora.
Fecha automático.

### 25.4 Caminho de escoamento (Geo)

`FlowTracer`: de um ponto, segue a descida mais íngreme triângulo a triângulo
até sair do TIN, cair num fundo (ponto sem descida) ou tocar um alvo (polyline
de canaleta, com tolerância). Devolve o caminho 3D, o comprimento e o motivo da
parada. Limite de passos para nunca travar.
Testes: rampa → reta na direção da queda; vale em V → cai no talvegue e segue
por ele; fundo de bacia fechada → para com motivo "fundo".
Fecha automático.
