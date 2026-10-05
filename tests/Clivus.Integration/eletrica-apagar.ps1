<#
    O Apagar da Edicao (05/10/2026): CLIVUS_APAGAR com as cinco opcoes, sobre
    um desenho com tudo que o plugin pinta e escreve (veja clivus-apagar.scr).
    Tudo lido do desenho pelo LISP (XData, cor e dicionario) em cada etapa:

      - "12345" leva tudo de uma vez e um U devolve o desenho exatamente como
        estava (uma transacao, um passo do desfazer);
      - 3 (sombras): os contornos e etiquetas somem, os registros zeram e os
        modulos marcados voltam a cor de antes (a da analise, vermelha);
      - 1 (cores): nenhuma peca fica com cor de analise, sombra ou pendencia;
        cada modulo com a cor do contorno da mesa dele, pilar de mesa que cabe
        ByLayer, strings e sinais ByLayer, textos de analise ByLayer;
      - 5 (infra): equipamentos em campo e as definicoes somem; inversores,
        trafos e UCs zeram; os modelos ficam; as strings ficam soltas e sem
        tag, e as tags escritas somem;
      - 2 (textos): textos de analise, tags e sinais somem; as strings ficam;
      - 4 (strings): as strings somem; os tipos de string ficam.
    Em toda etapa: mesas, modulos, pilares, faces, arvore, area e alinhamento
    nao mudam, e as quatro entidades do usuario (duas em camadas CLIVUS_*)
    continuam la com a cor delas.
#>
function Testar-Apagar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-apagar--sonda' -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { $problemas.Add('clivus-apagar: nao achei o centro do terreno.'); return $false }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy, [double] $z = 0) { [string]::Format($inv, '{0:0.###},{1:0.###},{2:0.###}', $cx + $dx, $cy + $dy, $z) }

    $sub = @{
        '{{A1}}' = (P -45 -50); '{{A2}}' = (P 45 -50); '{{A3}}' = (P 45 50); '{{A4}}' = (P -45 50)
        '{{L1}}' = (P -45 -50); '{{L2}}' = (P -45 50); '{{LADO}}' = (P 0 0)
        '{{P1}}' = (P -20 -20 9999); '{{P2}}' = (P 15 -20 9999); '{{P3}}' = (P -20 15 9999)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-apagar' -Script (Join-Path $PSScriptRoot 'clivus-apagar.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_APAGAR_FIM') -lt 0) {
        $problemas.Add("clivus-apagar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $etapas = @{}
    foreach ($m in [regex]::Matches($t, 'CLIVUS_APAGAR_DUMP etapa=(\w+)((?: \w+=\S+)+) fim')) {
        $valores = @{}
        foreach ($par in $m.Groups[2].Value.Trim().Split(' ')) { $k, $v = $par.Split('=', 2); $valores[$k] = $v }
        $etapas[$m.Groups[1].Value] = $valores
    }

    $erros = @()
    foreach ($e in 'inicio', 'tudo', 'desfeito', 'sombras', 'cores', 'infra', 'textos', 'strings') {
        if (-not $etapas.ContainsKey($e)) { $erros += "faltou a contagem da etapa $e" }
    }
    if ($erros.Count -gt 0) { $problemas.Add("clivus-apagar: $($erros -join '; '). Veja $($r.Saida)"); return $false }

    $i = $etapas['inicio']
    function N($etapa, $k) { [int]$etapas[$etapa][$k] }
    function Igual($etapa, [string[]] $chaves) {
        foreach ($k in $chaves) { if ($etapas[$etapa][$k] -ne $i[$k]) { $script:errosApagar += "${etapa}: $k mudou ($($i[$k]) -> $($etapas[$etapa][$k]))" } }
    }
    $script:errosApagar = @()

    # O desenho de partida tem de tudo.
    foreach ($k in 'String', 'SinalString', 'StringTag', 'TextoAnalise', 'Tag', 'Sombra', 'alocadas', 'comtag', 'strpintadas', 'sinpintados',
                   'anpintados', 'modsombra', 'modvermelho', 'contvermelho', 'desiguais', 'Arvore', 'AreaDoTrafo') {
        if ((N 'inicio' $k) -le 0) { $erros += "o desenho de partida nao tem $k" }
    }
    if ((N 'inicio' 'Equipamento') -ne 3 -or (N 'inicio' 'defs') -ne 3) { $erros += "esperava 3 equipamentos em campo (tem $($i['Equipamento']), $($i['defs']) definicoes)" }
    if ($i['inv'] -ne '4' -or $i['trafos'] -ne '2' -or $i['ucs'] -ne '1' -or $i['modelos'] -ne '1') { $erros += "o cadastro de partida nao e 4 inversores, 2 trafos, 1 UC, 1 modelo" }
    if ((N 'inicio' 'SinalString') -ne 4 * (N 'inicio' 'String')) { $erros += 'cada string devia ter 2 sinais e 2 circulos' }
    if ((N 'inicio' 'usuario') -ne 4) { $erros += 'as 4 entidades do usuario nao foram criadas' }

    # O que nunca muda, em nenhuma etapa: as pecas das mesas e o resto do desenho.
    $fixas = 'Mesa', 'Modulo', 'Pilar', 'Face', 'Arvore', 'Area', 'Alinhamento', 'modelos', 'tipos', 'usuario'
    foreach ($e in 'tudo', 'desfeito', 'sombras', 'cores', 'infra', 'textos', 'strings') { Igual $e $fixas }

    # 12345 de uma vez.
    foreach ($k in 'Nota', 'TextoAnalise', 'Tag', 'StringTag', 'SinalString', 'String', 'Sombra', 'Equipamento', 'AreaDoTrafo', 'defs', 'contvermelho',
                   'modsombra', 'modvermelho', 'desiguais', 'pilpintados', 'anpintados', 'foracamada', 'perfildiverso') {
        if ((N 'tudo' $k) -ne 0) { $erros += "12345 deixou $k=$($etapas['tudo'][$k])" }
    }
    if ($etapas['tudo']['inv'] -ne '0' -or $etapas['tudo']['trafos'] -ne '0' -or $etapas['tudo']['ucs'] -ne '0') { $erros += '12345 nao zerou o cadastro da infra' }

    # Um U devolve tudo.
    foreach ($k in $i.Keys) { if ($etapas['desfeito'][$k] -ne $i[$k]) { $erros += "o U nao devolveu $k ($($i[$k]) -> $($etapas['desfeito'][$k]))" } }

    # 3: sombras.
    $s = 'sombras'
    if ((N $s 'Sombra') -ne 0 -or (N $s 'modsombra') -ne 0) { $erros += 'as sombras ficaram (contorno ou modulo marcado)' }
    if ((N $s 'modvermelho') -ne (N $s 'Modulo')) { $erros += "os modulos nao voltaram a cor de antes da sombra (vermelhos: $($etapas[$s]['modvermelho']) de $($etapas[$s]['Modulo']))" }
    if ((N $s 'sombrapint') -ne 1 -or (N $s 'motivos') -ne 1) { $erros += 'os registros da sombra nao zeraram' }
    Igual $s @('Nota', 'TextoAnalise', 'Tag', 'StringTag', 'SinalString', 'String', 'Equipamento', 'AreaDoTrafo', 'defs', 'alocadas', 'comtag', 'strpintadas', 'inv', 'anpint')

    # 1: cores.
    $c = 'cores'
    foreach ($k in 'modvermelho', 'modsombra', 'contvermelho', 'desiguais', 'pilpintados', 'strpintadas', 'sinpintados', 'anpintados', 'foracamada', 'perfildiverso') {
        if ((N $c $k) -ne 0) { $erros += "as cores deixaram $k=$($etapas[$c][$k])" }
    }
    if ((N $c 'anpint') -ne 1) { $erros += 'o registro das pecas pintadas da analise nao zerou' }
    Igual $c @('Nota', 'TextoAnalise', 'Tag', 'StringTag', 'SinalString', 'String', 'Equipamento', 'AreaDoTrafo', 'defs', 'alocadas', 'comtag', 'inv', 'trafos')

    # 5: infra.
    $f = 'infra'
    foreach ($k in 'Equipamento', 'AreaDoTrafo', 'defs', 'alocadas', 'comtag', 'StringTag') { if ((N $f $k) -ne 0) { $erros += "a infra deixou $k=$($etapas[$f][$k])" } }
    if ($etapas[$f]['inv'] -ne '0' -or $etapas[$f]['trafos'] -ne '0' -or $etapas[$f]['ucs'] -ne '0') { $erros += 'a infra nao zerou inversores, trafos e UCs' }
    Igual $f @('String', 'SinalString', 'TextoAnalise', 'Tag', 'Nota')

    # 2: textos.
    $x = 'textos'
    foreach ($k in 'Nota', 'TextoAnalise', 'Tag', 'SinalString', 'StringTag', 'Sombra') { if ((N $x $k) -ne 0) { $erros += "os textos deixaram $k=$($etapas[$x][$k])" } }
    Igual $x @('String')

    # 4: strings.
    if ((N 'strings' 'String') -ne 0) { $erros += 'as strings ficaram' }
    if ($etapas['strings']['tipos'] -ne '1') { $erros += 'os tipos de string sumiram' }

    if ($t -notmatch 'APAGAR plano Cores: \d+ pe') { $erros += 'a linha do plano nao saiu' }
    if ($t -notmatch 'APAGAR N.o entendi "9"') { $erros += 'a opcao 9 nao foi recusada' }

    $erros += $script:errosApagar

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-apagar: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host ("  (apagar: {0} strings, {1} textos de analise, {2} entidades de sombra, {3} em campo; U devolve; usuario intacto)" -f `
        $i['String'], $i['TextoAnalise'], $i['Sombra'], $i['Equipamento']) -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Apagar'
