<#
.SYNOPSIS
    Monta o UFV.bundle e, opcionalmente, instala para o usuario atual.

.DESCRIPTION
    O bundle e uma pasta com esta forma:

        UFV.bundle/
          PackageContents.xml
          Contents/
            UFV.Plugin.dll
            UFV.Core.dll
            UFV.Geo.dll

    Colocada em %APPDATA%\Autodesk\ApplicationPlugins, o Civil 3D a carrega
    sozinho ao abrir, sem NETLOAD.

    As DLLs do AutoCAD nao entram no bundle: sao referencias com Copy Local =
    false, porque o AutoCAD ja as tem carregadas (ver 02-arquitetura.md).

    Este script monta e copia. Detectar versao instalada e avisar de
    incompatibilidade e trabalho do instalador, no passo 0.6.

.PARAMETER Configuracao
    Debug (padrao) ou Release.

.PARAMETER Instalar
    Alem de montar, copia para ApplicationPlugins do usuario atual.

.PARAMETER Desinstalar
    Remove o bundle de ApplicationPlugins e sai.

.EXAMPLE
    .\tools\publicar-bundle.ps1 -Instalar
    .\tools\publicar-bundle.ps1 -Desinstalar
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuracao = 'Debug',

    [switch] $Instalar,
    [switch] $Desinstalar
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz     = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$projeto  = Join-Path $raiz 'src\UFV.Plugin\UFV.Plugin.csproj'
$bundle   = Join-Path $raiz 'artefatos\UFV.bundle'
$destino  = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\UFV.bundle'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "C:\Program Files\dotnet;$env:PATH"
}

# ---- desinstalar -----------------------------------------------------------

if ($Desinstalar) {
    if (Test-Path $destino) {
        Remove-Item $destino -Recurse -Force
        Write-Host "Bundle removido de $destino" -ForegroundColor Green
    } else {
        Write-Host "Nada a remover: $destino nao existe." -ForegroundColor DarkGray
    }
    exit 0
}

# ---- compilar --------------------------------------------------------------

Write-Host "Compilando UFV.Plugin ($Configuracao)..." -ForegroundColor DarkGray

$anterior = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & dotnet build $projeto -c $Configuracao -v quiet --nologo *> (Join-Path $env:TEMP 'ufv-bundle-build.log')
    $codigo = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $anterior
}

if ($codigo -ne 0) {
    Write-Host 'UFV.Plugin nao compila.' -ForegroundColor Red
    Get-Content (Join-Path $env:TEMP 'ufv-bundle-build.log') | Select-Object -Last 30 |
        ForEach-Object { Write-Host "  $_" }
    exit 1
}

# AppendTargetFrameworkToOutputPath = false no .csproj: a saida nao tem a
# pasta do framework.
$saidaDoBuild = Join-Path $raiz "src\UFV.Plugin\bin\$Configuracao"
if (-not (Test-Path (Join-Path $saidaDoBuild 'UFV.Plugin.dll'))) {
    Write-Host "UFV.Plugin.dll nao encontrada em $saidaDoBuild." -ForegroundColor Red
    exit 1
}

# ---- montar ----------------------------------------------------------------

if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
$conteudo = Join-Path $bundle 'Contents'
New-Item -ItemType Directory -Path $conteudo -Force | Out-Null

Copy-Item (Join-Path $raiz 'src\UFV.Plugin\PackageContents.xml') $bundle

# So o que e nosso. O resto o AutoCAD ja tem.
foreach ($nome in 'UFV.Plugin.dll', 'UFV.Core.dll', 'UFV.Geo.dll') {
    $origem = Join-Path $saidaDoBuild $nome
    if (-not (Test-Path $origem)) {
        Write-Host "Faltando na saida do build: $nome" -ForegroundColor Red
        exit 1
    }
    Copy-Item $origem $conteudo
}

# Os .pdb ajudam a ler a pilha de uma excecao durante o desenvolvimento.
if ($Configuracao -eq 'Debug') {
    Get-ChildItem $saidaDoBuild -Filter 'UFV.*.pdb' -File |
        ForEach-Object { Copy-Item $_.FullName $conteudo }
}

Write-Host "Bundle montado em $bundle" -ForegroundColor Green

# ---- instalar --------------------------------------------------------------

if (-not $Instalar) {
    Write-Host 'Use -Instalar para copiar para ApplicationPlugins.' -ForegroundColor DarkGray
    exit 0
}

# O AutoCAD segura as DLLs enquanto esta aberto.
$aberto = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
if ($aberto) {
    Write-Host 'O AutoCAD/Civil 3D esta aberto. Feche antes de instalar o bundle.' -ForegroundColor Red
    exit 1
}

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
New-Item -ItemType Directory -Path (Split-Path -Parent $destino) -Force | Out-Null
Copy-Item $bundle $destino -Recurse

Write-Host "Instalado em $destino" -ForegroundColor Green
Write-Host 'Abra o Civil 3D: a aba UFV aparece sozinha, sem NETLOAD.' -ForegroundColor DarkGray
exit 0
