# Arquitetura da parte elétrica

A elétrica encaixa na mesma estrutura de camadas do layout. Nada de novo na divisão; só onde cada coisa mora.

## Camadas (relembrando)
- `Clivus.Core`: motor puro. Tipos elétricos, regras de vínculo, modelos de inversor/trafo/UC, lógica de alocação, varredura e composição de tag. Zero CAD.
- `Clivus.Geo`: geometria e terreno. Traçado da string acompanhando a declividade dos módulos, posição dos retângulos de equipamento em campo, amostragem de cota. Sem API de CAD.
- `Clivus.Cli`: console de teste do motor, roda sem AutoCAD.
- `Clivus.Plugin`: a casca fina. Botões, abas, modais, seleção em campo, desenho das entidades. Só aqui entra DLL de AutoCAD/Civil 3D.

## Onde mora cada responsabilidade elétrica
- **Modelos** (inversor com MPPT e entradas, trafo genérico, UC): tipos em `Clivus.Core`. Biblioteca/catálogo persistida junto com o desenho.
- **Alocação de string em inversor**: a regra (trava de string já alocada, excesso de capacidade, contagem) vive em `Clivus.Core`. A seleção em campo e o destaque visual vivem em `Clivus.Plugin`.
- **Traçado da string** (convencional, leapfrog, livre por cliques, respeitar declividade): a geometria em `Clivus.Geo`; os cliques e o desenho em `Clivus.Plugin`.
- **Numeração** (composição da tag, varredura, blocos, ordem): toda a lógica em `Clivus.Core`. O desenho das tags e a seleção de mesas por bloco em `Clivus.Plugin`.
- **Resumo do sistema**: consolidação em `Clivus.Core` a partir do vínculo; a tabela na tela em `Clivus.Plugin`.

## Identidade das entidades elétricas
Mesmo esquema do layout: XData no dicionário `CLIVUS`, GUID próprio por entidade (string, inversor, trafo, UC), carimbo de proveniência. Layer nunca é identidade. Cada string, inversor, trafo e UC é única e carrega seus atributos e o vínculo com o nível acima.

## Teste de arquitetura
O teste que já falha se CAD vazar para Core ou Geo continua valendo e passa a cobrir os tipos elétricos também.
