<#
    13.1: o cadastro do trafo. Um em branco (T1) e um do padrao 2 (T2,
    2500 kVA 800 V / 13800 V); edita a potencia e as observacoes do T1;
    o apelido repetido do T2 e recusado; a lista final sai do desenho.
#>
function Testar-Eletrica131 {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-13-1' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-13-1.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-13-1 terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -notmatch 'ELETRICA trafo T1 criado') { $erros += 'T1 nao foi criado' }
    if ($t -notmatch 'ELETRICA trafo T2 criado') { $erros += 'T2 nao foi criado' }
    if ($t -notmatch 'ELETRICA recusado: .*apelido') { $erros += 'o apelido repetido nao foi recusado' }
    $final = $t.Substring($t.LastIndexOf('ELETRICA 2 trafo(s)'))
    if ($final -notmatch 'ELETRICA TRAFO T1 nome="Trafo 1" entrada=0 saida=0 kva=1000 k=0 z=0 tamanho=3x2.5x2.5 notas="para-raios 12 kV"') { $erros += 'T1 no desenho nao tem os campos editados' }
    if ($final -notmatch 'ELETRICA TRAFO T2 nome="Trafo 2" entrada=800 saida=13800 kva=2500 k=1 z=6 tamanho=3x2.5x2.5') { $erros += 'T2 no desenho nao tem os campos do padrao' }
    if ($t -notmatch 'configura\S+ el\S+trica precisa da interface') { $erros += 'CLIVUS_ELETRICA sem interface nao avisou' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-13-1: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (trafo: em branco e do padrao, editar, apelido repetido recusado)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-Eletrica131'
