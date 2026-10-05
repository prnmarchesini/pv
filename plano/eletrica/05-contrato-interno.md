# Contrato interno da parte elétrica

Escrito em 04/10/2026, antes de dividir as etapas 11 a 16 entre agentes. É o
que todos usam igual. Mudar este contrato exige mudar este arquivo e todos os
usos no mesmo commit.

## Onde mora cada coisa

| o quê | onde | código |
|---|---|---|
| tipo de string (biblioteca) | dicionário, chave `STRING_TIPOS` | `StringType`, `StringLibrary` (Core), `StringTypeStore` (Plugin) |
| string desenhada | XData (tipo `String`) da `Polyline3d` do traçado, camada `CLIVUS_STRING` | `ElectricalString` (Core), `ElectricalStore.SaveString/LoadString/Strings` |
| modelo de inversor | dicionário, `INVERSOR_MODELOS` | `InverterModel`, `ElectricalStore.InverterModels/Save...` |
| inversor | dicionário, `INVERSORES` | `Inverter` |
| transformador | dicionário, `TRAFOS` | `Transformer` |
| subestação (UC) | dicionário, `SUBESTACOES` (formato 2 desde 05/10/2026; o 1 continua sendo lido) | `ConsumerUnit` |
| bloco físico da subestação compartilhada (o cubículo) | dicionário, `SUBESTACOES_BLOCOS` | `Substation` (Core, `Electrical.cs`), `ElectricalStore.Substations/SaveSubstations` |
| nome do skid (14.7), um por trafo | dicionário, `SKIDS` | `Skid` (Core, `ElectricalSetup.cs`), `ElectricalStore.Skids/SaveSkids` |
| retângulo de equipamento em campo | XData (tipo `Equipamento`) da entidade, camada `CLIVUS_EQUIPAMENTO` | `EquipmentPlacement`, `ElectricalStore.SavePlacement/LoadPlacement` |

## A cadeia (cada elo guarda o GUID do elo de cima)

- módulo → string: `ElectricalString.Modules` (GUID do bloco do módulo, `ModuleIdentity.Id`).
- string → inversor: `ElectricalString.Inverter` (vazio = livre). **Só aqui.**
  Uma string tem no máximo um inversor por construção. Alocar e desalocar
  regravam só o XData da polilinha; a geometria não muda.
- inversor → trafo: `Inverter.Transformer` (o skid, 14.7; vazio = sem skid).
  O registro `Skid` guarda só o nome do grupo; quem diz que inversores
  são do skid é este campo.
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
