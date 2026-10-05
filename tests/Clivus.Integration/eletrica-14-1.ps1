<#
    14.1: modelos de inversor. Um generico (1 MPPT x 2) e um tipo Huawei
    (5 x 4 = 20 entradas); nome repetido e zero entrada sao recusados. O
    total de entradas e derivado e lido do desenho.
#>
function Testar-Eletrica141 {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-14-1' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-14-1.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-14-1 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if (([regex]::Matches($t, 'ELETRICA recusado:')).Count -ne 2) { $erros += 'nome repetido e zero entrada nao foram os dois recusados' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 modelo(s)'))
    if ($final -notmatch 'ELETRICA MODELO nome="Generico cliente" mppt=1 entradas=2 total=2 tamanho=1.1x0.7x0.6') { $erros += 'o generico nao esta no desenho' }
    if ($final -notmatch 'ELETRICA MODELO nome="Huawei 250" mppt=5 entradas=4 total=20 ') { $erros += 'o Huawei nao tem 20 entradas' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-14-1: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (modelo de inversor: generico e Huawei 5x4=20, repetido e zero recusados)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica141'
