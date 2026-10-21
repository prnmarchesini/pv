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
