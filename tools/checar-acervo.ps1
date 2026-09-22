<#
.SYNOPSIS
    Confere o hash de cada arquivo do acervo imutavel.

.DESCRIPTION
    Regra de 04-testes.md: tests/acervo/ e congelado. Entradas, desenhos de
    referencia e resultados esperados nao mudam. Qualquer diferenca de hash
    deixa o placar inteiro vermelho.

    O manifesto e tests/acervo/MANIFESTO.sha256, no formato do sha256sum:

        <hash em minusculas>  <caminho relativo a tests/acervo, com barra normal>

    Linhas em branco e linhas comecando com # sao ignoradas.

    O Claude Code nunca edita o acervo nem o manifesto. Ele propoe o arquivo
    esperado em tests/proposto/, o Renan confere a mao, move para o acervo e
    atualiza o manifesto.

    Limite conhecido: o manifesto mora no mesmo diretorio que ele protege, entao
    quem altera um arquivo do acervo e recalcula a linha correspondente fica
    verde. Por isso o script tambem confronta o manifesto com a versao no git e
    avisa quando ele mudou. O aviso nao derruba o placar, porque atualizar o
    manifesto e um ato legitimo do Renan; mas ele aparece, e e o Renan que
    confirma se aquela mudanca foi dele.

.PARAMETER Silencioso
    Nao imprime nada; so devolve o codigo de saida (0 = ok, 1 = divergencia).
#>
[CmdletBinding()]
param(
    [switch] $Silencioso
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz      = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$acervo    = Join-Path $raiz 'tests\acervo'
$manifesto = Join-Path $acervo 'MANIFESTO.sha256'

function Relatar {
    param([string] $Texto, [string] $Cor = 'Gray')
    if (-not $Silencioso) { Write-Host $Texto -ForegroundColor $Cor }
}

if (-not (Test-Path $acervo -PathType Container)) {
    Relatar "Acervo nao encontrado em $acervo." 'Red'
    exit 1
}
if (-not (Test-Path $manifesto -PathType Leaf)) {
    Relatar "MANIFESTO.sha256 nao encontrado em $acervo." 'Red'
    exit 1
}

# Caminhos canonicos: o proprio manifesto e reconhecido mesmo se o script for
# chamado por um caminho com '..', junction ou nome curto 8.3.
$acervoCanonico    = (Resolve-Path $acervo).Path.TrimEnd('\', '/')
$manifestoCanonico = (Resolve-Path $manifesto).Path

$problemas  = [System.Collections.Generic.List[string]]::new()
$conferidos = 0
$declarados = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

foreach ($linha in Get-Content $manifesto) {
    $texto = $linha.Trim()
    if ($texto -eq '' -or $texto.StartsWith('#')) { continue }

    # "<hash>  <caminho>" ou "<hash> *<caminho>" (modo binario do sha256sum).
    # O caminho nao leva Trim(): espaco no fim de nome de arquivo e legitimo.
    if ($texto -notmatch '^([0-9a-fA-F]{64})\s+\*?(.+)$') {
        $problemas.Add("linha mal formada no manifesto: $texto")
        continue
    }

    $esperado = $Matches[1].ToLowerInvariant()
    $relativo = $Matches[2]

    # Um caminho com '..' ou absoluto conferiria arquivo de fora do acervo e
    # passaria verde carregando qualquer coisa.
    $normalizado = $relativo.Replace('/', '\')
    if ([IO.Path]::IsPathRooted($normalizado) -or
        ($normalizado -split '\\') -contains '..') {
        $problemas.Add("caminho invalido no manifesto (sai do acervo): $relativo")
        continue
    }

    $null = $declarados.Add($relativo.Replace('\', '/'))

    $arquivo = Join-Path $acervo $normalizado
    if (-not (Test-Path $arquivo -PathType Leaf)) {
        $problemas.Add("faltando no acervo: $relativo")
        continue
    }

    # Cinto e suspensorio: mesmo sem '..', um link pode apontar para fora.
    $destino = (Resolve-Path $arquivo).Path
    if (-not $destino.StartsWith($acervoCanonico + '\', [StringComparison]::OrdinalIgnoreCase)) {
        $problemas.Add("o caminho $relativo resolve para fora do acervo: $destino")
        continue
    }

    $obtido = (Get-FileHash $arquivo -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($obtido -ne $esperado) {
        $problemas.Add("hash diferente: $relativo")
        $problemas.Add("    esperado $esperado")
        $problemas.Add("    obtido   $obtido")
        continue
    }

    $conferidos++
}

# Arquivo no acervo que o manifesto nao declara tambem e divergencia: ou alguem
# acrescentou sem congelar, ou o manifesto ficou para tras. -Force pega tambem
# arquivo oculto, que sem isso entraria sem ser declarado nem detectado.
Get-ChildItem $acervo -Recurse -File -Force |
    Where-Object { $_.FullName -ne $manifestoCanonico } |
    ForEach-Object {
        $relativo = $_.FullName.Substring($acervoCanonico.Length).TrimStart('\', '/').Replace('\', '/')
        if (-not $declarados.Contains($relativo)) {
            $problemas.Add("no acervo mas fora do manifesto: $relativo")
        }
    }

# ---- o manifesto mudou desde o ultimo commit? ------------------------------

$avisos = [System.Collections.Generic.List[string]]::new()
if (Get-Command git -ErrorAction SilentlyContinue) {
    $sujo = & git -C $raiz status --porcelain -- 'tests/acervo/MANIFESTO.sha256' 2>$null
    if ($LASTEXITCODE -eq 0 -and $sujo) {
        $avisos.Add('MANIFESTO.sha256 esta alterado em relacao ao git. So o Renan muda o manifesto (ver 04-testes.md).')
    }
}

# ---- veredito --------------------------------------------------------------

if ($problemas.Count -gt 0) {
    Relatar 'Acervo FALHOU:' 'Red'
    foreach ($p in $problemas) { Relatar "  $p" 'Red' }
    exit 1
}

foreach ($a in $avisos) { Relatar "Aviso: $a" 'Yellow' }

if ($conferidos -eq 0) {
    Relatar 'Acervo OK (manifesto vazio: nada congelado ainda).' 'DarkGray'
} else {
    Relatar "Acervo OK ($conferidos arquivo(s) conferido(s))." 'Green'
}
exit 0
