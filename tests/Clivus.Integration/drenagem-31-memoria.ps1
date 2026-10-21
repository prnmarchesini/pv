<#
    Passo 31 (Memoria > Calculo): area de 100 x 100 m, uma canaleta meia-cana
    de 15 cm atravessando e as bacias. A memoria grava o Excel (abas do
    memorial) e escreve a canaleta; a vazao e conferida aqui, de forma
    independente, pelo metodo racional com a IDF do memorial (Q = C.I.A/3600,
    I = K.T^a/(tc+b)^c). A altura sugerida e aplicada: o raio da canaleta muda
    para o sugerido, o alerta de lamina some e o solido continua conferindo.
#>
function Testar-DrenagemMemoria {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-memoria'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("${rotulo}: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    $xlsx = Join-Path $saida 'nivel2-drenagem-memoria.xlsx'
    if (Test-Path $xlsx) { Remove-Item $xlsx -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-memoria.scr') -Substituicoes @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{C1}}' = (P -45 -20); '{{C2}}' = (P 45 -25)
        '{{XLSX}}' = $xlsx
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_DRNMEM FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Etapa([int] $n) {
        $a = $t.IndexOf("CLIVUS_DRNMEM ETAPA$n")
        if ($a -lt 0) { return '' }
        $b = $t.IndexOf('CLIVUS_DRNMEM ETAPA' + ($n + 1), $a + 10)
        if ($b -lt 0) { $b = $t.IndexOf('CLIVUS_DRNMEM FIM', $a) }
        return $t.Substring($a, $b - $a)
    }

    $padrao = 'DRN_MEMORIA nome=(\S+) area=([\d.]+) tc=([\d.]+) i=([\d.]+) q=([\d.]+) imin=([-\d.]+) imax=([-\d.]+) hmax=([\d.NaN]+) vmax=([\d.NaN]+) f=([\d.]+) ysug=([\d.NaN]+) des=(\S+) alertas=(\d+) fim'

    if ((Etapa 1) -notmatch $padrao) { $erros += 'etapa 1: a memoria nao escreveu a canaleta' }
    else {
        $area = [double]::Parse($Matches[2], $inv); $tc = [double]::Parse($Matches[3], $inv)
        $i = [double]::Parse($Matches[4], $inv); $q = [double]::Parse($Matches[5], $inv)
        $ysug1 = $Matches[11]; $alertas1 = [int] $Matches[13]

        # A IDF e o racional, conferidos aqui (parametros padrao: o memorial de Valparaiso).
        $iEsperada = 1544.886 * [math]::Pow(25, 0.142) / [math]::Pow($tc + 24.953, 0.756)
        $qEsperada = 0.15 * $iEsperada * $area / 3600 / 1000
        if ($area -le 0) { $erros += 'etapa 1: a canaleta ficou sem bacia' }
        if ($tc -lt 5 - 1e-6) { $erros += "etapa 1: tc $tc abaixo do minimo de 5 min" }
        if ([math]::Abs($i - $iEsperada) -gt 0.01) { $erros += "etapa 1: intensidade $i, a IDF da $iEsperada" }
        if ([math]::Abs($q - $qEsperada) -gt 0.0002) { $erros += "etapa 1: vazao $q m3/s, o racional da $qEsperada" }
        if ($ysug1 -eq 'NaN') { $erros += 'etapa 1: sem altura sugerida' }
    }
    if ((Etapa 1) -notmatch 'MEM.RIA 1 canaleta\(s\), \d+ alerta\(s\), gravada em') { $erros += 'etapa 1: o Excel nao foi gravado' }

    if (-not (Test-Path $xlsx)) { $erros += 'o arquivo XLSX nao existe' }
    else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($xlsx)
        try {
            $livro = $zip.Entries | Where-Object FullName -eq 'xl/workbook.xml'
            $leitor = New-Object IO.StreamReader($livro.Open())
            $xml = $leitor.ReadToEnd(); $leitor.Close()
            foreach ($aba in 'Bacias', 'Chuva', 'Vaz', 'Canaletas', 'Dimens', 'Dissipadores', 'Alertas') {
                if ($xml.IndexOf("name=""$aba") -lt 0) { $erros += "o Excel nao tem a aba $aba" }
            }
        }
        finally { $zip.Dispose() }
    }

    # Depois de aplicar a altura sugerida: o raio vira o sugerido e o alerta de lamina some.
    if ((Etapa 2) -notmatch 'MEM.RIA altura sugerida aplicada em 1 canaleta') { $erros += 'etapa 2: a altura sugerida nao foi aplicada' }
    if ((Etapa 2) -notmatch $padrao) { $erros += 'etapa 2: a memoria nao foi refeita' }
    elseif ((Etapa 2) -match 'passa da altura da se') { $erros += 'etapa 2: com a altura sugerida, ainda ha alerta de lamina' }
    if ((Etapa 2) -notmatch 'DRN_CANALETA nome=\S+ solidos=1 malhas=0 volume=([\d.]+) esperado=([\d.]+)') { $erros += 'etapa 2: o solido da canaleta nao foi refeito' }
    elseif ([math]::Abs([double]::Parse($Matches[1], $inv) - [double]::Parse($Matches[2], $inv)) -gt 0.03 * [double]::Parse($Matches[2], $inv)) { $erros += 'etapa 2: o volume do solido nao bate com a secao nova' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem memoria: bacia, IDF e racional conferidos, Excel com as abas do memorial, altura sugerida aplicada sem alerta)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemMemoria'
