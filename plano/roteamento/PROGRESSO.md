# Progresso do roteamento de cabo

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`, ver `LEIA-PRIMEIRO.md`).

## Etapa 17: base comum das rotas

- 17.1 Botão e abas (CC, Combiner, CA, MT) com pré-requisitos da cadeia: AGUARDANDO VALIDAÇÃO
- 17.2 Layers e cores por rota: AGUARDANDO VALIDAÇÃO
- 17.3 Profundidade da vala por aba: AGUARDANDO VALIDAÇÃO
- 17.4 Seleção da vala em campo e atribuição automática da layer: AGUARDANDO VALIDAÇÃO
- 17.5 Motor de percurso 3D e comprimento: AGUARDANDO VALIDAÇÃO
- 17.6 Lance como entidade com GUID e vínculos: AGUARDANDO VALIDAÇÃO
- 17.7 Mecanismo comum de aviso e pintura: AGUARDANDO VALIDAÇÃO
- 17.8 Recontagem antes de qualquer contagem: AGUARDANDO VALIDAÇÃO
- 17.9 Apagar (tudo ou por seleção): AGUARDANDO VALIDAÇÃO

## Etapa 18: rota CC

- 18.1 Saída da string e contorno da mesa (nunca cortar o miolo): AGUARDANDO VALIDAÇÃO
- 18.2 Escolha do lado de menor comprimento: AGUARDANDO VALIDAÇÃO
- 18.3 Atribuição manual de lado: AGUARDANDO VALIDAÇÃO
- 18.4 Chegada na vala e percurso por ela: AGUARDANDO VALIDAÇÃO
- 18.5 Vala que não passa da mesa: avisar e pintar: AGUARDANDO VALIDAÇÃO
- 18.6 Escolha do cabo e botão Gerar: AGUARDANDO VALIDAÇÃO
- 18.7 Dois lances por string (positivo e negativo): AGUARDANDO VALIDAÇÃO
- 18.8 Ver cabos CC com tabela e totalização: AGUARDANDO VALIDAÇÃO

## Etapa 19: combiner box

- 19.1 Cadastro, dimensão, tag e alocação em campo: AGUARDANDO VALIDAÇÃO
- 19.2 Vínculo na cadeia e alocação de strings na combiner: AGUARDANDO VALIDAÇÃO
- 19.3 Aba Combiner na rota de cabos (dois trechos): AGUARDANDO VALIDAÇÃO

## Etapa 20: rota CA

- 20.1 Raio virtual de busca da vala: AGUARDANDO VALIDAÇÃO
- 20.2 Percurso pela vala até o trafo: AGUARDANDO VALIDAÇÃO
- 20.3 Vala fora do raio: avisar e pintar: AGUARDANDO VALIDAÇÃO
- 20.4 Escolha do cabo, gerar e tabela: AGUARDANDO VALIDAÇÃO

## Etapa 21: rota MT

- 21.1 Raio virtual nos dois lados: AGUARDANDO VALIDAÇÃO
- 21.2 Destino pela UC vinculada, não pela mais próxima: AGUARDANDO VALIDAÇÃO
- 21.3 Vala fora do raio: AGUARDANDO VALIDAÇÃO
- 21.4 Escolha do cabo, gerar e tabela: AGUARDANDO VALIDAÇÃO

## Etapa 22: bibliotecas

- 22.1 Biblioteca de cabos (preenchida inicial, editável): AGUARDANDO VALIDAÇÃO
- 22.2 Escolha do cabo por rota e troca barata: AGUARDANDO VALIDAÇÃO
- 22.3 Biblioteca de módulos com arquivo PAN: AGUARDANDO VALIDAÇÃO
- 22.4 Temperaturas máxima e mínima nas configurações do projeto: AGUARDANDO VALIDAÇÃO
- 22.5 Método de instalação da NBR 5410: AGUARDANDO VALIDAÇÃO

## Etapa 23: tabelas e memorial

- 23.1 Voc na mínima e tensão de operação na máxima: AGUARDANDO VALIDAÇÃO
- 23.2 Correntes do CC: AGUARDANDO VALIDAÇÃO
- 23.3 Queda de tensão no CC: AGUARDANDO VALIDAÇÃO
- 23.4 Tabela do CC: AGUARDANDO VALIDAÇÃO
- 23.5 Cálculos de CA e MT com corrente admissível lado a lado: AGUARDANDO VALIDAÇÃO
- 23.6 Tabelas de CA e MT: AGUARDANDO VALIDAÇÃO
- 23.7 Recalcular ao trocar bitola, sem redesenhar: AGUARDANDO VALIDAÇÃO
- 23.8 Exportar tabelas: AGUARDANDO VALIDAÇÃO

## Etapa 24: resumo de cabos

- 24.1 Aba de cabos no resumo da usina: AGUARDANDO VALIDAÇÃO
- 24.2 Lista de material de cabo: AGUARDANDO VALIDAÇÃO

## 17.1 Botão e abas (10/10/2026): AGUARDANDO VALIDAÇÃO

- Botão **Rota de cabos** no painel Elétrica (`CLIVUS_ROTA_CABOS`; em inglês
  `CLIVUS_CABLE_ROUTES`, em espanhol `CLIVUS_RUTA_CABLES`), ícone da vala com
  o cabo descendo até o fundo. Janela solta, uma por desenho, abas CC,
  Combiner, CA, MT.
- A regra é do Core (`CableRoutes.Missing`): aba indisponível fica
  desabilitada, a dica dela (passando o mouse) e o rodapé da janela dizem o
  que falta. Voltar para a janela relê o desenho.
- Sem interface (Core Console), o comando escreve `ROTA CC: disponível.` ou
  `ROTA CA: falta ...` por aba.
- Assumido: "existe no desenho" = string desenhada e equipamento com o
  retângulo **em campo** (a rota precisa do ponto físico). Só cadastrado não
  libera, mas o recado diz "N no cadastro, nenhum com o retângulo em campo".
  A conta é pelo cadastro (`CableRoutes.Drawing`): a subestação compartilhada
  é um equipamento só (o bloco), e retângulo sem cadastro (COPY, UNDO) não
  libera aba. Cadastro ilegível aparece como ATENÇÃO no rodapé.
- Assumido: a aba Combiner pede string, combiner e inversor (os dois
  trechos). Enquanto a combiner não existe (etapa 19), ela fica sempre
  desabilitada, dizendo isso.
- Feito numa VM Linux: Core e Geo compilados e testados (.NET 10 SDK com
  `-p:LangVersion=13`, o C# do PC). O `Clivus.Plugin` NÃO foi compilado aqui
  (precisa do AutoCAD); compilar no PC antes de testar.
- `tools/rodar-testes.ps1` passou a rodar até a etapa 17.

**Como conferir:** num desenho sem trafo em campo, botão Rota de cabos: a aba
CA aparece cinza, e a dica dela e o rodapé dizem "CA: falta um transformador
(não há nenhum no cadastro)" (ou "um transformador em campo (1 no cadastro,
...)" se ele só estiver cadastrado). Ponha o trafo em campo, volte à janela:
a aba CA libera.
- Revisão (subagente): apontou a contagem de UCs no lugar de blocos, o
  retângulo órfão liberando aba e o cadastro ilegível calado; os três
  corrigidos, com teste.

## Rodada de 10/10/2026 (Renan: "Faça tudo, vou dormir, amanhã eu testo tudo")

Feito tudo de 17.2 a 24.2, mais o menu Exportar (pedido do Renan no mesmo dia:
"quero um menu exportar para eu escolher e exportar cabos, pilares etc."). Feito
na VM Linux: `Clivus.Plugin` compilado contra as DLLs de referência do AutoCAD /
Civil 3D 2026 (NuGet), Core e Geo testados. **Nada rodou dentro do Civil 3D**:
os testes de nível 2 deste módulo não existem ainda (anotado em Observações).

**Onde está cada coisa**
- Botão Rota de cabos: abas CC, Combiner, CA, MT e Resumo. Cada aba de rota:
  profundidade da vala, raio de busca, alcance (CC/Combiner) ou fator de
  potência (CA/MT), método NBR 5410, cabo da biblioteca, Salvar; Selecionar
  vala; Gerar, Apagar tudo, Apagar escolhendo, Forçar lado (CC/Combiner);
  módulo PAN e temperaturas (aba CC); Ver cabos (reconta) e Exportar CSV.
- Combiner: Configuração elétrica, aba Combiner (cadastro, inversor, alocar em
  campo, + Strings, soltar strings).
- Temperaturas: Configurações, no fim do formulário do sistema.
- Exportar (era o botão Excel, painel Saída): janela de caixas de marcar.

**Regras e como foram feitas**
- Vala (17.4): qualquer LWPOLYLINE/POLYLINE/LINE do usuário vira vala da rota
  (XData `Vala` + camada `CLIVUS_VALA_<rota>`). Polilinha do plugin (string,
  mesa, cabo) não entra. Valas que se cruzam ou cuja ponta encosta (até 0,5 m)
  em outra ficam ligadas; o cabo anda pelo caminho mais curto da rede.
- CC (18): de cada ponta (+ e −) da string, reto para fora pela borda comprida
  mais perto, até 0,5 m além de TODAS as mesas da fileira desse lado (mesa
  vizinha deslocada, mais larga ou torta não fica embaixo do cabo); por fora,
  paralelo à fileira (mesmo número de fileira no letreiro, paralelas e
  alinhadas), até 0,5 m além da ponta dela; reto até bater na vala (alcance da
  aba); pela vala até as valas dentro do raio do inversor (vale o menor
  percurso que se liga: a vala mais perto pode estar solta). Os dois lances da string vão pelo mesmo lado: o de
  menor soma (18.2), ou o forçado (18.3).
- CA/MT/combiner → inversor (19.3, 20, 21): todas as valas dentro do raio de
  cada equipamento; o par (saída, chegada) de menor percurso que se liga. MT vai à subestação
  da UC vinculada ao trafo (a unitária, ou o bloco da compartilhada).
- 3D (regra 9): o cabo vai na cota do terreno (lido a cada 1 m) até a vala,
  desce a profundidade, segue a vala acompanhando o terreno, sobe e vai ao
  equipamento (centro da base do retângulo, 0,80 m acima do terreno). Comprimento
  = o da Polyline3d desenhada, medido na hora (regra 7).
- Falhas (regra 4): o Gerar lista cada trecho sem rota com o motivo e pinta de
  LARANJA (o vermelho já é o da mesa suja) a mesa (CC), a string ou a caixa 3D
  do equipamento. A pintura é
  por entidade, reversível, por rota; o Gerar seguinte desfaz e repinta.
- Recontagem (17.8): o Gerar grava os lances esperados; Ver cabos e Resumo
  recontam o desenho, e lance gerado que sumiu (Delete por fora) entra como
  linha sem comprimento e pinta a origem. Apagar pelo botão não conta como sumido.
- Combiner (19): registro `COMBINERS` e `COMBINER_STRINGS`. String na combiner
  liga no inversor da combiner (o vínculo da string é regravado); strings em
  combiner saem do CC direto e vão pela aba Combiner.

**Assumido (conferir)**
- Exit da mesa pela borda comprida mais perto do módulo da ponta, com folga de
  0,5 m (constante `RowExit.Margin`).
- Combiner → inversor desenhado como UM lance (o par + e −); na tabela, a queda
  conta ida e volta (2 × o comprimento). Na lista de material entra uma vez.
- "Corrente de operação" da tabela CC = Isc (curto) ao lado da Imp (MPPT); a
  queda usa a Imp. O PAN não traz o coeficiente da Vmp: com o da potência
  (muPmpReq), β_Vmp ≈ γ_Pmp − α_Isc relativo; sem ele, o da Voc em proporção
  (otimista: a tabela não diz qual usou).
- CA: tensão = a de baixa do trafo do inversor (o inversor não tem tensão de
  saída no cadastro), trifásico ou monofásico pela caixa da aba, potência = a do
  modelo do inversor (kW), com o fator de potência da aba. MT: corrente pela potência do trafo (kVA) na tensão
  de média dele.
- Temperaturas: ambiente, usadas como estão (o sistema não soma aquecimento da
  célula). Padrão 0 °C e 40 °C — a tabela CC diz sempre quais usou. Configuração
  passou ao formato 2 (desenho antigo continua legível, com as de partida).
- Os botões de campo da aba (Gerar, Selecionar vala...) salvam antes o que foi
  digitado e não salvo: o Gerar usa sempre o que está na tela.
- Combiner sem inversor não mexe no vínculo das strings; string na combiner que
  liga em outro inversor (realocada depois) é avisada e pintada na aba Combiner.
- Biblioteca de cabos: `%LOCALAPPDATA%\Clivus Solar\cabos.json`; a aba guarda
  uma cópia do cabo escolhido no desenho (o desenho não depende da biblioteca
  de quem abre). Valores de partida de catálogo/NBR 5410 — REVISAR.
- PAN: guardado no desenho (registro `ROTA_MODULOS_PAN`), um por modelo; a
  string usa o PAN do modelo do módulo da mesa dela (pelo perfil), ou o único
  PAN do desenho.
- Seleção em campo: Shift+clique tira (o padrão do AutoCAD, como a alocação de
  strings); o plano dizia Ctrl.
- O Excel do nível 2 (`CLIVUS_EXCEL_AUTO`) continua só com as quatro abas do layout.

**Revisão (dois subagentes, Core/Geo e Plugin)**: apontaram, entre outros, a
regra 2 quebrada com mesa vizinha desalinhada/mais larga/torta, a vala mais
perto solta virando falha falsa, o retângulo do equipamento pintado sem
aparecer, o CSV com números velhos, o rodapé "falta ." ao clicar na janela, a
biblioteca ilegível sobrescrita, o Gerar com o valor velho do formulário e a
cadeia da combiner inconsistente. Tudo corrigido, com teste onde dá. Ficaram
anotados (não corrigidos): a palavra-chave dos prompts (Tudo/Selecionar/
Automatico) não é traduzida, como no resto do plugin; a seleção com placar está
em três cópias parecidas (inversor, combiner, vala); mesas da fileira giradas
mais de 1° ou deslocadas mais de meia largura não entram na fileira.

**Como conferir (sugestão de roteiro)**
1. Configurações: temperaturas; salvar.
2. Configuração elétrica: inversores e trafo em campo (como antes).
3. Desenhe polilinhas de vala; Rota de cabos, aba CC: Selecionar vala.
4. Carregar .PAN; escolher cabo 6 mm²; Salvar; Gerar. Conferir que nenhum cabo
   corta o meio da mesa; ver a pintura de uma fileira sem vala.
5. Ver cabos: comprimentos, Voc, queda. Trocar para 10 mm², Salvar, Ver cabos:
   comprimentos iguais, queda menor (23.7).
6. Apagar um cabo com Delete e Ver cabos: a mesa pintada, linha sem comprimento.
7. CA e MT idem. Resumo: totais e lista de material com folga.
8. Exportar: escolher Cabos e Pilares; abrir no Excel.

## Observações

- A combiner box tinha ficado fora do plano elétrico (etapas 11 a 16) por decisão do Renan. Entra agora, na etapa 19: a cadeia de `plano/eletrica/04-modelo-de-dados.md` (UC -> trafo -> inversor -> string) ganha o elo opcional inversor -> combiner -> string, e a alocação de strings no inversor que já existe (14.3 elétrica) continua valendo quando não há combiner.
- A atribuição manual de lado (18.3) era passo 2 na conversa, mas o Renan autorizou já deixar previsto.
- Faltam os testes de nível 2 (Core Console) do roteamento: vala, gerar, recontagem e
  exportar. Não dá para rodar na VM; o roteiro acima é a conferência manual.
- O resumo elétrico (etapa 16) ainda não mostra a combiner na árvore (inversor ->
  combiner -> string): as strings da combiner aparecem no inversor dela.
- (coisas fora do escopo que aparecerem vão aqui)
