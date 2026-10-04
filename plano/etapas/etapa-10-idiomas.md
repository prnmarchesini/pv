# Etapa 10: Clivus Solar em vários idiomas (português, inglês, espanhol)

Origem: Renan, 04/10/2026: "Quero ter web e plug-in multi linguagem, iniciando
por inglês e espanhol"; "Se possível, comandos CAD também".

O tamanho (contado em 04/10/2026): no plugin, umas 2 mil frases (867 linhas
com acento só no plugin, 475 no Core), 459 mensagens de linha de comando, 17
janelas, 112 comandos e 61 pontos que formatam número no padrão brasileiro.
Na web, a landing, as 4 páginas do app e o visualizador 3D.

## Decisões (tomadas sem perguntar, pela regra de seguir; o Renan muda se quiser)

- **O português é a fonte.** O texto em português continua escrito no código,
  como hoje; ele é a chave. Inglês e espanhol ficam em catálogos (`en.json`,
  `es.json`), "frase em português → tradução". Código que não foi traduzido
  continua funcionando em português: nada quebra no meio do caminho.
- **Espanhol neutro** (América Latina), com vírgula decimal. Inglês com ponto
  decimal. Datas no formato do idioma.
- **Qual idioma:** em Configurações, "Idioma: Automático / Português / English /
  Español". Automático segue o idioma do Civil 3D instalado. Fica guardado no
  perfil do usuário, não no desenho.
- **O que NÃO muda de idioma:** nomes de camada, de bloco, o XData, os nomes
  globais dos comandos (`CLIVUS_*`, que os botões e os scripts usam) e os
  arquivos de troca (PVsyst tem formato fixo). Desenho feito em português abre
  igual em inglês: só a tela muda.
- **Comandos:** cada comando ganha um nome digitável em inglês e em espanhol
  (por exemplo `CLIVUS_SWAP_TABLE`, `CLIVUS_CAMBIAR_MESA`), registrados
  conforme o idioma escolhido. O nome em português continua valendo sempre.
- **Web:** a landing em `/`, `/en/` e `/es/`, geradas de um modelo só; o app
  escolhe pelo `?lang=`, depois pelo cookie, depois pelo navegador
  (`Accept-Language`). O plugin manda o idioma dele ao servidor, e o erro volta
  no idioma.
- **Testes:** o nível 2 continua em português (as mensagens que ele confere não
  mudam). Casos novos rodam em inglês e em espanhol. Um teste garante que toda
  frase usada no código tem tradução nos dois catálogos, com os mesmos
  marcadores (`{0}`, `{1}`).

## 10.1 O mecanismo (Core)
`Idioma` (o idioma atual, a cultura de números e datas) e `Tr` (traduzir uma
frase, ou uma frase com marcadores). Catálogos embutidos no Core. Frase sem
tradução volta em português e fica registrada. Testes: troca de idioma,
marcadores, número com vírgula e ponto.
Validação: automática (só modelo).

## 10.2 A guarda (teste)
Ferramenta que extrai do código toda frase passada a `Tr`, e teste que exige
tradução em inglês e espanhol para cada uma, com os mesmos marcadores, e
nenhuma tradução sobrando no catálogo.
Validação: automática.

## 10.3 Configurações > Idioma, ribbon e janelas (tela)
A escolha do idioma; a ribbon (rótulos e dicas) e as 17 janelas traduzidas;
trocar o idioma refaz a ribbon na hora.

## 10.4 Mensagens da linha de comando e relatórios (tela)
As mensagens dos comandos, os avisos do vigia, os relatórios (Estado,
Análises, resumo do terreno) e o Excel, com os números no padrão do idioma.

## 10.5 Comandos em inglês e espanhol (tela)
Os nomes digitáveis de cada comando nos dois idiomas, registrados conforme o
idioma; ajuda (`CLIVUS_AJUDA`) lista os do idioma atual.

## 10.6 Instalador (tela)
O instalador no idioma do Windows (português, inglês, espanhol).

## 10.7 Landing em inglês e espanhol (web)
`/en/` e `/es/`, seletor de idioma, `hreflang`; o idioma do navegador sugere.

## 10.8 App em inglês e espanhol (web)
A página 3D, a área do cliente, o entrar e os erros da API no idioma; o
plugin manda o idioma dele.
