<#
    11.4: leapfrog e tracado livre por cliques. Modelo 1 (F1.1, 2V de N
    colunas): a fileira inteira em leapfrog, duas vezes (uma por fileira):
    + na coluna 1 e - na 2 embaixo; em cima, clicando perto da coluna 1, o
    mesmo. Modelo 2 (F1.1): dois U livres, um em cada metade (N/2 embaixo e
    sobe), com o - voltando ao lado do +. Nenhum modulo sem string.
#>
function Testar-StringLeapfrog {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-string-leapfrog'
    if (-not $sub) { $problemas.Add('clivus-string-leapfrog: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-leapfrog' -Script (Join-Path $PSScriptRoot 'clivus-string-leapfrog.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-leapfrog terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'Modelo 1: mesas F1\.1 \((\d+)x2\)') { $erros += 'o tipo nao ficou com a F1.1 2V' }
    else {
        $n = [int]$Matches[1]; $m = [int]($n / 2)
        foreach ($f in 1, 2) {
            if ($t -notmatch "STRING_TRACADO Modelo 1 $($f): $n m\S+dulo\(s\): \+ em mesa 1 col\. 1 fil\. $f, \S+ em mesa 1 col\. 2 fil\. $f \(leapfrog\) \[L$($n - 1)\|0\.0\.$($f - 1),0\.2\.$($f - 1),0\.4\.") { $erros += "o leapfrog da fileira $f nao alterna com + na coluna 1 e - na 2" }
        }
        if ($t -notmatch "STRING_TRACADO Modelo 2 1: $(2 * $m) m\S+dulo\(s\): \+ em mesa 1 col\. 1 fil\. 1, \S+ em mesa 1 col\. 1 fil\. 2 \[C$($m - 1),C$m,C$(2 * $m - 1)\|") { $erros += 'o U da primeira metade nao volta com o - ao lado do +' }
        if ($t -notmatch "STRING_TRACADO Modelo 2 2: $(2 * ($n - $m)) m\S+dulo\(s\): \+ em mesa 1 col\. $n fil\. 1, \S+ em mesa 1 col\. $n fil\. 2") { $erros += 'o U da segunda metade nao saiu' }
    }
    if (([regex]::Matches($t, 'STRING_TRACADO Modelo \d sem string: 0')).Count -ne 2) { $erros += 'ficou modulo sem string' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-leapfrog: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (strings 11.4: leapfrog de fileira inteira, + e - lado a lado; dois U livres)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringLeapfrog'
