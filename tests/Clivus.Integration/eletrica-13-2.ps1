<#
    13.2: o retangulo do trafo em campo. Dois trafos com o Z do clique
    absurdo (9999): a cota de cada um, lida da entidade, e a do terreno +
    0,80 m. O T1 e posto de novo (move o mesmo) e ganha o apelido TR-A: a
    tag escrita no topo do bloco acompanha; o vinculo com a UC1 fica.
    Usa Pontos-Do-Terreno e Conferir-Equipamentos de eletrica-12-3.ps1.
#>
function Testar-Eletrica132 {
    param([string] $Desenho)

    $pontos = Pontos-Do-Terreno -Desenho $Desenho -Rotulo 'clivus-eletrica-13-2' -Deslocamentos @{ P1 = @(-25, 10); P2 = @(20, 20); P3 = @(5, -25) }
    if (-not $pontos) { $problemas.Add('clivus-eletrica-13-2: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-13-2' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-13-2.scr') -Substituicoes $pontos.Sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-13-2 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    # Relatorios na ordem: T1 em P1, T2 em P2, T1 em P3 (movido).
    $erros = @(Conferir-Equipamentos -Texto $t -Esperados @(
        @{ Tipo = 'Transformer'; XY = $pontos.XY.P3; Relatorio = 2 },
        @{ Tipo = 'Transformer'; XY = $pontos.XY.P2; Relatorio = 1 }))

    $inv = [Globalization.CultureInfo]::InvariantCulture
    $x3 = [string]::Format($inv, '{0:0.000}', $pontos.XY.P3[0])
    $x2 = [string]::Format($inv, '{0:0.000}', $pontos.XY.P2[0])
    if ($t -notmatch "CLIVUS_EQUIP tipo=Transformer x=$([regex]::Escape($x3)) .* fim tag=TR-A /tag") { $erros += 'a tag do T1 em campo nao virou TR-A' }
    if ($t -notmatch "CLIVUS_EQUIP tipo=Transformer x=$([regex]::Escape($x2)) .* fim tag=T2 /tag") { $erros += 'a tag do T2 em campo nao e T2' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 1 subestacao(oes)'))
    if ($final -notmatch 'ELETRICA UC UC1 .* trafos=TR-A\s') { $erros += 'o vinculo UC1-T1 mudou' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-13-2: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (trafo em campo: cota do terreno + 0,80 lida da entidade, mover, tag acompanha o apelido, vinculo intacto)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica132'
