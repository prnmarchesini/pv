<#
    11.1: a biblioteca de tipos de string. Cria dois (Modelo 1 e 2), renomeia
    o 1 (Leste 28: nome do usuario nao conta na sequencia), apaga o 2, cria
    outro (Modelo 1, que nao existe mais) e lista o que ficou no desenho.
#>
function Testar-StringTipos {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-tipos' -Script (Join-Path $PSScriptRoot 'clivus-string-tipos.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-tipos terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'STRING Modelo 1 criado') { $erros += 'Modelo 1 nao foi criado' }
    if ($t -notmatch 'STRING Modelo 2 criado') { $erros += 'Modelo 2 nao foi criado' }
    if ($t -notmatch 'STRING Modelo 1 renomeado para Leste 28') { $erros += 'nao renomeou' }
    if (([regex]::Matches($t, 'STRING Modelo 1 criado')).Count -ne 2) { $erros += 'o terceiro nao virou Modelo 1' }
    if ($t -notmatch 'STRING 2 tipo\(s\): Leste 28 \S+ sem mesas escolhidas; Modelo 1 \S+ sem mesas escolhidas') { $erros += 'a lista final nao e Leste 28 e Modelo 1' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-tipos: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (strings: biblioteca com nomes, renomear, apagar, gravada no desenho)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringTipos'
