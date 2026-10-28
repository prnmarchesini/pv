# Progresso do carport

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

**Plano escrito em 28/10/2026. Decisões do Renan no `LEIA-PRIMEIRO.md`
(fila contínua, altura livre fixa, tudo com lista de material). O
Renan aprovou o plano em 28/10/2026 ("esta ok"), com a regra 4: o carport é só
mais um tipo em Configurações › Nova estrutura, e nada da fixa muda.**

## Etapa 34: o modelo

- 34.1 Perfil do carport: VALIDADO (automático) — `CarportProfile`
- 34.2 Geometria de um pórtico e de um vão: VALIDADO (automático) — `CarportGeometry`, teste de ouro contra o `.glb`
- 34.3 A fila: VALIDADO (automático) — `CarportRow`
- 34.4 Lista de material: VALIDADO (automático) — `CarportBill`, Excel em três abas

## Etapa 35: no Civil 3D

- 35.1 Nova estrutura: Fixa ou Carport: AGUARDANDO VALIDAÇÃO
- 35.2 Janela do carport: AGUARDANDO VALIDAÇÃO
- 35.3 Desenhar a fila: AGUARDANDO VALIDAÇÃO — CLIVUS_CARPORT (ribbon Processar › Carport › Desenhar fila)
- 35.4 Editar, regerar e apagar: AGUARDANDO VALIDAÇÃO — Regerar filas e Apagar fila (editar = mudar o carport nas Configurações e Regerar)
- 35.5 Lista de material na tela: AGUARDANDO VALIDAÇÃO — Lista de material (Excel)

## Etapa 36: o carport no resto do Clivus

- 36.1 O vão como mesa: AGUARDANDO VALIDAÇÃO — 3D do Clivus (página e servidor) e Exportar para o TermoVision; strings, numeração, sombras e PVsyst ainda não leem o carport

## Observações

- Vão padrão = largura dos módulos do vão (5,667 m), não os 5,50 m do `.glb`
  (ver "O que o modelo não diz"). Confirmar com o Renan.
- 34.1 (28/10/2026), o que assumi:
  - Medidas do padrão tiradas do `.glb` no plano inclinado: módulo 2,278 × 1,134 × 0,030;
    folga 0 entre colunas (os quadros encostam) e 0,010 na descida; pilar aberto 15°;
    bases a 2,425 m; recuos da viga 0,549 (baixa) e 0,503 (alta); seções 2 × C 130×50
    (pilar), 2 × C 110×50 (viga), C 127×50 (terça), 2 × C 75×25 (mão francesa);
    bloco 0,092 acima do chão com chapa de 0,31 × 0,006.
  - Terças: 2 por módulo, a 0,57 m de cada ponta (média do `.glb`, que não é simétrico:
    0,51 a 0,63). Mão francesa: 0,62 m no pilar; na viga, 0,84 (pilar alto) e 0,71
    (pilar baixo), em dois campos, porque no `.glb` as duas são diferentes (mudança
    feita no 34.2; a média errava 8 cm).
  - A potência do módulo do padrão (600 W) não está no `.glb`: é para trocar pelo módulo
    do projeto na janela.
  - Dados elétricos do módulo (PAN) não entram no perfil do carport agora; entram com
    versão nova do formato quando a etapa 36 precisar (strings e PVsyst).
- 34.2 (28/10/2026), o que assumi:
  - Teste de ouro em chão plano no lugar do `.glb`: módulos a 1 cm (borda baixa
    2,345; borda alta, em cima do quadro, 3,21; Y de -1,716 a 5,088); bases, topos
    dos pilares e viga a 3 cm; terças e mãos francesas a 8 cm (a mão francesa é
    conferida no eixo do pilar; no `.glb` ela encosta na face). O ponto mais baixo
    sobre o chão sai 1,70 m, na mão francesa do pilar baixo, como no modelo.
  - As bases ficam 2,4 cm para o lado baixo do meio da cobertura (`PillarOffset`
    -0,024), como no `.glb`.
  - Embaixo do módulo vem a terça, encostada nele, e a viga encostada na terça; no
    `.glb` a viga fica 2 cm mais alta (dentro da tolerância).
  - Altura livre no pilar mais alto do pórtico; o outro pilar cresce o desnível.
    Entre dois pórticos com desnível, a cobertura do vão é um plano só.
  - Sem balanço nas pontas da fila: a cobertura termina no eixo do pórtico da ponta.
- 34.3 (28/10/2026), o que assumi:
  - O eixo clicado é a linha do meio da cobertura; o primeiro pórtico fica no
    primeiro clique. Sem número de vãos, enche o eixo (folga de 1 mm para a medida
    exata); com número, o segundo clique só dá o sentido.
  - Desnível entre pórticos vizinhos acima de 5 % vira aviso, não recusa.
  - Teto de 200 vãos por fila, contra clique errado.
- 34.4 (28/10/2026), o que assumi:
  - A lista sai das mesmas peças do desenho; barras agrupadas pelo comprimento de
    corte arredondado ao centímetro; terça cortada a cada vão (emenda no pórtico).
  - Grampos: em cada linha de terça, um intermediário entre módulos vizinhos e um de
    ponta em cada ponta da fila. Parafusos e ligações não entram (dependem do
    fornecedor); o Excel diz isso numa nota.
  - Excel com três abas: resumo, peças (comprimento × quantidade) e um pilar por
    linha com a cota do chão, para a obra.
- Revisão da etapa 34 por subagente (28/10/2026): 6 defeitos, todos corrigidos,
  cada um com teste:
  1. mão francesa na viga sem teto (840 em vez de 0,84 passava): agora até meia
     cobertura, e o pórtico recusa a que cai fora da viga;
  2. posição do módulo lida pelo conversor do .NET aceitava "1" e minúscula:
     agora só o nome exato;
  3. desnível grande entre as bases do pórtico: a fila avisa acima de 0,5 m com o
     comprimento do pilar, e a recusa aponta o chão como causa;
  4. a altura livre agora vale também sobre o chão embaixo da borda baixa (o chão
     que sobe para o lado baixo encostava na borda);
  5. com desnível ao longo da fila os módulos saíam tortos (89,3° a 10 %) e com
     fresta: agora retângulos, as colunas dividem o vão inclinado e as terças vão
     de viga a viga;
  6. distância da terça NaN com uma terça só passava na validação.
  E das observações: cantos sempre anti-horários em planta (borda baixa primeiro),
  corte arredondado para CIMA ao centímetro, 5 % exatos não avisam, piso de 0,5 m
  entre as bases, e a janela ajusta a terça ao trocar para paisagem.
- 35.1 e 35.2 (28/10/2026), o que assumi:
  - O botão "Nova mesa..." virou "Nova estrutura..." (pedido do Renan); escolhendo
    Fixa, segue exatamente o caminho de antes. Os carports ficam no registro
    `CARPORTS`, separado do `MESAS`; o nome não se repete entre mesa e carport.
  - Os carports aparecem na lista depois das mesas, sem prioridade (não entram no
    motor nem na Escolha das estruturas). Subir e Descer não valem para eles.
  - Na janela, o módulo pode vir de uma mesa do desenho ou ser digitado; não há
    biblioteca de carports na pasta do usuário, só "Abrir arquivo..." e
    "Salvar arquivo..." (.clivuscarport.json).

## Roteiro de tela para o Renan (35.1 e 35.2)

1. Com o Civil 3D fechado, instalar (`tools\publicar-bundle.ps1` e depois
   `tools\instalar.ps1`) e abrir um desenho que já tem mesas.
2. Configurações › Estruturas: o botão agora é "Nova estrutura...". Clicar,
   escolher **Fixa** e conferir que abre a janela de Mesa de sempre (cancelar).
3. "Nova estrutura..." › **Carport**: abre a janela do carport com o biposte de
   referência e o corte do pórtico. Mudar a altura livre e a inclinação e ver o
   corte e o resumo mudarem. Trocar para Paisagem e voltar.
4. "Usar": o carport aparece na lista, depois das mesas, com "carport".
   "Salvar no desenho", fechar e abrir de novo as Configurações: o carport continua lá.
5. Editar, Duplicar e Remover com o carport escolhido.
6. A aba "Escolha das estruturas" e o resto das mesas continuam iguais.
- Primeira tela do Renan (28/10/2026), três pedidos atendidos no mesmo dia:
  - "não consigo subir na lista" (carport escolhido): Subir e Descer agora movem
    o carport entre os carports (eles ficam depois das mesas; a prioridade das
    mesas é do motor).
  - "não aparece a opção do carport na escolha das estruturas": os carports
    entram na aba, com setas, marcar e cor. Renan escolheu (pergunta de 28/10):
    o marcado é o que o desenho do carport usa; o motor das mesas continua só
    com mesas. Registro `CARPORTS` passou à versão 2 (com o "usar"); a versão 1,
    que o Renan já tinha gravado, ainda é lida com todos marcados.
  - Azimute por linha de apoio (Parâmetros › "Pela linha no desenho..."): esconde
    a janela, pede dois pontos com elástico, nada fica no desenho; a mesa olha para
    a perpendicular mais perto do azimute atual (linha subindo 20° → 340°, "20°
    para oeste do norte"). `FacingFromLine` no Core, com testes.
- 28/10/2026 (tarde), "FAÇA TUDO": 35.3 a 36.1 feitos de uma vez.
  - CLIVUS_CARPORT: escolhe o carport marcado, eixo (meio da cobertura), lado da
    água, vãos (Enter enche). Tudo em sólidos: módulos com a espessura deles em
    azul, aço na cor do carport, bases em concreto; XData "Carport" com o id da
    fila em cada peça; a fila registrada em `CARPORT_FILAS` (CarportRows).
  - CLIVUS_CARPORT_REGERAR, CLIVUS_CARPORT_APAGAR (clica uma peça), CLIVUS_CARPORT_LISTA
    (Excel). Ribbon: Processar › Carport (menu com os quatro).
  - 3D: a fila entra na página 3D (lista "canaletas", triângulos) e no servidor
    (visualizador do app desenha a lista desde clivussolar 1e45366; contrato:
    campo opcional `canaletas`).
  - TermoVision: cada vão vai como mesa no Exportar (CarportReturn, id fixo por vão).
  - Conferido no Core Console com o Paradise.dwg do Renan (cópia): fila de 58,6 m,
    9 vãos, 10 pórticos, 135 módulos (o carport dele, com Risen), 279 peças, pilares
    de 2,30 a 2,97 m pelo chão; regerar, lista, 3D (3348 triângulos, foto conferida)
    e o retorno do TermoVision (9 mesas, borda baixa primeiro).
  - Não feito ainda: strings, numeração, sombras e PVsyst lerem o carport.
- Tela de 28/10 (Usina "nenhuma fileira cabe"): o Usina é o motor da fixa e não põe
  carport; a mensagem dizia que a área estava do outro lado do alinhamento "teste2".

## Roteiro de tela para o Renan (35.3 a 36.1)

1. Terreno escolhido; Configurações › Escolha das estruturas com o carport marcado.
2. Processar › Carport › Desenhar fila: clicar o começo e o fim do eixo (meio da
   cobertura), clicar do lado para onde a água cai, Enter. Orbitar: pilares em V,
   viga, terças, mãos francesas, bases, módulos.
3. Mudar algo do carport nas Configurações (ex.: altura livre), Salvar, e Carport ›
   Regerar filas.
4. Carport › Lista de material: abre o Excel.
5. Saída › 3D: o carport no navegador e o link publicado.
6. Saída › TermoVision › Exportar: os vãos chegam como mesas na área.
7. Carport › Apagar fila: clicar uma peça; a fila some.
- 28/10/2026 (fim da tarde), tela do Renan: "o gerar usina deveria funcionar, eu escolho
  a area, o alinhamento e ele enche normal" e "padronizar IGUALZINHO as mesas".
  - Usina e Regerar área: com carport marcado na Escolha das estruturas, a área enche
    de filas de carport pela MESMA distribuição das mesas (RowDistributor: alinhamento,
    lado, azimute e pitch do projeto; pegada = um vão, folga zero; pitch nunca menor que
    a cobertura). Mesa e carport marcados juntos: pergunta qual vai. A fila guarda a área;
    rodar de novo troca as filas daquela área. Conferido no Paradise do Renan: Área 2,
    alinhamento "teste": 3 filas, 19 vãos, 285 módulos (205,2 kWp).
  - Cada vão é grupo (um clique pega o vão, como a mesa) com contorno da cobertura na
    camada CLIVUS_CARPORT_MESA, na cor do carport. Aço sempre galvanizado (CorDoAco).
  - TermoVision: contrato combinado com a sessão de lá (aprovado pelo Renan): cada mesa
    leva "tipo" ("fixa" | "carport") e o carport leva "estrutura.triangulos". Exemplo
    mandado a eles (scratchpad/desenho/retorno-carport.json, 19 vãos). Lá ainda falta
    guardar e desenhar.
- 28/10/2026 (noite), Renan: "usei a ferramenta grupo e não mostra o kWp ... todas
  ferramentas tem que funcionar com a mesa" e "sairam grudadinhos, falta corredor".
  - Cada vão é MESA para o plugin: contorno com TableIdentity (rótulo C<fila>.<vão>,
    ModulePowerWatts, ProfileName = nome do carport, id = IdDoVao), módulos no bloco
    de módulo da fixa (ModuleIdentity) e face de cima (FaceIdentity). O aço continua
    com o XData "Carport". Grupo, Resumo, Recontar, kWp da seleção, Excel, strings,
    rota de cabos, sombras, tags, Estado e Validar leem o vão como mesa.
  - Quem refaz mesa FIXA pula o carport (LayoutScan.TablesSemCarport): Pintar
    análises, análises de ponta/pilar, Trocar mesa, Numerar (o rótulo C não é F),
    vizinhas do Recalcular. Pontas recusa com aviso. Recalcular / pendentes / Grupo ›
    Recalcular num vão refazem a FILA de carport dele. Regerar área e Apagar tudo
    limpam o aço da fila que perdeu os vãos.
  - Corredor entre filas no carport (Aisle, padrão 6 m, campo opcional no arquivo);
    o pitch do carport no Usina é cobertura + corredor.
  - Conferido no Paradise: Usina 2 filas, 12 vãos, 180 módulos; Recontar 12 mesas,
    180 módulos, 129,6 kWp; Pendente + Recalcular refez a fila; Validar limpo.
