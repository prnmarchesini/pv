# Clivus Solar: ponte com o TermoVision. Instruções para o Claude Code

> Plano escrito em 26/10/2026 a partir do pedido do Renan, trazido pela sessão do
> TermoVision: *"se eu tiver o clivus aberto em paralelo com o termovision, eu quero
> selecionar a area e clicar 'Exportar area para o clivus' ai abre direto no meu
> autocad. Ai eu desenho os modulos (...) e ai, clico no clivus, exportar para o
> termovision"*. Renan escolheu "abrir etapa 33 já" em 26/10/2026.
> Etapa **33**, branch `termovision`.

Vale tudo que já valia: regras sagradas (`plano/01-regras-sagradas.md`),
camadas (`plano/02-arquitetura.md`), testes e acervo (`plano/04-testes.md`),
frases de tela em `Tr.T`/`Tr.F` com inglês e espanhol (etapa 10).

Leia nesta ordem:

1. `contrato-clivus.md` (cópia de `C:\Dev\termovision\docs\contrato-clivus.md`;
   quem manda é o original: se mudar, recopie)
2. `PROGRESSO.md`
3. `etapas/etapa-33-termovision.md`

## Regra de ouro

Um passo por vez. Passo com coisa na tela fica `AGUARDANDO VALIDAÇÃO` e o
trabalho segue; passo só de modelo fecha `VALIDADO (automático)`.
**Mudança no contrato: escrever antes à sessão do TermoVision** (`termovision-2f`).

## O caminho

1. No TermoVision, "Exportar área para o Clivus" abre
   `clivus://termovision?v=1&servidor=…&envio=cv_…&token=tvc_…`.
2. O Windows chama `Clivus.Link.exe "<endereço>"` (protocolo `clivus:` no HKCU,
   gravado pelo próprio plugin ao carregar, apontando para o exe da pasta do
   bundle). O exe só confere o formato e grava o endereço em
   `%LOCALAPPDATA%\Clivus Solar\entrada\<guid>.clivus` e sai. Nada de passar o
   endereço por linha de comando de shell (tem `&`).
3. O plugin vigia essa pasta (`FileSystemWatcher`); na folga do AutoCAD pergunta
   "Abrir a área X da usina Y?" e roda a montagem no desenho ativo. Com o AutoCAD
   fechado o arquivo espera e é pego no próximo carregamento.
4. Sem protocolo: comando **Abrir do TermoVision** com o endereço colado.
5. Montagem: superfície TIN da grade, orto como imagem no lugar, contorno como
   área do Clivus, objetos como árvores, geolocalização.
6. **Exportar para o TermoVision**: mesas do desenho → `POST /retorno`.

## Onde mora cada coisa

- `Clivus.Core`: `TermoVisionLink` (ler e validar o endereço), `TermoVisionPackage`
  (ler o `clivus.json`, a grade em pontos e triângulos, o world file),
  `TermoVisionReturn` (montar o JSON do retorno a partir das mesas),
  `TermoVisionClient` (os três pedidos HTTP, com `HttpMessageHandler` injetável).
- `Clivus.Link` (projeto novo, WinExe sem janela, sem CAD): grava o endereço na
  pasta de entrada.
- `Clivus.Plugin`: registro do protocolo, vigia da pasta, comandos
  `CLIVUS_TV_ABRIR` e `CLIVUS_TV_EXPORTAR`, criação da TinSurface, da imagem
  raster, da área e das árvores.

## Decisões (para o Renan confirmar)

1. **Token fora do DWG.** O contrato pede guardar servidor, envio e token. No
   desenho vão servidor e envio (NOD, chave `TERMOVISION`); o token vai em
   `%LOCALAPPDATA%\Clivus Solar\termovision\<envio>.token`, cifrado por usuário
   (DPAPI). Motivo: o DWG é mandado a terceiros e o token grava mesas por 7 dias.
   Para o servidor nada muda.
2. **Objetos viram árvores do Clivus** (as sombras e o PVsyst já as entendem):
   copa com o diâmetro do círculo de mesma área do contorno, altura total =
   `altura_m`, tronco 40 %, copa 60 %, pé no `chao_m`. O contorno original fica
   desenhado na layer `CLIVUS_TV_OBJETO` para o Renan ver o formato real.
3. **Terreno:** TIN nova `TV <área>` com os nós que têm cota (null fica fora).
   O Civil 3D triangula; o lado máximo de triângulo (1,5 × o passo) impede que
   ele cubra os buracos sem dado. Vira o terreno do Clivus (como o Processar).
   O sistema de coordenadas do Civil não é mudado: o desenho fica em UTM puro
   e a localização (lat/lon) vai para o Clivus, que é o que o sol usa.
4. **Cantos da mesa no retorno:** os vértices do contorno da mesa
   (`CantosDaMesa`: (0,0) → (L,0) → (L,M2) → (0,M2)), que já são borda baixa
   do começo ao fim e alta voltando, como o contrato pede.

## Checklist do revisor (somar ao do protocolo)

- [ ] Token aparece em log, em mensagem de tela, no DWG ou em exceção?
- [ ] Servidor sem HTTPS aceito fora de `127.0.0.1`/`localhost`?
- [ ] Endereço malformado ou parâmetro faltando derruba o AutoCAD em vez de avisar?
- [ ] ZIP com caminho `..` ou arquivo gigante é extraído sem freio?
- [ ] Grade: índice `z[j·colunas + i]`, linha 0 ao sul, null fora; passo e origem certos?
- [ ] Cantos: ordem 1→2 borda baixa no sentido da fileira, 3→4 alta voltando; Z da face de cima?
- [ ] Mesmo clique reenviado leva o mesmo `X-Pedido`; clique novo leva outro?
- [ ] Erro HTTP mostra o `detail` do servidor em vez de um texto genérico?
