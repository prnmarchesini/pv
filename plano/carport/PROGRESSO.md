# Progresso do carport

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

**Plano escrito em 28/10/2026. Decisões do Renan no `LEIA-PRIMEIRO.md`
(fila contínua, altura livre fixa, tudo com lista de material). O
Renan aprovou o plano em 28/10/2026 ("esta ok"), com a regra 4: o carport é só
mais um tipo em Configurações › Nova estrutura, e nada da fixa muda.**

## Etapa 34: o modelo

- 34.1 Perfil do carport: VALIDADO (automático) — `CarportProfile`, 22 testes
- 34.2 Geometria de um pórtico e de um vão: PENDENTE
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
    0,51 a 0,63). Mão francesa: 0,62 m no pilar e 0,77 m na viga (média da da frente e
    da de trás). Diferença até 7 cm do `.glb`; o teste de ouro do 34.2 usa essa tolerância
    para essas peças e 1 cm para os módulos.
  - A potência do módulo do padrão (600 W) não está no `.glb`: é para trocar pelo módulo
    do projeto na janela.
  - Dados elétricos do módulo (PAN) não entram no perfil do carport agora; entram com
    versão nova do formato quando a etapa 36 precisar (strings e PVsyst).
