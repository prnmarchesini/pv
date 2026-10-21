# Clivus Solar: drenagem. Instruções para o Claude Code

> Plano escrito em 21/10/2026 a partir do pedido do Renan
> (`0 - Assets/Drenagem/Melhorias renan.docx`) e do memorial de referência
> (`0 - Assets/Drenagem/Memorial Drenagem R00.docx`, UFV Valparaiso, 13/10/2026).
> Etapas **25 a 31**, depois das 17 a 24 do roteamento.

Vale tudo que já valia: regras sagradas do layout (`plano/01-regras-sagradas.md`),
arquitetura em camadas (`plano/02-arquitetura.md`), testes e acervo
(`plano/04-testes.md`) e a etapa 10 (toda frase de tela nova em `Tr.T`/`Tr.F`,
com inglês e espanhol). Aqui está só o que muda ou se soma.

Leia nesta ordem antes de qualquer código:

1. `01-regras-drenagem.md`
2. `02-calculos-hidraulicos.md` (fórmulas, tabelas e os números de ouro do memorial)
3. `PROGRESSO.md`
4. O arquivo da etapa atual em `etapas/`

## Regra de ouro

**Um passo por vez.** Passo com coisa na tela fica `AGUARDANDO VALIDAÇÃO` e o
trabalho segue (pedido de 26/09/2026); passo só de número ou modelo fecha como
`VALIDADO (automático)`. Fora do escopo: anotar em `PROGRESSO.md`, Observações.

## O que é o módulo

Painel **Drenagem** na aba Clivus da ribbon, com três grupos:

| Grupo | Botões | O que faz |
|---|---|---|
| **Análise** | Flechas, Linhas | Flechas de sentido da água sobre o TIN, coloridas por faixa de declividade. Linha de análise: o Renan desenha, o perfil aparece pronto. |
| **Desenho** | Canaleta | Transforma linha de análise em canaleta (meia-cana ou trapezoidal), com sólido 3D e perfil de caída. |
| **Memória** | Bacias, Memória | Zonas de influência de cada canaleta (hatch), lista canaleta × área e o cálculo hidráulico completo do memorial, com Excel. |

Fluxo do projetista: Flechas (entender o terreno) → Linhas (testar caminhos) →
Canaleta (fixar o caminho e a seção) → Bacias (quem manda água para quem) →
Memória (vazão, lâmina, velocidade, seção adotada, dissipador, transposição).

## O que já existe e será reaproveitado

| Precisa | Já existe | Onde |
|---|---|---|
| Cota do terreno | `Tin.TryGetZ`, `TerrainCache.Get(doc)` | `Clivus.Geo/Tin.cs`, `Clivus.Plugin/TerrainCache.cs` |
| Linha assentada no TIN | `Draping.Along` / `Draping.Below` | `Clivus.Geo/Draping.cs` |
| Seta com % ou grau | `SetaDeDeclividade`, `SlopeLabel`, `SlopeUnit` | `Clivus.Plugin/SetaDeDeclividade.cs`, `Clivus.Core/SlopeLabel.cs` |
| Escala de cor | `PaletaDeCores`, `SombrasCommands.CorDaFracao` | `Clivus.Plugin` |
| Linhas e textos no modelo | `DrawingTables`, `TableFrame`, `TableCells` | `Clivus.Core` |
| Hatch | `GroupDrawer`, `AreaDoTrafo` | `Clivus.Plugin` |
| Layers `CLIVUS_*`, XData, dicionário | `LayoutLayers`, `PluginXData`, `PluginDictionary` | `Clivus.Plugin` |
| Ribbon | `RibbonLayout.Tabs` (dados) + `RibbonClivus` (montagem) | `Clivus.Core`, `Clivus.Plugin` |
| Janela WPF em código | `JanelaDeCabos`, `JanelaDeSombras` | `Clivus.Plugin` |
| Excel | `XlsxWriter`, padrão do `QuantityReport` | `Clivus.Core` |
| Visualização 3D web | `ColladaWriter`, `Publicador3D` | `Clivus.Core` |

O que **não** existe e nasce aqui: normal/declividade/aspecto por triângulo do
TIN, caminho de escoamento (descida mais íngreme), gráfico de perfil, sólido
varrido ao longo de caminho, hidráulica (Manning, IDF, método racional).

## Onde mora cada coisa (camadas)

- `Clivus.Geo`: `TinSlope` (declividade e aspecto por triângulo), `FlowTracer`
  (descida mais íngreme sobre o TIN), `ProfileSampler` (estacas de uma linha),
  `Watershed` (bacias por canaleta), `ChannelSection` (geometria 2D da seção).
- `Clivus.Core`: `Hydraulics` (Manning, lâmina normal por bisseção, bordo livre),
  `Rainfall` (IDF, Kirpich, Kerby, método racional), `DrainageReport` (tabelas e
  Excel), `ProfileChart` (layout do gráfico em coordenadas de papel, sem CAD).
- `Clivus.Plugin`: comandos `CLIVUS_DRN_*`, janelas, desenho de setas, perfil,
  sólido, hatch.

Nada de AutoCAD em Core ou Geo. Todo o cálculo é testável em nível 1.

## Checklist do revisor (somar ao do `plano/03-protocolo-de-passo.md`)

- [ ] Alguma cota veio do clique em vez do TIN?
- [ ] Declividade: sinal (descendo é positivo no sentido do fluxo), % contra m/m contra grau?
- [ ] Unidades: L em km na Kirpich/Kerby e em m no resto; Q em l/s contra m³/s; mm/h; m² contra ha?
- [ ] Trecho com declividade abaixo da mínima ou invertida fica calado?
- [ ] Bacia com área que não chega em canaleta nenhuma some sem aviso?
- [ ] Soma das áreas das bacias + área sem destino = área analisada?
- [ ] Lâmina maior que a altura da seção (canaleta afogada) passa sem aviso?
- [ ] Texto virado ou em pé no gráfico ou nas setas?
- [ ] O sistema "aprova" a canaleta? (proibido: mostra os números e os alertas; quem decide é o Renan)
