<#
.SYNOPSIS
    Monta o ClivusSolar.bundle e, opcionalmente, instala para o usuario atual.

.DESCRIPTION
    O bundle e uma pasta com esta forma:

        ClivusSolar.bundle/
          PackageContents.xml
          Contents/
            Clivus.Plugin.dll
            Clivus.Core.dll
            Clivus.Geo.dll

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

.PARAMETER ParaTodaAMaquina
    Com -Instalar, instala em %PROGRAMFILES%\Autodesk\ApplicationPlugins,
    onde o Civil 3D confia no plugin e para de avisar que a DLL nao e
    assinada (pedido do Renan em 30/09/2026). Pede elevacao sozinho: o
    Windows mostra o "Deseja permitir...?" na tela. Sem este parametro, se
    a instalacao da maquina ja existe, e para la que vai, de qualquer jeito:
    duas copias fariam o AutoCAD carregar a que achasse primeiro.

.EXAMPLE
    .\tools\publicar-bundle.ps1 -Instalar
    .\tools\publicar-bundle.ps1 -Desinstalar
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuracao = 'Debug',

    [switch] $Instalar,
    [switch] $Desinstalar,
    [switch] $ParaTodaAMaquina
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz     = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$projeto  = Join-Path $raiz 'src\Clivus.Plugin\Clivus.Plugin.csproj'
$bundle   = Join-Path $raiz 'artefatos\ClivusSolar.bundle'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "C:\Program Files\dotnet;$env:PATH"
}

# ---- desinstalar -----------------------------------------------------------

if ($Desinstalar) {
    & (Join-Path $PSScriptRoot 'instalar.ps1') -Desinstalar
    exit $LASTEXITCODE
}

# ---- compilar --------------------------------------------------------------

Write-Host "Compilando Clivus.Plugin ($Configuracao)..." -ForegroundColor DarkGray

$anterior = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & dotnet build $projeto -c $Configuracao -v quiet --nologo *> (Join-Path $env:TEMP 'clivus-bundle-build.log')
    $codigo = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $anterior
}

if ($codigo -ne 0) {
    Write-Host 'Clivus.Plugin nao compila.' -ForegroundColor Red
    Get-Content (Join-Path $env:TEMP 'clivus-bundle-build.log') | Select-Object -Last 30 |
        ForEach-Object { Write-Host "  $_" }
    exit 1
}

# AppendTargetFrameworkToOutputPath = false no .csproj: a saida nao tem a
# pasta do framework.
$saidaDoBuild = Join-Path $raiz "src\Clivus.Plugin\bin\$Configuracao"
if (-not (Test-Path (Join-Path $saidaDoBuild 'Clivus.Plugin.dll'))) {
    Write-Host "Clivus.Plugin.dll nao encontrada em $saidaDoBuild." -ForegroundColor Red
    exit 1
}

# ---- montar ----------------------------------------------------------------

if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
$conteudo = Join-Path $bundle 'Contents'
New-Item -ItemType Directory -Path $conteudo -Force | Out-Null

Copy-Item (Join-Path $raiz 'src\Clivus.Plugin\PackageContents.xml') $bundle

# So o que e nosso. O resto o AutoCAD ja tem.
foreach ($nome in 'Clivus.Plugin.dll', 'Clivus.Core.dll', 'Clivus.Geo.dll') {
    $origem = Join-Path $saidaDoBuild $nome
    if (-not (Test-Path $origem)) {
        Write-Host "Faltando na saida do build: $nome" -ForegroundColor Red
        exit 1
    }
    Copy-Item $origem $conteudo
}

# Os .pdb ajudam a ler a pilha de uma excecao durante o desenvolvimento.
if ($Configuracao -eq 'Debug') {
    Get-ChildItem $saidaDoBuild -Filter 'Clivus.*.pdb' -File |
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

$instalador = Join-Path $PSScriptRoot 'instalar.ps1'
$daMaquina = Join-Path $env:ProgramFiles 'Autodesk\ApplicationPlugins\ClivusSolar.bundle'

if (-not $ParaTodaAMaquina -and (Test-Path $daMaquina)) {
    Write-Host "O plugin ja esta instalado para a maquina ($daMaquina): a versao nova vai para la." -ForegroundColor DarkGray
    $ParaTodaAMaquina = $true
}

if (-not $ParaTodaAMaquina) {
    & $instalador -Bundle $bundle
    exit $LASTEXITCODE
}

$eu = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())

if ($eu.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    & $instalador -Bundle $bundle -ParaTodaAMaquina
    exit $LASTEXITCODE
}

# Sem elevacao: o instalador roda elevado numa janela propria, e a saida
# dele vai para um arquivo, que e mostrado aqui depois.
$registro = Join-Path $env:TEMP 'clivus-instalar-maquina.txt'
Remove-Item $registro -ErrorAction SilentlyContinue

Write-Host 'Pedindo elevacao ao Windows para instalar em Arquivos de Programas (responda Sim na tela)...' -ForegroundColor Yellow

$comando = "& '$instalador' -Bundle '$bundle' -ParaTodaAMaquina *> '$registro'; exit `$LASTEXITCODE"

try {
    $processo = Start-Process powershell -Verb RunAs -Wait -PassThru -WindowStyle Hidden `
        -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $comando
}
catch {
    Write-Host 'A elevacao foi recusada: nada foi instalado.' -ForegroundColor Red
    exit 1
}

if (Test-Path $registro) { Get-Content $registro | Write-Host }
exit $processo.ExitCode
