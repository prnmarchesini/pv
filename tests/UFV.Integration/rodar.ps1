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
        [string] $Rotulo,
        [hashtable] $Substituicoes = @{}
    )

    $scr = Join-Path $saida "nivel2-$Rotulo.scr"
    $out = Join-Path $saida "nivel2-$Rotulo.out"

    # Barra normal: dentro de uma string LISP a barra invertida e escape, e
    # "C:\Dev\..." chegaria ao NETLOAD como "C:Dev...".
    $texto = (Get-Content $Script -Raw).Replace('{{DLL}}', $script:dll.Replace('\', '/'))

    foreach ($marcador in $Substituicoes.Keys) {
        $texto = $texto.Replace($marcador, ([string]$Substituicoes[$marcador]).Replace('\', '/'))
    }

    $texto | Set-Content $scr -Encoding ascii

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

# Desenhos com superficie, para o caso do terreno: TODOS os .dwg do acervo.
# Nao um caminho fixo — assim, congelar mais um desenho e so copiar o arquivo
# e declarar o hash no manifesto; o teste passa a rodar contra ele sozinho.
$pastaDoAcervo = Join-Path $raiz 'tests\acervo'
$desenhos = @(
    Get-ChildItem $pastaDoAcervo -Recurse -Filter '*.dwg' -File -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
)

$doAcervo = $desenhos.Count -gt 0

if (-not $doAcervo) {
    # Material de trabalho, enquanto o acervo nao tiver nada congelado.
    $solto = Join-Path $raiz '0 - Assets\Curvas Itatiba.dwg'
    if (Test-Path $solto) { $desenhos = @($solto) }
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
    param([string] $Desenho, [string] $Rotulo)

    $nomeDoDesenho = [IO.Path]::GetFileNameWithoutExtension($Desenho)
    $esperado = $esperados[$nomeDoDesenho]

    $r = Invoke-CoreConsole -Desenho $Desenho `
                            -Script (Join-Path $PSScriptRoot 'ufv-terreno.scr') `
                            -Rotulo $Rotulo

    if ($r.Estourou) {
        $problemas.Add("$Rotulo passou de $limiteEmSegundos s. Veja $($r.Saida)")
        return $false
    }

    if ($r.Codigo -ne 0) {
        $problemas.Add("$Rotulo terminou com codigo $($r.Codigo). Veja $($r.Saida)")
        return $false
    }

    $linhas = $r.Texto -split "`r?`n"

    $cabecalho = $linhas | Where-Object { $_ -match 'Superfícies do desenho: (\d+)' } | Select-Object -First 1
    if (-not $cabecalho) {
        $problemas.Add("$Rotulo nao anunciou quantas superficies achou. Veja $($r.Saida)")
        return $false
    }

    $null = $cabecalho -match 'Superfícies do desenho: (\d+)'
    $anunciadas = [int] $Matches[1]

    if ($anunciadas -lt 1) {
        $problemas.Add("$Rotulo nao achou superficie no desenho. Veja $($r.Saida)")
        return $false
    }

    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'superficies no desenho' `
                            -Obtido $anunciadas -Esperado $esperado.Superficies

    # A linha de item, como Describe() a escreve: dois espacos, o nome, um
    # travessao e a contagem com separador de milhar.
    $itens = @($linhas | Where-Object { $_ -match '^\s{2}\S.* — [\d.]+ pontos?\s*$' })

    if ($itens.Count -ne $anunciadas) {
        $problemas.Add(
            "ufv-terreno anunciou $anunciadas superficie(s) mas imprimiu $($itens.Count) linha(s). " +
            "Veja $($r.Saida)")
        return $false
    }

    # O processamento do passo 1.4: o resumo tem que sair, e os numeros tem
    # que ser coerentes entre si. Sem isto, o miolo do 1.4 — ler a superficie
    # e montar a malha — nao teria teste automatico nenhum, porque ele so
    # existe do lado do CAD.
    if ($r.Texto -notmatch 'Terreno processado: \S') {
        $problemas.Add("$Rotulo nao processou a superficie. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'triângulos:\s+([\d.]+)') {
        $problemas.Add("$Rotulo nao informou a quantidade de triangulos. Veja $($r.Saida)")
        return $false
    }

    $triangulos = [int] ($Matches[1] -replace '\.', '')
    if ($triangulos -lt 1) {
        $problemas.Add("$Rotulo processou a superficie e achou $triangulos triangulos. Veja $($r.Saida)")
        return $false
    }

    # Contagem de triangulo e exata: o Civil 3D diz um numero inteiro, e o
    # motor tem que chegar no mesmo.
    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'triangulos' `
                            -Obtido $triangulos -Esperado $esperado.Triangulos

    if ($esperado -and $esperado.Superficie) {
        if ($r.Texto -notmatch ('Terreno processado: ' + [regex]::Escape($esperado.Superficie))) {
            $problemas.Add(
                "$Rotulo processou outra superficie; esperava '$($esperado.Superficie)'. Veja $($r.Saida)")
            return $false
        }
    }

    if ($r.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("$Rotulo nao informou as cotas. Veja $($r.Saida)")
        return $false
    }

    $cotaMinima = [double]::Parse($Matches[1], [Globalization.CultureInfo]::GetCultureInfo('pt-BR'))
    $cotaMaxima = [double]::Parse($Matches[2], [Globalization.CultureInfo]::GetCultureInfo('pt-BR'))

    if ($cotaMaxima -lt $cotaMinima) {
        $problemas.Add("$Rotulo devolveu cota maxima ($cotaMaxima) menor que a minima ($cotaMinima).")
        return $false
    }

    # 1 cm, que e a ultima casa que o Civil 3D mostra nas propriedades.
    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'cota minima' `
                            -Obtido $cotaMinima -Esperado $esperado.CotaMinima -Tolerancia 0.01
    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'cota maxima' `
                            -Obtido $cotaMaxima -Esperado $esperado.CotaMaxima -Tolerancia 0.01

    if ($r.Texto -notmatch 'área em planta:\s+([\d.,]+) m²') {
        $problemas.Add("$Rotulo nao informou a area. Veja $($r.Saida)")
        return $false
    }

    $areaEmPlanta = [double]::Parse($Matches[1], [Globalization.CultureInfo]::GetCultureInfo('pt-BR'))
    if ($areaEmPlanta -le 0) {
        $problemas.Add("$Rotulo devolveu area em planta de $areaEmPlanta. Veja $($r.Saida)")
        return $false
    }

    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'area em planta' `
                            -Obtido $areaEmPlanta -Esperado $esperado.AreaEmPlanta -Tolerancia 0.01

    if ($r.Texto -notmatch 'área do terreno:\s+([\d.,]+) m²') {
        $problemas.Add("$Rotulo nao informou a area do terreno. Veja $($r.Saida)")
        return $false
    }

    $areaDoTerreno = [double]::Parse($Matches[1], [Globalization.CultureInfo]::GetCultureInfo('pt-BR'))

    # A area no espaco nunca e menor que a projetada: terreno inclinado tem
    # mais chao do que aparece no mapa, e plano tem o mesmo. Menor seria erro
    # de conta, e e o tipo de erro que passa despercebido porque o numero
    # continua parecendo razoavel.
    $null = Conferir-Numero -Rotulo $Rotulo -Nome 'area do terreno' `
                            -Obtido $areaDoTerreno -Esperado $esperado.AreaDoTerreno -Tolerancia 0.01

    if ($areaDoTerreno -lt ($areaEmPlanta - 0.01)) {
        $problemas.Add(
            "$Rotulo devolveu area do terreno ($areaDoTerreno) menor que a projetada ($areaEmPlanta).")
        return $false
    }

    # ---- a localizacao geografica (passo 1.6) ------------------------------
    #
    # Nao basta sair um par de numeros: ele precisa cair PERTO do terreno.
    # Num desenho real, a primeira versao lia o ponto de referencia da
    # geolocalizacao — um ponto de calibracao arbitrario, a mil quilometros da
    # usina — e anunciava 14 graus sul para um terreno a 23 graus sul. Nove
    # graus de latitude estragam a posicao do sol, o azimute das mesas e toda
    # a conta de sombreamento, e o numero sai plausivel.
    #
    # A conferencia e grosseira de proposito: a latitude e estimada pela
    # coordenada norte em UTM, e so precisa bater dentro de um grau para
    # provar que o ponto convertido e o do terreno, e nao outro qualquer.

    # A linha tem que EXISTIR. Sem esta exigencia, a conferencia toda ficava
    # dentro de um "se a linha aparecer" — e qualquer mudanca no texto da
    # mensagem, ou uma excecao engolida na hora de obter a localizacao, fazia
    # o caso passar verde sem testar nada. Foi assim que dois outros trechos
    # deste arquivo ja passaram a nao testar coisa nenhuma.
    # (?m) para o $ ancorar no fim da LINHA, e nao do texto inteiro.
    if ($r.Texto -notmatch '(?m)^\s*localização:\s+\S') {
        $problemas.Add("$Rotulo : o resumo nao traz a linha de localizacao. Veja $($r.Saida)")
        return $false
    }

    # Desenho sem geolocalizacao e caso legitimo: o comando diz isso e nao ha
    # coordenada para conferir.
    if ($r.Texto -notmatch 'localização:\s+não definida') {
        if ($r.Texto -notmatch 'localização:\s+([\d,\.]+)° ([NSns]), ([\d,\.]+)° ([LOlo])') {
            $problemas.Add(
                "$Rotulo : a localizacao nao esta no formato esperado (grau e hemisferio). " +
                "Veja $($r.Saida)")
            return $false
        }

        $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
        $latitude = [double]::Parse($Matches[1], $ptbr) * $(if ($Matches[2] -eq 'S') { -1 } else { 1 })
        $longitude = [double]::Parse($Matches[3], $ptbr) * $(if ($Matches[4] -eq 'O') { -1 } else { 1 })

        if ($r.Texto -notmatch 'centroY=(-?[\d.]+)') {
            $problemas.Add("$Rotulo : o comando nao informou o centro do terreno. Veja $($r.Saida)")
            return $false
        }

        $norte = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)

        # Estimativa grosseira pela coordenada norte em UTM do hemisferio sul:
        # 10.000.000 m no equador, ~110.574 m por grau. So serve para provar
        # que o ponto convertido e o do terreno, e nao um ponto qualquer.
        #
        # Vale enquanto os desenhos do acervo forem UTM do hemisferio sul. Um
        # desenho de outra projecao deixaria este teste vermelho sem defeito
        # nenhum — e ai a conferencia precisa mudar, nao o plugin.
        $latitudeEstimada = -(10000000 - $norte) / 110574

        if ([Math]::Abs($latitude - $latitudeEstimada) -gt 1.0) {
            $problemas.Add(
                "$Rotulo : a localizacao diz latitude $latitude, mas o terreno esta perto de " +
                "$([Math]::Round($latitudeEstimada, 2)). Veja $($r.Saida)")
            return $false
        }

        # O Brasil inteiro fica entre 35 e 74 graus a oeste. Nao e uma
        # conferencia geografica de verdade; e a rede que pega longitude com o
        # sinal trocado, que e o erro classico e sai plausivel.
        if ($longitude -gt 0) {
            $problemas.Add(
                "$Rotulo : longitude positiva ($longitude) num terreno em UTM sul. " +
                "Veja $($r.Saida)")
            return $false
        }
    }

    if ($r.Texto -match 'UFV_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
        if ($Matches[1] -ne $Matches[2]) {
            $problemas.Add("$Rotulo nao restaurou o SECURELOAD.")
            return $false
        }
    }
    else {
        $problemas.Add("$Rotulo nao informa o SECURELOAD antes e depois. Veja $($r.Saida)")
        return $false
    }

    # ---- a conferencia que vale: motor contra Civil 3D ---------------------
    #
    # Os numeros acima provam que o motor e coerente consigo mesmo, e isso e
    # exatamente o que uma leitura sistematicamente errada preserva. Aqui o
    # comando imprime tambem o que o proprio Civil 3D diz da superficie, por
    # um caminho independente, e os dois lados sao comparados. Vale para
    # qualquer desenho que o Renan congele depois, sem ninguem cravar numero
    # nenhum a mao.

    if ($r.Texto -match 'CIVIL3D indisponivel') {
        $problemas.Add("$Rotulo nao conseguiu ler as estatisticas do Civil 3D. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CIVIL3D triangulos=(\d+) cotaMin=(-?[\d.]+) cotaMax=(-?[\d.]+) pontos=(\d+)') {
        $problemas.Add("$Rotulo nao imprimiu a conferencia do Civil 3D. Veja $($r.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $trianguloDoCad = [int] $Matches[1]
    $cotaMinimaDoCad = [double]::Parse($Matches[2], $invariante)
    $cotaMaximaDoCad = [double]::Parse($Matches[3], $invariante)

    if ($triangulos -ne $trianguloDoCad) {
        $problemas.Add(
            "$Rotulo : o motor leu $triangulos triangulos e o Civil 3D diz $trianguloDoCad.")
        return $false
    }

    # 1 cm: o Civil 3D mostra a elevacao com tres casas.
    if ([Math]::Abs($cotaMinima - $cotaMinimaDoCad) -gt 0.01) {
        $problemas.Add(
            "$Rotulo : cota minima do motor $cotaMinima, do Civil 3D $cotaMinimaDoCad.")
        return $false
    }

    if ([Math]::Abs($cotaMaxima - $cotaMaximaDoCad) -gt 0.01) {
        $problemas.Add(
            "$Rotulo : cota maxima do motor $cotaMaxima, do Civil 3D $cotaMaximaDoCad.")
        return $false
    }

    # Se alguma conferencia de numero falhou, ela ja registrou o problema.
    return -not ($problemas | Where-Object { $_ -like "$Rotulo *" })
}

# ---- os valores esperados --------------------------------------------------
#
# Conferir so a coerencia interna dos numeros nao prova nada: uma leitura
# sistematicamente errada mantem tudo batendo entre si — a area continua
# positiva, a cota maxima continua acima da minima —, so que sobre o terreno
# errado. O que fecha isso e comparar com numeros que nao sairam do plugin.

$esperadoDoAcervo   = Join-Path $pastaDoAcervo 'etapa-1\terreno-esperado.psd1'
$esperadoProposto   = Join-Path $raiz 'tests\proposto\etapa-1\terreno-esperado.psd1'

$arquivoEsperado = if (Test-Path $esperadoDoAcervo) { $esperadoDoAcervo }
                   elseif (Test-Path $esperadoProposto) { $esperadoProposto }
                   else { $null }

$esperados = @{}
if ($arquivoEsperado) {
    $esperados = Import-PowerShellDataFile -LiteralPath $arquivoEsperado

    if ($arquivoEsperado -eq $esperadoProposto) {
        Write-Host '  (valores esperados ainda em tests\proposto; o Renan precisa conferir e mover para o acervo)' -ForegroundColor DarkGray
    }
}

<#
    Confere um numero do resumo contra o esperado.
#>
function Conferir-Numero {
    param(
        [string] $Rotulo,
        [string] $Nome,
        $Obtido,
        $Esperado,
        [double] $Tolerancia = 0.0
    )

    if ($null -eq $Esperado) { return $true }

    $diferenca = [Math]::Abs([double]$Obtido - [double]$Esperado)
    if ($diferenca -le $Tolerancia) { return $true }

    $script:problemas.Add(
        "$Rotulo : $Nome deu $Obtido e o esperado e $Esperado (diferenca de $diferenca).")
    return $false
}

<#
    O carimbo de proveniencia (passo 1.5), em duas metades.

    Primeira: processa o terreno e salva o desenho numa copia. Segunda: abre a
    copia, ja noutro processo do Core Console, e pergunta o status.

    Duas metades porque e isso que o plano pede — "reabrir o desenho recupera o
    registro". Um carimbo guardado em memoria passaria num teste de uma sessao
    so e falharia no uso real, que e meses depois, noutra maquina, com o
    arquivo vindo por e-mail.

    A copia fica em artefatos, nunca no acervo: o acervo e congelado por hash,
    e salvar por cima dele deixaria o placar vermelho — corretamente.
#>
function Testar-Carimbo {
    param([string] $Desenho)

    $copia = Join-Path $saida 'carimbo.dwg'
    if (Test-Path $copia) { Remove-Item $copia -Force }

    $gravar = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'ufv-carimbo-gravar' `
                                 -Script (Join-Path $PSScriptRoot 'ufv-carimbo-gravar.scr') `
                                 -Substituicoes @{ '{{SAIDA}}' = $copia }

    if ($gravar.Texto -notmatch 'UFV_GRAVADO') {
        $problemas.Add("ufv-carimbo: a primeira metade nao terminou. Veja $($gravar.Saida)")
        return $false
    }

    if (-not (Test-Path $copia)) {
        $problemas.Add("ufv-carimbo: o desenho nao foi salvo em $copia. Veja $($gravar.Saida)")
        return $false
    }

    $ler = Invoke-CoreConsole -Desenho $copia -Rotulo 'ufv-carimbo-ler' `
                              -Script (Join-Path $PSScriptRoot 'ufv-carimbo-ler.scr')

    # Ancorado no inicio da linha: sem isso o proprio eco do comando
    # ("UFV_TERRENO_STATUS Loading AECC Mapcheck...") casa com o padrao e o
    # teste le "Loading" como se fosse o estado.
    if ($ler.Texto -match '(?m)^STATUS SemCarimbo') {
        $problemas.Add(
            'ufv-carimbo: o carimbo nao sobreviveu ao arquivo ser salvo e reaberto. ' +
            "Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -notmatch '(?m)^STATUS (\w+):') {
        $problemas.Add("ufv-carimbo: a segunda metade nao respondeu o status. Veja $($ler.Saida)")
        return $false
    }

    $estado = $Matches[1]
    if ($estado -ne 'Atual') {
        $problemas.Add(
            "ufv-carimbo: depois de reabrir, o status deu '$estado' e devia ser 'Atual'. " +
            "Veja $($ler.Saida)")
        return $false
    }

    # O carimbo precisa dizer de qual superficie ele e, e quando foi feito:
    # sem isso o aviso futuro nao teria o que mostrar ao usuario.
    if ($ler.Texto -notmatch 'terreno de \S.*processado em \d{2}/\d{2}/\d{4} \d{2}:\d{2}') {
        $problemas.Add("ufv-carimbo: o status nao traz a superficie e a data. Veja $($ler.Saida)")
        return $false
    }

    # Terceira metade: move a superficie e exige que o carimbo perceba.
    #
    # E o caso que o Renan achou a mao. Sem ele, a deteccao de terreno
    # envelhecido so era exercitada por mutacao de codigo — e mutar o codigo
    # nao prova que o Civil 3D muda o que se espera que ele mude quando a
    # superficie e editada de verdade.
    $mover = Invoke-CoreConsole -Desenho $copia -Rotulo 'ufv-carimbo-mover' `
                                -Script (Join-Path $PSScriptRoot 'ufv-carimbo-mover.scr')

    if ($mover.Texto -notmatch 'UFV_MOVEU=([1-9]\d*)') {
        $problemas.Add("ufv-carimbo: nao consegui mover a superficie no desenho. Veja $($mover.Saida)")
        return $false
    }

    if ($mover.Texto -notmatch '(?m)^STATUS (\w+):') {
        $problemas.Add("ufv-carimbo: depois de mover, o status nao respondeu. Veja $($mover.Saida)")
        return $false
    }

    $estadoDepoisDeMover = $Matches[1]
    if ($estadoDepoisDeMover -ne 'Desatualizado') {
        $problemas.Add(
            "ufv-carimbo: a superficie foi movida 50 m e o status deu '$estadoDepoisDeMover'. " +
            "Mover o terreno muda de onde sai cada cota, e o carimbo tem que perceber. " +
            "Veja $($mover.Saida)")
        return $false
    }

    # E a mensagem precisa dizer o que aconteceu, nao so que algo aconteceu.
    if ($mover.Texto -notmatch 'foi movida') {
        $problemas.Add(
            "ufv-carimbo: o aviso nao diz que a superficie foi movida. Veja $($mover.Saida)")
        return $false
    }

    return $true
}

<#
    A consulta de cota (passo 1.7).

    Usa o comando do produto, o mesmo que o usuario aciona pelo botao: no Core
    Console o GetPoint le do proprio script, entao nao e preciso um comando
    separado so para o teste.

    Os pontos saem da caixa da superficie, que o comando automatico imprime.
    O de dentro e o centro; o de fora fica a dez quilometros. Assim o caso
    vale para qualquer desenho congelado depois, sem coordenada cravada a mao.
#>
function Testar-Coordenada {
    param([string] $Desenho, [string] $Rotulo)

    # Primeiro descobrimos onde fica o terreno, rodando o automatico.
    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo "$Rotulo--sonda" `
                                -Script (Join-Path $PSScriptRoot 'ufv-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("$Rotulo : nao consegui achar o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    # Cultura invariante no ponto decimal: com a do PowerShell em portugues,
    # "314068,126" sai com virgula, e o AutoCAD le virgula como separador de
    # coordenada — o ponto viraria quatro numeros e o comando engasgaria.
    $dentro = [string]::Format($invariante, '{0:0.###},{1:0.###}', $centroX, $centroY)
    $fora = [string]::Format($invariante, '{0:0.###},{1:0.###}', $centroX + 10000, $centroY + 10000)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo $Rotulo `
                            -Script (Join-Path $PSScriptRoot 'ufv-coord.scr') `
                            -Substituicoes @{ '{{DENTRO}}' = $dentro; '{{FORA}}' = $fora }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("$Rotulo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    # O ponto de dentro tem que devolver uma cota, e ela tem que cair entre a
    # cota minima e a maxima do terreno — que o proprio comando imprimiu.
    if ($r.Texto -notmatch '(?m)^\s+X [\d\.,]+\s+Y [\d\.,]+\s+Z ([\d\.,-]+)\s*$') {
        $problemas.Add("$Rotulo : o ponto de dentro nao devolveu cota. Veja $($r.Saida)")
        return $false
    }

    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $z = [double]::Parse($Matches[1], $ptbr)

    if ($r.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("$Rotulo : nao achei as cotas do resumo. Veja $($r.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    if ($z -lt $minima -or $z -gt $maxima) {
        $problemas.Add(
            "$Rotulo : a cota consultada ($z) esta fora da faixa do terreno ($minima a $maxima). " +
            "Veja $($r.Saida)")
        return $false
    }

    # E o ponto de fora tem que ser recusado, em vez de receber um numero.
    if ($r.Texto -notmatch 'fora do terreno') {
        $problemas.Add(
            "$Rotulo : um ponto a dez quilometros do terreno nao foi recusado. " +
            "Veja $($r.Saida)")
        return $false
    }

    return $true
}

<#
    A area de implantacao (passos 2.2 e 2.3), em duas metades.

    Primeira: processa o terreno, traca uma area e salva. Segunda: abre a
    copia noutro processo e confere que a identidade da area e a MESMA.

    E o que o plano pede em 2.2: "GUID sobrevive a salvar e reabrir". Uma area
    que perde a identidade vira uma polilinha qualquer no meio de milhares, e
    todo resultado calculado sobre ela fica orfao.

    Os vertices saem da caixa da superficie, para o caso valer em qualquer
    desenho do acervo.
#>
function Testar-Area {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'ufv-area--sonda' `
                                -Script (Join-Path $PSScriptRoot 'ufv-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("ufv-area : nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    # Um quadrado de 100 m em volta do centro do terreno.
    function Ponto([double] $dx, [double] $dy) {
        [string]::Format($invariante, '{0:0.###},{1:0.###}', $centroX + $dx, $centroY + $dy)
    }

    $copia = Join-Path $saida 'area.dwg'
    if (Test-Path $copia) { Remove-Item $copia -Force }

    $nome = 'Area de teste'

    $criar = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'ufv-area-criar' `
        -Script (Join-Path $PSScriptRoot 'ufv-area-criar.scr') `
        -Substituicoes @{
            '{{P1}}' = (Ponto -50 -50)
            '{{P2}}' = (Ponto  50 -50)
            '{{P3}}' = (Ponto  50  50)
            '{{P4}}' = (Ponto -50  50)
            '{{NOME}}' = $nome
            '{{SAIDA}}' = $copia
        }

    if ($criar.Texto -notmatch 'UFV_GRAVADO') {
        $problemas.Add("ufv-area : a primeira metade nao terminou. Veja $($criar.Saida)")
        return $false
    }

    if ($criar.Texto -notmatch 'Área criada:') {
        $problemas.Add("ufv-area : a area nao foi criada. Veja $($criar.Saida)")
        return $false
    }

    # A area tem que ter ganhado vertices no contorno do relevo: um quadrado
    # de 100 m sobre terreno real cruza muitos triangulos, e sem os vertices
    # extras a linha passaria por dentro do morro.
    if ($criar.Texto -notmatch 'vértices no terreno:\s+(\d+)') {
        $problemas.Add("ufv-area : nao achei a contagem de vertices. Veja $($criar.Saida)")
        return $false
    }

    $verticesNoTerreno = [int] $Matches[1]
    if ($verticesNoTerreno -le 4) {
        $problemas.Add(
            "ufv-area : a area ficou com $verticesNoTerreno vertices, os mesmos quatro tracados. " +
            "Sobre terreno de verdade ela tinha que ganhar vertices ao cruzar os triangulos. " +
            "Veja $($criar.Saida)")
        return $false
    }

    if ($criar.Texto -notmatch 'NODESENHO ([0-9a-f-]{36}) ') {
        $problemas.Add("ufv-area : a area nao apareceu na listagem. Veja $($criar.Saida)")
        return $false
    }

    $identidadeAntes = $Matches[1]

    if (-not (Test-Path $copia)) {
        $problemas.Add("ufv-area : o desenho nao foi salvo. Veja $($criar.Saida)")
        return $false
    }

    $ler = Invoke-CoreConsole -Desenho $copia -Rotulo 'ufv-area-ler' `
                              -Script (Join-Path $PSScriptRoot 'ufv-area-ler.scr')

    if ($ler.Texto -notmatch 'NODESENHO ([0-9a-f-]{36}) ') {
        $problemas.Add(
            'ufv-area : depois de salvar e reabrir, a area nao foi reconhecida. ' +
            "A identidade nao sobreviveu ao arquivo. Veja $($ler.Saida)")
        return $false
    }

    $identidadeDepois = $Matches[1]

    if ($identidadeDepois -ne $identidadeAntes) {
        $problemas.Add(
            "ufv-area : a identidade mudou ao reabrir ($identidadeAntes -> $identidadeDepois). " +
            "Veja $($ler.Saida)")
        return $false
    }

    # E o registro central tambem precisa ter sobrevivido: ele e o indice, e
    # sem ele o plugin varreria o desenho inteiro a cada comando.
    if ($ler.Texto -notmatch ('REGISTRADA ' + [regex]::Escape($identidadeAntes))) {
        $problemas.Add(
            'ufv-area : a area sobreviveu mas sumiu do registro central. ' +
            "Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -notmatch ([regex]::Escape($nome))) {
        $problemas.Add("ufv-area : o nome da area nao sobreviveu. Veja $($ler.Saida)")
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

# A janela da mesa (3.7) e o unico WPF fora da ribbon. Se ela for nomeada sem
# cuidado, o NETLOAD inteiro cai num host sem interface - e o sintoma e o
# plugin sumir, nao a janela falhar.
$total++
if (Testar-Caso -Rotulo 'ufv-mesa-sem-interface' -Desenho $desenhoVazio `
                -Script (Join-Path $PSScriptRoot 'ufv-mesa-sem-interface.scr') `
                -Esperados @(
                    [regex]::Escape("Plugin UFV carregado, versão $versao"),
                    [regex]::Escape('A janela da mesa precisa da interface do Civil 3D'))) {
    $passaram++
}

# O caso do terreno conta no total SEMPRE. Antes ele era simplesmente pulado
# quando o desenho nao estava la, e o placar saia "1/1 OK", verde, afirmando
# que tudo passou enquanto o unico teste que prova a leitura do desenho nao
# tinha rodado. Teste que some em silencio e pior que teste que falha.
if ($desenhos.Count -eq 0) {
    $total++
    $problemas.Add(
        'ufv-terreno nao rodou: nao ha desenho com superficie. Congele um em ' +
        'tests\acervo (ver plano\04-testes.md).')
}
else {
    if (-not $doAcervo) {
        Write-Host "  (desenho fora do acervo; usando $($desenhos[0]))" -ForegroundColor DarkGray
    }

    foreach ($desenho in $desenhos) {
        $total++
        $rotulo = 'ufv-terreno--' + [IO.Path]::GetFileNameWithoutExtension($desenho)
        if (Testar-CasoDoTerreno -Desenho $desenho -Rotulo $rotulo) { $passaram++ }
    }

    # O carimbo roda uma vez so: salvar e reabrir custa dois processos do
    # Core Console, e o que se testa e o formato, que nao muda de desenho
    # para desenho.
    $total++
    if (Testar-Carimbo -Desenho $desenhos[0]) { $passaram++ }

    # A consulta de cota, idem.
    $total++
    if (Testar-Coordenada -Desenho $desenhos[0] -Rotulo 'ufv-coord') { $passaram++ }

    # E a area, que tambem salva e reabre.
    $total++
    if (Testar-Area -Desenho $desenhos[0]) { $passaram++ }
}

# ---- veredito --------------------------------------------------------------

if ($problemas.Count -eq 0 -and $passaram -eq $total) {
    Escrever-Linha 'Nivel 2' "$passaram/$total" 'OK'
    exit 0
}

Escrever-Linha 'Nivel 2' "$passaram/$total" 'FALHOU'
foreach ($p in $problemas) { Write-Host "  $p" -ForegroundColor Red }
exit 1
