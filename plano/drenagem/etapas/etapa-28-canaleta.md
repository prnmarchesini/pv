# Etapa 28: Desenho › Canaleta

Pedido do Renan: transformar as linhas de análise em canaleta, escolhendo
meia-cana (raio) ou trapezoidal (dimensões), com "um menu legalzinho".
Interseções ficam para depois (fora de escopo, anotado).

### 28.1 Biblioteca de seções (Core)

`ChannelSection` com dois tipos:
- **Trapezoidal**: b, z (ou x), y, e (Figura 7 do memorial);
- **Meia-cana**: r, e.
Revestimento (concreto, grama…) com n de Manning e velocidade máxima. Seções
salvas por nome no projeto (dicionário `CLIVUS`), com padrão "Trap 30/60"
(b 0,30; z 0,6; y 0,35; e 0,10; concreto) e "Meia-cana 30" (r 0,15; e 0,08).
Geometria da seção testada contra a Tabela 9 (x e B).
Fecha automático.

### 28.2 Janela da canaleta

Seleciona uma ou mais linhas de análise. Escolhe o tipo, e a janela mostra
**o desenho da seção cotado** que muda ao digitar (como a Figura 7). Campos só
do tipo escolhido. Revestimento. Assentamento: borda no terreno (padrão; fundo
= terreno − y) ou fundo com declividade mínima garantida (ver 28.4).
Converter: a linha vira canaleta (`CLIVUS_DRN_CANALETA`), XData com a seção.
**Validação do Renan:** cria uma de cada tipo e confere a seção e o XData.

### 28.3 Perfil da canaleta

Mesmo gráfico do 27.3, com duas linhas: terreno e fundo da canaleta (e topo
da parede tracejado). Por trecho: "% | grau" do fundo; cotas do fundo nas
estacas. Trecho com declividade < 0,005 em amarelo e invertido em vermelho
(regra 4), listados na janela.
**Validação do Renan:** cria uma canaleta numa área plana e vê o alerta.

### 28.4 Fundo com declividade mínima (opcional)

Em vez de copiar o terreno, o fundo desce sempre pelo menos 0,5 %: onde o
terreno não deixa, a canaleta aprofunda (e a escavação a mais aparece no
perfil). Nunca sobe. É a mesma ideia de "nunca afundar a mesa", ao contrário.
**Pergunta aberta 2:** o Renan quer isso já, ou só o alerta do 28.3?
**Validação do Renan:** liga a opção numa área plana e confere o fundo.

### 28.5 Editar e desfazer

Duplo clique (ou botão) reabre a janela com a seção da canaleta; trocar a
seção refaz 3D e perfil. "Voltar a linha" desfaz a canaleta. Tudo com UNDO.
**Validação do Renan:** troca uma trapezoidal por meia-cana e volta.
