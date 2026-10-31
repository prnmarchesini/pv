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
$script:CasosAreas = @('Testar-Areas', 'Testar-AcoesDaArea')

<#
    Testar-AcoesDaArea (30/10/2026): as acoes por area da janela Areas, pelos
    comandos que os botoes mandam, e a area achada mesmo com a linha do
    cliente por cima. A area repetida e recusada; o Recalcular clicado na
    linha sobreposta acha a Oeste e recalcula as mesas dela; o Apagar area
    apaga a Leste vazia sem perguntar e a Oeste com mesas so com o Sim; o
    registro fica limpo (Validar), e o U devolve tudo.
#>
function Testar-AcoesDaArea {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-areas-acoes--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-areas-acoes: nao achei o terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $inv)
    $centroY = [double]::Parse($Matches[2], $inv)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($inv, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-areas-acoes' `
        -Script (Join-Path $PSScriptRoot 'clivus-areas-acoes.scr') `
        -Substituicoes @{
            '{{A1}}' = (Ponto3 -50 -50 0); '{{A2}}' = (Ponto3 0 -50 0); '{{A3}}' = (Ponto3 0 50 0); '{{A4}}' = (Ponto3 -50 50 0)
            '{{B1}}' = (Ponto3 0 -50 0); '{{B2}}' = (Ponto3 50 -50 0); '{{B3}}' = (Ponto3 50 50 0); '{{B4}}' = (Ponto3 0 50 0)
            '{{L1}}' = (Ponto3 -50 -50 0); '{{L2}}' = (Ponto3 -50 50 0); '{{LADO}}' = (Ponto3 -25 0 0)
            # A linha do cliente em cima da Oeste, com cotas estragadas.
            '{{C1}}' = (Ponto3 -50 -50 9999); '{{C2}}' = (Ponto3 0 -50 -40); '{{C3}}' = (Ponto3 0 50 777); '{{C4}}' = (Ponto3 -50 50 0)
            '{{NA_LINHA}}' = (Ponto3 -50 10 0)
            '{{DENTRO_LESTE}}' = (Ponto3 25 0 0)
            # A area 1 m maior por fora da Oeste (31/10/2026) e um ponto na linha dela.
            '{{G1}}' = (Ponto3 -51 -51 0); '{{G2}}' = (Ponto3 1 -51 0); '{{G3}}' = (Ponto3 1 51 0); '{{G4}}' = (Ponto3 -51 51 0)
            '{{NA_LINHA_G}}' = (Ponto3 -51 10 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto -notmatch 'CLIVUS_FIM') {
        $problemas.Add("clivus-areas-acoes terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    function Conta([string] $rotulo) {
        if ($t -match "CLIVUS_ACOES $rotulo areas=(\d+) mesas=(\d+)") { return @([int] $Matches[1], [int] $Matches[2]) }
        return $null
    }

    $inicio = Conta 'INICIO'
    if ($null -eq $inicio -or $inicio[0] -ne 2 -or $inicio[1] -lt 2) {
        $problemas.Add("clivus-areas-acoes: o comeco nao tem as duas areas e a fileira na Oeste. Veja $($r.Saida)")
        return $false
    }
    $mesas = $inicio[1]
    $ok = $true

    function Espera([string] $rotulo, [int] $areas, [int] $nMesas, [string] $oQue) {
        $c = Conta $rotulo
        if ($null -eq $c -or $c[0] -ne $areas -or $c[1] -ne $nMesas) {
            $problemas.Add("clivus-areas-acoes: $oQue (esperava $areas area(s) e $nMesas mesa(s); veio $($c -join ' e ')). Veja $($r.Saida)")
            return $false
        }
        return $true
    }

    if ($t -notmatch 'J. existe a .rea "Area Oeste" com este contorno' -or -not (Espera 'REPETIDA' 2 $mesas 'a area repetida nao foi recusada')) { $ok = $false }

    $naLinha = $t.Substring($t.IndexOf('CLIVUS_ACOES RECALCULAR_NA_LINHA'))
    if ($naLinha -notmatch '.rea: Area Oeste\.' -or $naLinha -notmatch "RECALCULAR $mesas mesa\(s\) de Area Oeste" -or $naLinha -notmatch "RECALCULAR $mesas de $mesas mesa\(s\) recalculada\(s\)") {
        $problemas.Add("clivus-areas-acoes: o clique na linha do cliente por cima da Oeste nao achou a Oeste, ou nao recalculou as $mesas mesas dela. Veja $($r.Saida)")
        $ok = $false
    }

    if ($t -notmatch 'Nenhuma mesa dentro de Area Leste' -or -not (Espera 'RECALCULADO' 2 $mesas 'o recalcular mudou as areas ou as mesas')) { $ok = $false }

    # 31/10/2026: a area de fora, quase igual a Oeste, sai sozinha; as mesas sao da Oeste e ficam.
    if (-not (Espera 'COM_GEMEA' 3 $mesas 'a area 1 m maior por fora da Oeste nao foi criada')) { $ok = $false }
    $gemea = $t.Substring($t.IndexOf('CLIVUS_ACOES APAGAR_GEMEA'))
    $gemea = $gemea.Substring(0, [Math]::Max(0, $gemea.IndexOf('CLIVUS_ACOES SEM_GEMEA')))
    if ($gemea -notmatch 'AREA Area Oeste B apagada' -or $gemea -match 'Apagar\?' -or -not (Espera 'SEM_GEMEA' 2 $mesas 'apagar a area de fora levou mesa da Oeste (ou perguntou)')) { $ok = $false }

    if ($t -notmatch 'AREA Area Leste apagada' -or -not (Espera 'SEM_LESTE' 1 $mesas 'a Leste vazia nao foi apagada')) { $ok = $false }

    if ($t -notmatch 'Nada foi apagado' -or -not (Espera 'OESTE_FICOU' 1 $mesas 'o Enter (Nao) apagou a Oeste')) { $ok = $false }

    if ($t -notmatch 'AREA Area Oeste apagada' -or -not (Espera 'SEM_AREAS' 0 0 'o Sim nao apagou a Oeste e as mesas')) { $ok = $false }

    if ($t -notmatch 'VALIDAR_TOTAIS areas=0 ') {
        $problemas.Add("clivus-areas-acoes: depois de apagar, o registro ainda tem area que nao existe (Validar). Veja $($r.Saida)")
        $ok = $false
    }

    if (-not (Espera 'DEPOIS_DO_U' 1 $mesas 'o U nao devolveu a Oeste e as mesas')) { $ok = $false }

    # 31/10/2026: o "todas as areas" (Limpar, U, Regerar).
    if ($t -notmatch "AREAS $mesas mesa\(s\) apagada\(s\) em 1 .rea" -or -not (Espera 'TODAS_LIMPAS' 1 0 'Limpar todas nao tirou as mesas (ou tirou a area)')) { $ok = $false }
    if (-not (Espera 'LIMPAS_DESFEITO' 1 $mesas 'o U nao devolveu as mesas do Limpar todas')) { $ok = $false }
    $regeradas = Conta 'TODAS_REGERADAS'
    if ($t -notmatch 'AREAS 1 de 1 .rea\(s\) regerada' -or $null -eq $regeradas -or $regeradas[0] -ne 1 -or $regeradas[1] -lt 1) {
        $problemas.Add("clivus-areas-acoes: Regerar todas nao regerou a Oeste (veio $($regeradas -join ' e ')). Veja $($r.Saida)")
        $ok = $false
    }

    if (-not (Espera 'DE_NOVO' 0 0 'depois do U, a Oeste nao foi apagada de novo')) { $ok = $false }

    if ($t -notmatch 'CLIVUS_SECURELOAD_ANTES=(\d+) DEPOIS=(\d+)' -or $Matches[1] -ne $Matches[2]) {
        $problemas.Add("clivus-areas-acoes: o SECURELOAD nao voltou ao que era. Veja $($r.Saida)")
        return $false
    }

    if ($ok) { Write-Host "  (acoes da area: repetida recusada; clique na linha do cliente achou a Oeste e recalculou $mesas mesa(s); Leste vazia apagada direto; Oeste so com o Sim; registro limpo; U devolveu)" -ForegroundColor DarkGray }

    return $ok
}

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

    # O F2 e o menu do botao direito, pela linha da janela (a caixa do nome e o Enter).
    if ($r.Texto -notmatch 'CLIVUS_AREAS_RENOMEAR_F2 novo=(\d+) velho=(\d+) fim' -or $Matches[1] -ne '1' -or $Matches[2] -ne '0') {
        $erros += 'renomear pelo F2 na linha da janela nao trocou o nome (a linha entrou em edicao?)'
    }

    if ($r.Texto -notmatch 'CLIVUS_AREAS_RENOMEAR novo=(\d+) velho=(\d+) letreiro=(\d) fim' -or $Matches[1] -ne '1' -or $Matches[2] -ne '0' -or $Matches[3] -ne '1') {
        $erros += 'renomear pelo menu da linha: o XData da polilinha ou o letreiro nao ficaram com o nome novo'
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
