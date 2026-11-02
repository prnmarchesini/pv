<#
    Os casos de nivel 2 da ponte com o TermoVision (plano/termovision, etapa
    33). O rodar.ps1 carrega este arquivo, e o caso entra em $CasosTermoVision.

    Abre o pacote real da Paradise (tests/acervo/etapa-33/paradise_tela.zip)
    sem rede, poe uma fileira dentro da area que veio dele e confere o corpo do
    retorno: tudo no terreno do pacote (regra: tudo que desenha acompanha o
    terreno), cantos na ordem do contrato.
#>
$script:CasosTermoVision = @()

function Testar-TermoVision {
    param([string] $Desenho)

    $rotulo = 'clivus-tv'
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $zip = Join-Path $raiz 'tests\acervo\etapa-33\paradise_tela.zip'

    # O que o pacote diz: a area e a faixa de cotas do terreno.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $arquivo = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $leitor = New-Object IO.StreamReader(($arquivo.Entries | Where-Object FullName -eq 'clivus.json').Open())
        $pacote = $leitor.ReadToEnd() | ConvertFrom-Json
        $leitor.Close()
    }
    finally { $arquivo.Dispose() }

    $cotas = @($pacote.terreno.z | Where-Object { $null -ne $_ })
    $zMin = ($cotas | Measure-Object -Minimum).Minimum
    $zMax = ($cotas | Measure-Object -Maximum).Maximum
    $area = $pacote.area_utm
    $ce = ($area | ForEach-Object { $_[0] } | Measure-Object -Average).Average
    $cn = ($area | ForEach-Object { $_[1] } | Measure-Object -Average).Average
    $oeste = @($area | Sort-Object { $_[0] } | Select-Object -First 2 | Sort-Object { $_[1] })

    function P3([double] $e, [double] $n) { [string]::Format($inv, '{0:0.###},{1:0.###},0', $e, $n) }

    $json = Join-Path $saida 'nivel2-clivus-tv-retorno.json'
    if (Test-Path $json) { Remove-Item $json -Force }
    $projeto = Join-Path $saida 'nivel2-clivus-tv-projeto.json'
    if (Test-Path $projeto) { Remove-Item $projeto -Force }

    # O alinhamento pela borda oeste da area, 3 m para dentro; o lado e o meio da area.
    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-tv.scr') -Substituicoes @{
        '{{ZIP}}'  = $zip
        '{{L1}}'   = (P3 ($oeste[0][0] + 3) ($oeste[0][1] + 3))
        '{{L2}}'   = (P3 ($oeste[1][0] + 3) ($oeste[1][1] - 3))
        '{{LADO}}' = (P3 $ce $cn)
        '{{JSON}}' = $json
        '{{PROJETO}}' = $projeto
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_TV_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    if ($t -notmatch 'CLIVUS_TV MONTADO superficie=(\d+) orto=(\d) arvores=(\d+) envio=cv_1b152a3a5ce8b700') {
        $erros += 'a montagem nao terminou'
    }
    else {
        if ([int] $Matches[1] -ne $cotas.Count) { $erros += "a superficie tem $($Matches[1]) pontos; o pacote tem $($cotas.Count) nos com cota" }
        if ($Matches[2] -ne '1') { $erros += 'o orto nao foi posto' }
        if ([int] $Matches[3] -ne $pacote.objetos.Count) { $erros += "esperava $($pacote.objetos.Count) arvore(s), vieram $($Matches[3])" }
    }

    if ($t -notmatch 'CLIVUS_TV_LISP imagens=(\d+) arvores=(\d+) objetos=(\d+) areas=(\d+) v1=(-?[\d.]+),(-?[\d.]+),(-?[\d.]+)') {
        $erros += 'o LISP nao contou o desenho'
    }
    else {
        if ([int] $Matches[1] -ne 1) { $erros += "esperava 1 imagem do orto, ha $($Matches[1])" }
        if ([int] $Matches[2] -lt $pacote.objetos.Count) { $erros += 'faltam arvores no desenho' }
        if ([int] $Matches[3] -ne $pacote.objetos.Count) { $erros += 'faltam os contornos dos objetos' }
        if ([int] $Matches[4] -lt 1) { $erros += 'a area nao foi criada' }
        $zVertice = [double]::Parse($Matches[7], $inv)

        # A cota do terreno ali (CLIVUS_COORD, numero na cultura da tela).
        # A cota do terreno ali (CLIVUS_COORD, numero na cultura da tela): 1a no vertice, 2a no pe da arvore.
        $coords = [regex]::Matches($t, '(?m)^\s+X [\d\.,]+\s+Y [\d\.,]+\s+Z ([\d\.,-]+)\s*$')
        if ($coords.Count -lt 2) { $erros += 'o CLIVUS_COORD nao respondeu no vertice da area e na arvore' }
        else {
            $zTerreno = [double]::Parse($coords[0].Groups[1].Value.Replace('.', '').Replace(',', '.'), $inv)
            if ([Math]::Abs($zTerreno - $zVertice) -gt 0.01) { $erros += "o vertice da area esta na cota $zVertice e o terreno ali e $zTerreno" }
            if ($t -notmatch 'CLIVUS_TV_ARVORE z=(-?[\d.]+)') { $erros += 'o LISP nao leu a arvore' }
            else {
                $zArvore = [double]::Parse($Matches[1], $inv)
                $zChao = [double]::Parse($coords[1].Groups[1].Value.Replace('.', '').Replace(',', '.'), $inv)
                if ([Math]::Abs($zArvore - $zChao) -gt 0.01) { $erros += "a arvore esta na cota $zArvore e o terreno ali e $zChao" }
            }
        }
    }

    if ($t -notmatch 'CLIVUS_TV EXPORTADO mesas=(\d+) recusadas=(\d+)') { $erros += 'o exportar nao respondeu' }
    elseif ([int] $Matches[1] -lt 1) { $erros += 'nenhuma mesa foi para o retorno' }
    elseif ([int] $Matches[2] -ne 0) { $erros += "$($Matches[2]) mesa(s) recusada(s) pelo exportar" }

    if (Test-Path $json) {
        $retorno = Get-Content $json -Raw -Encoding utf8 | ConvertFrom-Json
        if ($retorno.versao -ne 1) { $erros += 'o retorno nao e da versao 1' }
        foreach ($m in $retorno.mesas) {
            if (@($m.cantos).Count -ne 4) { $erros += "$($m.nome): $(@($m.cantos).Count) cantos"; continue }
            $z = @($m.cantos | ForEach-Object { $_[2] })
            if (@($z | Where-Object { $_ -lt $zMin - 0.5 -or $_ -gt $zMax + 6 }).Count -gt 0) { $erros += "$($m.nome): canto fora das cotas do terreno ($zMin a $zMax)" }
            if (($z[0] + $z[1]) / 2 -gt ($z[2] + $z[3]) / 2 + 1e-6) { $erros += "$($m.nome): a borda alta veio primeiro" }
            if ($m.colunas -lt 1 -or $m.fiadas -lt 1) { $erros += "$($m.nome): $($m.colunas) colunas, $($m.fiadas) fiadas" }
            if ($m.modulo.largura_m -le 0 -or $m.modulo.altura_m -le 0) { $erros += "$($m.nome): modulo sem medida" }
            if ($m.guid -notmatch '^[0-9a-f-]{36}$') { $erros += "$($m.nome): guid $($m.guid)" }
        }
    }
    else { $erros += 'o arquivo do retorno nao foi gravado' }

    # 33.10: o projeto sem envio leva o suporte de cada mesa fixa (os pilares e as barras), e o pe de
    # cada pilar fica abaixo da grade do terreno que vai junto (regra: tudo que desenha acompanha o
    # terreno; no 3D do TermoVision o pilar nao pode flutuar). Os pilares vem primeiro, de 8 em 8 triangulos.
    $pilares = 0
    if ($t -notmatch 'CLIVUS_TV PROJETO mesas=(\d+) recusadas=(\d+) .*estrutura=(\S+) triangulos=(\d+)') { $erros += 'o projeto sem envio nao respondeu' }
    elseif ([int] $Matches[1] -lt 1 -or [int] $Matches[2] -ne 0 -or $Matches[3] -eq '-' -or [int] $Matches[4] -lt 8) { $erros += "o projeto saiu sem a estrutura das mesas ($($Matches[0]))" }
    if (Test-Path $projeto) {
        $p = Get-Content $projeto -Raw -Encoding utf8 | ConvertFrom-Json
        $g = $p.terreno
        function Grade([double] $e, [double] $n) {
            $i = [Math]::Floor(($e - $g.e0) / $g.passo_m); $j = [Math]::Floor(($n - $g.n0) / $g.passo_m)
            if ($i -lt 0 -or $j -lt 0 -or $i + 1 -ge $g.colunas -or $j + 1 -ge $g.linhas) { return $null }
            $nos = @(0, 1) | ForEach-Object { $dj = $_; @(0, 1) | ForEach-Object { $g.z[($j + $dj) * $g.colunas + $i + $_] } }
            if (@($nos | Where-Object { $null -eq $_ }).Count -gt 0) { return $null }
            $fi = ($e - $g.e0) / $g.passo_m - $i; $fj = ($n - $g.n0) / $g.passo_m - $j
            return ($nos[0] * (1 - $fi) + $nos[1] * $fi) * (1 - $fj) + ($nos[2] * (1 - $fi) + $nos[3] * $fi) * $fj
        }
        foreach ($m in @($p.mesas | Where-Object { $_.tipo -eq 'fixa' })) {
            $tri = @($m.estrutura.triangulos)
            if ($tri.Count -lt 8) { $erros += "$($m.nome): o projeto foi sem a estrutura"; continue }
            for ($k = 0; $k + 8 -le $tri.Count; $k += 8) {
                $v = @($tri[$k..($k + 7)])
                $xs = @($v | ForEach-Object { $_[0], $_[3], $_[6] }); $ys = @($v | ForEach-Object { $_[1], $_[4], $_[7] }); $zs = @($v | ForEach-Object { $_[2], $_[5], $_[8] })
                $mx = $xs | Measure-Object -Minimum -Maximum; $my = $ys | Measure-Object -Minimum -Maximum; $mz = $zs | Measure-Object -Minimum -Maximum
                if ($mx.Maximum - $mx.Minimum -gt 0.6 -or $my.Maximum - $my.Minimum -gt 0.6 -or $mz.Maximum - $mz.Minimum -lt 0.3) { break }
                $pilares++
                $chao = Grade (($mx.Maximum + $mx.Minimum) / 2) (($my.Maximum + $my.Minimum) / 2)
                if ($null -eq $chao) { $erros += "$($m.nome): pilar fora da grade do terreno"; continue }
                if ($mz.Minimum -gt $chao - 0.25) { $erros += "$($m.nome): o pe do pilar esta na cota $($mz.Minimum) e a grade ali e $chao (flutua no 3D)" }
                if ($mz.Maximum -le $chao) { $erros += "$($m.nome): o topo do pilar esta abaixo do terreno" }
            }
        }
        if ($pilares -lt 1) { $erros += 'nenhum pilar no corpo do projeto' }
    }
    else { $erros += 'o corpo do projeto nao foi gravado' }

    if ($erros.Count -gt 0) {
        foreach ($e in $erros) { $problemas.Add("${rotulo}: $e. Veja $($r.Saida)") }
        return $false
    }

    Write-Host "  (TermoVision: pacote da Paradise aberto sem rede, $($cotas.Count) pontos, orto, $($pacote.objetos.Count) arvore(s), area no TIN; $(@($retorno.mesas).Count) mesa(s) no retorno, borda baixa primeiro, cotas no terreno; projeto com $pilares pilar(es) com o pe abaixo do terreno)" -ForegroundColor DarkGray
    return $true
}

$script:CasosTermoVision += 'Testar-TermoVision'
