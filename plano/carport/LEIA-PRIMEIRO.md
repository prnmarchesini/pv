# Clivus Solar: carport. Instruções para o Claude Code

> Plano escrito em 28/10/2026 a partir do pedido do Renan: *"hoje temos fixa,
> agora vamos projetar carport"*. Referência: `0 - Assets/Cobertura/`
> (`carport-biposte.glb`, o 3D completo de um vão, e duas fotos).
> Etapas **34 a 36**, branch `carport`.

Vale tudo que já valia: regras sagradas (`plano/01-regras-sagradas.md`),
camadas (`plano/02-arquitetura.md`), testes e acervo (`plano/04-testes.md`),
frases de tela em `Tr.T`/`Tr.F` com inglês e espanhol (etapa 10), e as
memórias "tudo que desenha acompanha o terreno" e "texto nunca virado".

Leia nesta ordem:

1. `01-modelo-de-referencia.md` (as medidas tiradas do `.glb`; viram o padrão)
2. `PROGRESSO.md`
3. `etapas/etapa-34-carport.md`

## Regra de ouro

Um passo por vez. Passo com coisa na tela fica `AGUARDANDO VALIDAÇÃO` e o
trabalho segue; passo só de modelo fecha `VALIDADO (automático)`.

## Decisões do Renan (28/10/2026)

1. **Fila contínua.** Os vãos se repetem lado a lado e dois vãos vizinhos
   dividem o pórtico, como na foto do pátio. N vãos → N + 1 pórticos.
2. **Altura livre fixa.** Cada pórtico assenta no chão da própria base (cota do
   TIN em cada pilar) e a borda baixa dos módulos fica sempre a 2,35 m (padrão,
   editável) do chão naquele pórtico. Em terreno com desnível ao longo da fila,
   o telhado acompanha; cada pilar sai com o comprimento que precisa.
3. **Tudo, com lista de material.** Módulos, pilares, vigas, terças, mãos
   francesas e bases em 3D, e a lista de peças com comprimentos. As medidas do
   `.glb` são o padrão; o Renan edita.

## O que é o carport no Clivus

Um tipo de estrutura novo, ao lado da mesa fixa. Ele não usa o motor de
fileiras da fixa (que procura a melhor fileira no terreno): o projetista
desenha o eixo da fila no estacionamento e o Clivus monta os vãos.

| Peça | Padrão (do `.glb`) |
|---|---|
| Vão (entre pórticos) | 5,50 m; 5 módulos na largura |
| Módulos | retrato, 3 na descida; folga 4 mm na largura, 13 mm na descida |
| Inclinação | 7° |
| Borda baixa dos módulos | 2,35 m do chão |
| Pórtico | 2 pilares em V (C duplo); 2,42 m entre bases, ~3,7 m no topo |
| Viga principal | C duplo, 5,77 m na rampa, balanço ~2,2 m para cada lado |
| Terças | 6, passo 1,12 m, correm ao longo da fila |
| Mão francesa | uma por pilar, do pilar à viga |
| Base | chapa 0,31 × 0,31 m com nervuras, bloco de 0,10 m |

## Onde mora cada coisa

- `Clivus.Core`: `CarportProfile` (os parâmetros e o arquivo
  `.clivuscarport.json`), `CarportGeometry` (as peças de um pórtico e de um vão
  a partir das cotas das bases, sem CAD), `CarportRow` (o eixo, os pórticos e
  os vãos), `CarportBill` (a lista de material e o Excel).
- `Clivus.Plugin`: janela do carport, comando de desenhar a fila, sólidos 3D,
  camadas `CLIVUS_CARPORT_*`, XData com a identidade da fila e do vão,
  editar, regerar e apagar.

## O que já existe e será reaproveitado

| Precisa | Já existe |
|---|---|
| Módulo (medidas, PAN) | `SolarModule`, `ModuleLibrary`, `PanModule` |
| Cota do terreno | `TerrainCache.Get(doc)`, `Tin.TryGetZ` |
| Faces dos módulos, contorno da mesa, XData | `LayoutDrawer`, `LayoutXData`, `LayoutScan` |
| Sombras, PVsyst, 3D web | `ShadingModel`, `ColladaWriter`, `Publicador3D` |
| Strings e numeração | etapas 11 a 16 (leem as mesas pelo `LayoutScan`) |
| Excel | `XlsxWriter`, padrão do `QuantityReport` |

A pergunta da etapa 36 é quanto disso enxerga o vão do carport como uma
"mesa" sem mudar nada. O que não enxergar vira passo próprio.

## Checklist do revisor (somar ao do protocolo)

- [ ] Algum ponto desenhado usa Z do clique em vez da cota do TIN?
- [ ] Borda baixa dos módulos a exatamente a altura livre do chão do pórtico?
- [ ] Pilar com comprimento negativo ou absurdo em terreno íngreme: avisa em vez de desenhar?
- [ ] N vãos → N + 1 pórticos, e o pórtico do meio não sai duplicado?
- [ ] Lista de material: a soma dos comprimentos bate com as peças desenhadas?
- [ ] Os números de ouro do `.glb` (01-modelo-de-referencia.md) passam no teste?
