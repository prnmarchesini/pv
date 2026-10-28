# Contrato TermoVision ↔ Clivus Solar (v1)

Pedido do Renan (26/10/2026): *"quero poder cotornar uma area, vc exporta a topografia, eu faço o desenho, e ai o
clivus solar exportar para o termovision (...) se eu tiver o clivus aberto em paralelo com o termovision, eu quero
selecionar a area e clicar "Exportar area para o clivus" ai abre direto no meu autocad. Ai eu desenho os modulos o
autocad faço tudo e ai, clico no clivus, exportar para o termovision"*.

Três lados: o TermoVision (o portal `https://app.termovision.tech`, ou o develop), o plugin Clivus no AutoCAD Civil 3D
do PC do Renan, e o Renan, que testa. Quem muda este contrato avisa o outro lado antes.

## O caminho

1. **No TermoVision**, Nova UFV: a pessoa escolhe uma ÁREA DE MESAS e clica **Exportar área para o Clivus**. O
   servidor cria um ENVIO (a usina, a área, quem pediu, o espaço) com um TOKEN `tvc_…` que vale 7 dias.
2. A página abre o endereço
   `clivus://termovision?v=1&servidor=<url>&envio=<id>&token=<tvc_…>` (cada valor com `encodeURIComponent`).
   O Windows entrega o endereço ao Clivus (o protocolo `clivus:` registrado no HKCU pelo plugin ou pelo instalador).
   Se nada abrir, a página oferece **Copiar o código** (o mesmo endereço inteiro, para colar no comando do Clivus) e
   **Baixar o pacote (.zip)**.
3. **No Clivus**: o plugin baixa o pacote, monta o desenho (a superfície TIN, o orto por baixo, o contorno da área, as
   árvores, a localização) e a pessoa desenha as mesas como sempre.
4. **Exportar para o TermoVision**, no Clivus: as mesas vão de volta pelo mesmo envio. No TermoVision elas TROCAM as
   mesas daquela área da Nova UFV (como um Regerar: as de antes ficam marcadas, e o Desfazer da Nova UFV as traz).
   Pode voltar quantas vezes quiser enquanto o envio vale; cada volta é uma operação nova.

## A autenticação

`Authorization: Bearer <tvc_…>` em todo pedido do plugin. O token é do ENVIO (uma área de uma usina): ele só baixa o
pacote daquele envio e só grava as mesas daquela área. O servidor guarda só o sha256 do token. A pessoa que exportou
tem de seguir com acesso à usina (bloqueada, sem acesso, a área desfeita: o envio para de valer).

| Resposta | Quando |
|----------|--------|
| 401 `{"detail": "..."}` | token que não existe, vencido ou que deixou de valer (o texto diz qual, em português) |
| 404 | a área do envio foi desfeita |
| 400 | o corpo do retorno com erro (o texto diz qual mesa e o quê) |
| 413 | retorno maior que 20 MB descomprimido |

## `GET {servidor}/api/clivus/v1/envio`

O resumo, sem baixar o pacote (o plugin mostra antes de montar):
`{"versao": 1, "envio": id, "usina": {"id", "nome"}, "area": {"id", "nome"}, "crs": {...}, "expira_em": ISO,
"criado_por": nome, "tamanho_estimado_mb": n}`.

## `GET {servidor}/api/clivus/v1/pacote` → `application/zip`

| Arquivo | O que é |
|---------|---------|
| `clivus.json` | o principal (abaixo) |
| `terreno.xml` | a mesma grade do terreno em LandXML 1.2 (`<Surface>` com `<Pnts>` e `<Faces>`): para importar à mão no Civil 3D se preciso |
| `orto.jpg` + `orto.jgw` | o ortomosaico RGB recortado, em UTM (o world file: tamanho do pixel e o centro do pixel de cima à esquerda); ausente se a usina não tem orto |

`clivus.json`:

```json
{
  "versao": 1,
  "gerado_em": "2026-10-26T15:00:00Z",
  "servidor": "https://app.termovision.tech",
  "envio": "cv_…",
  "usina": {"id": "…", "nome": "Hotel Paradise"},
  "area": {"id": "…", "nome": "Área 1"},
  "crs": {"epsg": 31983, "nome": "SIRGAS 2000 / UTM zone 23S", "zona": 23, "hemisferio": "S"},
  "geolocalizacao": {"lat": -22.9, "lon": -47.1},
  "area_utm": [[e, n], …],
  "terreno": {"fonte": "dtm" | "dem", "passo_m": 1.0, "e0": …, "n0": …, "colunas": c, "linhas": l,
              "z": [ … c × l números, null onde não há dado … ]},
  "orto": {"arquivo": "orto.jpg", "mundo": "orto.jgw", "e_min": …, "n_min": …, "e_max": …, "n_max": …,
           "px_m": 0.05} ,
  "objetos": [{"nome": "Orto 3", "altura_m": 9.0, "chao_m": 612.3, "contorno": [[e, n], …]}],
  "projeto": {"modulo": {"largura_m", "altura_m", "espaco_m"},
              "config": {"fiadas", "modulos_por_mesa", "pitch_m", "inclinacao_graus", "altura_livre_m",
                         "rumo_frente_graus", "vao_m"}},
  "retorno": {"url": "https://app.termovision.tech/api/clivus/v1/retorno"}
}
```

* Coordenadas ABSOLUTAS em UTM SIRGAS 2000 (metro), a zona da área; `z` em metro, a altitude do modelo de elevação.
* A grade: o nó (i, j) — coluna i, linha j — fica em `(e0 + i·passo, n0 + j·passo)`; `z[j·colunas + i]`. A linha 0 é a
  do SUL (n0), crescendo para o norte. Cobre a área com 20 m de folga. O passo é 1 m, maior só se a grade passar de
  1 milhão de nós.
* `terreno.fonte`: `dtm` é o chão sem vegetação (o do Solarorto); `dem` é o modelo de elevação (com o terreno editado
  no painel Terreno, se houver): então as árvores podem estar no terreno.
* `objetos`: as árvores e as casas que o modelo de elevação tem 1 m ou mais acima do chão, só o pedaço dentro da área,
  sem as mesas — o mesmo do .dae do PVsyst. Prisma: o contorno, do `chao_m` ao `chao_m + altura_m`.
* `projeto`: a configuração da Nova UFV, para o Clivus sugerir a mesa (pode ignorar).

## `POST {servidor}/api/clivus/v1/retorno`

`Content-Type: application/json; charset=utf-8` (pode ir com `Content-Encoding: gzip`) e `X-Pedido: <id único do
clique>` (o reenvio do MESMO clique recebe a mesma resposta, nada gravado de novo).

```json
{
  "versao": 1,
  "plugin": "1.4.0",
  "desenho": "Paradise.dwg",
  "mesas": [
    {"guid": "…", "nome": "M12",
     "cantos": [[e, n, z], [e, n, z], [e, n, z], [e, n, z]],
     "colunas": 13, "fiadas": 2,
     "modulo": {"largura_m": 1.134, "altura_m": 2.278, "espaco_m": 0.02}}
  ]
}
```

* `cantos`: a FACE de cima da mesa (o plano dos módulos), em UTM. Os cantos 1 e 2 na borda BAIXA (a frente, a ponta
  baixa), do começo para o fim da fileira; 3 e 4 na borda ALTA, do fim para o começo (o contorno em volta).
* `colunas`: módulos ao longo da fileira; `fiadas`: módulos na rampa (1V = 1, 2V = 2). O total é `colunas × fiadas`.
* `modulo`: o módulo da mesa (largura ao longo, altura na rampa, espaço entre módulos).

Resposta 200:
`{"gravado": true, "operacao": id, "mesas": n, "sairam": n, "fora_da_area": n, "avisos": [...], "url": "<o mapa>"}`.

No TermoVision cada mesa vira uma mesa da Nova UFV: o eixo e os cantos na planta, a inclinação e o rumo da frente
tirados dos cantos 3D, a ponta baixa ao solo pelo terreno do TermoVision, o módulo e a contagem como vieram. A mesa
cujo meio cai fora da área entra assim mesmo, na área do envio, e `fora_da_area` conta quantas.
