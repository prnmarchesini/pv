<#
    Os casos de nivel 2 das mesas conforme os objetos do cliente (plano/bifila,
    etapa 40). O rodar.ps1 carrega este arquivo, e os casos entram em
    $CasosCampo.

    Testar-Campo (40.2): no terreno de teste, oito objetos do cliente (3DPOLY
    fechadas de 4 vertices, com Z 9999): duas fileiras de tres, um repetido e
    um curto. O CLIVUS_USINA_OBJETOS_AUTO tem que pôr seis mesas, cada uma no
    seu objeto, com os pilares no terreno; os objetos ficam como estavam; o
    repetido e o curto saem marcados; e o Regerar area refaz pelos objetos.
#>
$script:CasosCampo = @('Testar-Campo')

function Testar-Campo {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-campo--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'cotaMin=(-?[\d.]+) cotaMax=(-?[\d.]+) pontos=\d+ centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-campo: nao achei o terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cotaMin = [double]::Parse($Matches[1], $inv)
    $cotaMax = [double]::Parse($Matches[2], $inv)
    $centroX = [double]::Parse($Matches[3], $inv)
    $centroY = [double]::Parse($Matches[4], $inv)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($inv, '{0:0.####},{1:0.####},{2:0.####}', $centroX + $dx, $centroY + $dy, $z)
    }

    # O objeto: a area dos modulos de uma 2P14 (18,502 x 4,788 m em 3D), 10
    # graus, o lado sul (alto) em y0 e o norte (a ponta baixa) 4,715 m acima.
    $grau = [Math]::PI / 180
    $plano = 4.788 * [Math]::Cos(10 * $grau)
    $sobe = 4.788 * [Math]::Sin(10 * $grau)

    function Objeto([double] $x0, [double] $y0, [double] $comprimento) {
        $p = @(
            (Ponto3 $x0 $y0 (9999 + $sobe)),
            (Ponto3 ($x0 + $comprimento) $y0 (9999 + $sobe)),
            (Ponto3 ($x0 + $comprimento) ($y0 + $plano) 9999),
            (Ponto3 $x0 ($y0 + $plano) 9999))
        '(command "_.3DPOLY" "' + ($p -join '" "') + '" "_C")'
    }

    $passo = 18.502 + 0.35
    $objetos = @(
        (Objeto -30 10 18.502), (Objeto (-30 + $passo) 10 18.502), (Objeto (-30 + 2 * $passo) 10 18.502),
        (Objeto -30 0 18.502), (Objeto (-30 + $passo) 0 18.502), (Objeto (-30 + 2 * $passo) 0 18.502),
        (Objeto -30 10 18.502),
        (Objeto -30 -15 9.3))

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-campo' `
        -Script (Join-Path $PSScriptRoot 'clivus-campo.scr') `
        -Substituicoes @{
            '{{A1}}'      = (Ponto3 -50 -50 0)
            '{{A2}}'      = (Ponto3  50 -50 0)
            '{{A3}}'      = (Ponto3  50  50 0)
            '{{A4}}'      = (Ponto3 -50  50 0)
            '{{OBJETOS}}' = ($objetos -join "`r`n")
            '{{ZMIN}}'    = $cotaMin.ToString('0.###', $inv)
            '{{ZMAX}}'    = $cotaMax.ToString('0.###', $inv)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-campo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $ok = $true

    if ($r.Texto -notmatch '8 objeto\(s\) lido\(s\), 6 viram mesa') {
        $problemas.Add("clivus-campo: o relatorio nao diz 8 objetos lidos e 6 mesas. Veja $($r.Saida)")
        $ok = $false
    }

    if ($r.Texto -notmatch '1 repetido' -or $r.Texto -notmatch '1 sem mesa do tamanho') {
        $problemas.Add("clivus-campo: o relatorio nao aponta o repetido e o curto. Veja $($r.Saida)")
        $ok = $false
    }

    if ($r.Texto -notmatch 'CLIVUS_CAMPO mesas=(\d+) pertos=(\d+) pilares=(\d+) pilaresok=(\d+) objetos=(\d+) marcas=(\d+)') {
        $problemas.Add("clivus-campo: o LISP nao respondeu. Veja $($r.Saida)")
        return $false
    }

    $mesas, $pertos, $pilares, $pilaresOk, $lidos, $marcas = $Matches[1..6] | ForEach-Object { [int] $_ }

    # Seis mesas, cada uma no seu objeto; doze pilares por mesa (a bifila),
    # todos na faixa do terreno (nenhum no Z 9999 dos objetos); os oito
    # objetos do cliente intactos; o repetido e o curto marcados.
    if ($mesas -ne 6 -or $pertos -ne 6 -or $pilares -ne 72 -or $pilaresOk -ne 72 -or $lidos -ne 8 -or $marcas -ne 2) {
        $problemas.Add("clivus-campo: mesas=$mesas (6), no objeto=$pertos (6), pilares=$pilares (72), na faixa do terreno=$pilaresOk (72), objetos do cliente=$lidos (8), marcas=$marcas (2). Veja $($r.Saida)")
        $ok = $false
    }

    if ($r.Texto -notmatch 'CLIVUS_CAMPO2 mesas=(\d+) pertos=(\d+) objetos=(\d+) marcas=(\d+)') {
        $problemas.Add("clivus-campo: o Regerar area nao respondeu. Veja $($r.Saida)")
        return $false
    }

    $mesas2, $pertos2, $lidos2, $marcas2 = $Matches[1..4] | ForEach-Object { [int] $_ }

    if ($mesas2 -ne 6 -or $pertos2 -ne 6 -or $lidos2 -ne 8 -or $marcas2 -ne 2 -or $r.Texto -notmatch 'REFAZER .+ segue os objetos do cliente') {
        $problemas.Add("clivus-campo: depois do Regerar area, mesas=$mesas2 (6), no objeto=$pertos2 (6), objetos=$lidos2 (8), marcas=$marcas2 (2), ou o Regerar nao foi pelos objetos. Veja $($r.Saida)")
        $ok = $false
    }

    if ($r.Texto -notmatch 'CLIVUS_SECURELOAD_ANTES=(\d+) DEPOIS=(\d+)' -or $Matches[1] -ne $Matches[2]) {
        $problemas.Add("clivus-campo: o SECURELOAD nao voltou ao que era. Veja $($r.Saida)")
        return $false
    }

    return $ok
}
