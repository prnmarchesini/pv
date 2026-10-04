# Licença do Clivus Solar: como funciona a validação

Para quem chega agora. O contrato (formatos e rotas, o que não pode mudar sem
combinar) está em `plano/contrato-ativacao.md`. Este arquivo explica o
funcionamento de ponta a ponta, onde fica cada peça e como operar (testar,
trocar chave, trocar domínio). Escrito em 04/10/2026 pelo agente do servidor.

## 1. O fluxo, do cliente ao comando liberado

```
 cliente            app (servidor)                     plugin (Civil 3D)
 ───────            ──────────────                     ─────────────────
 entra no app ───▶  área do cliente
 "Gerar código" ──▶ cria CLV-XXXX-XXXX-XXXX-XXXX
                    (mostra 1 vez, guarda só o hash)
 copia o código ─────────────────────────────────────▶ botão Ativar (CLIVUS_ATIVAR)
                                                       manda código + máquina
                    POST /api/v1/licencas/ativar  ◀────
                    confere código e vagas
                    assina a licença (ECDSA, kid) ────▶ confere a assinatura com a
                                                       chave pública embutida e
                                                       grava em %LOCALAPPDATA%\
                                                       Clivus Solar\licenca.txt
                                                       comandos CLIVUS_* liberados
 ... 30 dias ...
                    POST /api/v1/licencas/revalidar ◀── renova em segundo plano
                    licença nova (+30/+45 dias) ──────▶ grava a nova
```

- A **máquina** é o SHA-256 de `clivus:` + MachineGuid do Windows. A licença
  vale só nela.
- **Um código vale para 2 computadores.** O terceiro recebe 409. Na área do
  cliente, a pessoa libera um computador para abrir vaga.
- **Cada conta tem um código ativo por vez.** Para trocar, revoga o antigo e
  gera outro. Sem essa regra, o limite de 2 computadores não limitaria nada.
- Reativar no **mesmo** computador devolve licença nova sem gastar vaga
  (reinstalar o plugin não consome licença).

## 2. A licença

Um texto `{payload}.{assinatura}`, as duas partes em base64url sem `=`.

- `payload`: JSON com `v`, `kid`, `licenca`, `conta`, `plano`, `maquina`,
  `emitida_em`, `revalidar_em` (+30 dias) e `expira_em` (+45 dias).
- `assinatura`: ECDSA P-256 com SHA-256 sobre os bytes exatos do payload, no
  formato r‖s (64 bytes).
- A **chave privada** fica só no servidor (variável do Coolify). A **pública**
  fica embutida no plugin, em `PluginInfo.ChavesPublicasDaLicenca`, por `kid`.
  Hoje existe uma: `2026a`.

O plugin (`License.Check`, em `src/Clivus.Core/License.cs`) aceita a licença se:

1. a assinatura confere com a chave do `kid` dela (`kid` desconhecido não vale);
2. `v` é 1;
3. `maquina` é a deste computador;
4. agora ≤ `expira_em`.

O resultado é um destes estados:

| estado | quando | o que o usuário vê |
|---|---|---|
| `Valid` | dentro do prazo | nada; os comandos rodam |
| `Revalidate` | passou de `revalidar_em` | nada; os comandos rodam e o plugin renova em segundo plano |
| `Expired` | passou de `expira_em` (15 dias sem conseguir renovar) | comando barrado: precisa de internet ou de ativar de novo |
| `Invalid` | assinatura errada, outra máquina, formato errado | comando barrado: precisa ativar |

## 3. Onde fica cada peça

**Plugin (este repositório):**

| arquivo | papel |
|---|---|
| `src/Clivus.Core/License.cs` | conferência pura (assinatura, máquina, prazos), testável sem AutoCAD |
| `src/Clivus.Core/PluginInfo.cs` | `ChavesPublicasDaLicenca` (por `kid`), `ServidorDeProducao`, `PortalDoApp` |
| `src/Clivus.Plugin/Licenciamento.cs` | chama o servidor, grava a licença e **barra** os comandos `CLIVUS_*` sem licença (pelo evento de trava do documento) |
| `src/Clivus.Plugin/AtivarCommands.cs` | a janela Ativar e o link para o portal do app |
| `tests/Clivus.Core.Tests/LicenseTests.cs` | testes da conferência, inclusive `AChaveEmbutidaConfereALicencaDoServidor`, que usa uma licença emitida pelo servidor de produção |

Comandos que rodam **sem** licença: `CLIVUS_ATIVAR`, `CLIVUS_SOBRE`,
`CLIVUS_OLA` e `CLIVUS_MIGRAR`.

**Servidor (repositório privado `prnmarchesini/clivussolar`, pasta `app/`):**

| arquivo | papel |
|---|---|
| `app/services/licencas.py` | gera o código, ativa, revalida, libera, revoga e assina |
| `app/api/v1/licencas.py` | as rotas `/api/v1/licencas/ativar` e `/revalidar` |
| `app/web.py` | a área do cliente (gerar código, computadores, liberar, revogar) |
| `scripts/validate_licencas.py` | o gate: 27 verificações, inclusive licença adulterada |

## 4. Endereços

Os três são o mesmo app:

- `ServidorDeProducao` (3D e licenças): hoje
  `https://h5rxptnbwh7bvaufisadgzos.177.153.20.214.sslip.io`, o domínio
  temporário do Coolify com HTTPS válido.
- `PortalDoApp`: o mesmo endereço, onde o cliente gera o código.
- A variável `CLIVUS_SERVIDOR` vale por cima do embutido (para teste).
  `CLIVUS_LICENCAS` separa só o endereço das licenças.

O plugin só fala com HTTPS (http só em `localhost`) e só abre no navegador um
link do mesmo endereço do servidor.

## 5. Operação

### Desenvolvimento: a conta Develop

Enquanto o plugin está em desenvolvimento (DLLs trocadas direto, sem
instalador), tudo roda na **conta Develop** do app (`develop@clivussolar.com`):

- as licenças dela saem com `plano: "develop"`;
- o que o plugin publica com `CLIVUS_SERVIDOR_CHAVE` cai nos projetos dela;
- o código de ativação develop já foi gerado. O Renan tem o código e a senha da
  conta; nada disso vai para o git.

Ativar o PC de desenvolvimento:

1. Use uma build com a chave `2026a` (a partir do commit `c421925`). Os comandos
   do Clivus passam a pedir licença.
2. No Civil 3D, rode `CLIVUS_ATIVAR`, cole o código develop e confirme. A
   mensagem diz `develop@clivussolar.com`, plano `develop`, e até quando vale.
3. Na área do cliente (conta Develop), o computador aparece na lista. São 2
   vagas por código; a de uma máquina antiga se libera ali.

Cliente de verdade (depois do cadastro): faz o mesmo com o código que ele
mesmo gera na conta dele, e a licença sai com `plano: "gratuito"`.

### Trocar de chave (vazou, ou rotina)

1. O servidor gera um par novo com `kid` novo (por exemplo `2026b`) e publica
   a **pública** no `CANAL.md`.
2. O plugin **acrescenta** a chave nova em `ChavesPublicasDaLicenca`, sem tirar a
   velha, e sai uma versão nova do plugin.
3. Só então o servidor passa a assinar com a chave nova.
4. A chave velha sai do plugin quando não houver mais licença assinada com ela
   (45 dias). Se a chave vazou, sai na hora, e todos ativam de novo.

### Trocar de domínio (quando `app.clivussolar.com` existir)

1. O servidor passa a atender os dois endereços e avisa no canal.
2. O plugin troca `ServidorDeProducao`, o que também muda o `PortalDoApp`.
3. Versões antigas do plugin continuam funcionando pelo endereço temporário
   enquanto ele existir.

## 6. Como testar

- **Conferência (sem AutoCAD):** `dotnet test tests/Clivus.Core.Tests`
  (filtro `LicenseTests`). Em Linux, 15 testes de `InstaladorTests` e
  `ArchitectureTests` falham porque chamam `powershell.exe` e usam caminhos do
  Windows; no Windows eles passam.
- **Nível 2 (Civil 3D + servidor falso):** `tests/Clivus.Integration/servidor-falso.py`
  assina com uma chave de teste. No build Debug, `CLIVUS_LICENCA_CHAVE_TESTE`
  (`kid=base64`) troca a chave embutida pela de teste, e
  `CLIVUS_LICENCA_ARQUIVO_TESTE` troca o arquivo da licença. O build Release
  (o instalado) ignora as duas.
- **Servidor:** `scripts/validate_licencas.py` no repositório do servidor. As
  licenças do servidor também foram conferidas com este `License.cs`
  compilado num SDK .NET 8: `Valid` ao ativar, `Revalidate` aos 31 dias,
  `Expired` aos 46, e `Invalid` com outra máquina ou com `kid` desconhecido.

## 7. O que ainda não existe

- Limite de tentativas em `/ativar` (fase 2, antes de abrir cadastro). Com
  79 bits no código, adivinhar não é viável, mas o limite fica no plano.
- Planos pagos: hoje todo mundo é `gratuito`. Cobrar é política do servidor
  (prazo e plano no payload); o plugin já lê `plano`.
- Tela de licenças no `adm` (o portal do Renan).
