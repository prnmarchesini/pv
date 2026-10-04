<#
.SYNOPSIS
    Gera o instalador do Clivus Solar: artefatos\instalador\ClivusSolar-Setup-<versao>.exe
    e o .sha256 ao lado (para a pagina de download publicar).

.DESCRIPTION
    1. Monta o bundle Release (tools\publicar-bundle.ps1, sem instalar).
    2. Compacta o bundle em src\Clivus.Instalador\bundle.zip (fora do git).
    3. Publica o Clivus.Instalador num .exe so (depende do .NET 8 Desktop,
       que o Civil 3D 2026 ja instala).
    4. Grava o SHA-256.

    Assinatura de codigo: quando houver certificado (plano/seguranca.md),
    -Certificado "caminho.pfx" assina o .exe com o signtool do Windows SDK.

.PARAMETER Certificado
    O .pfx de assinatura de codigo (opcional). A senha vem de CLIVUS_PFX_SENHA.
#>
[CmdletBinding()]
param([string] $Certificado)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$versao = ([xml] (Get-Content (Join-Path $raiz 'Directory.Build.props'))).Project.PropertyGroup[0].Version
$bundle = Join-Path $raiz 'artefatos\ClivusSolar.bundle'
$zip = Join-Path $raiz 'src\Clivus.Instalador\bundle.zip'
$saida = Join-Path $raiz 'artefatos\instalador'

Write-Host "Clivus Solar $($versao): montando o bundle..." -ForegroundColor DarkGray
& (Join-Path $PSScriptRoot 'publicar-bundle.ps1') -Configuracao Release | Out-Null
if (-not (Test-Path (Join-Path $bundle 'PackageContents.xml'))) { Write-Host 'O bundle nao foi montado.' -ForegroundColor Red; exit 1 }

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $bundle -DestinationPath $zip

Write-Host 'Publicando o instalador...' -ForegroundColor DarkGray
$publicacao = Join-Path $env:TEMP 'clivus-instalador-publicacao'
if (Test-Path $publicacao) { Remove-Item $publicacao -Recurse -Force }
& dotnet publish (Join-Path $raiz 'src\Clivus.Instalador\Clivus.Instalador.csproj') -c Release -o $publicacao -v quiet --nologo
if ($LASTEXITCODE -ne 0) { Write-Host 'O instalador nao compila.' -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Path $saida -Force | Out-Null
$exe = Join-Path $saida "ClivusSolar-Setup-$versao.exe"
Copy-Item (Join-Path $publicacao 'ClivusSolar-Setup.exe') $exe -Force

if ($Certificado) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'x64' } | Select-Object -Last 1
    if (-not $signtool) { Write-Host 'signtool nao encontrado (Windows SDK).' -ForegroundColor Red; exit 1 }
    & $signtool.FullName sign /f $Certificado /p $env:CLIVUS_PFX_SENHA /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $exe
    if ($LASTEXITCODE -ne 0) { Write-Host 'A assinatura falhou.' -ForegroundColor Red; exit 1 }
}

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$exe.sha256" -Value "$hash  $(Split-Path -Leaf $exe)" -Encoding ascii

$tamanho = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Instalador: $exe ($tamanho MB)" -ForegroundColor Green
Write-Host "SHA-256:    $hash" -ForegroundColor Green
