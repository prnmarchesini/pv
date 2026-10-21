<#
    Passo 26 (Analise > Flechas): area de 100 x 100 m no centro do terreno,
    flechas a cada 5 m pelo caminho do botao da janela. Cada ponta na faixa de
    cotas do Civil 3D (lido em LISP) e na cota do TIN mais 5 cm (conferir do
    plugin); legenda com as 5 faixas; gerar de novo nao dobra; Tudo pelo
    comando; apagar zera.
#>
function Testar-DrenagemFlechas {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-flechas'
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

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-flechas.scr') -Substituicoes @{
        '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
        '{{ZMIN}}' = $zmin.ToString('0.###', $inv); '{{ZMAX}}' = $zmax.ToString('0.###', $inv)
    }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    if ($t -notmatch 'CLIVUS_DRNFLECHAS linhas=(\d+) pontos=(\d+) fora=(\d+) insercoes=(\d+) legenda=(\d+) de_novo=(\d+) insercoes2=(\d+) tudo=(\d+) fora_tudo=(\d+) apagado=(\d+) fim') {
        $problemas.Add("${rotulo}: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $linhas = [int] $Matches[1]; $pontos = [int] $Matches[2]; $fora = [int] $Matches[3]
    $insercoes = [int] $Matches[4]; $legenda = [int] $Matches[5]
    $deNovo = [int] $Matches[6]; $insercoes2 = [int] $Matches[7]
    $tudo = [int] $Matches[8]; $foraTudo = [int] $Matches[9]; $apagado = [int] $Matches[10]

    $erros = @()

    # 100 x 100 m a cada 5 m: 400 pontos de grade (20 x 20), menos os planos.
    $flechas = $linhas / 3
    if ($linhas % 3 -ne 0) { $erros += "$linhas LINE no bloco: nao e multiplo de 3 (flecha incompleta)" }
    if ($flechas + $pontos -lt 380 -or $flechas + $pontos -gt 420) { $erros += "$flechas flecha(s) e $pontos ponto(s) plano(s) numa area de 100 x 100 m a cada 5 m (esperado ~400)" }
    if ($fora -ne 0) { $erros += "$fora ponta(s) fora da faixa de cotas do terreno ($zmin a $zmax)" }
    if ($insercoes -ne 1) { $erros += "$insercoes insercao(oes) do bloco das flechas (esperado 1)" }
    if ($legenda -ne 5) { $erros += "legenda com $legenda quadrado(s) de cor (esperado 5)" }
    if ($deNovo -ne $linhas -or $insercoes2 -ne 1) { $erros += "gerar de novo deu $deNovo LINE e $insercoes2 insercao(oes): dobrou ou mudou" }
    if ($tudo -le $linhas) { $erros += "no terreno todo vieram $tudo LINE, nao mais que na area ($linhas)" }
    if ($foraTudo -ne 0) { $erros += "no terreno todo, $foraTudo ponta(s) fora da faixa de cotas" }
    if ($apagado -ne 0) { $erros += "depois de apagar sobraram $apagado entidade(s)" }

    if ($t -notmatch 'DRN_CONFERIR flechas_linhas=(\d+) pontos=(\d+) ok=(\d+) fora=(\d+) sem_terreno=(\d+) fim') {
        $erros += 'o conferir do plugin nao respondeu'
    }
    else {
        $esperado = 2 * [int] $Matches[1] + [int] $Matches[2]
        if ([int] $Matches[3] -ne $esperado -or [int] $Matches[4] -ne 0 -or [int] $Matches[5] -ne 0) {
            $erros += "conferir do plugin: ok=$($Matches[3]) de $esperado, fora=$($Matches[4]), sem terreno=$($Matches[5]) (toda ponta tem que estar no TIN + 5 cm)"
        }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem flechas: $flechas flechas e $pontos pontos na area, $([int]($tudo / 3)) no terreno todo, todas no TIN + 5 cm, legenda de 5 faixas)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemFlechas'
