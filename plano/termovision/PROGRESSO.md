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
5. Na ribbon, Saída › TermoVision › Exportar para o TermoVision. Conferir as
   mesas lá.
6. Sem o protocolo: Saída › TermoVision › Abrir do TermoVision e colar o
   "Copiar o código" da página.

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
