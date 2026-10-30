<#
    Os casos de nivel 2 da janela Areas (etapa 41). O rodar.ps1 carrega este
    arquivo, e os casos entram em $CasosAreas.

    Testar-Areas (41): duas areas lado a lado (Oeste e Leste, 50 x 100 m), a
    fileira 1 so na Oeste. Pelo caminho dos botoes da janela (o gancho
    clivus-areas-pela-janela, fora de comando):
      - o resumo: a Oeste com as mesas, a Leste vazia, o total igual ao
        Recontar, a area em planta de cada uma (0,5 ha);
      - hachurar todas: duas hachuras e dois nomes, a cota de cada uma na
        faixa do terreno, o nome da Oeste acima do topo das mesas dela (regra
        sagrada 5, lida da entidade em LISP), nenhum modulo vermelho de
        pendente depois do REGEN (o vigia calado), e a lista dizendo
        "hachurada" nas duas;
      - renomear: o nome novo no XData da polilinha e no letreiro;
      - tirar a hachura de uma, o Excel por area, tirar todas.
#>
$script:CasosAreas = @('Testar-Areas')

function Testar-Areas {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-areas--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'cotaMin=(-?[\d.]+) cotaMax=(-?[\d.]+) pontos=\d+ centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-areas: nao achei o terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cotaMin = [double]::Parse($Matches[1], $inv)
    $cotaMax = [double]::Parse($Matches[2], $inv)
    $centroX = [double]::Parse($Matches[3], $inv)
    $centroY = [double]::Parse($Matches[4], $inv)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($inv, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $planilha = Join-Path $saida 'clivus-areas.xlsx'
    if (Test-Path $planilha) { Remove-Item $planilha -Force }

    # Os cliques com Z 9999: area e hachura no Z do clique estao erradas.
    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-areas' `
        -Script (Join-Path $PSScriptRoot 'clivus-areas.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 9999)
            '{{A2}}'   = (Ponto3   0 -50 9999)
            '{{A3}}'   = (Ponto3   0  50 9999)
            '{{A4}}'   = (Ponto3 -50  50 9999)
            '{{B1}}'   = (Ponto3   0 -50 9999)
            '{{B2}}'   = (Ponto3  50 -50 9999)
            '{{B3}}'   = (Ponto3  50  50 9999)
            '{{B4}}'   = (Ponto3   0  50 9999)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3 -25   0 0)
            '{{XLSX}}' = $planilha
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-areas terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $erros = @()

    if ($r.Texto -notmatch 'RECONTAR_TOTAIS mesas=(\d+)') {
        $problemas.Add("clivus-areas: o Recontar nao contou. Veja $($r.Saida)")
        return $false
    }
    $recontar = [int] $Matches[1]

    # Uma linha do resumo: mesas e area em planta.
    function Linha([string] $bloco, [string] $tipo, [string] $nome) {
        $m = [regex]::Match($bloco, "AREAS_LINHA tipo=$tipo nome=$([regex]::Escape($nome)) ha=(-?[\d.]+) mesas=(\d+) modulos=(\d+) kwp=(-?[\d.]+) pilares=(\d+) metros=(-?[\d.]+) divisa=(\d+) ocupacao=(-?[\d.]+) objetos=(\d) hachura=(\d) fim")
        if (-not $m.Success) { return $null }
        return [pscustomobject]@{
            Ha = [double]::Parse($m.Groups[1].Value, $inv); Mesas = [int] $m.Groups[2].Value; Modulos = [int] $m.Groups[3].Value
            Pilares = [int] $m.Groups[5].Value; Hachura = [int] $m.Groups[10].Value
        }
    }

    function Bloco([string] $de, [string] $ate) {
        $i = $r.Texto.IndexOf($de)
        $j = $r.Texto.IndexOf($ate)
        if ($i -lt 0 -or $j -lt $i) { return '' }
        return $r.Texto.Substring($i, $j - $i)
    }

    $ler1 = Bloco 'CLIVUS_AREAS_LER1' 'CLIVUS_AREAS_FIM_LER1'
    $oeste = Linha $ler1 'Area' 'Area Oeste'
    $leste = Linha $ler1 'Area' 'Area Leste'
    $total = Linha $ler1 'Total' 'Total'

    if ($null -eq $oeste -or $null -eq $leste -or $null -eq $total) {
        $problemas.Add("clivus-areas: o resumo nao listou Oeste, Leste e Total. Veja $($r.Saida)")
        return $false
    }

    if ($oeste.Mesas -le 0 -or $leste.Mesas -ne 0 -or $total.Mesas -ne $recontar -or ($oeste.Mesas + $leste.Mesas) -ne $total.Mesas -or $ler1 -match 'tipo=Outside') {
        $erros += "resumo: Oeste $($oeste.Mesas) (>0), Leste $($leste.Mesas) (0), Total $($total.Mesas) (Recontar $recontar), sem linha de fora das areas"
    }

    if ([Math]::Abs($oeste.Ha - 0.5) -gt 0.005 -or [Math]::Abs($leste.Ha - 0.5) -gt 0.005 -or [Math]::Abs($total.Ha - 1.0) -gt 0.01) {
        $erros += "area em planta: Oeste $($oeste.Ha) ha, Leste $($leste.Ha) ha, Total $($total.Ha) ha (esperava 0,5, 0,5 e 1)"
    }

    if ($r.Texto -notmatch 'CLIVUS_AREAS_HACHURA hachuras=(\d+) nomes=(\d+) cotamin=(\S+) cotamax=(\S+) oeste=(\S+) topo=(\S+) vermelhos=(\d+) fim') {
        $problemas.Add("clivus-areas: o LISP nao leu as hachuras. Veja $($r.Saida)")
        return $false
    }

    $hachuras = [int] $Matches[1]; $nomes = [int] $Matches[2]; $vermelhos = [int] $Matches[7]
    $cotaHMin = $Matches[3]; $cotaHMax = $Matches[4]; $nomeOeste = $Matches[5]; $topo = $Matches[6]

    if ($hachuras -ne 2 -or $nomes -ne 2) {
        $erros += "hachurar todas deixou $hachuras hachura(s) e $nomes nome(s) (esperava 2 e 2)"
    }
    elseif ($cotaHMin -eq 'nil' -or $nomeOeste -eq 'nil' -or $topo -eq 'nil') {
        $erros += "sem cota para conferir (hachura $cotaHMin, nome da Oeste $nomeOeste, topo das mesas $topo)"
    }
    else {
        $zMin = [double]::Parse($cotaHMin, $inv); $zMax = [double]::Parse($cotaHMax, $inv)
        $zNome = [double]::Parse($nomeOeste, $inv); $zTopo = [double]::Parse($topo, $inv)

        # Na faixa do terreno (mais a altura das mesas), nunca no Z 9999 dos cliques.
        if ($zMin -lt ($cotaMin - 1) -or $zMax -gt ($cotaMax + 10)) {
            $erros += "cota das hachuras de $zMin a $zMax fora da faixa do terreno ($cotaMin a $cotaMax)"
        }

        # A hachura da Oeste (o nome dela fica 5 cm acima) passa por cima das mesas.
        if ($zNome - 0.05 -lt $zTopo + 0.29) {
            $erros += "a hachura da Oeste ($($zNome - 0.05)) nao ficou acima do topo das mesas ($zTopo) com a folga de 0,30 m"
        }
    }

    if ($vermelhos -gt 0) { $erros += "$vermelhos modulo(s) vermelhos de pendente (o vigia tomou a hachura por edicao)" }
    if ((Bloco 'CLIVUS_AREAS_DEPOIS_HACHURA' 'CLIVUS_AREAS_LER2') -match 'pendente') { $erros += 'o vigia escreveu "pendente" depois da hachura' }

    $ler2 = Bloco 'CLIVUS_AREAS_LER2' 'CLIVUS_AREAS_FIM_LER2'
    $oeste2 = Linha $ler2 'Area' 'Area Oeste'
    $leste2 = Linha $ler2 'Area' 'Area Leste'
    if ($null -eq $oeste2 -or $null -eq $leste2 -or $oeste2.Hachura -ne 1 -or $leste2.Hachura -ne 1) {
        $erros += 'a lista nao diz que as duas areas estao hachuradas'
    }

    if ($r.Texto -notmatch 'CLIVUS_AREAS_RENOMEAR novo=(\d+) velho=(\d+) letreiro=(\d) fim' -or $Matches[1] -ne '1' -or $Matches[2] -ne '0' -or $Matches[3] -ne '1') {
        $erros += 'renomear: o XData da polilinha ou o letreiro nao ficaram com o nome novo'
    }

    if ($r.Texto -notmatch 'CLIVUS_AREAS_TIRAR hachuras=(\d+) nomes=(\d+) oeste=(\d) fim' -or $Matches[1] -ne '1' -or $Matches[2] -ne '1' -or $Matches[3] -ne '0') {
        $erros += 'tirar a hachura da Oeste nao deixou so a da Leste'
    }

    if ($r.Texto -notmatch 'CLIVUS_AREAS_TIRAR_TODAS hachuras=(\d+) nomes=(\d+) fim' -or $Matches[1] -ne '0' -or $Matches[2] -ne '0') {
        $erros += 'tirar todas deixou hachura ou nome no desenho'
    }

    # O Excel por area: o Resumo e uma aba por area, com o nome novo.
    if (-not (Test-Path $planilha)) {
        $erros += "o Excel nao foi gravado em $planilha"
    }
    else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($planilha)
        try {
            $leitor = New-Object IO.StreamReader($zip.GetEntry('xl/workbook.xml').Open())
            try { $pasta = [xml] $leitor.ReadToEnd() } finally { $leitor.Dispose() }
        }
        finally { $zip.Dispose() }

        $abas = @($pasta.workbook.sheets.sheet | ForEach-Object { $_.name }) -join '|'
        if ($abas -ne 'Resumo|Area Oeste|Area Leste 2') { $erros += "o Excel tem as abas [$abas] (esperava Resumo|Area Oeste|Area Leste 2)" }
    }

    if ($r.Texto -notmatch 'CLIVUS_SECURELOAD_ANTES=(\d+) DEPOIS=(\d+)' -or $Matches[1] -ne $Matches[2]) {
        $problemas.Add("clivus-areas: o SECURELOAD nao voltou ao que era. Veja $($r.Saida)")
        return $false
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-areas: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (areas: Oeste $($oeste.Mesas) mesa(s), Leste vazia, total = Recontar; hachuras de $cotaHMin a $cotaHMax m, acima das mesas ($topo m); renomear, tirar e Excel)" -ForegroundColor DarkGray
    return $true
}
