<#
    11.2: escolher em campo as mesas de um tipo de string. So mesa entra (a
    string e a linha soltas na selecao ficam de fora); uma mesa 2V vira uma
    mesa de duas fileiras; duas vizinhas viram duas mesas com o vao de campo;
    mesas de fileiras diferentes sao recusadas; selecao sem mesa avisa.
#>
function Testar-StringMesas {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-string-mesas'
    if (-not $sub) { $problemas.Add('clivus-string-mesas: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-mesas' -Script (Join-Path $PSScriptRoot 'clivus-string-mesas.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-mesas terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'CLIVUS_ACHADAS F1.1=sim F1.2=sim F2.1=sim') { $erros += 'o LISP nao achou F1.1, F1.2 e F2.1' }

    if ($t -notmatch 'STRING Modelo 1 criado\. Modelo 1: mesas F1\.1 \((\d+)x(\d+)\), (\d+) m') { $erros += 'uma mesa (com string e linha na selecao) nao virou o Modelo 1 so com a F1.1' }
    elseif ([int]$Matches[2] -ne 2 -or [int]$Matches[1] * 2 -ne [int]$Matches[3]) { $erros += "a F1.1 devia ser 2V: $($Matches[0])" }

    if ($t -notmatch 'STRING Modelo 2 criado\. Modelo 2: mesas (F1\.[12]), (F1\.[12]) \((\d+)x2;(\d+)x2\)' -or $Matches[1] -eq $Matches[2]) { $erros += 'duas vizinhas nao viraram o Modelo 2 com duas mesas 2V' }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    if ($t -notmatch 'STRING_DESENHO Modelo 2 \d+x2;\d+x2 ([\d.]+);([\d.]+);([\d.]+)\s') { $erros += 'o Modelo 2 nao gravou o desenho do cartesiano com um vao' }
    else {
        $largura = [double]::Parse($Matches[1], $inv); $altura = [double]::Parse($Matches[2], $inv); $vao = [double]::Parse($Matches[3], $inv)
        if ($largura -lt 0.5 -or $largura -gt 2.5 -or $altura -lt 0.5 -or $altura -gt 3 -or $vao -gt 5) { $erros += "desenho do cartesiano estranho: celula $largura x $altura, vao $vao" }
    }

    if ($t -notmatch 'STRING N\S+o escolhi as mesas: F1\.1 e F2\.1 n\S+o est\S+o na mesma fileira') { $erros += 'F1.1 com F2.1 nao foi recusado' }
    if ($t -notmatch 'STRING N\S+o escolhi as mesas: nenhuma mesa do plugin na sele') { $erros += 'selecao so com linha e string nao avisou' }
    if ($t -notmatch 'STRING 2 tipo\(s\)') { $erros += 'a biblioteca nao ficou com 2 tipos' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-mesas: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (strings 11.2: so mesa entra, 1 mesa 2V, 2 vizinhas com vao, fileiras diferentes recusadas)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringMesas'
