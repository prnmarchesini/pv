# Clivus Solar Civil 3D: instruções para o Claude Code

Você está construindo o **Clivus Solar**, um plugin de AutoCAD Civil 3D que substitui o PVcase no layout de usinas fotovoltaicas em terreno inclinado. O nome é de 04/10/2026; antes era "Plugin UFV", com comandos `UFV_*` e o prefixo `MARCHENG_UFV` em camadas, blocos e XData. Hoje é `CLIVUS_*` e `CLIVUS`, e desenho antigo é migrado ao abrir (`MigracaoDoNome`). O plano de requisitos completo está em:
https://claude.ai/code/artifact/3668dee4-233d-4a4c-b596-262f1f089c09

O plano de EXECUÇÃO está em `plano/`. Leia nesta ordem antes de qualquer código:

1. `plano/01-regras-sagradas.md` (invioláveis, valem para todo passo)
2. `plano/02-arquitetura.md` (estrutura da solução, o que pode referenciar o quê)
3. `plano/03-protocolo-de-passo.md` (o ritual obrigatório de cada passo)
4. `plano/04-testes.md` (níveis de teste e o acervo imutável)
5. `plano/PROGRESSO.md` (onde você está)
6. O arquivo da etapa atual em `plano/etapas/`

## Regra de ouro

**Um passo por vez. Ao terminar um passo, PARE e espere o Renan validar.** Não comece o passo seguinte, não "adiante" código, não faça nada fora do escopo do passo. Se achar algo que precisa ser feito fora do escopo, anote em `plano/PROGRESSO.md` na seção "Observações" e siga.

## Como começar uma sessão

1. Abra `plano/PROGRESSO.md` e ache o primeiro passo que não está `VALIDADO`.
2. Se ele estiver `AGUARDANDO VALIDAÇÃO`, não faça nada: pergunte ao Renan se ele validou.
3. Se estiver `PENDENTE` ou `REPROVADO`, execute o protocolo do passo.

## Idioma

Código, nomes de classe e métodos em inglês. Comentários, mensagens ao usuário, commits e relatórios em português do Brasil.

## Nunca

- Alterar um arquivo que já está em `tests/acervo/`. Acrescentar arquivo novo e declarar o hash no manifesto pode, desde 25/09/2026 (ver `plano/04-testes.md`).
- Apagar ou afrouxar um teste para ele passar.
- Referenciar DLL do AutoCAD/Civil 3D em `Clivus.Core` ou `Clivus.Geo`.
- Renomear, renumerar ou mover entidades do usuário sem comando explícito dele.
- Marcar como `VALIDADO` um passo que tem comando na tela. Só o Renan faz isso. Passo que é só modelo, arquivo ou número (sem nada para ver no Civil 3D) o Claude Code fecha sozinho com os testes automáticos, marca `VALIDADO (automático)` e registra no PROGRESSO.md o que assumiu (decisão do Renan em 25/09/2026: "vou validar somente coisas no cad").
