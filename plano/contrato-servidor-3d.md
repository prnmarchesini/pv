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
  o token do login. Desde a etapa 43, sem a chave, `Authorization: Licenca
  {a licença}` (seção "Etapa 43", no fim).
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
  "local": { "latitude": -23.0, "longitude": -46.8, "fuso": -3 },
  "terreno": {
    "x0": -50.0, "y0": -25.0, "passo": 2.0,
    "colunas": 51, "linhas": 26,
    "z": [0.0, 0.1, null, ...]
  },
  "faces":   [[x1,y1,z1, x2,y2,z2, x3,y3,z3, x4,y4,z4, corRGB], ...],
  "pilares": [[x, y, zTopo, zPe, largura, profundidade, giro], [x, y, zTopo, zPe], ...],
  "arvores": [[x, y, zChao, alturaTronco, larguraTronco, alturaCopa, larguraCopa], ...],
  "sombras": [[x1,y1,z1, x2,y2,z2, ...], ...],
  "canaletas": [[x1,y1,z1, x2,y2,z2, x3,y3,z3, corRGB], ...],
  "resumo": {
    "total": { "mesas": 618, "modulos": 17304, "kwp": 9515.2 },
    "areas": [{ "nome": "Área 1", "mesas": 600, "modulos": 16800, "kwp": 9240 },
              { "nome": "Fora das áreas", "fora": true, "mesas": 18, "modulos": 504, "kwp": 275.2 }],
    "tipos": [{ "nome": "GC800", "mesas": 618, "modulos": 17304, "kwp": 9515.2 }]
  }
}
```

- `terreno.z`: `colunas × linhas` cotas, linha a linha (y), coluna a coluna
  (x); `null` onde não há terreno. O ponto (i, j) fica em
  `(x0 + i·passo, y0 + j·passo)`.
- `faces`: uma face de módulo por item, quatro cantos em ordem (borda baixa
  primeiro) e a cor como inteiro `0xRRGGBB` (a cor do tipo de mesa ou da
  marca de sombra, como está no desenho).
- `pilares`: o pilar visível, do pé (no terreno) ao topo. Desde 31/10/2026
  (passo 42.2, "os pilares também sejam modelados") ele pode trazer a seção:
  `largura` (ao longo da mesa) e `profundidade` em metros, acima de 0 e até
  10, e o `giro` em radianos, de X para Y — a escala X e Y e a rotação do
  bloco do pilar. São os três ou nenhum; sem eles, a seção é 0,10 × 0,10 e
  o giro 0. O visualizador desenha uma caixa por pilar, que faz e recebe
  sombra.
- `arvores`: o pé e as medidas; tronco e copa são cilindros (a copa começa
  onde o tronco acaba).
- `sombras`: contornos fechados das sombras desenhadas, já no terreno.
- `canaletas` (opcional, desde 28/10/2026): triângulos prontos
  `[x1,y1,z1, x2,y2,z2, x3,y3,z3, corRGB]` — as canaletas da drenagem e o
  carport (aço, bases e módulos com espessura). O servidor guarda como vem; o
  visualizador desenha na camada das mesas.
- `local` (opcional, desde 31/10/2026, passo 42.2): onde a usina está, para o
  navegador calcular o sol (o `SolarCalculator` do plugin portado para
  JavaScript, `sol.js`) e fazer a sombra em tempo real: `latitude` (−90 a
  90, positiva ao norte) e `longitude` (−180 a 180, positiva a leste) em
  graus, e `fuso` em horas (−14 a 14; −3 em Brasília), o mesmo das sombras
  do desenho. Sem ele (desenho sem localização), o visualizador fica sem os
  controles do sol e com a luz de sempre. No envio ao servidor a latitude e
  a longitude vão arredondadas a 0,1° (uns 11 km): o sol muda menos de
  0,05° e o lugar exato da usina não sai do computador, como a `origem`; a
  página local leva o lugar com o centímetro.
- `resumo` (opcional, desde 31/10/2026, passo 42.2): o quadro "Resumo" das
  camadas, pela conta da janela Áreas e do Recontar. `total` com `mesas`,
  `modulos` e `kwp` (potência DC); `areas` (só quando o desenho tem área,
  com a linha das mesas fora delas marcada `"fora": true`) e `tipos` (por
  tipo de mesa), cada linha com o `nome` e os mesmos três números. Números
  sem sinal; até 1000 linhas por lista. Sem ele, o item "Resumo" não
  aparece.
- Listas vazias são válidas; `terreno` pode faltar. O servidor recusa com
  `400` o `local`, a seção do pilar ou o `resumo` fora do formato acima (NaN
  e infinito também); `null` vale como ausente.

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
  vir embutida. O sol e o resumo (passo 42.2) são dois arquivos do plugin,
  `src/Clivus.Core/Visualizador3D/sol.js` e `resumo.js`, que o servidor serve
  iguais em `/estatico/`: mudou um, copia para o outro. A página pública
  leva o logo do Clivus e a propaganda (o botão "Conheça o Clivus Solar"
  leva à landing, `CLIVUS_SITE_URL`); a local, só o logo.
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
- `POST /api/v1/modulos` com `Authorization: Bearer {CLIVUS_SERVIDOR_CHAVE}`
  (ou, sem a chave, `Licenca {a licença}`, etapa 43), o corpo de
  `ModuleLibrary.ToServiceJson` (até 1,2 MB, com o .PAN):
  - `201`: o módulo da lista mais `"situacao": "pendente"` (exemplo em
    `tests/dados/servidor-modulo-pendente.json`). Ele só entra na lista
    pública depois que uma pessoa da gestão confere o .PAN e aprova em
    `/gestao/modulos`. Até lá, o plugin o deixa só na janela que o mandou.
  - Erros com `{"erro": "..."}` no idioma do `Accept-Language`: `401` (chave),
    `409` (o modelo já existe, pendente ou aprovado, sem diferença de
    maiúscula), `413` (grande demais), `422` (medida ou parte elétrica fora
    dos limites do plugin, ou com NaN ou infinito), `400` (JSON ou UTF-8),
    `503` (o servidor está sem a chave do plugin configurada).
  - O plugin trata o `409` do servidor do Clivus como "já enviado" (o modelo
    espera a gestão, talvez mandado em outra sessão, ou já foi aprovado): o
    módulo fica na lista das janelas de Mesa da sessão, marcado "esperando a
    validação", até a lista pública trazê-lo. O .PAN acima de 500 mil
    caracteres é recusado antes de subir.

## Identidade visual

Cores do Clivus: azul-petróleo `#0F2533`, âmbar `#F4A51C`, branco. A página
do servidor usa as mesmas da página local.

## Idioma

A mensagem de `{"erro": ...}` de `POST /api/v1/cenas` e `GET /api/v1/cenas/{id}`
segue o `Accept-Language` do pedido: `pt*` → português, `es*` → espanhol,
outro → inglês. Sem o cabeçalho, português, com o mesmo texto de antes (o
plugin antigo não muda nada). Status, rotas e campos não mudam com o idioma.
O plugin manda `Accept-Language: pt-BR`, `en` ou `es` conforme o idioma dele.

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
  (`CLIVUS_SERVIDOR` ou o de produção) ou do site de download
  (`PluginInfo.SiteDeDownload`, hoje
  `https://noxsbamrcobkfhfkcl3iva7x.177.153.20.214.sslip.io`, o `$site` do
  `tools/gerar-instalador.ps1`). Redirecionamento: até 3, cada endereço novo
  pela mesma regra. Outro host, o plugin recusa: um servidor comprometido
  não manda rodar um executável de qualquer lugar. Trocar o site de download
  pede uma versão do plugin que saia antes (as instaladas só baixam do host
  que conhecem ou do próprio servidor).
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
