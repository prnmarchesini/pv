<#
    Aba Numeracao, 05/10/2026 (Renan: "o botao selecionar mesas nao deixa
    selecionar, e cada bloco precisa de dois sentidos direita-esquerda e
    cima-baixo ... um resumo de quantas strings o bloco tem").
    - O registro NUMERACAO_VARREDURA antigo (formato 1, sem o sentido na
      faixa) e lido com o sentido na faixa de antes e regravado no formato 2.
    - O Selecionar da linha do bloco: o comando CLIVUS_NUMERACAO_MESAS (o que
      o botao manda pela linha de comando) grava as mesas do bloco.
    - Os dois sentidos da usina e de cada bloco: o paralelo e recusado, e a
      tag gerada segue o que avanca E o da faixa (pares na mesma faixa
      conferidos, e tem que haver par na faixa, senao o teste nao prova nada).
    - O resumo: mesas e strings por bloco, a soma igual as strings do desenho.
    - O Mostrar da linha, pelo MESMO caminho do botao (fora de comando): a
      selecao implicita tem os contornos das mesas do bloco e so eles.
    Usa Numeracao-Usina e Numeracao-Strings (eletrica-15-4.ps1).
#>

# Confere a ordem das tags com os dois sentidos. $Sentidos: bloco (0 = fora) -> @(avanca, faixa). Devolve @{ Erros; Pares (na mesma faixa) }.
function Numeracao-ConfereFaixa {
    param($Strings, [hashtable] $Prefixos, [hashtable] $Sentidos, [int] $Blocos)

    function Eixo([string] $sentido, $s) {
        switch ($sentido) {
            'LeftToRight' { return $s.X }
            'RightToLeft' { return -$s.X }
            'TopToBottom' { return -$s.Y }
            default       { return $s.Y }
        }
    }

    $erros = @()
    $naFaixa = 0
    foreach ($grupo in ($Strings | Where-Object { $_.Inversor -ne '-' } | Group-Object Inversor)) {
        $prefixo = $Prefixos[$grupo.Name]
        $itens = @()
        foreach ($s in $grupo.Group) {
            if (-not $s.Tag.StartsWith($prefixo) -or $s.Tag.Substring($prefixo.Length) -notmatch '^\d+$') { $erros += "$($grupo.Name): tag $($s.Tag) nao comeca com $prefixo"; continue }
            $itens += [pscustomobject]@{ N = [int]$s.Tag.Substring($prefixo.Length); S = $s }
        }

        $ordem = @($itens | Sort-Object N)
        if ((($ordem | ForEach-Object { $_.N }) -join ',') -ne ((1..$ordem.Count) -join ',')) { $erros += "$($grupo.Name): sequencial com buraco"; continue }

        for ($i = 1; $i -lt $ordem.Count; $i++) {
            $a = $ordem[$i - 1].S; $b = $ordem[$i].S
            $ra = if ($a.Bloco -eq 0) { $Blocos + 1 } else { $a.Bloco }
            $rb = if ($b.Bloco -eq 0) { $Blocos + 1 } else { $b.Bloco }
            if ($rb -lt $ra) { $erros += "$($grupo.Name): $($b.Tag) fura a ordem dos blocos"; continue }
            if ($rb -gt $ra) { continue }

            $sentido = $Sentidos[$a.Bloco][0]; $faixa = $Sentidos[$a.Bloco][1]
            $pa = Eixo $sentido $a; $pb = Eixo $sentido $b
            $sa = Eixo $faixa $a; $sb = Eixo $faixa $b

            if ([math]::Abs($pb - $pa) -le 0.5) { $naFaixa++ }
            $ok = ($pb -ge $pa - 0.5) -and (($pb -gt $pa + 0.5) -or ($sb -ge $sa - 0.001))
            if (-not $ok) { $erros += "$($grupo.Name): $($b.Tag) em ($($b.X), $($b.Y)) nao vem depois de $($a.Tag) em ($($a.X), $($a.Y)) ($sentido; na faixa $faixa)" }
        }
    }

    return @{ Erros = $erros; Pares = $naFaixa }
}

function Testar-NumeracaoFaixa {
    param([string] $Desenho)

    $u = Numeracao-Usina -Desenho $Desenho -Rotulo 'clivus-numeracao-faixa'
    if (-not $u) { $problemas.Add('clivus-numeracao-faixa: nao achei o centro ou as cotas do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-faixa' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-faixa.scr') -Substituicoes $u.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-faixa terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    # 1) O formato 1.
    $antigo = $t.Substring($t.IndexOf('CLIVUS_ANTIGO_LISTA'), $t.IndexOf('CLIVUS_ANTIGO_FIM') - $t.IndexOf('CLIVUS_ANTIGO_LISTA'))
    if ($antigo -notmatch 'NUMERACAO usina de cima para baixo; 1 bloco\(s\); na faixa, da esquerda para a direita') { $erros += 'o formato 1 nao foi lido com a usina de cima para baixo e, na faixa, da esquerda para a direita' }
    if ($antigo -notmatch 'NUMERACAO bloco 1\. Antigo \S+ 0 mesa\(s\), da direita para a esquerda; na faixa, de cima para baixo') { $erros += 'o bloco do formato 1 nao veio com o sentido na faixa de antes (de cima para baixo)' }
    if ($antigo -match 'ATEN') { $erros += 'o formato 1 foi lido com problema' }
    if ($t -notmatch 'CLIVUS_REGRAVADO formato=2 campos=9 faixa=LeftToRight fim') { $erros += 'a primeira mudanca nao regravou no formato 2 com o sentido na faixa' }

    if ($t -notmatch 'CLIVUS_NUMERACAO_MESAS total=(\d+) oeste=(\d+) leste=(\d+) strings=(\d+)') {
        $problemas.Add("clivus-numeracao-faixa: nao li as mesas. Veja $($r.Saida)")
        return $false
    }
    $oeste = [int]$Matches[2]; $leste = [int]$Matches[3]; $nstrings = [int]$Matches[4]
    if ($oeste -lt 2 -or $leste -lt 2) { $erros += "a usina nao tem mesas dos dois lados (oeste=$oeste leste=$leste)" }

    # 2) O Selecionar (o comando do botao).
    if ($t -notmatch "NUMERACAO Bloco 1: $oeste mesa\(s\)\.") { $erros += "o Selecionar do Bloco 1 nao gravou as $oeste mesas" }
    if ($t -notmatch "NUMERACAO Bloco 2: $leste mesa\(s\)\.") { $erros += "o Selecionar do Bloco 2 nao gravou as $leste mesas" }

    # 3) Os dois sentidos.
    $recusa = $t.Substring($t.IndexOf('CLIVUS_RECUSA'), $t.IndexOf('CLIVUS_RECUSA_FIM') - $t.IndexOf('CLIVUS_RECUSA'))
    if ($recusa -notmatch 'NUMERACAO Recusado: o sentido na faixa tem que ser perpendicular') { $erros += 'o sentido na faixa paralelo ao que avanca nao foi recusado' }

    $final = $t.Substring($t.IndexOf('CLIVUS_LISTA_FINAL'), $t.IndexOf('CLIVUS_LISTA_FINAL_FIM') - $t.IndexOf('CLIVUS_LISTA_FINAL'))
    if ($final -notmatch 'NUMERACAO usina de cima para baixo; 2 bloco\(s\); na faixa, da direita para a esquerda') { $erros += 'a usina nao ficou de cima para baixo e, na faixa, da direita para a esquerda' }
    if ($final -notmatch "NUMERACAO bloco 1\. Bloco 1 \S+ $oeste mesa\(s\), da esquerda para a direita; na faixa, de baixo para cima; $oeste mesa\(s\), (\d+) string\(s\)") { $erros += 'o Bloco 1 nao ficou da esquerda para a direita e, na faixa, de baixo para cima (ou sem o resumo)'; $s1 = -1 } else { $s1 = [int]$Matches[1] }
    if ($final -notmatch "NUMERACAO bloco 2\. Bloco 2 \S+ $leste mesa\(s\), de baixo para cima; na faixa, da direita para a esquerda; $leste mesa\(s\), (\d+) string\(s\)") { $erros += 'o Bloco 2 nao ficou de baixo para cima e, na faixa, da direita para a esquerda (ou sem o resumo)'; $s2 = -1 } else { $s2 = [int]$Matches[1] }

    # 4) O resumo.
    if ($s1 -gt 0 -and $s2 -gt 0 -and $s1 + $s2 -ne $nstrings) { $erros += "o resumo dos blocos soma $($s1 + $s2) string(s), o desenho tem $nstrings" }
    if ($s1 -eq 0 -or $s2 -eq 0) { $erros += "bloco sem string no resumo ($s1 e $s2)" }
    if ($final -notmatch "NUMERACAO resumo Em blocos: $($oeste + $leste) mesa\(s\), $nstrings string\(s\)\. Fora de bloco: 0 string\(s\)") { $erros += "o resumo geral nao diz $($oeste + $leste) mesa(s), $nstrings string(s) em blocos e 0 fora" }

    $gerar = $t.Substring($t.IndexOf('CLIVUS_FASE_GERAR'), $t.IndexOf('CLIVUS_LISTA_FINAL') - $t.IndexOf('CLIVUS_FASE_GERAR'))
    $strings = @(Numeracao-Strings $gerar)
    if (@($strings | Where-Object { $_.Tag -ne '-' }).Count -ne 14) { $erros += "$(@($strings | Where-Object { $_.Tag -ne '-' }).Count) string(s) com tag (esperava 14)" }
    $prefixos = @{ 'Inversor_1' = 'T1.I1.S'; 'Inversor_2' = 'T1.I2.S'; 'Inversor_3' = 'T2.I3.S'; 'Inversor_4' = 'I4.S' }
    $sentidos = @{ 0 = @('TopToBottom', 'RightToLeft'); 1 = @('LeftToRight', 'BottomToTop'); 2 = @('BottomToTop', 'RightToLeft') }
    $conferido = Numeracao-ConfereFaixa -Strings $strings -Prefixos $prefixos -Sentidos $sentidos -Blocos 2
    $erros += @($conferido.Erros)
    if ($conferido.Pares -lt 1) { $erros += 'nenhum par de strings seguidas na mesma faixa: o sentido na faixa nao foi posto a prova' }

    # 5) O Mostrar pelo caminho do botao.
    if ($t -notmatch "NUMERACAO mostradas pela janela $oeste de Bloco 1") { $erros += "o Mostrar do Bloco 1 nao achou as $oeste mesas" }
    if ($t -notmatch "CLIVUS_MOSTRAR 1 n=$oeste mesas=$oeste fim") { $erros += "a selecao implicita do Mostrar do Bloco 1 nao tem so as $oeste mesas dele" }
    if ($t -notmatch "CLIVUS_MOSTRAR 2 n=$leste mesas=$leste fim") { $erros += "a selecao implicita do Mostrar do Bloco 2 nao tem so as $leste mesas dele" }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-faixa: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numeracao: formato 1 lido e regravado no 2; Selecionar pelo comando; dois sentidos por bloco, $($conferido.Pares) par(es) na mesma faixa conferidos; resumo $s1 + $s2 = $nstrings strings; Mostrar seleciona $oeste e $leste mesas)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoFaixa'
