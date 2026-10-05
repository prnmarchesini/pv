<#
    Modelo de inversor com as entradas de cada MPPT (pedido do Renan em
    05/10/2026): 5 MPPTs com 4;4;4;5;5 = 22 entradas; lista que nao bate com
    o numero de MPPTs e MPPT sem entrada sao recusados; o formato 1 (MPPT x
    entradas por MPPT) e lido como a lista repetida e regravado no 2.
#>
function Testar-Eletrica141Mppt {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-1-mppt' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-1-mppt.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-1-mppt terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    $marca = $t.IndexOf('CLIVUS_MPPT_ANTES_DE_REGRAVAR')
    $antes = $t.Substring(0, [Math]::Max(0, $marca))
    if ($antes -notmatch 'ELETRICA 1 modelo\(s\) formato_modelos=1') { $erros += 'o formato 1 nao ficou gravado' }
    if ($antes -notmatch 'ELETRICA MODELO nome="Antigo 5x4" mppt=5 entradas=4 total=20 ') { $erros += 'o formato 1 nao foi lido como 5 MPPTs de 4' }
    if (([regex]::Matches($t, 'ELETRICA recusado:')).Count -ne 2) { $erros += 'lista curta e MPPT sem entrada nao foram os dois recusados' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 3 modelo(s)'))
    if ($final -notmatch 'formato_modelos=2') { $erros += 'a gravacao nao passou ao formato 2' }
    if ($final -notmatch 'ELETRICA MODELO nome="Antigo 5x4" mppt=5 entradas=4 total=20 ') { $erros += 'o modelo antigo mudou ao regravar' }
    if ($final -notmatch 'ELETRICA MODELO nome="Huawei 330" mppt=5 entradas=4;4;4;5;5 total=22 ') { $erros += 'o Huawei 330 nao tem a lista 4;4;4;5;5 = 22' }
    if ($final -notmatch 'ELETRICA MODELO nome="Igual" mppt=3 entradas=6 total=18 ') { $erros += 'um numero so nao valeu para os 3 MPPTs' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-1-mppt: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (MPPTs com entradas proprias: 4;4;4;5;5 = 22, formato 1 lido e regravado)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica141Mppt'
