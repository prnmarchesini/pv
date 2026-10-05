<#
    11.8: mesa sem tipo que case nao e preenchida e o sistema avisa qual.
    Usina mista; so o Modelo 1 (uma de 28, duas strings). Cada mesa de 14
    (perfil lido do XData em LISP) tem o aviso nominal "F?.?: mesa de 14
    modulos (7x2) sem tipo de string", fica selecionada no desenho, e nao
    recebe string: as strings desenhadas sao 2 por mesa de 28.
#>
function Testar-StringSemTipo {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina-Mista -Desenho $Desenho -Rotulo 'clivus-string-sem-tipo'
    if (-not $sub) { $problemas.Add('clivus-string-sem-tipo: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-sem-tipo' -Script (Join-Path $PSScriptRoot 'clivus-string-sem-tipo.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-sem-tipo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    $perfil = @{}
    foreach ($m in [regex]::Matches($t, 'CLIVUS_MESA (F\d+\.\d+) (Mesa 2V(?:28|14))')) { $perfil[$m.Groups[1].Value] = $m.Groups[2].Value }
    $de14 = @($perfil.Keys | Where-Object { $perfil[$_] -eq 'Mesa 2V14' } | Sort-Object)
    $de28 = @($perfil.Keys | Where-Object { $perfil[$_] -eq 'Mesa 2V28' })
    if ($de14.Count -lt 1 -or $de28.Count -lt 1) { $erros += "a usina mista nao saiu ($($de28.Count) de 28, $($de14.Count) de 14)" }

    $avisadas = @([regex]::Matches($t, 'STRING_GERAR Aviso: (F\d+\.\d+): mesa de 14 m\S+dulos \(7x2\) sem tipo de string') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    if (($avisadas -join ',') -ne ($de14 -join ',')) { $erros += "avisadas [$($avisadas -join ', ')] em vez das de 14 [$($de14 -join ', ')]" }
    if ($t -notmatch "STRING_GERAR \d+ string\(s\) em \d+ grupo\(s\) de mesas; $($de14.Count) mesa\(s\) sem tipo") { $erros += 'o resumo nao conta as mesas sem tipo' }

    if ($t -notmatch 'CLIVUS_SEM_TIPO strings=(\d+) selecionadas=([ F\d.]*)') { $erros += 'nao li a contagem final' }
    else {
        if ([int]$Matches[1] -ne 2 * $de28.Count) { $erros += "$($Matches[1]) strings no desenho para $($de28.Count) mesa(s) de 28 (devia ser o dobro; mesa de 14 nao recebe)" }
        $sel = @($Matches[2].Trim() -split ' ' | Where-Object { $_ } | Sort-Object)
        if (($sel -join ',') -ne ($de14 -join ',')) { $erros += "selecionadas [$($sel -join ', ')] em vez das de 14" }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-sem-tipo: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (strings 11.8: $($de14.Count) mesa(s) de 14 sem tipo, avisadas pelo nome e selecionadas; nenhuma string nelas)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringSemTipo'
