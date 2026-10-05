<#
    15.5: edicao granular. Regerar um bloco (depois de mudar o sentido dele)
    muda so as strings dele, e elas ficam na ordem nova; apagar as de um
    inversor e refazer esse inversor nao tocam nos outros; apagar todas
    esvazia tudo, textos inclusive. Usa as funcoes de eletrica-15-4.ps1.
#>
function Testar-NumeracaoEditar {
    param([string] $Desenho)

    $u = Numeracao-Usina -Desenho $Desenho -Rotulo 'clivus-numeracao-editar'
    if (-not $u) { $problemas.Add('clivus-numeracao-editar: nao achei o centro ou as cotas do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-editar' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-editar.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-editar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $fases = @{}
    $nomes = 'A', 'B', 'C', 'D', 'E'
    for ($i = 0; $i -lt $nomes.Count; $i++) {
        $de = $t.IndexOf("CLIVUS_FASE $($nomes[$i])")
        $ate = if ($i + 1 -lt $nomes.Count) { $t.IndexOf("CLIVUS_FASE $($nomes[$i + 1])") } else { $t.IndexOf('CLIVUS_NUMERACAO_FIM') }
        $trecho = $t.Substring($de, $ate - $de)
        $tags = @{}
        foreach ($s in @(Numeracao-Strings $trecho)) { $tags["$($s.X),$($s.Y)"] = $s }
        $textos = if ($trecho -match 'CLIVUS_NUMERACAO_TEXTOS fase=\w textos=(\d+)') { [int]$Matches[1] } else { -1 }
        $fases[$nomes[$i]] = @{ Trecho = $trecho; Strings = $tags; Textos = $textos }
    }

    $A = $fases['A'].Strings; $B = $fases['B'].Strings; $C = $fases['C'].Strings; $D = $fases['D'].Strings; $E = $fases['E'].Strings
    $prefixos = @{ 'Inversor_1' = 'T1.I1.S'; 'Inversor_2' = 'T1.I2.S'; 'Inversor_3' = 'T2.I3.S'; 'Inversor_4' = 'I4.S' }
    $erros = @()

    if ($A.Count -lt 14) { $erros += "fase A: so $($A.Count) string(s) listada(s)" }

    # B: so as do Bloco 2 podem mudar; a ordem nova confere com o Bloco 2 de baixo para cima.
    $mudaram = @($A.Keys | Where-Object { $A[$_].Tag -ne $B[$_].Tag })
    $foraDoBloco = @($mudaram | Where-Object { $A[$_].Bloco -ne 2 })
    if ($foraDoBloco.Count -gt 0) { $erros += "fase B: $($foraDoBloco.Count) string(s) fora do Bloco 2 mudaram de tag" }
    if ($mudaram.Count -eq 0) { $erros += 'fase B: nenhuma tag do Bloco 2 mudou com o sentido novo (o caso nao prova nada)' }
    $erros += @(Numeracao-Confere -Strings @($B.Values) -Prefixos $prefixos -Sentidos @{ 1 = 'RightToLeft'; 2 = 'BottomToTop'; 0 = 'LeftToRight' } -Blocos 2 | ForEach-Object { "fase B: $_" })
    if ($fases['B'].Trecho -match 'repetida') { $erros += 'fase B: avisou tag repetida sem ter' }

    # C: so as do Inversor 2 ficam sem tag; as outras como em B.
    $doDois = @($B.Keys | Where-Object { $B[$_].Inversor -eq 'Inversor_2' })
    foreach ($k in $B.Keys) {
        $esperado = if ($B[$k].Inversor -eq 'Inversor_2') { '-' } else { $B[$k].Tag }
        if ($C[$k].Tag -ne $esperado) { $erros += "fase C: a string em $k ficou $($C[$k].Tag), esperava $esperado"; break }
    }
    if ($fases['C'].Trecho -notmatch "NUMERACAO $($doDois.Count) tag\(s\) apagada\(s\) de $($doDois.Count) string\(s\)") { $erros += "fase C: a frase nao diz $($doDois.Count) tags apagadas" }

    # D: refazer o Inversor 2 volta ao que era em B.
    $diferentes = @($B.Keys | Where-Object { $B[$_].Tag -ne $D[$_].Tag })
    if ($diferentes.Count -gt 0) { $erros += "fase D: $($diferentes.Count) string(s) diferentes de B depois de refazer o Inversor 2" }

    # E: todas sem tag.
    if (@($E.Values | Where-Object { $_.Tag -ne '-' }).Count -gt 0) { $erros += 'fase E: sobrou tag depois de apagar todas' }

    $contagens = "$($fases['A'].Textos) $($fases['B'].Textos) $($fases['C'].Textos) $($fases['D'].Textos) $($fases['E'].Textos)"
    $esperadas = "14 14 $(14 - $doDois.Count) 14 0"
    if ($contagens -ne $esperadas) { $erros += "textos no desenho por fase [$contagens], esperava [$esperadas]" }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-editar: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numeracao: regerar o Bloco 2 mudou $($mudaram.Count) tag(s) so dele; apagar e refazer o Inversor 2 ($($doDois.Count) strings) sem tocar nos outros; apagar todas)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoEditar'
