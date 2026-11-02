# Progresso da ponte com o TermoVision

Status possíveis: PENDENTE, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

**Etapa aberta em 26/10/2026 a pedido do Renan ("abrir etapa 33 já").
As decisões 1 a 4 do `LEIA-PRIMEIRO.md` ficam para ele confirmar.**

## Etapa 33

- 33.1 Endereço clivus://: VALIDADO (automático)
- 33.2 Pacote: VALIDADO (automático) — inclui o pacote real da Paradise no acervo
- 33.3 Retorno: VALIDADO (automático)
- 33.4 Cliente HTTP: VALIDADO (automático) — também contra o servidor de teste (CLIVUS_TV_TESTE)
- 33.5 Protocolo e pasta de entrada: AGUARDANDO VALIDAÇÃO
- 33.6 Abrir do TermoVision: AGUARDANDO VALIDAÇÃO
- 33.7 Exportar para o TermoVision: AGUARDANDO VALIDAÇÃO
- 33.8 TermoVision na ribbon: AGUARDANDO VALIDAÇÃO
- 33.9 O projeto do TermoVision sem envio (02/11/2026, contrato v2 com a sessão do TermoVision; Renan: "se eu fizer pelo termovision, o cliente vai ver outro produto e ficar curioso"; "a meta é eu chegar e ver o dwg ... aberto em um 3D do termovision sobre vista de satelite em link publico"): o desenho com mesas que não veio do TermoVision vira uma usina pública de lá (dona: a empresa de sistema "Clivus Solar"); as mesas (com monoposte/biposte), o terreno em grade (2 m até 2 km de lado, as mesas com 100 m de folga, linha 0 ao sul) e o sistema de coordenadas (a zona e o hemisfério, o EPSG do SIRGAS na América do Sul) vão ao servidor do Clivus com a licença, que repassa ao TermoVision com a chave dele (nunca no PC) e devolve o link; o GUID do desenho fica no dicionário do plugin (TERMOVISION_PROJETO): mandar de novo atualiza a mesma usina e o mesmo link, e o "Salvar como" pergunta; o desenho fora de UTM na zona da localização é recusado: AGUARDANDO VALIDAÇÃO — `TermoVisionProject` (Core, 6 testes), `TermoVisionProjeto` (plugin), `CLIVUS_TV_PROJETO_AUTO`; o corpo do PLAT A_Perfiles (Panamá): 589 mesas biposte, terreno 278 × 358 de 2 m, 17N EPSG 31971
- Revisão do 33.9 (02/11/2026, por subagente):
  - As duas perguntas do botão têm o Não como padrão e dizem que o link é público. Se o desenho já criou a usina, a pergunta passa a ser "Atualizar".
  - O GUID é gravado no desenho antes do envio. O X-Pedido sai do GUID e do corpo: o mesmo conteúdo dá o mesmo pedido.
  - A zona UTM vem do código do sistema do desenho (UTM84-17N, SIRGAS2000.UTM-23S), senão da localização, senão de uma zona vizinha. O código WGS 84 dá o EPSG 326/327 (`TermoVisionProjectCrs.Resolve`, 2 testes novos).
  - O envio tem janela de andamento com Cancelar, vai em pedaços e usa Expect: 100-continue.
  - O plugin recusa sem mesa válida ou com mais de 5000 mesas.
  - Só abre e copia o link se for https de termovision.tech.
- Envio real (02/11/2026, a cópia do PLAT A_Perfiles pelo Core Console, licença desta máquina): 589 mesas, terreno 278x358 de 2 m, 17N EPSG 31971. O link é https://app.termovision.tech/v/tvv_Hh1ajMUX-lxIqkg_zdt0hf1eMH2FI0vwWfLnypegCZo (usina mc7qhhf0, 90 dias).
  - O 3D está certo: as mesas sobre o terreno com o satélite da Esri e a sombra do instante.
  - O 2D do TermoVision abria no interior de SP, e não no Panamá. Foi avisado à sessão do TermoVision, que é dona da correção.
  - A termovision-63 corrigiu tudo no mesmo dia:
    - o 2D enquadra pela grade do terreno (7982d652);
    - o terreno do Clivus não vira mais "objeto" (43c88048): os taludes de 4 m faziam sombra falsa;
    - o 3D guarda as placas (0,3 s em vez de 10 s).
  - Conferido de novo no Chrome: a usina em 8,2138 / -81,5972, ao lado da subestação e da rodovia; 21/06 às 07:30 dá sombra em 175 mesas, de outra mesa.
  - Sai na 0.1.14 (614824f), publicada e instalada.
- 33.10 A estrutura das mesas fixas no projeto do TermoVision (02/11/2026; o Renan, olhando o 3D do PLAT A só com as placas: "leve a estrutura tbm, fica mais profissional"; o TermoVision desenha os `estrutura.triangulos` da mesa fixa por baixo da face, d6426dc7): AGUARDANDO VALIDAÇÃO (a tela é o 3D do link).
  - Vão os pilares, do pé ao topo do bloco, e as barras do biposte que estão no desenho: viga, mão francesa, terça e tirante. Os módulos não vão, e a monoposte leva só os pilares.
  - O pé do pilar fica 0,3 m abaixo do menor entre o TIN e o nó mais baixo da célula da grade de 2 m que vai junto. No talude, a grade passava até 0,5 m abaixo do TIN, e 13 pilares flutuavam.
  - O detalhe desce até caber: 5000 triângulos por mesa, 400 mil no envio e 18 MB de corpo.
  - O código está em `TermoVisionStructure` (Core, 7 testes) e no `TermoVisionProjeto.Suportes`.
  - Nível 2 (`-SoTermoVision`): o projeto da Paradise com 21 pilares, o pé de cada um abaixo do terreno.
  - O PLAT A foi reenviado sem os tirantes, porque com eles passava dos 18 MB: 158.688 triângulos, 16,6 MB (1,8 MB em gzip). Nenhum dos 7.068 pilares fica acima da grade, e nada passa acima da face dos módulos. O link é o mesmo.
  - Commits 3955e0e e f969eb5. Saiu na 0.1.15 (0e380aa, id 13 no servidor, SHA-256 c048ebe5…1e4a), publicada e instalada no PC do Renan. Core 2769/2769.
  - Do lado do TermoVision ficaram dois pontos, anotados pela termovision-63 para quando a energia voltar: a treliça aparece por cima da face em algumas mesas (é briga de profundidade, porque a estrutura fica 32 a 33 mm abaixo da face em todas as 589; o conserto é um gl.polygonOffset) e a rota das placas, que pesa 19,5 MB e leva de 10 a 30 s.

Nível 2 (`rodar.ps1 -SoTermoVision`): abre o pacote da Paradise sem rede num
desenho vazio. Confere superfície, orto, árvores, área e árvore na cota do TIN.
Depois põe uma fileira e confere o retorno: 3 mesas, borda baixa primeiro,
cotas no terreno. O corpo gerado foi mandado à mão ao servidor de teste em
26/10 e voltou 200 (`gravado: true`, 3 nasceram). A sessão do TermoVision
conferiu do lado de lá:
- 14 × 2 módulos de 1,303 × 2,384;
- inclinação 20,0°, frente para o norte;
- ponta baixa 0,33 a 0,36 m acima do terreno de lá;
- 4,4995 m em planta (a rampa × cos 20°), 18,69 m de comprimento.

Bate com o contrato; nada a mudar do lado deles.

## Roteiro de tela para o Renan

1. Instalar a branch (`publicar-bundle.ps1 -Instalar`) com o Civil 3D fechado e
   abrir o Civil 3D com um desenho novo.
2. No TermoVision, em Nova UFV, escolher a área e clicar "Exportar área para o
   Clivus". O Civil 3D vem para a frente e pergunta se abre a área (com o nome
   do servidor). Responder Sim.
3. Conferir o terreno (superfície "TV <área>"), o orto no lugar, o contorno da
   área, as árvores e o contorno real delas (layer CLIVUS_TV_OBJETO). Orbitar
   em 3D.
4. Desenhar mesas como sempre.
5. Na ribbon, Saída › TermoVision (botão único desde 28/10/2026: no desenho
   que veio de lá, manda as mesas). Conferir as mesas lá.
6. Sem o protocolo, num desenho que não veio do TermoVision: Saída ›
   TermoVision e colar o "Copiar o código" da página (CLIVUS_TV_ABRIR
   continua para abrir outra área num desenho que já veio de lá).
7. **O projeto sem envio (33.9):** num desenho com mesas que não veio do TermoVision (o PLAT A_Perfiles),
   Saída › TermoVision: a pergunta "Criar lá uma usina pública...?"; Sim. A linha de comando diz a usina, as
   mesas e o link; o link vai para a área de transferência e abre no navegador: as mesas em 3D sobre o
   terreno, com o satélite por baixo. Mandar de novo: o mesmo link. Salvar como outro arquivo e mandar: a
   pergunta "Atualizar a mesma usina?".

## Observações

Revisões feitas: uma do Core (33.1 a 33.4) e uma do plugin (33.5 a 33.8).
Tudo que elas apontaram de grave foi corrigido. Ficaram anotados:

- **Desinstalar não apaga o protocolo.** A chave `HKCU\Software\Classes\clivus`
  fica. Quem grava é o último plugin carregado que tem o `Clivus.Link.exe` ao
  lado.
- **Token em texto.** O endereço colado fica no histórico da linha de comando.
  O arquivo `.clivus` da pasta de entrada guarda o token em texto até o plugin
  ler (ele apaga ao ler; o vencido é descartado).
- **Foco do Windows.** Trazer o AutoCAD para a frente pode só piscar a barra de
  tarefas (regra de foreground do Windows). Se incomodar, o Link chama
  `AllowSetForegroundWindow`.
- **Download sem barra.** O Abrir baixa o pacote sem barra de andamento nem
  Cancelar. Para a Paradise (0,7 MB) não pesa; para área grande pode valer uma
  janela de progresso.
- **Caminho do orto.** O orto fica em `<desenho>_TermoVision\<envio>\orto.jpg`
  ao lado do DWG salvo, ou em `%LOCALAPPDATA%` no desenho novo. A imagem guarda
  o caminho absoluto: num "Salvar como" em outra pasta, o orto fica onde estava.
- **Colunas e fiadas.** Saem do maior índice de face mais 1. Numa mesa cortada
  no meio, a contagem não muda.
- **Coordenadas do Civil.** O sistema de coordenadas do Civil não é mudado (o
  desenho fica em UTM puro). Só a localização do Clivus é gravada.
- **Um envio por desenho.** Outra área pede um desenho novo, e o mesmo envio
  não abre duas vezes.
- 28/10/2026: a área aberta chegava sem curvas de nível. O terreno estava lá (TIN
  com 10.824 pontos da grade de 1 m, 564,90 a 568,64 m); faltava estilo: a TIN
  nascia com o estilo padrão do desenho, que fora do modelo do Civil mostra só a
  borda. Agora o Abrir dá à TIN o estilo "CLIVUS Curvas {secundária} e {mestra} m"
  (borda, mestra e secundária; intervalo pelo desnível, `ContourIntervals`: no
  máximo 20 curvas; a Área 2 sai com 0,2 m e 1 m). Não foi preciso pedir curvas
  nem pontos novos ao TermoVision: a grade do pacote já é o DEM. Conferido no
  Core Console com o pacote do acervo ("CLIVUS_TV CURVAS secundaria=0.2 mestra=1").
