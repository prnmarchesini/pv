<#
.SYNOPSIS
    Roda todos os testes e imprime o placar por etapa.

.DESCRIPTION
    Exigencia de 04-testes.md: na etapa N, os testes de todas as etapas
    anteriores rodam e ficam verdes. Este script e o comando unico que da o
    placar.

    Nivel 1: testes puros (xUnit), separados por etapa pelo trait [Trait("Etapa", "N")].
    Nivel 2: testes dentro do CAD via Core Console (entram no placar no passo 0.5).
    Acervo:  hash dos arquivos congelados, conferido por checar-acervo.ps1.

    O placar so e VERDE se tudo estiver verde, inclusive o acervo.

    Cuidados que este script toma, cada um por causa de um jeito de o placar mentir:

    - Um .trx por projeto de teste e por etapa. Um arquivo unico para a solucao
      inteira seria sobrescrito pelo ultimo projeto a terminar, e um teste
      falhando em outro projeto sumiria do placar.
    - Soma dos testes com trait conferida contra o total do projeto. Teste sem
      [Trait("Etapa", ...)] nao roda em nenhum filtro e passaria despercebido.
    - Codigo de saida do dotnet test conferido. Runner que morre antes de
      escrever o .trx e falha, nao "sem testes".
    - A saida do dotnet test vai para arquivo e e mostrada quando algo falha.

.PARAMETER Etapa
    Roda so as etapas indicadas. Sem isso, roda todas.

.PARAMETER SemCompilar
    Pula a compilacao (use quando acabou de compilar).

.EXAMPLE
    .\tools\rodar-testes.ps1
    .\tools\rodar-testes.ps1 -Etapa 0
#>
[CmdletBinding()]
param(
    [int[]] $Etapa,
    [switch] $SemCompilar
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz    = Split-Path -Parent $PSScriptRoot
$solucao = Join-Path $raiz 'UFV.sln'
$saida   = Join-Path $raiz 'artefatos\testes'

# @(0) e falsy em PowerShell, entao "-Etapa 0" com if ($Etapa) rodaria tudo.
$etapas = if ($PSBoundParameters.ContainsKey('Etapa')) { $Etapa } else { 0..7 }

# O dotnet pode nao estar no PATH da sessao logo depois de instalado.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "C:\Program Files\dotnet;$env:PATH"
}

$problemas = [System.Collections.Generic.List[string]]::new()

function Escrever-Linha {
    param([string] $Rotulo, [string] $Placar, [string] $Situacao)

    $cor = switch ($Situacao) {
        'OK'     { 'Green' }
        'FALHOU' { 'Red' }
        default  { 'DarkGray' }
    }
    Write-Host ('{0,-10}{1,-9}' -f $Rotulo, $Placar) -NoNewline
    Write-Host $Situacao -ForegroundColor $cor
}

<#
    Roda um projeto de teste e devolve a contagem lida do .trx.
    $Filtro vazio = todos os testes do projeto.
#>
function Invoke-ProjetoDeTeste {
    param(
        [string] $Csproj,
        [string] $Filtro,
        [string] $Rotulo
    )

    $trx = Join-Path $saida "$Rotulo.trx"
    $log = Join-Path $saida "$Rotulo.log"

    $argumentos = @(
        'test', $Csproj,
        '--no-build',
        '--nologo',
        '-v', 'quiet',
        '--results-directory', $saida,
        '--logger', "trx;LogFileName=$Rotulo.trx"
    )
    if ($Filtro) { $argumentos += @('--filter', $Filtro) }

    # O dotnet test escreve o resumo das falhas no stderr; com
    # ErrorActionPreference = Stop isso viraria erro terminante antes do placar.
    # A saida vai para arquivo e e mostrada no fim quando algo falha.
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet @argumentos *> $log
        $codigo = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $anterior
    }

    if (-not (Test-Path $trx)) {
        # Sem .trx o runner nao chegou a rodar. Codigo 0 aqui seria mentira.
        return [pscustomobject]@{
            Total = 0; Passou = 0; Falhou = 0
            Executou = ($codigo -eq 0)
            Log = $log
        }
    }

    [xml] $resultado = Get-Content $trx
    $c = $resultado.TestRun.ResultSummary.Counters

    return [pscustomobject]@{
        Total    = [int] $c.total
        Passou   = [int] $c.passed
        Falhou   = [int] $c.failed
        Executou = $true
        Log      = $log
    }
}

# ---- compilacao ------------------------------------------------------------

if (-not $SemCompilar) {
    Write-Host 'Compilando...' -ForegroundColor DarkGray

    $logBuild = Join-Path $env:TEMP 'ufv-build.log'
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet build $solucao -v quiet --nologo *> $logBuild
        $codigoBuild = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $anterior
    }

    if ($codigoBuild -ne 0) {
        Write-Host 'A solucao nao compila. Placar vermelho.' -ForegroundColor Red
        Get-Content $logBuild | Select-Object -Last 40 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
}

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
New-Item -ItemType Directory -Path $saida -Force | Out-Null

$projetos = Get-ChildItem (Join-Path $raiz 'tests') -Filter '*.csproj' -Recurse -File |
            Sort-Object FullName

if ($projetos.Count -eq 0) {
    Write-Host 'Nenhum projeto de teste encontrado em tests/.' -ForegroundColor Red
    exit 1
}

# ---- nivel 1: testes puros -------------------------------------------------

Write-Host ''

foreach ($n in $etapas) {
    $total = 0; $passou = 0; $falhou = 0; $tudoExecutou = $true

    foreach ($projeto in $projetos) {
        $nome = [IO.Path]::GetFileNameWithoutExtension($projeto.Name)
        $r = Invoke-ProjetoDeTeste -Csproj $projeto.FullName -Filtro "Etapa=$n" -Rotulo "etapa-$n--$nome"

        $total += $r.Total; $passou += $r.Passou; $falhou += $r.Falhou
        if (-not $r.Executou) {
            $tudoExecutou = $false
            $problemas.Add("etapa $n / $nome nao chegou a rodar. Log: $($r.Log)")
        }
        elseif ($r.Falhou -gt 0) {
            $problemas.Add("etapa $n / $nome com $($r.Falhou) falha(s). Log: $($r.Log)")
        }
    }

    if (-not $tudoExecutou) {
        Escrever-Linha "Etapa $n" '?' 'FALHOU'
        continue
    }

    if ($total -eq 0) {
        Escrever-Linha "Etapa $n" '-' 'sem testes'
        continue
    }

    $situacao = if ($falhou -eq 0 -and $passou -eq $total) { 'OK' } else { 'FALHOU' }
    Escrever-Linha "Etapa $n" "$passou/$total" $situacao
}

# ---- todo teste precisa declarar sua etapa ---------------------------------
# Um teste sem [Trait("Etapa", ...)] nao e pego por nenhum filtro: nao roda,
# nao aparece no placar e ninguem nota. Aqui a soma por etapa e conferida
# contra o total do projeto. So faz sentido quando o placar roda inteiro.

if (-not $PSBoundParameters.ContainsKey('Etapa')) {
    foreach ($projeto in $projetos) {
        $nome = [IO.Path]::GetFileNameWithoutExtension($projeto.Name)

        $todos = Invoke-ProjetoDeTeste -Csproj $projeto.FullName -Filtro '' -Rotulo "todos--$nome"
        if (-not $todos.Executou) {
            $problemas.Add("$nome nao chegou a rodar na contagem total. Log: $($todos.Log)")
            continue
        }

        $comEtapa = 0
        foreach ($n in 0..7) {
            $trx = Join-Path $saida "etapa-$n--$nome.trx"
            if (Test-Path $trx) {
                [xml] $x = Get-Content $trx
                $comEtapa += [int] $x.TestRun.ResultSummary.Counters.total
            }
        }

        if ($todos.Total -ne $comEtapa) {
            $problemas.Add(
                "$nome tem $($todos.Total) teste(s), mas so $comEtapa declaram [Trait(`"Etapa`", ...)]. " +
                'Teste sem etapa nunca roda no placar (ver 04-testes.md).')
        }
    }
}

# ---- nivel 2: Core Console -------------------------------------------------
# Entra no placar no passo 0.5.

$nivel2 = Join-Path $raiz 'tests\UFV.Integration\rodar.ps1'
if (Test-Path $nivel2) {
    & $nivel2
    if ($LASTEXITCODE -ne 0) { $problemas.Add('nivel 2 (Core Console) falhou.') }
} else {
    Escrever-Linha 'Nivel 2' '-' 'sem testes'
}

# ---- acervo ----------------------------------------------------------------

try {
    & (Join-Path $PSScriptRoot 'checar-acervo.ps1') -Silencioso
    $codigoAcervo = $LASTEXITCODE
}
catch {
    $codigoAcervo = 1
    $problemas.Add("checar-acervo.ps1 lancou: $($_.Exception.Message)")
}

if ($codigoAcervo -eq 0) {
    Escrever-Linha 'Acervo' '' 'OK'
} else {
    Escrever-Linha 'Acervo' '' 'FALHOU'
    $problemas.Add('acervo divergente. Rode tools\checar-acervo.ps1 para ver o detalhe.')
}

# ---- veredito --------------------------------------------------------------

Write-Host ''
if ($problemas.Count -eq 0) {
    Write-Host 'Tudo verde.' -ForegroundColor Green
    exit 0
}

Write-Host 'Placar vermelho. Conserte antes de seguir (ver 03-protocolo-de-passo.md, fase 4).' -ForegroundColor Red
foreach ($p in $problemas) { Write-Host "  - $p" -ForegroundColor Red }
exit 1
