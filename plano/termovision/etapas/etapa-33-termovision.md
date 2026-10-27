# Etapa 33: ponte com o TermoVision

### 33.1 Endereço clivus:// (Core)

`TermoVisionLink.TryParse(texto, out link, out erro)`: aceita
`clivus://termovision?v=1&servidor=…&envio=cv_…&token=tvc_…` com os valores
codificados, espaços e aspas em volta (colado). Exige v=1, servidor `https://`
(ou `http://` só em `127.0.0.1`/`localhost`), envio começando com `cv_`, token
com `tvc_`. Erro em português dizendo o que falta. `ToString()` nunca mostra o
token. Testes com o endereço do contrato, codificado, faltando cada parte, v=2,
http remoto.
Fecha automático.

### 33.2 Pacote (Core)

`TermoVisionPackage.Read(Stream zip)`: lê `clivus.json` (sem extrair nada para o
disco; limite de tamanho por entrada), `orto.jgw` e os bytes do `orto.jpg` se
houver. Modelo: crs, geolocalização, área, terreno (grade), orto, objetos,
projeto. `Terrain.Points()` e `Terrain.Triangles()`: nó (i, j) em
(e0 + i·passo, n0 + j·passo), `z[j·colunas + i]`, linha 0 ao sul, células com
null fora. World file: 6 linhas → tamanho do pixel e canto. Testes com pacote
montado no teste (grade 3×2 com um null, json inválido, falta clivus.json).
Fecha automático.

### 33.3 Retorno (Core)

`TermoVisionReturn.Build(plugin, desenho, mesas)` → JSON do contrato. Entrada:
guid, nome, os 4 cantos na ordem do `CantosDaMesa`, colunas, fiadas, módulo
(largura ao longo, altura na rampa, espaço). Confere: 4 cantos, sem NaN,
colunas e fiadas ≥ 1. `TermoVisionResult.Parse` lê a resposta 200.
Fecha automático.

### 33.4 Cliente HTTP (Core)

`TermoVisionClient`: `Summary()`, `Package()`, `Send(corpo, pedido)` com
`Authorization: Bearer`, `X-Pedido`, gzip no envio, timeout. Erros 401/404/400/413
viram mensagem com o `detail` do servidor. Testes com `HttpMessageHandler` falso;
mais um teste opcional contra `http://127.0.0.1:8731` quando a variável
`CLIVUS_TV_TESTE` traz um endereço de teste.
Fecha automático.

### 33.5 Protocolo e pasta de entrada

Projeto `Clivus.Link` (WinExe, net8.0-windows): valida com `TermoVisionLink` e
grava em `%LOCALAPPDATA%\Clivus Solar\entrada\`. Vai no bundle. O plugin, ao
carregar, grava `HKCU\Software\Classes\clivus` (`URL Protocol`, comando
`"<pasta do bundle>\Clivus.Link.exe" "%1"`) se faltar ou apontar para outro
lugar. Vigia da pasta: na folga, pergunta e chama a montagem; arquivo lido é
apagado; arquivo com mais de 7 dias é descartado.
**Validação do Renan:** com o Civil 3D aberto, no TermoVision clica "Exportar
área para o Clivus"; o Civil 3D pergunta se abre a área.

### 33.6 Abrir do TermoVision

Comando `CLIVUS_TV_ABRIR` (o endereço colado ou vindo da pasta): mostra o resumo
(`/envio`), baixa o pacote com andamento, cria a TIN, o orto (RasterImage no
lugar pelo world file), a área do Clivus, as árvores, a geolocalização, e grava
servidor e envio no desenho (token cifrado fora, decisão 1). Desenho com outro
envio: pergunta antes de trocar. Zoom na área no fim.
**Validação do Renan:** abre a área da Paradise e confere terreno, orto no
lugar, contorno e árvores; orbita em 3D.

### 33.7 Exportar para o TermoVision

Comando `CLIVUS_TV_EXPORTAR`: lê as mesas (`LayoutScan` + `MesasDoDesenho`),
monta o retorno, manda com `X-Pedido` novo, mostra a resposta (mesas, saíram,
fora da área, avisos) e oferece abrir a `url`. Sem envio no desenho: avisa e
oferece colar um endereço. Mesa sem perfil conhecido: fica fora e é contada.
**Validação do Renan:** desenha mesas, exporta e vê as mesas no TermoVision.

### 33.8 Painel TermoVision na ribbon

Painel **TermoVision** com Abrir e Exportar, ícones novos, nomes em inglês e
espanhol. Teste em `RibbonLayoutTests`.
**Validação do Renan:** confere o painel e os dois botões.
