<#
    Itens 14 e 15 das melhorias de 10/10/2026 (potencia do modulo e PAN):
      - CLIVUS_ESTRUTURA_PAN carrega o PAN real (Risen 700 Wp, copia em
        tests\dados\pan) na estrutura "Mesa 2V28" (720 Wp): a estrutura fica
        com 700 Wp e os dados eletricos, a fonte unica devolve a Voc do PAN
        para ela e nada para a "Mesa 2V14"; as mesas ja desenhadas com 720
        aparecem como divergencia;
      - CLIVUS_POTENCIA_PELO_PAN passa as mesas desenhadas da 2V28 para 700
        Wp e nao mexe em mais nada: a geometria medida em LISP (entidades e
        soma dos pontos de insercao) e a mesma, as da 2V14 ficam em 720, as
        divergencias somem e o kWp do recontar bate com os watts do XData;
      - CLIVUS_TROCAR_POTENCIA 650 (o item do botao direito da area) simula a
        usina inteira a 650 Wp: kWp do recontar e do resumo = modulos x 0,65,
        a fonte nao devolve dado eletrico para ninguem e a tabela CC da rota
        diz por que;
      - o botao "Usar a configuracao da mesa..." (fora de comando, pelo mesmo
        caminho da faixa) desfaz e os calculos voltam; a palavra Mesa do
        comando tambem desfaz.
#>
function Testar-EletricaPotencia {
    param([string] $Desenho)

    $rotulo = 'clivus-potencia'
    $u = Numeracao-Usina -Desenho $Desenho -Rotulo $rotulo
    if (-not $u) { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $pan = Join-Path $raiz 'tests\dados\pan\Risen_RSM132-8-700BHDG.PAN'
    if (-not (Test-Path $pan)) { $problemas.Add("${rotulo}: nao achei o PAN de teste em $pan."); return $false }
    $u.Sub['{{PAN}}'] = $pan

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-potencia.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_POTENCIA_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $erros = @()

    function Fase([string] $de, [string] $ate) {
        $i = $t.IndexOf("CLIVUS_FASE $de")
        $j = if ($ate) { $t.IndexOf("CLIVUS_FASE $ate") } else { $t.IndexOf('CLIVUS_POTENCIA_FIM') }
        if ($i -lt 0 -or $j -lt $i) { return '' }
        return $t.Substring($i, $j - $i)
    }

    function Estrutura([string] $f, [string] $nome) {
        if ($f -match "POTENCIA_ESTRUTURA nome=$nome potencia=(\S+) pan=(\S+) voc=(\S+)") { return @{ Potencia = $Matches[1]; Pan = $Matches[2]; Voc = $Matches[3] } }
        return $null
    }

    function Kwp([string] $f) {
        if ($f -match 'POTENCIA_KWP recontar=([\d.]+) modulos=(\d+) resumo=([\d.]+) resumomodulos=(\d+)') {
            return @{ Recontar = [double]::Parse($Matches[1], $inv); Modulos = [int]$Matches[2]; Resumo = [double]::Parse($Matches[3], $inv); ResumoModulos = [int]$Matches[4] }
        }
        return $null
    }

    function Mesas([string] $f, [string] $nome, [string] $watts) {
        if ($f -match "POTENCIA_MESAS estrutura=$nome watts=$watts mesas=(\d+)") { return [int]$Matches[1] }
        return 0
    }

    function Lisp([string] $fase) {
        if ($t -match "CLIVUS_POT_LISP fase=$fase entidades=(\d+) soma=(-?[\d.]+) modulos=(\d+) watts=([\d.]+)") {
            return @{ Entidades = [int]$Matches[1]; Soma = [double]::Parse($Matches[2], $inv); Modulos = [int]$Matches[3]; Watts = [double]::Parse($Matches[4], $inv) }
        }
        return $null
    }

    $inicio = Fase 'INICIO' 'PAN'
    $comPan = Fase 'PAN' 'ATUALIZADA'
    $atualizada = Fase 'ATUALIZADA' 'SIMULADA'
    $simulada = Fase 'SIMULADA' 'DESFEITA'
    $desfeita = Fase 'DESFEITA' 'MESA'
    $mesa = Fase 'MESA' $null

    # --- inicio: duas estruturas de 720, sem PAN, sem divergencia
    if ($inicio -notmatch 'POTENCIA_AUTO simulada=nenhuma eletrica=1') { $erros += 'inicio: esperava sem simulacao e com eletrica' }
    $e28 = Estrutura $inicio 'Mesa_2V28'
    if (-not $e28 -or $e28.Potencia -ne '720' -or $e28.Pan -ne '-' -or $e28.Voc -ne '-') { $erros += "inicio: Mesa 2V28 [$($e28 | Out-String)] esperava 720 sem PAN" }
    if ($inicio -notmatch 'POTENCIA_DIVERGENCIAS n=0') { $erros += 'inicio: havia divergencia sem PAN nenhum' }
    $mesas28 = Mesas $inicio 'Mesa_2V28' '720'
    $mesas14 = Mesas $inicio 'Mesa_2V14' '720'
    if ($mesas28 -lt 1 -or $mesas14 -lt 1) { $erros += "inicio: esperava mesas das duas estruturas desenhadas a 720 (2V28=$mesas28, 2V14=$mesas14)" }
    $k0 = Kwp $inicio
    $l0 = Lisp 'inicio'
    if (-not $k0 -or -not $l0) { $erros += 'inicio: nao li o kWp ou a conta do LISP' }
    elseif ([math]::Abs($k0.Recontar - $l0.Watts / 1000) -gt 0.01) { $erros += "inicio: recontar $($k0.Recontar) kWp, LISP $($l0.Watts / 1000) kWp" }

    # --- PAN na estrutura: 700 Wp e os dados eletricos; divergencia das mesas desenhadas
    if ($t -notmatch 'ESTRUTURA_PAN estrutura="Mesa 2V28" modelo=RSM132-8-700BHDG pmax=700 antes=720') { $erros += 'o CLIVUS_ESTRUTURA_PAN nao gravou o PAN de 700 na Mesa 2V28 (de 720)' }
    $p28 = Estrutura $comPan 'Mesa_2V28'
    $p14 = Estrutura $comPan 'Mesa_2V14'
    if (-not $p28 -or $p28.Potencia -ne '700' -or $p28.Pan -ne '700' -or $p28.Voc -ne '49.83') { $erros += "PAN: Mesa 2V28 [$($p28.Potencia) $($p28.Pan) $($p28.Voc)], esperava 700 700 49.83" }
    if (-not $p14 -or $p14.Potencia -ne '720' -or $p14.Voc -ne '-') { $erros += "PAN: Mesa 2V14 [$($p14.Potencia) $($p14.Voc)], esperava 720 sem dado eletrico" }
    if ($comPan -notmatch 'POTENCIA_DIVERGENCIAS n=1' -or $comPan -notmatch 'POTENCIA_DIVERGENCIA Estrutura "Mesa 2V28": \d+ mesa\(s\) desenhada\(s\) com m.dulo de 720 Wp, e a estrutura diz 700 Wp') { $erros += 'PAN: a divergencia das mesas desenhadas a 720 contra a estrutura de 700 nao apareceu' }

    # --- atualizar a potencia: so a potencia muda
    if ($t -notmatch "POTENCIA_PAN estrutura=`"Mesa 2V28`" mesas=$mesas28 watts=700") { $erros += "atualizar: esperava $mesas28 mesa(s) da Mesa 2V28 passadas a 700" }
    if ((Mesas $atualizada 'Mesa_2V28' '700') -ne $mesas28 -or (Mesas $atualizada 'Mesa_2V28' '720') -ne 0) { $erros += 'atualizar: sobrou mesa da 2V28 fora de 700' }
    if ((Mesas $atualizada 'Mesa_2V14' '720') -ne $mesas14) { $erros += 'atualizar: mexeu nas mesas da 2V14' }
    if ($atualizada -notmatch 'POTENCIA_DIVERGENCIAS n=0') { $erros += 'atualizar: a divergencia nao sumiu' }
    $l1 = Lisp 'atualizada'
    $k1 = Kwp $atualizada
    if (-not $l1 -or -not $k1) { $erros += 'atualizar: nao li o kWp ou a conta do LISP' }
    else {
        if ($l1.Entidades -ne $l0.Entidades -or [math]::Abs($l1.Soma - $l0.Soma) -gt 0.0001 -or $l1.Modulos -ne $l0.Modulos) { $erros += "atualizar: a geometria mudou (entidades $($l0.Entidades)->$($l1.Entidades), soma $($l0.Soma)->$($l1.Soma))" }
        if ([math]::Abs($k1.Recontar - $l1.Watts / 1000) -gt 0.01) { $erros += "atualizar: recontar $($k1.Recontar) kWp, LISP $($l1.Watts / 1000) kWp" }
        if ($l1.Watts -ge $l0.Watts) { $erros += 'atualizar: os watts nao cairam de 720 para 700' }
    }

    # --- potencia trocada pela area: 650 para a usina inteira, calculos desligados
    if ($t -notmatch 'POTENCIA_SIMULADA watts=650') { $erros += 'o CLIVUS_TROCAR_POTENCIA 650 nao gravou' }
    if ($simulada -notmatch 'POTENCIA_AUTO simulada=650 eletrica=0') { $erros += 'simulada: esperava 650 e os calculos desligados' }
    $s28 = Estrutura $simulada 'Mesa_2V28'
    if (-not $s28 -or $s28.Voc -ne '-') { $erros += 'simulada: a Mesa 2V28 ainda devolve dado eletrico' }
    $k2 = Kwp $simulada
    if (-not $k2) { $erros += 'simulada: nao li o kWp' }
    else {
        if ([math]::Abs($k2.Recontar - $k2.Modulos * 0.65) -gt 0.01) { $erros += "simulada: recontar $($k2.Recontar) kWp, esperava $($k2.Modulos) x 0,65" }
        if ($k2.ResumoModulos -lt 1 -or [math]::Abs($k2.Resumo - $k2.ResumoModulos * 0.65) -gt 0.01) { $erros += "simulada: resumo $($k2.Resumo) kWp, esperava $($k2.ResumoModulos) x 0,65" }
    }
    if ($simulada -notmatch 'POTENCIA_CC nota=1') { $erros += 'simulada: a tabela CC da rota nao diz por que os calculos sumiram' }

    # --- o botao da faixa desfaz; a palavra Mesa tambem
    if ($desfeita -notmatch 'A usina voltou a usar a pot.ncia do m.dulo de cada mesa') { $erros += 'desfazer: o botao da faixa nao disse que voltou' }
    if ($desfeita -notmatch 'POTENCIA_AUTO simulada=nenhuma eletrica=1') { $erros += 'desfazer: a simulacao nao saiu' }
    $d28 = Estrutura $desfeita 'Mesa_2V28'
    if (-not $d28 -or $d28.Voc -ne '49.83') { $erros += 'desfazer: os dados eletricos da Mesa 2V28 nao voltaram' }
    $k3 = Kwp $desfeita
    if (-not $k3 -or [math]::Abs($k3.Recontar - $k1.Recontar) -gt 0.001) { $erros += 'desfazer: o kWp nao voltou ao das mesas' }
    if ($mesa -notmatch 'POTENCIA_SIMULADA watts=600' -or $mesa -notmatch 'POTENCIA_SIMULADA nenhuma' -or $mesa -notmatch 'POTENCIA_AUTO simulada=nenhuma eletrica=1') { $erros += 'a palavra Mesa do comando nao desfez a simulacao de 600' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (potencia: PAN de 700 na Mesa 2V28, $mesas28 mesa(s) passadas a 700 sem mudar a geometria, simulacao 650 desliga a eletrica, desfeita pelo botao e por Mesa)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaPotencia'
