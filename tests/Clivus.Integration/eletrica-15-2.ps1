<#
    15.2: sentido da usina e blocos com mesas e sentido proprios, gravados no
    desenho. A selecao de campo so aceita mesa (as strings na selecao sao
    ignoradas) e a mesa que estava em outro bloco passa para o novo.
#>
function Testar-NumeracaoBlocos {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-numeracao-blocos'
    if (-not $sub) { $problemas.Add('clivus-numeracao-blocos: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numeracao-blocos' -Script (Join-Path $PSScriptRoot 'clivus-numeracao-blocos.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_NUMERACAO_FIM') -lt 0) {
        $problemas.Add("clivus-numeracao-blocos terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    if ($t -notmatch 'CLIVUS_NUMERACAO_MESAS total=(\d+) oeste=(\d+) leste=(\d+)') {
        $problemas.Add("clivus-numeracao-blocos: nao li as mesas. Veja $($r.Saida)")
        return $false
    }

    $total = [int]$Matches[1]; $oeste = [int]$Matches[2]; $leste = [int]$Matches[3]
    $final = $t.Substring($t.IndexOf('CLIVUS_NUMERACAO_LISTA_FINAL'))
    $erros = @()

    if ($oeste -lt 2 -or $leste -lt 1) { $erros += "a usina nao tem mesas dos dois lados (oeste=$oeste leste=$leste)" }
    if ($t -notmatch "NUMERACAO Bloco 2: $($leste + 1) mesa\(s\); 1 sa.ram de outro bloco") { $erros += "o Bloco 2 nao ficou com $($leste + 1) mesa(s) com 1 vinda do Bloco 1 (strings na selecao contaram?)" }
    if ($final -notmatch 'NUMERACAO usina de cima para baixo; 2 bloco\(s\)') { $erros += 'o sentido da usina ou a quantidade de blocos nao e a esperada' }
    if ($final -notmatch "NUMERACAO bloco 1\. Bloco 1 \S+ $($oeste - 1) mesa\(s\), de baixo para cima") { $erros += "o Bloco 1 nao ficou com $($oeste - 1) mesa(s) de baixo para cima" }
    if ($final -notmatch "NUMERACAO bloco 2\. Leste \S+ $($leste + 1) mesa\(s\), da direita para a esquerda") { $erros += "o Leste nao ficou com $($leste + 1) mesa(s) da direita para a esquerda" }
    if ($t -notmatch 'NUMERACAO N.o renomeei: j. existe um bloco chamado "bloco 1"') { $erros += 'renomear para um nome que ja existe nao foi recusado' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-numeracao-blocos ($total mesas): $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numeracao: usina de cima para baixo; Bloco 1 com $($oeste - 1) mesa(s) de baixo para cima, Leste com $($leste + 1) da direita para a esquerda)" -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-NumeracaoBlocos'
