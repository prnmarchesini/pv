# Develop: ligar o plugin ao servidor e publicar o 3D

Para o agente do plugin (e para o programador que está chegando). É a
sequência para, em desenvolvimento, ativar a licença e publicar uma usina real
no servidor. Escrito pelo agente do servidor em 04/10/2026. Contexto completo:
`plano/licenca-como-funciona.md` e as mensagens do servidor no `CANAL.md`.

## O que já está pronto do lado do servidor

- **App** (API, 3D e área do cliente):
  `https://h5rxptnbwh7bvaufisadgzos.177.153.20.214.sslip.io`. Já está embutido
  no plugin (`PluginInfo.ServidorDeProducao`), então não precisa de
  `CLIVUS_SERVIDOR`.
- **Conta Develop** no app: `develop@clivussolar.com`, plano `develop`. Tudo o
  que o plugin publica cai nos projetos dela, e as licenças dela saem com
  `"plano": "develop"`.
- **Código de ativação develop**: já gerado na conta Develop.
- **Licença ligada no plugin**: chave pública `2026a` em
  `PluginInfo.ChavesPublicasDaLicenca`, a partir do commit `c421925`.

## Os dois segredos (NÃO estão neste repositório, que é público)

O Renan define no PC, uma vez:

| variável de ambiente | para quê |
|---|---|
| `CLIVUS_SERVIDOR_CHAVE` | a chave do `Bearer` no `POST /api/v1/cenas` (publicar) |
| `CLIVUS_DEV_CODIGO` | o código de ativação develop, para colar no `CLIVUS_ATIVAR` |

Os valores ficam com o Renan, e ele roda `setx` no PowerShell. Variável criada
com `setx` só vale em programa aberto **depois**: feche e abra o Civil 3D (e o
terminal) depois do `setx`. Para conferir: `echo $env:CLIVUS_SERVIDOR_CHAVE`.

## Sequência

1. `git pull` e uma build com a licença ligada (`c421925` em diante).
2. Abra o Civil 3D (depois do `setx`).
3. **Ativar:** rode `CLIVUS_ATIVAR` e cole o valor de `CLIVUS_DEV_CODIGO`. No
   nível 2, use `CLIVUS_ATIVAR_AUTO` com o código. A mensagem esperada é
   "Clivus Solar ativado para develop@clivussolar.com (plano develop), até
   dd/mm/aaaa". A licença fica em `%LOCALAPPDATA%\Clivus Solar\licenca.txt`.
4. **Publicar:** abra um desenho com usina processada (terreno, mesas,
   pilares) e rode `CLIVUS_3D` → Publicar (ou `CLIVUS_3D_PUBLICAR_AUTO`). O
   plugin manda `POST /api/v1/cenas` com gzip, a chave e `origem: [0,0,0]`, e
   recebe `201 {id, url, expira_em}`.
5. Abra a `url` (`.../3d/<id>`): o 3D da usina, também no celular.
6. **Escreva no `CANAL.md`:** o id, o nome do desenho, quantos módulos e
   pilares, o tamanho, e o que viu (ou o erro exato que o plugin mostrou). O
   agente do servidor confere o mesmo id do lado dele e responde.

## Se der erro

| o plugin diz | causa provável |
|---|---|
| "a chave do servidor não foi aceita" (401) | `CLIVUS_SERVIDOR_CHAVE` errada ou não carregada (o Civil 3D foi aberto antes do `setx`) |
| "código não encontrado" (404) | código digitado errado; o servidor já normaliza maiúsculas e espaços |
| "este código já está ativo em 2 máquinas" (409) | libere uma máquina na área do cliente (conta Develop) |
| "corpo fora do contrato: ..." (400) | a cena não bate com `plano/contrato-servidor-3d.md`; o texto diz o campo |
| "a usina é grande demais" (413) | mais de 50 MB descomprimido |
| comando barrado "sem licença válida" | falta o passo 3, ou a build não tem a chave `2026a` |

Na área do cliente (login da conta Develop com o Renan) dá para ver os
projetos publicados, o link de cada 3D e os computadores ativados.
