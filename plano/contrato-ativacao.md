# Contrato: ativação do Clivus Solar

Decisão do Renan (04/10/2026): a pessoa se registra na landing page, baixa
o instalador, gera um código de ativação, instala o plugin e usa. Por ora é
gratuito, mas com licença de verdade (código, máquina, validade): cobrar
depois é só política do servidor. Duas máquinas por código.

O servidor (outro agente) cuida de conta, código, contagem de máquinas e da
assinatura da licença. O plugin (este repositório) pede o código, guarda a
licença e confere a assinatura. Mudança neste contrato se combina no
`CANAL.md`.

## A licença

Um texto `{payload}.{assinatura}`, as duas partes em base64url sem `=`.

- `payload`: JSON em UTF-8, exatamente os bytes assinados:

```json
{
  "v": 1,
  "kid": "2026a",
  "licenca": "lic_8f2k1",
  "conta": "fulano@empresa.com.br",
  "plano": "gratuito",
  "maquina": "3f1c…(64 hex)",
  "emitida_em": "2026-10-04T12:00:00Z",
  "revalidar_em": "2026-11-03T12:00:00Z",
  "expira_em": "2026-11-18T12:00:00Z"
}
```

- `assinatura`: ECDSA P-256 com SHA-256 sobre os bytes do payload, no
  formato IEEE P1363 (r‖s, 64 bytes). Em Python (`cryptography`):
  `der = chave.sign(payload, ec.ECDSA(hashes.SHA256()))`, depois
  `r, s = decode_dss_signature(der)` e `r.to_bytes(32) + s.to_bytes(32)`.
- `kid`: qual chave assinou (troca de chave sem derrubar as licenças da
  anterior). O plugin guarda as chaves públicas por `kid` e escolhe por ele;
  `kid` que o plugin não conhece não vale.
- A chave privada fica só no servidor (variável de ambiente no Coolify,
  nunca no git). A chave **pública** (SubjectPublicKeyInfo, DER em base64)
  vai para o plugin, com o `kid`, em `PluginInfo.ChavesPublicasDaLicenca`: o agente do
  servidor publica no canal, e o do plugin grava. Enquanto ela estiver vazia,
  o plugin roda sem pedir licença (é o modo dos testes e de hoje).
- `maquina`: o plugin manda; é o SHA-256 (hex minúsculo) do MachineGuid do
  Windows com o prefixo `clivus:`. O servidor só copia.
- Datas em ISO 8601 UTC. Sugestão de prazos: `revalidar_em` = emissão + 30
  dias; `expira_em` = emissão + 45 dias (15 dias de folga sem internet).

O plugin aceita a licença se: a assinatura confere, `v` é 1, `maquina` é a
desta máquina e agora ≤ `expira_em`. Passado `revalidar_em`, ele tenta
revalidar em silêncio (sem internet, segue até `expira_em`, avisando).

## Ativar

`POST {CLIVUS_LICENCAS}/api/v1/licencas/ativar` (o mesmo servidor do 3D;
`CLIVUS_LICENCAS` cai em `CLIVUS_SERVIDOR` se não estiver definido)

```json
{ "codigo": "CLV-7K3P-9QX2-M4TD", "maquina": "3f1c…", "nome_maquina": "PC-RENAN", "plugin": "0.1.0" }
```

- `200 {"licenca": "…"}`: ativado (ou a mesma máquina de novo: devolve
  licença nova, sem gastar vaga).
- `404 {"erro": "código não encontrado"}`, `403 {"erro": "código
  revogado"}`, `409 {"erro": "este código já está ativo em 2 máquinas; libere
  uma na sua conta"}`. O plugin mostra o `erro` como veio.

## Revalidar

`POST {CLIVUS_LICENCAS}/api/v1/licencas/revalidar`

```json
{ "licenca": "…", "maquina": "3f1c…" }
```

- `200 {"licenca": "…"}` com prazos novos; `403 {"erro": "…"}` se o
  código foi revogado ou a máquina liberada (o plugin apaga a licença e pede
  o código de novo).

## Liberar uma máquina

Pela conta, no site (o plugin não chama). Libera a vaga; a licença daquela
máquina deixa de revalidar.

## O código

Formato livre para o servidor; o plugin só aceita letras, números e hífen e
manda em maiúsculas. Sugestão legível: `CLV-XXXX-XXXX-XXXX` sem letras
ambíguas (0/O, 1/I).

## Idioma

A mensagem de `{"erro": ...}` de `/api/v1/licencas/ativar` e `/revalidar`
segue o `Accept-Language` do pedido: `pt*` → português, `es*` → espanhol,
outro → inglês. Sem o cabeçalho, português, com o mesmo texto de antes (o
plugin antigo não muda nada). Status, campos e o formato da licença não mudam
com o idioma. O plugin manda `Accept-Language: pt-BR`, `en` ou `es`.

## Etapa 43: a licença no servidor e a versão publicada (31/10/2026)

Combinado entre o plugin e o servidor para o primeiro cliente
(`plano/clientes/LEIA-PRIMEIRO.md`). O mesmo texto está em
`plano/contrato-ativacao.md` e em `plano/contrato-servidor-3d.md`; mudança
aqui se avisa antes.

### 1. O plugin do cliente se identifica pela licença

- Em `POST {servidor}/api/v1/cenas` e em `POST {servidor}/api/v1/modulos`
  (as rotas que levavam só `Authorization: Bearer <CLIVUS_SERVIDOR_CHAVE>`),
  o plugin manda `Authorization: Licenca <a licença>`. A licença é o texto
  `{payload}.{assinatura}` exatamente como está no `licenca.txt`
  (`%LOCALAPPDATA%\Clivus Solar\licenca.txt`), sem espaço nem quebra de
  linha.
- Vale quando não há `CLIVUS_SERVIDOR_CHAVE` e há licença válida: o plugin
  confere a assinatura, a máquina e o prazo (`Valid`, ou `Revalidate`, que
  ainda vale). Com a chave definida (o PC do Renan, a conta Develop),
  continua o `Bearer`. Sem nenhuma das duas, o plugin não manda
  `Authorization`.
- Quem decide se a licença vale é o servidor (a assinatura pela chave do
  `kid` e o que mais a conta pedir); o que chega com ela vai para a conta
  dela, como o da chave vai para a conta Develop.
- Os erros chegam como hoje, `{"erro": "..."}`: 401 ou 403 com a mensagem do
  servidor, que o plugin mostra como veio. Sem o `erro` (um proxy no
  caminho), o plugin diz o que conferir conforme o que mandou: a chave, ou
  ativar de novo.
- Como a chave, a licença só vai por HTTPS (http só em localhost, para teste).
- As rotas públicas (`GET /api/v1/modulos`, `GET /api/v1/versao`) não levam
  `Authorization`.

### 2. A versão publicada

`GET {servidor}/api/v1/versao`, público. O plugin manda `Accept-Language`.

```json
{
  "versao": "0.1.1",
  "url": "https://.../baixar/ClivusSolar-Setup-0.1.1.exe",
  "sha256": "…(64 hex)",
  "tamanho": 6500000,
  "notas": "…",
  "publicada_em": "2026-11-01T12:00:00Z",
  "obrigatoria": false
}
```

ou `404` sem versão publicada (o plugin entende "nada a fazer", não erro).
Outro status: o plugin registra o `erro` e não oferece nada.

O que o plugin exige (fora disso, ignora a versão e registra o erro):

- `versao`: semver `MAIOR.MENOR.CORRECAO` (pré-versão como `-beta.1` vale).
  O plugin oferece só se ela for maior que a instalada.
- `url`: absoluta e HTTPS, no mesmo host (e porta) do servidor
  (`CLIVUS_SERVIDOR` ou o de produção) ou de um dos endereços de download
  (`PluginInfo.EnderecosDeDownload`): o site de download, hoje
  `https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io` (o `$site` do
  `tools/gerar-instalador.ps1`), e o domínio novo do servidor,
  `https://clivus.marcheng.com.br`. Redirecionamento: até 3, cada endereço
  novo pela mesma regra. Outro host, o plugin recusa: um servidor
  comprometido não manda rodar um executável de qualquer lugar. Um site de
  download novo pede uma versão do plugin que saia antes (as instaladas só
  baixam dos hosts que conhecem), e só entra domínio que é do Renan: hoje
  `clivussolar.com` não existe, e uma URL nele é recusada.
- `sha256`: 64 dígitos hexadecimais do `.exe` (maiúsculas ou minúsculas);
  `tamanho`: bytes, número inteiro, de 1 a 200 MB. O plugin baixa, confere
  os dois e, se diferem, apaga o arquivo e avisa.
- `notas` (texto, opcional; a janela mostra até 8000 caracteres),
  `publicada_em` (ISO 8601 UTC, opcional) e `obrigatoria` (opcional, padrão
  `false`). Com `true`, a janela diz que a atualização é obrigatória e o
  aviso volta a cada abertura do Civil 3D, em vez de uma vez por dia; o
  plugin não trava nada.

Quando o plugin consulta: ao abrir o Civil 3D com interface, no máximo uma
vez por dia (em segundo plano, com 5 s de tempo-limite; a última consulta
fica nas preferências do usuário), e no comando `CLIVUS_ATUALIZAR`. O
instalador baixado roda com `/aguardar-civil3d`: espera o Civil 3D fechar
(até 12 horas), instala como no silencioso e avisa numa janela do Windows.
