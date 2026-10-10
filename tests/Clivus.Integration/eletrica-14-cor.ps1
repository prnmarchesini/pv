<#
    A cor por inversor (pedido do Renan em 05/10/2026): os inversores criados
    ganham cores distintas da paleta; a string alocada fica com a cor do
    inversor (DXF 420); trocar a cor repinta as strings dele; soltar volta
    para ByLayer (sem 420); o formato 1 dos inversores (sem cor) e lido com a
    cor automatica e regravado no 2. Usa Substituicoes-Da-Usina.
#>
function Testar-Eletrica14Cor {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-14-cor'
    if (-not $sub) { $problemas.Add('clivus-eletrica-14-cor: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-cor' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-cor.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-cor terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    # O RGB de cada inversor, como o DXF 420 o guarda: a ultima listagem antes da etapa.
    function Rgb([string] $texto, [string] $nome) {
        $m = @([regex]::Matches($texto, "ELETRICA COR nome=""$nome"" cor=#[0-9A-F]{6} rgb=(\d+) fim"))
        if ($m.Count -eq 0) { return $null }
        return $m[-1].Groups[1].Value
    }
    function Strings([string] $etapa) {
        @([regex]::Matches($t, "CLIVUS_STR etapa=$etapa inv=(\S*) c420=(-?\d+) c62=(\d+) fim") | ForEach-Object {
            [pscustomobject]@{ Inv = $_.Groups[1].Value; C420 = $_.Groups[2].Value; C62 = $_.Groups[3].Value }
        })
    }

    $antes = $t.Substring(0, $t.IndexOf('CLIVUS_ETAPA_FIM alocadas'))
    $cores = @('Inversor 1', 'Inversor 2', 'Inversor 3' | ForEach-Object { Rgb $antes $_ })
    # A paleta: laranja, azul, verde (os tres primeiros, na ordem de criacao).
    if (($cores -join ',') -ne '15761920,1999590,2007090') { $erros += "as cores automaticas nao sao laranja, azul e verde: $($cores -join ',')" }

    $alocadas = Strings 'alocadas'
    $grupos = @($alocadas | Where-Object { $_.Inv -ne '' } | Group-Object Inv | Sort-Object Count -Descending)
    if ($grupos.Count -ne 2 -or $grupos[0].Count -ne 4 -or $grupos[1].Count -ne 3) { $erros += 'a alocacao nao deu 4 e 3 strings' }
    else {
        $inv1 = $grupos[0].Name; $inv2 = $grupos[1].Name
        if (@($grupos[0].Group | Where-Object { $_.C420 -ne $cores[0] }).Count -gt 0) { $erros += 'as strings do Inversor 1 nao ficaram laranja' }
        if (@($grupos[1].Group | Where-Object { $_.C420 -ne $cores[1] }).Count -gt 0) { $erros += 'as strings do Inversor 2 nao ficaram azuis' }
        if (@($alocadas | Where-Object { $_.Inv -eq '' -and ($_.C420 -ne '-1' -or $_.C62 -ne '256') }).Count -gt 0) { $erros += 'string livre pintada (devia ser ByLayer)' }

        $trocada = Strings 'trocada'
        if (@($trocada | Where-Object { $_.Inv -eq $inv1 -and $_.C420 -ne '1193046' }).Count -gt 0) { $erros += 'trocar a cor nao repintou as strings do Inversor 1 (#123456)' }
        if (@($trocada | Where-Object { $_.Inv -eq $inv2 -and $_.C420 -ne $cores[1] }).Count -gt 0) { $erros += 'trocar a cor do Inversor 1 mexeu nas do Inversor 2' }
        if ($t -notmatch 'ELETRICA cor de Inversor 1 trocada; 4 string\(s\) repintada\(s\)') { $erros += 'o recado da troca de cor nao diz 4 strings' }

        $soltas = Strings 'soltas'
        if (@($soltas | Where-Object { $_.Inv -eq $inv2 }).Count -ne 0) { $erros += 'soltar nao soltou as do Inversor 2' }
        if (@($soltas | Where-Object { $_.Inv -eq '' -and ($_.C420 -ne '-1' -or $_.C62 -ne '256') }).Count -gt 0) { $erros += 'as soltas nao voltaram para ByLayer' }
        if (@($soltas | Where-Object { $_.Inv -eq $inv1 -and $_.C420 -eq '1193046' }).Count -ne 4) { $erros += 'soltar o Inversor 2 mexeu na cor do Inversor 1' }
        if ($soltas.Count -ne $alocadas.Count) { $erros += 'soltar mudou o numero de strings' }
    }

    $marca = $t.IndexOf('CLIVUS_COR_ANTES_DE_REGRAVAR')
    $antigo = $t.Substring(0, $marca)
    $antigo = $antigo.Substring($antigo.LastIndexOf('ELETRICA inversores do formato 1 gravados'))
    if ($antigo -notmatch 'ELETRICA CORES formato_inversores=1 automaticas=3') { $erros += 'o formato 1 dos inversores nao foi lido com 3 cores automaticas' }
    if ((Rgb $antigo 'Inversor 1') -ne '15761920') { $erros += 'no formato 1 o Inversor 1 nao ganhou a primeira cor da paleta' }
    $final = $t.Substring($marca)
    if ($final -notmatch 'ELETRICA CORES formato_inversores=3 automaticas=0') { $erros += 'a gravacao nao passou os inversores ao formato atual (3)' }
    if ((Rgb $final 'Inversor 4') -ne '12490240') { $erros += 'o Inversor 4 nao ganhou a quarta cor (ouro)' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-cor: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (cor por inversor: strings pintadas pelo 420, troca repinta, soltar volta a ByLayer, formato 1 lido)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica14Cor'
