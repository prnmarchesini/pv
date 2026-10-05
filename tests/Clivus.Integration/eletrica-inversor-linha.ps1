<#
    A edicao direto na linha da tabela de inversores (05/10/2026: o bloco
    "Escolhido" / "Salvar inversor" / "Por no trafo" saiu da aba, pedido do
    Renan: "essa parte nao estou entendendo nada, melhore MUITO ela"):
    - o modelo da caixa da linha: o Inversor 1 (5 strings) nao vai para o
      Pequeno (4 entradas), recusado com o porque, e continua Huawei 250; o
      Inversor 2 (3 strings) vai; modelo que nao existe e recusado;
    - o nome da celula: vazio e "inversor 2" (repetido, sem olhar maiuscula)
      recusados; "  INV-NORTE " gravado aparado; nome em duas linhas recusado;
    - o Apagar das linhas escolhidas: INV-NORTE e Inversor 2 de uma vez, as 8
      strings deles livres no desenho, o Skid Norte (que ficou sem inversor)
      some, os inversores 3 e 4 ficam.
    Tudo pelo mesmo caminho da janela (contexto da aplicacao, fora de comando,
    pela EscritaForaDeComando). Usa Substituicoes-Da-Usina.
#>
function Testar-EletricaInversorLinha {
    param([string] $Desenho)

    $sub = Substituicoes-Da-Usina -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-linha'
    if (-not $sub) { $problemas.Add('clivus-eletrica-inversor-linha: nao achei o centro do terreno.'); return $false }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-eletrica-inversor-linha' -Script (Join-Path $PSScriptRoot 'clivus-eletrica-inversor-linha.scr') -Substituicoes $sub

    if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ELETRICA_FIM') -lt 0) {
        $problemas.Add("clivus-eletrica-inversor-linha terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $t = $r.Texto
    $erros = @()
    if ($t -match 'ELETRICA ERRO') { $erros += 'algum passo deu ELETRICA ERRO' }

    # Antes: as strings de cada um e o skid.
    $antes = $t.Substring(0, [Math]::Max(0, $t.IndexOf('CLIVUS_LINHA_ANTES')))
    $antes = $antes.Substring([Math]::Max(0, $antes.LastIndexOf('ELETRICA 4 inversor(es)')))
    if ($antes -notmatch 'ELETRICA INVERSOR nome="Inversor 1" modelo="Huawei 250" strings=5 entradas=20 ') { $erros += 'o Inversor 1 nao comecou com 5 strings no Huawei 250' }
    if ($antes -notmatch 'ELETRICA INVERSOR nome="Inversor 2" modelo="Huawei 250" strings=3 entradas=20 ') { $erros += 'o Inversor 2 nao comecou com 3 strings no Huawei 250' }
    if ($antes -notmatch 'ELETRICA SKID nome="Skid Norte" trafo=T1 inversores=Inversor 1,Inversor 2 fim') { $erros += 'o Skid Norte nao comecou com os inversores 1 e 2' }

    # O modelo e o nome na linha.
    if ($t -notmatch 'ELETRICA janela linha recusado: o Pequeno tem 4 entradas e o Inversor 1 j\S+ tem 5 strings; solte 1 string\(s\) antes') { $erros += 'o Pequeno (4 entradas) nao foi recusado para o Inversor 1 (5 strings)' }
    if ($t -notmatch 'ELETRICA janela linha modelo: Pequeno gravado') { $erros += 'o Inversor 2 (3 strings) nao foi para o Pequeno' }
    if ($t -notmatch 'ELETRICA janela linha recusado: modelo Nenhum nao existe') { $erros += 'modelo que nao existe nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela linha recusado: o nome n\S+o pode ficar vazio') { $erros += 'o nome vazio nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela linha recusado: j\S+ existe um inversor chamado "inversor 2"') { $erros += 'o nome repetido nao foi recusado' }
    if ($t -notmatch 'ELETRICA janela linha nome: \[INV-NORTE\] gravado') { $erros += 'o nome INV-NORTE nao foi gravado' }
    if ($t -notmatch 'ELETRICA janela linha recusado: \[Nome\] com 2 inversor\(es\)') { $erros += 'nome em duas linhas nao foi recusado' }

    $editada = $t.Substring(0, [Math]::Max(0, $t.IndexOf('CLIVUS_LINHA_EDITADA')))
    $editada = $editada.Substring([Math]::Max(0, $editada.LastIndexOf('ELETRICA 4 inversor(es)')))
    if ($editada -notmatch 'ELETRICA INVERSOR nome="INV-NORTE" modelo="Huawei 250" strings=5 entradas=20 trafo=T1 ') { $erros += 'o INV-NORTE nao ficou com o nome, o Huawei 250, as 5 strings e o T1' }
    if ($editada -notmatch 'ELETRICA INVERSOR nome="Inversor 2" modelo="Pequeno" strings=3 entradas=4 trafo=T1 excesso=0') { $erros += 'o Inversor 2 nao ficou no Pequeno com as 3 strings' }
    if ($editada -match 'nome="Inversor 1"') { $erros += 'o Inversor 1 continuou com o nome antigo' }

    # O Apagar das escolhidas.
    if ($t -notmatch 'ELETRICA janela linha apagados=INV-NORTE,Inversor 2 soltas=8 fim') { $erros += 'o apagar das escolhidas nao levou o INV-NORTE e o Inversor 2 com as 8 strings' }
    $final = $t.Substring(0, [Math]::Max(0, $t.IndexOf('CLIVUS_ELETRICA_FIM')))
    $final = $final.Substring([Math]::Max(0, $final.LastIndexOf('ELETRICA 2 inversor(es)')))
    if ($final -notmatch '^ELETRICA 2 inversor\(es\) (\d+) string\(s\) (\d+) livre\(s\)' -or $Matches[1] -ne $Matches[2]) { $erros += 'depois de apagar, nao ficaram 2 inversores com todas as strings livres' }
    if ($final -notmatch 'ELETRICA INVERSOR nome="Inversor 3" ' -or $final -notmatch 'ELETRICA INVERSOR nome="Inversor 4" ') { $erros += 'os inversores 3 e 4 nao ficaram' }
    if ($final -notmatch 'ELETRICA 0 skid\(s\)') { $erros += 'o Skid Norte, sem inversor, nao sumiu' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-eletrica-inversor-linha: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host '  (linha da tabela: modelo recusado pelas strings, nome recusado/gravado, apagar varias)' -ForegroundColor DarkGray
    return $true
}

$script:CasosEletricos += 'Testar-EletricaInversorLinha'
