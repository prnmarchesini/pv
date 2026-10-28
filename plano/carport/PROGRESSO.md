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
- 34.3 A fila: PENDENTE
- 34.4 Lista de material: PENDENTE

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
