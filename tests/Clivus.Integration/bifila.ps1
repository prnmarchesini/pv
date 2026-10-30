<#
    Os casos de nivel 2 da bifila (plano/bifila, etapas 37 a 39). O rodar.ps1
    carrega este arquivo, e os casos entram em $CasosBifila.

    Testar-Bifila (38.2): a fileira 1 com a bifila Arctech GC800 no terreno de
    teste. Confere no proprio desenho, em LISP: dois pilares por portico (XData
    versao 3, linha Front/Rear), P3 = topo - terreno, as pecas da estrutura por
    tipo, e a cota de cada solido dentro da faixa do terreno (o alinhamento
    vem com Z 9999 de proposito: peca no Z do clique sai dali).
#>
$script:CasosBifila = @('Testar-Bifila')

function Testar-Bifila {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-bifila--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'cotaMin=(-?[\d.]+) cotaMax=(-?[\d.]+) pontos=\d+ centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-bifila: nao achei o terreno. Veja $($sonda.Saida)")
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

    $planilha = Join-Path $raiz 'artefatos\testes\clivus-bifila.xlsx'
    if (Test-Path $planilha) { Remove-Item $planilha -Force }
    $html = Join-Path $raiz 'artefatos\testes\clivus-bifila-3d.html'
    if (Test-Path $html) { Remove-Item $html -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-bifila' `
        -Script (Join-Path $PSScriptRoot 'clivus-bifila.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 9999)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
            '{{ZMIN}}' = $cotaMin.ToString('0.###', $inv)
            '{{ZMAX}}' = $cotaMax.ToString('0.###', $inv)
            '{{XLSX}}' = $planilha
            '{{HTML}}' = $html
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-bifila terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'BIFILA Biposte 2P14 \(0,80 m livre\) no desenho') {
        $problemas.Add("clivus-bifila: a bifila nao foi gravada no desenho. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'desenhado: (\d+) mesa\(s\), (\d+) pilar\(es\), (\d+) módulo\(s\) com face') {
        $problemas.Add("clivus-bifila: nao achei o resumo do desenho. Veja $($r.Saida)")
        return $false
    }

    $mesas = [int] $Matches[1]
    $pilaresRelatados = [int] $Matches[2]
    $modulos = [int] $Matches[3]

    if ($mesas -lt 3 -or $modulos -ne 28 * $mesas -or $pilaresRelatados -ne 12 * $mesas) {
        $problemas.Add("clivus-bifila: $mesas mesa(s), $pilaresRelatados pilar(es) e $modulos modulo(s); esperava 12 pilares e 28 modulos por mesa, pelo menos 3 mesas. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_BIFILA pilares=(\d+) v3=(\d+) frente=(\d+) tras=(\d+) p3ok=(\d+) p3total=(\d+) pares=(\d+)') {
        $problemas.Add("clivus-bifila: nao consegui ler os pilares em LISP. Veja $($r.Saida)")
        return $false
    }

    $np = [int] $Matches[1]; $v3 = [int] $Matches[2]; $frente = [int] $Matches[3]; $tras = [int] $Matches[4]
    $p3ok = [int] $Matches[5]; $p3total = [int] $Matches[6]; $pares = [int] $Matches[7]

    if ($np -ne 12 * $mesas -or $v3 -ne $np -or $frente -ne 6 * $mesas -or $tras -ne 6 * $mesas) {
        $problemas.Add("clivus-bifila: $np pilar(es), $v3 na versao 3, $frente dianteiro(s) e $tras traseiro(s) para $mesas mesa(s). Veja $($r.Saida)")
        return $false
    }

    if ($p3total -eq 0 -or $p3ok -ne $p3total) {
        $problemas.Add("clivus-bifila: P3 = topo - terreno fechou em $p3ok de $p3total pilar(es). Veja $($r.Saida)")
        return $false
    }

    # Os 3,00 m da Arctech entre o dianteiro e o traseiro, em planta, lidos dos blocos.
    if ($pares -ne $frente) {
        $problemas.Add("clivus-bifila: so $pares de $frente dianteiro(s) ficam a 3,00 m do traseiro. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_BIFILA2 solidos=(\d+) xdata=(\d+) vigas=(\d+) maos=(\d+) tercas=(\d+) tirantes=(\d+) arestas=(\d+) dentro=(\d+) pertos=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-bifila: nao consegui ler as pecas em LISP. Veja $($r.Saida)")
        return $false
    }

    $ns = [int] $Matches[1]; $xd = [int] $Matches[2]; $vigas = [int] $Matches[3]; $maos = [int] $Matches[4]
    $tercas = [int] $Matches[5]; $tirantes = [int] $Matches[6]; $arestas = [int] $Matches[7]; $dentro = [int] $Matches[8]
    $pertos = [int] $Matches[9]
    $zmin = [double]::Parse($Matches[10], $inv); $zmax = [double]::Parse($Matches[11], $inv)

    if ($xd -ne $ns -or $vigas -ne 6 * $mesas -or $tercas -ne 4 * $mesas -or $ns -ne $vigas + $maos + $tercas + $tirantes) {
        $problemas.Add("clivus-bifila: $ns solido(s), $xd com XData, $vigas viga(s), $tercas terca(s) para $mesas mesa(s). Veja $($r.Saida)")
        return $false
    }

    # Mao francesa e tirante podem virar aviso num pedaco ingreme do terreno
    # (pegariam dentro do chao); sem aviso, tem que estar tudo la.
    $comAviso = $r.Texto -match 'ATENÇÃO \(biposte\)'
    if ((-not $comAviso -and ($maos -ne 12 * $mesas -or $tirantes -ne 4 * $mesas)) -or $maos -gt 12 * $mesas -or $tirantes -gt 4 * $mesas -or $maos -eq 0) {
        $problemas.Add("clivus-bifila: $maos mao(s) francesa(s) e $tirantes tirante(s) para $mesas mesa(s) (aviso da bifila: $comAviso). Veja $($r.Saida)")
        return $false
    }

    # Regra sagrada 5, lida da geometria de cada solido: as 12 arestas da
    # caixa (sem elas nao ha cota para conferir), tudo na faixa do terreno
    # (nada perto do Z 9999 do clique) e cada peca junto dos pilares da mesa
    # dela: acima do pe enterrado mais baixo e abaixo do topo mais alto + 1 m.
    if ($arestas -ne $ns -or $dentro -ne $ns -or $pertos -ne $ns -or $zmin -lt $cotaMin - 0.01 -or $zmax -gt $cotaMax + 4.0) {
        $problemas.Add("clivus-bifila: de $ns solido(s), $arestas com as 12 arestas, $dentro na faixa do terreno e $pertos junto dos pilares; pecas de $zmin a $zmax, terreno de $cotaMin a $cotaMax. Veja $($r.Saida)")
        return $false
    }

    # 38.3: PB e PA uma vez por portico, P3 em cada pilar: 6 x 2 + 12 = 24
    # cotas por mesa, o dobro dos pilares (na monoposte sao 3 por pilar).
    if ($r.Texto -notmatch 'CLIVUS_BIFILA3 alturas=(\d+) sobra=(\d+) sobrap=(\d+)') {
        $problemas.Add("clivus-bifila: nao consegui ler as alturas e o que sobrou do Apagar tudo. Veja $($r.Saida)")
        return $false
    }

    $alturas = [int] $Matches[1]; $sobra = [int] $Matches[2]; $sobrap = [int] $Matches[3]

    if ($alturas -ne 2 * $np) {
        $problemas.Add("clivus-bifila: as Alturas deram $alturas cota(s) para $np pilar(es); esperava $(2 * $np) (PB/PA por portico). Veja $($r.Saida)")
        return $false
    }

    # O Apagar tudo da area leva a mesa inteira: nenhuma peca da bifila sobra.
    if ($sobra -ne 0 -or $sobrap -ne 0) {
        $problemas.Add("clivus-bifila: depois do Apagar tudo sobraram $sobra peca(s) da estrutura e $sobrap pilar(es). Veja $($r.Saida)")
        return $false
    }

    # 38.3 e 39.2: a quantificacao da PB conta um ponto por portico, e diz
    # "em pórticos"; a do pilar conta todos, "em pilares".
    $pb = [regex]::Match($r.Texto, 'QUANTIFICAR [^\n]*?\(total (\d+)\) — em pórticos')
    $pil = [regex]::Match($r.Texto, 'QUANTIFICAR [^\n]*?\(total (\d+)\) — em pilares')
    if (-not $pb.Success -or -not $pil.Success -or [int] $pb.Groups[1].Value -ne $np / 2 -or [int] $pil.Groups[1].Value -ne $np) {
        $problemas.Add("clivus-bifila: a quantificacao deu PB '$($pb.Value)' e pilar '$($pil.Value)'; esperava $($np / 2) em porticos na PB e $np em pilares no pilar. Veja $($r.Saida)")
        return $false
    }

    # 38.4: o Excel com a aba Estrutura, a linha de cada pilar e as pecas lidas do desenho.
    if ($r.Texto -notmatch 'EXCEL_ESTRUTURA pecas=(\d+) vigas=(\d+) maos=(\d+) tercas=(\d+) tirantes=(\d+) dianteiros=(\d+) traseiros=(\d+)' `
        -or [int] $Matches[1] -ne $ns -or [int] $Matches[2] -ne $vigas -or [int] $Matches[3] -ne $maos -or [int] $Matches[6] -ne $frente -or [int] $Matches[7] -ne $tras) {
        $problemas.Add("clivus-bifila: o Excel nao leu as pecas do desenho (esperava $ns peca(s), $vigas viga(s), $maos mao(s), $frente/$tras pilares). Veja $($r.Saida)")
        return $false
    }

    if (-not (Test-Path $planilha)) {
        $problemas.Add("clivus-bifila: o CLIVUS_EXCEL_AUTO nao gravou $planilha. Veja $($r.Saida)")
        return $false
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($planilha)
    try {
        function LerParteDaBifila([string] $nome) {
            $entrada = $zip.GetEntry($nome)
            if ($null -eq $entrada) { return $null }
            $leitor = New-Object IO.StreamReader($entrada.Open())
            try { [xml] $leitor.ReadToEnd() } finally { $leitor.Dispose() }
        }

        $nomes = @((LerParteDaBifila 'xl/workbook.xml').workbook.sheets.sheet | ForEach-Object { $_.name })
        $pilaresXml = LerParteDaBifila "xl/worksheets/sheet$([array]::IndexOf($nomes, 'Pilares') + 1).xml"
    }
    finally { $zip.Dispose() }

    $c1 = $pilaresXml.worksheet.sheetData.row.c | Where-Object { $_.r -eq 'C1' } | Select-Object -First 1
    # A celula de texto vem em linha (<is><t>), sem <v>: com StrictMode, ler
    # uma propriedade que nao existe e erro, entao pergunta antes.
    $textoC1 = ''
    if ($null -ne $c1) {
        if ($c1.PSObject.Properties['v']) { $textoC1 = [string] $c1.v }
        else {
            $t = $c1.is.t
            $textoC1 = if ($t -is [string]) { $t } else { [string] $t.'#text' }
        }
    }

    if (($nomes -join '|') -ne 'Resumo|Análises|Pilares|Compra de pilares|Estrutura' -or $textoC1 -ne 'Linha') {
        $problemas.Add("clivus-bifila: o Excel tem as abas [$($nomes -join '|')] e a coluna C da aba Pilares diz '$textoC1'; esperava a aba Estrutura no fim e a coluna Linha. Veja $planilha")
        return $false
    }

    # 39.1: o 3D leva as pecas da bifila, cada barra em 8 triangulos (os 4
    # lados, sem as tampas das pontas: BarMesh, caps: false).
    if (-not (Test-Path $html)) {
        $problemas.Add("clivus-bifila: o CLIVUS_3D_AUTO nao gravou $html. Veja $($r.Saida)")
        return $false
    }

    $pagina = [IO.File]::ReadAllText($html, [Text.Encoding]::UTF8)
    if ($pagina -notmatch 'const D = (\{.*?\});\r?\n') {
        $problemas.Add("clivus-bifila: a cena nao esta na pagina 3D. Veja $html")
        return $false
    }

    $cena = $Matches[1] | ConvertFrom-Json
    $triangulos = @($cena.canaletas).Count
    if ($triangulos -ne 8 * $ns -or @($cena.pilares).Count -ne $np) {
        $problemas.Add("clivus-bifila: o 3D tem $triangulos triangulo(s) de estrutura e $(@($cena.pilares).Count) pilar(es); esperava $(8 * $ns) e $np. Veja $html")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_SECURELOAD_ANTES=(\d+) DEPOIS=(\d+)' -or $Matches[1] -ne $Matches[2]) {
        $problemas.Add("clivus-bifila: o SECURELOAD nao voltou ao que era. Veja $($r.Saida)")
        return $false
    }

    return $true
}
