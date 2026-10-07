<#
    Reprovacao de 05/10/2026 (subestacao compartilhada x UCs): o bloco fisico
    da compartilhada (dicionario SUBESTACOES_BLOCOS) com as UCs UC1, UC2...
    dentro (cada uma aponta para ele). O desenho do formato 1 (UCs sem bloco)
    e lido com as compartilhadas num bloco "Subestacao compartilhada" (o mesmo
    GUID antes e depois de gravar); a unitaria continua bloco e UC. O resumo
    mostra o bloco com as UCs dentro; apagar o bloco leva as UCs e solta os
    trafos sem apagar os trafos.
#>
function Testar-EletricaSubestacaoBloco {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-12-1-bloco' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-12-1-bloco.scr')

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-12-1-bloco terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    $iLido = $t.IndexOf('CLIVUS_BLOCO_LIDO')
    $iGravado = $t.IndexOf('CLIVUS_BLOCO_GRAVADO')
    $iApagado = $t.IndexOf('CLIVUS_BLOCO_APAGADO')
    if ($iLido -lt 0 -or $iGravado -lt $iLido -or $iApagado -lt $iGravado) {
        $problemas.Add("clivus-eletrica-12-1-bloco: marcadores fora de ordem. Veja $($r.Saida)")
        return $false
    }

    $lido = $t.Substring($iLido, $iGravado - $iLido)
    $gravado = $t.Substring($iGravado, $iApagado - $iGravado)
    $apagado = $t.Substring($iApagado)

    $idLido = $null
    if ($lido -notmatch 'ELETRICA 1 bloco\(s\) migradas=2 formato_ucs=1') { $erros += 'o formato antigo nao foi lido com as 2 compartilhadas num bloco' }
    if ($lido -match 'ELETRICA BLOCO nome="Subesta\S+o compartilhada" tamanho=5x4x3 ucs=UC1,UC2 id=(\S+) fim') { $idLido = $Matches[1] }
    else { $erros += 'o bloco do formato antigo nao tem UC1 e UC2 com o tamanho da UC1' }
    if ($lido -notmatch 'ELETRICA UC U1 modo=Unitary') { $erros += 'a unitaria do formato antigo sumiu' }

    if ($t -notmatch 'ELETRICA uc UC3 criada') { $erros += 'a UC3 nao foi criada' }
    if ($t -notmatch 'ELETRICA uc UC2 editada') { $erros += 'o nome da UC2 nao foi salvo' }
    if ($t -notmatch 'ELETRICA bloco editado') { $erros += 'o bloco nao foi editado' }
    if ($gravado -notmatch 'ELETRICA 1 bloco\(s\) migradas=0 formato_ucs=2') { $erros += 'depois de gravar, o desenho nao esta no formato 2 com o bloco' }
    if ($gravado -match 'ELETRICA BLOCO nome="Cubiculo Norte" tamanho=6x3x2\.8 ucs=UC1,UC2,UC3 id=(\S+) fim') {
        if ($Matches[1] -ne $idLido) { $erros += "o bloco mudou de GUID ao gravar ($idLido -> $($Matches[1]))" }
    }
    else { $erros += 'o bloco gravado nao tem nome, tamanho e as tres UCs' }
    if ($gravado -notmatch 'ELETRICA UC UC1 modo=Shared .* trafos=T1,T2\s') { $erros += 'a UC1 nao tem T1 e T2' }
    if ($gravado -notmatch 'ELETRICA UC UC2 modo=Shared nome="Medicao Sul"') { $erros += 'a UC2 nao tem o nome novo' }
    if ($gravado -notmatch 'ELETRICA UC U1 modo=Unitary .* trafos=T3\s') { $erros += 'a U1 nao tem o T3' }
    if ($gravado -notmatch 'RESUMO_BLOCO nome=Cubiculo_Norte ucs=UC1,UC2,UC3 ') { $erros += 'o resumo nao mostra o bloco com as UCs' }

    if ($t -notmatch 'ELETRICA bloco apagado ucs=3 trafos=2') { $erros += 'apagar o bloco nao levou as 3 UCs e os 2 trafos' }
    if ($apagado -notmatch 'ELETRICA 1 subestacao\(oes\)') { $erros += 'sobrou UC compartilhada depois de apagar o bloco' }
    if ($apagado -notmatch 'ELETRICA 0 bloco\(s\)') { $erros += 'o bloco nao saiu' }
    if ($apagado -notmatch 'ELETRICA 3 trafo\(s\)') { $erros += 'apagar o bloco apagou trafo' }
    if ($apagado -notmatch 'ELETRICA TRAFO T1 .* uc=\s') { $erros += 'o T1 ficou com UC apagada' }
    if ($apagado -notmatch 'ELETRICA TRAFO T3 .* uc=U1\s') { $erros += 'o T3 perdeu a U1' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-12-1-bloco: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (bloco compartilhado: formato antigo lido, UCs dentro, nome e trafos de cada UC, resumo, apagar)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaSubestacaoBloco'
