<#
    Passos 28 e 29 (Canaleta e 3D): linha vira canaleta trapezoidal (borda no
    terreno), troca para meia-cana com fundo de declividade minima, polilinha
    solta vira canaleta, canaleta volta a linha, canaleta movida e apagada.
    Em cada passo: o solido existe, o volume bate com a area da parede vezes o
    comprimento (3 %), as cotas do 3D batem com o fundo e a borda do perfil
    (5 cm), no modo de declividade minima nao sobra trecho ruim, e nada fica
    orfao (grafico, 3D ou pintura sem canaleta).
#>
function Testar-DrenagemCanaleta {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-canaleta'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'cotaMin=(-?[\d.]+) cotaMax=(-?[\d.]+) pontos=\d+ centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("${rotulo}: nao achei o centro e as cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $zmin = [double]::Parse($Matches[1], $inv)
    $zmax = [double]::Parse($Matches[2], $inv)
    $cx = [double]::Parse($Matches[3], $inv)
    $cy = [double]::Parse($Matches[4], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-canaleta.scr') -Substituicoes @{
        '{{P1}}' = (P -30 -30); '{{P2}}' = (P 0 0); '{{P3}}' = (P 20 10); '{{G}}' = (P 150 -80)
        '{{Q1}}' = (P -30 30); '{{Q2}}' = (P 20 35)
        '{{ZMIN}}' = ($zmin - 0.001).ToString('0.###', $inv); '{{ZMAX}}' = ($zmax + 0.001).ToString('0.###', $inv)
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_DRNCAN FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $script:erros = @()

    function Etapa([int] $n) {
        $a = $t.IndexOf("CLIVUS_DRNCAN ETAPA$n")
        if ($a -lt 0) { return '' }
        $b = $t.IndexOf('CLIVUS_DRNCAN ETAPA' + ($n + 1), $a + 10)
        if ($b -lt 0) { $b = $t.IndexOf('CLIVUS_DRNCAN FIM', $a) }
        return $t.Substring($a, $b - $a)
    }

    function Canaletas([string] $trecho) {
        $lista = @()
        foreach ($m in [regex]::Matches($trecho, 'DRN_CANALETA nome=(\S+) solidos=(\d+) malhas=(\d+) volume=([\d.]+) esperado=([\d.]+) zmin=([-\d.]+) zmin_esperado=([-\d.]+) zmax=([-\d.]+) zmax_esperado=([-\d.]+) alertas=(\d+) baixos=(\d+) invertidos=(\d+) pecas=(\d+) fim')) {
            $g = $m.Groups
            $lista += [pscustomobject]@{
                Nome = $g[1].Value; Solidos = [int] $g[2].Value; Malhas = [int] $g[3].Value
                Volume = [double]::Parse($g[4].Value, $inv); Esperado = [double]::Parse($g[5].Value, $inv)
                Zmin = [double]::Parse($g[6].Value, $inv); ZminE = [double]::Parse($g[7].Value, $inv)
                Zmax = [double]::Parse($g[8].Value, $inv); ZmaxE = [double]::Parse($g[9].Value, $inv)
                Alertas = [int] $g[10].Value; Baixos = [int] $g[11].Value; Invertidos = [int] $g[12].Value; Pecas = [int] $g[13].Value
            }
        }
        return , $lista
    }

    function Orfaos([string] $trecho) {
        $o = 0
        if ($trecho -match 'perfis_orfaos=(\d+)') { $o += [int] $Matches[1] }
        if ($trecho -match 'orfaos_3d=(\d+)') { $o += [int] $Matches[1] }
        return $o
    }

    function Linhas([string] $trecho) {
        $lista = @()
        foreach ($m in [regex]::Matches($trecho, 'DRN_LINHA nome=(\S+) guid=\w+ canaleta=(\d) vertices=(\d+) ok=(\d+) fora=(\d+) sem_terreno=(\d+) perfil=(\d+)')) {
            $lista += [pscustomobject]@{ Nome = $m.Groups[1].Value; Canaleta = [int] $m.Groups[2].Value; Vertices = [int] $m.Groups[3].Value; Ok = [int] $m.Groups[4].Value; Perfil = [int] $m.Groups[7].Value }
        }
        return , $lista
    }

    function Conferir($c, [string] $quando) {
        if ($c.Solidos -ne 1) { $script:erros += "${quando}: $($c.Nome) com $($c.Solidos) solido(s) e $($c.Malhas) malha(s) (esperado 1 solido)" ; return }
        if ([math]::Abs($c.Volume - $c.Esperado) -gt 0.03 * $c.Esperado) { $script:erros += "${quando}: $($c.Nome) volume $($c.Volume) m3, esperado $($c.Esperado) (area da parede x comprimento)" }
        if ([math]::Abs($c.Zmin - $c.ZminE) -gt 0.05) { $script:erros += "${quando}: $($c.Nome) fundo do 3D em $($c.Zmin), esperado $($c.ZminE)" }
        if ([math]::Abs($c.Zmax - $c.ZmaxE) -gt 0.05) { $script:erros += "${quando}: $($c.Nome) topo do 3D em $($c.Zmax), esperado $($c.ZmaxE)" }
        if ($c.Pecas -ne 1) { $script:erros += "${quando}: $($c.Nome) com $($c.Pecas) peca(s) (esperado 1: o dissipador no pe)" }
        if ($c.Alertas -ne $c.Baixos + $c.Invertidos) { $script:erros += "${quando}: $($c.Nome) com $($c.Alertas) pintura(s) para $($c.Baixos + $c.Invertidos) trecho(s) ruim(ns)" }
    }

    # 1: linha vira canaleta trapezoidal.
    $c1 = Canaletas (Etapa 1)
    if ($c1.Count -ne 1) { $script:erros += "etapa 1: $($c1.Count) canaleta(s)" } else { Conferir $c1[0] 'trapezoidal' }
    if ((Etapa 1) -notmatch 'camada=CLIVUS_DRN_CANALETA fora=0') { $script:erros += 'a canaleta nao foi para a camada dela, ou tem vertice fora da faixa de cotas' }
    if ((Etapa 1) -notmatch 'CANALETA 1 canaleta\(s\) Trap 30/60') { $script:erros += 'o converter nao relatou a canaleta' }
    $l1 = Linhas (Etapa 1)
    if ($l1.Count -ne 1 -or $l1[0].Canaleta -ne 1 -or $l1[0].Ok -ne $l1[0].Vertices -or $l1[0].Perfil -lt 20) { $script:erros += 'etapa 1: a canaleta nao esta no TIN ou sem grafico' }

    # 2: troca para meia-cana com declividade minima.
    $c2 = Canaletas (Etapa 2)
    if ($c2.Count -ne 1) { $script:erros += "etapa 2: $($c2.Count) canaleta(s)" }
    else {
        Conferir $c2[0] 'meia-cana'
        if ($c2[0].Baixos + $c2[0].Invertidos -ne 0) { $script:erros += "meia-cana com declividade minima ainda tem $($c2[0].Baixos) trecho(s) baixo(s) e $($c2[0].Invertidos) invertido(s)" }
        if ($c1.Count -eq 1 -and [math]::Abs($c2[0].Volume - $c1[0].Volume) -lt 1e-6) { $script:erros += 'trocar a secao nao mudou o solido' }
    }

    # 3: polilinha solta vira canaleta; a original sai.
    $c3 = Canaletas (Etapa 3)
    if ($c3.Count -ne 2) { $script:erros += "etapa 3: $($c3.Count) canaleta(s) (esperado 2)" } else { foreach ($c in $c3) { Conferir $c 'polilinha' } }
    if ((Etapa 3) -notmatch 'original_existe=0') { $script:erros += 'a polilinha convertida continuou no desenho' }

    # 4: a segunda volta a linha; o 3D dela sai.
    $c4 = Canaletas (Etapa 4)
    if ($c4.Count -ne 1) { $script:erros += "etapa 4: $($c4.Count) canaleta(s) (esperado 1)" }
    if ((Orfaos (Etapa 4)) -ne 0) { $script:erros += 'depois de voltar a linha, ficou 3D ou pintura orfao' }
    $l4 = Linhas (Etapa 4)
    if (@($l4 | Where-Object Canaleta -eq 0).Count -ne 1) { $script:erros += 'a canaleta desfeita nao virou linha de analise' }

    # 5: movida, o 3D acompanha.
    $c5 = Canaletas (Etapa 5)
    if ($c5.Count -ne 1) { $script:erros += "etapa 5: $($c5.Count) canaleta(s)" } else { Conferir $c5[0] 'movida' }
    if ($c4.Count -eq 1 -and $c5.Count -eq 1 -and [math]::Abs($c5[0].Zmax - $c4[0].Zmax) -lt 1e-6 -and [math]::Abs($c5[0].Zmin - $c4[0].Zmin) -lt 1e-6) { $script:erros += 'movida 6 m, o 3D nao mudou de cota' }

    # 6: apagada, nada fica.
    $c6 = Canaletas (Etapa 6)
    if ($c6.Count -ne 0) { $script:erros += "etapa 6: $($c6.Count) canaleta(s) depois de apagar" }
    if ((Orfaos (Etapa 6)) -ne 0) { $script:erros += 'apagada a canaleta, ficou grafico, 3D ou pintura' }

    if ($script:erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($script:erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem canaleta: trapezoidal e meia-cana, solido com o volume e as cotas do perfil, minima sem trecho ruim, desfazer, mover e apagar sem orfao)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemCanaleta'
