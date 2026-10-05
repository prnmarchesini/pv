<#
    A composicao livre da tag (05/10/2026). Le os textos DA ENTIDADE (LISP):
    - A: desenho antigo (NUMERACAO formato 1, "Trafo|Inv|S|-") gera as mesmas
      tags de antes (Trafo1-Inv1-S1...; o inversor sem trafo, Inv4-S1);
    - B: o Salvar da aba com T{T}-INV{I:00}S{S}, fundo e moldura: formato 2, e
      os 14 textos ja desenhados ganham fundo (na cor da tela) e moldura sem
      mudar o texto;
    - C: sem {I} com 4 inversores e campo desconhecido, recusados; fica o B;
    - D: gerar a usina pela tela: T1-INV01S1..., INV04S1 (o pedaco do trafo
      some), 14 textos com fundo e moldura, na faixa de cotas do terreno;
    - E/F: apagar e gerar o Bloco 1 so mexem nas strings dele;
    - G/H: apagar e gerar o Inversor 3 so mexem nas dele;
    - I: tirar a moldura deixa o fundo; J: so a moldura, sem fundo.
    Usa as funcoes de eletrica-15-4.ps1.
#>
function Testar-NumeracaoModelo {
    param([string] $Desenho)

    $u = Numeracao-Usina -Desenho $Desenho -Rotulo 'clivus-numeracao-modelo'
    if (-not $u) { $problemas.Add('clivus-numeracao-modelo: nao achei o centro ou as cotas do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-modelo' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-modelo.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-modelo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $nomes = 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J'
    $fases = @{}
    for ($i = 0; $i -lt $nomes.Count; $i++) {
        $de = $t.IndexOf("CLIVUS_FASE $($nomes[$i])")
        $ate = if ($i + 1 -lt $nomes.Count) { $t.IndexOf("CLIVUS_FASE $($nomes[$i + 1])") } else { $t.IndexOf('CLIVUS_NUMERACAO_FIM') }
        if ($de -lt 0 -or $ate -lt $de) { $problemas.Add("clivus-numeracao-modelo: faltou a fase $($nomes[$i]). Veja $($r.Saida)"); return $false }
        $trecho = $t.Substring($de, $ate - $de)
        $tags = @{}
        foreach ($s in @(Numeracao-Strings $trecho)) { $tags["$($s.X),$($s.Y)"] = $s }
        $textos = $null
        if ($trecho -match 'CLIVUS_NUMERACAO_TEXTOS fase=\w textos=(\d+) iguais=(\d+) fundo=(\d+) tela=(\d+) moldura=(\d+) folga=(-?[\d.]+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
            $textos = @{
                N = [int]$Matches[1]; Iguais = [int]$Matches[2]; Fundo = [int]$Matches[3]; Tela = [int]$Matches[4]; Moldura = [int]$Matches[5]
                Folga = [double]::Parse($Matches[6], $inv); Zmin = [double]::Parse($Matches[7], $inv); Zmax = [double]::Parse($Matches[8], $inv)
            }
        }
        $fases[$nomes[$i]] = @{ Trecho = $trecho; Strings = $tags; Textos = $textos }
    }

    $erros = @()

    function Textos-Confere([string] $fase, [int] $n, [int] $fundo, [int] $moldura) {
        $x = $fases[$fase].Textos
        if (-not $x) { return @("fase ${fase}: nao li os textos") }
        $e = @()
        if ($x.N -ne $n -or $x.Iguais -ne $n) { $e += "fase ${fase}: textos=$($x.N) iguais a tag=$($x.Iguais), esperava $n" }
        if ($x.Fundo -ne $fundo -or $x.Tela -ne $fundo) { $e += "fase ${fase}: fundo em $($x.Fundo) (na cor da tela em $($x.Tela)), esperava $fundo" }
        if ($x.Moldura -ne $moldura) { $e += "fase ${fase}: moldura em $($x.Moldura), esperava $moldura" }
        if ($n -gt 0 -and ($x.Zmin -lt ($u.Min - 0.01) -or $x.Zmax -gt ($u.Max + 5))) { $e += "fase ${fase}: tags de $($x.Zmin) a $($x.Zmax), fora da faixa do terreno ($($u.Min) a $($u.Max))" }
        return $e
    }

    # A: o formato 1 gera as tags de antes.
    $A = $fases['A'].Strings
    if ($fases['A'].Trecho -notmatch 'NUMERACAO modelo=Trafo\{T\}-Inv\{I\}-S\{S\} fundo=0 moldura=0 formato=1') { $erros += 'fase A: o registro antigo nao foi lido como Trafo{T}-Inv{I}-S{S} (formato 1)' }
    if ($A.Count -lt 14) { $erros += "fase A: so $($A.Count) string(s) listada(s)" }
    $antigos = @{ 'Inversor_1' = 'Trafo1-Inv1-S'; 'Inversor_2' = 'Trafo1-Inv2-S'; 'Inversor_3' = 'Trafo2-Inv3-S'; 'Inversor_4' = 'Inv4-S' }
    $erros += @(Numeracao-Confere -Strings @($A.Values) -Prefixos $antigos -Sentidos @{ 1 = 'RightToLeft'; 2 = 'RightToLeft'; 0 = 'LeftToRight' } -Blocos 2 | ForEach-Object { "fase A: $_" })
    $erros += Textos-Confere 'A' 14 0 0

    # B: o Salvar da aba, formato 2, fundo e moldura nos textos ja desenhados.
    if ($fases['B'].Trecho -notmatch 'NUMERACAO tela composicao salva: .*14 tag') { $erros += 'fase B: o Salvar nao disse que pos fundo e moldura nas 14 tags' }
    if ($fases['B'].Trecho -notmatch 'NUMERACAO modelo=T\{T\}-INV\{I:00\}S\{S\} fundo=1 moldura=1 formato=2') { $erros += 'fase B: o registro nao ficou T{T}-INV{I:00}S{S} com fundo e moldura (formato 2)' }
    if ($fases['B'].Trecho -notmatch 'Exemplo: T1-INV01S1, T1-INV01S2, T1-INV02S1; inversor sem trafo: INV03S1') { $erros += 'fase B: o exemplo nao confere' }
    $erros += Textos-Confere 'B' 14 14 14
    if ($fases['B'].Textos -and [math]::Abs($fases['B'].Textos.Folga - 1.2) -gt 0.01) { $erros += "fase B: a folga do fundo foi $($fases['B'].Textos.Folga), esperava 1.2" }

    # C: recusados; fica o B.
    if ($fases['C'].Trecho -notmatch 'NUMERACAO tela composicao recusada: .*falta \{I\}.*4 inversores') { $erros += 'fase C: o modelo sem {I} com 4 inversores nao foi recusado' }
    if ($fases['C'].Trecho -notmatch 'NUMERACAO tela composicao recusada: .*campo \{X\} desconhecido') { $erros += 'fase C: o campo {X} nao foi recusado' }
    if ($fases['C'].Trecho -notmatch 'NUMERACAO modelo=T\{T\}-INV\{I:00\}S\{S\} fundo=1 moldura=1 formato=2') { $erros += 'fase C: a recusa mexeu no registro' }

    # D: gerar a usina pela tela.
    $D = $fases['D'].Strings
    $novos = @{ 'Inversor_1' = 'T1-INV01S'; 'Inversor_2' = 'T1-INV02S'; 'Inversor_3' = 'T2-INV03S'; 'Inversor_4' = 'INV04S' }
    if ($fases['D'].Trecho -notmatch 'NUMERACAO tela gerar usina: 14 string\(s\) com tag') { $erros += 'fase D: o Gerar da usina pela tela nao deu 14 tags' }
    $erros += @(Numeracao-Confere -Strings @($D.Values) -Prefixos $novos -Sentidos @{ 1 = 'RightToLeft'; 2 = 'RightToLeft'; 0 = 'LeftToRight' } -Blocos 2 | ForEach-Object { "fase D: $_" })
    $erros += Textos-Confere 'D' 14 14 14

    # E: apagar o Bloco 1 so tira as dele.
    $E = $fases['E'].Strings
    $doBloco1 = @($D.Keys | Where-Object { $D[$_].Bloco -eq 1 -and $D[$_].Tag -ne '-' })
    if ($doBloco1.Count -eq 0) { $erros += 'fase E: o Bloco 1 nao tem string com tag (o caso nao prova nada)' }
    foreach ($k in $D.Keys) {
        $esperado = if ($D[$k].Bloco -eq 1) { '-' } else { $D[$k].Tag }
        if ($E[$k].Tag -ne $esperado) { $erros += "fase E: a string em $k (bloco $($D[$k].Bloco)) ficou $($E[$k].Tag), esperava $esperado"; break }
    }
    if ($fases['E'].Trecho -notmatch "NUMERACAO tela apagar bloco: $($doBloco1.Count) tag\(s\) apagada\(s\)") { $erros += "fase E: a frase nao diz $($doBloco1.Count) tags apagadas" }
    $erros += Textos-Confere 'E' (14 - $doBloco1.Count) (14 - $doBloco1.Count) (14 - $doBloco1.Count)

    # F: gerar o Bloco 1 volta ao D.
    $F = $fases['F'].Strings
    if (@($D.Keys | Where-Object { $D[$_].Tag -ne $F[$_].Tag }).Count -gt 0) { $erros += 'fase F: gerar o Bloco 1 nao voltou as tags da usina inteira' }
    if ($fases['F'].Trecho -match 'repetida|desatualizada') { $erros += 'fase F: avisou tag repetida ou desatualizada sem ter' }
    $erros += Textos-Confere 'F' 14 14 14

    # G: apagar o Inversor 3 so tira as dele.
    $G = $fases['G'].Strings
    $doTres = @($F.Keys | Where-Object { $F[$_].Inversor -eq 'Inversor_3' })
    foreach ($k in $F.Keys) {
        $esperado = if ($F[$k].Inversor -eq 'Inversor_3') { '-' } else { $F[$k].Tag }
        if ($G[$k].Tag -ne $esperado) { $erros += "fase G: a string em $k ficou $($G[$k].Tag), esperava $esperado"; break }
    }
    if ($fases['G'].Trecho -notmatch "NUMERACAO tela apagar inversor: $($doTres.Count) tag\(s\) apagada\(s\)") { $erros += "fase G: a frase nao diz $($doTres.Count) tags apagadas" }
    $erros += Textos-Confere 'G' (14 - $doTres.Count) (14 - $doTres.Count) (14 - $doTres.Count)

    # H: gerar o Inversor 3 volta ao D.
    $H = $fases['H'].Strings
    if (@($D.Keys | Where-Object { $D[$_].Tag -ne $H[$_].Tag }).Count -gt 0) { $erros += 'fase H: gerar o Inversor 3 nao voltou as tags da usina inteira' }
    $erros += Textos-Confere 'H' 14 14 14

    # I: sem moldura, o fundo fica.
    $erros += Textos-Confere 'I' 14 14 0

    # J: so a moldura, sem fundo.
    $erros += Textos-Confere 'J' 14 0 14

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-modelo: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numeracao: formato antigo igual; T{T}-INV{I:00}S{S} com fundo e moldura lidos do MTEXT; apagar/gerar Bloco 1 ($($doBloco1.Count)) e Inversor 3 ($($doTres.Count)) pela tela sem tocar no resto)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoModelo'
