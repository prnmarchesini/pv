<#
.SYNOPSIS
    Testes de nivel 2: o plugin rodando dentro do CAD, sem interface.

.DESCRIPTION
    04-testes.md, nivel 2: o Core Console (accoreconsole.exe) e o AutoCAD sem
    interface, tocado por linha de comando com um script .scr. Ele abre um
    desenho, roda os comandos do plugin, e a saida e comparada com o esperado.
    Roda sem ninguem clicar em nada.

    Na etapa 0 ha um unico teste, de fumaca: carregar o plugin e ver o UFV_OLA
    responder com a versao certa. Se isso passa, a cadeia inteira esta de pe -
    build, NETLOAD, registro do comando e a mensagem chegando ao usuario.

    Chamado por tools\rodar-testes.ps1, que espera a linha do placar e o
    codigo de saida (0 = verde).

.PARAMETER Civil3DPath
    Raiz da instalacao. O padrao e o mesmo de Directory.Build.props.
#>
[CmdletBinding()]
param(
    [string] $Civil3DPath = 'C:\Program Files\Autodesk\AutoCAD 2026\'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$saida   = Join-Path $raiz 'artefatos\testes'
$modelo  = Join-Path $PSScriptRoot 'ufv-ola.scr'

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
    Le SECURELOAD de um perfil do registro, ou $null se a chave sumiu ou nao
    tem o valor. O AutoCAD cria e remove perfis por conta propria, entao
    acessar a propriedade direto quebra com Set-StrictMode.
#>
function Ler-SecureLoad {
    param([string] $Perfil)

    $item = Get-ItemProperty -LiteralPath $Perfil -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    if (-not ($item.PSObject.Properties.Name -contains 'SecureLoad')) { return $null }
    return $item.SecureLoad
}

function Falhar {
    param([string] $Motivo)
    Escrever-Linha 'Nivel 2' '0/1' 'FALHOU'
    Write-Host "  $Motivo" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Path $saida -Force | Out-Null

# ---- o que precisamos ter a mao --------------------------------------------

$console = Join-Path $Civil3DPath 'accoreconsole.exe'
if (-not (Test-Path $console)) {
    Falhar "accoreconsole.exe nao encontrado em $Civil3DPath. Passe -Civil3DPath."
}

$dll = Join-Path $raiz 'src\UFV.Plugin\bin\Debug\UFV.Plugin.dll'
if (-not (Test-Path $dll)) {
    Falhar "UFV.Plugin.dll nao encontrada em $dll. Compile antes (tools\rodar-testes.ps1 ja faz isso)."
}

# O desenho de referencia da etapa 0 e so um lugar para o comando rodar: nao ha
# terreno nem area ainda. Quando o acervo tiver o desenho da etapa 1, ele passa
# a mandar aqui.
$desenho = Join-Path $raiz 'tests\acervo\etapa-0\vazio.dwg'
if (-not (Test-Path $desenho)) {
    $candidatos = @(
        (Join-Path $Civil3DPath 'UserDataCache\Template\map2d.dwt'),
        (Join-Path $Civil3DPath 'UserDataCache\Template\map3d.dwt'),
        (Join-Path $Civil3DPath 'C3D\UserDataCache\Template\_Autodesk Civil 3D (Metric) NCS.dwt')
    )
    $desenho = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $desenho) {
        Falhar 'Nenhum desenho de partida encontrado (nem no acervo, nem nos templates do Civil 3D).'
    }
}

# ---- a mensagem esperada ---------------------------------------------------
# Vem da mesma fonte que o build usa, para o teste nao passar a mentir quando
# a versao subir.

$props = [xml] (Get-Content (Join-Path $raiz 'Directory.Build.props'))
$no    = $props.SelectSingleNode('//Version')
$versao = if ($no) { $no.InnerText.Trim() } else { '' }

if (-not $versao) { Falhar 'Nao consegui ler <Version> de Directory.Build.props.' }

$esperado = "Plugin UFV carregado, versão $versao"

# ---- montar e rodar o script -----------------------------------------------

$scr = Join-Path $saida 'nivel2-ufv-ola.scr'
$out = Join-Path $saida 'nivel2-ufv-ola.out'

# Barra normal: dentro de uma string LISP a barra invertida e escape, e
# "C:\Dev\..." chegaria ao NETLOAD como "C:Dev...". O AutoCAD aceita as duas.
(Get-Content $modelo -Raw).Replace('{{DLL}}', $dll.Replace('\', '/')) |
    Set-Content $scr -Encoding ascii

if (Test-Path $out) { Remove-Item $out -Force }

# SECURELOAD e salvo no perfil do usuario, e o Core Console grava o valor no
# registro assim que ele muda: devolver a variavel em LISP no fim do script nao
# desfaz a gravacao. Entao guardamos os valores aqui e os devolvemos depois,
# aconteca o que acontecer com o processo do CAD. Um teste nao pode deixar o
# AutoCAD do Renan menos protegido do que achou.
$perfisAntes = @{}
Get-ChildItem 'HKCU:\SOFTWARE\Autodesk\AutoCAD' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Property -contains 'SecureLoad' } |
    ForEach-Object {
        $valor = Ler-SecureLoad $_.PSPath
        if ($null -ne $valor) { $perfisAntes[$_.PSPath] = $valor }
    }

# O accoreconsole escreve em UTF-16. Redirecionar pelo cmd preserva os bytes;
# o redirecionamento do PowerShell 5.1 os estraga.
try {
    cmd /c "`"$console`" /i `"$desenho`" /s `"$scr`" > `"$out`" 2>&1" | Out-Null
    $codigo = $LASTEXITCODE
}
finally {
    foreach ($perfil in @($perfisAntes.Keys)) {
        $agora = Ler-SecureLoad $perfil
        if ($null -ne $agora -and $agora -ne $perfisAntes[$perfil]) {
            Set-ItemProperty -LiteralPath $perfil -Name 'SecureLoad' -Value $perfisAntes[$perfil]
        }
    }
}

if (-not (Test-Path $out)) {
    Falhar "O Core Console nao produziu saida (codigo $codigo)."
}

$texto = [IO.File]::ReadAllText($out, [Text.Encoding]::Unicode)

if ($codigo -ne 0) {
    Falhar "Core Console terminou com codigo $codigo. Saida em $out"
}

if ($texto -notmatch [regex]::Escape($esperado)) {
    Falhar "A saida nao traz `"$esperado`". Veja $out"
}

# O script LISP tambem devolve o SECURELOAD, como segunda linha de defesa.
if ($texto -match 'UFV_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
    if ($Matches[1] -ne $Matches[2]) {
        Falhar "SECURELOAD nao foi restaurado: entrou $($Matches[1]), saiu $($Matches[2])."
    }
} else {
    Falhar "A saida nao informa o SECURELOAD antes e depois. Veja $out"
}

# E conferimos o registro, que e onde o estrago ficaria.
$mudados = @($perfisAntes.Keys) | Where-Object {
    $agora = Ler-SecureLoad $_
    ($null -ne $agora) -and ($agora -ne $perfisAntes[$_])
}

if ($mudados) {
    Falhar "SECURELOAD continua diferente no registro depois do teste: $($mudados -join ', ')"
}

Escrever-Linha 'Nivel 2' '1/1' 'OK'
exit 0
