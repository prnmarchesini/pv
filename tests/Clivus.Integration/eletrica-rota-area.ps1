<#
    Vala que entra na area dos inversores (segunda rodada de tela do Renan de
    10/10/2026, item 6, plano/melhorias-2026-10-10.md): "sempre que chegar uma
    vala DENTRO da area ou do inversor, a vala que chega tem prioridade, mesmo
    que o raio de 10 m ao lado de outra vala tenha um trajeto menor".
    O cenario do desenho de Itatiba: a area ao sul da usina, com o Inversor 1
    posto nela pelo Local dos inversores e o Inversor 2 posto a mao dentro dela
    (sem o registro da area, como os inversores 9 e 10 do Renan). Uma vala entra
    na area pelo sul; outra para a 6 m ao norte dela (8 m do Inversor 2, no raio,
    e por ela o caminho e menor). Depois do Gerar CC (o comando do botao), todos
    os 24 lances chegam aos dois inversores pela vala de dentro da area.
#>
function Testar-EletricaRotaArea {
    param([string] $Desenho)

    $rotulo = 'clivus-rota-area'
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$rotulo--sonda" -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')
    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') { $problemas.Add("${rotulo}: nao achei o centro do terreno."); return $false }

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $cx = [double]::Parse($Matches[1], $inv)
    $cy = [double]::Parse($Matches[2], $inv)
    function P([double] $dx, [double] $dy) { [string]::Format($inv, '{0:0.###},{1:0.###}', $cx + $dx, $cy + $dy) }

    # A area: 20 x 14 m ao sul da usina (x = 40 a 60, y = -64 a -50). O Inversor 2 no canto de cima.
    # Vala do leste x = 35 (de -80 a 35); a do sul entra na area ate y = -58; a do norte para em y = -44.
    $sub = @{
        '{{A1}}' = (P -30 -30); '{{A2}}' = (P 30 -30); '{{A3}}' = (P 30 30); '{{A4}}' = (P -30 30)
        '{{L1}}' = (P -30 -30); '{{L2}}' = (P -30 30); '{{LADO}}' = (P 0 0)
        '{{S1}}' = (P 40 -64); '{{S2}}' = (P 60 -64); '{{S3}}' = (P 60 -50); '{{S4}}' = (P 40 -50)
        '{{I2}}' = (P 57 -52)
        '{{V1}}' = (P 35 -80); '{{V2}}' = (P 35 35)
        '{{SUL1}}' = (P 45 -80); '{{SUL2}}' = (P 45 -58)
        '{{N1}}' = (P 35 -44); '{{N2}}' = (P 58 -44)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $rotulo -Script (Join-Path $PSScriptRoot 'clivus-rota-area.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ROTAAREA_FIM') -lt 0) {
        $problemas.Add("$rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()

    $i = $t.IndexOf('CLIVUS_ROTAAREA CONFERIR')
    $conf = if ($i -ge 0) { $t.Substring($i) } else { '' }

    if ($t -notmatch 'ROTA CC: 24 lance') { $erros += 'o Gerar CC nao desenhou os 24 lances' }

    foreach ($n in 1, 2) {
        if ($conf -notmatch "ROTA_ACESSO inversor=Inversor_$n contorno=(\S+) local=(\S+) na_area=(\S+) entram=(\d+) raio=(\d+) chegam_de_dentro=(\d+) chegam_de_fora=(\d+) fim") {
            $erros += "nao achei o acesso do Inversor $n na conferencia"
            continue
        }

        $contorno = $Matches[1]; $local = $Matches[2]; $area = $Matches[3]; $raio = [int]$Matches[5]; $dentro = [int]$Matches[6]; $fora = [int]$Matches[7]
        if ($area -ne 'Sala_rota') { $erros += "o Inversor $n nao esta dentro da area em planta (na_area=$area)" }
        if ($contorno -ne 'area') { $erros += "o motor usou a $contorno do Inversor $n, nao a area onde ele esta (local=$local)" }
        # No raio de 10 m do Inversor 2 so passa a vala do norte (a que entra na area para a 13 m dele).
        if ($n -eq 2 -and $raio -lt 1) { $erros += 'a vala do norte nao ficou no raio do Inversor 2 (o caso nao prova a regra)' }
        if ($dentro -ne 12 -or $fora -ne 0) { $erros += "dos 12 lances do Inversor $n, $dentro chegam pela vala de dentro da area e $fora por fora dela" }
    }

    if ($erros.Count -gt 0) {
        $problemas.Add("${rotulo}: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (vala que entra na area: os 24 lances dos dois inversores, um deles sem o registro da area, chegam pela vala de dentro dela)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaRotaArea'
