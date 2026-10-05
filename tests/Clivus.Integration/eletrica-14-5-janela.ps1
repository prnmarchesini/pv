<#
    O botao Selecionar da aba Inversor (reprovacao de 05/10/2026: "o botao
    selecionar no inversor nao faz nada, somente o botao + funciona"). O
    gancho de teste chama o MESMO caminho do botao, fora de comando; a
    selecao implicita lida depois pelo LISP (ssget "_I") tem as 5 strings do
    Inversor 1 e so elas; depois as 3 do Inversor 2, sem nenhuma do 1. Usa
    Substituicoes-Da-Usina.
#>
function Testar-Eletrica145Janela {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-5-janela'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-5-janela: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-5-janela' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-5-janela.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-5-janela terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'CLIVUS_JANELA_SEL n=(\d+) dele=(\d+) todas_dele=(\d+) fim') { $erros += 'sem a leitura da selecao' }
    elseif ($Matches[1] -ne '5' -or $Matches[2] -ne '5' -or $Matches[3] -ne '5') { $erros += "o Selecionar do Inversor 1 deixou n=$($Matches[1]) dele=$($Matches[2]) de $($Matches[3])" }
    if ($t -notmatch 'CLIVUS_JANELA_SEL2 n=(\d+) do1=(\d+) fim') { $erros += 'sem a leitura da segunda selecao' }
    elseif ($Matches[1] -ne '3' -or $Matches[2] -ne '0') { $erros += "o Selecionar do Inversor 2 deixou n=$($Matches[1]), $($Matches[2]) do Inversor 1" }
    if ($t -notmatch 'ELETRICA selecionadas pela janela 5 de Inversor 1') { $erros += 'o caminho do botao nao achou as 5 strings' }
    if ($t -notmatch 'ELETRICA selecionadas pela janela 3 de Inversor 2') { $erros += 'o caminho do botao nao achou as 3 strings do Inversor 2' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-5-janela: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (Selecionar pela janela: a selecao implicita tem as strings do inversor e so elas)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica145Janela'
