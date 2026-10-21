# Regras invioláveis da drenagem

Somam-se às regras sagradas do layout.

## 1. Tudo acompanha o terreno

Flecha, linha de análise, perfil, canaleta e bacia tiram a cota do TIN
(`TerrainCache`). Nunca do clique, nunca Z = 0. Ponto fora do TIN: avisa e pinta.

## 2. A linha é do Renan

Linha de análise e canaleta nascem de polyline que o Renan desenha. O sistema
não inventa, não move e não "endireita" o traçado em planta. Pode adensar
vértices em 3D para seguir o terreno, nunca mudar o caminho em planta.

## 3. O sentido da água é o do terreno

O sentido do fluxo de uma canaleta é do ponto mais alto para o mais baixo do
fundo. Se a polyline foi desenhada ao contrário, o sistema não a reverte: só
desenha a seta de fluxo do lado certo e conta as estacas a partir da montante.

## 4. Nunca falhar calado

Trecho com declividade abaixo da mínima (0,005 m/m, NBR 10844 5.7.1), trecho
invertido (água parada), lâmina acima da altura útil, velocidade acima da
máxima do revestimento ou abaixo da mínima de autolimpeza, área que não chega
em canaleta nenhuma: o sistema lista o motivo e **pinta** o trecho ou a área.

## 5. O sistema calcula, o Renan decide

Igual ao roteamento: o sistema não escolhe seção sozinho nem "aprova" nada.
Recebe tipo e dimensões, faz as contas e mostra os números e os alertas. A única
sugestão automática permitida é a altura adotada `y` (lâmina + bordo livre
arredondada para cima em 5 cm), e ela aparece como sugestão, editável.

## 6. Mesma conta que o memorial

As fórmulas são as do memorial de referência, inclusive a forma da Kerby usada
nele (ver `02-calculos-hidraulicos.md`). Os números de ouro do memorial são
teste de nível 1 e não se afrouxam.

## 7. Cada coisa desenhada é entidade com identidade

Canaleta e bacia têm GUID e XData `CLIVUS` (tipo, seção, vínculo
canaleta ↔ bacia). Layer é aparência, nunca identidade. Recalcular nunca
duplica: substitui o que o mesmo GUID já desenhou.

## 8. Texto legível

Texto do gráfico horizontal; texto ao longo de linha segue o eixo e nunca fica
de cabeça para baixo (regra de 07/10).
