# Plugin UFV Civil 3D: instruções para o Claude Code

Você está construindo um plugin de AutoCAD Civil 3D que substitui o PVcase no layout de usinas fotovoltaicas em terreno inclinado. O plano de requisitos completo está em:
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

- Editar arquivos dentro de `tests/acervo/` (ver `plano/04-testes.md`).
- Apagar ou afrouxar um teste para ele passar.
- Referenciar DLL do AutoCAD/Civil 3D em `UFV.Core` ou `UFV.Geo`.
- Renomear, renumerar ou mover entidades do usuário sem comando explícito dele.
- Marcar um passo como `VALIDADO`. Só o Renan faz isso.
