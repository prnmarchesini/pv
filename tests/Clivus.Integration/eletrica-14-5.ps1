<#
    14.5: por inversor, selecionar todas (selecao implicita no CAD), soltar
    todas (as strings continuam no desenho, livres, com a mesma geometria),
    o outro inversor pega as soltas, e a string de outro inversor fica
    travada. Editar troca o nome. Usa Substituicoes-Da-Usina.
#>
function Testar-Eletrica145 {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-5'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-5: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-5' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-5.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-5 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'INVERSOR Inversor 1: 4 string\(s\) selecionada\(s\)') { $erros += 'selecionar todas nao achou as 4' }
    $impl = @([regex]::Matches($t, 'CLIVUS_IMPLICITA n=(\d+)'))
    if ($impl.Count -eq 0 -or $impl[-1].Groups[1].Value -ne '4') { $erros += 'a selecao implicita no CAD nao tem as 4 strings' }
    if ($t -notmatch 'ELETRICA soltas 4 de Inversor 1') { $erros += 'soltar todas nao soltou 4' }
    $soltas = @([regex]::Matches($t, 'CLIVUS_SOLTAS dele=(\d+) total_antes=(\d+) total_depois=(\d+) livres=(\d+) fim'))
    if ($soltas.Count -eq 0) { $erros += 'sem a contagem depois de soltar' }
    else {
        $m = $soltas[-1].Groups
        if ($m[1].Value -ne '4' -or $m[4].Value -ne '4') { $erros += "das $($m[1].Value) strings soltas, $($m[4].Value) ficaram livres" }
        if ($m[2].Value -ne $m[3].Value) { $erros += "soltar apagou string do desenho: $($m[2].Value) antes, $($m[3].Value) depois" }
    }
    $antes = @([regex]::Matches($t, 'CLIVUS_GEO_ANTES((?: \d+:-?[\d.]+)+) fim'))
    $depois = @([regex]::Matches($t, 'CLIVUS_GEO_DEPOIS((?: \d+:-?[\d.]+)+) fim'))
    if ($antes.Count -eq 0 -or $depois.Count -eq 0 -or $antes[-1].Groups[1].Value -ne $depois[-1].Groups[1].Value) { $erros += 'a geometria das strings mudou ao soltar' }
    if ($t -notmatch 'INVERSOR Inversor 2: 4 string\(s\) alocada\(s\), 0 j\S+ eram dele, 0 recusada') { $erros += 'o Inversor 2 nao pegou as 4 soltas' }
    if ($t -notmatch 'INVERSOR Inversor 1: 0 string\(s\) alocada\(s\), 0 j\S+ eram dele, 4 recusada') { $erros += 'as strings do Inversor 2 nao ficaram travadas para o Inversor 1' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 inversor(es)'))
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 1" modelo="Huawei 250" strings=0 ') { $erros += 'o Inversor 1 nao ficou com 0' }
    if ($final -notmatch 'ELETRICA INVERSOR nome="INV-NORTE" modelo="Huawei 250" strings=4 ') { $erros += 'o Inversor 2 nao virou INV-NORTE com 4' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-5: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (selecionar, soltar sem apagar, travada para outro inversor, editar)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica145'
