<#
    O contrato da parte eletrica (plano/eletrica/05-contrato-interno.md): a
    usina pelo motor e as strings de teste, uma por fileira de cada mesa.
    Tambem e o modelo para os casos eletricos que precisam de usina: a funcao
    Substituicoes-Da-Usina da os pontos da area e do alinhamento.
#>
function Substituicoes-Da-Usina {
    param([string] $Desenho, [string] $Rotulo)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { return $null }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###},0', $cx + $dx, $cy + $dy) }

    return @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{L1}}' = (P -50 -50); '{{L2}}' = (P -50 50); '{{LADO}}' = (P 0 0)
    }
}

function Testar-EletricaContrato {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-contrato'
    if (-not $sub) { $problemas.Add('clivus-eletrica-contrato: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-contrato' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-contrato.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto -notmatch 'CLIVUS_CONTRATO strings=(\d+) modulos=(\d+)') {
        $problemas.Add("clivus-eletrica-contrato terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $strings = [int]$Matches[1]; $modulos = [int]$Matches[2]
    if ($strings -le 0 -or $r.Texto -notmatch "STRINGS_TESTE $strings string") {
        $problemas.Add("clivus-eletrica-contrato: $strings string(s) de teste para $modulos modulo(s). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (contrato eletrico: $strings string(s) de teste em $modulos modulo(s))" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaContrato'
