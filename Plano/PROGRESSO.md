# Progresso

Status: PENDENTE | EM ANDAMENTO | AGUARDANDO VALIDAÇÃO | VALIDADO | REPROVADO
Só o Renan marca VALIDADO.

| Passo | Descrição | Status | Observação |
|---|---|---|---|
| 0.1 | Solução e projetos vazios | AGUARDANDO VALIDAÇÃO | Alvo mudou de Civil 3D 2025 para 2026 |
| 0.2 | Teste de arquitetura e placar | AGUARDANDO VALIDAÇÃO | |
| 0.3 | Comando hello world | AGUARDANDO VALIDAÇÃO | |
| 0.4 | Carregamento automático (bundle) | AGUARDANDO VALIDAÇÃO | Falta abrir o Civil 3D e ver a aba |
| 0.5 | Core Console fumaça | AGUARDANDO VALIDAÇÃO | |
| 0.6 | Instalador com detecção de versão | AGUARDANDO VALIDAÇÃO | Falta instalar numa máquina limpa |

(As linhas das etapas seguintes são acrescentadas ao iniciar cada etapa, copiando os passos do arquivo dela.)

## Placar na entrega da etapa 0

```
Etapa 0   45/45    OK
Nivel 2   1/1      OK
Acervo             OK
```

## Observações

**Civil 3D 2026, não 2025.** A máquina tem Civil 3D 2026 (série R25.1, `acmgd.dll`
25.1.74.0.0). `02-arquitetura.md` e `etapa-0-fundacao.md` foram corrigidos. O
framework não mudou: 2025 e 2026 são ambos .NET 8.

**O .NET SDK não estava instalado**, só o runtime. Instalado o 8.0.425.

**SECURELOAD no teste de nível 2.** O AutoCAD só carrega código de caminho
confiável, e a pasta `bin` do build não é uma. Sem interface não há como
autorizar, então o teste baixa a guarda pelo tempo do NETLOAD e devolve depois.
Como o valor fica salvo no perfil do usuário, quem garante a devolução é o
runner, em `finally`, inclusive apagando a entrada nos perfis onde ela não
existia antes. Em produção nada disso acontece: o bundle fica em
`ApplicationPlugins`, que o AutoCAD já trata como caminho confiável.

**O acervo ainda não tem desenho.** O teste de nível 2 cai num template do
Civil 3D e diz qual usou. Quando a etapa 1 congelar um `.dwg` de referência, ele
passa a mandar — e a entrada do teste muda.

**A pasta do plano ficou `Plano` com P maiúsculo.** A intenção era `plano`; um
processo segurava o diretório na hora da reorganização. Um `git mv` resolve
quando estiver livre.

**Fora do escopo, para as etapas seguintes:**

- O instalador copia para `ApplicationPlugins` do usuário atual. Instalação para
  todos os usuários da máquina não foi feita.
- Os códigos de saída 1, 4 e 5 do instalador não têm teste automatizado: exigem
  máquina sem AutoCAD ou com o CAD aberto.
- O botão da ribbon não tem ícone (`ShowImage = false`).
