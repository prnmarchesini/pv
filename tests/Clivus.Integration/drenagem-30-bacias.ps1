<#
    Passo 30 (Memoria > Bacias): area de 100 x 100 m e uma canaleta
    atravessando; bacias pelas areas de implantacao. Confere o resultado
    gravado (a soma das bacias e da area sem destino da a area analisada,
    perto dos 10.000 m2), o desenho (um rotulo por mancha, borda no TIN + 5 cm,
    hatch nunca abaixo do terreno), o ajuste manual (marca a bacia como
    manual; polilinha do Clivus e recusada) e o apagar.
#>
function Testar-DrenagemBacias {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-bacias'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("${rotulo}: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-bacias.scr') -Substituicoes @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{C1}}' = (P -45 -20); '{{C2}}' = (P 45 -25)
        '{{M1}}' = (P -45 30); '{{M2}}' = (P -25 30); '{{M3}}' = (P -25 45); '{{M4}}' = (P -45 45)
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_DRNBAC FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Etapa([int] $n) {
        $a = $t.IndexOf("CLIVUS_DRNBAC ETAPA$n")
        if ($a -lt 0) { return '' }
        $b = $t.IndexOf('CLIVUS_DRNBAC ETAPA' + ($n + 1), $a + 10)
        if ($b -lt 0) { $b = $t.IndexOf('CLIVUS_DRNBAC FIM', $a) }
        return $t.Substring($a, $b - $a)
    }

    function Bacias([string] $trecho) {
        if ($trecho -notmatch 'DRN_BACIAS bacias=(-?\d+) sem_destino=([\d.]+) analisada=([\d.]+) hatches=(\d+) bordas=(\d+) rotulos=(\d+) borda_ok=(\d+) borda_fora=(\d+) borda_sem_terreno=(\d+) hatch_enterrado=(\d+) fim') { return $null }
        $g = $Matches
        $soma = 0.0; $manual = 0
        foreach ($m in [regex]::Matches($trecho, 'DRN_BACIA canaleta=\S+ area=([\d.]+) L=([\d.]+) dh=([-\d.]+) manual=(\d) fim')) {
            $soma += [double]::Parse($m.Groups[1].Value, $inv)
            $manual += [int] $m.Groups[4].Value
        }
        return [pscustomobject]@{
            Bacias = [int] $g[1]; SemDestino = [double]::Parse($g[2], $inv); Analisada = [double]::Parse($g[3], $inv)
            Hatches = [int] $g[4]; Bordas = [int] $g[5]; Rotulos = [int] $g[6]
            Ok = [int] $g[7]; Fora = [int] $g[8]; Sem = [int] $g[9]; Enterrado = [int] $g[10]
            Soma = $soma; Manual = $manual
        }
    }

    $b1 = Bacias (Etapa 1)
    if ($null -eq $b1) { $erros += 'etapa 1: o conferir das bacias nao respondeu' }
    else {
        if ($b1.Bacias -ne 1) { $erros += "etapa 1: $($b1.Bacias) bacia(s) (esperado 1, da canaleta)" }
        if ([math]::Abs($b1.Analisada - 10000) -gt 400) { $erros += "etapa 1: area analisada $($b1.Analisada) m2 numa area de 100 x 100 m" }
        if ([math]::Abs($b1.Soma + $b1.SemDestino - $b1.Analisada) -gt 0.01) { $erros += "etapa 1: bacias ($($b1.Soma)) + sem destino ($($b1.SemDestino)) nao dao a area analisada ($($b1.Analisada))" }
        if ($b1.Soma -le 0) { $erros += 'etapa 1: a bacia da canaleta ficou sem area' }
        if ($b1.Hatches -lt 1 -or $b1.Rotulos -ne $b1.Hatches -or $b1.Bordas -lt $b1.Hatches) { $erros += "etapa 1: $($b1.Hatches) hatch(es), $($b1.Rotulos) rotulo(s), $($b1.Bordas) borda(s)" }
        if ($b1.Fora -ne 0) { $erros += "etapa 1: $($b1.Fora) ponto(s) da borda fora da cota do TIN + 5 cm" }
        if ($b1.Enterrado -ne 0) { $erros += "etapa 1: $($b1.Enterrado) hatch(es) abaixo do terreno" }
    }
    if ((Etapa 1) -notmatch 'BACIAS 1 bacia\(s\) em') { $erros += 'etapa 1: o calcular nao relatou a bacia' }

    $b2 = Bacias (Etapa 2)
    if ($null -eq $b2) { $erros += 'etapa 2: o conferir das bacias nao respondeu' }
    elseif ($b2.Manual -ne 1) { $erros += 'etapa 2: o ajuste manual nao marcou a bacia' }
    if ((Etapa 2) -notmatch 'BACIAS Essa polilinha . do Clivus') { $erros += 'etapa 2: a polilinha da area (do Clivus) foi aceita como ajuste manual' }
    if ((Etapa 2) -notmatch 'BACIAS A .rea escolhida vai para Canaleta 1') { $erros += 'etapa 2: o ajuste manual nao foi feito' }

    $b3 = Bacias (Etapa 3)
    if ($null -eq $b3 -or $b3.Hatches + $b3.Bordas + $b3.Rotulos -ne 0) { $erros += 'etapa 3: depois de apagar ficou desenho de bacia' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem bacias: $([math]::Round($b1.Soma)) m2 na bacia, $([math]::Round($b1.SemDestino)) sem destino, de $([math]::Round($b1.Analisada)); borda no TIN, hatch acima; ajuste manual e apagar)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemBacias'
