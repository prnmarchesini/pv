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
- 38.6 O PAN enche o módulo e sobe para o servidor: PENDENTE

## Etapa 39: o resto do Clivus

- 39.1 3D e TermoVision: AGUARDANDO VALIDAÇÃO
- 39.2 Tudo funciona com biposte (a prova): PENDENTE

## Etapa 40: mesas conforme os objetos do cliente

- 40.1 Reconhecer as mesas do cliente: PENDENTE
- 40.2 Gerar conforme objetos em campo: PENDENTE

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

## Roteiro de tela (38.1 a 39.1)

1. **Nova estrutura (38.1 e 38.5):** Configurações › Estruturas › Nova estrutura… › **Fixa**.
   A janela de Mesa abre na aba "Perfil e módulo"; clicar no cartão **Biposte** abre a aba
   Biposte, na Arctech 2P14 GC800, com as seções Pórtico, Mãos francesas, Terças, Tirantes e
   Seções das peças. Mudar um campo, clicar Monoposte e voltar para Biposte: o campo
   continua mudado. No corte, conferir os dois
   pilares, a viga, as duas mãos francesas e as cotas T2, T2t e 3,000 m entre os pilares.
   Apertar GC1000 e ver as mãos francesas mudarem. "Usar esta mesa", Salvar no desenho,
   fechar e reabrir: ela está na lista com "(bifila)" e na Escolha das estruturas.
2. **Desenhar (38.2):** marcar só a bifila na Escolha, pôr a faixa da PB do projeto em
   0,84 a 1,60 (a GC800 pede 0,832 na face do módulo) e processar uma área. Orbitar uma
   mesa: 12 pilares (6 pórticos com dianteiro e traseiro), viga em cada pórtico, mãos
   francesas, 4 terças e o X de tirantes nos vãos 2 e 4 da linha de trás. Apagar uma mesa:
   tudo some junto.
3. **Ferramentas (38.3):** Análises › Ponta baixa › Inserir: um texto por pórtico, não dois.
   Pontas numa mesa bifila: os pilares continuam com 2,20 m de enterro. Trocar uma
   monoposte por bifila e voltar.
4. **Excel (38.4):** Exportar Excel com "Estrutura (bifila)" marcada: aba Estrutura com
   pilares por linha, vigas, mãos francesas, terças (barras e emendas) e tirantes; aba
   Pilares com a coluna Linha.
5. **3D (39.1):** Ver 3D numa área bifila: a estrutura aparece em cinza aço embaixo dos
   módulos.

