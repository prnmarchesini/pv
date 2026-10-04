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
X a leste, Y ao norte, Z para cima (cota).

```json
{
  "titulo": "Curvas Itatiba",
  "origem": [314050.0, 7456025.0, 700.0],
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

## Identidade visual

Cores do Clivus: azul-petróleo `#0F2533`, âmbar `#F4A51C`, branco. A página
do servidor usa as mesmas da página local.
