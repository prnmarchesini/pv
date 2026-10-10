# Etapa 17: base comum das rotas

Tudo aqui é compartilhado pelas quatro abas (CC, Combiner, CA, MT). Construir uma vez, bem feito, e reusar. Cada aba tem os seus próprios valores, independentes.

### 17.1 Botão e abas

Botão **Rota de cabos** no menu. Abre o modal com abas: CC, Combiner, CA, MT.
Cada aba só fica habilitada quando os dois lados do trecho existem no desenho: CC precisa de string e inversor (ou string e combiner), CA precisa de inversor e trafo, MT precisa de trafo e subestação. Aba indisponível diz o que está faltando.
**Validação do Renan:** abre o menu num desenho sem trafo e confere que a aba CA avisa o que falta.

### 17.2 Layers e cores por rota

Cada rota tem layer própria e cor própria, tanto para a vala quanto para o cabo. Criadas pelo plugin se não existirem. Layer é aparência, nunca identidade.
**Validação do Renan:** confere as layers criadas e as cores de cada rota.

### 17.3 Profundidade da vala

Primeira seção de cada aba: profundidade da vala daquela rota (ex. 60 cm, 80 cm, 1 m). É o que leva o cabo para o 3D e entra no comprimento contado.
**Validação do Renan:** põe profundidades diferentes em CC e CA e confere que são independentes.

### 17.4 Seleção da vala

Segunda seção: botão de selecionar vala. Clica, o modal **fica oculto**, o cursor vira o quadradinho de seleção, o Renan vai clicando nas polylines que são vala daquela rota. Ctrl desseleciona. Uma caixa mostra ao vivo quantas linhas estão selecionadas. No Enter, o modal volta, informa quantas linhas foram selecionadas e **atribui automaticamente a layer de vala daquela rota** às polylines escolhidas.
A vala é polyline comum, desenhada à mão pelo Renan antes de abrir o menu. O sistema nunca cria nem altera o traçado dela.
**Validação do Renan:** seleciona um punhado de polylines, testa o Ctrl, confere a contagem na volta e a layer aplicada.

### 17.5 Percurso 3D e comprimento

Motor de percurso: dado um ponto de origem e uma vala, o cabo vai até a vala, desce à profundidade configurada, segue o traçado da vala e sobe no destino. O comprimento contado é o do percurso 3D real, descidas e subidas incluídas.
**Validação do Renan:** confere um lance conhecido medindo no CAD contra o número da tabela.

### 17.6 Lance como entidade

Cada linha de cabo é um lance com GUID próprio, comprimento próprio, vínculo com o elo de origem e de destino, e polaridade quando houver. XData em `CLIVUS`.
**Validação do Renan:** clica num cabo e confere os atributos dele.

### 17.7 Avisos e pintura

Mecanismo comum de aviso: toda vez que um trecho não puder ser roteado, o sistema lista o motivo e **pinta** as entidades envolvidas. Nunca falhar calado.
**Validação do Renan:** força um caso sem vala e confere o aviso e a pintura.

### 17.8 Recontagem antes de contar

Toda contagem recalcula antes de exibir, inclusive no simples ato de abrir o **Ver cabos**. Varre o desenho, detecta o que foi apagado com Delete puro por fora do plugin, acerta os números e pinta o que sumiu.
**Validação do Renan:** apaga um cabo com Delete, abre o Ver cabos e confere que a contagem já veio certa e a mesa foi pintada.

### 17.9 Apagar

Em cada aba: apagar tudo daquela rota, ou apagar escolhendo em campo quais trechos. Apagar cabo apaga só o cabo, nunca a string, a vala ou qualquer entidade de layout.
**Validação do Renan:** apaga alguns cabos pelo botão e confere que o resto do desenho ficou intacto.
