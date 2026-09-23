<#
.SYNOPSIS
    Testes de nivel 2: o plugin rodando dentro do CAD, sem interface.

.DESCRIPTION
    04-testes.md, nivel 2: o Core Console (accoreconsole.exe) e o AutoCAD sem
    interface, tocado por linha de comando com um script .scr. Ele abre um
    desenho, roda os comandos do plugin, e a saida e comparada com o esperado.
    Roda sem ninguem clicar em nada.

    Casos de hoje:

      1. UFV_OLA responde com a versao certa. Se isso passa, a cadeia inteira
         esta de pe: build, NETLOAD, registro do comando e a mensagem chegando
         ao usuario.
      2. UFV_TERRENO lista as superficies de um desenho de verdade. E a unica
         prova de que a leitura do desenho funciona: nenhum teste de nivel 1
         chega la, porque essa parte e toda API de Civil 3D.

    Sobre o SECURELOAD: o AutoCAD so carrega codigo de caminho confiavel, a
    pasta bin do build nao e uma, e sem interface nao ha como o usuario
    autorizar. Os scripts baixam a guarda pelo tempo do NETLOAD; quem garante
    a devolucao e este runner, num finally, inclusive apagando a entrada nos
    perfis onde ela nao existia antes. Um teste nao pode deixar o AutoCAD do
    Renan menos protegido do que achou.

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

$raiz  = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$saida = Join-Path $raiz 'artefatos\testes'

$limiteEmSegundos = 300
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

function Parar-Com {
    param([string] $Motivo)
    Escrever-Linha 'Nivel 2' '0/?' 'FALHOU'
    Write-Host "  $Motivo" -ForegroundColor Red
    exit 1
}

<#
    Roda um script .scr no Core Console sobre um desenho e devolve a saida.
    Cuida do SECURELOAD e do tempo limite.
#>
function Invoke-CoreConsole {
    param(
        [string] $Desenho,
        [string] $Script,
        [string] $Rotulo
    )

    $scr = Join-Path $saida "nivel2-$Rotulo.scr"
    $out = Join-Path $saida "nivel2-$Rotulo.out"

    # Barra normal: dentro de uma string LISP a barra invertida e escape, e
    # "C:\Dev\..." chegaria ao NETLOAD como "C:Dev...".
    (Get-Content $Script -Raw).Replace('{{DLL}}', $script:dll.Replace('\', '/')) |
        Set-Content $scr -Encoding ascii

    if (Test-Path $out) { Remove-Item $out -Force }

    # Guardamos TODOS os perfis, inclusive os que ainda nao tem o valor. Esse e
    # o caso perigoso: num perfil onde o usuario nunca mexeu em SECURELOAD, o
    # setvar do script CRIA a entrada no registro, e ela ficaria com 0 gravado
    # para sempre. $null aqui significa "nao existia", e o finally apaga.
    $perfisAntes = @{}
    Get-ChildItem 'HKCU:\SOFTWARE\Autodesk\AutoCAD' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -eq 'Variables' } |
        ForEach-Object { $perfisAntes[$_.PSPath] = Ler-SecureLoad $_.PSPath }

    $estourou = $false
    $codigo = $null
    $texto = ''

    # Rodamos o Core Console direto, sem cmd no meio: assim nao ha aspas
    # aninhadas para escapar, e a saida e lida ja decodificada. Ele escreve em
    # UTF-16, que declaramos em StandardOutputEncoding — deixar o padrao
    # entrega texto picotado, com um espaco entre cada letra.
    $inicio = New-Object System.Diagnostics.ProcessStartInfo
    $inicio.FileName               = $script:console
    $inicio.Arguments              = "/i `"$Desenho`" /s `"$scr`""
    $inicio.UseShellExecute        = $false
    $inicio.CreateNoWindow         = $true
    $inicio.RedirectStandardOutput = $true
    $inicio.RedirectStandardError  = $true
    $inicio.StandardOutputEncoding = [System.Text.Encoding]::Unicode
    $inicio.StandardErrorEncoding  = [System.Text.Encoding]::Unicode

    try {
        $processo = [System.Diagnostics.Process]::Start($inicio)

        # Os dois fluxos sao lidos em paralelo: ler um ate o fim e so depois o
        # outro trava se o segundo encher o buffer do pipe.
        $lendoSaida = $processo.StandardOutput.ReadToEndAsync()
        $lendoErro  = $processo.StandardError.ReadToEndAsync()

        if ($processo.WaitForExit($limiteEmSegundos * 1000)) {
            # A sobrecarga com timeout devolve assim que o processo morre, mas
            # o objeto ainda nao teve ExitCode preenchido.
            $processo.WaitForExit()
            $codigo = $processo.ExitCode
            $texto  = $lendoSaida.Result + $lendoErro.Result
        }
        else {
            $processo.Kill()
            $estourou = $true
        }
    }
    finally {
        foreach ($perfil in @($perfisAntes.Keys)) {
            $antes = $perfisAntes[$perfil]
            $agora = Ler-SecureLoad $perfil

            if ($antes -eq $agora) { continue }

            if ($null -eq $antes) {
                Remove-ItemProperty -LiteralPath $perfil -Name 'SecureLoad' -ErrorAction SilentlyContinue
            }
            elseif (Test-Path -LiteralPath $perfil) {
                # -Type DWord porque o valor pode ter sido apagado no meio: sem
                # isso o Set-ItemProperty recriaria como String.
                Set-ItemProperty -LiteralPath $perfil -Name 'SecureLoad' -Value $antes -Type DWord
            }
        }

        # E conferimos o registro, que e onde o estrago ficaria.
        foreach ($perfil in @($perfisAntes.Keys)) {
            if ((Ler-SecureLoad $perfil) -ne $perfisAntes[$perfil]) {
                $problemas.Add("SECURELOAD continua diferente no registro: $perfil")
            }
        }
    }

    Set-Content $out -Value $texto -Encoding UTF8

    return [pscustomobject]@{
        Texto    = $texto
        Codigo   = $codigo
        Estourou = $estourou
        Saida    = $out
    }
}

<#
    Roda um caso e registra o que deu errado. Devolve $true se passou.
#>
function Testar-Caso {
    param(
        [string] $Rotulo,
        [string] $Desenho,
        [string] $Script,
        [string[]] $Esperados
    )

    $r = Invoke-CoreConsole -Desenho $Desenho -Script $Script -Rotulo $Rotulo

    if ($r.Estourou) {
        $problemas.Add("$Rotulo passou de $limiteEmSegundos s e foi encerrado. Veja $($r.Saida)")
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($r.Texto)) {
        $problemas.Add("$Rotulo nao produziu saida (codigo $($r.Codigo)).")
        return $false
    }

    if ($r.Codigo -ne 0) {
        $problemas.Add("$Rotulo terminou com codigo $($r.Codigo). Veja $($r.Saida)")
        return $false
    }

    foreach ($esperado in $Esperados) {
        if ($r.Texto -notmatch $esperado) {
            $problemas.Add("$Rotulo nao traz /$esperado/. Veja $($r.Saida)")
            return $false
        }
    }

    # O script LISP tambem devolve o SECURELOAD, como segunda linha de defesa.
    if ($r.Texto -match 'UFV_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
        if ($Matches[1] -ne $Matches[2]) {
            $problemas.Add("$Rotulo nao restaurou o SECURELOAD: entrou $($Matches[1]), saiu $($Matches[2]).")
            return $false
        }
    }
    else {
        $problemas.Add("$Rotulo nao informa o SECURELOAD antes e depois. Veja $($r.Saida)")
        return $false
    }

    return $true
}

# ---- o que precisamos ter a mao --------------------------------------------

New-Item -ItemType Directory -Path $saida -Force | Out-Null

$script:console = Join-Path $Civil3DPath 'accoreconsole.exe'
if (-not (Test-Path $script:console)) {
    Parar-Com "accoreconsole.exe nao encontrado em $Civil3DPath. Passe -Civil3DPath."
}

$script:dll = Join-Path $raiz 'src\UFV.Plugin\bin\Debug\UFV.Plugin.dll'
if (-not (Test-Path $script:dll)) {
    Parar-Com "UFV.Plugin.dll nao encontrada em $script:dll. Compile antes (tools\rodar-testes.ps1 ja faz isso)."
}

# Desenho vazio para o caso de fumaca: nao ha terreno nem area ainda.
$desenhoVazio = Join-Path $raiz 'tests\acervo\etapa-0\vazio.dwg'
if (-not (Test-Path $desenhoVazio)) {
    $candidatos = @(
        (Join-Path $Civil3DPath 'UserDataCache\Template\map2d.dwt'),
        (Join-Path $Civil3DPath 'UserDataCache\Template\map3d.dwt'),
        (Join-Path $Civil3DPath 'C3D\UserDataCache\Template\_Autodesk Civil 3D (Metric) NCS.dwt')
    )
    $desenhoVazio = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $desenhoVazio) {
        Parar-Com 'Nenhum desenho de partida encontrado (nem no acervo, nem nos templates do Civil 3D).'
    }
}

# Desenho com superficie, para o caso do terreno. Quando o acervo da etapa 1
# tiver o desenho congelado, ele manda; ate la, o material de trabalho serve.
$desenhoComTerreno = Join-Path $raiz 'tests\acervo\etapa-1\terreno.dwg'
$doAcervo = Test-Path $desenhoComTerreno

if (-not $doAcervo) {
    $desenhoComTerreno = Join-Path $raiz '0 - Assets\Curvas Itatiba.dwg'
}

# ---- a mensagem esperada do UFV_OLA ----------------------------------------
# Vem da mesma fonte que o build usa, para o teste nao passar a mentir quando
# a versao subir.

$props = [xml] (Get-Content (Join-Path $raiz 'Directory.Build.props'))
$no    = $props.SelectSingleNode('//Version')
$versao = if ($no) { $no.InnerText.Trim() } else { '' }

if (-not $versao) { Parar-Com 'Nao consegui ler <Version> de Directory.Build.props.' }

<#
    O caso do terreno, conferido de verdade.

    Procurar solto por "\d+ pontos" na saida inteira do Core Console nao prova
    nada: o banner, o eco dos comandos e as proprias mensagens do Civil 3D
    passam pelo mesmo padrao. O que se confere aqui e a linha no formato que o
    Describe() produz, ancorada, e que a quantidade de linhas impressas bate
    com o total que o comando anunciou.
#>
function Testar-CasoDoTerreno {
    $r = Invoke-CoreConsole -Desenho $desenhoComTerreno `
                            -Script (Join-Path $PSScriptRoot 'ufv-terreno.scr') `
                            -Rotulo 'ufv-terreno'

    if ($r.Estourou) {
        $problemas.Add("ufv-terreno passou de $limiteEmSegundos s. Veja $($r.Saida)")
        return $false
    }

    if ($r.Codigo -ne 0) {
        $problemas.Add("ufv-terreno terminou com codigo $($r.Codigo). Veja $($r.Saida)")
        return $false
    }

    $linhas = $r.Texto -split "`r?`n"

    $cabecalho = $linhas | Where-Object { $_ -match 'Superfícies do desenho: (\d+)' } | Select-Object -First 1
    if (-not $cabecalho) {
        $problemas.Add("ufv-terreno nao anunciou quantas superficies achou. Veja $($r.Saida)")
        return $false
    }

    $null = $cabecalho -match 'Superfícies do desenho: (\d+)'
    $anunciadas = [int] $Matches[1]

    if ($anunciadas -lt 1) {
        $problemas.Add("ufv-terreno nao achou superficie no desenho. Veja $($r.Saida)")
        return $false
    }

    # A linha de item, como Describe() a escreve: dois espacos, o nome, um
    # travessao e a contagem com separador de milhar.
    $itens = @($linhas | Where-Object { $_ -match '^\s{2}\S.* — [\d.]+ pontos?\s*$' })

    if ($itens.Count -ne $anunciadas) {
        $problemas.Add(
            "ufv-terreno anunciou $anunciadas superficie(s) mas imprimiu $($itens.Count) linha(s). " +
            "Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -match 'UFV_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
        if ($Matches[1] -ne $Matches[2]) {
            $problemas.Add("ufv-terreno nao restaurou o SECURELOAD.")
            return $false
        }
    }
    else {
        $problemas.Add("ufv-terreno nao informa o SECURELOAD antes e depois. Veja $($r.Saida)")
        return $false
    }

    return $true
}

# ---- os casos --------------------------------------------------------------

$passaram = 0
$total = 0

$total++
if (Testar-Caso -Rotulo 'ufv-ola' -Desenho $desenhoVazio -Script (Join-Path $PSScriptRoot 'ufv-ola.scr') `
                -Esperados @([regex]::Escape("Plugin UFV carregado, versão $versao"))) {
    $passaram++
}

# O caso do terreno conta no total SEMPRE. Antes ele era simplesmente pulado
# quando o desenho nao estava la, e o placar saia "1/1 OK", verde, afirmando
# que tudo passou enquanto o unico teste que prova a leitura do desenho nao
# tinha rodado. Teste que some em silencio e pior que teste que falha.
$total++

if (-not (Test-Path $desenhoComTerreno)) {
    $problemas.Add(
        'ufv-terreno nao rodou: falta um desenho com superficie. Congele um em ' +
        'tests\acervo\etapa-1\terreno.dwg (ver plano\04-testes.md).')
}
else {
    if (-not $doAcervo) {
        Write-Host "  (terreno fora do acervo; usando $desenhoComTerreno)" -ForegroundColor DarkGray
    }

    if (Testar-CasoDoTerreno) { $passaram++ }
}

# ---- veredito --------------------------------------------------------------

if ($problemas.Count -eq 0 -and $passaram -eq $total) {
    Escrever-Linha 'Nivel 2' "$passaram/$total" 'OK'
    exit 0
}

Escrever-Linha 'Nivel 2' "$passaram/$total" 'FALHOU'
foreach ($p in $problemas) { Write-Host "  $p" -ForegroundColor Red }
exit 1
