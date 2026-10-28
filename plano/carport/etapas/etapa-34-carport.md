# Etapas 34 a 36: carport

## Etapa 34: o modelo (Core, sem CAD)

### 34.1 Perfil do carport

`CarportProfile`: módulo, retrato ou paisagem, módulos na descida, módulos por
vão, folgas, inclinação, altura livre da borda baixa, pilares (afastamento na
base e no topo, perfil), viga (balanços), terças (quantidade ou passo), mão
francesa (pontos de ligação), base. Padrões = `01-modelo-de-referencia.md`.
Arquivo `.clivuscarport.json` com versão, como o perfil da mesa (versão
diferente é recusada com o motivo). Validação com teto contra erro de escala.
Fecha automático.

### 34.2 Geometria de um pórtico e de um vão

`CarportGeometry`: dadas a cota do chão de cada pilar e a direção da fila, as
peças em 3D (eixo de cada barra com seção, as faces dos módulos, as bases).
Altura livre fixa: a borda baixa fica a `altura livre` do chão **do pórtico**
(o mais alto dos dois pilares dele manda, para nenhum ponto ficar abaixo).
Teste de ouro: o vão padrão em chão plano reproduz o `.glb` (tabela do
modelo de referência, tolerância 1 cm nos módulos e 7 cm nas terças e mãos
francesas, ver PROGRESSO). Confere também que a viga alcança o topo dos dois
pilares (a regra que saiu do perfil no 34.1 por depender da geometria).
Fecha automático.

### 34.3 A fila

`CarportRow`: eixo (dois pontos), número de vãos ou "encher até o fim", lado
das vagas (para onde a água cai). Pórticos nas divisas, N vãos → N + 1
pórticos. Em terreno com desnível, as terças e os módulos de cada vão vão de um
pórtico ao outro em linha reta. Avisos: desnível entre pórticos acima de um
limite (padrão 5 %), pilar mais curto que a mão francesa.
Fecha automático.

### 34.4 Lista de material

`CarportBill`: por fila e total: pilares (comprimento de cada um), vigas,
terças (emendas no pórtico), mãos francesas, chapas de base, blocos, módulos,
peças pequenas por vão. Excel no padrão do `QuantityReport`.
Fecha automático.

## Etapa 35: no Civil 3D

### 35.1 Nova estrutura: Fixa ou Carport

Configurações › Estruturas: "Nova mesa..." passa a "Nova estrutura..." e
pergunta o tipo. Fixa → a janela de Mesa de hoje, sem mudança. Carport → a
janela do carport. Os carports do desenho entram na mesma lista com
"(carport)"; Editar, Duplicar e Remover valem para os dois tipos, cada um na
sua janela. Carport fica fora da Escolha das estruturas (é do motor da fixa).
Guardado à parte no desenho (`DrawingCarports`, chave própria), sem tocar no
formato das mesas.

### 35.2 Janela do carport

Perfil do carport editável (seções em colunas, rótulo ao lado, sem rolagem à
toa), salvar e abrir perfis, desenho de corte do pórtico ao vivo com as cotas
(altura livre, ponto mais baixo).
**Validação do Renan:** Configurações › Estruturas › Nova estrutura… › Carport,
mudar altura e inclinação e ver o corte; Salvar no desenho e reabrir.

### 35.3 Desenhar a fila

Comando `CLIVUS_CARPORT`: clicar início e fim do eixo, escolher o lado da
caída, quantos vãos (ou encher). Desenha módulos (faces, como a mesa fixa),
pilares, vigas, terças, mãos francesas e bases em sólidos 3D nas camadas
`CLIVUS_CARPORT_*`, tudo na cota do TIN. XData com a fila, o vão e o pórtico.
**Validação do Renan:** desenhar uma fila no estacionamento com desnível,
orbitar e conferir que a borda baixa está a 2,35 m do chão em cada pórtico.

### 35.4 Editar, regerar e apagar

Botão direito na fila: mudar vãos, trocar o perfil, regerar (relê o terreno),
apagar a fila inteira. Nada muda sem comando.
**Validação do Renan:** cada uma das quatro.

### 35.5 Lista de material na tela

Botão "Lista de material" do carport: a tabela na janela e o Excel.
**Validação do Renan:** abrir o Excel e conferir com a fila desenhada.

## Etapa 36: o carport no resto do Clivus

### 36.1 O vão como mesa

Levantar o que lê mesa pelo `LayoutScan` (contagem, potência, numeração,
strings, sombras, PVsyst, 3D web, Exportar para o TermoVision) e fazer o vão
do carport aparecer como mesa onde faz sentido. O que não servir sem mudança
vira um passo 36.x próprio, anotado aqui antes de começar.
