# Contrato interno da parte elétrica

Escrito em 04/10/2026, antes de dividir as etapas 11 a 16 entre agentes. É o
que todos usam igual. Mudar este contrato exige mudar este arquivo e todos os
usos no mesmo commit.

## Onde mora cada coisa

| o quê | onde | código |
|---|---|---|
| tipo de string (biblioteca) | dicionário, chave `STRING_TIPOS` | `StringType`, `StringLibrary` (Core), `StringTypeStore` (Plugin) |
| string desenhada | XData (tipo `String`) da `Polyline3d` do traçado, camada `CLIVUS_STRING` | `ElectricalString` (Core), `ElectricalStore.SaveString/LoadString/Strings` |
| modelo de inversor | dicionário, `INVERSOR_MODELOS` (formato 3 desde 05/10/2026, com a potência CA em kW; o 2, a lista das entradas de cada MPPT, e o 1 continuam sendo lidos) | `InverterModel`, `ElectricalStore.InverterModels/Save...` |
| inversor | dicionário, `INVERSORES` (formato 2 desde 05/10/2026, com a cor; o 1 continua sendo lido) | `Inverter` |
| transformador | dicionário, `TRAFOS` | `Transformer` |
| subestação (UC) | dicionário, `SUBESTACOES` (formato 2 desde 05/10/2026; o 1 continua sendo lido) | `ConsumerUnit` |
| bloco físico da subestação compartilhada (o cubículo) | dicionário, `SUBESTACOES_BLOCOS` | `Substation` (Core, `Electrical.cs`), `ElectricalStore.Substations/SaveSubstations` |
| nome do skid (14.7), um por trafo | dicionário, `SKIDS` | `Skid` (Core, `ElectricalSetup.cs`), `ElectricalStore.Skids/SaveSkids` |
| varredura da atribuição automática das strings | dicionário, `ALOCACAO_VARREDURA` (separada da `NUMERACAO_VARREDURA`) | `AllocationScan` (Core), `AtribuicaoAutomatica.Varredura/GravarVarredura` (Plugin) |
| retângulo de equipamento em campo | XData (tipo `Equipamento`) da entidade, camada `CLIVUS_EQUIPAMENTO` | `EquipmentPlacement`, `ElectricalStore.SavePlacement/LoadPlacement` |
| hatch da área do trafo (05/10/2026) | XData (tipo `AreaDoTrafo`, o GUID do trafo) do `Hatch`, camada `CLIVUS_TRAFO_AREA` | `TransformerArea`, `TransformerAreaMark` (Core), `AreaDoTrafo` (Plugin) |

## A cadeia (cada elo guarda o GUID do elo de cima)

- módulo → string: `ElectricalString.Modules` (GUID do bloco do módulo, `ModuleIdentity.Id`).
- string → inversor: `ElectricalString.Inverter` (vazio = livre). **Só aqui.**
  Uma string tem no máximo um inversor por construção. Alocar e desalocar
  regravam só o XData da polilinha; a geometria não muda.
- inversor → trafo: `Inverter.Transformer` (o skid, 14.7; vazio = sem skid).
  O registro `Skid` guarda só o nome do grupo; quem diz que inversores
  são do skid é este campo. "Trafo do inversor" e "skid do trafo" são a
  mesma coisa: não há outro vínculo inversor → trafo. Muda por dois
  caminhos (05/10/2026):
  - a tabela da aba Inversor (`ElectricalSetup.SetTransformer`): a caixa
    Trafo da linha ou o "Pôr no trafo" das linhas escolhidas. Escolha
    explícita: o inversor de outro trafo MUDA (sem trava); "sem trafo" solta.
  - a seleção em campo, `CLIVUS_ELETRICA_SKID` (`Group`, no quadro fechado
    "Agrupar em campo (skid)"): dá nome ao grupo; inversor de outro skid fica
    travado.
  Nos dois, o trafo que fica sem inversor perde o registro `Skid` (o nome);
  o nome do skid do trafo de destino fica. Sem registro, o nome mostrado é
  "Skid T1".
- trafo → subestação: `Transformer.ConsumerUnit` (vazio = sem UC). Muda pela
  aba Subestação (`LinkTransformer`/`UnlinkTransformer`) ou pelo formulário do
  trafo (`SaveTransformer`, campos e UC tudo ou nada); a trava é a mesma nos
  dois (trafo de outra UC fica travado até ser solto; a unitária tem um trafo só).
- UC compartilhada → bloco: `ConsumerUnit.Substation` (o GUID do `Substation`).
  A usina tem um bloco compartilhado (`EnsureSharedSubstation`), com as UCs
  C1, C2... dentro, cada uma com nome e um ou mais trafos. A unitária é UC e
  bloco ao mesmo tempo: `Substation` vazio, a dimensão é dela.
- o tipo da string: `ElectricalString.Type` (o `StringType.Id` que a gerou;
  vazio nas strings de teste).
- a tag da string: `ElectricalString.Tag` (vazia até a numeração, etapa 15).

Nenhum vínculo é derivado de posição no desenho.

## A subestação física (05/10/2026)

- O que vai para o campo é a subestação física: a unitária (o GUID da UC) ou o
  bloco compartilhado (o GUID do `Substation`). Os dois usam
  `EquipmentKind.ConsumerUnit` no `EquipmentPlacement`; o GUID diz qual é. A UC
  compartilhada não vai para o campo. `CLIVUS_ELETRICA_POSICIONAR C1` põe o
  bloco onde a C1 está (código ou nome da UC achando o bloco).
- `SUBESTACOES` formato 2: 8 campos por UC (os 7 de antes e o GUID do bloco,
  vazio na unitária). O formato 1 (7 campos) é lido pela versão do cabeçalho
  (`RecordTable.VersionOf`); grava-se sempre o 2.
- Desenho antigo: a compartilhada sem bloco (ou com bloco que sumiu) cai, na
  leitura (`ElectricalSetup`), no bloco que existe ou num "Subestação
  compartilhada" criado com o tamanho da primeira delas e GUID derivado dela
  (ler de novo antes de gravar dá o mesmo GUID). `MigratedUnits` conta quantas;
  a próxima gravação leva o bloco para o desenho. Retângulo antigo de UC
  compartilhada fica no desenho como sobra: a aba Subestação avisa, e "Apagar
  UC" o leva junto.
- Apagar o bloco (`RemoveSubstation`) leva as UCs dele e solta os trafos delas;
  os trafos ficam.
- Resumo: `ElectricalSummary.Build(..., substations)` põe o bloco
  (`SummaryRowKind.Substation`) com as UCs dele um nível para dentro; a
  unitária fica como antes.

## A potência do modelo de inversor (05/10/2026)

- `INVERSOR_MODELOS` formato 3: 8 campos (os 7 do formato 2 e a potência
  nominal CA em kW, invariante, vazia = não informada). `InverterModel.PowerKw`
  (0 = não informada; de 0 a `ElectricalDefaults.MaxInverterPowerKw`).
- Os formatos 2 e 1 (7 campos) são lidos pela versão do cabeçalho com a
  potência 0. Grava-se sempre o 3.
- A tabela da aba Inversor (`InverterTable`, Core): strings pelo vínculo
  (`StringAllocation.CountByInverter`), kWp de cada inversor do resumo
  (`ElectricalSummary`, `SystemSummary.AllInverters`: a potência dos módulos
  das strings dele pela mesa dona), kW do modelo e CC/CA = kWp / kW (só com
  potência informada). Nada é gravado: é leitura.

## O modelo de inversor por MPPT (05/10/2026)

- `INVERSOR_MODELOS` formato 2: 7 campos (GUID, nome, quantos MPPTs, a lista
  das entradas de cada MPPT separada por ";", ex. "4;4;4;5;5", e a
  dimensão). `InverterModel.InputsByMppt` é a lista; `TotalInputs` a soma
  (a capacidade do inversor em strings).
- O formato 1 (MPPT e entradas por MPPT, o mesmo número para todos) é lido
  pela versão do cabeçalho (`InverterModel.ParseLegacy`): vira a lista com o
  valor repetido. Grava-se hoje o 3 (acima).

## A cor do inversor (05/10/2026)

- `INVERSORES` formato 2: 5 campos (os 4 de antes e a cor, "#RRGGBB").
  O formato 1 (4 campos) é lido pela versão do cabeçalho com a cor null, e o
  `ElectricalSetup` dá a cor automática na leitura, na ordem da lista
  (`InverterColors.Next`: a da paleta menos usada; ler de novo dá a mesma).
  `ColoredInverters` conta quantas; a próxima gravação as leva ao desenho.
- Inversor criado ganha a cor da paleta menos usada (`InverterColors.Palette`:
  legível no fundo escuro e no claro, sem lilás/violeta/roxo da sombra nem
  magenta/vermelho de aviso). `SetInverterColor` troca.
- A cor é representação: a polilinha da string alocada e os sinais dela (+, −
  e os círculos, pelo `StringSign`) ficam com a cor do inversor; livre (ou de
  inversor que não está no cadastro), ByLayer. Quem grava o vínculo
  (`StringsDoDesenho.Gravar`) pinta junto; trocar a cor repinta
  (`CorDasStrings.Repintar`). Nenhum vínculo é lido da cor.

## A atribuição automática (05/10/2026)

- A varredura dela é própria (`AllocationScan`: o sentido que avança e o
  sentido dentro da faixa, perpendicular), gravada em `ALOCACAO_VARREDURA`;
  a da numeração (`NUMERACAO_VARREDURA`, a tag) não muda por ela. A ordem é a
  mesma regra (`ScanOrder.Order`, que ganhou o sentido na faixa; sem ele, o
  de antes).
- `StringAutoAllocation.Allocate`: as strings livres (sem GUID repetido, com
  o primeiro módulo no desenho), na ordem da varredura, enchem os inversores
  na ordem da lista do cadastro até `TotalInputs`; as já alocadas não mudam e
  contam; inversor cheio ou sem modelo é pulado; a sobra é contada. Só o
  campo `Inverter` das livres muda (gravado por `StringsDoDesenho.Gravar`, que
  pinta com a cor do inversor).

## O hatch da área do trafo (05/10/2026)

- Botões "Hatch da área" e "Hatch de todos" da aba Transformador. A área
  sai da cadeia (trafo → inversores pelo `Inverter.Transformer` → strings
  pelo `ElectricalString.Inverter` → módulos), nunca da posição: as faces
  dos módulos (sem face, a extensão do bloco) viram o contorno do Core
  (`TransformerArea.Outline`: união, fechamento de 4 m, folga de 0,5 m,
  menos os módulos de strings de outros inversores, sem buraco < 1 m²).
- Um `Hatch` SOLID por ilha, 60 % transparente, com a cor do trafo (a
  paleta dos inversores de trás para a frente, pela ordem do trafo) e o
  XData `AreaDoTrafo`; gerar de novo apaga o antigo daquele trafo, e apagar
  o trafo o leva. Cota: o canto mais alto dos módulos da ilha + 0,30 m (a
  mesma escolha da marca do grupo). É representação: nada é lido dele.

## Para testar sem o traçado de verdade

`CLIVUS_STRINGS_TESTE_AUTO` (só no build Debug): uma string por fileira de
cada mesa, os módulos na ordem das colunas, sem tipo, inversor nem tag. Use
depois de gerar a usina num caso de nível 2 (veja `eletrica-contrato.ps1`).

## Nível 2

Cada passo põe o seu caso num arquivo `tests/Clivus.Integration/eletrica-<passo>.ps1`
que define a função e faz `$script:CasosEletricos += 'Nome-Da-Funcao'`
(modelo: `eletrica-11-1.ps1`). Não mexa no `rodar.ps1`.

## Tela

- Comandos: constantes novas em `src/Clivus.Core/PluginInfo.Eletrica*.cs`
  (o `PluginInfo` é `partial`), cada comando digitável com nome em inglês e
  espanhol em `CommandNames.Table`; depois `python tools/gerar-comandos-traduzidos.py`.
- Janelas: soltas, uma por desenho, escrita no desenho por
  `EscritaForaDeComando.Fazer` (trava e vigia calado), clique em try/catch.
- Ribbon: o painel Elétrica já tem String (`CLIVUS_STRING`), Configuração
  elétrica (`CLIVUS_ELETRICA`) e Resumo elétrico (`CLIVUS_ELETRICA_RESUMO`).
- Texto de tela sempre em `Tr.T`/`Tr.F` (etapa 10), com tradução em inglês e
  espanhol.
