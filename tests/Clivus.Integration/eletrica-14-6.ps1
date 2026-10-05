<#
    14.6: o retangulo do inversor em campo. Dois inversores com o Z do
    clique absurdo (9999): a cota de cada um, lida da entidade, e a do
    terreno + 0,80 m. O Inversor 1 e posto de novo (move o mesmo) e
    renomeado para INV-A: a tag no topo acompanha.
    Usa Pontos-Do-Terreno e Conferir-Equipamentos de eletrica-12-3.ps1.
#>
function Testar-Eletrica146 {
    param([string] $Desenho)

    $pontos = Pontos-Do-Terreno -Desenho $Desenho -Rotulo 'clivus-eletrica-14-6' -Deslocamentos @{ P1 = @(-12, 8); P2 = @(10, 25); P3 = @(25, -10) }
    if (-not $pontos) { $problemas.Add('clivus-eletrica-14-6: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-6' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-6.scr') -Substituicoes $pontos.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-6 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    # Relatorios na ordem: Inversor 1 em P1, Inversor 2 em P2, Inversor 1 em P3 (movido).
    $erros = @(Conferir-Equipamentos -Texto $t -Esperados @(
        @{ Tipo = 'Inverter'; XY = $pontos.XY.P3; Relatorio = 2 },
        @{ Tipo = 'Inverter'; XY = $pontos.XY.P2; Relatorio = 1 }))

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $x3 = [string]::Format($inv, '{0:0.000}', $pontos.XY.P3[0])
    $x2 = [string]::Format($inv, '{0:0.000}', $pontos.XY.P2[0])
    if ($t -notmatch "CLIVUS_EQUIP tipo=Inverter x=$([regex]::Escape($x3)) .* fim tag=INV-A /tag") { $erros += 'a tag do Inversor 1 em campo nao virou INV-A' }
    if ($t -notmatch "CLIVUS_EQUIP tipo=Inverter x=$([regex]::Escape($x2)) .* fim tag=Inversor 2 /tag") { $erros += 'a tag do Inversor 2 em campo nao e Inversor 2' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-6: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (inversor em campo: cota do terreno + 0,80 lida da entidade, mover, tag acompanha o nome)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica146'
