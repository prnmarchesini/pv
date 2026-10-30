# Contrato: plugin Clivus Solar → servidor 3D

Proposta de 04/10/2026 (Renan): os PCs com o Clivus Solar mandam a usina para
um servidor, e o servidor monta o 3D para abrir no navegador. No começo é só
um gerador (sem login); o login vem depois. Dois agentes trabalham juntos: o
deste repositório cuida do plugin e do envio; o outro, num repositório
separado, cuida do servidor (Coolify, na VPS).

Este arquivo é o contrato entre os dois. Mudança aqui é combinada antes.

## O que o plugin manda

`POST {CLIVUS_SERVIDOR}/api/v1/cenas`

- `Content-Type: application/json`, corpo em UTF-8. Pode vir com
  `Content-Encoding: gzip` (o terreno de uma área grande passa de alguns MB).
- `Authorization: Bearer {chave}`: no começo uma chave só, combinada e lida
  de variável de ambiente nos dois lados (`CLIVUS_SERVIDOR_CHAVE`). Depois,
  o token do login.
- Tamanho máximo aceito: 50 MB descomprimido (recusar com 413 acima disso).

Corpo:

```json
{
  "versao": 1,
  "plugin": "0.1.0",
  "desenho": "Curvas Itatiba",
  "cena": { ...a cena, abaixo... }
}
```

A cena é exatamente o JSON que o plugin já gera para a página 3D local
(`Viewer3DPage.Json`, em `src/Clivus.Core/Viewer3DPage.cs`). Coordenadas em
metro, relativas a `origem` (para caber em float de 32 bits no navegador);
X a leste, Y ao norte, Z para cima (cota). Desde 04/10/2026 o plugin manda
`origem` como `[0, 0, 0]`: a coordenada real da usina não sai do computador
(plano/seguranca.md; o visualizador do servidor não usa o campo, combinado no
CANAL.md). A página local continua com a origem verdadeira.

```json
{
  "titulo": "Curvas Itatiba",
  "origem": [0, 0, 0],
  "terreno": {
    "x0": -50.0, "y0": -25.0, "passo": 2.0,
    "colunas": 51, "linhas": 26,
    "z": [0.0, 0.1, null, ...]
  },
  "faces":   [[x1,y1,z1, x2,y2,z2, x3,y3,z3, x4,y4,z4, corRGB], ...],
  "pilares": [[x, y, zTopo, zPe], ...],
  "arvores": [[x, y, zChao, alturaTronco, larguraTronco, alturaCopa, larguraCopa], ...],
  "sombras": [[x1,y1,z1, x2,y2,z2, ...], ...]
}
```

- `terreno.z`: `colunas × linhas` cotas, linha a linha (y), coluna a coluna
  (x); `null` onde não há terreno. O ponto (i, j) fica em
  `(x0 + i·passo, y0 + j·passo)`.
- `faces`: uma face de módulo por item, quatro cantos em ordem (borda baixa
  primeiro) e a cor como inteiro `0xRRGGBB` (a cor do tipo de mesa ou da
  marca de sombra, como está no desenho).
- `pilares`: o traço visível, do pé (no terreno) ao topo.
- `arvores`: o pé e as medidas; tronco e copa são cilindros (a copa começa
  onde o tronco acaba).
- `sombras`: contornos fechados das sombras desenhadas, já no terreno.
- `canaletas` (opcional, desde 28/10/2026): triângulos prontos
  `[x1,y1,z1, x2,y2,z2, x3,y3,z3, corRGB]` — as canaletas da drenagem e o
  carport (aço, bases e módulos com espessura). O servidor guarda como vem; o
  visualizador desenha na camada das mesas.
- Listas vazias são válidas; `terreno` pode faltar.

## O que o servidor responde

`201 Created`

```json
{
  "id": "k3f9x2",
  "url": "https://{dominio}/3d/k3f9x2",
  "expira_em": "2026-11-03T12:00:00Z"
}
```

Erros: `400` (JSON fora do contrato, com `{"erro": "..."}` em português),
`401` (chave errada), `413` (grande demais). O plugin mostra a mensagem de
`erro` ao usuário como veio.

## O que o servidor serve

- `GET /3d/{id}`: a página 3D. O visualizador de referência é o do plugin
  (o modelo HTML em `Viewer3DPage.cs`, com three.js r128): o servidor pode
  usar o mesmo, carregando a cena de `GET /api/v1/cenas/{id}` em vez de ela
  vir embutida.
- `GET /api/v1/cenas/{id}`: a cena como foi enviada.
- `GET /api/v1/saude`: `200 {"ok": true}` (o plugin testa antes de enviar).

## Do lado do plugin (este repositório)

- `CLIVUS_3D` ganha a opção "Publicar": monta a mesma cena, manda para
  `CLIVUS_SERVIDOR` e abre a `url` devolvida. Sem servidor no ar, continua
  gravando o HTML local, como hoje.
- `CLIVUS_SERVIDOR` e `CLIVUS_SERVIDOR_CHAVE` vêm de variável de ambiente
  (como `CLIVUS_SERVICO`, o serviço de módulos).

## A biblioteca de módulos (38.6 e 38.8, 30/10/2026)

O servidor também guarda a biblioteca de módulos da janela de Mesa. Ela já
existia no serviço local (`servidor/`, `tools\servico-local.ps1`, rotas sem o
`/api/v1`), que continua valendo quando `CLIVUS_SERVICO` aponta para ele.

- `GET /api/v1/modulos`: a lista pública, só com os módulos que a gestão
  aprovou. Cada módulo vem no formato de `tests/dados/servico-modulos-com-pan.json`:
  `id`, `marca`, `modelo`, `potencia_w`, `altura_m`, `largura_m`,
  `espessura_m`, `eletrica` (ou null), `tem_pan` e `pan_nome`. É a cópia que o
  gate `validate_modulos` do servidor confere.
- `GET /api/v1/modulos/{id}` e `GET /api/v1/modulos/{id}/pan` (o .PAN como foi
  enviado): só os aprovados.
- `POST /api/v1/modulos` com `Authorization: Bearer {CLIVUS_SERVIDOR_CHAVE}`,
  o corpo de `ModuleLibrary.ToServiceJson` (até 1,2 MB, com o .PAN):
  - `201`: o módulo da lista mais `"situacao": "pendente"` (exemplo em
    `tests/dados/servidor-modulo-pendente.json`). Ele só entra na lista
    pública depois que uma pessoa da gestão confere o .PAN e aprova em
    `/gestao/modulos`. Até lá, o plugin o deixa só na janela que o mandou.
  - Erros com `{"erro": "..."}` no idioma do `Accept-Language`: `401` (chave),
    `409` (o modelo já existe, pendente ou aprovado, sem diferença de
    maiúscula), `413` (grande demais), `422` (medida ou parte elétrica fora
    dos limites do plugin, ou com NaN ou infinito), `400` (JSON ou UTF-8).

## Identidade visual

Cores do Clivus: azul-petróleo `#0F2533`, âmbar `#F4A51C`, branco. A página
do servidor usa as mesmas da página local.

## Idioma

A mensagem de `{"erro": ...}` de `POST /api/v1/cenas` e `GET /api/v1/cenas/{id}`
segue o `Accept-Language` do pedido: `pt*` → português, `es*` → espanhol,
outro → inglês. Sem o cabeçalho, português, com o mesmo texto de antes (o
plugin antigo não muda nada). Status, rotas e campos não mudam com o idioma.
O plugin manda `Accept-Language: pt-BR`, `en` ou `es` conforme o idioma dele.
