<#
    A prova da 39.2 (plano/bifila): monoposte ou biposte, o resto do Clivus sai
    igual. O rodar.ps1 carrega este arquivo, e o caso entra em $CasosEstruturas.

    Testar-Estruturas: o clivus-estruturas.scr roda duas vezes no mesmo desenho
    de teste, uma com a monoposte que tem a mesa da GC800 e outra com a GC800
    (biposte), e passa pela cadeia inteira (usina, alturas, tags, grupo,
    strings, eletrica, rota de cabos, vala, resumos, numeracao, analises,
    sombras, PVsyst, Excel, 3D, TermoVision, validar, recontar). Confere:
      - a impressao digital do desenho, camada por camada (quantas entidades e
        a soma de X + Y dos pontos): igual nas duas, menos nas camadas da
        estrutura (pilares, pecas e cotas das alturas);
      - o resumo da usina: mesmas fileiras, mesas, modulos e kWp; o biposte com
        o dobro de pilares, e as pecas so nele;
      - o resumo da rota e o resumo eletrico, texto a texto;
      - os arquivos: o DAE do PVsyst, as abas do Excel que nao sao de pilar nem
        de estrutura, a cena do 3D (tudo menos pilares e pecas) e o retorno do
        TermoVision.
    E que nada disso e vazio: mesas, strings, inversores e sombras existem.
#>
$script:CasosEstruturas = @('Testar-Estruturas')

function Testar-Estruturas {
    param([string] $Desenho)

    $rotulo = 'clivus-estruturas'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    $pasta = Join-Path $raiz 'artefatos\testes'
    $rodadas = @{}

    foreach ($tipo in @('mono', 'bi')) {
        $arquivos = @{
            DAE  = Join-Path $pasta "$rotulo-$tipo.dae"
            XLSX = Join-Path $pasta "$rotulo-$tipo.xlsx"
            HTML = Join-Path $pasta "$rotulo-$tipo-3d.html"
            JSON = Join-Path $pasta "$rotulo-$tipo-tv.json"
        }
        foreach ($f in $arquivos.Values) { if (Test-Path $f) { Remove-Item $f -Force } }

        $sub = @{
            '{{ESTRUTURA}}' = $(if ($tipo -eq 'mono') { 'CLIVUS_MONO_2P14_AUTO' } else { 'CLIVUS_BIFILA_EXEMPLO_AUTO' })
            '{{A1}}' = (P -30 -30); '{{A2}}' = (P 30 -30); '{{A3}}' = (P 30 30); '{{A4}}' = (P -30 30)
            '{{L1}}' = (P -30 -30); '{{L2}}' = (P -30 30); '{{LADO}}' = (P 0 0)
            '{{V1}}' = (P 35 -35); '{{V2}}' = (P 35 35)
            '{{P}}' = (P -40 0)
            '{{DAE}}' = $arquivos.DAE; '{{XLSX}}' = $arquivos.XLSX; '{{HTML}}' = $arquivos.HTML; '{{JSON}}' = $arquivos.JSON
        }

        $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo-$tipo" -Script (Join-Path $PSScriptRoot 'clivus-estruturas.scr') -Substituicoes $sub

        if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ESTR_FIM') -lt 0) {
            $problemas.Add("$rotulo ($tipo) terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
            return $false
        }

        if ($r.Texto -notmatch 'CLIVUS_SECURELOAD_ANTES=(\d+) DEPOIS=(\d+)' -or $Matches[1] -ne $Matches[2]) {
            $problemas.Add("$rotulo ($tipo): o SECURELOAD nao voltou ao que era. Veja $($r.Saida)")
            return $false
        }

        $rodadas[$tipo] = @{ R = $r; Arquivos = $arquivos }
    }

    $ok = $true
    $mono = $rodadas['mono']
    $bi = $rodadas['bi']

    # ---- o resumo da usina
    $usina = 'USINA (\d+) fileira\(s\), (\d+) mesa\(s\), (\d+) m\S+dulo\(s\), ([\d.,]+) kWp, (\d+) pilar\(es\)'
    $um = [regex]::Match($mono.R.Texto, $usina)
    $ub = [regex]::Match($bi.R.Texto, $usina)
    if (-not $um.Success -or -not $ub.Success) {
        $problemas.Add("${rotulo}: nao achei o resumo da usina. Veja $($mono.R.Saida) e $($bi.R.Saida)")
        return $false
    }

    $fileiras = [int] $um.Groups[1].Value
    $mesas = [int] $um.Groups[2].Value
    $modulos = [int] $um.Groups[3].Value
    if ($mesas -lt 10) { $problemas.Add("${rotulo}: a usina saiu com $mesas mesa(s); o teste precisa de pelo menos 10. Veja $($mono.R.Saida)"); $ok = $false }

    for ($g = 1; $g -le 4; $g++) {
        if ($um.Groups[$g].Value -ne $ub.Groups[$g].Value) {
            $problemas.Add("${rotulo}: a usina mudou com a estrutura: monoposte '$($um.Value)', biposte '$($ub.Value)'.")
            $ok = $false
            break
        }
    }

    $pilaresMono = [int] $um.Groups[5].Value
    $pilaresBi = [int] $ub.Groups[5].Value
    if ($pilaresMono -le 0 -or $pilaresBi -ne 2 * $pilaresMono) {
        $problemas.Add("${rotulo}: pilares monoposte=$pilaresMono, biposte=$pilaresBi; o biposte tem o dobro.")
        $ok = $false
    }

    # ---- a impressao digital, camada por camada
    function Impressao([string] $texto) {
        $h = @{}
        foreach ($m in [regex]::Matches($texto, 'CLIVUS_IMP (\S+) n=(\d+) s=(-?[\d.]+)')) {
            $h[$m.Groups[1].Value] = @{ N = [int] $m.Groups[2].Value; S = [double]::Parse($m.Groups[3].Value, $inv) }
        }
        $h
    }

    $im = Impressao $mono.R.Texto
    $ib = Impressao $bi.R.Texto
    $daEstrutura = '^CLIVUS_(PILAR|ESTRUTURA|ALTURAS)\|'
    $iguais = 0
    $diferentes = New-Object System.Collections.Generic.List[string]

    foreach ($k in (@($im.Keys) + @($ib.Keys) | Sort-Object -Unique)) {
        if ($k -match $daEstrutura) { continue }
        $a = $im[$k]
        $b = $ib[$k]
        if ($null -eq $a -or $null -eq $b -or $a.N -ne $b.N -or [Math]::Abs($a.S - $b.S) -gt 0.01 * [Math]::Max(1, $a.N)) {
            $diferentes.Add("$k (monoposte $(if ($a) { "$($a.N) / $($a.S)" } else { 'nada' }); biposte $(if ($b) { "$($b.N) / $($b.S)" } else { 'nada' }))")
        }
        else { $iguais++ }
    }

    if ($diferentes.Count -gt 0) {
        $problemas.Add("${rotulo}: $($diferentes.Count) camada(s) mudaram com a estrutura: $(($diferentes | Select-Object -First 8) -join '; ')")
        $ok = $false
    }

    # Nada vazio: mesas, strings, inversores, sombras.
    foreach ($precisa in @('CLIVUS_MESA|', 'CLIVUS_STRING|', 'CLIVUS_EQUIPAMENTO|', 'CLIVUS_SOMBRA|')) {
        if (-not ($im.Keys | Where-Object { $_.StartsWith($precisa) -and $im[$_].N -gt 0 })) {
            $problemas.Add("${rotulo}: a monoposte nao tem nada em $($precisa.TrimEnd('|')); a comparacao nao provaria nada. Veja $($mono.R.Saida)")
            $ok = $false
        }
    }

    # As pecas da estrutura so no biposte.
    $pecasBi = @($ib.Keys | Where-Object { $_ -like 'CLIVUS_ESTRUTURA|*' } | ForEach-Object { $ib[$_].N } | Measure-Object -Sum).Sum
    $pecasMono = @($im.Keys | Where-Object { $_ -like 'CLIVUS_ESTRUTURA|*' } | ForEach-Object { $im[$_].N } | Measure-Object -Sum).Sum
    if (-not $pecasBi -or $pecasBi -ne 26 * $mesas -or $pecasMono) {
        $problemas.Add("${rotulo}: pecas da estrutura: monoposte $pecasMono (0), biposte $pecasBi ($(26 * $mesas)).")
        $ok = $false
    }

    # ---- os resumos da rota e da eletrica, texto a texto
    foreach ($bloco in @('ROTA', 'ELETRICA')) {
        $padrao = "CLIVUS_ESTR_${bloco}_INICIO(.*?)CLIVUS_ESTR_${bloco}_FIM"
        $tm = [regex]::Match($mono.R.Texto, $padrao, 'Singleline')
        $tb = [regex]::Match($bi.R.Texto, $padrao, 'Singleline')
        # As linhas de amostra (ROTA_RESUMO_LINHA) trazem as tres primeiras strings
        # pelo identificador delas, que e aleatorio a cada rodada; os totais ficam.
        $limpo = { param($t) (($t -split "`r?`n") | Where-Object { $_ -notmatch '^Command:|princ|ROTA_RESUMO_LINHA' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join "`n" }
        $textoMono = & $limpo $tm.Groups[1].Value
        $textoBi = & $limpo $tb.Groups[1].Value
        if (-not $tm.Success -or $textoMono.Length -lt 20) {
            $problemas.Add("${rotulo}: o resumo $bloco nao saiu. Veja $($mono.R.Saida)")
            $ok = $false
        }
        elseif ($textoMono -ne $textoBi) {
            $problemas.Add("${rotulo}: o resumo $bloco mudou com a estrutura. Veja $($mono.R.Saida) e $($bi.R.Saida)")
            $ok = $false
        }
    }

    # ---- os arquivos
    function SomaDosNumeros([string] $texto) {
        $s = 0.0; $n = 0
        foreach ($m in [regex]::Matches($texto, '-?\d+\.\d+(?:[eE][-+]?\d+)?')) { $s += [double]::Parse($m.Value, $inv); $n++ }
        @{ S = [Math]::Round($s, 2); N = $n }
    }

    foreach ($f in @('DAE', 'XLSX', 'HTML', 'JSON')) {
        if (-not (Test-Path $mono.Arquivos[$f]) -or -not (Test-Path $bi.Arquivos[$f])) {
            $problemas.Add("${rotulo}: o arquivo $f nao saiu nas duas rodadas ($($mono.Arquivos[$f]), $($bi.Arquivos[$f])).")
            return $false
        }
    }

    # O DAE do PVsyst: as faces dos modulos, mesmas coordenadas.
    $dm = SomaDosNumeros ([IO.File]::ReadAllText($mono.Arquivos.DAE))
    $db = SomaDosNumeros ([IO.File]::ReadAllText($bi.Arquivos.DAE))
    if ($dm.N -eq 0 -or $dm.N -ne $db.N -or [Math]::Abs($dm.S - $db.S) -gt 0.05) {
        $problemas.Add("${rotulo}: o DAE do PVsyst mudou com a estrutura ($($dm.N) numeros, soma $($dm.S); $($db.N), soma $($db.S)).")
        $ok = $false
    }

    # O Excel: as abas que nao sao de pilar, estrutura ou resumo, celula a celula.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    function Abas([string] $xlsx) {
        $zip = [IO.Compression.ZipFile]::OpenRead($xlsx)
        try {
            $ler = { param($nome) $e = $zip.GetEntry($nome); if ($e) { $s = New-Object IO.StreamReader($e.Open()); try { $s.ReadToEnd() } finally { $s.Dispose() } } }
            $livro = [xml](& $ler 'xl/workbook.xml')
            $rels = [xml](& $ler 'xl/_rels/workbook.xml.rels')
            $comp = @{}
            $textos = & $ler 'xl/sharedStrings.xml'
            $partilhadas = @()
            if ($textos) { $partilhadas = @(([xml]$textos).GetElementsByTagName('si') | ForEach-Object { $_.InnerText }) }
            foreach ($aba in $livro.workbook.sheets.sheet) {
                $id = $aba.GetAttribute('id', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships')
                $alvo = ($rels.Relationships.Relationship | Where-Object { $_.Id -eq $id }).Target
                $xmlDaAba = [xml](& $ler ('xl/' + $alvo.TrimStart('/').Replace('xl/', '')))
                # Celula sem o atributo t (numero) e sem v (vazia): o modo estrito nao deixa ler pela propriedade.
                $linhas = foreach ($linha in $xmlDaAba.GetElementsByTagName('row')) {
                    $valores = foreach ($c in $linha.GetElementsByTagName('c')) {
                        $t = $c.GetAttribute('t')
                        $v = @($c.GetElementsByTagName('v'))
                        $texto = if ($t -eq 's' -and $v.Count -gt 0) { $partilhadas[[int] $v[0].InnerText] } elseif ($t -eq 'inlineStr') { $c.InnerText } elseif ($v.Count -gt 0) { $v[0].InnerText } else { '' }
                        # A hora da quantificacao ou da exportacao pode virar de minuto entre as rodadas.
                        $texto -replace '\d{2}/\d{2}/\d{4} \d{2}:\d{2}', '(hora)'
                    }
                    ,@($valores)
                }
                $comp[$aba.name] = @($linhas)
            }
            $comp
        }
        finally { $zip.Dispose() }
    }

    $xm = Abas $mono.Arquivos.XLSX
    $xb = Abas $bi.Arquivos.XLSX
    $semEstrutura = @($xb.Keys | Where-Object { -not $xm.ContainsKey($_) })
    if ($semEstrutura.Count -ne 1 -or $semEstrutura[0] -notmatch 'Estrutura') {
        $problemas.Add("${rotulo}: o Excel do biposte devia ter so a aba Estrutura a mais; tem: $($semEstrutura -join ', ').")
        $ok = $false
    }

    $abasIguais = 0
    foreach ($aba in $xm.Keys) {
        if ($aba -match 'Pilar|Estrutura|Resumo|Compra') { continue }
        $lm = @($xm[$aba]); $lb = @($xb[$aba])
        $diferentes = @()

        if ($lm.Count -ne $lb.Count) { $diferentes += "linhas $($lm.Count) e $($lb.Count)" }
        else {
            for ($i = 0; $i -lt $lm.Count; $i++) {
                $a = @($lm[$i]); $b = @($lb[$i])

                # Analises: a parte livre do pilar conta pilares (o biposte tem o
                # dobro); a PB e a PA contam porticos no biposte e pilares na
                # monoposte (o mesmo numero: um por estacao), e so o rotulo muda.
                if ($aba -match '^An.lises$' -and $a.Count -gt 8 -and $a[0] -match 'pilar') {
                    if ([int] $b[8] -ne 2 * [int] $a[8]) { $diferentes += "linha $($i + 1): total do pilar $($a[8]) e $($b[8]) (o biposte tem o dobro)" }
                    continue
                }

                if ($aba -match '^An.lises$' -and $a.Count -gt 1 -and $a[1] -eq 'pilares' -and $b[1] -eq 'pórticos') { $a[1] = $b[1] }

                if (($a -join '|') -ne ($b -join '|')) { $diferentes += "linha $($i + 1): '$($a -join '|')' e '$($b -join '|')'" }
            }
        }

        if ($diferentes.Count -gt 0) {
            $problemas.Add("${rotulo}: a aba '$aba' do Excel mudou com a estrutura: $(($diferentes | Select-Object -First 3) -join '; ')")
            $ok = $false
        }
        else { $abasIguais++ }
    }
    if ($abasIguais -lt 2) { $problemas.Add("${rotulo}: so $abasIguais aba(s) do Excel para comparar (sairam: $($xm.Keys -join ', ')).") ; $ok = $false }

    # O 3D: a cena inteira, menos pilares e pecas.
    $cenas = foreach ($t in @($mono, $bi)) {
        $pagina = [IO.File]::ReadAllText($t.Arquivos.HTML, [Text.Encoding]::UTF8)
        if ($pagina -notmatch 'const D = (\{.*?\});\r?\n') { $null } else { $Matches[1] | ConvertFrom-Json }
    }
    if ($null -eq $cenas[0] -or $null -eq $cenas[1]) {
        $problemas.Add("${rotulo}: a cena do 3D nao esta na pagina.")
        $ok = $false
    }
    else {
        foreach ($p in $cenas[0].PSObject.Properties.Name) {
            if ($p -in @('pilares', 'canaletas')) { continue }
            $a = $cenas[0].$p | ConvertTo-Json -Depth 8 -Compress
            $b = $cenas[1].$p | ConvertTo-Json -Depth 8 -Compress
            if ($a -ne $b) { $problemas.Add("${rotulo}: o 3D mudou com a estrutura em '$p'."); $ok = $false }
        }
    }

    # O TermoVision: as mesas que voltam, mesmas coordenadas.
    $tm = SomaDosNumeros ([IO.File]::ReadAllText($mono.Arquivos.JSON))
    $tb = SomaDosNumeros ([IO.File]::ReadAllText($bi.Arquivos.JSON))
    if ($tm.N -eq 0 -or $tm.N -ne $tb.N -or [Math]::Abs($tm.S - $tb.S) -gt 0.05) {
        $problemas.Add("${rotulo}: o retorno do TermoVision mudou com a estrutura ($($tm.N) numeros, soma $($tm.S); $($tb.N), soma $($tb.S)).")
        $ok = $false
    }

    if ($ok) {
        Escrever-Linha $rotulo "$fileiras fileiras, $mesas mesas, $modulos modulos; $iguais camadas iguais, $abasIguais abas, DAE, 3D e TermoVision iguais; pilares $pilaresMono x $pilaresBi" 'OK'
    }

    return $ok
}
