<#
    Rota CC (rodada de tela do Renan de 10/10/2026, plano/melhorias-2026-10-10.md):
      - item 19: com os dois inversores "Automatico pelas strings" e nenhum em
        campo, a aba CC fica liberada (o Gerar CC e quem os poe);
      - item 11: de tres linhas viradas vala, uma apagada sai no Atualizar
        valas e outra solta continua no desenho (Polyline3d no TIN, sem a
        marca, fora da camada da vala); a rota fica com uma vala;
      - item 10: depois do Gerar CC, em cada mesa com cabo, todos os cabos
        das strings dela passam por um ponto comum fora dela (a entrada unica),
        do lado da borda mais alta da mesa;
      - itens 12 e 16: o resumo CC agrupado U1 > T1 > Inversor 1 / Inversor 2
        com os subtotais, a coluna da tag da string e as colunas de calculo;
      - item 19: o Inversor 1 movido a mao e o Recalcular rota so dele: os
        lances do Inversor 2 ficam os mesmos (handles iguais), os do 1 sao
        novos e chegam na posicao nova;
      - item 18: o Inversor 2 apagado sai do resumo com o aviso; os dois
        apagados: "Inversor nao esta em campo: nao e possivel mostrar o resumo";
        com os cabos apagados tambem, o resumo ainda avisa: os automaticos
        "ainda nao postos" (o Gerar CC os poe) e o posto a mao "nao esta em campo";
      - item 1 (revisao): o Inversor 1 levado com o MOVE do AutoCAD fica com
        a cota do lugar velho; o Recalcular reassenta a base no TIN + 0,80.
    A usina tem 60 x 60 m no centro do terreno; a vala CC corre norte-sul 5 m
    a leste dela (como no caso do local dos inversores).
#>
function Testar-EletricaRotaCc {
    param([string] $Desenho)

    $rotulo = 'clivus-rota-cc'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }
    function Q([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###} {1:0.###}', $cx + $dx, $cy + $dy) }

    $sub = @{
        '{{A1}}' = (P -30 -30); '{{A2}}' = (P 30 -30); '{{A3}}' = (P 30 30); '{{A4}}' = (P -30 30)
        '{{L1}}' = (P -30 -30); '{{L2}}' = (P -30 30); '{{LADO}}' = (P 0 0)
        '{{V1}}' = (P 35 -35); '{{V2}}' = (P 35 35)
        '{{X1}}' = (P 40 -25); '{{X2}}' = (P 40 -15); '{{X1XY}}' = (Q 40 -25)
        '{{Y1}}' = (P 45 10); '{{Y2}}' = (P 45 20); '{{Y1XY}}' = (Q 45 10)
        '{{M}}' = (P 40 5); '{{M0}}' = (P 40 -12)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-rota-cc.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ROTACC_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    # O trecho do texto entre dois marcadores.
    function Trecho([string] $de, [string] $ate) {
        $i = $t.IndexOf($de)
        $j = if ($ate) { $t.IndexOf($ate, [math]::Max($i, 0)) } else { $t.Length }
        if ($i -lt 0 -or $j -lt 0) { return '' }
        return $t.Substring($i, $j - $i)
    }

    # ---- (19) a aba CC liberada sem inversor em campo ------------------------
    $abas = Trecho 'CLIVUS_ROTACC ABAS' 'CLIVUS_ROTACC APAGAR'
    if ($abas -notmatch 'ROTA CC: dispon') { $erros += 'com os inversores automaticos fora de campo, a aba CC nao ficou liberada' }
    if ($abas -notmatch 'ROTA CA: falta') { $erros += 'a aba CA deveria continuar pedindo o inversor em campo' }

    # ---- (11) atualizar e soltar valas --------------------------------------
    if ($t -notmatch 'CLIVUS_ROTACC APAGAR achada=1') { $erros += 'nao achei a vala a apagar (o teste nao apagou nada)' }
    $atualizar = Trecho 'CLIVUS_ROTACC ATUALIZAR' 'CLIVUS_ROTACC SOLTAR'
    if ($atualizar -notmatch '1 vala\(s\) apagada\(s\) do desenho sa') { $erros += 'o Atualizar valas nao disse que a vala apagada saiu' }
    if ($atualizar -notmatch 'A rota tem 2 vala\(s\)') { $erros += 'depois do Atualizar a rota deveria ter 2 valas' }
    if ($t -notmatch 'CLIVUS_ROTACC SOLTAR achada=1') { $erros += 'nao achei a vala a soltar' }
    $soltar = Trecho 'CLIVUS_ROTACC SOLTAR' 'CLIVUS_ROTACC GERAR'
    if ($soltar -notmatch '1 linha\(s\) deixaram de ser vala C\S+ .*A rota tem 1 vala\(s\)') { $erros += 'o Soltar valas nao soltou uma linha (ou a rota nao ficou com 1 vala)' }
    if ($t -notmatch 'CLIVUS_ROTACC_SOLTA tipo=(\S+) flags=(\d+) xdata=(\d) camada=(\S+) fim') { $erros += 'a linha solta sumiu do desenho' }
    else {
        if ($Matches[1] -ne 'POLYLINE' -or (([int]$Matches[2]) -band 8) -ne 8) { $erros += "a linha solta deixou de ser a Polyline3d do TIN ($($Matches[1]) flags $($Matches[2]))" }
        if ($Matches[3] -ne '0') { $erros += 'a linha solta ainda tem o XData do Clivus' }
        if ($Matches[4] -like 'CLIVUS_VALA*') { $erros += 'a linha solta ficou na camada da vala' }
    }

    # ---- (10) uma entrada por mesa, pelo lado alto ----------------------------
    $conf1 = Trecho 'CLIVUS_ROTACC CONFERIR1' 'CLIVUS_ROTACC RESUMO1'
    $entradas = @([regex]::Matches($conf1, 'ROTA_ENTRADA mesa=(\S+) cabos=(\d+) comum=(\d) alto=(\S) distancia=(\S+) fim'))
    if ($entradas.Count -lt 3) { $erros += "esperava ao menos 3 mesas com cabo CC, achei $($entradas.Count)" }
    foreach ($e in $entradas) {
        if ($e.Groups[3].Value -ne '1') { $erros += "a mesa $($e.Groups[1].Value) tem cabos sem um ponto de entrada comum ($($e.Groups[2].Value) cabos)" }
        elseif ($e.Groups[4].Value -ne '1') { $erros += "a entrada da mesa $($e.Groups[1].Value) nao e do lado alto" }
        # O ponto comum mais perto da mesa tem que ser a entrada dela (rente a borda, a 0,5 m), nao o encontro la na vala.
        elseif ([double]::Parse($e.Groups[5].Value, $inv) -gt 1.0) { $erros += "a mesa $($e.Groups[1].Value) nao tem uma entrada so: os cabos so se juntam a $($e.Groups[5].Value) m dela" }
    }
    if (@($entradas | Where-Object { [int]$_.Groups[2].Value -ge 4 }).Count -eq 0) { $erros += 'nenhuma mesa com duas strings (4 cabos) para provar a entrada unica' }
    $lances1 = @([regex]::Matches($conf1, 'ROTA_LANCE handle=(\S+) string=\S+ para=(\S+) polaridade=\S+ de=\S+ ate=(-?[\d.]+),(-?[\d.]+) fim'))
    if ($lances1.Count -ne 24) { $erros += "esperava 24 lances CC (12 strings), achei $($lances1.Count)" }
    $cabos1 = @([regex]::Matches($conf1, 'ROTA_CABO rota=DirectCurrent vertices=\d+ fora=(\d+) abaixo=([\d.]+) acima=([\d.]+)'))
    foreach ($c in $cabos1) {
        if ([int]$c.Groups[1].Value -ne 0 -or [double]::Parse($c.Groups[2].Value, $inv) -gt 0.001 -or [double]::Parse($c.Groups[3].Value, $inv) -gt 0.001) {
            $erros += 'lance CC fora da faixa do terreno (abaixo do fundo da vala ou acima da base)'
            break
        }
    }

    # ---- (12, 16) o resumo agrupado ------------------------------------------
    $res1 = Trecho 'CLIVUS_ROTACC RESUMO1' 'CLIVUS_ROTACC RECALCULAR'
    if ($res1 -notmatch 'ROTA_RESUMO_TIPO tipo=CC circuitos=12 cabos=24 ') { $erros += 'o resumo CC nao tem os 12 circuitos (24 cabos)' }
    if ($res1 -notmatch 'ROTA_RESUMO_GRUPO tipo=CC nivel=0 nome=U1 circuitos=12 cabos=24 ') { $erros += 'o resumo CC nao agrupou tudo na U1' }
    if ($res1 -notmatch 'ROTA_RESUMO_GRUPO tipo=CC nivel=1 nome=T1 circuitos=12 ') { $erros += 'o resumo CC nao tem o T1 abaixo da U1' }
    foreach ($n in 1, 2) { if ($res1 -notmatch "ROTA_RESUMO_GRUPO tipo=CC nivel=2 nome=Inversor_$n circuitos=6 cabos=12 ") { $erros += "o resumo CC nao tem o Inversor $n com 6 circuitos" } }
    if ($res1 -match 'ROTA_RESUMO_FORA tipo=CC') { $erros += 'com os inversores em campo, o resumo CC nao podia avisar fora de campo' }
    if ($res1 -notmatch 'ROTA_RESUMO_CABECALHO [^|]+\|Tag da string\|.*\|Total de cabo \(m\)\|Cabo \+ \(m\)\|Cabo \S \(m\)\|Voc na m[^|]+\|Vmp, Vmppt \(V\)\|Isc \(A\)\|Imp, Imppt \(A\)\|Corrente m\S+xima do cabo \(A\)\|Fator de corre\S+\|Corrente corrigida \(A\)\|Suporta') { $erros += 'o resumo CC nao tem a coluna da tag, o cabo + e o cabo - e as de calculo (com a corrente maxima, o fator e a corrigida)' }
    # Segunda rodada de 10/10/2026, item 7: o total da usina na grade (sempre no fim) e no CSV do Exportar.
    if ($res1 -notmatch 'ROTA_RESUMO_TOTAL tipo=CC circuitos=12 lances=24 cabos=24 metros=[\d.]+ excel=Total_da_usina_\(12_circuito\(s\)\) fim') { $erros += 'o resumo CC nao tem a linha do total da usina (12 circuitos, 24 lances)' }
    if ($res1 -notmatch 'ROTA_RESUMO_CSV_FIM Total da usina \(12 circuito\(s\)\);') { $erros += 'o CSV do resumo CC nao termina com o total da usina' }
    if ($res1 -notmatch 'ROTA_RESUMO_LINHA ') { $erros += 'o resumo CC nao tem linha de circuito com a tag' }

    # ---- (19) recalcular so o Inversor 1 ----------------------------------------
    $conf2 = Trecho 'CLIVUS_ROTACC CONFERIR2' 'CLIVUS_ROTACC RESUMO2'
    $lances2 = @([regex]::Matches($conf2, 'ROTA_LANCE handle=(\S+) string=\S+ para=(\S+) polaridade=\S+ de=\S+ ate=(-?[\d.]+),(-?[\d.]+) fim'))
    $antes2 = @($lances1 | Where-Object { $_.Groups[2].Value -eq 'Inversor_2' } | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    $depois2 = @($lances2 | Where-Object { $_.Groups[2].Value -eq 'Inversor_2' } | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    if ($antes2.Count -ne 12 -or (Compare-Object $antes2 $depois2)) { $erros += 'o Recalcular do Inversor 1 mexeu nos cabos do Inversor 2' }
    $antes1 = @($lances1 | Where-Object { $_.Groups[2].Value -eq 'Inversor_1' } | ForEach-Object { $_.Groups[1].Value })
    $novos1 = @($lances2 | Where-Object { $_.Groups[2].Value -eq 'Inversor_1' })
    if ($novos1.Count -ne 12) { $erros += "depois do Recalcular, esperava 12 lances do Inversor 1, achei $($novos1.Count)" }
    if (@($novos1 | Where-Object { $antes1 -contains $_.Groups[1].Value }).Count -gt 0) { $erros += 'o Recalcular nao refez os lances do Inversor 1' }
    foreach ($l in $novos1) {
        $x = [double]::Parse($l.Groups[3].Value, $inv); $y = [double]::Parse($l.Groups[4].Value, $inv)
        if ([math]::Abs($x - ($cx + 40)) -gt 0.01 -or [math]::Abs($y - ($cy + 5)) -gt 0.01) { $erros += 'um lance do Inversor 1 nao chega na posicao nova dele'; break }
    }
    if ((Trecho 'CLIVUS_ROTACC RECALCULAR' 'CLIVUS_ROTACC CONFERIR2') -notmatch 'Recalculados 1 inversor\(es\): Inversor 1') { $erros += 'o Recalcular nao disse que recalculou so o Inversor 1' }
    # Levado com o MOVE do AutoCAD: o Recalcular pos a base no TIN + 0,80 do lugar novo.
    if ($conf2 -notmatch 'ROTA_EQUIP tag=Inversor_1 desvio=([\d.]+) ' -or [double]::Parse($Matches[1], $inv) -gt 0.001) { $erros += 'o Inversor 1 movido com o MOVE nao foi reassentado no TIN + 0,80 pelo Recalcular' }

    # ---- (18) inversor apagado ----------------------------------------------------
    $res2 = Trecho 'CLIVUS_ROTACC RESUMO2' 'CLIVUS_ROTACC RESUMO3'
    if ($res2 -notmatch 'ROTA_RESUMO_TIPO tipo=CC circuitos=6 cabos=12 ') { $erros += 'com o Inversor 2 apagado, o resumo CC ainda conta os cabos dele' }
    if ($res2 -notmatch 'ROTA_RESUMO_FORA tipo=CC Fora de campo: Inversor 2\.') { $erros += 'com o Inversor 2 apagado, o resumo CC nao avisou' }
    if ($res2 -notmatch 'ROTA_RESUMO sumidos=0 orfaos=12') { $erros += 'os 12 cabos do Inversor 2 nao foram contados como orfaos' }
    $res3 = Trecho 'CLIVUS_ROTACC RESUMO3' 'CLIVUS_ROTACC RESUMO4'
    if ($res3 -notmatch 'ROTA_RESUMO_TIPO tipo=CC circuitos=0 cabos=0 ') { $erros += 'com os dois inversores apagados, o resumo CC ainda mostra cabo' }
    if ($res3 -notmatch 'ROTA_RESUMO_FORA tipo=CC Inversor n\S+o est\S+ em campo: n\S+o \S+ poss\S+vel mostrar o resumo \(Inversor 1, Inversor 2\)') { $erros += 'com os dois apagados, o resumo nao disse que nao da para mostrar' }

    # Cabos apagados tambem: o resumo nao sai vazio sem aviso.
    $res4 = Trecho 'CLIVUS_ROTACC RESUMO4' 'CLIVUS_ROTACC RESUMO5'
    if ($res4 -notmatch 'ROTA_RESUMO_TIPO tipo=CC circuitos=0 cabos=0 ') { $erros += 'com os cabos apagados, o resumo CC ainda mostra cabo' }
    if ($res4 -notmatch 'ROTA_RESUMO_FORA tipo=CC Ainda n\S+o postos em campo \(aloca\S+ autom\S+tica\): Inversor 1, Inversor 2\. Gere a rota CC') { $erros += 'sem cabos e sem os automaticos em campo, o resumo CC nao avisou que falta gerar a rota CC' }
    if ($res4 -match 'ROTA_RESUMO_FORA tipo=CC [^\r\n]*n\S+o est\S+ em campo') { $erros += 'os automaticos ainda nao postos foram avisados como fora de campo' }
    $res5 = Trecho 'CLIVUS_ROTACC RESUMO5' 'CLIVUS_ROTACC_FIM'
    if ($res5 -notmatch 'ROTA_RESUMO_FORA tipo=CC Inversor n\S+o est\S+ em campo: n\S+o \S+ poss\S+vel mostrar o resumo \(Inversor 2\)\. Ainda n\S+o postos em campo \(aloca\S+ autom\S+tica\): Inversor 1\.') { $erros += 'com o Inversor 2 a mao fora de campo e sem cabos, o resumo CC nao avisou os dois casos' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (rota CC: aba liberada pelos automaticos; valas atualizada e solta; $($entradas.Count) mesas com entrada unica do lado alto; resumo U1 > T1 > inversores; recalcular so do Inversor 1 com a base reassentada; inversor apagado sai do resumo; sem cabos, o resumo ainda avisa)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaRotaCc'
