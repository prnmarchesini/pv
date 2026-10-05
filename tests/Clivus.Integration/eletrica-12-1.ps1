<#
    12.1: subestacoes compartilhadas (C1, C2) e a tabela de trafos. C1 recebe
    T1 e T2; o T1 nao pode ir para a C2 (travado ate ser solto); C2 recebe T3.
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
    if ($t -notmatch 'ELETRICA uc C1 criada' -or $t -notmatch 'ELETRICA uc C2 criada') { $erros += 'C1 e C2 nao foram criadas' }
    if ($t -notmatch 'ELETRICA recusado: T1 j\S+ est\S+ ligado a C1') { $erros += 'o T1 nao ficou travado na C1' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 subestacao(oes)'))
    if ($final -notmatch 'ELETRICA UC C1 modo=Shared nome="Subesta\S+ C1" tamanho=4x3x3 trafos=T1,T2') { $erros += 'C1 nao tem T1 e T2' }
    if ($final -notmatch 'ELETRICA UC C2 modo=Shared nome="Subesta\S+ C2" tamanho=4x3x3 trafos=T3\s') { $erros += 'C2 nao tem so o T3' }
    if ($final -notmatch 'ELETRICA TRAFO T1 .* uc=C1') { $erros += 'o T1 no desenho nao aponta para a C1' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-12-1: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (subestacao: C1 com T1 e T2, C2 com T3, trafo de outra travado)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica121'
