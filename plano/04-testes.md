# Testes

## Exigência
Cada etapa entrega um conjunto de testes inviolável e imutável. Na etapa N, os testes de todas as etapas anteriores rodam e ficam verdes. Um comando dá o placar por etapa.

## Nível 1: testes puros (a maior parte)
xUnit em `UFV.Core.Tests` e `UFV.Geo.Tests`. Rodam em segundos, sem CAD. Cada teste leva o traço da etapa: `[Trait("Etapa", "3")]`.

Tipos:
- **Exemplo trabalhado**: números conferidos à mão. Ex.: tesoura 4 m, tilt 10°, ponta baixa 0,30 m, pilar a 2,00 m → altura livre 0,647 m (0,30 + 2,00 × sen 10°).
- **Terreno analítico**: planos inclinados, rampas, calombo gaussiano, onde a resposta certa é calculável.
- **Propriedade**: entradas aleatórias com semente fixa, e os verificadores de regra sagrada (`UFV.Core.Invariants`) conferem cada saída.

## Nível 2: dentro do CAD
Core Console (`accoreconsole.exe`, AutoCAD sem interface) roda um script `.scr` que abre um desenho de referência, executa os comandos do plugin, exporta o resultado em JSON e fecha. O JSON é comparado com o esperado.

## Acervo imutável
`tests/acervo/etapa-N/` guarda entradas, desenhos `.dwg` de referência e resultados esperados. `tests/acervo/MANIFESTO.sha256` lista o hash de cada arquivo.

- `checar-acervo.ps1` recalcula os hashes. Qualquer diferença: falha, e o placar inteiro fica vermelho.
- **Acervo e manifesto são mantidos pelo Claude Code desde 25/09/2026.** Até então a regra era o Renan conferir à mão o arquivo proposto em `tests/proposto/` e movê-lo; nesse dia ele decidiu que só valida o que se vê no Civil 3D. O que continua valendo: entrada no acervo é sempre um commit próprio, dizendo de onde veio cada número e o que ainda não foi conferido contra o Civil 3D (campo `Conferido`). Um arquivo que já está no acervo nunca é alterado; se a leitura mudar de propósito, entra um arquivo novo com o motivo.

## Placar
`rodar-testes.ps1` imprime:

```
Etapa 0   12/12  OK
Etapa 1   47/47  OK
Etapa 2   31/33  FALHOU
Acervo    OK
```
