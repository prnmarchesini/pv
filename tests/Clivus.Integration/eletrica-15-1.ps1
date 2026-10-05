<#
    15.1: a composicao da tag em tres pedacos. O padrao (T1.I1.S1), uma com
    risquinho e prefixos longos, uma colada sem o trafo (1S1), uma colada
    ambigua (recusada; a gravada fica) e a releitura do desenho.
#>
function Testar-NumeracaoTag {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-tag' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-tag.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-tag terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $tags = [regex]::Matches($t, 'NUMERACAO tag Exemplo: (\S+), (\S+), (\S+); inversor sem trafo: (\S+)') | ForEach-Object { $_.Groups[1..4].Value -join ' ' }
    $esperado = @(
        'T1.I1.S1 T1.I1.S2 T1.I2.S1 I3.S1',
        'Trafo1-Inv1-S1 Trafo1-Inv1-S2 Trafo1-Inv2-S1 Inv3-S1',
        '1S1 1S2 2S1 3S1',
        '1S1 1S2 2S1 3S1'
    )

    $erros = @()
    if (($tags -join ' | ') -ne ($esperado -join ' | ')) { $erros += "as tags foram [$($tags -join ' | ')], esperava [$($esperado -join ' | ')]" }
    if ($t -notmatch 'NUMERACAO N.o salvei: colado, o inversor precisa de prefixo') { $erros += 'a composicao colada ambigua nao foi recusada' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-tag: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (numeracao: tag T1.I1.S1, Trafo1-Inv1-S1, 1S1; colada ambigua recusada)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoTag'
