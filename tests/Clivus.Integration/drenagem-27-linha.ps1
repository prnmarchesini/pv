<#
    Passo 27 (Analise > Linhas): a linha desenhada pelo comando do botao fica
    com cada vertice na cota do TIN (lido em LISP contra a faixa do Civil 3D e
    pelo conferir do plugin) e ganha o grafico; movida, volta ao terreno e o
    grafico muda; copiada, a copia ganha GUID, nome e grafico proprios;
    apagada, o grafico vai junto; uma polilinha escolhida vira linha (a
    original sai); Atualizar refaz tudo sem deixar grafico orfao.
#>
function Testar-DrenagemLinha {
    param([string] $Desenho)

    $rotulo = 'clivus-drn-linha'
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

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-drn-linha.scr') -Substituicoes @{
        '{{P1}}' = (P -30 -30); '{{P2}}' = (P 0 0); '{{P3}}' = (P 20 10); '{{G}}' = (P 150 -80)
        '{{Q1}}' = (P -30 30); '{{Q2}}' = (P 20 35)
        '{{ZMIN}}' = ($zmin - 0.001).ToString('0.###', $inv); '{{ZMAX}}' = ($zmax + 0.001).ToString('0.###', $inv)
    }

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_DRNLINHA FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    function Etapa([int] $n) {
        $a = $t.IndexOf("CLIVUS_DRNLINHA ETAPA$n")
        if ($a -lt 0) { return '' }
        $b = $t.IndexOf('CLIVUS_DRNLINHA ETAPA' + ($n + 1), $a + 10)
        if ($b -lt 0) { $b = $t.IndexOf('CLIVUS_DRNLINHA FIM', $a) }
        return $t.Substring($a, $b - $a)
    }

    function Linhas([string] $trecho) {
        $lista = @()
        foreach ($m in [regex]::Matches($trecho, 'DRN_LINHA nome=(\S+) guid=(\w+) canaleta=(\d) vertices=(\d+) ok=(\d+) fora=(\d+) sem_terreno=(\d+) perfil=(\d+) titulo=\[([^\]]*)\] fim')) {
            $lista += [pscustomobject]@{
                Nome = $m.Groups[1].Value; Guid = $m.Groups[2].Value; Vertices = [int] $m.Groups[4].Value
                Ok = [int] $m.Groups[5].Value; Fora = [int] $m.Groups[6].Value; Sem = [int] $m.Groups[7].Value
                Perfil = [int] $m.Groups[8].Value; Titulo = $m.Groups[9].Value
            }
        }
        return , $lista
    }

    function Orfaos([string] $trecho) { if ($trecho -match 'DRN_LINHAS total=(\d+) perfis_orfaos=(\d+) fim') { return [int] $Matches[2] } return -1 }

    function NoTerreno($lista, [string] $quando) {
        foreach ($l in $lista) {
            if ($l.Ok -ne $l.Vertices -or $l.Fora -ne 0 -or $l.Sem -ne 0) { $script:erros += "${quando}: $($l.Nome) com $($l.Fora) vertice(s) fora da cota do TIN e $($l.Sem) sem terreno (de $($l.Vertices))" }
            if ($l.Perfil -lt 20) { $script:erros += "${quando}: o grafico de $($l.Nome) tem so $($l.Perfil) entidade(s)" }
        }
    }

    $script:erros = @()

    $e1 = Linhas (Etapa 1)
    if ($e1.Count -ne 1) { $script:erros += "depois de desenhar, $($e1.Count) linha(s) (esperado 1)" } else { NoTerreno $e1 'desenhada' }
    if ((Etapa 1) -notmatch 'fora1=0') { $script:erros += 'em LISP, vertice da linha fora da faixa de cotas do terreno' }
    if ((Etapa 1) -notmatch 'LINHA Linha 1: [\d.,]+ m') { $script:erros += 'o comando nao relatou a Linha 1' }

    $e2 = Linhas (Etapa 2)
    if ($e2.Count -ne 1) { $script:erros += "depois de mover, $($e2.Count) linha(s)" }
    else {
        NoTerreno $e2 'movida'
        if ($e1.Count -eq 1 -and $e2[0].Titulo -eq $e1[0].Titulo) { $script:erros += 'movida 10 m, o grafico nao mudou (titulo igual)' }
        if ($e1.Count -eq 1 -and $e2[0].Guid -ne $e1[0].Guid) { $script:erros += 'movida, a linha trocou de GUID' }
    }
    if ((Etapa 2) -notmatch 'fora2=0') { $script:erros += 'em LISP, depois de mover, vertice fora da faixa de cotas' }
    if ((Etapa 2) -notmatch 'LINHA 1 linha\(s\) mexida\(s\)') { $script:erros += 'o vigia nao relatou a linha mexida' }

    $e3 = Linhas (Etapa 3)
    if ($e3.Count -ne 2) { $script:erros += "depois de copiar, $($e3.Count) linha(s) (esperado 2)" }
    else {
        NoTerreno $e3 'copiada'
        if ($e3[0].Guid -eq $e3[1].Guid) { $script:erros += 'a copia ficou com o mesmo GUID da original' }
        if (($e3 | ForEach-Object Nome | Sort-Object) -join ',' -ne 'Linha_1,Linha_2') { $script:erros += "nomes depois de copiar: $(($e3 | ForEach-Object Nome) -join ',')" }
    }
    if ((Orfaos (Etapa 3)) -ne 0) { $script:erros += 'grafico orfao depois de copiar' }

    $e4 = Linhas (Etapa 4)
    if ($e4.Count -ne 1) { $script:erros += "depois de apagar a copia, $($e4.Count) linha(s)" }
    if ((Orfaos (Etapa 4)) -ne 0) { $script:erros += 'apagada a copia, o grafico dela ficou (orfao)' }

    $e5 = Linhas (Etapa 5)
    if ($e5.Count -ne 2) { $script:erros += "depois do Selecionar, $($e5.Count) linha(s) (esperado 2)" } else { NoTerreno $e5 'selecionada' }
    if ((Etapa 5) -notmatch 'original_existe=0') { $script:erros += 'a polilinha escolhida continuou no desenho' }
    if ((Orfaos (Etapa 5)) -ne 0) { $script:erros += 'grafico orfao depois do Atualizar' }
    if ((Etapa 5) -notmatch 'LINHA 2 gr.fico\(s\) refeito\(s\)') { $script:erros += 'o Atualizar nao refez os 2 graficos' }

    if ($script:erros.Count -gt 0) {
        $problemas.Add("${rotulo}: " + ($script:erros -join '; ') + ". Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (drenagem linha: desenhada, movida, copiada, apagada e escolhida, sempre no TIN, grafico acompanhando, sem orfao)" -ForegroundColor DarkGray
    return $true
}

$script:CasosDrenagem += 'Testar-DrenagemLinha'
