# Protocolo de cada passo

Todo passo segue exatamente estas fases. Não pule nenhuma.

## 1. Entender
Leia o passo no arquivo da etapa. Se algo estiver ambíguo, PERGUNTE ao Renan antes de escrever código. Chutar regra de negócio é o erro mais caro deste projeto.

## 2. Testes primeiro
Escreva os testes do passo antes da implementação. Rode e confirme que falham pelo motivo certo.

## 3. Implementar o mínimo
Só o necessário para os testes do passo passarem. Sem generalizar para passos futuros.

## 4. Rodar TUDO
`tools/rodar-testes.ps1`. Todos os testes de todas as etapas anteriores precisam estar verdes, e `checar-acervo.ps1` precisa passar. Algo vermelho: conserte antes de seguir. Nunca edite teste antigo para passar.

## 5. Revisão de código (obrigatória, além dos testes)
Lance um **subagente revisor** que recebe só o diff do passo e este checklist, sem o seu raciocínio. Ele responde item por item:

- [ ] Alguma referência de CAD entrou em Core ou Geo?
- [ ] Alguma regra sagrada pode ser violada por uma entrada que os testes não cobrem?
- [ ] Unidades: algum grau misturado com radiano, cm com m?
- [ ] Casos de borda: lista vazia, um elemento, ponto fora do terreno, divisão por zero, tilt 0, terreno plano, terreno vertical?
- [ ] Transação aberta dentro de laço? Objeto do banco usado fora da transação?
- [ ] Exceção engolida sem log?
- [ ] Código morto, duplicado ou fora do escopo do passo?
- [ ] Nome de coisa em layer usado como identidade?

Corrija o que ele apontar, rode tudo de novo.

## 6. Commit
Um commit por passo: `etapa X.Y: <descrição>`.

## 7. Relatório e PARADA
Atualize `PROGRESSO.md`: status `AGUARDANDO VALIDAÇÃO`. Escreva para o Renan, curto:
- o que foi feito;
- placar dos testes;
- o que o revisor apontou e o que foi corrigido;
- **o que ele precisa conferir** (o item "Validação do Renan" do passo), com instrução de como.

E PARE.

## 8. Depois da validação
Se o Renan aprovar e o passo gerou resultado de referência, os arquivos esperados entram no acervo **por ele** (ver `04-testes.md`). Se reprovar, status `REPROVADO` com o motivo, e o passo recomeça na fase 1.
