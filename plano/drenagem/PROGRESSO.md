# Progresso da drenagem

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`, ver `LEIA-PRIMEIRO.md`).

**Plano aprovado pelo Renan em 21/10/2026 ("pode fazer"). As três perguntas abertas
ficaram sem resposta; segui com as decisões abaixo, que ficam para o Renan confirmar.**

## Etapa 25: base

- 25.1 Painel Drenagem na ribbon: AGUARDANDO VALIDAÇÃO
- 25.2 Layers da drenagem: VALIDADO (automático)
- 25.3 Declividade e aspecto do TIN: VALIDADO (automático)
- 25.4 Caminho de escoamento: VALIDADO (automático)

## Etapa 26: Análise › Flechas

- 26.1 Janela das flechas: AGUARDANDO VALIDAÇÃO
- 26.2 Desenho das flechas: AGUARDANDO VALIDAÇÃO
- 26.3 Legenda: AGUARDANDO VALIDAÇÃO

## Etapa 27: Análise › Linhas e perfil

- 27.1 Linha de análise: AGUARDANDO VALIDAÇÃO
- 27.2 Amostragem do perfil: VALIDADO (automático)
- 27.3 Gráfico do perfil: AGUARDANDO VALIDAÇÃO
- 27.4 Gráfico vivo: AGUARDANDO VALIDAÇÃO

## Etapa 28: Desenho › Canaleta

- 28.1 Biblioteca de seções: VALIDADO (automático)
- 28.2 Janela da canaleta: AGUARDANDO VALIDAÇÃO
- 28.3 Perfil da canaleta: AGUARDANDO VALIDAÇÃO
- 28.4 Fundo com declividade mínima: AGUARDANDO VALIDAÇÃO (decisão 2)
- 28.5 Editar e desfazer: AGUARDANDO VALIDAÇÃO

## Etapa 29: canaleta em 3D

- 29.1 Sólido da canaleta: AGUARDANDO VALIDAÇÃO
- 29.2 Quantitativos: VALIDADO (automático)
- 29.3 No Ver 3D: AGUARDANDO VALIDAÇÃO

## Etapa 30: Memória › Bacias

- 30.1 Cálculo das bacias: VALIDADO (automático)
- 30.2 Desenho das bacias: AGUARDANDO VALIDAÇÃO
- 30.3 Ajuste manual: AGUARDANDO VALIDAÇÃO
- 30.4 Lista canaleta × bacia: AGUARDANDO VALIDAÇÃO

## Etapa 31: Memória › cálculo

- 31.1 Motor hidráulico: VALIDADO (automático)
- 31.2 Parâmetros do projeto: AGUARDANDO VALIDAÇÃO
- 31.3 Tabelas na tela: AGUARDANDO VALIDAÇÃO
- 31.4 Exportar Excel: VALIDADO (automático)
- 31.5 Seção a partir da conta: AGUARDANDO VALIDAÇÃO
- 31.6 Dissipador e transposição: AGUARDANDO VALIDAÇÃO (decisão 3)

## Etapa 32: segunda rodada (21/10/2026, depois da primeira tela)

- 32.1 Linha no sentido desenhado, X × Y, corre e sobe: AGUARDANDO VALIDAÇÃO
- 32.2 Rastreio planta ↔ gráfico, liga e desliga: AGUARDANDO VALIDAÇÃO
- 32.3 Nome da canaleta em campo: AGUARDANDO VALIDAÇÃO
- 32.4 Menu Desenhos: AGUARDANDO VALIDAÇÃO
- 32.5 Dissipador como opção, nítido na memória: AGUARDANDO VALIDAÇÃO
- 32.6 Caixa entre canaletas: AGUARDANDO VALIDAÇÃO
- 32.7 Exportar canaletas: AGUARDANDO VALIDAÇÃO
- 32.8 Vazão no ponto: AGUARDANDO VALIDAÇÃO
- 32.9 Plúvio (Memória › Cálculo: Estado e Local, já carregado pela coordenada): AGUARDANDO VALIDAÇÃO
- 32.10 Gráfico em grupo, Recriar gráfico e Apagar gráfico: AGUARDANDO VALIDAÇÃO
- 32.11 Vazão no ponto com a cruz e o placar na tela: AGUARDANDO VALIDAÇÃO
- 32.12 Água andando pelo fundo real (barriga, transbordo, duas saídas): AGUARDANDO VALIDAÇÃO
- 32.13 Vazão também no gráfico; trecho ruim do fundo explicado (o que a água faz e quanto cavar): AGUARDANDO VALIDAÇÃO

## Decisões tomadas sem resposta (ficam para o Renan confirmar)

1. Altura adotada: arredonda para cima de 0,05 m em 0,05 m, e o passo do
   arredondamento é parâmetro da Memória (0,10 reproduz o 0,50 do memorial).
   O y adotado é sempre editável.
2. Fundo da canaleta: padrão "borda no terreno" (fundo = terreno − y) com
   alerta e pintura do trecho abaixo de 0,5 % ou invertido; "fundo com
   declividade mínima" existe como opção na janela (28.4).
3. Dissipador e transposição: entram nas tabelas da Memória e são desenhados
   como bloco 3D simples (31.6).

## Observações

- Interseções de canaletas: fora de escopo por decisão do Renan ("depois a gente bola").
- A Kerby do memorial não é a de livro; seguimos o memorial (regra 6).
- Revisão por subagente feita por etapa (25–26, 27–29, 30–31), não por passo: os passos
  de uma etapa dividem os mesmos arquivos. Os bugs que ela achou foram corrigidos com teste.
- TIN de curvas de nível (Itatiba) tem triângulos planos e patamares de mais de 20 m entre
  duas curvas: o caminho da água sonda em volta até 50 m antes de dizer que empoça.
- A canaleta não guarda o sentido: a montante é a ponta mais alta do terreno, lida a cada
  vez. Canaleta com as duas pontas quase na mesma cota pode virar de sentido com um grip.
  Fica para o Renan dizer se quer o sentido gravado.
- 30.3: o ajuste manual é uma polilinha fechada marcada com a canaleta (não o grip no
  hatch, que é refeito a cada cálculo).
- A vazão de cada canaleta vem só da bacia dela: canaleta que deságua em outra não soma
  (interseções, fora de escopo).
- Altura sugerida da meia-cana: o menor raio (no passo) em que lâmina + bordo livre cabem;
  o bordo livre é o da Tabela 36 do DNIT (valetas), o mesmo da trapezoidal.

- Plúvio 2.1 (21/10/2026): o plugin lê a base instalada (`GPRH\Arquivos Comuns\Estados_Plu21.bin`,
  549 estações, e `Localidades_Plu21.bin`, 4937 cidades de 8 estados), nunca leva cópia. A
  interpolação é o inverso da distância à 10ª potência, distância em graus sem cosseno, sobre
  todas as estações: a única que reproduz os quatro números de Valparaiso do memorial. O formato
  dos dois arquivos foi lido do próprio arquivo (não é documentado).
- A janela da memória só pré-carrega o Plúvio quando o desenho ainda não gravou a chuva (parâmetros
  iguais aos do memorial); gravado, mostra o gravado.
- Escolher canaleta (Vazão, Dissipador, Pegar da canaleta, Ajuste manual das bacias) agora é clique
  em ponto, com a cruz: vale a linha mais perto do clique (30 pixels). O quadradinho pegava o 3D e
  a pintura que ficam em cima da canaleta ("Isso não é uma canaleta", tela do Renan).
- Recriar e Apagar gráfico ficam na ribbon (Análise) e na janela da Canaleta; na ribbon só uma vez
  (regra dos botões sem repetição).

- 32.12 (tela do Renan, canaleta 3 de Itatiba com barriga): a vazão no ponto crescia em linha reta
  da montante até a ponta, como se o fundo descesse sempre. Agora a água que as bacias põem em
  cada estaca anda pelo fundo como ele está (ChannelFlowRouting): barriga até a altura da parede
  enche e passa; mais funda que a parede, a água junta no fundo dela e transborda (alerta). A vazão
  de projeto da memória é a da estaca com mais água (igual à da bacia quando o fundo desce até a
  saída: o memorial de Valparaiso não muda). O tc e a chuva continuam os da bacia inteira.
- 32.13: o traço grosso do gráfico não era "a água volta": é o trecho do fundo abaixo da mínima
  (amarelo em planta) ou plano/subindo (vermelho). Agora cada um ganha uma nota embaixo do fundo,
  no gráfico, e o alerta da memória diz o mesmo (ChannelBottomAdvice): "0,2% < 0,5%: água lenta",
  "plano: água parada", "sobe: a água volta" (sobe sem barriga, sai pela ponta de cima),
  "barriga de X m: enche e passa" ou "barriga de X m > parede Y m: transborda"; e "aprofundar Z m".
  O Z é o máximo, no trecho (e na barriga dele), entre o fundo atual e o fundo que desce a mínima
  desde a ponta de cima (o que o modo "Fundo com declividade mínima" faria). A barriga daqui é a
  das duas pontas como saída; a da Vazão no ponto (32.12) também conta barrigas encadeadas.

## Roteiro de tela das mudanças de 21/10/2026 (tarde)

1. Memória › Cálculo num desenho com localização: a linha "Plúvio 2.1" já vem com o estado e
   "Local do desenho", e K, a, b, c preenchidos (o recado diz a estação mais perto). Troque o estado
   e escolha uma cidade ou uma estação: os números e a "Chuva (IDF)" mudam. Calcular grava.
2. Análise › Linhas: um clique no gráfico pega o gráfico inteiro (grupo); arraste. Passe o mouse no
   gráfico: a seta anda na linha no lugar novo.
3. Análise › Recriar gráfico (Enter = todos): o gráfico refeito fica onde você deixou. Análise ›
   Apagar gráfico: some; mexa na linha: não volta; Recriar: volta no mesmo lugar. Na janela da
   Canaleta, os mesmos dois botões.
4. Memória › Vazão no ponto: a cruz (não o quadradinho); passando o mouse na canaleta, o placar grande
   mostra "Vazão: Canaleta 1 · 35,2 m · Q 12,3 l/s (fim 24,8)"; clique: o valor fica escrito ao lado
   do ponto até sair. Dissipador e Pegar da canaleta também com a cruz.
5. Na canaleta com barriga (Canaleta 3 de Itatiba): Bacias e Cálculo de novo; Vazão no ponto perto da
   ponta da direita mostra pouca água "→ a barriga em ~120 m (transborda)" e, embaixo, quanto seria com
   o fundo descendo até a ponta; no fundo da barriga, a maior. Memória › Alertas: "a água não tem
   saída na estaca …".
6. Vazão no ponto com o mouse dentro do gráfico de uma canaleta: o placar dá a vazão da estaca da
   vertical (a mesma do rastreio) e um círculo marca o ponto no gráfico e na planta; clique no
   gráfico: o "Q … l/s" fica escrito nos dois. Canaleta › Atualizar (ou Recriar gráfico) numa
   canaleta com traço grosso: embaixo de cada trecho, o que a água faz e "aprofundar X m".

## Roteiro de tela da etapa 32

1. Análise › Linhas: clique A, outros pontos e B (a linha aparece enquanto clica; Desfazer tira o
   último), Enter. A linha nasce já com o gráfico; o quadro do gráfico segue o mouse: clique onde quer
   (Enter ou Esc deixa ao lado). O gráfico começa no A, tem "Cota (m)" e, em cada trecho, "corre X m ·
   sobe/desce Y m" com % e graus; em planta, "A", "B" e o nome da linha.
2. Passe o mouse no gráfico: a seta magenta anda na linha em planta. Passe o mouse na linha ou na
   canaleta: a vertical anda no gráfico, com a dica (estaca, terreno e fundo). Análise › Rastrear
   desliga e liga.
3. Cada canaleta com o nome escrito ao longo dela (Canaleta 1 · Trap 30/60).
4. Desenhos (o antigo Canaleta) › Canaleta, Caixa, Dissipador, Exportar canaletas.
5. Desenhos › Dissipador: clique perto da saída da canaleta: o trapézio com o modelo DES em planta e o
   sólido em 3D. Memória › Cálculo › aba Dissipadores: a conta escrita (B, L1, L2, comprimento,
   velocidade de saída). Canaleta com saída rápida e sem dissipador: alerta "sugere dissipador".
6. Desenhos › Caixa: clique no encontro das canaletas: caixa oca, fundo 10 cm abaixo da canaleta mais
   funda que chega, nome CX 1. Medidas muda a caixa; Remover tira.
7. Desenhos › Exportar canaletas: Excel com Canaletas, Por tipo (metros e quantidade), Caixas e
   Dissipadores.
8. Memória › Vazão no ponto (depois das Bacias): clique em pontos da canaleta e veja a vazão crescer
   até a do fim.

## Roteiro de tela (o que o Renan confere no Civil 3D)

Antes: `tools/publicar-bundle.ps1` e `tools/instalar.ps1` com o Civil 3D fechado. Abra um
desenho com terreno (Terreno) e uma área de implantação.

1. **25.1** Aba Clivus Solar › painel **Drenagem**: Análise (suspenso: Flechas, Linhas),
   Canaleta (grande), Memória (suspenso: Bacias, Cálculo). Confira os ícones nos dois temas.
2. **26** Análise › Flechas: Onde = Áreas de implantação, 5 m, Gerar. Flechas coloridas
   pelas faixas (verde até 5 %, laranja até 10 %, vermelho até 15 %, roxo até 20 %, azul
   acima), apontando morro abaixo; legenda à direita em cima. Teste Janela (dois cantos),
   "Escrever a declividade", outras faixas, Apagar. Orbite: as flechas em cima do terreno.
3. **27** Análise › Linhas: clique 3 ou 4 pontos no terreno, Enter, clique onde vai o
   gráfico (ou Enter = ao lado). O gráfico: terreno verde, grade, banda com distância e
   cotas, "% | grau" e seta da água em cada trecho de 20 m, título com comprimento, cotas,
   declividade média e exagero. Arraste um vértice da linha (grip): ela volta ao terreno e o
   gráfico muda sozinho. Copie a linha: a cópia ganha nome e gráfico. Apague: o gráfico some.
   "Selecionar" numa polilinha sua: vira linha (a sua some).
4. **28** Canaleta: confira o desenho cotado mudando ao digitar (trapezoidal e meia-cana),
   salve uma seção sua, Converter linhas… e escolha a linha. O gráfico vira o da canaleta
   (fundo azul, topo da parede); trecho que sobe ou desce menos de 0,5 % fica vermelho no
   gráfico e pintado em planta (amarelo/vermelho). Mude para "Fundo com declividade mínima"
   e converta de novo: a pintura some e a canaleta aprofunda onde precisa. Pegar da
   canaleta…, Voltar a linha….
5. **29** Orbite a canaleta: o sólido da parede seguindo o terreno, e o dissipador no pé
   (a ponta mais baixa). Saída › 3D: as canaletas aparecem, com a caixa "Canaletas" para
   ligar e desligar.
6. **30** Memória › Bacias: Calcular bacias. Um hatch por canaleta (com a cor dela), a
   borda no terreno, o rótulo com área, L e ΔH; a área cuja água não chega a canaleta
   nenhuma em vermelho, com o motivo no relato. Clique numa linha da lista: zoom nela.
   Ajuste manual…: polilinha fechada sua + canaleta; Calcular de novo: a lista diz "manual".
7. **31** Memória › Cálculo: os parâmetros do memorial de Valparaiso; Calcular; abas
   Bacias, Chuva, Vazão, Canaletas, Dimensões, Dissipadores, Transposições, Quantitativos,
   Alertas. Ponha uma camada de via em "Camadas das vias" com uma linha cruzando a canaleta:
   Calcular e conferir a transposição (tabela e laje 3D depois de Canaleta › Atualizar).
   Exportar Excel…. Aplicar altura sugerida… numa canaleta com alerta de lâmina: a seção
   cresce, o 3D e o perfil são refeitos e o alerta some.
