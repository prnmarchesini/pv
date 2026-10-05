<#
    14.2: a lista de inversores por modelo. 4 do Huawei 250 (20 entradas) e
    2 do Sungrow 110 (18), nomes Inversor 1 a 6 continuando, cada um com 0
    strings. Modelo que nao existe e recusado.
#>
function Testar-Eletrica142 {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-2' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-2.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-2 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'ELETRICA inversores Inversor 1,Inversor 2,Inversor 3,Inversor 4 criados') { $erros += 'os 4 primeiros nao sao Inversor 1 a 4' }
    if ($t -notmatch 'ELETRICA inversores Inversor 5,Inversor 6 criados') { $erros += 'os do segundo modelo nao continuaram em 5 e 6' }
    if ($t -notmatch 'ELETRICA recusado: modelo nao existe') { $erros += 'modelo inexistente nao foi recusado' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 6 inversor(es)'))
    foreach ($n in 1..4) { if ($final -notmatch "ELETRICA INVERSOR nome=""Inversor $n"" modelo=""Huawei 250"" strings=0 entradas=20 trafo= excesso=0 fim") { $erros += "Inversor $n nao e Huawei com 0 strings" } }
    foreach ($n in 5..6) { if ($final -notmatch "ELETRICA INVERSOR nome=""Inversor $n"" modelo=""Sungrow 110"" strings=0 entradas=18 trafo= excesso=0 fim") { $erros += "Inversor $n nao e Sungrow com 0 strings" } }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-2: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (inversores: 4 Huawei e 2 Sungrow, nomes continuando, 0 strings cada)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica142'
