<#
    11.3: polaridade e tracado convencional. Um tipo com a F1.1 (2V, N
    colunas) e duas strings convencionais, uma por fileira: a de baixo com o
    + na coluna 1 e o - na coluna N, a de cima ao contrario (pontas opostas
    nas duas). Nenhum modulo sem string. O clique em diagonal e recusado, e
    a biblioteca lida do desenho continua com as duas strings.
#>
function Testar-StringTracado {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-string-tracado'
    if (-not $sub) { $problemas.Add('clivus-string-tracado: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-tracado' -Script (Join-Path $PSScriptRoot 'clivus-string-tracado.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-tracado terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'Modelo 1: mesas F1\.1 \((\d+)x2\)') { $erros += 'o tipo nao ficou com a F1.1 2V' }
    else {
        $n = [int]$Matches[1]
        if ($t -notmatch "STRING_TRACADO Modelo 1 1: $n m\S+dulo\(s\): \+ em mesa 1 col\. 1 fil\. 1, \S+ em mesa 1 col\. $n fil\. 1 \[C$($n - 1)\|0\.0\.0,0\.1\.0,") { $erros += "a string de baixo nao vai da coluna 1 (+) a $n (-)" }
        if ($t -notmatch "STRING_TRACADO Modelo 1 2: $n m\S+dulo\(s\): \+ em mesa 1 col\. $n fil\. 2, \S+ em mesa 1 col\. 1 fil\. 2") { $erros += "a string de cima nao vai da coluna $n (+) a 1 (-)" }
        if ($t -notmatch "STRING 1 tipo\(s\): Modelo 1 \S+ 1 mesa\(s\), $(2 * $n) m\S+dulo\(s\) \($($n)x2\), 2 string\(s\) de $n/$n") { $erros += 'a biblioteca lida do desenho nao tem as duas strings' }
    }
    if ($t -notmatch 'STRING_TRACADO Modelo 1 sem string: 0') { $erros += 'ficou modulo sem string' }
    if ($t -notmatch 'STRING N\S+o liguei: o trecho tem que seguir uma fileira ou uma coluna') { $erros += 'o clique em diagonal nao foi recusado' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-tracado: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (strings 11.3: duas strings convencionais, + e - em pontas opostas, diagonal recusada)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringTracado'
