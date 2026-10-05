<#
    12.3: o retangulo da subestacao em campo (regra sagrada 5: a cota vem do
    terreno, nunca do clique). Os pontos chegam com Z = 9999; a cota de cada
    retangulo e lida DA ENTIDADE (LISP) e tem que ser a do terreno no ponto
    + 0,80 m, dentro da faixa do terreno. Por em campo de novo move o mesmo
    retangulo; o vinculo com o trafo fica.

    Conferir-Equipamentos e usada tambem pelos casos do trafo (13.2) e do
    inversor (14.6).
#>
function Pontos-Do-Terreno {
    param([string] $Desenho, [string] $Rotulo, [hashtable] $Deslocamentos)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { return $null }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)

    $sub = @{}
    $xy = @{}
    foreach ($nome in $Deslocamentos.Keys) {
        $d = $Deslocamentos[$nome]
        $sub["{{$nome}}"] = [string]::Format($inv, '{0:0.###},{1:0.###},9999', $cx + $d[0], $cy + $d[1])
        $xy[$nome] = @(($cx + $d[0]), ($cy + $d[1]))
    }
    return @{ Sub = $sub; XY = $xy }
}

<#
    Confere os retangulos lidos da entidade (linhas CLIVUS_EQUIP) contra os
    relatorios do comando (EQUIPAMENTO ... em campo: terreno a Z m, base a B m)
    e contra a faixa de cotas do terreno. $Esperados: lista de @{ Tipo; XY;
    Relatorio = indice do relatorio que vale para esse retangulo }.
    Devolve a lista de erros (vazia = tudo certo).
#>
function Conferir-Equipamentos {
    param([string] $Texto, [object[]] $Esperados)

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $erros = @()

    if ($Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') { return @('nao achei a faixa de cotas do terreno') }
    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    $relatorios = @([regex]::Matches($Texto, 'EQUIPAMENTO .+? em campo: terreno a (-?[\d.,]+) m, base a (-?[\d.,]+) m') | ForEach-Object {
        @{ Chao = [double]::Parse($_.Groups[1].Value.Replace(',', '.'), $inv); Base = [double]::Parse($_.Groups[2].Value.Replace(',', '.'), $inv) }
    })

    $lidos = @([regex]::Matches($Texto, 'CLIVUS_EQUIP tipo=(\w+) x=(-?[\d.]+) y=(-?[\d.]+) z=(-?[\d.]+) layer=(\S+) bloco=(\S+) fim') | ForEach-Object {
        @{ Tipo = $_.Groups[1].Value; X = [double]::Parse($_.Groups[2].Value, $inv); Y = [double]::Parse($_.Groups[3].Value, $inv)
           Z = [double]::Parse($_.Groups[4].Value, $inv); Layer = $_.Groups[5].Value }
    })

    if ($lidos.Count -ne $Esperados.Count) { $erros += "$($lidos.Count) retangulo(s) no desenho, esperava $($Esperados.Count)" }

    foreach ($e in $Esperados) {
        $achado = $lidos | Where-Object { $_.Tipo -eq $e.Tipo -and [math]::Abs($_.X - $e.XY[0]) -lt 0.01 -and [math]::Abs($_.Y - $e.XY[1]) -lt 0.01 } | Select-Object -First 1
        if (-not $achado) { $erros += "nenhum $($e.Tipo) em $($e.XY -join ',')"; continue }
        if ($achado.Layer -ne 'CLIVUS_EQUIPAMENTO') { $erros += "$($e.Tipo) na camada $($achado.Layer)" }
        if ($e.Relatorio -ge $relatorios.Count) { $erros += "faltou o relatorio $($e.Relatorio) do comando"; continue }

        $rel = $relatorios[$e.Relatorio]
        $chao = $achado.Z - 0.8
        if ($achado.Z -gt 9000) { $erros += "$($e.Tipo) ficou com o Z do clique ($($achado.Z))" }
        if ($chao -lt $minima - 0.001 -or $chao -gt $maxima + 0.001) { $erros += "$($e.Tipo): base Z=$($achado.Z) menos 0,80 fora da faixa do terreno ($minima a $maxima)" }
        if ([math]::Abs($achado.Z - $rel.Base) -gt 0.002 -or [math]::Abs($rel.Base - $rel.Chao - 0.8) -gt 0.002) {
            $erros += "$($e.Tipo): Z da entidade $($achado.Z), relatorio terreno $($rel.Chao) base $($rel.Base)"
        }
    }

    return $erros
}

function Testar-Eletrica123 {
    param([string] $Desenho)

    $pontos = Pontos-Do-Terreno -Desenho $Desenho -Rotulo 'clivus-eletrica-12-3' -Deslocamentos @{ P1 = @(-20, -20); P2 = @(15, -20); P3 = @(-20, 15) }
    if (-not $pontos) { $problemas.Add('clivus-eletrica-12-3: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-12-3' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-12-3.scr') -Substituicoes $pontos.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-12-3 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    # Relatorios na ordem: C1 em P1, U1 em P2, C1 em P3 (movida).
    $erros = @(Conferir-Equipamentos -Texto $t -Esperados @(
        @{ Tipo = 'ConsumerUnit'; XY = $pontos.XY.P3; Relatorio = 2 },
        @{ Tipo = 'ConsumerUnit'; XY = $pontos.XY.P2; Relatorio = 1 }))

    if ($t -notmatch 'EQUIPAMENTO N\S+o h\S+ equipamento "C9"') { $erros += 'equipamento inexistente nao foi avisado' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 subestacao(oes)'))
    if ($final -notmatch 'ELETRICA UC C1 modo=Shared .* trafos=T1\s') { $erros += 'o vinculo C1-T1 mudou ao mover' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-12-3: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (subestacao em campo: cota do terreno + 0,80 lida da entidade, mover sem duplicar, vinculo intacto)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica123'
