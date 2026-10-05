<#
    11.7: o tracado desenhado acompanha o plano de cada modulo (regra
    eletrica 5; regra sagrada 5: a cota e lida DA ENTIDADE, pelo LISP, nao do
    relatorio do plugin). Para cada vertice de cada string, e para cinco
    pontos ao longo de cada trecho, que caem em cima de uma face de modulo: a distancia ao plano
    dessa face fica entre 0 (nunca enterrado) e 8 cm (nunca voando). Todo
    vertice cai em cima de um modulo, e cada string tem pelo menos um vertice
    por modulo. Os sinais + e - (um de cada por string) tambem ficam no plano
    do modulo. Regerar por cima nao duplica, e a string ligada a inversor
    continua la.
#>
function Testar-StringDesenho {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina-Mista -Desenho $Desenho -Rotulo 'clivus-string-desenho'
    if (-not $sub) { $problemas.Add('clivus-string-desenho: nao achei o centro do terreno.'); return $false }

    $dump = Join-Path $saida 'nivel2-clivus-string-desenho.txt'
    if (Test-Path $dump) { Remove-Item $dump -Force }
    $sub['{{DUMP}}'] = $dump

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-desenho' -Script (Join-Path $PSScriptRoot 'clivus-string-desenho.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0 -or -not (Test-Path $dump)) {
        $problemas.Add("clivus-string-desenho terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $faces = [System.Collections.Generic.List[object]]::new()
    $strings = [System.Collections.Generic.List[object]]::new()
    $textos = [System.Collections.Generic.List[object]]::new()
    $atual = $null

    foreach ($linha in [IO.File]::ReadAllLines($dump)) {
        $p = $linha.Split(' ')
        switch ($p[0]) {
            'S' { $atual = [pscustomobject]@{ Id = $p[1]; Modulos = [int]$p[2]; V = [System.Collections.Generic.List[double[]]]::new() }; $strings.Add($atual) }
            'V' { $atual.V.Add([double[]]@([double]::Parse($p[1], $inv), [double]::Parse($p[2], $inv), [double]::Parse($p[3], $inv))) }
            'F' {
                $c = for ($k = 1; $k -le 12; $k++) { [double]::Parse($p[$k], $inv) }
                $faces.Add([double[]]$c)
            }
            'T' { $textos.Add([pscustomobject]@{ S = $p[1]; P = [double[]]@([double]::Parse($p[2], $inv), [double]::Parse($p[3], $inv), [double]::Parse($p[4], $inv)) }) }
        }
    }

    # Indice das faces numa grade de 2 m, pela caixa de cada uma.
    $grade = @{}
    for ($i = 0; $i -lt $faces.Count; $i++) {
        $f = $faces[$i]
        $xs = @($f[0], $f[3], $f[6], $f[9]); $ys = @($f[1], $f[4], $f[7], $f[10])
        for ($gx = [math]::Floor(($xs | Measure-Object -Minimum).Minimum / 2); $gx -le [math]::Floor(($xs | Measure-Object -Maximum).Maximum / 2); $gx++) {
            for ($gy = [math]::Floor(($ys | Measure-Object -Minimum).Minimum / 2); $gy -le [math]::Floor(($ys | Measure-Object -Maximum).Maximum / 2); $gy++) {
                $chave = "$gx,$gy"
                if (-not $grade.ContainsKey($chave)) { $grade[$chave] = [System.Collections.Generic.List[int]]::new() }
                $grade[$chave].Add($i)
            }
        }
    }

    # A distancia (em z) do ponto ao plano de cada face que o contem em planta (2 mm de folga na borda).
    function Alturas([double] $x, [double] $y, [double] $z) {
        $res = [System.Collections.Generic.List[double]]::new()
        $lista = $grade["$([math]::Floor($x / 2)),$([math]::Floor($y / 2))"]
        if (-not $lista) { return ,$res }
        foreach ($i in $lista) {
            $f = $faces[$i]
            $sinal = 0; $dentro = $true
            for ($k = 0; $k -lt 4; $k++) {
                $ax = $f[3 * $k]; $ay = $f[3 * $k + 1]; $bx = $f[3 * (($k + 1) % 4)]; $by = $f[3 * (($k + 1) % 4) + 1]
                $l = [math]::Sqrt(($bx - $ax) * ($bx - $ax) + ($by - $ay) * ($by - $ay))
                $lado = (($bx - $ax) * ($y - $ay) - ($by - $ay) * ($x - $ax)) / [math]::Max($l, 1e-9)
                if ($sinal -eq 0 -and [math]::Abs($lado) -gt 0.002) { $sinal = [math]::Sign($lado) }
                if ($sinal -ne 0 -and $lado * $sinal -lt -0.002) { $dentro = $false; break }
            }
            if (-not $dentro) { continue }
            # Plano pelas diagonais.
            $ux = $f[6] - $f[0]; $uy = $f[7] - $f[1]; $uz = $f[8] - $f[2]
            $vx = $f[9] - $f[3]; $vy = $f[10] - $f[4]; $vz = $f[11] - $f[5]
            $nx = $uy * $vz - $uz * $vy; $ny = $uz * $vx - $ux * $vz; $nz = $ux * $vy - $uy * $vx
            $cx = ($f[0] + $f[3] + $f[6] + $f[9]) / 4; $cy = ($f[1] + $f[4] + $f[7] + $f[10]) / 4; $cz = ($f[2] + $f[5] + $f[8] + $f[11]) / 4
            $pz = $cz - ($nx * ($x - $cx) + $ny * ($y - $cy)) / $nz
            $res.Add($z - $pz)
        }
        return ,$res
    }

    $erros = @()
    $t2 = @([regex]::Matches($r.Texto, 'STRING_GERAR Modelo 3 \S F\d+\.\d+, F\d+\.\d+: 2 string')).Count
    $min = 1e9; $max = -1e9; $fora = 0; $pontos = 0; $poucos = 0

    foreach ($s in $strings) {
        if ($s.V.Count -lt $s.Modulos) { $poucos++ }
        for ($k = 0; $k -lt $s.V.Count; $k++) {
            $v = $s.V[$k]
            $hs = [System.Collections.Generic.List[double]]::new()
            $hs.AddRange((Alturas $v[0] $v[1] $v[2]))
            if ($hs.Count -eq 0) { $fora++ }
            if ($k + 1 -lt $s.V.Count) {
                # Cinco pontos ao longo do trecho: uma reta que enterra ou voa
                # sobre o modulo da ponta aparece aqui, mesmo com o meio no vao.
                $w = $s.V[$k + 1]
                foreach ($q in 1..5) {
                    $f = $q / 6
                    $hs.AddRange((Alturas ($v[0] + ($w[0] - $v[0]) * $f) ($v[1] + ($w[1] - $v[1]) * $f) ($v[2] + ($w[2] - $v[2]) * $f)))
                }
            }
            foreach ($h in $hs) {
                $pontos++
                if ($h -lt $min) { $min = $h }
                if ($h -gt $max) { $max = $h }
            }
        }
    }

    $minT = 1e9; $maxT = -1e9
    foreach ($t in $textos) {
        foreach ($h in (Alturas $t.P[0] $t.P[1] $t.P[2])) { $minT = [math]::Min($minT, $h); $maxT = [math]::Max($maxT, $h) }
    }

    if ($strings.Count -lt 20 -or $t2 -lt 1) { $erros += "so $($strings.Count) string(s) desenhada(s)" }
    if ($faces.Count -lt 100) { $erros += "so $($faces.Count) face(s) lida(s)" }
    if ($fora -gt 0) { $erros += "$fora vertice(s) fora de cima de modulo" }
    if ($poucos -gt 0) { $erros += "$poucos string(s) com menos vertices que modulos" }
    if ($min -lt -0.003 -or $max -gt 0.08) { $erros += "tracado de $([math]::Round($min, 4)) a $([math]::Round($max, 4)) m do plano do modulo (fora de 0 a 8 cm)" }
    if ($textos.Count -ne 2 * $strings.Count -or @($textos | Where-Object { $_.S -eq '+' }).Count -ne $strings.Count) { $erros += "$($textos.Count) sinal(is) para $($strings.Count) string(s)" }
    if ($minT -lt -0.003 -or $maxT -gt 0.08) { $erros += "sinais de $([math]::Round($minT, 4)) a $([math]::Round($maxT, 4)) m do plano do modulo" }

    $t = $r.Texto
    if ($t -notmatch 'STRING_PRESA F1\.1 ([0-9a-f-]{36})') { $erros += 'nao prendeu a string da F1.1' }
    else {
        $presa = $Matches[1]
        if ($t -notmatch 'CLIVUS_REGERAR antes=(\d+) textos=(\d+) depois=(\d+) textos=(\d+) presas= ?([0-9a-f-]{36})?') { $erros += 'nao li a contagem depois de regerar' }
        elseif ($Matches[1] -ne $Matches[3] -or $Matches[2] -ne $Matches[4]) { $erros += "regerar mudou a contagem: $($Matches[0])" }
        elseif ($Matches[5] -ne $presa) { $erros += "a string presa ($presa) nao ficou: $($Matches[0])" }
        if ($t -notmatch 'STRING_GERAR Aviso: (F[\d.]+, )?F1\.1(, F[\d.]+)?: tem string ligada a inversor') { $erros += 'regerar nao avisou a mesa com string ligada a inversor' }
        if ($t -notmatch 'STRING_GERAR \d+ string\(s\) livre\(s\) que j\S+ estavam nessas mesas foram substitu') { $erros += 'regerar nao disse que substituiu as livres' }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-desenho: $($erros -join '; '). Veja $($r.Saida) e $dump")
        return $false
    }

    Write-Host ("  (strings 11.7: $($strings.Count) strings, $pontos pontos sobre modulos de {0:0.000} a {1:0.000} m do plano; sinais de {2:0.000} a {3:0.000}; regerar nao duplica)" -f $min, $max, $minT, $maxT) -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringDesenho'
