<#
    O hatch da area do trafo (pedido do Renan em 05/10/2026: "na aba do
    trafo, ter um botao, para criar um hatch em torno da area que compreende
    as strings dos inversores que compoem aquele trafo"). Pelo caminho do
    botao (gancho fora de comando): trafo sem inversor e sem string e
    recusado com o porque; o T1 ganha hatch; gerar de novo substitui (o mesmo
    numero de hatches, nenhum handle antigo); "todos" faz T1 e T2 e recusa o
    T3. Lido da entidade: camada CLIVUS_TRAFO_AREA, SOLID, a cor do trafo,
    60 % de transparencia, o trafo no XData, a cota (0,30 m acima do canto
    mais alto dos modulos da ilha, dentro da faixa das faces; regra 5) e a
    area. Usa Substituicoes-Da-Usina.
#>
function Testar-EletricaTrafoHatch {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-trafo-hatch'
    if (-not $sub) { $problemas.Add('clivus-eletrica-trafo-hatch: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-trafo-hatch' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-trafo-hatch.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-trafo-hatch terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $erros = @()

    function Trecho([string] $de, [string] $ate) {
        $i = $t.IndexOf("CLIVUS_PASSO $de"); $j = $t.IndexOf("CLIVUS_PASSO $ate")
        if ($i -lt 0 -or $j -lt $i) { return '' }
        return $t.Substring($i, $j - $i)
    }

    function Hatches([string] $trecho) {
        @([regex]::Matches($trecho, 'CLIVUS_HATCH h=(\S+) camada=(\S+) padrao=(\S+) trafo=(\S+) c420=(-?\d+) t440=(-?\d+) z=(-?[\d.]+) area=([\d.]+) fim') | ForEach-Object {
            [pscustomobject]@{
                H = $_.Groups[1].Value; Camada = $_.Groups[2].Value; Padrao = $_.Groups[3].Value; Trafo = $_.Groups[4].Value
                C420 = $_.Groups[5].Value; T440 = $_.Groups[6].Value
                Z = [double]::Parse($_.Groups[7].Value, $inv); Area = [double]::Parse($_.Groups[8].Value, $inv)
            }
        })
    }

    function Ilhas([string] $trecho) {
        @([regex]::Matches($trecho, 'ELETRICA HATCH_ILHA trafo=(\S+) handle=(\S+) cota=(-?[\d.]+) topo=(-?[\d.]+) modulos=(\d+) area=([\d.]+) fim') | ForEach-Object {
            [pscustomobject]@{
                Trafo = $_.Groups[1].Value; H = $_.Groups[2].Value
                Cota = [double]::Parse($_.Groups[3].Value, $inv); Topo = [double]::Parse($_.Groups[4].Value, $inv)
                Modulos = [int]$_.Groups[5].Value; Area = [double]::Parse($_.Groups[6].Value, $inv)
            }
        })
    }

    # ---- recusas com o porque
    if ((Trecho 'sem_inversor' 'sem_string') -notmatch 'ELETRICA HATCH erro=True T1 n\S+o tem inversor') { $erros += 'o T1 sem inversor nao foi recusado com o porque' }
    if ((Trecho 'sem_string' 't1') -notmatch 'ELETRICA HATCH erro=True Os inversores de T1 n\S+o t\S+m string alocada') { $erros += 'o T1 sem string nao foi recusado com o porque' }

    # ---- T1
    $p1 = Trecho 't1' 't1_de_novo'
    $h1 = @(Hatches $p1)
    $i1 = @(Ilhas $p1)
    if ($p1 -notmatch 'ELETRICA HATCH erro=False T1: hatch da \S+rea com (\d+) ilha') { $erros += 'o T1 nao ganhou hatch' }
    if ($h1.Count -lt 1 -or $h1.Count -ne $i1.Count) { $erros += "o T1 tem $($h1.Count) hatch(es) no desenho e o botao disse $($i1.Count)" }
    $trafoT1 = if ($h1.Count -gt 0) { $h1[0].Trafo } else { '' }
    $olivaRgb = 140 * 65536 + 140 * 256 + 20
    foreach ($h in $h1) {
        if ($h.Camada -ne 'CLIVUS_TRAFO_AREA') { $erros += "hatch na camada $($h.Camada)" }
        if ($h.Padrao -ne 'SOLID') { $erros += "hatch com padrao $($h.Padrao)" }
        if ([int64]$h.C420 -ne $olivaRgb) { $erros += "o T1 nao tem a cor dele (420=$($h.C420))" }
        if ([int64]$h.T440 -ne 33554432 + 102) { $erros += "transparencia $($h.T440), nao 60 %" }
        if ($h.Trafo -ne $trafoT1) { $erros += 'hatches do T1 com trafos diferentes no XData' }
        $ilha = $i1 | Where-Object { $_.H -eq $h.H }
        if (-not $ilha) { $erros += "o hatch $($h.H) nao e o que o botao disse"; continue }
        if ([Math]::Abs($h.Z - $ilha.Cota) -gt 0.002) { $erros += "a cota lida ($($h.Z)) nao e a do botao ($($ilha.Cota))" }
        if ([Math]::Abs($ilha.Cota - $ilha.Topo - 0.30) -gt 0.002) { $erros += "o hatch nao esta 0,30 m acima do canto mais alto ($($ilha.Cota) e $($ilha.Topo))" }
        if ([Math]::Abs($h.Area - $ilha.Area) -gt [Math]::Max(0.05, 0.001 * $ilha.Area)) { $erros += "a area lida ($($h.Area)) nao e a do contorno ($($ilha.Area))" }
        if ($ilha.Modulos -lt 1 -or $h.Area -lt $ilha.Modulos * 1.0) { $erros += "a area ($($h.Area) m2) nao cobre os $($ilha.Modulos) modulos" }
    }

    # ---- de novo: substitui, nao duplica
    $p2 = Trecho 't1_de_novo' 'todos'
    $h2 = @(Hatches $p2)
    if ($p2 -notmatch 'O anterior foi substitu') { $erros += 'gerar de novo nao avisou que substituiu' }
    if ($h2.Count -ne $h1.Count) { $erros += "gerar de novo deixou $($h2.Count) hatch(es), eram $($h1.Count)" }
    if (@($h2 | Where-Object { @($h1 | ForEach-Object H) -contains $_.H }).Count -gt 0) { $erros += 'o hatch antigo do T1 ficou no desenho' }

    # ---- todos: T1 e T2, o T3 recusado
    $p3 = Trecho 'todos' 'fim'
    $h3 = @(Hatches $p3)
    $i3 = @(Ilhas $p3)
    if ($p3 -notmatch 'ELETRICA HATCH erro=False Hatch de 2 de 3 trafo') { $erros += 'o "todos" nao fez 2 de 3' }
    if ($p3 -notmatch 'T3 n\S+o tem inversor') { $erros += 'o T3 vazio nao foi recusado no "todos"' }
    $doT1 = @($i3 | Where-Object Trafo -eq 'T1'); $doT2 = @($i3 | Where-Object Trafo -eq 'T2')
    if ($doT1.Count -ne $h1.Count -or $doT2.Count -lt 1) { $erros += "o todos desenhou $($doT1.Count) do T1 e $($doT2.Count) do T2" }
    if ($h3.Count -ne $doT1.Count + $doT2.Count) { $erros += "o desenho tem $($h3.Count) hatches depois do todos" }
    $verdeMar = 40 * 65536 + 150 * 256 + 110
    foreach ($h in $h3) {
        $doDois = @($doT2 | ForEach-Object H) -contains $h.H
        if ($doDois -and [int64]$h.C420 -ne $verdeMar) { $erros += "o T2 nao tem a cor dele (420=$($h.C420))" }
        if ($doDois -and $h.Trafo -eq $trafoT1) { $erros += 'o hatch do T2 diz que e do T1' }
    }

    # ---- regra 5: a cota vem das faces dos modulos (que acompanham o terreno)
    if ($t -notmatch 'CLIVUS_FACES n=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+) fim') { $erros += 'sem as faces dos modulos' }
    else {
        $zmin = [double]::Parse($Matches[2], $inv); $zmax = [double]::Parse($Matches[3], $inv)
        foreach ($h in $h3) {
            if ($h.Z -lt $zmin -or $h.Z -gt $zmax + 0.301) { $erros += "hatch na cota $($h.Z), fora da faixa das faces ($zmin a $zmax + 0,30)" }
        }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-trafo-hatch: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (hatch da area: recusas, T1 com $($h1.Count) ilha(s), substitui sem duplicar, todos = T1 + T2, cota acima dos modulos)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaTrafoHatch'
