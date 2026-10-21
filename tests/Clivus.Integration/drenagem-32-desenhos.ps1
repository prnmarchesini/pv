<#
    Etapa 32 (segunda rodada do Renan, 21/10/2026): dissipador na ponta clicada
    (contorno, rotulo e solido presos a canaleta), caixa no encontro (solido
    oco), Exportar canaletas (Excel com as abas Canaletas, Por tipo, Caixas,
    Dissipadores) e a vazao no ponto (cresce ao longo da canaleta e no fim bate
    com a do calculo).
#>
function Testar-DrenagemDesenhos {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-desenhos'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("${rotulo}: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }
    function L([double] $dx, [double] $dy) { [string]::Format($inv, '({0:0.###} {1:0.###} 0.0)', $cx + $dx, $cy + $dy) }

    $xlsx = Join-Path $saida 'nivel2-drenagem-canaletas.xlsx'
    if (Test-Path $xlsx) { Remove-Item $xlsx -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-desenhos.scr') -Substituicoes @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{P1}}' = (P -30 -30); '{{P2}}' = (P 0 0); '{{P3}}' = (P 20 10)
        '{{P1L}}' = (L -29.9 -29.9); '{{P2L}}' = (L 0 0); '{{P3L}}' = (L 19.9 9.95)
        '{{XLSX}}' = $xlsx
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_DRNDES FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Etapa([int] $n) {
        $a = $t.IndexOf("CLIVUS_DRNDES ETAPA$n")
        if ($a -lt 0) { return '' }
        $b = $t.IndexOf('CLIVUS_DRNDES ETAPA' + ($n + 1), $a + 10)
        if ($b -lt 0) { $b = $t.IndexOf('CLIVUS_DRNDES FIM', $a) }
        return $t.Substring($a, $b - $a)
    }

    # 1: o dissipador (contorno, rotulo e solido) preso a canaleta.
    if ((Etapa 1) -notmatch 'DISSIPADOR DES \d+-\d+ na (sa.da|ponta de cima) de Canaleta 1') { $erros += 'o dissipador nao foi posto' }
    if ((Etapa 1) -notmatch 'DRN_CANALETA nome=Canaleta_1 solidos=1 .* pecas=(\d+) fim') { $erros += 'o conferir da canaleta nao respondeu' }
    elseif ([int] $Matches[1] -ne 3) { $erros += "o dissipador tem $($Matches[1]) peca(s) (esperado 3: contorno, rotulo e solido)" }

    # 2: a caixa, com a canaleta chegando.
    if ((Etapa 2) -notmatch 'CAIXA CX 1: interna 0,6 . 0,6 m, parede 0,15 m, [\d,]+ m de fundo \(1 canaleta\(s\) chegando\)') { $erros += 'a caixa nao foi posta com a canaleta chegando' }
    if ((Etapa 2) -notmatch 'solidos3d=(\d+)' -or [int] $Matches[1] -ne 3) { $erros += 'esperava 3 solidos (canaleta, dissipador, caixa)' }

    # 3: o Excel das canaletas.
    if ((Etapa 3) -notmatch 'EXPORTAR 1 canaleta\(s\), [\d,]+ m em planta, 1 caixa\(s\), 1 dissipador\(es\)') { $erros += 'o Exportar nao relatou a canaleta, a caixa e o dissipador' }
    if (-not (Test-Path $xlsx)) { $erros += 'o Excel das canaletas nao foi gravado' }
    else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($xlsx)
        try {
            $leitor = New-Object IO.StreamReader(($zip.Entries | Where-Object FullName -eq 'xl/workbook.xml').Open())
            $xml = $leitor.ReadToEnd(); $leitor.Close()
            foreach ($aba in 'Canaletas', 'Por tipo', 'Caixas', 'Dissipadores') {
                if ($xml.IndexOf("name=""$aba") -lt 0) { $erros += "o Excel nao tem a aba $aba" }
            }
        }
        finally { $zip.Dispose() }
    }

    # 4: a vazao em tres pontos: cresce (ou fica igual) para jusante e no fim bate com a do calculo.
    $vazoes = @()
    foreach ($m in [regex]::Matches((Etapa 4), 'VAZ.O Canaleta 1, a ([\d,]+) m da ponta de cima \(de ([\d,]+) m\): passa a .gua de ([\d.]+) m. de ([\d.]+) m.; Q = ([\d,]+) l/s \([\d,]+ m./s\), corre para [^.]+\. Maior da canaleta: ([\d,]+) l/s')) {
        $vazoes += [pscustomobject]@{
            D = [double]::Parse($m.Groups[1].Value.Replace(',', '.'), $inv)
            Q = [double]::Parse($m.Groups[5].Value.Replace(',', '.'), $inv)
            Fim = [double]::Parse($m.Groups[6].Value.Replace(',', '.'), $inv)
        }
    }
    # Tres cliques na canaleta e um no grafico (a 10 m da ponta de cima, pedido de 21/10/2026).
    if ($vazoes.Count -ne 4) { $erros += "a vazao no ponto respondeu $($vazoes.Count) vez(es) (esperado 4: 3 na canaleta e 1 no grafico)" }
    else {
        $ordem = @($vazoes | Sort-Object D)
        for ($k = 1; $k -lt $ordem.Count; $k++) {
            if ($ordem[$k - 1].Q -gt $ordem[$k].Q) { $erros += 'a vazao nao cresce ao longo da canaleta' }
        }
        if ($ordem[-1].Q -gt $ordem[-1].Fim + 0.1) { $erros += 'a vazao no ponto passa da do fim' }
        if ($ordem[-1].Q -le 0) { $erros += 'a vazao no fim e zero' }
        if (-not ($vazoes | Where-Object { [math]::Abs($_.D - 10) -lt 0.5 })) { $erros += 'o clique no grafico nao deu a vazao a 10 m da ponta' }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem desenhos: dissipador com 3 pecas, caixa com a canaleta chegando, Excel das canaletas, vazao de $($ordem[0].Q) a $($ordem[-1].Q) l/s ao longo da canaleta, tambem pelo grafico)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemDesenhos'
