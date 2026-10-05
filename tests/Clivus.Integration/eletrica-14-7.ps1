<#
    14.7: o skid (trafo + inversores) pela selecao em campo, pelo comando de
    verdade. Skid Norte (T1) com os inversores 1 a 4 (o retangulo do T2 na
    selecao nao entra); Skid T2 com 5 e 6 (o 4, do T1, fica travado). Mover
    o retangulo do Inversor 1 nao muda o skid dele: o vinculo e dado.
    Usa Pontos-Do-Terreno de eletrica-12-3.ps1.
#>
function Testar-Eletrica147 {
    param([string] $Desenho)

    $pontos = Pontos-Do-Terreno -Desenho $Desenho -Rotulo 'clivus-eletrica-14-7' -Deslocamentos @{
        P1 = @(-30, 0); P2 = @(30, 0)
        Q1 = @(-20, -15); Q2 = @(-15, -15); Q3 = @(-10, -15); Q4 = @(-5, -15); Q5 = @(20, -15); Q6 = @(25, -15)
    }
    if (-not $pontos) { $problemas.Add('clivus-eletrica-14-7: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-7' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-7.scr') -Substituicoes $pontos.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-7 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'CLIVUS_SKID_SEL a=5 b=3') { $erros += 'a selecao montada no LISP nao tem 5 e 3 retangulos' }
    if ($t -notmatch 'SKID Skid Norte \(T1\): 4 inversor\(es\) agrupado\(s\), 0 j\S+ eram dele, 0 recusado\(s\) .* Agora 4 inversor') { $erros += 'o Skid Norte nao ficou com os 4 (ou o T2 entrou)' }
    if ($t -notmatch 'SKID Skid T2 \(T2\): 2 inversor\(es\) agrupado\(s\), 0 j\S+ eram dele, 1 recusado\(s\)') { $erros += 'o Skid T2 nao recusou o Inversor 4' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 6 inversor(es)'))
    if ($final -notmatch 'ELETRICA SKID nome="Skid Norte" trafo=T1 inversores=Inversor 1,Inversor 2,Inversor 3,Inversor 4 fim') { $erros += 'o skid do T1 no desenho nao e o esperado' }
    if ($final -notmatch 'ELETRICA SKID nome="Skid T2" trafo=T2 inversores=Inversor 5,Inversor 6 fim') { $erros += 'o skid do T2 no desenho nao e o esperado' }
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 1" .* trafo=T1 ') { $erros += 'o Inversor 1 perdeu o trafo ao ser movido' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-7: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (skid: 4 no T1 e 2 no T2, so inversor entra, travado em outro skid, mover nao muda o vinculo)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica147'
