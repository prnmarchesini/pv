<#
    14.4: excesso de capacidade. Um inversor de 2 MPPT x 2 entradas = 4
    recebe 6 strings pela selecao: a alocacao e feita (avisa, nao impede) e
    o aviso de excesso sai na linha de comando e na lista (excesso=2).
    Usa Substituicoes-Da-Usina de eletrica-contrato.ps1.
#>
function Testar-Eletrica144 {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-4'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-4: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-4' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-4.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-4 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'INVERSOR Inversor 1: 6 string\(s\) alocada\(s\), .* Agora 6 de 4 entradas') { $erros += 'as 6 strings nao foram alocadas' }
    if ($t -notmatch 'EXCESSO no Inversor 1: 6 strings para 4 entradas do modelo Pequeno \(2 a mais\)') { $erros += 'o aviso de excesso nao saiu na linha de comando' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 1 inversor(es)'))
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 1" modelo="Pequeno" strings=6 entradas=4 trafo= excesso=2 fim') { $erros += 'a lista nao mostra excesso=2' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-4: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (excesso: 6 strings em 4 entradas, alocadas e avisadas)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica144'
