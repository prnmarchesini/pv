<#
    Reprovacao de 05/10/2026 ("nos trafos nao mostra a potencia depois de
    salvo"): o formulario do trafo grava e rele potencia, tensoes, fator K e
    impedancia pelo MESMO caminho de conversao de texto da janela
    (TransformerForm.Read), com "2500", "13.800", "13800", "800", "6,5",
    "0,8 kV", "3.150", "7,5 %". A UC escolhida no formulario segue a trava da
    aba Subestacao (unitaria com um trafo so; trafo de outra UC travado), e o
    Salvar recusado nao grava nada (tudo ou nada). O trafo do padrao vem com os
    numeros do padrao.
#>
function Testar-EletricaTrafoFormulario {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-13-1-formulario' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-13-1-formulario.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-13-1-formulario terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    $iPadrao = $t.IndexOf('CLIVUS_FORM_PADRAO')
    $iFinal = $t.IndexOf('CLIVUS_FORM_FINAL')
    if ($iPadrao -lt 0 -or $iFinal -lt $iPadrao) {
        $problemas.Add("clivus-eletrica-13-1-formulario: marcadores fora de ordem. Veja $($r.Saida)")
        return $false
    }

    $padrao = $t.Substring($iPadrao, $iFinal - $iPadrao)
    if ($padrao -notmatch 'ELETRICA TRAFO T2 nome="Trafo 2" entrada=800 saida=13800 kva=2500 k=1 z=6 ') { $erros += 'o trafo do padrao nao veio com os numeros do padrao' }

    if ($t -notmatch 'ELETRICA trafo T1 salvo pelo formulario') { $erros += 'o formulario do T1 nao salvou' }
    if ($t -notmatch 'ELETRICA recusado: U1 \S+ unit\S+ria e j\S+ tem o trafo T1') { $erros += 'a U1 aceitou um segundo trafo pelo formulario' }
    if ($t -notmatch 'ELETRICA trafo T2 salvo pelo formulario') { $erros += 'o formulario do T2 nao salvou' }
    if ($t -notmatch 'ELETRICA recusado: n\S+o consigo ler a tens\S+o de sa\S+da') { $erros += 'o numero ilegivel nao foi recusado pelo nome do campo' }
    if ($t -notmatch 'ELETRICA recusado: T1 j\S+ est\S+ ligado a U1') { $erros += 'o T1 nao ficou travado na U1 pelo formulario' }

    $final = $t.Substring($iFinal)
    if ($final -notmatch 'ELETRICA TRAFO T1 nome="Trafo seco" entrada=800 saida=13800 kva=2500 k=4 z=6\.5 tamanho=3x2\.5x2\.5 notas="" uc=U1\s') { $erros += 'T1 no desenho nao tem os numeros digitados (800 / 13.800 / 2500 / 4 / 6,5) e a U1' }
    if ($final -notmatch 'ELETRICA TRAFO T2 nome="Trafo 2" entrada=800 saida=13800 kva=3150 k=1 z=7\.5 tamanho=3\.2x2\.6x2\.6 notas="" uc=C1\s') { $erros += 'T2 no desenho nao tem os numeros digitados (3.150 / 7,5 %) e a C1' }
    if ($final -notmatch 'ELETRICA UC U1 .* trafos=T1\s') { $erros += 'a U1 nao lista o T1' }
    if ($final -notmatch 'ELETRICA UC C1 .* trafos=T2\s') { $erros += 'a C1 nao lista o T2' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-13-1-formulario: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (formulario do trafo: numeros digitados gravados e relidos, UC com trava, recusa sem gravar)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaTrafoFormulario'
