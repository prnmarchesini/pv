# Progresso da bifila

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

**Plano escrito em 30/10/2026 a partir do pedido do Renan ("crie TODA
estrutura para este novo tipo, TUDO deve funcionar com ela"). As decisões do
`LEIA-PRIMEIRO.md` foram tomadas por mim e ficam para ele confirmar.**

## Etapa 37: o modelo

- 37.1 A bifila no perfil da mesa: VALIDADO (automático) — `BifilaFrame`, `.ufvmesa.json` v4 (724d7ad)
- 37.2 Dois pilares por pórtico: VALIDADO (automático) — `PillarLine`, números de ouro da Arctech (ef2c30a)
- 37.3 As peças da estrutura: VALIDADO (automático) — `BifilaStructure` (aeb2f5c)
- 37.4 Lista de material: VALIDADO (automático) — `StructureBill`, aba Estrutura (b2d1733)

## Etapa 38: no Civil 3D

- 38.1 Nova estrutura: Fixa bifila: AGUARDANDO VALIDAÇÃO
- 38.2 Desenhar a bifila: AGUARDANDO VALIDAÇÃO — nível 2 `clivus-bifila` (rodar.ps1 -SoBifila)
- 38.3 As ferramentas que leem pilar: AGUARDANDO VALIDAÇÃO
- 38.4 Lista de material na tela: AGUARDANDO VALIDAÇÃO
- 38.5 A janela de Mesa em abas: AGUARDANDO VALIDAÇÃO
- 38.6 O PAN enche o módulo e sobe para o servidor: AGUARDANDO VALIDAÇÃO — serviço: `servidor/tests/test_pan.py`
- 38.7 Fabricantes, biposte genérico e a altura básica (P1 e P2): AGUARDANDO VALIDAÇÃO — `VendorStructures`, `PillarsAtBasicHeight`, 10 testes
- 38.8 Os módulos no servidor do Clivus, com a validação da gestão: AGUARDANDO VALIDAÇÃO — servidor: clivussolar 2cc6561 (gate `validate_modulos`); plugin: `ModuleServiceReply`, 20 testes

## Etapa 39: o resto do Clivus

- 39.1 3D e TermoVision: AGUARDANDO VALIDAÇÃO
- 39.2 Tudo funciona com biposte (a prova): AGUARDANDO VALIDAÇÃO — nível 2 `clivus-estruturas` (rodar.ps1 -SoEstruturas)

## Etapa 40: mesas conforme os objetos do cliente

- 40.1 Reconhecer as mesas do cliente: VALIDADO (automático) — `FieldObjects`, 21 testes; nos 641 objetos do Aurora: 609 mesas em 101 fileiras, 18 repetidos e 14 sobrepostos
- 40.2 Gerar conforme objetos em campo: AGUARDANDO VALIDAÇÃO — nível 2 `clivus-campo` (rodar.ps1 -SoCampo)

## Observações

- 37.1 (30/10/2026), o que assumi:
  - Medidas tiradas da geometria vetorial dos PDFs (ver `01-modelo-de-referencia.md`):
    T1 4,030 centrada nos módulos; T2 0,498 e T2t 3,544 (o ponto do plano em cima do eixo
    de cada pilar), 3,000 m entre eles na horizontal; topo do pilar 0,126 abaixo da face
    dos módulos; enterro 2,20; vãos 3,2 + 3,5 × 3 + 3,2 com balanço 0,851.
  - A Arctech cota a altura livre no fundo do módulo; a PB do Clivus é a face de cima. A
    GC800 pede PB ≥ 0,832 e a GC1000 PB ≥ 1,032 na faixa da PB do projeto (Configurações).
  - Seções não cotadas (só 3D): pilar 0,050 × 0,095, viga 0,05 × 0,08, terça 0,05 × 0,10,
    mão francesa 0,04 × 0,06, tirante Ø 0,016.
  - "Sem tirante" é um jeito só: linha nenhuma e lista vazia; os vãos ficam em ordem.
  - Revisão (subagente): 7 defeitos, todos corrigidos com teste — validação que lançava
    exceção com vão-alvo absurdo; terça fora da tesoura; pilares encostados em planta
    (conta em planta, no perfil, que conhece a inclinação); `"rodLine": "1"` lido calado
    (agora só pelo nome exato); topo do pilar entre o módulo e o fundo da viga; dois jeitos
    de dizer "sem tirante"; NaN em campo sem uso.
  - Achado fora do escopo: o `arrangement` do perfil (antigo, de 2026-09) aceita `"1"`
    entre aspas como `DoubleRow`, pelo conversor do .NET. Não mexi (não é da bifila).
- 37.2: a PB e a PA são da estação (iguais nos dois pilares do pórtico); o traseiro conta
  como pilar com problema na distribuição, como qualquer pilar.
- 37.3, o que assumi:
  - Os pilares NÃO entram como barras: continuam blocos do pilar (o desenho, o XData, as
    análises e a lista de compra já tratam deles). As barras são viga, mão francesa, terça
    e tirante.
  - Pilha debaixo dos módulos: terça encostada no módulo, viga encostada na terça (eixo da
    viga 0,173 abaixo da face na Arctech, ~5 cm abaixo do topo do pilar, como no corte).
  - A mão francesa sai do eixo do pilar (no desenho ela pega na face, 9 cm ao lado): a
    cabeça encosta ~1,10 m à frente do pilar, contra 1,08 no desenho.
  - Revisão: cabeça colada no pilar ou além do outro pilar, pé acima da viga, aviso de
    "aumente" para peça comprida, vazio calado, pilar sem terreno calado, tirante sem a
    linha no aviso, NaN — todos corrigidos com teste.
- 37.4, o que assumi:
  - Corte arredondado para cima no milímetro (0,1 mm de folga): a peça de catálogo de
    1,244 continua 1,244 e nenhum corte sai curto. Terça em barras de até 7,0 m (dá as 3
    barras e 2 emendas por linha da Arctech).
  - Revisão: corte curto pelo arredondamento ao centímetro, emenda negativa com barra
    inválida, emendas sem a barra no nome, faltava a lista por mesa e a nota de "nenhuma
    peça" — corrigidos com teste.
- 38.x (30/10/2026), o que assumi:
  - Na janela de Mesa, a bifila mostra "Entre pilares, na horizontal" (3,00): o T2t é
    calculado com a inclinação do campo ao ler. Os botões GC800/GC1000 só trocam as mãos
    francesas (é o que muda entre as duas). As seções são texto "0,05 × 0,08".
  - Nova estrutura › Fixa bifila parte da última bifila do desenho ou da GC800.
  - O pilar da bifila grava o XData na versão 3 (a linha no 12º campo); o da monoposte
    continua na 2. As peças vão em sólidos na camada CLIVUS_ESTRUTURA (nasce só quando há
    bifila), XData "Estrutura" v1, no grupo da mesa; mesa que não cabe deixa as peças
    magenta na camada das marcadas, como o resto dela.
  - Na Usina mista, cada mesa usa o T3 da própria estrutura (antes valia o do primeiro tipo
    para todos). Tipo sem T3 continua com o enterro da configuração da usina.
  - O 3D lê as faces de cada sólido (Brep) e manda em triângulos, na lista das canaletas
    (sem mudança de contrato com o servidor).
  - O teste de nível 2 da cota lê os sólidos por XEDGES (o Core Console não tem
    getpropertyvalue nem ActiveX).
- Revisão do 38.1/38.2 (subagente), corrigido:
  - Nova › Fixa abria em modo bifila quando a última mesa era bifila.
  - Os avisos da bifila sumiam no Recalcular, no Trocar, nas Pontas e nas Análises; agora
    saem em todos, com as mesas de cada um.
  - PB/PA ficavam faltando quando o dianteiro tinha problema: agora é o primeiro pilar sem
    problema da estação (`PillarStations`), a mesma regra no desenho, nas Alturas e nas
    Análises.
  - Peça órfã no Refazer.
  - A janela recalcula ao mudar as seções e recusa inclinação negativa.
  - XData só pelo nome exato.
  - Sólido lido primeiro no LayoutScan.
  - A mesa suja pinta também a estrutura.
  - O corte aplica as regras do Core às mãos francesas.
  - O sólido da barra é um só para carport e bifila (`SolidoDeBarra`).
  - O teste de nível 2 ficou mais forte: sólidos pelo XData, cota de cada sólido sozinho
    (PICKSTYLE 0), 12 arestas exigidas, peça junto dos pilares, 3,00 m entre pilares,
    nada sobrando depois do Apagar tudo.
- Revisão do 38.3/38.4/39.1 (subagente), corrigido:
  - A regra do T3 mora num lugar só (`SystemConfiguration.WithEmbedmentOf`): a fileira e a
    mesa avulsa (Pontas, Recalcular com pontas) aplicam o T3 de cada mesa. Antes a mesma
    bifila saía com 2,20 m na Usina e com o enterro da configuração nas Pontas.
  - O plugin entrega ao motor a configuração do projeto: o tipo sem T3 numa usina mista não
    herda mais o T3 do primeiro tipo.
  - A ponta da mesa usa o pilar que tem PB (`PillarStations.EndCarrier`).
  - O módulo da análise usa o pilar da estação.
  - O Excel só escreve a linha da estrutura com a aba marcada.
  - Os avisos ordenam F1.2 antes de F1.10.
  - Uma cor de aço só.
  - O 3D não explode mais os sólidos: o XData de cada peça guarda o eixo e a seção, e o
    Core monta a barra (`BarMesh`, a mesma do carport) em 8 triângulos (sem as tampas). Peça
    movida ou copiada aparece onde está.
- Fica para o Renan ver na tela (riscos conhecidos):
  - Peso: são 26 sólidos por mesa 2P14; numa usina de 500 mesas, uns 13 mil. Medir o tempo
    do Processar e o tamanho do DWG no projeto do Panamá.
  - 3D: as peças vão na camada das canaletas da página (a caixa "Canaletas e carport" liga
    e desliga a bifila também; formato do servidor igual, só mais faces). Conferir de longe
    se a terça, 3,3 cm abaixo do módulo, não aparece por cima dele.
  - Commits: o 38.1 a 39.1 saíram em dois commits (o núcleo no Core e o plugin), e não um
    por passo, porque as correções das revisões cruzaram os passos nos mesmos arquivos.
- 38.5 (30/10/2026), o que assumi:
  - A janela abre sempre na aba "Perfil e módulo", com o tipo da mesa aberta marcado; para
    uma mesa nova, o tipo da última mesa do desenho.
  - Só a aba do tipo escolhido aparece; a do outro fica guardada (os textos dos campos,
    o arranjo e os vãos escritos), e o perfil salvo de outro tipo também guarda a atual.
  - Na tela, "bifila" virou "biposte" (a palavra do Renan nos pedidos de 30/10); o nome
    técnico (`Bifila`, `frame.bifila`) não muda. Dois testes do 37.1 que procuravam a
    palavra na mensagem passaram a procurar "biposte", com a mesma força.
  - Nova estrutura voltou a Fixa e Carport: a Fixa bifila virou o cartão Biposte.
  - Revisão (subagente, que rodou a janela fora do Civil 3D e clicou nos cartões): nada grave;
    corrigido o nível 2 da bifila, que procurava "ATENÇÃO (bifila)"; a semente do biposte com
    outro módulo vai pelo vão-alvo (os vãos escritos da GC800 só fecham com o módulo de
    1,303 m); "Seções das peças" sem o "(3D)", que o layout compacto escrevia "(3d)"; "Sem
    tirante" não apaga mais os vãos escritos no campo; o LEIA-PRIMEIRO.
  - Fica como está: o nome é um só para os dois tipos (fica na primeira aba); trocar o tipo
    não muda o nome.
- 40.1 (30/10/2026), o que assumi:
  - O objeto é a área dos módulos ou a mesa inteira (com as sobras): os dois casam.
  - Tamanho e ângulo reto medidos em 3D (o retângulo do cliente vira paralelogramo em
    planta); o eixo e o lado da ponta baixa, em planta. A inclinação é o giro em torno do eixo
    (nos 641 objetos: 9,999° a 10,001°).
  - A referência é o que a maioria tem; tamanho, rumo, inclinação e ponta baixa diferentes
    são apontados, e a mesa sai mesmo assim. Não viram mesa: o que não é retângulo de 4
    vértices, o repetido (fica o primeiro), o que está em cima de parte de outro, o que está
    com o centro fora da área. Atravessar o contorno vira mesa, com aviso.
  - A fileira se parte onde a folga entre duas mesas passa da do motor (5 m no padrão),
    para a fileira do Clivus ser a corrente que o motor resolve.
- 40.2, núcleo (30/10/2026): `FieldPlacement` casa cada objeto com a primeira mesa em uso do
  tamanho e da inclinação dele, recusa o projeto que olha para o outro lado (o motor viraria
  as mesas) e roda o motor duas vezes: a primeira com a rampa do objeto, a segunda com a célula
  corrigida pelo que a primeira mediu (o motor ancora a mesa no canto da ponta baixa e encurta
  a planta pelo giro). 8 testes, um deles conferido desligando a segunda passada (ele falha).
  Mono e biposte nos mesmos objetos dão os módulos no mesmo lugar (a menos de 1 cm).
- 40.2, na tela (30/10/2026), o que assumi:
  - Botão "Conforme objetos" no painel Processar, ao lado de Usina, e "Gerar conforme objetos
    em campo…" no clique direito da área. Pergunta "[Todos/Camada/Escolher]": Todos são as
    polilinhas fechadas de 4 vértices com o centro dentro da área; Camada, as de uma camada
    (clica-se um objeto); Escolher, os objetos selecionados.
  - Processa antes de apagar: se o motor falhar, as mesas da área ficam. Depois troca as
    mesas que a área tinha (pergunta antes, com quantas saem e quantas entram).
  - As marcas (círculo e motivo, na cota do terreno) ficam na camada CLIVUS_DIVERGENCIAS; a
    rodada seguinte na mesma área apaga as da vez anterior.
  - A área guarda, no dicionário dela, que segue os objetos e como foram escolhidos: o
    Regerar área, o Regerar todas e o automático refazem pelos objetos, sem alinhamento. O
    Regerar fileira não mexe nessa área, e o Trocar não reespaça as vizinhas.
  - Nível 2: 8 objetos (6 + 1 repetido + 1 curto) no Z 9999 → 6 mesas a 0,000 m do centro
    dos objetos, 72 pilares na faixa do terreno, os 8 objetos intactos, 2 marcas; o Regerar
    área refaz igual.
  - Revisão (subagente), corrigido com teste: o eixo perto de 180° (ruído de coordenada ou
    vértices ao contrário) virava a mesa e abria a junta — cada pegada passa para o sentido da
    referência (o teste falha sem isso); um retângulo que não é mesa (cerca) tirava o lugar das
    mesas — os do tamanho da maioria disputam primeiro; a sobreposição pequena na mesma
    fileira passava — o alcance cobre a mesa inteira; o objeto 2D desenhado com a largura em
    planta não casava; a fileira a 3° da maioria virava mesas soltas — as fileiras se agrupam
    pelo eixo; a polilinha com o primeiro vértice repetido no fim (GIS, DXF) era descartada;
    camadas congeladas, desligadas e do Clivus ficam fora do Todos; a área esquece os objetos
    na Usina pelo alinhamento e no Apagar tudo; camada travada não derruba; as marcas da vez
    anterior saem também a até 30 m da área; o objeto de outro tamanho que casa com outra mesa
    em uso não é divergência; o "sem mesa do tamanho" saía duas vezes; o Regerar todas conta
    só a área que gerou. Nível 2: mais um objeto 2D (7 mesas) e o Apagar tudo esquecendo.
- 39.2 (30/10/2026), a prova:
  - O nível 2 `clivus-estruturas` roda a mesma cadeia duas vezes no mesmo terreno, com a
    mesma mesa (a 2P14 GC800): uma vez com estrutura monoposte (`CLIVUS_MONO_2P14_AUTO`,
    só de teste) e outra com o biposte. A cadeia: usina, alturas, tag de mesas e de
    módulos, grupo, strings, configuração elétrica (UC, trafo, modelo, inversores, alocação,
    skid), local dos inversores pelas strings, cabos da rota, vala, Gerar CC, conferência e
    resumo da rota, resumo elétrico, numeração, tag das strings, análises (PB, pilar,
    declividade), árvore e sombras, PVsyst, Excel com todas as abas (`CLIVUS_EXCEL_TUDO_AUTO`,
    só de teste), 3D, TermoVision, Validar e Recontar.
  - Resultado (10 fileiras, 30 mesas, 840 módulos): as 22 camadas do desenho que não são
    estrutura saem iguais nas duas (quantas entidades e a soma de X + Y de cada uma): área,
    alinhamento, mesas, módulos e faces, tags de mesa, de módulo e de string, strings, cabos
    CC + e −, vala, inversores, grupo, sombras e textos, árvore, declividade. Iguais também:
    o resumo da usina, o resumo da rota (1.028,59 m de cabo CC, por inversor), o resumo
    elétrico, as abas do Excel que não são de pilar, o DAE do PVsyst, a cena do 3D (menos
    pilares e peças) e o retorno do TermoVision. Muda só a estrutura: 180 × 360 pilares,
    0 × 780 peças, 540 × 720 cotas das alturas (PB/PA uma por pórtico, P3 em cada pilar).
  - O levantamento do "tudo" (subagente) achou quatro lugares onde o biposte escorregava,
    corrigidos com teste: a contagem da PB/PA dizia "pilares" e conta pórticos (agora diz
    "pórticos" quando o desenho tem biposte; o nível 2 da bifila confere); o Apagar › Cores
    deixava as peças do biposte vermelhas; a regra das pontas iguais olhava o último pilar da
    lista (o traseiro) e não o que leva a PB (o teste falha com a regra antiga); o palpite do
    perfil pelo comprimento (mesa antiga sem o nome do perfil) não olhava o tipo, e a 2P14
    monoposte e a GC800 têm o mesmo comprimento. A caixa do 3D diz "Canaletas, carport e
    estrutura do biposte".
  - Revisão (subagente), corrigido: o palpite do perfil pelo comprimento ainda podia trocar
    o tipo da estrutura quando não achava perfil do mesmo tipo — agora Recalcular, Pontas,
    Regerar fileira e Repintar recusam e mandam usar o Regerar área (`GuessByLength`, com
    teste); a contagem em pórticos sai dos pilares que o Quantificar já lê. A prova ficou
    mais apertada: X, Y e Z separados e o texto na impressão digital (5 cm de folga), as cotas
    de PB e PA comparadas (só o P3 muda), as 21 camadas exigidas, os resumos elétrico e de
    cabos no Excel, o rótulo "pórticos" conferido, Validar e Recontar conferidos, as linhas de
    batimento do console fora. Passa: 24 camadas e 5 abas iguais; as mesas ficam na mesma
    cota nas duas estruturas.
  - Ficam fora do nível 2 (sem comando automático ou só de tela): a rota CA e MT (precisam
    do trafo e da subestação posicionados), a drenagem (não lê mesa), o carport (estrutura à
    parte), salvar e reabrir o DWG, e as janelas. Estão no roteiro de tela.
- 38.6 (30/10/2026), o que assumi:
  - O PAN do PVsyst traz as medidas em metro (Height, Width, Depth; o do Aurora, 2,384 ×
    1,303 × 0,033). O lado maior vira a altura. PAN sem as três medidas: as medidas ficam
    como estavam, e o recado diz.
  - "Carregar .PAN..." põe o módulo do PAN na lista de modelos (o da biblioteca com o mesmo
    modelo, ou ele mesmo, só nesta janela) e enche marca, modelo, medidas, potência e a parte
    elétrica. Se a biblioteca do serviço não tem o modelo, pergunta se cadastra, com o PAN;
    com o serviço fora do ar, diz que o "Cadastrar módulo..." leva depois.
  - "Cadastrar módulo..." tem "Ler .PAN..."; com o PAN já lido na janela de Mesa, abre com ele.
    O PAN só vai junto se o modelo não foi trocado à mão.
  - O serviço (`servidor/`, FastAPI) guarda a parte elétrica (V, A, V/°C, A/°C, %/°C) e o
    arquivo como veio (até 500 mil caracteres); a lista devolve a parte elétrica e se tem PAN,
    sem o texto; o arquivo sai em `GET /modulos/{id}/pan`. Migração `3c1d2e7f9a10` (o
    `tools/servico-local.ps1` já roda o `alembic upgrade head`). Escolher na lista um módulo
    que tem a parte elétrica no serviço enche os dados elétricos.
  - **O "servidor" hoje é o serviço de módulos local** (`tools\servico-local.ps1`, ou o
    endereço de `CLIVUS_SERVICO`). O servidor de produção (Coolify: 3D, licenças, portal) não
    hospeda o serviço de módulos; para os usuários subirem PAN para todos, o serviço precisa
    ir para lá — decisão do Renan (e da sessão do servidor, pelo CANAL.md), fora deste passo.
  - Testes: 8 no serviço (pytest, 32 ao todo) e 8 no Core (medidas do PAN, o lado maior, a
    parte elétrica indo e voltando). As janelas são de tela.
  - Revisão (subagente), corrigido com teste: um módulo com coeficiente NaN ou infinito,
    que o serviço aceitava, derrubava a lista de todos para a biblioteca embutida — o
    serviço recusa (422 limpo) e o plugin lê a parte elétrica sem depender dela; o serviço
    antigo jogava o PAN fora e o recado dizia "com o PAN" — o plugin confere `tem_pan` na
    resposta; a parte elétrica e o arquivo passam a acompanhar o módulo escolhido (a do PAN
    lido, a do serviço, ou nenhuma, nunca a de outro módulo); o cadastro fica com as medidas
    da janela de Mesa; o PUT sem PAN mantém o PAN; nome de arquivo com qualquer caractere
    no download; o NUL sai do texto; o PAN do mesmo modelo lido de novo troca a entrada
    provisória; a lista que não recarrega não fica em branco. O contrato da lista com PAN
    (`tests/dados/servico-modulos-com-pan.json`) é conferido pelos dois lados (pytest e
    Core). Serviço: 53 testes.
  - Fica como está: o texto do PAN antigo (ANSI) é guardado como lido (Latin-1), não os bytes
    exatos; os números, que são o que conta, são ASCII.
- 38.7 (30/10/2026), o que assumi:
  - Pedidos do Renan: "se for um biposte específico do fabricante, crie uma aba de
    fabricantes e aí um genérico, para a pessoa montar o biposte dela" ("esse biposte eu vi
    que tem umas mãos francesas, etc"), e, com o desenho da Arctech ao lado (3059 e 3888),
    "cadê a altura básica das duas pernas da mesa? Vc colocou T3 somente. Ao meu ver seria
    P1 que é o pilar menor e P2 que é o pilar maior, e aí o resto".
  - Aba **Fabricantes** na janela de Mesa: um cartão por estrutura (`VendorStructures`, Core),
    com as peças, a altura livre, os pilares de catálogo e a fonte. Hoje são as duas da
    Arctech (2P14 GC800MM-NS 10 e GC1000MM-NS 20), com o nome do fabricante (a mesa vai para
    o desenho como "Arctech 2P14 GC800"). "Usar esta estrutura" enche a aba do biposte
    (mesa, pórtico, mãos francesas, terças, tirantes, seções) e a abre; a monoposte fica
    guardada. Não muda o módulo nem o nome da mesa (renomear mesa do desenho é do usuário);
    os vãos escritos do fabricante só ficam com a largura do módulo dele, senão vai o vão-alvo.
  - O **biposte genérico** (`BifilaReference.Generic`, sem nome de fabricante) é a semente do
    cartão Biposte: o pórtico de referência sem mão francesa e sem tirante, pelo vão-alvo.
    Quem tiver mãos francesas e tirantes põe nas seções deles. Antes a semente era a GC800
    inteira, com as mãos francesas dela.
  - **P1 e P2 são calculados, não digitados.** A altura da mesa vem da faixa da ponta baixa do
    projeto, igual para monoposte e biposte (o motor assenta a mesa na mínima no chão plano);
    o P2 sai do P1, da distância entre os pilares e da inclinação. Digitar os dois daria três
    donos para a mesma altura. Então o Pórtico abre com P1 (pilar menor, o dianteiro) e P2
    (pilar maior, o traseiro), inteiros com o enterro, na **altura básica**: chão plano, ponta
    baixa na mínima do projeto (a da aba Projeto das Configurações, mesmo sem gravar) e o T3
    da mesa. Embaixo, quanto cada um tem acima do chão. O corte desenha o chão nessa altura
    e cota P1, P2 e T3.
  - O P2 do catálogo da Arctech (3,888) é maior que o da altura básica (3,588): tem a folga do
    chão caindo 10 % para trás (3,00 × 10 % = 0,300). O cartão do fabricante diz isso, e o
    teste confere que os números do catálogo fecham com a conta (P1 3,059 com a ponta baixa
    de 0,832; P2 de catálogo = P2 + 0,300).
  - Com uma estrutura de fabricante e a ponta baixa mínima do projeto diferente da que dá a
    altura livre dela (0,832 na GC800, 1,032 na GC1000), a janela avisa embaixo do P1 e do P2;
    não muda a configuração do projeto.
  - Testes: 10 no Core (o P1 e o P2 são os do motor no chão plano, no biposte e na monoposte;
    o enterro é o T3 ou o mínimo do projeto; os números de catálogo; o genérico sem nada de
    fabricante). As abas e o corte são de tela.
- 38.8 (30/10/2026), o que assumi:
  - Pedidos do Renan: "então suba para o servidor de produção" e "os arquivos pan do cliente
    vão para a página de gestão do clivus, onde uma pessoa valida os pans e vai integrando a
    plataforma de forma pública após validação".
  - Servidor (clivussolar, 2cc6561, no ar desde 30/10 às ~17h30; o trabalho foi de um fork e
    eu revisei antes do push): `GET /api/v1/modulos` só com os aprovados, `POST` com a chave
    do plugin cria **pendente**, a página `/gestao/modulos` aprova ou recusa (com motivo) e
    baixa o .PAN. Quem entra na gestão: as contas de `CLIVUS_ADM_EMAILS` (sem ela, a Develop).
    Os três módulos embutidos no plugin entram aprovados na tabela vazia. Conferido no ar: a
    lista com os 3, a gestão sem login vai para `/entrar`, o `POST` sem chave dá 401 com o
    `{"erro"}`. O contrato está em `plano/contrato-servidor-3d.md`.
  - Plugin: sem `CLIVUS_SERVICO`, a janela de Mesa lê os módulos do servidor do Clivus (o do
    Publicar 3D, com `/api/v1`); com ela, do serviço local, como antes. O cadastro leva a
    `CLIVUS_SERVIDOR_CHAVE` (só por HTTPS) e o idioma; a resposta "pendente" deixa o módulo só
    na janela que o mandou, escolhido, com o .PAN, e o recado diz que ele espera a validação.
    Sem a chave, o "Carregar .PAN" não pergunta se cadastra (o servidor recusaria); o
    "Cadastrar módulo..." diz que falta a chave.
  - Hoje a chave do plugin é uma só (a sua) e publica na conta Develop: até cada conta ter o
    seu token (etapa das licenças), todo envio aparece como "Enviado por Develop", e cliente
    sem a chave não manda módulo.
  - Fica de fora: tirar da lista um módulo já aprovado e avisar o cliente da recusa.
  - Testes: 20 no Core (`ModuleServiceReply`: o pendente, o `{"erro"}` do servidor e o
    `detail` do serviço local) e o gate do servidor (70 conferências, que reprova nos sete
    defeitos plantados). O HTTP do plugin é de tela.
- Incidente de 30/10/2026: a revisão do 38.7 (subagente) rodou `tools/traducoes.py --faltam`
  passando os fontes como destino, e nove .cs viraram "[]". Recuperei os versionados do git e
  refiz as edições a partir da conversa (o diff saiu idêntico, linha por linha); os dois novos
  foram reescritos. O `--faltam` agora só grava em .json fora de src/, tests/ e plano/
  (ba8874c), e a revisão foi refeita numa worktree isolada.

## Roteiro de tela (38.1 a 40.2)

1. **Nova estrutura (38.1 e 38.5):** Configurações › Estruturas › Nova estrutura… › **Fixa**.
   A janela de Mesa abre na aba "Perfil e módulo"; clicar no cartão **Biposte** abre a aba
   Biposte, no biposte genérico (sem mão francesa e sem tirante), com as seções Pórtico, Mãos
   francesas, Terças, Tirantes e Seções das peças. Mudar um campo, clicar Monoposte e voltar
   para Biposte: o campo continua mudado. No corte, conferir os dois pilares, a viga e as
   cotas T2, T2t e 3,000 m entre os pilares. "Usar esta mesa", Salvar no desenho, fechar e
   reabrir: ela está na lista com "(biposte)" e na Escolha das estruturas.
2. **Desenhar (38.2):** marcar só a bifila na Escolha, pôr a faixa da PB do projeto em
   0,84 a 1,60 (a GC800 pede 0,832 na face do módulo) e processar uma área. Orbitar uma
   mesa: 12 pilares (6 pórticos com dianteiro e traseiro), viga em cada pórtico, mãos
   francesas, 4 terças e o X de tirantes nos vãos 2 e 4 da linha de trás. Apagar uma mesa:
   tudo some junto.
3. **Ferramentas (38.3):** Análises › Ponta baixa › Inserir: um texto por pórtico, não dois.
   Pontas numa mesa bifila: os pilares continuam com 2,20 m de enterro. Trocar uma
   monoposte por bifila e voltar.
4. **Excel (38.4):** Exportar Excel com "Estrutura (biposte)" marcada: aba Estrutura com
   pilares por linha, vigas, mãos francesas, terças (barras e emendas) e tirantes; aba
   Pilares com a coluna Linha.
5. **3D (39.1):** Ver 3D numa área bifila: a estrutura aparece em cinza aço embaixo dos
   módulos.
6. **Mesas conforme os objetos (40.2):** no `PLAT A_Perfiles.dwg` (Panamá), criar a área
   pelo contorno de `_CONTORNO`, marcar só o biposte (GC800) na Escolha das estruturas, pôr
   o azimute olhando para o sul (Configurações › Parâmetros) e a faixa da PB em 0,84 a
   1,60. Processar › Conforme objetos › Todos. Conferir no relatório quantos objetos viram
   mesa e as divergências (no Aurora: objetos repetidos e "CELDAS NUEVAS" em cima das
   antigas); as marcas vermelhas na camada CLIVUS_DIVERGENCIAS; cada mesa em cima do seu
   objeto. Depois Regerar área: refaz pelos objetos.
7. **Tudo com biposte (39.2):** numa área com o biposte em uso (faixa da PB 0,84 a 1,60),
   passar por tudo e comparar com uma área monoposte:
   - Processar: Usina, Regerar área, Regerar todas;
   - Edição: Recalcular, Recalcular pendentes, Pontas, Validar, Recontar, Renomear, Estado,
     Marcar pendente, Trocar mesa, Regerar fileira, Apagar (cores, textos, sombras, strings),
     Apagar tudo, Grupos; copiar, mover e apagar uma mesa com o AutoCAD (o vigia);
   - Análises: PB e PA (um por pórtico, "Contado em: pórticos"), parte livre, enterrada e total
     do pilar (cada pilar), declividade; cores e quantificar;
   - Tags: fileiras, mesas, módulos, strings;
   - Sombras: árvore, sombras, por quê;
   - Elétrica: String, Configuração elétrica (UC, trafo, inversores, alocar, skid,
     numeração), Resumo, Rota de cabos (CC, CA, MT, valas, recolocar, recalcular, cabos,
     resumo);
   - Saídas: PVsyst, Excel (todas as abas; a Estrutura só no biposte), 3D (a caixa
     "Canaletas, carport e estrutura do biposte" liga e desliga as peças), Publicar 3D,
     TermoVision;
   - Salvar, fechar e abrir o DWG: as mesas, os pilares e as peças voltam.
8. **PAN (38.6):** com o serviço local no ar (`tools\servico-local.ps1`) e
   `CLIVUS_SERVICO=http://localhost:8765` (sem ela, desde o 38.8, vale o item 10), janela de Mesa › Perfil e
   módulo › Carregar .PAN... › `Panama\Docs\JAM66D46-720_LB.PAN`: o modelo vira "JA Solar
   JAM66D46-720/LB", as medidas 2,384 × 1,303 × 0,033, a potência 720 e os dados elétricos
   enchem; a pergunta "Cadastrar agora, com o PAN?" › Sim: o módulo entra na lista. Fechar e
   abrir a janela, escolher esse módulo na lista: os dados elétricos vêm junto.
9. **Fabricantes e altura básica (38.7):** com a faixa da PB do projeto em 0,8325 a 1,60
   (Configurações › Projeto), abrir a janela de Mesa. Aba **Fabricantes**: dois cartões da
   Arctech, cada um com as peças, a altura livre e os pilares de catálogo. "Usar esta
   estrutura" na GC800: abre a aba Biposte com as mãos francesas de 1,244 e 1,209, tirantes
   nos vãos 2 e 4, e o Pórtico começa com **P1 3,059** e **P2 3,588** (0,859 e 1,388 acima do
   chão). No corte, o chão na altura certa e as cotas P1, P2 e T3. Usar a GC1000: as mãos
   francesas mudam e aparece o recado de que ela pede a ponta baixa de 1,032. Voltar à
   primeira aba, Monoposte e Biposte de novo: a GC1000 continua na aba. Mudar a PB mínima do
   projeto e reabrir a janela: o P1 e o P2 acompanham.
10. **Módulos no servidor do Clivus (38.8):** sem `CLIVUS_SERVICO` e com a
    `CLIVUS_SERVIDOR_CHAVE`, abrir a janela de Mesa: embaixo do modelo, "Módulos do servidor
    do Clivus: 3". Carregar .PAN › `JAM66D46-720_LB.PAN` › "Cadastrar agora?" › Sim: o recado
    diz que ele foi para o servidor e espera a validação; o módulo fica escolhido, com os
    dados elétricos. No site, entrar com a conta Develop › cartão **Gestão**: o módulo
    pendente, com o .PAN para baixar. Aprovar. Fechar e abrir a janela de Mesa: ele está na
    lista ("Módulos do servidor do Clivus: 4"), e escolher ele traz os dados elétricos.
