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
