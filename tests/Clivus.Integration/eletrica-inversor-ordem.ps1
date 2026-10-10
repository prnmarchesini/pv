<#
    A aba Inversor de 10/10/2026 (pedidos do Renan), pelo mesmo caminho da
    janela (contexto da aplicacao, fora de comando, pela EscritaForaDeComando):
    - "quando eu clicar em soltar as strings de um inversor, as tags devem ser
      apagadas": o Soltar da linha (Inversor 1, 5 strings) leva as 5 tags e os
      5 textos; o "Soltar todas da usina" leva as outras 5; as strings ficam
      no desenho, livres e sem tag;
    - a ordem da lista: o caso real (nao tem o Inversor 1; o criado sai
      Inversor 5 e e renomeado para Inversor 1, e fica no fim); Ordenar por
      Nome (natural) poe ele no comeco; arrastar (Mover) sobe e desce; por
      Trafo (T1, T2, sem trafo por ultimo, estavel) e por Trafo e Nome; cada
      ordem lida de volta do desenho pela tabela;
    - "Apagar todos": os 4 inversores de uma vez, com as 3 tags das strings
      que eram deles.
    Usa Substituicoes-Da-Usina.
#>
function Testar-EletricaInversorOrdem {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-ordem'
    if (-not $sub) { $problemas.Add('clivus-eletrica-inversor-ordem: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-ordem' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-inversor-ordem.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-inversor-ordem terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -match 'ELETRICA ERRO') { $erros += 'algum passo deu ELETRICA ERRO' }

    # O trecho entre o marcador anterior e este.
    function Trecho([string] $Marca, [string] $Antes) {
        $fim = $t.IndexOf($Marca)
        if ($fim -lt 0) { return '' }
        $ini = if ($Antes) { [Math]::Max(0, $t.LastIndexOf($Antes, $fim)) } else { 0 }
        return $t.Substring($ini, $fim - $ini)
    }

    # A ordem da tabela no trecho (a ultima listagem dele).
    function Ordem([string] $Marca, [string] $Antes) {
        $trecho = Trecho $Marca $Antes
        $nomes = @([regex]::Matches($trecho, 'ELETRICA TABELA nome=(\S+) ') | ForEach-Object { $_.Groups[1].Value })
        return ($nomes -join ',')
    }

    # ---- Soltar apaga as tags
    if ($t -notmatch 'CLIVUS_ORDEM_TEXTOS fase=gerou textos=10') { $erros += 'gerar nao deixou 10 textos de tag' }
    if ($t -notmatch 'ELETRICA janela linha soltar Inversor 1: soltas=5 tags=5 fim') { $erros += 'o Soltar do Inversor 1 nao soltou as 5 strings com as 5 tags' }
    if ($t -notmatch 'CLIVUS_ORDEM_TEXTOS fase=soltou1 textos=5') { $erros += 'depois do Soltar do Inversor 1 nao ficaram 5 textos' }
    $soltou1 = Trecho 'CLIVUS_ORDEM_SOLTOU1' 'CLIVUS_ORDEM_TEXTOS fase=soltou1'
    if (@([regex]::Matches($soltou1, 'NUMERACAO_STRING tag=- inversor=- ')).Count -lt 5) { $erros += 'as strings soltas do Inversor 1 nao ficaram sem tag' }
    if (@([regex]::Matches($soltou1, 'NUMERACAO_STRING tag=[^-]\S* inversor=-')).Count -gt 0) { $erros += 'string livre ficou com tag' }
    if (@([regex]::Matches($soltou1, 'NUMERACAO_STRING tag=[^-]\S* inversor=Inversor_[23] ')).Count -ne 5) { $erros += 'as 5 tags dos inversores 2 e 3 nao ficaram' }

    if ($t -notmatch 'ELETRICA janela linha soltar usina: soltas=5 tags=5 fim') { $erros += 'o Soltar todas da usina nao soltou 5 strings com 5 tags' }
    if ($t -notmatch 'CLIVUS_ORDEM_TEXTOS fase=soltouusina textos=0') { $erros += 'depois do Soltar todas da usina sobrou texto de tag' }
    $usina = Trecho 'CLIVUS_ORDEM_SOLTOUUSINA' 'CLIVUS_ORDEM_TEXTOS fase=soltouusina'
    $todas = @([regex]::Matches($usina, 'NUMERACAO_STRING ')).Count
    if ($todas -lt 10) { $erros += "as strings sumiram do desenho ($todas listadas)" }
    if (@([regex]::Matches($usina, 'NUMERACAO_STRING tag=- inversor=- ')).Count -ne $todas) { $erros += 'depois do Soltar todas da usina, alguma string ficou com tag ou inversor' }

    # ---- a ordem
    $o = Ordem 'CLIVUS_ORDEM_CRIADO' 'CLIVUS_ORDEM_SOLTOUUSINA'
    if ($o -ne 'Inversor_2,Inversor_3,Inversor_4,Inversor_1') { $erros += "o Inversor 1 recriado nao ficou no fim ($o)" }
    $o = Ordem 'CLIVUS_ORDEM_NOME' 'CLIVUS_ORDEM_CRIADO'
    if ($o -ne 'Inversor_1,Inversor_2,Inversor_3,Inversor_4') { $erros += "ordenar por nome nao deu 1,2,3,4 ($o)" }
    if ($t -notmatch 'ELETRICA janela linha ordenar Name: Lista ordenada: 4 inversor\(es\) mudaram de lugar') { $erros += 'ordenar por nome nao disse que 4 mudaram' }
    if ($t -notmatch 'ELETRICA janela linha ordenar Name: A lista j\S+ estava nessa ordem') { $erros += 'ordenar de novo nao disse que ja estava na ordem' }
    if ($t -notmatch 'ELETRICA janela linha mover: Inversor 4 agora \S+ o 1\S+ da lista') { $erros += 'arrastar o Inversor 4 nao disse que ele virou o 1o' }
    $o = Ordem 'CLIVUS_ORDEM_SUBIU' 'CLIVUS_ORDEM_NOME'
    if ($o -ne 'Inversor_4,Inversor_1,Inversor_2,Inversor_3') { $erros += "arrastar o Inversor 4 para o lugar do 1 nao deu 4,1,2,3 ($o)" }
    $o = Ordem 'CLIVUS_ORDEM_TRAFO' 'CLIVUS_ORDEM_SUBIU'
    if ($o -ne 'Inversor_3,Inversor_4,Inversor_2,Inversor_1') { $erros += "ordenar por trafo nao deu 3 (T1), 4 e 2 (T2), 1 (sem) ($o)" }
    $o = Ordem 'CLIVUS_ORDEM_TRAFONOME' 'CLIVUS_ORDEM_TRAFO'
    if ($o -ne 'Inversor_3,Inversor_2,Inversor_4,Inversor_1') { $erros += "ordenar por trafo e nome nao deu 3, 2, 4, 1 ($o)" }
    if ($t -notmatch 'ELETRICA janela linha recusado: inversor Nenhum nao existe') { $erros += 'mover para inversor que nao existe nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela linha recusado: ordem \[Cor\]') { $erros += 'ordem que nao existe nao foi recusada' }
    $o = Ordem 'CLIVUS_ORDEM_DESCEU' 'CLIVUS_ORDEM_TRAFONOME'
    if ($o -ne 'Inversor_2,Inversor_4,Inversor_1,Inversor_3') { $erros += "arrastar o Inversor 3 para o lugar do 1 nao deu 2,4,1,3 ($o)" }

    # ---- Apagar todos
    if ($t -notmatch 'CLIVUS_ORDEM_TEXTOS fase=gerou2 textos=3') { $erros += 'gerar de novo nao deixou 3 textos' }
    if ($t -notmatch 'ELETRICA janela linha apagados=Inversor 2,Inversor 4,Inversor 1,Inversor 3 soltas=3 fim') { $erros += 'o Apagar todos nao levou os 4 inversores (na ordem da lista) com as 3 strings' }
    if ($t -notmatch 'ELETRICA janela linha apagados=4 tags=3 fim') { $erros += 'o Apagar todos nao disse 3 tags apagadas' }
    if ($t -notmatch 'CLIVUS_ORDEM_TEXTOS fase=apagoutodos textos=0') { $erros += 'depois do Apagar todos sobrou texto de tag' }
    $final = Trecho 'CLIVUS_ELETRICA_FIM' 'CLIVUS_ORDEM_TEXTOS fase=apagoutodos'
    if ($final -notmatch 'ELETRICA 0 inversor\(es\)') { $erros += 'depois do Apagar todos ainda ha inversor' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-inversor-ordem: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (soltar apaga as tags; ordem por nome, trafo, trafo e nome e arrastar, lida de volta; apagar todos)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaInversorOrdem'
