# Protocolo de cada passo

Todo passo segue exatamente estas fases. Não pule nenhuma. É o mesmo protocolo dos planos anteriores.

## 1. Entender

Leia o passo no arquivo da etapa. Se algo estiver ambíguo, PERGUNTE ao Renan antes de escrever código. Chutar regra de negócio é o erro mais caro deste projeto.

## 2. Testes primeiro

Escreva os testes do passo antes da implementação. Rode e confirme que falham pelo motivo certo.

## 3. Implementar o mínimo

Só o necessário para os testes do passo passarem. Sem generalizar para passos futuros.

## 4. Rodar TUDO

`tools/rodar-testes.ps1`. Todos os testes de todas as etapas anteriores (layout e elétrica inclusive) precisam estar verdes, e `checar-acervo.ps1` precisa passar. Algo vermelho: conserte antes de seguir. Nunca edite teste antigo para passar.

## 5. Revisão de código (obrigatória, além dos testes)

Lance um subagente revisor que recebe só o diff do passo e este checklist, sem o seu raciocínio. Ele responde item por item:

- [ ] Alguma referência de CAD entrou em Core ou Geo?
- [ ] Alguma regra de roteamento, elétrica ou sagrada pode ser violada por entrada que os testes não cobrem?
- [ ] O cabo pode cortar o meio da mesa em algum caso?
- [ ] Algum caminho escolhido que não é o de menor comprimento?
- [ ] Algum caso de falha que fica calado, sem aviso e sem pintura?
- [ ] A contagem pode ser exibida sem recálculo prévio?
- [ ] O comprimento está sendo medido em 3D, incluindo as descidas de vala?
- [ ] O sistema está decidindo bitola ou aprovando cabo em algum lugar? (proibido)
- [ ] Unidades: metro com centímetro, mm² com mm, grau com radiano, V com kV?
- [ ] Casos de borda: zero strings, vala vazia, vala fora do raio, vala que não passa da mesa, string exatamente no meio da mesa, trecho sem par na cadeia, cabo de comprimento zero?
- [ ] Transação aberta dentro de laço? Objeto do banco usado fora da transação?
- [ ] Exceção engolida sem log?
- [ ] Código morto, duplicado ou fora do escopo do passo?

Corrija o que ele apontar, rode tudo de novo.

## 6. Commit

Um commit por passo: `rota X.Y: <descrição>`.

## 7. Relatório e PARADA

Atualize `PROGRESSO.md`: status `AGUARDANDO VALIDAÇÃO`. Escreva para o Renan, curto:

- o que foi feito;
- placar dos testes;
- o que o revisor apontou e o que foi corrigido;
- **o que ele precisa conferir** (o item Validação do Renan do passo), com instrução de como.

E PARE.

## 8. Depois da validação

Se o Renan aprovar e o passo gerou resultado de referência, os arquivos esperados entram no acervo por ele. Se reprovar, status `REPROVADO` com o motivo, e o passo recomeça na fase 1.
