# Protocolo de cada passo

Todo passo segue exatamente estas fases. Não pule nenhuma. É o mesmo protocolo do módulo de layout.

## 1. Entender
Leia o passo no arquivo da etapa. Se algo estiver ambíguo, PERGUNTE ao Renan antes de escrever código. Chutar regra de negócio é o erro mais caro deste projeto.

## 2. Testes primeiro
Escreva os testes do passo antes da implementação. Rode e confirme que falham pelo motivo certo.

## 3. Implementar o mínimo
Só o necessário para os testes do passo passarem. Sem generalizar para passos futuros.

## 4. Rodar TUDO
`tools/rodar-testes.ps1`. Todos os testes de todas as etapas anteriores (layout inclusive) precisam estar verdes, e `checar-acervo.ps1` precisa passar. Algo vermelho: conserte antes de seguir. Nunca edite teste antigo para passar.

## 5. Revisão de código (obrigatória, além dos testes)
Lance um subagente revisor que recebe só o diff do passo e este checklist, sem o seu raciocínio. Ele responde item por item:

- [ ] Alguma referência de CAD entrou em Core ou Geo?
- [ ] Alguma regra elétrica ou sagrada pode ser violada por entrada que os testes não cobrem?
- [ ] Vínculo elétrico derivado de posição física em algum lugar? (proibido)
- [ ] Desalocar/apagar alocação toca na geometria da string? (proibido)
- [ ] String podendo cair em mais de um inversor? (proibido)
- [ ] Unidades: grau com radiano, cm com m, tensão trocada?
- [ ] Casos de borda: zero strings, zero inversores, mesa sem config, inversor cheio, bloco vazio, seleção vazia?
- [ ] Transação aberta dentro de laço? Objeto do banco usado fora da transação?
- [ ] Exceção engolida sem log? Caso que deveria avisar e ficou calado?
- [ ] Código morto, duplicado ou fora do escopo do passo?

Corrija o que ele apontar, rode tudo de novo.

## 6. Commit
Um commit por passo: `eletrica X.Y: <descrição>`.

## 7. Relatório e PARADA
Atualize `PROGRESSO.md`: status `AGUARDANDO VALIDAÇÃO`. Escreva para o Renan, curto:
- o que foi feito;
- placar dos testes;
- o que o revisor apontou e o que foi corrigido;
- **o que ele precisa conferir** (o item Validação do Renan do passo), com instrução de como.

E PARE.

## 8. Depois da validação
Se o Renan aprovar e o passo gerou resultado de referência, os arquivos esperados entram no acervo por ele. Se reprovar, status `REPROVADO` com o motivo, e o passo recomeça na fase 1.
