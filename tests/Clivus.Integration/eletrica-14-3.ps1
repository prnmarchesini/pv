<#
    14.3: alocacao de strings em campo pelo comando de verdade
    (CLIVUS_ELETRICA_ALOCAR com uma selecao montada no LISP). A selecao do
    Inversor 2 leva 3 strings do Inversor 1, 5 livres, 20 modulos e o
    alinhamento: entram so as 5 livres, as 3 sao recusadas (travadas), o
    resto nem conta. A geometria das 8 strings e lida antes e depois.
    Usa Substituicoes-Da-Usina de eletrica-contrato.ps1.
#>
function Testar-Eletrica143 {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-3'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-3: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-3' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-3.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-3 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'CLIVUS_SEL alocadas=3 livres=5 total=29') { $erros += 'a selecao montada nao tem 3 alocadas, 5 livres, 20 modulos e o alinhamento' }
    if ($t -notmatch 'ELETRICA alocadas 3 em Inversor 1') { $erros += 'o Inversor 1 nao recebeu 3' }
    if ($t -notmatch 'INVERSOR Inversor 2: 5 string\(s\) alocada\(s\), 0 j\S+ eram dele, 3 recusada\(s\) por serem de outro inversor\. Agora 5 de 20 entradas') { $erros += 'o relatorio da alocacao nao e 5 alocadas e 3 recusadas' }
    # O Core Console ecoa a expressao LISP: vale a ultima ocorrencia, a do resultado.
    $antes = @([regex]::Matches($t, 'CLIVUS_GEO_ANTES((?: \d+:-?[\d.]+)+) fim'))
    $depois = @([regex]::Matches($t, 'CLIVUS_GEO_DEPOIS((?: \d+:-?[\d.]+)+) fim'))
    if ($antes.Count -eq 0 -or $depois.Count -eq 0) { $erros += 'sem a geometria de antes e depois' }
    elseif ($antes[-1].Groups[1].Value -ne $depois[-1].Groups[1].Value) { $erros += "a geometria das strings mudou: antes '$($antes[-1].Groups[1].Value)', depois '$($depois[-1].Groups[1].Value)'" }
    elseif (($antes[-1].Groups[1].Value.Trim() -split ' ').Count -ne 8) { $erros += 'a geometria nao e das 8 strings' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 inversor(es)'))
    if ($final -notmatch 'ELETRICA 2 inversor\(es\) (\d+) string\(s\) (\d+) livre\(s\)') { $erros += 'sem a contagem final' }
    elseif ([int]$Matches[2] -ne [int]$Matches[1] - 8) { $erros += "livres $($Matches[2]) de $($Matches[1]), esperava 8 alocadas" }
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 1" modelo="Huawei 250" strings=3 ') { $erros += 'o Inversor 1 nao ficou com 3' }
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 2" modelo="Huawei 250" strings=5 ') { $erros += 'o Inversor 2 nao ficou com 5' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-3: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (alocar strings: so strings entram, as de outro inversor travadas, geometria intocada)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica143'
