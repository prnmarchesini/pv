<#
.SYNOPSIS
    Instala o Plugin UFV, conferindo antes se a versao do Civil 3D serve.

.DESCRIPTION
    Passo 0.6 de etapa-0-fundacao.md: detecta a versao do Civil 3D instalada,
    avisa se nao e compativel, e copia o bundle.

    A versao suportada nao esta escrita aqui: sai do PackageContents.xml, dos
    atributos SeriesMin e SeriesMax. E a mesma declaracao que o AutoCAD le para
    decidir se carrega o plugin, entao instalador e bundle nunca discordam.

    O que e conferido, nesta ordem:

      1. Existe AutoCAD instalado?
      2. A serie esta entre SeriesMin e SeriesMax? (R25.1 = AutoCAD 2026)
      3. E Civil 3D, e nao AutoCAD puro? O plugin usa a superficie TIN, que so
         existe no Civil 3D; sem AeccDbMgd.dll ele carrega e quebra no
         primeiro comando util.
      4. O AutoCAD esta fechado? Aberto, ele segura as DLLs.

    Estar em ApplicationPlugins faz o Civil 3D ACHAR o plugin, nao confiar
    nele: a DLL nao e assinada e a pasta do usuario e gravavel, entao toda
    abertura para num aviso. -ParaTodaAMaquina resolve (ver o parametro).

    Codigos de saida, para o instalador poder ser chamado por outro script:
      0  instalado (ou compativel, com -SomenteVerificar)
      1  erro de uso ou falha ao copiar
      2  versao incompativel
      3  AutoCAD sem Civil 3D
      4  nenhum AutoCAD encontrado
      5  AutoCAD aberto
      6  serie instalada em formato desconhecido

.PARAMETER SomenteVerificar
    So diz se serve; nao copia nada.

.PARAMETER Desinstalar
    Remove o bundle instalado e sai.

.PARAMETER ParaTodaAMaquina
    Instala em %PROGRAMFILES%\Autodesk\ApplicationPlugins, para todos os
    usuarios. Precisa de elevacao. E o unico jeito, sem assinar a DLL, de o
    Civil 3D carregar o plugin sem avisar que ele nao e confiavel: a pasta do
    usuario e gravavel, e o AutoCAD nao confia em pasta gravavel.

.PARAMETER Bundle
    Pasta UFV.bundle a instalar. O padrao e artefatos\UFV.bundle, que
    publicar-bundle.ps1 monta.

.PARAMETER SimularSerie
    Para os testes: finge que a serie instalada e esta, sem olhar o registro.

.PARAMETER SimularCivil3D
    Para os testes: finge que o Civil 3D esta (ou nao esta) presente.

.EXAMPLE
    .\tools\instalar.ps1
    .\tools\instalar.ps1 -ParaTodaAMaquina
    .\tools\instalar.ps1 -SomenteVerificar
    .\tools\instalar.ps1 -Desinstalar
#>
[CmdletBinding()]
param(
    [switch] $SomenteVerificar,
    [switch] $Desinstalar,
    [switch] $ParaTodaAMaquina,
    [string] $Bundle,
    [string] $SimularSerie,

    # String, e nao switch nem bool: chamado com -File, o PowerShell entrega
    # tudo como texto, e [bool] "0" e $true porque a string nao esta vazia.
    [ValidateSet('0', '1')]
    [string] $SimularCivil3D = '1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz    = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$destinoDoUsuario = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\UFV.bundle'
$destinoDaMaquina = Join-Path $env:ProgramFiles 'Autodesk\ApplicationPlugins\UFV.bundle'
$destino = if ($ParaTodaAMaquina) { $destinoDaMaquina } else { $destinoDoUsuario }

if (-not $Bundle) { $Bundle = Join-Path $raiz 'artefatos\UFV.bundle' }

function Dizer {
    param([string] $Texto, [string] $Cor = 'Gray')
    Write-Host $Texto -ForegroundColor $Cor
}

# ---- desinstalar -----------------------------------------------------------

if ($Desinstalar) {
    # Tira dos dois lugares: quem instalou para a maquina toda e depois so para
    # o usuario ficaria com duas copias, e o AutoCAD carregaria a errada.
    $removidos = 0
    foreach ($alvo in $destinoDaMaquina, $destinoDoUsuario) {
        if (-not (Test-Path $alvo)) { continue }
        try {
            Remove-Item $alvo -Recurse -Force
            Dizer "Plugin UFV removido de $alvo" 'Green'
            $removidos++
        }
        catch {
            Dizer "Sem permissao para remover $alvo. Rode como administrador." 'Red'
            exit 1
        }
    }
    if ($removidos -eq 0) { Dizer 'O Plugin UFV nao esta instalado.' 'DarkGray' }
    exit 0
}

# ---- de onde vem a serie suportada -----------------------------------------
# Do bundle, se ele existir; senao do PackageContents.xml do repositorio, para
# o -SomenteVerificar funcionar sem ter montado o bundle antes.

$descritor = Join-Path $Bundle 'PackageContents.xml'
if (-not (Test-Path $descritor)) {
    $descritor = Join-Path $raiz 'src\UFV.Plugin\PackageContents.xml'
}
if (-not (Test-Path $descritor)) {
    Dizer "PackageContents.xml nao encontrado. Rode tools\publicar-bundle.ps1 antes." 'Red'
    exit 1
}

$pacote     = [xml] (Get-Content $descritor)
$requisitos = $pacote.SelectSingleNode('//RuntimeRequirements')

# Sem este guarda antes do GetAttribute, um XML sem RuntimeRequirements
# derrubaria o script com erro terminante e a mensagem abaixo nunca sairia.
if ($null -eq $requisitos) {
    Dizer "PackageContents.xml nao tem RuntimeRequirements: $descritor" 'Red'
    exit 1
}

$serieMin  = $requisitos.GetAttribute('SeriesMin')
$serieMax  = $requisitos.GetAttribute('SeriesMax')
$versaoApp = $pacote.DocumentElement.GetAttribute('AppVersion')

if (-not $serieMin -or -not $serieMax) {
    Dizer "PackageContents.xml nao declara SeriesMin/SeriesMax." 'Red'
    exit 1
}

<#
    "R25.1" vira 25.1 para poder comparar. Serie fora do formato vira $null, e
    quem chama trata como desconhecida.
#>
function Converter-Serie {
    param([string] $Serie)

    if ($Serie -match '^[Rr](\d+)(?:\.(\d+))?$') {
        $menor = if ($Matches.Count -gt 2 -and $Matches[2]) { [int] $Matches[2] } else { 0 }
        return [version]::new([int] $Matches[1], $menor)
    }
    return $null
}

$minimo = Converter-Serie $serieMin
$maximo = Converter-Serie $serieMax

if (-not $minimo -or -not $maximo) {
    Dizer "SeriesMin/SeriesMax fora do formato esperado ('$serieMin', '$serieMax')." 'Red'
    exit 1
}

# ---- o que esta instalado nesta maquina ------------------------------------

Dizer "Plugin UFV $versaoApp" 'Cyan'
Dizer "Serie suportada: $serieMin$(if ($serieMin -ne $serieMax) { " a $serieMax" })" 'DarkGray'
Dizer ''

$instalacoes = @()

if ($PSBoundParameters.ContainsKey('SimularSerie')) {
    # Caminho de teste: nao olha o registro.
    if (-not $SimularSerie) {
        Dizer '-SimularSerie foi passado vazio.' 'Red'
        exit 1
    }
    $temCivil = ($SimularCivil3D -eq '1')
    $instalacoes += [pscustomobject]@{
        Serie    = $SimularSerie
        Caminho  = '(simulado)'
        TemCivil = $temCivil
    }
}
else {
    $chaveRaiz = 'HKLM:\SOFTWARE\Autodesk\AutoCAD'
    if (Test-Path $chaveRaiz) {
        foreach ($serie in Get-ChildItem $chaveRaiz -ErrorAction SilentlyContinue) {
            foreach ($produto in Get-ChildItem $serie.PSPath -ErrorAction SilentlyContinue) {
                $dados = Get-ItemProperty $produto.PSPath -ErrorAction SilentlyContinue
                if ($null -eq $dados) { continue }
                if (-not ($dados.PSObject.Properties.Name -contains 'AcadLocation')) { continue }

                $caminho = $dados.AcadLocation
                if (-not $caminho -or -not (Test-Path $caminho)) { continue }

                $instalacoes += [pscustomobject]@{
                    Serie    = $serie.PSChildName
                    Caminho  = $caminho
                    TemCivil = (Test-Path (Join-Path $caminho 'C3D\AeccDbMgd.dll'))
                }
            }
        }
    }
}

# Uma mesma instalacao aparece em mais de uma chave de produto.
$instalacoes = @($instalacoes | Sort-Object Serie, Caminho -Unique)

if ($instalacoes.Count -eq 0) {
    Dizer 'Nenhum AutoCAD encontrado nesta maquina.' 'Red'
    Dizer "O Plugin UFV precisa do Civil 3D (serie $serieMin)." 'Red'
    exit 4
}

foreach ($i in $instalacoes) {
    $rotulo = if ($i.TemCivil) { 'Civil 3D' } else { 'AutoCAD (sem Civil 3D)' }
    Dizer "  encontrado: $($i.Serie)  $rotulo  $($i.Caminho)" 'DarkGray'
}
Dizer ''

# ---- serve? ----------------------------------------------------------------

# Serie que nao casa com o formato "Rnn.n" nao e "versao errada": e versao que
# nao sabemos ler. Dizer "incompativel" ali mentiria sobre a causa e mandaria o
# usuario procurar a versao certa quando o problema e outro.
$ilegiveis = @($instalacoes | Where-Object { -not (Converter-Serie $_.Serie) })

$naSerie = @($instalacoes | Where-Object {
    $v = Converter-Serie $_.Serie
    $v -and $v -ge $minimo -and $v -le $maximo
})

if ($naSerie.Count -eq 0 -and $ilegiveis.Count -eq $instalacoes.Count) {
    $lidas = ($ilegiveis | ForEach-Object { "'$($_.Serie)'" }) -join ', '
    Dizer 'Versao instalada em formato desconhecido.' 'Red'
    Dizer "  li: $lidas" 'Red'
    Dizer "  esperava algo como $serieMin" 'Red'
    exit 6
}

if ($naSerie.Count -eq 0) {
    $encontradas = ($instalacoes | ForEach-Object { $_.Serie }) -join ', '
    Dizer "Versao incompativel." 'Red'
    Dizer "  esta maquina tem: $encontradas" 'Red'
    Dizer "  o plugin exige:   $serieMin$(if ($serieMin -ne $serieMax) { " a $serieMax" })" 'Red'
    Dizer ''
    Dizer 'Instalar assim mesmo nao ajudaria: o AutoCAD recusa o bundle pela mesma regra.' 'DarkGray'
    exit 2
}

$comCivil = @($naSerie | Where-Object { $_.TemCivil })

if ($comCivil.Count -eq 0) {
    Dizer 'A versao serve, mas e AutoCAD puro: falta o Civil 3D.' 'Red'
    Dizer 'O plugin le a superficie TIN, que so existe no Civil 3D.' 'Red'
    exit 3
}

$escolhida = $comCivil | Select-Object -First 1
Dizer "Compativel: $($escolhida.Serie) Civil 3D." 'Green'

if ($SomenteVerificar) { exit 0 }

# ---- instalar --------------------------------------------------------------

if (-not (Test-Path (Join-Path $Bundle 'PackageContents.xml'))) {
    Dizer "Bundle nao encontrado em $Bundle. Rode tools\publicar-bundle.ps1 antes." 'Red'
    exit 1
}

# O accoreconsole tambem segura as DLLs, e ele roda nos testes de nivel 2.
$segurando = @(
    Get-Process -Name 'acad', 'accoreconsole' -ErrorAction SilentlyContinue
)

if ($segurando.Count -gt 0) {
    $quais = ($segurando | ForEach-Object { $_.ProcessName } | Sort-Object -Unique) -join ', '
    Dizer "Feche antes de instalar: $quais esta aberto e segura as DLLs." 'Red'
    exit 5
}

# Escrever em Program Files exige elevacao. Melhor dizer isso antes de copiar
# pela metade e deixar um bundle quebrado no caminho.
if ($ParaTodaAMaquina) {
    $eu = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $eu.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Dizer 'Instalar para toda a maquina precisa de elevacao.' 'Red'
        Dizer 'Abra um PowerShell como administrador e rode de novo, ou use:' 'Red'
        Dizer "  Start-Process powershell -Verb RunAs -ArgumentList '-File','$PSCommandPath','-ParaTodaAMaquina'" 'DarkGray'
        exit 1
    }

    # Duas copias fazem o AutoCAD carregar a que achar primeiro, que nao e
    # necessariamente a que acabou de ser instalada.
    if (Test-Path $destinoDoUsuario) {
        Remove-Item $destinoDoUsuario -Recurse -Force
        Dizer "Removida a instalacao anterior do usuario em $destinoDoUsuario" 'DarkGray'
    }
}

try {
    if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destino) -Force | Out-Null
    Copy-Item $Bundle $destino -Recurse
}
catch {
    Dizer "Falha ao copiar para $destino : $($_.Exception.Message)" 'Red'
    exit 1
}

Dizer ''
Dizer "Instalado em $destino" 'Green'

# ---- por que a pasta importa -----------------------------------------------
#
# Estar em ApplicationPlugins faz o Civil 3D ACHAR o plugin, nao confiar nele.
# Como UFV.Plugin.dll nao e assinada, o AutoCAD so a carrega sem perguntar se
# ela estiver numa pasta que ele considere segura. "Segura" quer dizer
# protegida por permissao: uma pasta que o proprio usuario pode escrever nao
# vale, e %APPDATA% e uma dessas.
#
# Conferido na tela, no Civil 3D 2026: instalado em %APPDATA%, toda abertura
# para no aviso "Unsigned Executable File". Acrescentar a pasta ao
# TRUSTEDPATHS nao resolve - o AutoCAD despreza caminho gravavel pelo usuario.
#
# Sobram dois caminhos de verdade:
#
#   1. Instalar em %PROGRAMFILES%\Autodesk\ApplicationPlugins, que e protegido
#      por permissao. Precisa de elevacao, e e o que -ParaTodaAMaquina faz.
#   2. Assinar a DLL com certificado confiado pela maquina. Fica para quando
#      houver certificado; e a resposta certa para distribuir a terceiros.
#
# Sem um dos dois, o usuario aperta "Always Load" uma vez e o AutoCAD passa a
# confiar naquela DLL especifica. Funciona, mas se perde a cada nova versao.

if ($ParaTodaAMaquina) {
    Dizer ''
    Dizer 'Instalado numa pasta protegida: o Civil 3D carrega sem perguntar.' 'Green'
} else {
    Dizer ''
    Dizer 'Atenção: esta pasta é gravável pelo usuário, então o Civil 3D não a' 'Yellow'
    Dizer 'considera segura. Toda abertura vai avisar que UFV.Plugin.dll não é' 'Yellow'
    Dizer 'assinada e perguntar o que fazer.' 'Yellow'
    Dizer ''
    Dizer 'Para resolver, uma das duas:' 'Yellow'
    Dizer '  - aperte "Always Load" no aviso (vale para esta versão da DLL); ou' 'Yellow'
    Dizer '  - reinstale com  .\tools\instalar.ps1 -ParaTodaAMaquina  (pede elevação).' 'Yellow'
}

Dizer ''
Dizer 'Abra o Civil 3D: a aba UFV aparece sem NETLOAD.' 'DarkGray'
exit 0
