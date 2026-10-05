<#
    15.3: a ordem dos blocos na lista (a ordem da numeracao). Subir e descer
    mudam a lista gravada; na ponta, nao anda e avisa.
#>
function Testar-NumeracaoOrdem {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-ordem' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-ordem.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-ordem terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $final = $t.Substring($t.IndexOf('CLIVUS_NUMERACAO_LISTA_FINAL'))
    $ordem = [regex]::Matches($final, 'NUMERACAO bloco (\d)\. (Bloco \d)') | ForEach-Object { $_.Groups[2].Value }
    $erros = @()

    if (($ordem -join ', ') -ne 'Bloco 3, Bloco 2, Bloco 1') { $erros += "a lista final foi [$($ordem -join ', ')], esperava [Bloco 3, Bloco 2, Bloco 1]" }
    if (([regex]::Matches($t, 'NUMERACAO Bloco \d n.o andou')).Count -ne 2) { $erros += 'subir no topo e descer no fim deviam avisar que nao andou (2 vezes)' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-ordem: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (numeracao: blocos reordenados 3, 2, 1; na ponta nao anda)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoOrdem'
