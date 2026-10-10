<#
    Rota MT (roteamento; pedidos do Renan em 10/10/2026):
      - a vala desenhada em 2D (a principal na cota zero, o rabicho com Z = 9999) vira 3D e acompanha o TIN
        na profundidade da aba MT (1,00 m): cada vertice, lido da entidade,
        tem que estar no terreno daquele XY menos 1,00 (regra universal:
        "TODO desenho respeita o TIN"); nada na cota do clique;
      - o cabo MT fica entre o fundo da vala e a base do equipamento
        (terreno + 0,80), com o ponto mais baixo no fundo da vala;
      - o rabicho que entra no retangulo do trafo vale antes do raio: o cabo
        passa pela juncao dele com a principal. Pelo raio (como era), ele
        cortava em diagonal ate um vertice da principal mais ao sul.
    O desenho:
      principal V1 (12, 40) -> V2 (12, 5) -> V3 (12, -30) -> V4 (-17, -30);
      rabicho R1 (12, 10) -> R2 (20, 10); trafo T1 em (20, 10); U1 em (-20, -30).
#>
function Testar-EletricaRotaMt {
    param([string] $Desenho)

    $rotulo = 'clivus-rota-mt'
    $pontos = Pontos-Do-Terreno -Desenho $Desenho -Rotulo $rotulo -Deslocamentos @{
        T = @(20, 10); U = @(-20, -30)
        V1 = @(12, 40); V2 = @(12, 5); V3 = @(12, -30); V4 = @(-17, -30)
        R1 = @(12, 10); R2 = @(20, 10)
    }
    if (-not $pontos) { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    # A principal em 2D (o PLINE so aceita X,Y depois do primeiro ponto): cota zero, como o Renan desenha.
    $inv = [Globalization.CultureInfo]::InvariantCulture
    foreach ($v in 'V1', 'V2', 'V3', 'V4') { $pontos.Sub["{{$v}}"] = [string]::Format($inv, '{0:0.###},{1:0.###}', $pontos.XY[$v][0], $pontos.XY[$v][1]) }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-rota-mt.scr') -Substituicoes $pontos.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ROTA_FIM') -lt 0 -or $r.Texto.IndexOf('ROTA_CONFERIR_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    $valas = @([regex]::Matches($t, 'ROTA_VALA rota=MediumVoltage tipo=(\S+) vertices=(\d+) fora=(\d+) desvio=([\d.]+)'))
    if ($valas.Count -ne 2) { $erros += "esperava 2 valas MT, achei $($valas.Count)" }
    foreach ($v in $valas) {
        if ($v.Groups[1].Value -ne 'Polyline3d') { $erros += "vala ficou $($v.Groups[1].Value), nao Polyline3d" }
        if ([int]$v.Groups[3].Value -ne 0) { $erros += "vala com $($v.Groups[3].Value) ponto(s) fora do terreno" }
        if ([double]::Parse($v.Groups[4].Value, $inv) -gt 0.001) { $erros += "vala fora do terreno menos 1,00 m (desvio $($v.Groups[4].Value) m)" }
    }
    # A principal tem que ter ganho vertices nas arestas do TIN (mais que os 4 desenhados).
    if (-not ($valas | Where-Object { [int]$_.Groups[2].Value -gt 4 })) { $erros += 'a principal nao ganhou vertices nas arestas do TIN' }

    $cabos = @([regex]::Matches($t, 'ROTA_CABO rota=MediumVoltage vertices=(\d+) fora=(\d+) abaixo=([\d.]+) acima=([\d.]+) zmin=(-?[\d.]+) planta=(\S+)'))
    if ($cabos.Count -ne 1) { $erros += "esperava 1 cabo MT, achei $($cabos.Count)" }
    else {
        $c = $cabos[0]
        if ([int]$c.Groups[2].Value -ne 0) { $erros += "cabo com $($c.Groups[2].Value) ponto(s) fora do terreno" }
        if ([double]::Parse($c.Groups[3].Value, $inv) -gt 0.001) { $erros += "cabo abaixo do fundo da vala ($($c.Groups[3].Value) m)" }
        if ([double]::Parse($c.Groups[4].Value, $inv) -gt 0.001) { $erros += "cabo acima da base do equipamento ($($c.Groups[4].Value) m)" }

        $planta = @($c.Groups[6].Value -split ';' | ForEach-Object { $xy = $_ -split ','; ,@([double]::Parse($xy[0], $inv), [double]::Parse($xy[1], $inv)) })
        function Passa([double[]] $p) { @($planta | Where-Object { [math]::Abs($_[0] - $p[0]) -lt 0.01 -and [math]::Abs($_[1] - $p[1]) -lt 0.01 }).Count -gt 0 }
        if (-not (Passa $pontos.XY.R1)) { $erros += 'o cabo nao passou pela juncao do rabicho com a principal (foi pelo raio)' }
        if (-not (Passa $pontos.XY.R2) -and -not (Passa $pontos.XY.T)) { $erros += 'o cabo nao saiu pela ponta do rabicho dentro do trafo' }
    }

    if ($t -notmatch 'viraram vala M\S*, na camada CLIVUS_VALA_MT, assentadas no terreno a 1[.,]00 m') { $erros += 'o Selecionar vala nao disse que assentou a 1,00 m' }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (rota MT: vala 2D com Z 9999 virou 3D no TIN menos 1,00 m; cabo entre o fundo da vala e a base; rabicho antes do raio)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaRotaMt'
