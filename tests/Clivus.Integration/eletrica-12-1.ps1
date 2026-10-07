<#
    12.1: subestacoes compartilhadas (UC1, UC2) e a tabela de trafos. UC1 recebe
    T1 e T2; o T1 nao pode ir para a UC2 (travado ate ser solto); UC2 recebe T3.
    O vinculo e lido do desenho (o trafo guarda a UC).
#>
function Testar-Eletrica121 {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-12-1' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-12-1.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-12-1 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'ELETRICA uc UC1 criada' -or $t -notmatch 'ELETRICA uc UC2 criada') { $erros += 'UC1 e UC2 nao foram criadas' }
    if ($t -notmatch 'ELETRICA recusado: T1 j\S+ est\S+ ligado a UC1') { $erros += 'o T1 nao ficou travado na UC1' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 subestacao(oes)'))
    if ($final -notmatch 'ELETRICA UC UC1 modo=Shared nome="Subesta\S+ UC1" tamanho=4x3x3 trafos=T1,T2') { $erros += 'UC1 nao tem T1 e T2' }
    if ($final -notmatch 'ELETRICA UC UC2 modo=Shared nome="Subesta\S+ UC2" tamanho=4x3x3 trafos=T3\s') { $erros += 'UC2 nao tem so o T3' }
    if ($final -notmatch 'ELETRICA TRAFO T1 .* uc=UC1') { $erros += 'o T1 no desenho nao aponta para a UC1' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-12-1: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (subestacao: UC1 com T1 e T2, UC2 com T3, trafo de outra travado)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica121'
