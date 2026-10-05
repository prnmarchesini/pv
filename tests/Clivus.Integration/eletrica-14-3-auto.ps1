<#
    A atribuicao automatica das strings nos inversores (pedido do Renan em
    05/10/2026). Modelo de 3;4 = 7 entradas, tres inversores, o Inversor 2 ja
    com 2 strings. De cima para baixo: 7 + 5 + 7 = 19 atribuidas, as mais de
    cima, e a sobra avisada; de novo: tudo cheio, nada muda. Soltas todas, de
    baixo para cima: 21, as mais de baixo. Conferido pelo XData (o vinculo)
    e pelo DXF 420 (a cor do inversor). A varredura da numeracao nao muda.
    Usa Substituicoes-Da-Usina.
#>
function Testar-Eletrica143Auto {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-3-auto'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-3-auto: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-3-auto' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-3-auto.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-3-auto terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $erros = @()

    function Strings([string] $etapa) {
        @([regex]::Matches($t, "CLIVUS_STR etapa=$etapa h=(\S+) inv=(\S*) c420=(-?\d+) x=(-?[\d.]+) y=(-?[\d.]+) fim") | ForEach-Object {
            [pscustomobject]@{ H = $_.Groups[1].Value; Inv = $_.Groups[2].Value; C420 = $_.Groups[3].Value; Y = [double]::Parse($_.Groups[5].Value, $inv) }
        })
    }

    $laranja = '15761920'; $azul = '1999590'; $verde = '2007090'

    if ($t -notmatch 'ELETRICA recusado: varredura TopToBottom TopToBottom') { $erros += 'a varredura com os dois sentidos no mesmo eixo nao foi recusada' }
    if ($t -notmatch 'ELETRICA varredura da atribuicao TopToBottom LeftToRight; numeracao LeftToRight') { $erros += 'a varredura da atribuicao nao ficou gravada separada da numeracao' }

    # ---- de cima para baixo
    $antes = Strings 'antes'
    $manuais = @($antes | Where-Object { $_.Inv -ne '' } | ForEach-Object { $_.H })
    $cima = Strings 'cima'
    if ($antes.Count -eq 0 -or $cima.Count -ne $antes.Count) { $erros += "a atribuicao mudou o numero de strings ($($antes.Count) -> $($cima.Count))" }
    if ($manuais.Count -ne 2) { $erros += "o Inversor 2 nao comecou com 2 strings ($($manuais.Count))" }
    if ($t -notmatch 'ELETRICA atribuidas=19 sobra=(\d+) sem_posicao=0 copias=0 cheios=0 sem_modelo=0 fim' -or [int]$Matches[1] -ne $antes.Count - 21) { $erros += 'a primeira atribuicao nao deu 19 (7 + 5 + 7) com a sobra certa' }
    foreach ($par in @(@('Inversor 1', 7), @('Inversor 2', 5), @('Inversor 3', 7))) {
        if ($t -notmatch "ELETRICA ATRIBUIDO nome=""$($par[0])"" mais=$($par[1]) fim") { $erros += "o $($par[0]) nao ganhou $($par[1])" }
    }
    if ($t -notmatch 'string\(s\) livre\(s\) sobraram') { $erros += 'a sobra nao foi avisada' }

    $grupos = @($cima | Where-Object { $_.Inv -ne '' } | Group-Object Inv)
    if ($grupos.Count -ne 3 -or @($grupos | Where-Object { $_.Count -ne 7 }).Count -gt 0) { $erros += 'os tres inversores nao ficaram com 7 strings cada' }
    foreach ($g in $grupos) {
        $cores = @($g.Group | ForEach-Object { $_.C420 } | Sort-Object -Unique)
        if ($cores.Count -ne 1) { $erros += "strings do mesmo inversor com cores diferentes: $($cores -join ',')" }
    }
    $porCor = @{}
    foreach ($g in $grupos) { $porCor[$g.Group[0].C420] = $g.Group }
    if (-not ($porCor.ContainsKey($laranja) -and $porCor.ContainsKey($azul) -and $porCor.ContainsKey($verde))) { $erros += "as strings nao tem as cores dos inversores: $($porCor.Keys -join ',')" }
    if (@($cima | Where-Object { $_.Inv -eq '' -and $_.C420 -ne '-1' }).Count -gt 0) { $erros += 'string livre pintada' }
    if (@($porCor[$azul] | Where-Object { $manuais -contains $_.H }).Count -ne 2) { $erros += 'as 2 do Inversor 2 mudaram de dono' }

    $auto = @($cima | Where-Object { $_.Inv -ne '' -and $manuais -notcontains $_.H })
    $livres = @($cima | Where-Object { $_.Inv -eq '' })
    if ($auto.Count -gt 0 -and $livres.Count -gt 0) {
        $minAuto = ($auto | Measure-Object Y -Minimum).Minimum
        $maxLivre = ($livres | Measure-Object Y -Maximum).Maximum
        if ($minAuto -lt $maxLivre - 0.5) { $erros += "de cima para baixo pegou string de baixo (atribuida em y=$minAuto, livre em y=$maxLivre)" }
    }
    if ($porCor.ContainsKey($laranja) -and $porCor.ContainsKey($verde)) {
        if ((($porCor[$laranja] | Measure-Object Y -Minimum).Minimum) -lt (($porCor[$verde] | Measure-Object Y -Maximum).Maximum) - 0.5) { $erros += 'o Inversor 1 nao ficou acima do Inversor 3' }
    }

    # ---- de novo: todos cheios
    $segunda = $t.Substring(0, $t.IndexOf('CLIVUS_SEGUNDA_FIM'))
    $segunda = $segunda.Substring($segunda.LastIndexOf('CLIVUS_ETAPA_FIM cima'))
    if ($segunda -notmatch 'ELETRICA atribuidas=0 sobra=\d+ sem_posicao=0 copias=0 cheios=3 sem_modelo=0 fim') { $erros += 'a segunda atribuicao (tudo cheio) nao pulou os 3 cheios sem mexer' }

    # ---- soltas todas, de baixo para cima
    $baixo = Strings 'baixo'
    if ($t -notmatch 'ELETRICA atribuidas=21 sobra=\d+ sem_posicao=0 copias=0 cheios=0 sem_modelo=0 fim') { $erros += 'de baixo para cima nao deu 21' }
    $autoB = @($baixo | Where-Object { $_.Inv -ne '' })
    $livresB = @($baixo | Where-Object { $_.Inv -eq '' })
    if ($autoB.Count -ne 21) { $erros += "de baixo para cima ficaram $($autoB.Count) alocadas" }
    elseif ((($autoB | Measure-Object Y -Maximum).Maximum) -gt (($livresB | Measure-Object Y -Minimum).Minimum) + 0.5) { $erros += 'de baixo para cima pegou string de cima' }
    $laranjaB = @($baixo | Where-Object { $_.C420 -eq $laranja })
    $verdeB = @($baixo | Where-Object { $_.C420 -eq $verde })
    if ($laranjaB.Count -ne 7 -or $verdeB.Count -ne 7) { $erros += 'de baixo para cima os inversores nao ficaram com 7 e as cores deles' }
    elseif ((($laranjaB | Measure-Object Y -Maximum).Maximum) -gt (($verdeB | Measure-Object Y -Minimum).Minimum) + 0.5) { $erros += 'de baixo para cima o Inversor 1 nao ficou abaixo do 3' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-3-auto: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (atribuicao automatica: 19 de cima, cheios pulados, 21 de baixo, cores pelo 420)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica143Auto'
