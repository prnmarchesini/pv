<#
    11.5: clonar, espelhar e apagar um tipo. Modelo 1 (F1.1, 2V de N
    colunas) com duas strings convencionais; o clone (Modelo 2) sai igual;
    espelhado, cada string do Modelo 2 troca o + com o -; o Modelo 1 nao
    muda (o segundo clone, Modelo 3, e igual a ele); apagar o Modelo 3 deixa
    dois tipos no desenho.
#>
function Testar-StringModelo {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-string-modelo'
    if (-not $sub) { $problemas.Add('clivus-string-modelo: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-modelo' -Script (Join-Path $PSScriptRoot 'clivus-string-modelo.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-modelo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Pontas([string] $modelo, [int] $i) {
        if ($t -match "STRING_TRACADO $modelo $($i): \d+ m\S+dulo\(s\): \+ em (mesa 1 col\. \d+ fil\. \d), \S+ em (mesa 1 col\. \d+ fil\. \d)") { return @($Matches[1], $Matches[2]) }
        return $null
    }

    foreach ($i in 1, 2) {
        $original = Pontas 'Modelo 1' $i
        $clone = Pontas 'Modelo 3' $i
        $espelho = @()
        foreach ($m in [regex]::Matches($t, "STRING_TRACADO Modelo 2 $($i): \d+ m\S+dulo\(s\): \+ em (mesa 1 col\. \d+ fil\. \d), \S+ em (mesa 1 col\. \d+ fil\. \d)")) { $espelho += , @($m.Groups[1].Value, $m.Groups[2].Value) }

        if (-not $original -or -not $clone -or $espelho.Count -ne 2) { $erros += "faltou a string $i em algum modelo"; continue }
        if ($espelho[0][0] -ne $original[0] -or $espelho[0][1] -ne $original[1]) { $erros += "o clone da string $i nao saiu igual" }
        if ($espelho[1][0] -ne $original[1] -or $espelho[1][1] -ne $original[0]) { $erros += "o espelho da string $i nao trocou o + com o -" }
        if ($clone[0] -ne $original[0] -or $clone[1] -ne $original[1]) { $erros += "o Modelo 1 mudou depois de espelhar o clone (string $i)" }
    }

    if ($t -notmatch 'STRING Modelo 3 apagado da biblioteca') { $erros += 'nao apagou o Modelo 3' }
    if ($t -notmatch 'STRING 2 tipo\(s\): Modelo 1 \S+ 1 mesa\(s\), \d+ m\S+dulo\(s\) \(\d+x2\), 2 string\(s\) de \d+/\d+; Modelo 2 ') { $erros += 'a biblioteca nao ficou com os Modelos 1 e 2, com tracado' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-modelo: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (strings 11.5: clone igual, espelho troca + com -, original intacto, apagar)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringModelo'
