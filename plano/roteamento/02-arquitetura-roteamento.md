# Arquitetura do roteamento

Mesma estrutura de camadas de sempre. Nada muda na divisão, só onde cada coisa mora.

## Camadas

- `Clivus.Core`: motor puro. Biblioteca de cabos, biblioteca de módulos (PAN), configurações do projeto (temperaturas), regras de escolha de percurso, contagem de lances, todas as fórmulas elétricas e a montagem das tabelas. Zero CAD.
- `Clivus.Geo`: geometria. Percurso do cabo em 3D, contorno da mesa, projeção até a vala, descida à profundidade, cálculo de comprimento 3D, busca da vala mais próxima dentro de um raio.
- `Clivus.Cli`: console de teste, roda sem AutoCAD.
- `Clivus.Plugin`: a casca fina. Abas, modais, seleção de polylines em campo, atribuição de layer e cor, desenho dos lances, pintura de aviso. Só aqui entra DLL de AutoCAD/Civil 3D.

## Onde mora cada responsabilidade

- **Seleção da vala**: a seleção em campo e a atribuição da layer em `Clivus.Plugin`. A vala já selecionada vira, para o Core, uma lista de polilinhas abstratas (sequência de pontos), sem tipo de CAD.
- **Traçado**: a decisão de por onde o cabo vai (lado de saída, contorno, menor percurso, busca da vala) em `Clivus.Geo` e `Clivus.Core`. O desenho em `Clivus.Plugin`.
- **Contagem e tabelas**: todo em `Clivus.Core`, a partir dos lances. A exibição em `Clivus.Plugin`.
- **Bibliotecas e cálculos**: `Clivus.Core`. O parser do arquivo PAN também é Core, sem CAD.

## Identidade dos lances

Mesmo esquema do resto: XData no dicionário `CLIVUS`, GUID próprio por lance, vínculo com o elo de origem e de destino (qual string, qual inversor, qual trafo) e com a polaridade no caso do CC. Layer nunca é identidade, é só aparência.

## Layers e cores

Uma layer por tipo de rota, com cor própria: vala CC, vala CA, vala MT, vala combiner, e as layers dos cabos de cada trecho. No CC, positivo e negativo podem ir em layers separadas para facilitar a leitura, mas a identificação real é pelo XData, nunca pela layer.

## Teste de arquitetura

O teste que falha se CAD vazar para Core ou Geo continua valendo e passa a cobrir os tipos de roteamento.
