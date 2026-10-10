<#
    Local dos inversores (pedidos do Renan em 10/10/2026):
      - Area: os inversores escolhidos vao para dentro do retangulo que o
        usuario desenhou (sala, skid), um ao lado do outro, sem sobrepor, na
        cota do terreno + 0,80; o retangulo vira Polyline3d fechada no TIN;
      - Automatico: o Gerar da rota CC poe o inversor (que nao estava em
        campo) ao lado da vala, no ponto de menor cabo CC das strings dele, e
        traca os lances (2 por string), todos medidos contra o TIN;
      - movido a mao, o Gerar refaz a rota da posicao nova (nao recoloca);
      - Recolocar automaticos devolve o inversor ao ponto de menor cabo.
    A usina e a de Substituicoes-Da-Usina (100 x 100 m no centro do terreno);
    a vala CC corre norte-sul 5 m a leste da borda.
#>
function Testar-EletricaLocal {
    param([string] $Desenho)

    $rotulo = 'clivus-eletrica-local'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    $sub = @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{L1}}' = (P -50 -50); '{{L2}}' = (P -50 50); '{{LADO}}' = (P 0 0)
        '{{S1}}' = (P 62 -10); '{{S2}}' = (P 72 -10); '{{S3}}' = (P 72 -6); '{{S4}}' = (P 62 -6)
        '{{V1}}' = (P 55 -60); '{{V2}}' = (P 55 60)
        '{{M}}' = (P 60 5)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-eletrica-local.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_LOCAL_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Equip([string] $etapa) {
        @([regex]::Matches($t, "CLIVUS_LOCAL_EQUIP etapa=$etapa tag=(\S+) x=(-?[\d.]+) y=(-?[\d.]+) z=(-?[\d.]+) fim") | ForEach-Object {
            [pscustomobject]@{ Tag = $_.Groups[1].Value; X = [double]::Parse($_.Groups[2].Value, $inv); Y = [double]::Parse($_.Groups[3].Value, $inv); Z = [double]::Parse($_.Groups[4].Value, $inv) }
        })
    }

    # ---- (1) area ---------------------------------------------------------
    if ($t -notmatch 'CLIVUS_LOCAL_SALA tipo=POLYLINE flags=(\d+) fim' -or (([int]$Matches[1]) -band 9) -ne 9) { $erros += 'o retangulo da area nao virou Polyline3d fechada' }
    $area = @(Equip 'area' | Where-Object { $_.Tag -in 'Inversor_2', 'Inversor_3', 'Inversor_4' })
    if ($area.Count -ne 3) { $erros += "esperava os inversores 2, 3 e 4 em campo na area, achei $($area.Count)" }
    foreach ($e in $area) {
        if ($e.X -lt $cx + 62 -or $e.X -gt $cx + 72 -or $e.Y -lt $cy - 10 -or $e.Y -gt $cy - 6) { $erros += "$($e.Tag) fora da area ($($e.X), $($e.Y))" }
    }
    for ($i = 0; $i -lt $area.Count; $i++) {
        for ($j = $i + 1; $j -lt $area.Count; $j++) {
            $d = [math]::Sqrt([math]::Pow($area[$i].X - $area[$j].X, 2) + [math]::Pow($area[$i].Y - $area[$j].Y, 2))
            if ($d -lt 0.9) { $erros += "$($area[$i].Tag) e $($area[$j].Tag) um em cima do outro ($d m)" }
        }
    }
    if ($t -notmatch 'LOCAL 3 de 3 inversor\(es\) postos na') { $erros += 'o comando nao disse que pos os 3 na area' }

    # Cota: cada relatorio do comando (terreno e base) contra a entidade: base = terreno + 0,80.
    $relatorios = @([regex]::Matches($t, 'EQUIPAMENTO (Inversor \d) em campo: terreno a (-?[\d.,]+) m, base a (-?[\d.,]+) m') | ForEach-Object {
        [pscustomobject]@{ Tag = $_.Groups[1].Value; Chao = [double]::Parse($_.Groups[2].Value.Replace(',', '.'), $inv); Base = [double]::Parse($_.Groups[3].Value.Replace(',', '.'), $inv) }
    })
    if ($relatorios.Count -lt 4) { $erros += "esperava ao menos 4 relatorios de equipamento em campo, achei $($relatorios.Count)" }
    foreach ($rel in $relatorios) { if ([math]::Abs($rel.Base - $rel.Chao - 0.8) -gt 0.001) { $erros += "$($rel.Tag): base nao esta 0,80 acima do terreno" } }
    foreach ($e in $area) {
        $rel = $relatorios | Where-Object { $_.Tag -eq ($e.Tag -replace '_', ' ') } | Select-Object -First 1
        if (-not $rel -or [math]::Abs($rel.Base - $e.Z) -gt 0.001) { $erros += "$($e.Tag): a cota da entidade nao e a base do relatorio" }
    }

    # ---- (2) automatico ---------------------------------------------------
    $auto = @(Equip 'auto' | Where-Object { $_.Tag -eq 'Inversor_1' })
    if ($auto.Count -ne 1) { $erros += 'o Gerar CC nao pos o Inversor 1 (automatico) em campo' }
    else {
        $dx = [math]::Abs($auto[0].X - ($cx + 55))
        if ($dx -lt 0.5 -or $dx -gt 2.5) { $erros += "o Inversor 1 nao ficou ao lado da vala (a $dx m dela)" }
        if ($auto[0].Y -lt $cy - 50 -or $auto[0].Y -gt $cy + 50) { $erros += "o Inversor 1 ficou fora da altura da usina (y $($auto[0].Y))" }
        $rel = $relatorios | Where-Object { $_.Tag -eq 'Inversor 1' } | Select-Object -First 1
        if (-not $rel -or [math]::Abs($rel.Base - $auto[0].Z) -gt 0.001) { $erros += 'Inversor 1: a cota da entidade nao e a base do relatorio' }
    }
    $cabos = @([regex]::Matches($t, 'ROTA_CABO rota=DirectCurrent vertices=\d+ fora=(\d+) abaixo=([\d.]+) acima=([\d.]+)'))
    if ($cabos.Count -ne 12) { $erros += "esperava 12 lances CC (6 strings), achei $($cabos.Count)" }
    foreach ($c in $cabos) {
        if ([int]$c.Groups[1].Value -ne 0 -or [double]::Parse($c.Groups[2].Value, $inv) -gt 0.001 -or [double]::Parse($c.Groups[3].Value, $inv) -gt 0.001) {
            $erros += 'lance CC fora da faixa do terreno (abaixo do fundo da vala ou acima da base)'
            break
        }
    }
    if ($t -notmatch 'Inversor 1 posto ao lado da vala, no ponto de menor cabo CC das 6 string') { $erros += 'o Gerar nao disse que pos o Inversor 1 ao lado da vala' }

    # ---- (3) movido e (4) recolocado --------------------------------------
    $movido = @(Equip 'movido' | Where-Object { $_.Tag -eq 'Inversor_1' })
    if ($movido.Count -ne 1 -or [math]::Abs($movido[0].X - ($cx + 60)) -gt 0.01 -or [math]::Abs($movido[0].Y - ($cy + 5)) -gt 0.01) { $erros += 'movido a mao, o Gerar tirou o Inversor 1 do lugar' }
    $de = $t.LastIndexOf('CLIVUS_LOCAL MOVER')
    $ate = $t.LastIndexOf('CLIVUS_LOCAL RECOLOCAR')
    if ($de -lt 0 -or $ate -le $de) { $erros += 'nao achei os marcadores MOVER e RECOLOCAR' }
    else {
        $trecho = $t.Substring($de, $ate - $de)
        if ($trecho -match 'Inversor 1 posto ao lado da vala') { $erros += 'o Gerar depois de mover recolocou o Inversor 1' }
        if ($trecho -notmatch 'CC: 12 lance\(s\) desenhado') { $erros += 'o Gerar depois de mover nao refez os 12 lances' }
    }
    $recolocado = @(Equip 'recolocado' | Where-Object { $_.Tag -eq 'Inversor_1' })
    if ($auto.Count -eq 1 -and ($recolocado.Count -ne 1 -or [math]::Abs($recolocado[0].X - $auto[0].X) -gt 0.01 -or [math]::Abs($recolocado[0].Y - $auto[0].Y) -gt 0.01)) {
        $erros += 'o Recolocar nao devolveu o Inversor 1 ao ponto de menor cabo'
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (local: 3 inversores na area, no TIN + 0,80; automatico ao lado da vala com 12 lances no TIN; movido fica; recolocar volta)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaLocal'
