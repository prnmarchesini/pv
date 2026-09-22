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

    Este script so monta. Instalar e trabalho de tools\instalar.ps1, que antes
    confere a versao do Civil 3D; com -Instalar, chamamos ele.

.PARAMETER Configuracao
    Debug (padrao) ou Release.

.PARAMETER Instalar
    Alem de montar, chama tools\instalar.ps1 para instalar o bundle montado.

.PARAMETER Desinstalar
    Chama tools\instalar.ps1 -Desinstalar e sai.

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

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "C:\Program Files\dotnet;$env:PATH"
}

# ---- desinstalar -----------------------------------------------------------

if ($Desinstalar) {
    & (Join-Path $PSScriptRoot 'instalar.ps1') -Desinstalar
    exit $LASTEXITCODE
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
# Quem instala e tools\instalar.ps1, e so ele. Duplicar a copia aqui criaria um
# segundo caminho de instalacao sem a checagem de versao, que e a razao de o
# instalador existir.

if (-not $Instalar) {
    Write-Host 'Use -Instalar para instalar (chama tools\instalar.ps1).' -ForegroundColor DarkGray
    exit 0
}

& (Join-Path $PSScriptRoot 'instalar.ps1') -Bundle $bundle
exit $LASTEXITCODE
