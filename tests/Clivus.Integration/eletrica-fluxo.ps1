<#
    O fluxo eletrico inteiro num desenho so, com as pecas de verdade: usina
    mista pelo motor, dois tipos de string com tracado (duas de 28 vizinhas e
    uma de 28), strings reais geradas em todas as mesas (duas selecoes: uma
    mesa de 28 sozinha, depois o resto), subestacoes UC1 e U1,
    trafos T1 e T2, quatro inversores Huawei 250, alocacao pela selecao,
    skid pela selecao dos retangulos em campo, trafo na subestacao, numeracao
    e resumo. Sem CLIVUS_STRINGS_TESTE_AUTO nem a cadeia de exemplo.

    O que prova (tudo lido do desenho pelo LISP: XData e dicionario):
      - toda string tem o tipo gravado e modulos de uma mesa de 28 so (tipo de
        uma mesa) ou de duas de 28 vizinhas na mesma fileira (o par);
      - nenhum modulo em duas strings; a mesa de 14, sem tipo que case, fica
        sem string e e avisada pelo nome;
      - cada string em exatamente um inversor; realocar tudo para o Inversor 4
        recusa as dos outros (travadas) e nao muda nada;
      - a tag de cada string e T<trafo>.I<inversor>.S<n> pela posicao do trafo e
        do inversor no cadastro, com o sequencial 1..n reiniciando por
        inversor, na ordem da varredura;
      - o resumo (strings, modulos e kWp por inversor, trafo, subestacao e o
        total) bate com a conta refeita a partir do XData.
    Usa Numeracao-Strings e Numeracao-Confere de eletrica-15-4.ps1.
#>
function Fluxo-Pontos {
    param([string] $Desenho, [string] $Rotulo)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { return $null }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy, [double] $z = 0) { [string]::Format($inv, '{0:0.###},{1:0.###},{2:0.###}', $cx + $dx, $cy + $dy, $z) }

    # A area e a da usina mista (90 m de largura: mesas de 28 e de 14). O
    # equipamento chega com Z absurdo: a cota tem que vir do terreno.
    return @{
        '{{A1}}' = (P -45 -50); '{{A2}}' = (P 45 -50); '{{A3}}' = (P 45 50); '{{A4}}' = (P -45 50)
        '{{L1}}' = (P -45 -50); '{{L2}}' = (P -45 50); '{{LADO}}' = (P 0 0)
        '{{P1}}' = (P -30 0 9999); '{{P2}}' = (P 30 0 9999)
        '{{Q1}}' = (P -20 -15 9999); '{{Q2}}' = (P -15 -15 9999); '{{Q3}}' = (P 20 -15 9999); '{{Q4}}' = (P 25 -15 9999)
    }
}

function Testar-EletricaFluxo {
    param([string] $Desenho)

    $rotulo = 'clivus-eletrica-fluxo'
    $sub = Fluxo-Pontos -Desenho $Desenho -Rotulo $rotulo
    if (-not $sub) { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $dump = Join-Path $saida "nivel2-$rotulo.txt"
    if (Test-Path $dump) { Remove-Item $dump -Force }
    $sub['{{DUMP}}'] = $dump

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-eletrica-fluxo.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_FLUXO_FIM') -lt 0 -or -not (Test-Path $dump)) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $erros = @()

    # Os trechos da saida, pelos marcadores (o eco do LISP vem antes de cada um).
    function Trecho([string] $de, [string] $ate) {
        $a = $t.LastIndexOf("CLIVUS_FLUXO $de")
        $b = if ($ate) { $t.LastIndexOf("CLIVUS_FLUXO $ate") } else { $t.Length }
        if ($a -lt 0 -or $b -lt $a) { return '' }
        return $t.Substring($a, $b - $a)
    }
    $gerar1 = Trecho 'GERAR1' 'GERAR'
    $gerar = Trecho 'GERAR' 'ALOCAR'
    $alocar = Trecho 'ALOCAR' 'REALOCAR'
    $realocar = Trecho 'REALOCAR' 'SKID'
    $skid = Trecho 'SKID' 'NUMERACAO'
    $numeracao = Trecho 'NUMERACAO' 'TAGS'
    $tags = Trecho 'TAGS' 'RESUMO'
    $resumo = Trecho 'RESUMO' 'DESENHO'

    # ---- o desenho, como o LISP leu --------------------------------------
    $inversores = [ordered]@{}; $trafos = [ordered]@{}; $ucs = @{}; $mesas = @{}; $porLetreiro = @{}
    $strings = [System.Collections.Generic.List[object]]::new()
    $modulosNoDesenho = 0
    foreach ($linha in [IO.File]::ReadAllLines($dump)) {
        $p = $linha.Split(' ')
        switch ($p[0]) {
            'INV'     { $inversores[$p[1]] = [pscustomobject]@{ Nome = $p[3]; Trafo = $p[2]; Posicao = $inversores.Count + 1 } }
            'TRAFO'   { $trafos[$p[1]] = [pscustomobject]@{ Apelido = $p[3]; Uc = $p[2]; Posicao = $trafos.Count + 1 } }
            'UC'      { $ucs[$p[1]] = $p[2] }
            'MESA'    { $mesas[$p[1]] = [pscustomobject]@{ Letreiro = $p[2]; Perfil = $p[3] }; $porLetreiro[$p[2]] = $p[3] }
            'MODULOS' { $modulosNoDesenho = [int]$p[1] }
            'S'       {
                $strings.Add([pscustomobject]@{
                    Id = $p[1]; Tipo = $p[2]; Inversor = $p[3]; Tag = $p[4]; N = [int]$p[5]
                    Watts = [double]::Parse($p[6], $inv); Mesas = @($p[7] -split ','); Modulos = @($p[8..($p.Count - 1)])
                })
            }
        }
    }

    $de14 = @($porLetreiro.Keys | Where-Object { $porLetreiro[$_] -eq 'Mesa_2V14' } | Sort-Object)
    $de28 = @($porLetreiro.Keys | Where-Object { $porLetreiro[$_] -eq 'Mesa_2V28' })
    if ($de14.Count -lt 1 -or $de28.Count -lt 2) { $erros += "a usina mista nao saiu ($($de28.Count) mesa(s) de 28, $($de14.Count) de 14)" }
    if ($strings.Count -lt 10) { $erros += "so $($strings.Count) string(s) no desenho" }
    if ($inversores.Count -ne 4 -or $trafos.Count -ne 2 -or $ucs.Count -ne 2) { $erros += "cadastro com $($inversores.Count) inversor(es), $($trafos.Count) trafo(s), $($ucs.Count) subestacao(oes); esperava 4, 2, 2" }

    # ---- 1. strings reais: tipo, mesas de uma so ou vizinhas ---------------
    if ($t -notmatch 'STRING Modelo 1 renomeado para Par 28') { $erros += 'nao renomeou o Modelo 1 para Par 28' }
    $porTipo = @{}
    $todosOsModulos = @{}
    foreach ($s in $strings) {
        if ($s.Tipo -eq '-') { $erros += "string $($s.Id) sem tipo (string de teste?)"; continue }
        if ($s.Modulos.Count -ne $s.N) { $erros += "string $($s.Id): $($s.N) modulo(s) no XData, $($s.Modulos.Count) lidos" }
        foreach ($m in $s.Modulos) { $todosOsModulos[$m] = 1 + [int]$todosOsModulos[$m] }
        if (-not $porTipo.ContainsKey($s.Tipo)) { $porTipo[$s.Tipo] = @{} }
        $porTipo[$s.Tipo][$s.Mesas.Count] = 1 + [int]$porTipo[$s.Tipo][$s.Mesas.Count]

        if ($s.Mesas -contains '?') { $erros += "string $($s.Id) tem modulo sem mesa no desenho"; continue }
        foreach ($l in $s.Mesas) { if ($porLetreiro[$l] -ne 'Mesa_2V28') { $erros += "string $($s.Id) caiu na $l ($($porLetreiro[$l]))" } }
        switch ($s.Mesas.Count) {
            1 { }
            2 {
                $a = $s.Mesas[0].TrimStart('F') -split '\.'; $b = $s.Mesas[1].TrimStart('F') -split '\.'
                if ($a[0] -ne $b[0] -or [math]::Abs([int]$a[1] - [int]$b[1]) -ne 1) { $erros += "string $($s.Id) junta $($s.Mesas -join ' e '), que nao sao vizinhas na fileira" }
            }
            default { $erros += "string $($s.Id) passa por $($s.Mesas.Count) mesas ($($s.Mesas -join ', '))" }
        }
    }
    $repetidos = @($todosOsModulos.Keys | Where-Object { $todosOsModulos[$_] -gt 1 }).Count
    if ($repetidos -gt 0) { $erros += "$repetidos modulo(s) em mais de uma string" }

    # Cada tipo e de um feitio so: o par sempre em duas mesas, o outro em uma.
    $feitios = @($porTipo.Keys | ForEach-Object { ($porTipo[$_].Keys | Sort-Object) -join '+' } | Sort-Object)
    if (($feitios -join ' ') -ne '1 2') { $erros += "os tipos das strings nao sao um de uma mesa e um de duas (feitios: $($feitios -join ' '))" }

    # Mesa coberta: toda de 28 tem string; nenhuma de 14 tem.
    $cobertas = @{}
    foreach ($s in $strings) { foreach ($l in $s.Mesas) { $cobertas[$l] = 1 } }
    $de28Sem = @($de28 | Where-Object { -not $cobertas.ContainsKey($_) })
    $de14Com = @($de14 | Where-Object { $cobertas.ContainsKey($_) })
    if ($de28Sem.Count -gt 0) { $erros += "mesa(s) de 28 sem string: $($de28Sem -join ', ')" }
    if ($de14Com.Count -gt 0) { $erros += "mesa(s) de 14 com string: $($de14Com -join ', ')" }
    $somaModulos = ($strings | Measure-Object N -Sum).Sum
    if ($somaModulos -ne 28 * $de28.Count) { $erros += "as strings somam $somaModulos modulo(s), as $($de28.Count) mesa(s) de 28 tem $(28 * $de28.Count)" }

    # ---- 2. a mesa que nao casou e avisada pelo nome ------------------------
    # A primeira geracao (uma mesa de 28 sozinha) so cabe no Modelo 1.
    if ($t -notmatch 'CLIVUS_FLUXO_SOZINHA (F\d+\.\d+) outras=\d+') { $erros += 'nao li a mesa da primeira geracao' }
    else {
        $sozinha = $Matches[1]
        if ($gerar1 -notmatch "STRING_GERAR Modelo 1 \S $([regex]::Escape($sozinha)): 2 string" -or $gerar1 -notmatch 'STRING_GERAR 2 string\(s\) em 1 grupo\(s\) de mesas; 0 mesa\(s\) sem tipo') { $erros += "a primeira geracao nao pos o Modelo 1 (2 strings) na $sozinha" }
    }
    $avisadas = @([regex]::Matches($gerar, 'STRING_GERAR Aviso: (F\d+\.\d+): mesa de 14 m\S+dulos \(7x2\) sem tipo de string') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    if (($avisadas -join ',') -ne ($de14 -join ',')) { $erros += "avisadas [$($avisadas -join ', ')] em vez das de 14 [$($de14 -join ', ')]" }
    if ($gerar -notmatch "STRING_GERAR $($strings.Count - 2) string\(s\) em \d+ grupo\(s\) de mesas; $($de14.Count) mesa\(s\) sem tipo") { $erros += "o relatorio da segunda geracao nao diz $($strings.Count - 2) string(s) e $($de14.Count) mesa(s) sem tipo" }
    if (@([regex]::Matches($gerar, 'STRING_GERAR Par 28 \S (F\d+\.\d+, F\d+\.\d+): 2 string')).Count -lt 1) { $erros += 'o Par 28 nao caiu em nenhum par de mesas' }

    # ---- 3. alocacao: cada string em um inversor so --------------------------
    if ($t -notmatch 'CLIVUS_FLUXO_ALOCAR strings=(\d+) parte=(\d+)') { $erros += 'nao li a alocacao montada' }
    else {
        $n = [int]$Matches[1]; $parte = [int]$Matches[2]
        if ($n -ne $strings.Count) { $erros += "a alocacao viu $n string(s), o desenho tem $($strings.Count)" }
        $esperado = @(); $resto = $n
        foreach ($k in 1..4) { $q = [math]::Min($parte, $resto); $esperado += $q; $resto -= $q }
        foreach ($k in 1..4) {
            if ($esperado[$k - 1] -eq 0) { continue }
            if ($alocar -notmatch "INVERSOR Inversor $($k): $($esperado[$k - 1]) string\(s\) alocada\(s\), 0 j\S+ eram dele, 0 recusada") { $erros += "o Inversor $k nao recebeu as $($esperado[$k - 1]) livres" }
        }
        $doQuarto = $esperado[3]
        if ($realocar -notmatch "INVERSOR Inversor 4: 0 string\(s\) alocada\(s\), $doQuarto j\S+ eram dele, $($n - $doQuarto) recusada") { $erros += "realocar tudo no Inversor 4 nao recusou as $($n - $doQuarto) dos outros" }
    }
    $semInversor = @($strings | Where-Object { $_.Inversor -eq '-' }).Count
    $fantasma = @($strings | Where-Object { $_.Inversor -ne '-' -and -not $inversores.Contains($_.Inversor) }).Count
    $ids = @($strings | Group-Object Id | Where-Object { $_.Count -gt 1 }).Count
    if ($semInversor -gt 0 -or $fantasma -gt 0 -or $ids -gt 0) { $erros += "$semInversor string(s) sem inversor, $fantasma com inversor fora do cadastro, $ids GUID(s) repetido(s)" }

    # ---- 4. skid e subestacao (o cadastro, lido do dicionario) ----------------
    if ($skid -notmatch 'SKID Skid A \(T1\): 2 inversor\(es\) agrupado') { $erros += 'o Skid A nao agrupou os inversores 1 e 2 no T1' }
    if ($skid -notmatch 'SKID Skid B \(T2\): 2 inversor\(es\) agrupado') { $erros += 'o Skid B nao agrupou os inversores 3 e 4 no T2' }
    $cadeia = @{}
    foreach ($id in $inversores.Keys) {
        $i = $inversores[$id]
        $tr = if ($trafos.Contains($i.Trafo)) { $trafos[$i.Trafo] } else { $null }
        $uc = if ($tr -and $ucs.ContainsKey($tr.Uc)) { $ucs[$tr.Uc] } else { '-' }
        $cadeia[$i.Nome] = "$(if ($tr) { $tr.Apelido } else { '-' }) $uc"
    }
    $esperada = @{ 'Inversor_1' = 'T1 UC1'; 'Inversor_2' = 'T1 UC1'; 'Inversor_3' = 'T2 U1'; 'Inversor_4' = 'T2 U1' }
    foreach ($k in $esperada.Keys) { if ($cadeia[$k] -ne $esperada[$k]) { $erros += "$k esta em [$($cadeia[$k])], esperava [$($esperada[$k])] (trafo subestacao)" } }

    # ---- 5. as tags: esquema e sequencial por inversor -----------------------
    if ($numeracao -notmatch "NUMERACAO $($strings.Count) string\(s\) com tag; 0 sem tag") { $erros += "a numeracao nao deu tag as $($strings.Count) string(s)" }
    $prefixos = @{}
    foreach ($id in $inversores.Keys) {
        $i = $inversores[$id]
        $prefixos[$i.Nome] = "T$($trafos[$i.Trafo].Posicao).I$($i.Posicao).S"
    }
    foreach ($grupo in ($strings | Group-Object Inversor)) {
        $nome = $inversores[$grupo.Name].Nome
        $prefixo = $prefixos[$nome]
        $numeros = @($grupo.Group | ForEach-Object { if ($_.Tag.StartsWith($prefixo) -and $_.Tag.Substring($prefixo.Length) -match '^\d+$') { [int]$_.Tag.Substring($prefixo.Length) } else { -1 } } | Sort-Object)
        if (($numeros -join ',') -ne ((1..$grupo.Count) -join ',')) { $erros += "${nome}: tags [$(($grupo.Group | ForEach-Object Tag) -join ', ')] no XData, esperava $prefixo 1 a $($grupo.Count)" }
    }
    if (@($strings | Group-Object Tag | Where-Object { $_.Count -gt 1 }).Count -gt 0) { $erros += 'tag repetida no desenho' }
    # A ordem do sequencial e a da varredura (a usina da esquerda para a direita).
    $lista = @(Numeracao-Strings $tags)
    if ($lista.Count -ne $strings.Count) { $erros += "a lista da numeracao tem $($lista.Count) string(s), o desenho $($strings.Count)" }
    $porId = @{}; foreach ($s in $strings) { $porId[$s.Tag] = $s }
    foreach ($l in $lista) { if (-not $porId.ContainsKey($l.Tag) -or $inversores[$porId[$l.Tag].Inversor].Nome -ne $l.Inversor) { $erros += "a numeracao lista $($l.Tag) em $($l.Inversor), diferente do XData" } }
    $erros += @(Numeracao-Confere -Strings $lista -Prefixos $prefixos -Sentidos @{ 0 = 'LeftToRight' } -Blocos 0)

    # ---- 6. o resumo contra a conta pelo XData --------------------------------
    $conta = @{}
    foreach ($s in $strings) {
        $nome = $inversores[$s.Inversor].Nome
        if (-not $conta.ContainsKey($nome)) { $conta[$nome] = [pscustomobject]@{ S = 0; M = 0; W = 0.0 } }
        $conta[$nome].S++; $conta[$nome].M += $s.N; $conta[$nome].W += $s.Watts
    }
    function Soma([string[]] $nomes) {
        $x = [pscustomobject]@{ S = 0; M = 0; W = 0.0 }
        foreach ($k in $nomes) { if ($conta.ContainsKey($k)) { $x.S += $conta[$k].S; $x.M += $conta[$k].M; $x.W += $conta[$k].W } }
        return $x
    }
    function Confere([string] $quem, [int] $s, [int] $m, [double] $kwp, $x) {
        if ($s -ne $x.S -or $m -ne $x.M -or [math]::Abs($kwp - $x.W / 1000) -gt 0.006) {
            return "${quem}: resumo $s string(s), $m modulo(s), $kwp kWp; pelo XData $($x.S), $($x.M), $([math]::Round($x.W / 1000, 3))"
        }
    }
    $total = Soma @($conta.Keys)
    if ($resumo -notmatch 'RESUMO_TOTAIS ucs=(\d+) trafos=(\d+) inversores=(\d+) strings=(\d+) alocadas=(\d+) livres=(\d+) modulos=(\d+) kwp=([\d.]+) pendencias=(\d+)') { $erros += 'nao li os totais do resumo' }
    else {
        $m = $Matches
        if ("$($m[1]) $($m[2]) $($m[3]) $($m[4]) $($m[6])" -ne "2 2 4 $($strings.Count) 0") { $erros += "totais ucs=$($m[1]) trafos=$($m[2]) inversores=$($m[3]) strings=$($m[4]) livres=$($m[6]); esperava 2, 2, 4, $($strings.Count), 0" }
        $erros += @(Confere 'total' ([int]$m[5]) ([int]$m[7]) ([double]::Parse($m[8], $inv)) $total)
        if ($total.W -le 0) { $erros += 'a conta pelo XData deu 0 W (mesa sem potencia gravada?)' }
        if ($resumo -match 'ainda sem tag') { $erros += 'o resumo ainda aponta string sem tag depois da numeracao' }
    }
    $vistos = 0
    foreach ($l in [regex]::Matches($resumo, 'RESUMO_INVERSOR nome=(\S+) trafo=(\S+) uc=(\S+) strings=(\d+) capacidade=\d+ excesso=\d modulos=(\d+) kwp=([\d.]+)')) {
        $g = $l.Groups; $vistos++
        if ("$($g[2].Value) $($g[3].Value)" -ne $esperada[$g[1].Value]) { $erros += "resumo: $($g[1].Value) em [$($g[2].Value) $($g[3].Value)], esperava [$($esperada[$g[1].Value])]" }
        $erros += @(Confere $g[1].Value ([int]$g[4].Value) ([int]$g[5].Value) ([double]::Parse($g[6].Value, $inv)) (Soma @($g[1].Value)))
    }
    if ($vistos -ne 4) { $erros += "o resumo tem $vistos linha(s) de inversor, esperava 4" }

    # Trafo e subestacao: as linhas do texto do resumo (kWp com duas casas, na cultura da tela).
    function Numero([string] $x) { [double]::Parse($x.Replace(',', '.'), $inv) }
    $grupos = @{ 'T1' = @('Inversor_1', 'Inversor_2'); 'T2' = @('Inversor_3', 'Inversor_4'); 'UC1' = @('Inversor_1', 'Inversor_2'); 'U1' = @('Inversor_3', 'Inversor_4') }
    foreach ($quem in 'T1', 'T2', 'UC1', 'U1') {
        $fim = if ($quem.StartsWith('T')) { 'inversor\(es\)' } else { 'trafo\(s\)' }
        if ($resumo -notmatch "(?m)^\s*$quem \([^)]*\): (\d+) string\(s\), (\d+) m\S+dulo\(s\), ([\d.,]+) kWp; \d+ $fim") { $erros += "o resumo nao tem a linha do $quem"; continue }
        $erros += @(Confere $quem ([int]$Matches[1]) ([int]$Matches[2]) (Numero $Matches[3]) (Soma $grupos[$quem]))
    }

    $erros = @($erros | Where-Object { $_ })
    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida) e $dump")
        return $false
    }

    $porInv = (1..4 | ForEach-Object { $conta["Inversor_$_"].S }) -join '/'
    Write-Host ("  (fluxo eletrico: $($strings.Count) strings reais em $($de28.Count) mesas de 28 ($($de14.Count) de 14 avisada(s)), $porInv por inversor, T1>UC1 e T2>U1, tags por inversor, resumo {0:0.00} kWp igual ao XData)" -f ($total.W / 1000)) -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaFluxo'
