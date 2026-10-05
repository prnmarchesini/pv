<#
    12.2: duas subestacoes unitarias (U1, U2), cada uma com o seu trafo. A
    U1 recusa um segundo trafo (T3). O vinculo e lido do desenho.
#>
function Testar-Eletrica122 {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-12-2' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-12-2.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-12-2 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'ELETRICA uc U1,U2 criada') { $erros += 'U1 e U2 nao foram criadas' }
    if ($t -notmatch 'ELETRICA recusado: U1 \S+ unit\S+ria e j\S+ tem o trafo T1') { $erros += 'a U1 aceitou um segundo trafo' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 subestacao(oes)'))
    if ($final -notmatch 'ELETRICA UC U1 modo=Unitary nome="Subesta\S+ U1" tamanho=4x3x3 trafos=T1\s') { $erros += 'U1 nao tem so o T1' }
    if ($final -notmatch 'ELETRICA UC U2 modo=Unitary nome="Subesta\S+ U2" tamanho=4x3x3 trafos=T2\s') { $erros += 'U2 nao tem so o T2' }
    if ($final -notmatch 'ELETRICA TRAFO T3 .* uc=\s') { $erros += 'o T3 ficou com subestacao' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-12-2: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (subestacao unitaria: U1 com T1, U2 com T2, segundo trafo recusado)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica122'
