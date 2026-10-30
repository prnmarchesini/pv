# Clivus Solar: estrutura fixa bifila. Instruções para o Claude Code

> Plano escrito em 30/10/2026 a partir do pedido do Renan: *"nessa mesma pasta
> tem o PDF da estrutura fixa Bifila, crie no clivus este tipo de estrutura,
> hoje so temos monoposte, crie TODA estrutura para este novo tipo, TUDO deve
> funcionar com ela."* Referência: os dois desenhos da Arctech do projeto
> Aurora Solar (Panamá), em
> `OneDrive - MARCHENG\1 - Fotovoltaica\Engenharia\Panama\Docs`:
> `... GC1000MM-NS 20_ 1 de 2.pdf` e `2 de 2.pdf` (rev. 2, 29/04/2026) e
> `... GC800MM-NS 10_ 1 de 2.pdf` e `2 de 2.pdf` (rev. 3, 16/05/2026).
> Etapas **37 a 39**, branch `bifila` (saiu da `carport`).

Vale tudo que já valia: regras sagradas (`plano/01-regras-sagradas.md`),
camadas (`plano/02-arquitetura.md`), testes e acervo (`plano/04-testes.md`),
frases de tela em `Tr.T`/`Tr.F` com inglês e espanhol (etapa 10), e as
memórias "tudo que desenha acompanha o terreno", "texto nunca virado" e
"testar pelo caminho da tela".

Leia nesta ordem:

1. `01-modelo-de-referencia.md` (as medidas tiradas dos PDFs; viram o padrão)
2. `PROGRESSO.md`
3. `etapas/etapa-37-bifila.md`

## Regra de ouro

Um passo por vez. Passo com coisa na tela fica `AGUARDANDO VALIDAÇÃO` e o
trabalho segue; passo só de modelo fecha `VALIDADO (automático)`.

## O que é a bifila no Clivus

A mesma mesa fixa de sempre (módulos num plano, tesoura, pórticos ao longo da
mesa), com **dois pilares por pórtico** em vez de um: o dianteiro (lado baixo)
e o traseiro (lado alto), cravados, a 3,00 m um do outro na horizontal. Cada
pórtico tem uma viga (a tesoura), duas mãos francesas (uma de cada pilar até a
viga); as terças correm ao longo da mesa por cima das vigas; e alguns vãos têm
um X de tirantes na linha dos pilares traseiros.

## Decisões (30/10/2026, tomadas por mim; ficam para o Renan confirmar)

1. **A bifila é uma mesa fixa, não uma estrutura à parte.** Diferente do
   carport, ela entra no motor de fileiras: área, alinhamento, Escolha das
   estruturas, distribuição, pontas, recalcular, trocar, strings, numeração,
   sombras, PVsyst, 3D, TermoVision. Por isso ela é um `TableProfile` como as
   outras, guardado nas "MESAS" do desenho, com a parte bifila dentro do
   `TableFrame`. É o único jeito de "TUDO funcionar com ela" sem duplicar o
   motor.
2. **A monoposte não muda.** Perfil sem a parte bifila grava e lê exatamente
   como hoje (versões 2 e 3 do arquivo); a bifila sai na versão 4, que plugin
   antigo recusa com o motivo. A bateria inteira de antes passa sem mudar uma
   linha (teste de regressão).
3. **O motor não muda de regra.** Ele já encaixa a mesa pela ponta baixa (PB)
   dos módulos e só calcula o pilar depois; o pilar traseiro é mais um pilar
   calculado do mesmo jeito (altura do plano menos o chão debaixo dele, mais o
   enterro T3). A mesa que tiver pilar com problema (o traseiro também conta)
   continua sendo recusada no lugar pela distribuição, como hoje.
4. **O pilar não vai até o plano dos módulos.** Na bifila o topo do pilar fica
   abaixo da face de cima dos módulos (módulo + terça + viga): 0,126 m nos
   desenhos da Arctech. Sem isso, cada pilar sairia 12,6 cm mais comprido na
   lista de compra. Na monoposte esse número é zero, que é o que ela faz hoje.
5. **Mão francesa pelo comprimento da peça.** A mão francesa é peça de
   catálogo (1,296 e 1,233 m na GC1000). O projetista dá o comprimento e onde
   ela pega no pilar (abaixo do topo); o Clivus acha onde ela encosta na viga.
   Assim a lista de material sai com o comprimento que se compra.
6. **Desenha tudo.** Pilares como hoje (bloco do pilar); viga, mãos francesas,
   terças e tirantes em sólidos 3D numa camada própria (`CLIVUS_ESTRUTURA`),
   com XData ligando cada peça à mesa, no grupo da mesa. Apagar, copiar,
   recalcular e recontar tratam essas peças.
7. **Dois padrões prontos: Arctech GC800 (NS 10%) e GC1000 (NS 20%).** A
   janela abre na GC800 (a revisão mais nova, 16/05/2026). Os dois só mudam as
   mãos francesas; a altura do vão livre (0,80 ou 1,00 m no fundo do módulo) é
   a faixa da PB do projeto, que continua nas Configurações.
8. **TermoVision sem mudança de contrato.** A bifila vai como "fixa", como a
   monoposte. Mandar as peças da estrutura (como o carport manda) é mudança de
   contrato e fica anotada para combinar com a sessão do TermoVision.

## Onde mora cada coisa

- `Clivus.Core`: `BifilaFrame` (a parte bifila do `TableFrame`), `PillarLine`
  (frente/trás em `PillarPiece` e `PillarResult`), `BifilaStructure` (as peças
  em 3D a partir da mesa assentada, sem CAD), `StructureBill` (lista de
  material da estrutura e a aba do Excel).
- `Clivus.Plugin`: o cartão Biposte na primeira aba da janela de Mesa (38.5;
  antes, a opção "Fixa bifila" na Nova estrutura), a aba do biposte (corte e
  planta), desenho dos dois pilares e das peças, XData
  `Pilar` v3 e `Estrutura` v1, leitura pelo `LayoutScan`, Alturas, Análises,
  Pontas, Usina mista, Excel, 3D.

## O que já existe e será reaproveitado

| Precisa | Já existe |
|---|---|
| Mesa, módulos, pórticos (vãos escritos) | `TableLayout`, `TableFrame`, `PillarTable` |
| Altura e comprimento do pilar | `PillarCalculator` (genérico pela âncora) |
| Barra em sólido 3D | o `Barra` do carport (`CarportCommands`) |
| Faces extras no 3D do Clivus | o caminho das canaletas/carport (`Scene3D.Channels`) |
| Excel | `XlsxWriter`, `QuantityReport` |

## Checklist do revisor (somar ao do protocolo)

- [ ] A monoposte saiu igual? (perfil, geometria, pilares, XData, desenho, Excel)
- [ ] Algum ponto desenhado usa Z do clique em vez da cota do TIN?
- [ ] Os dois pilares do pórtico saem na ordem estação → frente → trás?
- [ ] PB/PA contadas e cotadas uma vez por pórtico, e não por pilar?
- [ ] Mão francesa que encosta no chão ou não alcança a viga: avisa em vez de desenhar torta?
- [ ] Os números de ouro do `01-modelo-de-referencia.md` passam no teste?
