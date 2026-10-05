<#
    A tabela de inversores da aba Inversor (pedidos do Renan em 05/10/2026):
    - a potencia do modelo: o formato 2 (sem potencia) e lido com 0 e
      regravado no 3; "abc" e "-5" recusados, "1.500" = 1500 kW, "62,5" =
      62,5 kW, por fim 250 kW, pela mesma leitura e gravacao do Salvar modelo;
    - o trafo de cada inversor pela tabela (o mesmo caminho da janela, fora de
      comando): Inversor 1 e 2 estavam no Skid Norte (T1) e vao, com o 3, para
      o T2 de uma vez (sem trava, e o skid do T1 que ficou vazio some); o 3
      volta ao T1; o 4 fica sem trafo; trafo que nao existe e recusado;
    - as colunas: strings 5/20 e 3/20, kW 250, o kWp de cada inversor igual
      ao do resumo eletrico, CC/CA = kWp / kW, e o total.
    Usa Substituicoes-Da-Usina.
#>
function Testar-EletricaInversorTabela {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-tabela'
    if (-not $sub) { $problemas.Add('clivus-eletrica-inversor-tabela: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-tabela' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-inversor-tabela.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-inversor-tabela terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $erros = @()

    # A potencia do modelo e o formato.
    $antes = $t.Substring(0, [Math]::Max(0, $t.IndexOf('CLIVUS_TABELA_ANTES_DE_REGRAVAR')))
    if ($antes -notmatch 'ELETRICA 1 modelo\(s\) formato_modelos=2') { $erros += 'o formato 2 nao ficou gravado' }
    if ($antes -notmatch 'ELETRICA MODELO nome="Velho 2x2" mppt=2 entradas=2 total=4 tamanho=\S+ potencia=0') { $erros += 'o formato 2 nao foi lido com a potencia 0' }
    if ($t -notmatch 'ELETRICA janela modelo recusado: potencia \[abc\]') { $erros += '"abc" nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela modelo recusado: potencia \[-5\]') { $erros += '"-5" nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela modelo Huawei 250 salvo com 1500 kW') { $erros += '"1.500" nao virou 1500 kW' }
    if ($t -notmatch 'ELETRICA janela modelo Huawei 250 salvo com 62.5 kW') { $erros += '"62,5" nao virou 62,5 kW' }
    if ($t -notmatch 'ELETRICA janela modelo Huawei 250 salvo com 250 kW') { $erros += '"250" nao foi salvo' }

    # O skid antes da tabela.
    $skid = $t.Substring(0, [Math]::Max(0, $t.IndexOf('CLIVUS_TABELA_SKID_FEITO')))
    $skid = $skid.Substring([Math]::Max(0, $skid.LastIndexOf('ELETRICA 4 inversor(es)')))
    if ($skid -notmatch 'ELETRICA SKID nome="Skid Norte" trafo=T1 inversores=Inversor 1,Inversor 2 fim') { $erros += 'o Skid Norte nao ficou com os inversores 1 e 2' }

    # O trafo pela tabela.
    if ($t -notmatch 'ELETRICA janela trafo: 3 inversor\(es\) postos no T2; 0 j\S+ eram dele\.') { $erros += 'os inversores 1, 2 e 3 nao foram juntos para o T2' }
    if ($t -notmatch 'ELETRICA janela trafo: 1 inversor\(es\) postos no T1; 0 j\S+ eram dele\.') { $erros += 'o Inversor 3 nao voltou ao T1' }
    if ($t -notmatch 'ELETRICA janela trafo: 0 inversor\(es\) ficaram sem trafo\.') { $erros += 'o Inversor 4 (ja sem trafo) nao deu 0' }
    if ($t -notmatch 'ELETRICA janela trafo recusado: trafo T9 nao existe') { $erros += 'trafo que nao existe nao foi recusado' }

    $marca = $t.IndexOf('CLIVUS_TABELA_LINHAS')
    $final = $t.Substring(0, [Math]::Max(0, $marca))
    $final = $final.Substring([Math]::Max(0, $final.LastIndexOf('ELETRICA 2 modelo(s)')))
    if ($final -notmatch 'formato_modelos=3') { $erros += 'a gravacao nao passou ao formato 3' }
    if ($final -notmatch 'ELETRICA MODELO nome="Velho 2x2" mppt=2 entradas=2 total=4 tamanho=\S+ potencia=0') { $erros += 'o modelo do formato 2 mudou ao regravar' }
    if ($final -notmatch 'ELETRICA MODELO nome="Huawei 250" mppt=5 entradas=4 total=20 tamanho=\S+ potencia=250') { $erros += 'o Huawei 250 nao ficou com 250 kW' }
    foreach ($par in @(@('Inversor 1', 'T2'), @('Inversor 2', 'T2'), @('Inversor 3', 'T1'), @('Inversor 4', ''))) {
        if ($final -notmatch "ELETRICA INVERSOR nome=`"$($par[0])`" .* trafo=$($par[1]) excesso=") { $erros += "o $($par[0]) nao ficou com o trafo [$($par[1])]" }
    }
    if ($final -notmatch 'ELETRICA 0 skid\(s\)') { $erros += 'o skid do T1, sem os inversores dele, nao sumiu' }

    # As colunas contra o resumo eletrico.
    $resumo = @{}
    foreach ($l in [regex]::Matches($t, 'RESUMO_INVERSOR nome=(\S+) trafo=\S+ uc=\S+ strings=(\d+) capacidade=\d+ excesso=\d modulos=\d+ kwp=([\d.]+)')) {
        $resumo[$l.Groups[1].Value] = [double]::Parse($l.Groups[3].Value, $inv)
    }

    $linhas = @{}
    foreach ($l in [regex]::Matches($t, 'ELETRICA TABELA nome=(\S+) trafo=(\S+) strings=(\d+) entradas=(\d+) kwp=(\S+) kw=(\S+) ccca=(\S+) fim')) {
        $linhas[$l.Groups[1].Value] = $l
    }

    $esperado = @{ 'Inversor_1' = @('T2', '5'); 'Inversor_2' = @('T2', '3'); 'Inversor_3' = @('T1', '0'); 'Inversor_4' = @('-', '0') }
    $somaKwp = 0.0
    foreach ($k in $esperado.Keys) {
        $l = $linhas[$k]
        if (-not $l) { $erros += "a tabela nao tem o $k"; continue }
        $trafo = $l.Groups[2].Value; $strings = $l.Groups[3].Value; $entradas = $l.Groups[4].Value
        $kwp = [double]::Parse($l.Groups[5].Value, $inv); $kw = [double]::Parse($l.Groups[6].Value, $inv)
        $somaKwp += $kwp
        if ($trafo -ne $esperado[$k][0] -or $strings -ne $esperado[$k][1] -or $entradas -ne '20' -or $kw -ne 250) { $erros += "${k}: trafo=$trafo strings=$strings entradas=$entradas kw=$kw, esperava $($esperado[$k][0]) $($esperado[$k][1]) 20 250" }
        if (-not $resumo.ContainsKey($k) -or [math]::Abs($resumo[$k] - $kwp) -gt 0.001) { $erros += "${k}: $kwp kWp na tabela, $($resumo[$k]) no resumo eletrico" }
        if ($l.Groups[7].Value -eq '-' -or [math]::Abs([double]::Parse($l.Groups[7].Value, $inv) - $kwp / 250) -gt 0.001) { $erros += "${k}: CC/CA $($l.Groups[7].Value), esperava $($kwp / 250)" }
    }
    if ($linhas['Inversor_1'] -and [double]::Parse($linhas['Inversor_1'].Groups[5].Value, $inv) -le 0) { $erros += 'o Inversor 1, com 5 strings, ficou com 0 kWp' }

    if ($t -notmatch 'ELETRICA TABELA_TOTAL inversores=4 strings=8 entradas=80 kwp=([\d.]+) kw=1000 ccca=([\d.]+) fim') { $erros += 'o total da tabela nao e 4 inversores, 8 strings de 80 entradas, 1000 kW' }
    elseif ([math]::Abs([double]::Parse($Matches[1], $inv) - $somaKwp) -gt 0.002) { $erros += "o total de kWp $($Matches[1]) nao e a soma das linhas ($somaKwp)" }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-inversor-tabela: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (tabela de inversores: potencia do modelo, trafo na linha e em lote, kWp = resumo, CC/CA, total)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaInversorTabela'
