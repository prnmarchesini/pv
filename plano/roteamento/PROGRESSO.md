# Progresso do roteamento de cabo

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`, ver `LEIA-PRIMEIRO.md`).

## Etapa 17: base comum das rotas

- 17.1 Botão e abas (CC, Combiner, CA, MT) com pré-requisitos da cadeia: AGUARDANDO VALIDAÇÃO
- 17.2 Layers e cores por rota: PENDENTE
- 17.3 Profundidade da vala por aba: PENDENTE
- 17.4 Seleção da vala em campo e atribuição automática da layer: PENDENTE
- 17.5 Motor de percurso 3D e comprimento: PENDENTE
- 17.6 Lance como entidade com GUID e vínculos: PENDENTE
- 17.7 Mecanismo comum de aviso e pintura: PENDENTE
- 17.8 Recontagem antes de qualquer contagem: PENDENTE
- 17.9 Apagar (tudo ou por seleção): PENDENTE

## Etapa 18: rota CC

- 18.1 Saída da string e contorno da mesa (nunca cortar o miolo): PENDENTE
- 18.2 Escolha do lado de menor comprimento: PENDENTE
- 18.3 Atribuição manual de lado: PENDENTE
- 18.4 Chegada na vala e percurso por ela: PENDENTE
- 18.5 Vala que não passa da mesa: avisar e pintar: PENDENTE
- 18.6 Escolha do cabo e botão Gerar: PENDENTE
- 18.7 Dois lances por string (positivo e negativo): PENDENTE
- 18.8 Ver cabos CC com tabela e totalização: PENDENTE

## Etapa 19: combiner box

- 19.1 Cadastro, dimensão, tag e alocação em campo: PENDENTE
- 19.2 Vínculo na cadeia e alocação de strings na combiner: PENDENTE
- 19.3 Aba Combiner na rota de cabos (dois trechos): PENDENTE

## Etapa 20: rota CA

- 20.1 Raio virtual de busca da vala: PENDENTE
- 20.2 Percurso pela vala até o trafo: PENDENTE
- 20.3 Vala fora do raio: avisar e pintar: PENDENTE
- 20.4 Escolha do cabo, gerar e tabela: PENDENTE

## Etapa 21: rota MT

- 21.1 Raio virtual nos dois lados: PENDENTE
- 21.2 Destino pela UC vinculada, não pela mais próxima: PENDENTE
- 21.3 Vala fora do raio: PENDENTE
- 21.4 Escolha do cabo, gerar e tabela: PENDENTE

## Etapa 22: bibliotecas

- 22.1 Biblioteca de cabos (preenchida inicial, editável): PENDENTE
- 22.2 Escolha do cabo por rota e troca barata: PENDENTE
- 22.3 Biblioteca de módulos com arquivo PAN: PENDENTE
- 22.4 Temperaturas máxima e mínima nas configurações do projeto: PENDENTE
- 22.5 Método de instalação da NBR 5410: PENDENTE

## Etapa 23: tabelas e memorial

- 23.1 Voc na mínima e tensão de operação na máxima: PENDENTE
- 23.2 Correntes do CC: PENDENTE
- 23.3 Queda de tensão no CC: PENDENTE
- 23.4 Tabela do CC: PENDENTE
- 23.5 Cálculos de CA e MT com corrente admissível lado a lado: PENDENTE
- 23.6 Tabelas de CA e MT: PENDENTE
- 23.7 Recalcular ao trocar bitola, sem redesenhar: PENDENTE
- 23.8 Exportar tabelas: PENDENTE

## Etapa 24: resumo de cabos

- 24.1 Aba de cabos no resumo da usina: PENDENTE
- 24.2 Lista de material de cabo: PENDENTE

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

## Observações

- A combiner box tinha ficado fora do plano elétrico (etapas 11 a 16) por decisão do Renan. Entra agora, na etapa 19: a cadeia de `plano/eletrica/04-modelo-de-dados.md` (UC -> trafo -> inversor -> string) ganha o elo opcional inversor -> combiner -> string, e a alocação de strings no inversor que já existe (14.3 elétrica) continua valendo quando não há combiner.
- A atribuição manual de lado (18.3) era passo 2 na conversa, mas o Renan autorizou já deixar previsto.
- (coisas fora do escopo que aparecerem vão aqui)
