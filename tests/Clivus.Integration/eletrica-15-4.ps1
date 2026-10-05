<#
    15.4: gerar as tags. A cadeia de exemplo (CLIVUS_NUMERACAO_EXEMPLO_AUTO):
    inversores 1 e 2 no trafo TA (o 1o da lista), 3 no TB, 4 sem trafo; 14
    strings alocadas pela ordem do handle. Confere, em duas fases (sem blocos;
    com dois blocos e a lista invertida): a tag de cada string pelo inversor do
    vinculo, o sequencial 1..n por inversor na ordem da varredura, os textos
    no desenho (lidos em LISP) iguais a tag gravada, na camada propria e na
    faixa de cotas do terreno (regra 5).

    Tambem define as funcoes que os casos 15.5 e 16.1 usam.
#>

# A usina (pontos da area e do alinhamento) e a faixa de cotas do terreno, de uma sonda so.
function Numeracao-Usina {
    param([string] $Desenho, [string] $Rotulo)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { return $null }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') { return $null }
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $min = [double]::Parse($Matches[1], $ptbr)
    $max = [double]::Parse($Matches[2], $ptbr)

    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###},0', $cx + $dx, $cy + $dy) }

    return @{
        Sub = @{
            '{{A1}}' = (P -50 -50); '{{A2}}' = (P 50 -50); '{{A3}}' = (P 50 50); '{{A4}}' = (P -50 50)
            '{{L1}}' = (P -50 -50); '{{L2}}' = (P -50 50); '{{LADO}}' = (P 0 0)
        }
        Min = $min
        Max = $max
    }
}

# As linhas NUMERACAO_STRING de um trecho da saida.
function Numeracao-Strings {
    param([string] $Texto)

    $inv = [Globalization.CultureInfo]::InvariantCulture
    foreach ($m in [regex]::Matches($Texto, 'NUMERACAO_STRING tag=(\S+) inversor=(\S+) bloco=(\S+) x=(\S+) y=(\S+)')) {
        $p = $m.Groups
        [pscustomobject]@{
            Tag      = $p[1].Value
            Inversor = $p[2].Value
            Bloco    = if ($p[3].Value -eq '-') { -1 } else { [int]$p[3].Value }
            X        = if ($p[4].Value -eq '-') { [double]::NaN } else { [double]::Parse($p[4].Value, $inv) }
            Y        = if ($p[5].Value -eq '-') { [double]::NaN } else { [double]::Parse($p[5].Value, $inv) }
        }
    }
}

<#
    Confere a numeracao de uma fase. $Prefixos: inversor -> comeco da tag
    (ex. Inversor_1 -> 'T1.I1.S'). $Sentidos: bloco (0 = fora de bloco) ->
    sentido (LeftToRight...). Devolve a lista de erros.
#>
function Numeracao-Confere {
    param($Strings, [hashtable] $Prefixos, [hashtable] $Sentidos, [int] $Blocos)

    $erros = @()
    foreach ($grupo in ($Strings | Where-Object { $_.Inversor -ne '-' } | Group-Object Inversor)) {
        $prefixo = $Prefixos[$grupo.Name]
        $itens = @()
        foreach ($s in $grupo.Group) {
            if (-not $s.Tag.StartsWith($prefixo) -or $s.Tag.Substring($prefixo.Length) -notmatch '^\d+$') { $erros += "$($grupo.Name): tag $($s.Tag) nao comeca com $prefixo"; continue }
            $itens += [pscustomobject]@{ N = [int]$s.Tag.Substring($prefixo.Length); S = $s }
        }

        $ordem = @($itens | Sort-Object N)
        $numeros = ($ordem | ForEach-Object { $_.N }) -join ','
        $esperado = (1..$ordem.Count) -join ','
        if ($numeros -ne $esperado) { $erros += "$($grupo.Name): sequencial $numeros, esperava $esperado"; continue }

        for ($i = 1; $i -lt $ordem.Count; $i++) {
            $a = $ordem[$i - 1].S; $b = $ordem[$i].S
            $ra = if ($a.Bloco -eq 0) { $Blocos + 1 } else { $a.Bloco }
            $rb = if ($b.Bloco -eq 0) { $Blocos + 1 } else { $b.Bloco }
            if ($rb -lt $ra) { $erros += "$($grupo.Name): $($b.Tag) (bloco $($b.Bloco)) depois de $($a.Tag) (bloco $($a.Bloco)) fura a ordem dos blocos"; continue }
            if ($rb -gt $ra) { continue }

            # O eixo que avanca (p) e o da faixa (s), como no Core.
            switch ($Sentidos[$a.Bloco]) {
                'LeftToRight' { $pa = $a.X;  $pb = $b.X;  $sa = -$a.Y; $sb = -$b.Y }
                'RightToLeft' { $pa = -$a.X; $pb = -$b.X; $sa = -$a.Y; $sb = -$b.Y }
                'TopToBottom' { $pa = -$a.Y; $pb = -$b.Y; $sa = $a.X;  $sb = $b.X }
                default       { $pa = $a.Y;  $pb = $b.Y;  $sa = $a.X;  $sb = $b.X }
            }

            $ok = ($pb -ge $pa - 0.5) -and (($pb -gt $pa + 0.5) -or ($sb -ge $sa - 0.001))
            if (-not $ok) { $erros += "$($grupo.Name): $($b.Tag) em ($($b.X), $($b.Y)) nao vem depois de $($a.Tag) em ($($a.X), $($a.Y)) no sentido $($Sentidos[$a.Bloco])" }
        }
    }

    return $erros
}

function Testar-NumeracaoGerar {
    param([string] $Desenho)

    $u = Numeracao-Usina -Desenho $Desenho -Rotulo 'clivus-numeracao-gerar'
    if (-not $u) { $problemas.Add('clivus-numeracao-gerar: nao achei o centro ou as cotas do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-gerar' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-gerar.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-gerar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'NUMERACAO_EXEMPLO ucs=1 trafos=2 inversores=4 alocadas=14 livres=(\d+)') {
        $problemas.Add("clivus-numeracao-gerar: a cadeia de exemplo nao alocou 14 strings. Veja $($r.Saida)")
        return $false
    }
    $livres = [int]$Matches[1]

    $prefixos = @{ 'Inversor_1' = 'T1.I1.S'; 'Inversor_2' = 'T1.I2.S'; 'Inversor_3' = 'T2.I3.S'; 'Inversor_4' = 'I4.S' }
    $fase1 = $t.Substring($t.IndexOf('CLIVUS_FASE 1'), $t.IndexOf('CLIVUS_FASE 2') - $t.IndexOf('CLIVUS_FASE 1'))
    $fase2 = $t.Substring($t.IndexOf('CLIVUS_FASE 2'))

    foreach ($fase in @(@{ N = '1'; T = $fase1; Sentidos = @{ 0 = 'LeftToRight' }; Blocos = 0 },
                        @{ N = '2'; T = $fase2; Sentidos = @{ 0 = 'LeftToRight'; 1 = 'RightToLeft'; 2 = 'BottomToTop' }; Blocos = 2 })) {
        $n = $fase.N
        $strings = @(Numeracao-Strings $fase.T)
        $comTag = @($strings | Where-Object { $_.Tag -ne '-' })

        if ($fase.T -notmatch "NUMERACAO 14 string\(s\) com tag; $livres sem tag") { $erros += "fase ${n}: o resumo nao diz 14 com tag e $livres sem" }
        if ($fase.T -notmatch "$livres string\(s\) sem inversor ficaram sem tag") { $erros += "fase ${n}: nao avisou as $livres sem inversor" }
        if ($fase.T -notmatch '1 inversor\(es\) sem trafo \(Inversor 4\)') { $erros += "fase ${n}: nao avisou o inversor 4 sem trafo" }
        if ($fase.T -notmatch 'Inversor 1 acima da capacidade: 5 strings em 4 entradas') { $erros += "fase ${n}: nao avisou o inversor 1 acima da capacidade" }
        if ($comTag.Count -ne 14 -or @($strings | Where-Object { $_.Inversor -eq '-' -and $_.Tag -ne '-' }).Count -ne 0) { $erros += "fase ${n}: $($comTag.Count) string(s) com tag (esperava 14, so as alocadas)" }
        if ($fase.N -eq '2' -and @($strings | Where-Object { $_.Bloco -eq 0 }).Count -ne 0) { $erros += 'fase 2: string fora dos dois blocos' }

        $erros += @(Numeracao-Confere -Strings $strings -Prefixos $prefixos -Sentidos $fase.Sentidos -Blocos $fase.Blocos | ForEach-Object { "fase ${n}: $_" })

        if ($t -notmatch "CLIVUS_NUMERACAO_TEXTOS fase=$n textos=(\d+) iguais=(\d+) camada=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)") { $erros += "fase ${n}: nao li os textos"; continue }
        $m = $Matches
        if ($m[1] -ne '14' -or $m[2] -ne '14' -or $m[3] -ne '14') { $erros += "fase ${n}: textos=$($m[1]) iguais a tag=$($m[2]) na camada=$($m[3]) (esperava 14, 14, 14: regerar nao pode empilhar)" }
        $inv = [Globalization.CultureInfo]::InvariantCulture
        $zmin = [double]::Parse($m[4], $inv); $zmax = [double]::Parse($m[5], $inv)
        if ($zmin -lt ($u.Min - 0.01) -or $zmax -gt ($u.Max + 5)) { $erros += "fase ${n}: tags de $zmin a $zmax, fora da faixa do terreno ($($u.Min) a $($u.Max))" }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-gerar: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numeracao: 14 tags por inversor na ordem da varredura, sem e com blocos; $livres sem inversor avisadas; textos no terreno)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoGerar'
