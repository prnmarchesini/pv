# Progresso da parte elétrica

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO.

## Etapa 11: Corrente contínua (strings)
- 11.1 Modelo de tipo de string e biblioteca: AGUARDANDO VALIDAÇÃO
- 11.2 Seleção de mesas e representação cartesiana dos módulos: AGUARDANDO VALIDAÇÃO
- 11.3 Marcar polaridade e traçado convencional: AGUARDANDO VALIDAÇÃO
- 11.4 Leapfrog e traçado livre por cliques: AGUARDANDO VALIDAÇÃO
- 11.5 Editar, apagar, clonar, espelhar modelo: AGUARDANDO VALIDAÇÃO
- 11.6 Aba Gerar: casar config com grupos de mesas: AGUARDANDO VALIDAÇÃO
- 11.7 Desenhar traçado respeitando a declividade do módulo: AGUARDANDO VALIDAÇÃO
- 11.8 Avisar mesa que não casou com nenhuma config: AGUARDANDO VALIDAÇÃO

## Etapa 12: Subestação
- 12.1 Cadastro de UC, modo compartilhado (C1, C2) e vínculo com trafo: AGUARDANDO VALIDAÇÃO
- 12.2 Modo unitário (bloquinhos independentes): AGUARDANDO VALIDAÇÃO
- 12.3 Dimensão, tag e alocação em campo: AGUARDANDO VALIDAÇÃO

## Etapa 13: Transformador
- 13.1 Cadastro genérico do trafo e tabela de padrões: AGUARDANDO VALIDAÇÃO
- 13.2 Dimensão, apelido como tag e alocação em campo: AGUARDANDO VALIDAÇÃO

## Etapa 14: Inversor
- 14.1 Modelo de inversor (MPPT, entradas, total): AGUARDANDO VALIDAÇÃO
- 14.2 Lista de inversores por modelo e contagem: AGUARDANDO VALIDAÇÃO
- 14.3 Alocação manual de strings em campo (trava, contagem ao vivo, Ctrl desseleciona, destaque): AGUARDANDO VALIDAÇÃO
- 14.4 Aviso de excesso de capacidade: AGUARDANDO VALIDAÇÃO
- 14.5 Editar, selecionar todas, apagar todas (solta vínculo, não apaga geometria): AGUARDANDO VALIDAÇÃO
- 14.6 Dimensão, tag e alocação do inversor em campo: AGUARDANDO VALIDAÇÃO
- 14.7 Agrupamento de inversores por trafo (skid): AGUARDANDO VALIDAÇÃO

## Etapa 15: Numeração das strings
- 15.1 Composição da tag em três pedaços (trafo, inversor, string): AGUARDANDO VALIDAÇÃO
- 15.2 Configuração da varredura (sentido) e blocos: AGUARDANDO VALIDAÇÃO
- 15.3 Ordem dos blocos na lista: AGUARDANDO VALIDAÇÃO
- 15.4 Botão Gerar: varre e cria tags no desenho: AGUARDANDO VALIDAÇÃO
- 15.5 Edição granular (apagar tudo, por inversor, refazer inversor, regerar bloco): AGUARDANDO VALIDAÇÃO

- Aba Numeração, 05/10/2026 (Renan: "o botão selecionar mesas não deixa
  selecionar, e cada bloco precisa de dois sentidos..."): AGUARDANDO VALIDAÇÃO.
  - O Selecionar pedia a seleção direto do clique da janela solta, fora de
    comando: o AutoCAD devolvia a pergunta na hora, sem deixar selecionar, e
    o código saía calado. Agora o botão esconde a janela e manda
    CLIVUS_NUMERACAO_MESAS (seleção dentro de comando, mesas do bloco
    destacadas); a janela volta com a frase do resultado.
  - Dois sentidos (o que avança e o da faixa) na usina e em cada bloco, como a
    atribuição da aba Inversor; `NUMERACAO_VARREDURA` formato 2 (o 1 é lido
    com o sentido na faixa de antes: a ordem de desenho antigo não muda).
  - Cada bloco numa linha: nome, "N mesa(s), M string(s)", os dois sentidos e
    Selecionar, Mostrar, ↑, ↓, ✎ (renomear), Apagar; resumo embaixo (em
    blocos, fora de bloco). Assumido: Selecionar substitui as mesas do bloco
    (como antes); string conta no bloco da mesa do primeiro módulo (a mesma
    regra do Regerar bloco); "Regerar bloco escolhido" usa a linha clicada.

## Etapa 16: Resumo do sistema
- 16.1 Menu de resumo completo consolidado pela cadeia de vínculo: AGUARDANDO VALIDAÇÃO

## Rodada de 04/10/2026 (Renan: "pode fazer tudo até o final depois eu volto revisando")

Tudo feito de uma vez: 11.1 por mim; 11.2–11.8 (agente A), 12–14 (agente B),
15–16 (agente C), cada etapa com revisão por subagente e o que ele apontou
corrigido; juntado e testado inteiro (bateria verde, nível 2 com todos os
casos elétricos). Decisões tomadas sem perguntar estão em cada item.

### Roteiro de tela (na ordem do trabalho)

**Strings (botão String)**
1. Configuração > Adicionar tipo de string: selecione uma mesa 2V junto com
   uma curva de nível, Enter: o tipo entra na lista e o cartesiano mostra as
   duas fileiras (a curva foi ignorada). Repita com duas mesas de 14 vizinhas:
   as duas aparecem com o vão entre elas. (11.1, 11.2)
2. No cartesiano: clique no módulo da coluna 1 de baixo e no da última coluna,
   Concluir string: + na coluna 1, − na última. (11.3)
3. Leapfrog + Fileira inteira, clique na coluna 1: + e − em módulos vizinhos.
   Convencional em U (1→7 embaixo, sobe, volta à 1): − ao lado do +. (11.4)
4. Clonar, Espelhar o clone: + e − trocaram; o original igual. (11.5)
5. Aba Gerar, numa área com mesas de 28 e de 14: cada tipo só nos grupos
   iguais; orbite no 3D: o traçado acompanha os módulos a 5 cm; gerar de novo
   não duplica. Só com o tipo de 28: aviso nominal de cada mesa de 14, que fica
   selecionada. (11.6–11.8)

**Configuração elétrica (botão Configuração elétrica)**
6. Transformador: Novo trafo e Novo do padrão (2.500 kVA, 800 V / 13.800 V);
   editar e salvar. Alocar em campo dois trafos: caixa 0,80 m acima do
   terreno, tag de cima; renomear a tag muda o letreiro. (13.1, 13.2)
7. Subestação: C1 e C2 compartilhadas, C1 com T1 e T2 (em C2, T1 aparece
   travado "(em C1)"); duas unitárias com trafos diferentes; alocar em campo.
   (12.1–12.3)
8. Inversor: Novo modelo 1×2 e "Huawei 250" 5×4 (total 20); criar 4 de um e 2
   do outro (Inversor 1 a 6, 0 strings). "+" num inversor: só strings entram,
   contagem ao vivo, Shift tira (Ctrl não), Enter volta com a contagem. Num
   modelo 2×2 aloque 6: aviso vermelho (avisa, não bloqueia). Soltar todas: as
   strings continuam no desenho, livres; string de outro inversor é recusada.
   Alocar dois inversores em campo; Skid: 4 inversores no T1, 2 no T2.
   (14.1–14.7)
9. Numeração: separador Ponto (T1.I1.S1), depois colado sem trafo (1S1);
   dois blocos com sentidos diferentes; subir/descer bloco muda a sequência;
   Gerar tags; Regerar bloco só mexe nele; Apagar/Refazer inversor. (15.1–15.5)

**Resumo elétrico**
10. Numa usina montada: subestações → trafos → inversores → strings → módulos
    e kWp; conferir contra o desenho; pendências em vermelho. (16.1)

### Decisões que o Renan confirma
- Excesso de capacidade do inversor avisa e não bloqueia.
- Desselecionar na alocação é Shift (o do AutoCAD); Ctrl não foi feito.
- Número do trafo e do inversor na tag = a posição na lista do cadastro;
  inversor sem trafo sai sem o pedaço do trafo (ex. I4.S1), avisado.
- String: pelo menos 2 módulos; tipo de mais mesas tem prioridade no gerar;
  mesas "vizinhas" = mesma fileira, letreiros seguidos.
- Unitárias numeradas U1, U2 (separadas das C1, C2).
- Trafos padrão: 800 V → 13,8 kV (1250/2500/3150 kVA), 800 V → 34,5 kV
  (2500/3150 kVA), 380 V → 13,8 kV (500 kVA).

## Observações
- Mesa recalculada troca os GUIDs dos módulos: as strings dela ficam soltas e
  regerar não as reconhece (desenha as novas sem apagar as velhas). Precisa de
  uma regra (reatar pela coluna/fileira, ou apagar as órfãs) — próximo passo.
