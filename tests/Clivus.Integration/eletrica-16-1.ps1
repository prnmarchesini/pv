<#
    16.1: o resumo do sistema pela cadeia de vinculo. A cadeia de exemplo
    (UC1 com TA; TB sem UC; inversores 1 e 2 no TA, 3 no TB, 4 sem trafo;
    modelo de 4 entradas; 5, 4, 3 e 2 strings) e conferida linha a linha, e
    os totais (strings alocadas, modulos e kWp) contra a conta refeita em
    LISP pelo XData (watts pela mesa dona de cada modulo). Antes de gerar a
    numeracao, as 14 alocadas aparecem como pendencia sem tag; depois, nao.
#>
function Testar-EletricaResumo {
    param([string] $Desenho)

    $u = Numeracao-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-resumo'
    if (-not $u) { $problemas.Add('clivus-eletrica-resumo: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-resumo' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-resumo.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_RESUMO_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-resumo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    if ($t -notmatch 'CLIVUS_RESUMO_LISP strings=(\d+) alocadas=(\d+) modulos=(\d+) watts=([\d.]+) porinversor=([\d,]+)') {
        $problemas.Add("clivus-eletrica-resumo: nao li a conta do LISP. Veja $($r.Saida)")
        return $false
    }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $lisp = @{ Strings = [int]$Matches[1]; Alocadas = [int]$Matches[2]; Modulos = [int]$Matches[3]; Kwp = [double]::Parse($Matches[4], $inv) / 1000; Por = $Matches[5].TrimEnd(',') }
    $antes = $t.Substring($t.IndexOf('CLIVUS_FASE ANTES'), $t.IndexOf('CLIVUS_FASE DEPOIS') - $t.IndexOf('CLIVUS_FASE ANTES'))
    $depois = $t.Substring($t.IndexOf('CLIVUS_FASE DEPOIS'))
    $erros = @()

    if ($lisp.Alocadas -ne 14 -or $lisp.Por -ne '5,4,3,2') { $erros += "a cadeia de exemplo nao alocou 5, 4, 3 e 2 strings (LISP: $($lisp.Alocadas), $($lisp.Por))" }

    foreach ($fase in @(@{ N = 'antes'; T = $antes }, @{ N = 'depois'; T = $depois })) {
        $n = $fase.N; $f = $fase.T
        if ($f -notmatch 'RESUMO_TOTAIS ucs=(\d+) trafos=(\d+) inversores=(\d+) strings=(\d+) alocadas=(\d+) livres=(\d+) modulos=(\d+) kwp=([\d.]+)') { $erros += "${n}: nao li os totais"; continue }
        $m = $Matches
        $obtido = "ucs=$($m[1]) trafos=$($m[2]) inversores=$($m[3]) strings=$($m[4]) alocadas=$($m[5]) livres=$($m[6]) modulos=$($m[7])"
        $esperado = "ucs=1 trafos=2 inversores=4 strings=$($lisp.Strings) alocadas=14 livres=$($lisp.Strings - 14) modulos=$($lisp.Modulos)"
        if ($obtido -ne $esperado) { $erros += "${n}: totais [$obtido], esperava [$esperado]" }
        $kwp = [double]::Parse($m[8], $inv)
        if ([math]::Abs($kwp - $lisp.Kwp) -gt 0.01) { $erros += "${n}: $kwp kWp no resumo, $($lisp.Kwp) kWp pela conta do LISP" }

        $linhas = @{}
        $somaModulos = 0
        foreach ($l in [regex]::Matches($f, 'RESUMO_INVERSOR nome=(\S+) trafo=(\S+) uc=(\S+) strings=(\d+) capacidade=(\d+) excesso=(\d) modulos=(\d+) kwp=')) {
            $linhas[$l.Groups[1].Value] = "$($l.Groups[2].Value) $($l.Groups[3].Value) $($l.Groups[4].Value) $($l.Groups[5].Value) $($l.Groups[6].Value)"
            $somaModulos += [int]$l.Groups[7].Value
        }

        $cadeia = @{ 'Inversor_1' = 'TA UC1 5 4 1'; 'Inversor_2' = 'TA UC1 4 4 0'; 'Inversor_3' = 'TB - 3 4 0'; 'Inversor_4' = '- - 2 4 0' }
        foreach ($k in $cadeia.Keys) { if ($linhas[$k] -ne $cadeia[$k]) { $erros += "${n}: $k [$($linhas[$k])], esperava [$($cadeia[$k])] (trafo uc strings capacidade excesso)" } }
        if ($somaModulos -ne $lisp.Modulos) { $erros += "${n}: os inversores somam $somaModulos modulos, o total e $($lisp.Modulos)" }

        foreach ($p in 'Inversor 1 acima da capacidade: 5 strings em 4 entradas', '1 inversor\(es\) sem trafo: Inversor 4', '1 trafo\(s\) sem subesta..o: TB', "$($lisp.Strings - 14) string\(s\) sem inversor") {
            if ($f -notmatch $p) { $erros += "${n}: a pendencia [$p] nao apareceu" }
        }
    }

    if ($antes -notmatch '14 string\(s\) alocada\(s\) ainda sem tag') { $erros += 'antes: nao apontou as 14 alocadas sem tag' }
    if ($depois -match 'ainda sem tag') { $erros += 'depois: ainda aponta string sem tag depois de gerar' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-resumo: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (resumo: UC1 > TA > inversores 1 e 2, TB sem UC, inversor 4 sem trafo; 14 strings, $($lisp.Modulos) modulos, $([math]::Round($lisp.Kwp, 2)) kWp iguais a conta do LISP)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaResumo'
