<#
    Usina mista (mesas de 28 e de 14 na mesma fileira), para os casos de
    gerar strings (11.6 a 11.8): area de 90 m de largura.
#>
function Substituicoes-Da-Usina-Mista {
    param([string] $Desenho, [string] $Rotulo)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { return $null }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###},0', $cx + $dx, $cy + $dy) }

    return @{
        '{{A1}}' = (P -45 -50); '{{A2}}' = (P 45 -50); '{{A3}}' = (P 45 50); '{{A4}}' = (P -45 50)
        '{{L1}}' = (P -45 -50); '{{L2}}' = (P -45 50); '{{LADO}}' = (P 0 0)
    }
}

<#
    11.6: a aba Gerar casa cada tipo com grupos de assinatura igual. Modelo 1
    (uma de 28) so cai em mesa de 28 sozinha; Modelo 2 (uma de 14) so em
    mesa de 14; Modelo 3 (duas de 28 vizinhas) so em duas de 28 seguidas da
    mesma fileira, e tem a preferencia. Toda mesa cai em exatamente um grupo.
    O perfil de cada mesa vem do XData lido em LISP.
#>
function Testar-StringGerar {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina-Mista -Desenho $Desenho -Rotulo 'clivus-string-gerar'
    if (-not $sub) { $problemas.Add('clivus-string-gerar: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-string-gerar' -Script (Join-Path $PSScriptRoot 'clivus-string-gerar.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_STRING_FIM') -lt 0) {
        $problemas.Add("clivus-string-gerar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    $perfil = @{}
    foreach ($m in [regex]::Matches($t, 'CLIVUS_MESA (F\d+\.\d+) (Mesa 2V(?:28|14))')) { $perfil[$m.Groups[1].Value] = $m.Groups[2].Value }
    $de14 = @($perfil.Keys | Where-Object { $perfil[$_] -eq 'Mesa 2V14' }).Count
    if ($perfil.Count -lt 10 -or $de14 -lt 1) { $erros += "a usina mista nao saiu ($($perfil.Count) mesas, $de14 de 14)" }

    $vistas = @{}
    $porModelo = @{ 'Modelo 1' = 0; 'Modelo 2' = 0; 'Modelo 3' = 0 }
    foreach ($m in [regex]::Matches($t, 'STRING_GERAR (Modelo \d) \S (F[\d., F]+): (\d+) string')) {
        $modelo = $m.Groups[1].Value
        $rotulos = @($m.Groups[2].Value -split ', ')
        $porModelo[$modelo]++
        foreach ($x in $rotulos) { $vistas[$x] = 1 + [int]$vistas[$x] }

        switch ($modelo) {
            'Modelo 1' { if ($rotulos.Count -ne 1 -or $perfil[$rotulos[0]] -ne 'Mesa 2V28') { $erros += "Modelo 1 caiu em $($m.Groups[2].Value)" } }
            'Modelo 2' { if ($rotulos.Count -ne 1 -or $perfil[$rotulos[0]] -ne 'Mesa 2V14') { $erros += "Modelo 2 caiu em $($m.Groups[2].Value)" } }
            'Modelo 3' {
                $ok = $rotulos.Count -eq 2 -and $perfil[$rotulos[0]] -eq 'Mesa 2V28' -and $perfil[$rotulos[1]] -eq 'Mesa 2V28'
                if ($ok) {
                    $a = $rotulos[0] -split '\.'; $b = $rotulos[1] -split '\.'
                    $ok = $a[0] -eq $b[0] -and [math]::Abs([int]$a[1] - [int]$b[1]) -eq 1
                }
                if (-not $ok) { $erros += "Modelo 3 caiu em $($m.Groups[2].Value)" }
            }
        }
    }

    if ($porModelo['Modelo 3'] -lt 1 -or $porModelo['Modelo 2'] -lt 1) { $erros += "faltou grupo de algum tipo (M1 $($porModelo['Modelo 1']), M2 $($porModelo['Modelo 2']), M3 $($porModelo['Modelo 3']))" }
    $fora = @($perfil.Keys | Where-Object { [int]$vistas[$_] -ne 1 })
    if ($fora.Count -gt 0) { $erros += "mesa fora de grupo ou em dois: $($fora -join ', ')" }
    if ($t -notmatch 'STRING_GERAR \d+ string\(s\) em \d+ grupo\(s\) de mesas; 0 mesa\(s\) sem tipo; 0 grupo') { $erros += 'o resumo da geracao nao diz zero mesa sem tipo' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-string-gerar: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (strings 11.6: $($perfil.Count) mesas; grupos M1 $($porModelo['Modelo 1']), M2 $($porModelo['Modelo 2']), M3 $($porModelo['Modelo 3']), cada um com a assinatura dele)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-StringGerar'
