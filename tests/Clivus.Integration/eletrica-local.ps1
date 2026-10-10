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
    A usina tem 60 x 60 m no centro do terreno (longe da borda do TIN, que
    tem buracos); a vala CC corre norte-sul 5 m a leste dela, e a area fica
    entre a vala e a borda do terreno.
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
        '{{A1}}' = (P -30 -30); '{{A2}}' = (P 30 -30); '{{A3}}' = (P 30 30); '{{A4}}' = (P -30 30)
        '{{L1}}' = (P -30 -30); '{{L2}}' = (P -30 30); '{{LADO}}' = (P 0 0)
        '{{S1}}' = (P 42 -10); '{{S2}}' = (P 52 -10); '{{S3}}' = (P 52 -6); '{{S4}}' = (P 42 -6)
        '{{V1}}' = (P 35 -35); '{{V2}}' = (P 35 35)
        '{{M}}' = (P 40 5)
        '{{T1}}' = (P 42 2); '{{T2}}' = (P 46.2 2); '{{T3}}' = (P 46.2 8); '{{T4}}' = (P 42 8)
        '{{P1}}' = (P 48 -3); '{{P2}}' = (P 50 -3); '{{P3}}' = (P 50 -2); '{{P4}}' = (P 48 -2)
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
    # A pegada real de cada caixa (do comando de conferencia, logo depois da area): inteira dentro, sem sobrepor; base no TIN + 0,80.
    $de = $t.LastIndexOf('CLIVUS_LOCAL CONFERIR_AREA')
    $trechoArea = if ($de -ge 0) { $t.Substring($de, $t.IndexOf('ROTA_CONFERIR_FIM', $de) - $de) } else { '' }
    $caixas = @([regex]::Matches($trechoArea, 'ROTA_EQUIP tag=(Inversor_[234]) desvio=([\d.]+) minx=(-?[\d.]+) miny=(-?[\d.]+) maxx=(-?[\d.]+) maxy=(-?[\d.]+) fim') | ForEach-Object {
        [pscustomobject]@{ Tag = $_.Groups[1].Value; Desvio = [double]::Parse($_.Groups[2].Value, $inv)
            MinX = [double]::Parse($_.Groups[3].Value, $inv); MinY = [double]::Parse($_.Groups[4].Value, $inv)
            MaxX = [double]::Parse($_.Groups[5].Value, $inv); MaxY = [double]::Parse($_.Groups[6].Value, $inv) }
    })
    if ($caixas.Count -ne 3) { $erros += "a conferencia nao achou as 3 caixas da area (achou $($caixas.Count))" }
    foreach ($c in $caixas) {
        if ($c.MinX -lt $cx + 42 - 0.001 -or $c.MaxX -gt $cx + 52 + 0.001 -or $c.MinY -lt $cy - 10 - 0.001 -or $c.MaxY -gt $cy - 6 + 0.001) { $erros += "$($c.Tag): a caixa sai da area" }
        if ($c.Desvio -gt 0.001) { $erros += "$($c.Tag): a base nao esta no TIN + 0,80 (desvio $($c.Desvio) m)" }
    }
    for ($i = 0; $i -lt $caixas.Count; $i++) {
        for ($j = $i + 1; $j -lt $caixas.Count; $j++) {
            $a = $caixas[$i]; $b = $caixas[$j]
            if ($a.MinX -lt $b.MaxX - 0.001 -and $b.MinX -lt $a.MaxX - 0.001 -and $a.MinY -lt $b.MaxY - 0.001 -and $b.MinY -lt $a.MaxY - 0.001) { $erros += "$($a.Tag) e $($b.Tag) se sobrepoem" }
        }
    }
    if ($trechoArea -notmatch 'ROTA_AREA nome=\S+ fechada=True fora=0 desvio=([\d.]+) fim' -or [double]::Parse($Matches[1], $inv) -gt 0.001) { $erros += 'a polilinha da area nao esta no TIN' }
    if ($t -notmatch 'LOCAL 3 de 3 inversor\(es\) postos na') { $erros += 'o comando nao disse que pos os 3 na area' }
    if ($t -match 'fora do terreno ficaram com a cota') { $erros += 'algum ponto da area ou da vala caiu fora do terreno (o desenho do teste tem que ficar dentro do TIN)' }

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
        $dx = [math]::Abs($auto[0].X - ($cx + 35))
        if ($dx -lt 0.5 -or $dx -gt 2.5) { $erros += "o Inversor 1 nao ficou ao lado da vala (a $dx m dela)" }
        if ($auto[0].Y -lt $cy - 30 -or $auto[0].Y -gt $cy + 30) { $erros += "o Inversor 1 ficou fora da altura da usina (y $($auto[0].Y))" }
        $rel = $relatorios | Where-Object { $_.Tag -eq 'Inversor 1' } | Select-Object -First 1
        if (-not $rel -or [math]::Abs($rel.Base - $auto[0].Z) -gt 0.001) { $erros += 'Inversor 1: a cota da entidade nao e a base do relatorio' }
    }
    $deGerar = $t.IndexOf('CLIVUS_LOCAL GERAR1')
    $ateMover = $t.IndexOf('CLIVUS_LOCAL MOVER')
    $trechoGerar = if ($deGerar -ge 0 -and $ateMover -gt $deGerar) { $t.Substring($deGerar, $ateMover - $deGerar) } else { $t }
    $cabos = @([regex]::Matches($trechoGerar, 'ROTA_CABO rota=DirectCurrent vertices=\d+ fora=(\d+) abaixo=([\d.]+) acima=([\d.]+)'))
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
    if ($movido.Count -ne 1 -or [math]::Abs($movido[0].X - ($cx + 40)) -gt 0.01 -or [math]::Abs($movido[0].Y - ($cy + 5)) -gt 0.01) { $erros += 'movido a mao, o Gerar tirou o Inversor 1 do lugar' }
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


    # ---- melhorias de 10/10/2026 -------------------------------------------
    function Trecho([string] $de, [string] $ate) {
        # O marcador inteiro (PEQUENA nao casa com PEQUENA_FIM), a primeira vez.
        $m = [regex]::Match($t, [regex]::Escape($de) + '(?![_\w])')
        if (-not $m.Success) { return '' }
        $a = $m.Index
        $b = $t.IndexOf($ate, $a + $de.Length)
        if ($b -lt 0) { $b = $t.Length }
        return $t.Substring($a, $b - $a)
    }
    function Linhas([string] $trecho) {
        $l = @{}
        foreach ($m in [regex]::Matches($trecho, 'ELETRICA LOCAL_LINHA nome=(\S+) local=(\S+) botao=(\S+) ver=(\S+) limite=(\S+) fim')) {
            $l[$m.Groups[1].Value] = [pscustomobject]@{ Local = $m.Groups[2].Value; Botao = $m.Groups[3].Value; Ver = $m.Groups[4].Value; Limite = $m.Groups[5].Value }
        }
        return $l
    }

    # (1) o nome dado ao criar a area
    if ($t -notmatch 'LOCAL 3 de 3 inversor\(es\) postos na Sala 1') { $erros += 'a area nova nao ficou com o nome dado (Sala 1)' }

    # (5) renomear: grava; nome vazio e recusado; a coluna Local mostra o nome novo
    $ren = Trecho 'CLIVUS_LOCAL RENOMEAR' 'CLIVUS_LOCAL AREA2'
    if ($ren -notmatch 'ELETRICA janela local renomear: Sala 1 agora se chama Skid norte') { $erros += 'a area nao foi renomeada' }
    if ($ren -notmatch 'ELETRICA janela local recusado: .*vazio') { $erros += 'o nome vazio da area nao foi recusado' }
    $l5 = Linhas $ren
    foreach ($n in 'Inversor_2', 'Inversor_3', 'Inversor_4') {
        if (-not $l5[$n] -or $l5[$n].Local -ne 'Skid_norte' -or $l5[$n].Botao -ne 'Move' -or $l5[$n].Ver -ne 'True') { $erros += "${n}: a linha nao mostra a area renomeada com Mover e Ver em campo" }
    }
    # Item 19: o automatico ja posto pela rota pode ser movido (Mover e Ver em campo); a coluna continua Auto.
    if (-not $l5['Inversor_1'] -or $l5['Inversor_1'].Local -ne 'Auto' -or $l5['Inversor_1'].Botao -ne 'Move' -or $l5['Inversor_1'].Ver -ne 'True') { $erros += 'Inversor 1 (automatico, em campo) nao mostra Auto com Mover e Ver em campo' }

    # (6) item 2 e 3: 9 numa area em pe (comeca pelo lado curto) em grade, o 10o sozinho na vaga livre
    if ($t -notmatch 'LOCAL 9 de 9 inversor\(es\) postos na Sala 2') { $erros += 'os 9 nao couberam todos na Sala 2' }
    if ((Trecho 'CLIVUS_LOCAL ULTIMO' 'CLIVUS_LOCAL CONFERIR_AREA2') -notmatch 'LOCAL 1 de 1 inversor\(es\) postos na Sala 2') { $erros += 'o ultimo, sozinho, nao entrou na Sala 2' }
    $conf2 = Trecho 'CLIVUS_LOCAL CONFERIR_AREA2' 'ROTA_CONFERIR_FIM'
    $sala2 = @([regex]::Matches($conf2, 'ROTA_EQUIP tag=Inversor_(\d+) desvio=([\d.]+) minx=(-?[\d.]+) miny=(-?[\d.]+) maxx=(-?[\d.]+) maxy=(-?[\d.]+) fim') | Where-Object { [int]$_.Groups[1].Value -ge 5 } | ForEach-Object {
        [pscustomobject]@{ Tag = "Inversor_$($_.Groups[1].Value)"; Desvio = [double]::Parse($_.Groups[2].Value, $inv)
            MinX = [double]::Parse($_.Groups[3].Value, $inv); MinY = [double]::Parse($_.Groups[4].Value, $inv)
            MaxX = [double]::Parse($_.Groups[5].Value, $inv); MaxY = [double]::Parse($_.Groups[6].Value, $inv) }
    })
    if ($sala2.Count -ne 10) { $erros += "esperava 10 caixas na Sala 2, achei $($sala2.Count)" }
    foreach ($c in $sala2) {
        if ($c.MinX -lt $cx + 42 - 0.001 -or $c.MaxX -gt $cx + 46.2 + 0.001 -or $c.MinY -lt $cy + 2 - 0.001 -or $c.MaxY -gt $cy + 8 + 0.001) { $erros += "$($c.Tag): a caixa sai da Sala 2" }
        if ($c.Desvio -gt 0.001) { $erros += "$($c.Tag): a base nao esta no TIN + 0,80 (desvio $($c.Desvio) m)" }
    }
    for ($i = 0; $i -lt $sala2.Count; $i++) {
        for ($j = $i + 1; $j -lt $sala2.Count; $j++) {
            $a = $sala2[$i]; $b = $sala2[$j]
            if ($a.MinX -lt $b.MaxX - 0.001 -and $b.MinX -lt $a.MaxX - 0.001 -and $a.MinY -lt $b.MaxY - 0.001 -and $b.MinY -lt $a.MaxY - 0.001) { $erros += "$($a.Tag) e $($b.Tag) se sobrepoem na Sala 2" }
        }
    }
    if (@($sala2 | ForEach-Object { [math]::Round($_.MinX, 2) } | Sort-Object -Unique).Count -lt 2 -or @($sala2 | ForEach-Object { [math]::Round($_.MinY, 2) } | Sort-Object -Unique).Count -lt 2) { $erros += 'a Sala 2 nao ficou em grade (linhas e colunas)' }

    # (7) area pequena: cabe 1 de 2, o aviso diz quantos; o que nao coube fica onde estava, coerente
    $peq = Trecho 'CLIVUS_LOCAL PEQUENA' 'CLIVUS_LOCAL PEQUENA_FIM'
    if ($peq -notmatch 'LOCAL 1 de 2 inversor\(es\) postos' -or $peq -notmatch 'pequena: couberam 1 de 2; ficaram de fora: Inversor 3') { $erros += 'a area pequena nao avisou quantos couberam' }

    # (8) automatico: o retangulo sai do campo, o Por em campo e recusado, a linha mostra Alocacao automatica
    $aut = Trecho 'CLIVUS_LOCAL AUTOMATICO' 'CLIVUS_LOCAL AUTOMATICO_FIM'
    if ($aut -notmatch '1 ret.ngulo\(s\) que estavam em campo foram apagados') { $erros += 'Automatico nao apagou o retangulo do Inversor 4' }
    if ($aut -notmatch 'EQUIPAMENTO Inversor 4 tem aloca..o autom.tica: quem o p.e em campo') { $erros += 'o Por em campo do inversor automatico nao foi recusado' }
    if (@(Equip 'automatico' | Where-Object { $_.Tag -eq 'Inversor_4' }).Count -ne 0) { $erros += 'o Inversor 4 automatico continua em campo' }
    $l8 = Linhas $aut
    if (-not $l8['Inversor_4'] -or $l8['Inversor_4'].Local -ne 'Auto' -or $l8['Inversor_4'].Botao -ne 'Automatic' -or $l8['Inversor_4'].Ver -ne 'False') { $erros += 'a linha do Inversor 4 automatico nao ficou Auto / Alocacao automatica' }
    if (-not $l8['Inversor_2'] -or $l8['Inversor_2'].Local -notmatch '^.{1,3}rea_3$') { $erros += 'o Inversor 2 nao ficou na area pequena' }
    if (-not $l8['Inversor_3'] -or $l8['Inversor_3'].Local -ne 'Skid_norte' -or $l8['Inversor_3'].Botao -ne 'Move') { $erros += 'o Inversor 3 (nao coube) nao ficou coerente na Skid norte' }
    foreach ($n in $l8.Keys) {
        $x = $l8[$n]
        if ($x.Local -ne '-' -and $x.Local -ne 'Auto' -and $x.Botao -ne 'Move') { $erros += "${n}: coluna Local '$($x.Local)' com botao $($x.Botao) (o erro do print)" }
    }

    # (9) Ver em campo: o retangulo fica selecionado; fora do campo, avisa
    $ver = Trecho 'CLIVUS_LOCAL VER' 'CLIVUS_LOCAL VER_FIM'
    if ($ver -notmatch 'INVERSOR Inversor 5 em campo, real.ado' -or $ver -notmatch 'CLIVUS_LOCAL_VER selecionados=1 fim') { $erros += 'Ver em campo nao selecionou o Inversor 5' }
    if ($ver -notmatch 'INVERSOR Inversor 4 n.o est. em campo') { $erros += 'Ver em campo de inversor fora do campo nao avisou' }

    # (10) Repartir pela fila: a soma dos limites passa a bater com as strings uteis
    $rep = Trecho 'CLIVUS_LOCAL REPARTIR' 'CLIVUS_LOCAL REPARTIR_FIM'
    $somas = @([regex]::Matches($rep, 'ELETRICA LOCAL_SOMA bate=(\S+) \[Limites: (\d+) de (\d+) strings') | ForEach-Object { [pscustomobject]@{ Bate = $_.Groups[1].Value; Soma = [int]$_.Groups[2].Value; Uteis = [int]$_.Groups[3].Value } })
    if ($somas.Count -ne 2) { $erros += 'nao achei a soma dos limites antes e depois do Repartir' }
    else {
        if ($somas[0].Bate -ne 'False' -or $somas[0].Soma -ne 280) { $erros += "antes do Repartir a soma devia ser 280 (14 x 20) e nao bater: $($somas[0].Soma)" }
        if ($somas[1].Bate -ne 'True' -or $somas[1].Soma -ne $somas[1].Uteis) { $erros += "depois do Repartir a soma dos limites nao bate: $($somas[1].Soma) de $($somas[1].Uteis)" }
    }
    if ($rep -notmatch 'repartidas em 14 inversor\(es\) na ordem do Distribuir') { $erros += 'o Repartir nao disse que seguiu a ordem do Distribuir' }

    # (11) pre-tag: uma por string alocada no Distribuir, o I do inversor, deitada, na cota dos modulos; o Soltar e a Numeracao apagam
    function Pretags([string] $etapa) {
        @([regex]::Matches($t, "CLIVUS_PRETAG etapa=$etapa texto=(\S+) tipo=(\S+) rot=(-?[\d.]+) z=(-?[\d.]+) fim") | ForEach-Object {
            [pscustomobject]@{ Texto = $_.Groups[1].Value; Tipo = $_.Groups[2].Value; Rot = [double]::Parse($_.Groups[3].Value, $inv); Z = [double]::Parse($_.Groups[4].Value, $inv) }
        })
    }
    $dist = @(Pretags 'distribuido')
    if ($t -notmatch '(\d+) string\(s\) atribu.da\(s\)' -or $dist.Count -ne [int]$Matches[1]) { $erros += "esperava uma pre-tag por string distribuida, achei $($dist.Count)" }
    if (@($dist | Where-Object { $_.Tipo -ne 'StringPreTag' -or $_.Texto -notmatch '^I([1-9]|1[0-4])$' }).Count -gt 0) { $erros += 'pre-tag com texto fora de I1..I14 ou sem a marca' }
    if (@($dist | Where-Object { [math]::Abs([math]::Sin($_.Rot)) -gt 0.5 }).Count -gt 0) { $erros += 'pre-tag em pe (o texto segue o eixo da mesa)' }
    if ($t -match 'CLIVUS_ZSTRINGS min=(-?[\d.]+) max=(-?[\d.]+) fim') {
        $zmin = [double]::Parse($Matches[1], $inv); $zmax = [double]::Parse($Matches[2], $inv)
        if (@($dist | Where-Object { $_.Z -lt $zmin - 1 -or $_.Z -gt $zmax + 4 }).Count -gt 0) { $erros += 'pre-tag fora da cota dos modulos (Z do clique ou zero)' }
    } else { $erros += 'nao achei a cota das strings' }
    $solto = @(Pretags 'solto')
    $doUm = @($dist | Where-Object { $_.Texto -eq 'I1' }).Count
    if ($doUm -eq 0 -or @($solto | Where-Object { $_.Texto -eq 'I1' }).Count -ne 0 -or $solto.Count -ne $dist.Count - $doUm) { $erros += 'o Soltar do Inversor 1 nao apagou so as pre-tags dele' }
    if (@(Pretags 'numerado').Count -ne 0) { $erros += 'a Numeracao nao apagou as pre-tags' }
    if (([regex]::Matches($t, 'CLIVUS_TAGREAL etapa=numerado fim')).Count -eq 0) { $erros += 'a Numeracao nao desenhou as tags de verdade' }
    # Apagar as tags (a tag nova vazia) nao deixa sem pre-tag a string que continua num inversor; a do I1 (solta) nao volta.
    $apagadas = @(Pretags 'tagsapagadas')
    if ($apagadas.Count -ne $solto.Count -or @($apagadas | Where-Object { $_.Texto -eq 'I1' }).Count -ne 0) { $erros += "apagadas as tags, esperava $($solto.Count) pre-tags (as strings que continuam nos inversores), achei $($apagadas.Count)" }
    if (([regex]::Matches($t, 'CLIVUS_TAGREAL etapa=tagsapagadas fim')).Count -ne 0) { $erros += 'o Apagar tags deixou tag de verdade' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (local: 3 inversores na area, no TIN + 0,80; automatico ao lado da vala com 12 lances no TIN; movido fica; recolocar volta; 9 + 1 em grade na area em pe; area pequena avisada; automatico apaga e trava; Ver em campo; Repartir bate a soma; pre-tag)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaLocal'
