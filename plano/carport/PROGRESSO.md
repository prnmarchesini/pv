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

- 35.1 Nova estrutura: Fixa ou Carport: PENDENTE
- 35.2 Janela do carport: PENDENTE
- 35.3 Desenhar a fila: PENDENTE
- 35.4 Editar, regerar e apagar: PENDENTE
- 35.5 Lista de material na tela: PENDENTE

## Etapa 36: o carport no resto do Clivus

- 36.1 O vão como mesa: PENDENTE

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
