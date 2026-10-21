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
