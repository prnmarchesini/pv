<#
.SYNOPSIS
    Testes de nivel 2: o plugin rodando dentro do CAD, sem interface.

.DESCRIPTION
    04-testes.md, nivel 2: o Core Console (accoreconsole.exe) e o AutoCAD sem
    interface, tocado por linha de comando com um script .scr. Ele abre um
    desenho, roda os comandos do plugin, e a saida e comparada com o esperado.
    Roda sem ninguem clicar em nada.

    Casos de hoje:

      1. CLIVUS_OLA responde com a versao certa. Se isso passa, a cadeia inteira
         esta de pe: build, NETLOAD, registro do comando e a mensagem chegando
         ao usuario.
      2. CLIVUS_TERRENO lista as superficies de um desenho de verdade. E a unica
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
    Write-Host ('{0,-11}{1,-9}' -f $Rotulo, $Placar) -NoNewline
    Write-Host $Situacao -ForegroundColor $cor
}

<#
    Le SECURELOAD de um perfil do registro, ou $null se a chave sumiu ou nao
    tem o valor. O AutoCAD cria e remove perfis por conta propria, entao
    acessar a propriedade direto quebra com Set-StrictMode.
#>
function Ler-SecureLoad {
    param([string] $Perfil, [string] $Nome = 'SecureLoad')

    $item = Get-ItemProperty -LiteralPath $Perfil -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    if (-not ($item.PSObject.Properties.Name -contains $Nome)) { return $null }
    return $item.$Nome
}

# As variaveis do registro que os scripts mexem e o finally devolve. PickStyle
# desde 27/09/2026: a mesa virou grupo, e os casos que simulam o usuario
# apagando UMA peca desligam a selecao por grupo (PICKSTYLE 0), que o
# AutoCAD grava no perfil. Se o script morresse no meio, o Civil 3D do Renan
# ficaria sem selecao por grupo.
$script:variaveisGuardadas = @('SecureLoad', 'PickStyle')

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
        ForEach-Object {
            foreach ($nome in $script:variaveisGuardadas) {
                $perfisAntes["$($_.PSPath)|$nome"] = Ler-SecureLoad $_.PSPath $nome
            }
        }

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
        foreach ($chave in @($perfisAntes.Keys)) {
            $perfil, $nome = $chave -split '\|', 2
            $antes = $perfisAntes[$chave]
            $agora = Ler-SecureLoad $perfil $nome

            if ($antes -eq $agora) { continue }

            if ($null -eq $antes) {
                Remove-ItemProperty -LiteralPath $perfil -Name $nome -ErrorAction SilentlyContinue
            }
            elseif (Test-Path -LiteralPath $perfil) {
                # -Type DWord porque o valor pode ter sido apagado no meio: sem
                # isso o Set-ItemProperty recriaria como String.
                Set-ItemProperty -LiteralPath $perfil -Name $nome -Value $antes -Type DWord
            }
        }

        # E conferimos o registro, que e onde o estrago ficaria.
        foreach ($chave in @($perfisAntes.Keys)) {
            $perfil, $nome = $chave -split '\|', 2

            if ((Ler-SecureLoad $perfil $nome) -ne $perfisAntes[$chave]) {
                $problemas.Add("$($nome.ToUpperInvariant()) continua diferente no registro: $perfil")
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
    if ($r.Texto -match 'CLIVUS_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
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

$script:dll = Join-Path $raiz 'src\Clivus.Plugin\bin\Debug\Clivus.Plugin.dll'
if (-not (Test-Path $script:dll)) {
    Parar-Com "Clivus.Plugin.dll nao encontrada em $script:dll. Compile antes (tools\rodar-testes.ps1 ja faz isso)."
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

# ---- a mensagem esperada do CLIVUS_OLA ----------------------------------------
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
                            -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr') `
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
            "clivus-terreno anunciou $anunciadas superficie(s) mas imprimiu $($itens.Count) linha(s). " +
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

    if ($r.Texto -match 'CLIVUS_SECURELOAD_ANTES=(\d+)\s+DEPOIS=(\d+)') {
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

    $gravar = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-carimbo-gravar' `
                                 -Script (Join-Path $PSScriptRoot 'clivus-carimbo-gravar.scr') `
                                 -Substituicoes @{ '{{SAIDA}}' = $copia }

    if ($gravar.Texto -notmatch 'CLIVUS_GRAVADO') {
        $problemas.Add("clivus-carimbo: a primeira metade nao terminou. Veja $($gravar.Saida)")
        return $false
    }

    if (-not (Test-Path $copia)) {
        $problemas.Add("clivus-carimbo: o desenho nao foi salvo em $copia. Veja $($gravar.Saida)")
        return $false
    }

    $ler = Invoke-CoreConsole -Desenho $copia -Rotulo 'clivus-carimbo-ler' `
                              -Script (Join-Path $PSScriptRoot 'clivus-carimbo-ler.scr')

    # Ancorado no inicio da linha: sem isso o proprio eco do comando
    # ("CLIVUS_TERRENO_STATUS Loading AECC Mapcheck...") casa com o padrao e o
    # teste le "Loading" como se fosse o estado.
    if ($ler.Texto -match '(?m)^STATUS SemCarimbo') {
        $problemas.Add(
            'clivus-carimbo: o carimbo nao sobreviveu ao arquivo ser salvo e reaberto. ' +
            "Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -notmatch '(?m)^STATUS (\w+):') {
        $problemas.Add("clivus-carimbo: a segunda metade nao respondeu o status. Veja $($ler.Saida)")
        return $false
    }

    $estado = $Matches[1]
    if ($estado -ne 'Atual') {
        $problemas.Add(
            "clivus-carimbo: depois de reabrir, o status deu '$estado' e devia ser 'Atual'. " +
            "Veja $($ler.Saida)")
        return $false
    }

    # O carimbo precisa dizer de qual superficie ele e, e quando foi feito:
    # sem isso o aviso futuro nao teria o que mostrar ao usuario.
    if ($ler.Texto -notmatch 'terreno de \S.*processado em \d{2}/\d{2}/\d{4} \d{2}:\d{2}') {
        $problemas.Add("clivus-carimbo: o status nao traz a superficie e a data. Veja $($ler.Saida)")
        return $false
    }

    # Terceira metade: move a superficie e exige que o carimbo perceba.
    #
    # E o caso que o Renan achou a mao. Sem ele, a deteccao de terreno
    # envelhecido so era exercitada por mutacao de codigo — e mutar o codigo
    # nao prova que o Civil 3D muda o que se espera que ele mude quando a
    # superficie e editada de verdade.
    $mover = Invoke-CoreConsole -Desenho $copia -Rotulo 'clivus-carimbo-mover' `
                                -Script (Join-Path $PSScriptRoot 'clivus-carimbo-mover.scr')

    if ($mover.Texto -notmatch 'CLIVUS_MOVEU=([1-9]\d*)') {
        $problemas.Add("clivus-carimbo: nao consegui mover a superficie no desenho. Veja $($mover.Saida)")
        return $false
    }

    if ($mover.Texto -notmatch '(?m)^STATUS (\w+):') {
        $problemas.Add("clivus-carimbo: depois de mover, o status nao respondeu. Veja $($mover.Saida)")
        return $false
    }

    $estadoDepoisDeMover = $Matches[1]
    if ($estadoDepoisDeMover -ne 'Desatualizado') {
        $problemas.Add(
            "clivus-carimbo: a superficie foi movida 50 m e o status deu '$estadoDepoisDeMover'. " +
            "Mover o terreno muda de onde sai cada cota, e o carimbo tem que perceber. " +
            "Veja $($mover.Saida)")
        return $false
    }

    # E a mensagem precisa dizer o que aconteceu, nao so que algo aconteceu.
    if ($mover.Texto -notmatch 'foi movida') {
        $problemas.Add(
            "clivus-carimbo: o aviso nao diz que a superficie foi movida. Veja $($mover.Saida)")
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
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

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
                            -Script (Join-Path $PSScriptRoot 'clivus-coord.scr') `
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

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-area--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-area : nao achei o centro do terreno. Veja $($sonda.Saida)")
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

    $criar = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-area-criar' `
        -Script (Join-Path $PSScriptRoot 'clivus-area-criar.scr') `
        -Substituicoes @{
            '{{P1}}' = (Ponto -50 -50)
            '{{P2}}' = (Ponto  50 -50)
            '{{P3}}' = (Ponto  50  50)
            '{{P4}}' = (Ponto -50  50)
            '{{NOME}}' = $nome
            '{{SAIDA}}' = $copia
        }

    if ($criar.Texto -notmatch 'CLIVUS_GRAVADO') {
        $problemas.Add("clivus-area : a primeira metade nao terminou. Veja $($criar.Saida)")
        return $false
    }

    if ($criar.Texto -notmatch 'Área criada:') {
        $problemas.Add("clivus-area : a area nao foi criada. Veja $($criar.Saida)")
        return $false
    }

    # A area tem que ter ganhado vertices no contorno do relevo: um quadrado
    # de 100 m sobre terreno real cruza muitos triangulos, e sem os vertices
    # extras a linha passaria por dentro do morro.
    if ($criar.Texto -notmatch 'vértices no terreno:\s+(\d+)') {
        $problemas.Add("clivus-area : nao achei a contagem de vertices. Veja $($criar.Saida)")
        return $false
    }

    $verticesNoTerreno = [int] $Matches[1]
    if ($verticesNoTerreno -le 4) {
        $problemas.Add(
            "clivus-area : a area ficou com $verticesNoTerreno vertices, os mesmos quatro tracados. " +
            "Sobre terreno de verdade ela tinha que ganhar vertices ao cruzar os triangulos. " +
            "Veja $($criar.Saida)")
        return $false
    }

    if ($criar.Texto -notmatch 'NODESENHO ([0-9a-f-]{36}) ') {
        $problemas.Add("clivus-area : a area nao apareceu na listagem. Veja $($criar.Saida)")
        return $false
    }

    $identidadeAntes = $Matches[1]

    if (-not (Test-Path $copia)) {
        $problemas.Add("clivus-area : o desenho nao foi salvo. Veja $($criar.Saida)")
        return $false
    }

    $ler = Invoke-CoreConsole -Desenho $copia -Rotulo 'clivus-area-ler' `
                              -Script (Join-Path $PSScriptRoot 'clivus-area-ler.scr')

    if ($ler.Texto -notmatch 'NODESENHO ([0-9a-f-]{36}) ') {
        $problemas.Add(
            'clivus-area : depois de salvar e reabrir, a area nao foi reconhecida. ' +
            "A identidade nao sobreviveu ao arquivo. Veja $($ler.Saida)")
        return $false
    }

    $identidadeDepois = $Matches[1]

    if ($identidadeDepois -ne $identidadeAntes) {
        $problemas.Add(
            "clivus-area : a identidade mudou ao reabrir ($identidadeAntes -> $identidadeDepois). " +
            "Veja $($ler.Saida)")
        return $false
    }

    # E o registro central tambem precisa ter sobrevivido: ele e o indice, e
    # sem ele o plugin varreria o desenho inteiro a cada comando.
    if ($ler.Texto -notmatch ('REGISTRADA ' + [regex]::Escape($identidadeAntes))) {
        $problemas.Add(
            'clivus-area : a area sobreviveu mas sumiu do registro central. ' +
            "Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -notmatch ([regex]::Escape($nome))) {
        $problemas.Add("clivus-area : o nome da area nao sobreviveu. Veja $($ler.Saida)")
        return $false
    }

    return $true
}

<#
    O alinhamento (passo 4.2): a linha tem que assentar no terreno.

    Regra sagrada 5 (01-regras-sagradas.md): qualquer coisa que o plugin
    desenha acompanha o terreno. O teste entrega ao comando cotas de clique
    absurdas (0 e 9999) e le, direto da entidade, a cota das duas pontas: as
    duas tem que cair dentro da faixa de cotas do terreno, e nenhuma pode ser
    a que foi digitada.
#>
function Testar-Alinhamento {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-alinhamento--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-alinhamento : nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    # Um "V" de tres pontos em volta do centro: dois trechos de 40*sqrt(2) m.
    $comprimentoEsperado = 2 * [math]::Sqrt(40 * 40 + 40 * 40)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-alinhamento' `
        -Script (Join-Path $PSScriptRoot 'clivus-alinhamento.scr') `
        -Substituicoes @{
            '{{P1}}'   = (Ponto3 -40 -20 0)
            '{{P2}}'   = (Ponto3   0  20 9999)
            '{{P3}}'   = (Ponto3  40 -20 0)
            '{{LADO}}' = (Ponto3   0 -30 0)
            '{{NOME}}' = 'Alinhamento de teste'
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-alinhamento terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'Alinhamento criado:') {
        $problemas.Add("clivus-alinhamento : o alinhamento nao foi criado. Veja $($r.Saida)")
        return $false
    }

    # Os tres pontos distam 113,137 m em planta no total. Se veio outro
    # numero, algo puxou os pontos (OSNAP ligado, por exemplo) e o resto do
    # teste nao esta mais conferindo o que pensa que confere.
    if ($r.Texto -notmatch 'comprimento em planta:\s+(-?[\d.,]+) m') {
        $problemas.Add("clivus-alinhamento : nao achei o comprimento em planta. Veja $($r.Saida)")
        return $false
    }

    # O relatorio do alinhamento sai na cultura do processo, que no Core
    # Console e ponto decimal; o resumo do terreno sai fixo em pt-BR. Aqui o
    # numero e lido aceitando os dois, porque nao tem separador de milhar.
    $comprimento = [double]::Parse($Matches[1].Replace(',', '.'), $invariante)
    if ([math]::Abs($comprimento - $comprimentoEsperado) -gt 0.01) {
        $problemas.Add(
            "clivus-alinhamento : o comprimento em planta deu $comprimento m, e nao $comprimentoEsperado m: os pontos " +
            "digitados foram deslocados antes de chegar ao comando. Veja $($r.Saida)")
        return $false
    }

    # O Core Console ecoa a expressao LISP antes do resultado, e o (princ)
    # devolve a string ainda por cima; por isso o nome da layer e lido so ate
    # o primeiro caractere que nao pode fazer parte de um nome.
    if ($r.Texto -notmatch 'CLIVUS_PONTAS tipo=(\w+) n=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+) layer=([A-Za-z0-9_\-]+)') {
        $problemas.Add("clivus-alinhamento : nao consegui ler os vertices da entidade. Veja $($r.Saida)")
        return $false
    }

    $tipo = $Matches[1]
    $n = [int] $Matches[2]
    $zmin = [double]::Parse($Matches[3], $invariante)
    $zmax = [double]::Parse($Matches[4], $invariante)
    $layer = $Matches[5]

    if ($tipo -ne 'POLYLINE' -or $layer -ne 'CLIVUS_ALINHAMENTO') {
        $problemas.Add(
            "clivus-alinhamento : a ultima entidade do desenho e '$tipo' na layer '$layer', nao e a polilinha do alinhamento. " +
            "Veja $($r.Saida)")
        return $false
    }

    # Sobre terreno de verdade, dois trechos de 56 m cruzam muitos triangulos:
    # a linha tinha que ganhar vertices alem dos tres tracados. Sem eles, ela
    # passa por dentro do morro (regra sagrada 5).
    if ($n -le 3) {
        $problemas.Add(
            "clivus-alinhamento : a polilinha ficou com $n vertices, os mesmos tres tracados. " +
            "Ela nao assentou no relevo. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m\s+\(desn') {
        $problemas.Add("clivus-alinhamento : nao achei as cotas do resumo do terreno. Veja $($r.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    if ($zmin -lt $minima -or $zmax -gt $maxima) {
        $problemas.Add(
            "clivus-alinhamento : os vertices foram de Z=$zmin a Z=$zmax, fora da faixa do terreno ($minima a $maxima). " +
            "A linha nao assentou no terreno. Veja $($r.Saida)")
        return $false
    }

    # As cotas digitadas nos cliques (0 e 9999) nao podem ter sobrevivido. O
    # 9999 ja cai fora da faixa; o 0 pode coincidir com um terreno ao nivel
    # do mar, entao a conferencia e explicita.
    if ($zmin -lt 0.0005 -and $minima -gt 0.001) {
        $problemas.Add("clivus-alinhamento : algum vertice manteve o Z=0 do clique. Veja $($r.Saida)")
        return $false
    }

    return $true
}

<#
    A configuracao do projeto (4.4): grava a amostra do Core em que todo campo
    difere do padrao (ProjectSettings.SampleAllDifferent, conferida campo a
    campo em nivel 1), salva, reabre e exige que cada campo volte igual.
    Comparar o conjunto inteiro, e nao um ou dois, e o que pega um campo
    esquecido na gravacao ou na leitura.
#>
function Testar-Config {
    param([string] $Desenho)

    $copia = Join-Path $saida 'config.dwg'
    if (Test-Path $copia) { Remove-Item $copia -Force }

    $gravar = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-config-gravar' `
                                 -Script (Join-Path $PSScriptRoot 'clivus-config-gravar.scr') `
                                 -Substituicoes @{ '{{SAIDA}}' = $copia }

    if ($gravar.Texto -notmatch 'CLIVUS_GRAVADO') {
        $problemas.Add("clivus-config: a primeira metade nao terminou. Veja $($gravar.Saida)")
        return $false
    }

    # CLIVUS_CONFIG num host sem interface tem que avisar, nao estourar.
    if ($gravar.Texto -notmatch 'A tela de configura\S+ precisa da interface') {
        $problemas.Add("clivus-config: CLIVUS_CONFIG sem interface nao avisou que precisa dela. Veja $($gravar.Saida)")
        return $false
    }

    if ($gravar.Texto -notmatch 'CONFIG Gravada para teste') {
        $problemas.Add("clivus-config: a configuracao de teste nao foi gravada. Veja $($gravar.Saida)")
        return $false
    }

    if (-not (Test-Path $copia)) {
        $problemas.Add("clivus-config: o desenho nao foi salvo em $copia. Veja $($gravar.Saida)")
        return $false
    }

    $gravados = @([regex]::Matches($gravar.Texto, '(?m)^CONFIG_CAMPO ([^\r\n]+?)\s*$') | ForEach-Object { $_.Groups[1].Value })

    # 33 campos, o mesmo numero que o teste de nivel 1 exige: um campo a menos
    # na gravacao passaria por "preservou tudo" se so o conjunto fosse comparado.
    if ($gravados.Count -ne 33) {
        $problemas.Add("clivus-config: a primeira metade escreveu $($gravados.Count) campos, e sao 33. Veja $($gravar.Saida)")
        return $false
    }

    # A configuracao de teste precisa ser mesmo diferente do padrao: se o
    # PITCH gravado for 6, um plugin que perdesse o registro e caisse no padrao
    # passaria o resto do teste.
    if ($gravados -notcontains 'PITCH=7.5') {
        $problemas.Add("clivus-config: a configuracao de teste nao difere do padrao (PITCH). Veja $($gravar.Saida)")
        return $false
    }

    $ler = Invoke-CoreConsole -Desenho $copia -Rotulo 'clivus-config-ler' `
                              -Script (Join-Path $PSScriptRoot 'clivus-config-ler.scr')

    if ($ler.Texto -match '(?m)^CONFIG Ausente') {
        $problemas.Add("clivus-config: a configuracao nao sobreviveu ao arquivo ser salvo e reaberto. Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -match '(?m)^CONFIG Problema: (.+)$') {
        $problemas.Add("clivus-config: depois de reabrir, a leitura reclamou: $($Matches[1]). Veja $($ler.Saida)")
        return $false
    }

    if ($ler.Texto -notmatch '(?m)^CONFIG Gravada no desenho') {
        $problemas.Add("clivus-config: a segunda metade nao respondeu. Veja $($ler.Saida)")
        return $false
    }

    $lidos = @([regex]::Matches($ler.Texto, '(?m)^CONFIG_CAMPO ([^\r\n]+?)\s*$') | ForEach-Object { $_.Groups[1].Value })

    $faltando = @($gravados | Where-Object { $lidos -notcontains $_ })
    $sobrando = @($lidos | Where-Object { $gravados -notcontains $_ })

    if ($faltando.Count -gt 0 -or $sobrando.Count -gt 0) {
        $problemas.Add(
            "clivus-config: a configuracao voltou diferente. Faltando: [$($faltando -join ', ')]. " +
            "Sobrando: [$($sobrando -join ', ')]. Veja $($ler.Saida)")
        return $false
    }

    return $true
}

<#
    Uma fileira no CAD (5.7 e 5.8): area de 100 m em volta do centro do
    terreno, alinhamento na borda sul com as mesas ao norte, fileira 1
    processada e desenhada. O que se le e da entidade, em LISP: contagem de
    pilares e faces, e a cota de topo de cada pilar dentro da faixa do
    terreno (regra sagrada 5).
#>
function Testar-Fileira {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-fileira--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-fileira: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-fileira' `
        -Script (Join-Path $PSScriptRoot 'clivus-fileira.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 9999)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-fileira terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^FILEIRA F1:') {
        $problemas.Add("clivus-fileira: a fileira nao foi processada. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'desenhado: (\d+) mesa\(s\), (\d+) pilar\(es\), (\d+) módulo\(s\) com face') {
        $problemas.Add("clivus-fileira: nao achei o resumo do desenho. Veja $($r.Saida)")
        return $false
    }

    $mesas = [int] $Matches[1]
    $pilaresRelatados = [int] $Matches[2]
    $modulosRelatados = [int] $Matches[3]

    if ($r.Texto -notmatch 'CLIVUS_DESENHO pilares=(\d+) faces=(\d+) alturas=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-fileira: nao consegui ler as entidades em LISP. Veja $($r.Saida)")
        return $false
    }

    $pilares = [int] $Matches[1]
    $faces = [int] $Matches[2]
    $alturas = [int] $Matches[3]
    $zmin = [double]::Parse($Matches[4], $invariante)
    $zmax = [double]::Parse($Matches[5], $invariante)

    # Numa area de 100 m com a mesa de exemplo (18,7 m, 7 pilares, 28
    # modulos; CLIVUS_FILEIRA_AUTO usa sempre ela, para nao depender de perfil
    # salvo na maquina) e 0,5 m de vao cabem cinco inteiras e uma na borda.
    if ($mesas -lt 5) {
        $problemas.Add("clivus-fileira: so $mesas mesa(s) na fileira, esperava pelo menos 5. Veja $($r.Saida)")
        return $false
    }

    if ($pilares -ne $pilaresRelatados -or $faces -ne $modulosRelatados -or $alturas -ne 3 * $pilares) {
        $problemas.Add(
            "clivus-fileira: o desenho tem $pilares pilar(es), $faces face(s) e $alturas altura(s); " +
            "o plugin relatou $pilaresRelatados e $modulosRelatados. Veja $($r.Saida)")
        return $false
    }

    if ($pilares -ne 7 * $mesas -or $faces -ne 28 * $mesas) {
        $problemas.Add(
            "clivus-fileira: $mesas mesa(s) deviam dar $(7 * $mesas) pilares e $(28 * $mesas) faces, " +
            "e sao $pilares e $faces. Veja $($r.Saida)")
        return $false
    }

    # As conferencias finas, lidas das entidades: modulo no lugar da face
    # (a matriz), face para cima, secao do pilar, XData em tudo, P3 fechando
    # com topo e terreno, camada de alturas desligada, aviso por marcada, e
    # a caixa do bloco do pilar "por bloco" (a cor da instancia aparece).
    if ($r.Texto -notmatch 'CLIVUS_DESENHO2 modulos=(\d+) casados=(\d+) normais=(\d+) secao=(\d+) xdata=(\d+) contornos=(\d+) p3ok=(\d+) p3total=(\d+) alturasoff=(\d) marcadas=(\d+) caixa62=(-?\d+)') {
        $problemas.Add("clivus-fileira: nao consegui ler as conferencias finas em LISP. Veja $($r.Saida)")
        return $false
    }

    $modulosInsert = [int] $Matches[1]
    $casados = [int] $Matches[2]
    $normais = [int] $Matches[3]
    $secao = [int] $Matches[4]
    $xdata = [int] $Matches[5]
    $contornos = [int] $Matches[6]
    $p3ok = [int] $Matches[7]
    $p3total = [int] $Matches[8]
    $alturasOff = [int] $Matches[9]
    $marcadasTexto = [int] $Matches[10]
    $caixa62 = [int] $Matches[11]

    if ($modulosInsert -ne $faces -or $casados -ne $modulosInsert) {
        $problemas.Add(
            "clivus-fileira: $modulosInsert bloco(s) de modulo para $faces face(s), e so $casados no lugar da face. " +
            "A matriz da mesa nao chegou certa ao AutoCAD. Veja $($r.Saida)")
        return $false
    }

    if ($normais -ne $faces) {
        $problemas.Add("clivus-fileira: $($faces - $normais) face(s) apontam para baixo. Veja $($r.Saida)")
        return $false
    }

    if ($secao -ne $pilares) {
        $problemas.Add("clivus-fileira: $($pilares - $secao) pilar(es) com secao ou comprimento errados. Veja $($r.Saida)")
        return $false
    }

    if ($contornos -ne $mesas -or $xdata -ne ($pilares + $modulosInsert + $faces + $contornos)) {
        $problemas.Add(
            "clivus-fileira: $contornos contorno(s) para $mesas mesa(s), e $xdata entidade(s) com XData de " +
            "$($pilares + $modulosInsert + $faces + $contornos). Veja $($r.Saida)")
        return $false
    }

    if ($p3total -lt 1 -or $p3ok -ne $p3total) {
        $problemas.Add(
            "clivus-fileira: em $($p3total - $p3ok) de $p3total pilar(es) topo - terreno nao da P3. " +
            "As cotas nao fecham entre si. Veja $($r.Saida)")
        return $false
    }

    if ($alturasOff -ne 1) {
        $problemas.Add("clivus-fileira: a camada das alturas nasceu ligada. Veja $($r.Saida)")
        return $false
    }

    # Desde 29/09/2026 a mesa que nao cabe nao leva texto no desenho (o
    # Renan: "para de colocar esses textos, estao estourando muito"): ela e
    # magenta inteira, e o motivo fica no XData e na linha de comando.
    if ($marcadasTexto -ne 0) {
        $problemas.Add("clivus-fileira: $marcadasTexto texto(s) na camada de marcadas; a mesa que nao cabe nao leva mais aviso escrito. Veja $($r.Saida)")
        return $false
    }

    if ($caixa62 -ne 0) {
        $problemas.Add("clivus-fileira: a caixa do bloco do pilar nao e 'por bloco' (62 = $caixa62): a cor de analise nao apareceria. Veja $($r.Saida)")
        return $false
    }

    # Regra sagrada 5: o topo de todo pilar esta entre a cota minima do
    # terreno e a maxima mais a altura de uma mesa (uns 5 m). E grosseiro;
    # a conferencia fina e a de P3 acima, que fecha topo, terreno e altura
    # livre pilar a pilar. (O Z = 9999 no alinhamento e prova do 4.2, que o
    # drapeia antes de gravar; fica como segunda linha de defesa.)
    if ($r.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m\s+\(desn') {
        $problemas.Add("clivus-fileira: nao achei as cotas do resumo do terreno. Veja $($r.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    if ($zmin -lt $minima -or $zmax -gt ($maxima + 5)) {
        $problemas.Add(
            "clivus-fileira: os topos dos pilares vao de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). " +
            "Algo foi desenhado sem acompanhar o terreno. Veja $($r.Saida)")
        return $false
    }

    return $true
}

<#
    A area inteira (5.9): mesma area e alinhamento da fileira, todas as
    fileiras processadas e desenhadas. Contagens por mesa e cotas dos
    pilares lidas em LISP; o tempo do motor e do desenho e relatado.
#>
function Testar-Usina {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-usina--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-usina: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-usina' `
        -Script (Join-Path $PSScriptRoot 'clivus-usina.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 9999)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-usina terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^USINA (\d+) fileira\(s\), (\d+) mesa\(s\), (\d+) módulo\(s\)') {
        $problemas.Add("clivus-usina: a usina nao foi processada. Veja $($r.Saida)")
        return $false
    }

    $fileiras = [int] $Matches[1]
    $mesas = [int] $Matches[2]
    $modulos = [int] $Matches[3]

    if ($r.Texto -notmatch 'CLIVUS_USINA_DESENHO pilares=(\d+) faces=(\d+) contornos=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-usina: nao consegui ler as entidades em LISP. Veja $($r.Saida)")
        return $false
    }

    $pilares = [int] $Matches[1]
    $faces = [int] $Matches[2]
    $contornos = [int] $Matches[3]
    $zmin = [double]::Parse($Matches[4], $invariante)
    $zmax = [double]::Parse($Matches[5], $invariante)

    # Uma area de 100 x 100 com pitch 6 da mais de dez fileiras de seis mesas.
    if ($fileiras -lt 10 -or $mesas -lt 50) {
        $problemas.Add("clivus-usina: so $fileiras fileira(s) e $mesas mesa(s). Veja $($r.Saida)")
        return $false
    }

    if ($contornos -ne $mesas -or $pilares -ne 7 * $mesas -or $faces -ne 28 * $mesas -or $modulos -ne 28 * $mesas) {
        $problemas.Add(
            "clivus-usina: $mesas mesa(s) deviam dar $mesas contornos, $(7 * $mesas) pilares e $(28 * $mesas) faces; " +
            "o desenho tem $contornos, $pilares e $faces, e o relatorio diz $modulos modulos. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m\s+\(desn') {
        $problemas.Add("clivus-usina: nao achei as cotas do resumo do terreno. Veja $($r.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    if ($zmin -lt $minima -or $zmax -gt ($maxima + 5)) {
        $problemas.Add(
            "clivus-usina: os topos dos pilares vao de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). " +
            "Veja $($r.Saida)")
        return $false
    }

    # Passo 8.8: gerar nao analisa.
    if ($r.Texto -notmatch 'CLIVUS_USINA_LIMPA cotas=(\d+) setas=(\d+) analise=(\d+) cores=(\d+)') {
        $problemas.Add("clivus-usina: nao consegui ler a conferencia do 8.8 em LISP. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] + [int] $Matches[2] + [int] $Matches[3] + [int] $Matches[4] -ne 0) {
        $problemas.Add(
            "clivus-usina: gerar nao pode analisar (8.8), e o desenho tem $($Matches[1]) cota(s), $($Matches[2]) seta(s), " +
            "$($Matches[3]) entidade(s) nas camadas de analise e $($Matches[4]) peca(s) pintada(s). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -match 'tempo: motor ([\d,]+) s, desenho ([\d,]+) s') {
        Write-Host "  (usina: $mesas mesas em $fileiras fileiras, sem analise; motor $($Matches[1]) s, desenho $($Matches[2]) s)" -ForegroundColor DarkGray
    }

    return $true
}

<#
    A exportacao para o PVsyst (6.2): processa uma fileira, exporta em DAE e
    LE O ARQUIVO: XML valido, uma geometria por face do desenho, material com
    o nome da camada, unidade metro, e o primeiro vertice da primeira face do
    desenho (menos a origem local lida do CABECALHO do arquivo, que tem que
    ser a mesma que o comando disse) esta na geometria cujo id e o GUID
    daquela face (lido do XData em LISP), ao milimetro. O numero de faces
    vem do LISP, nao do relatorio do plugin.
#>
function Testar-Exportar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-exportar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-exportar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $dae = Join-Path $saida 'nivel2-clivus-exportar.dae'
    if (Test-Path $dae) { Remove-Item $dae -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-exportar' `
        -Script (Join-Path $PSScriptRoot 'clivus-exportar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
            '{{DAE}}'  = $dae
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-exportar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^EXPORTAR (\d+) face\(s\) de módulo gravada\(s\)') {
        $problemas.Add("clivus-exportar: o comando nao gravou. Veja $($r.Saida)")
        return $false
    }

    $relatadas = [int] $Matches[1]

    if ($r.Texto -notmatch 'ORIGEM E=(-?[\d.E+-]+) N=(-?[\d.E+-]+) Z=(-?[\d.E+-]+)') {
        $problemas.Add("clivus-exportar: o comando nao disse a origem local. Veja $($r.Saida)")
        return $false
    }

    $origemX = [double]::Parse($Matches[1], $invariante)
    $origemY = [double]::Parse($Matches[2], $invariante)
    $origemZ = [double]::Parse($Matches[3], $invariante)

    if ($r.Texto -notmatch 'CLIVUS_EXPORTAR_LISP faces=(\d+) v0=(-?[\d.]+),(-?[\d.]+),(-?[\d.]+) guid=([0-9A-Fa-f-]+)') {
        $problemas.Add("clivus-exportar: nao consegui ler as faces em LISP. Veja $($r.Saida)")
        return $false
    }

    $faces = [int] $Matches[1]
    $v0xDesenho = [double]::Parse($Matches[2], $invariante)
    $v0yDesenho = [double]::Parse($Matches[3], $invariante)
    $v0zDesenho = [double]::Parse($Matches[4], $invariante)
    $guid0 = $Matches[5].Replace('-', '').ToLowerInvariant()

    if ($faces -lt 1 -or $relatadas -ne $faces) {
        $problemas.Add("clivus-exportar: o desenho tem $faces face(s) e o comando relatou $relatadas. Veja $($r.Saida)")
        return $false
    }

    if (-not (Test-Path $dae)) {
        $problemas.Add("clivus-exportar: o arquivo $dae nao foi criado. Veja $($r.Saida)")
        return $false
    }

    try {
        [xml] $xml = Get-Content $dae -Raw -Encoding UTF8
    }
    catch {
        $problemas.Add("clivus-exportar: o DAE nao e XML valido: $($_.Exception.Message)")
        return $false
    }

    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('c', 'http://www.collada.org/2005/11/COLLADASchema')

    $geometrias = @($xml.SelectNodes('//c:library_geometries/c:geometry', $ns))
    $nos = @($xml.SelectNodes('//c:visual_scene/c:node', $ns))
    $materiais = @($xml.SelectNodes('//c:library_materials/c:material', $ns))
    $unidade = $xml.SelectSingleNode('//c:asset/c:unit', $ns)

    if ($geometrias.Count -ne $faces -or $nos.Count -ne $faces) {
        $problemas.Add("clivus-exportar: o desenho tem $faces face(s), o DAE tem $($geometrias.Count) geometria(s) e $($nos.Count) no(s)")
        return $false
    }

    if ($materiais.Count -ne 1 -or $materiais[0].name -ne 'CLIVUS_FACE') {
        $problemas.Add("clivus-exportar: esperava um material chamado CLIVUS_FACE no DAE")
        return $false
    }

    if ($null -eq $unidade -or $unidade.meter -ne '1') {
        $problemas.Add("clivus-exportar: a unidade do DAE nao e o metro")
        return $false
    }

    # A origem que vale e a do CABECALHO do arquivo (e o que o usuario tera
    # em maos); ela tem que ser a mesma que o comando disse.
    $comentario = $xml.SelectSingleNode('//c:asset/c:contributor/c:comments', $ns)
    if ($null -eq $comentario -or $comentario.InnerText -notmatch 'E=(-?[\d.E+-]+) N=(-?[\d.E+-]+) Z=(-?[\d.E+-]+)\.') {
        $problemas.Add("clivus-exportar: o cabecalho do DAE nao traz a origem local")
        return $false
    }

    $arqX = [double]::Parse($Matches[1], $invariante)
    $arqY = [double]::Parse($Matches[2], $invariante)
    $arqZ = [double]::Parse($Matches[3], $invariante)

    if ([math]::Abs($arqX - $origemX) -gt 1e-6 -or [math]::Abs($arqY - $origemY) -gt 1e-6 -or [math]::Abs($arqZ - $origemZ) -gt 1e-6) {
        $problemas.Add("clivus-exportar: a origem do cabecalho ($arqX, $arqY, $arqZ) difere da dita pelo comando ($origemX, $origemY, $origemZ)")
        return $false
    }

    $v0x = $v0xDesenho - $arqX
    $v0y = $v0yDesenho - $arqY
    $v0z = $v0zDesenho - $arqZ

    # O primeiro vertice da primeira face do desenho, relativo a origem, tem
    # que estar NA GEOMETRIA DAQUELA FACE (id = GUID do XData), ao milimetro.
    $fa = $xml.SelectSingleNode("//c:float_array[@id='face-$guid0-pos-array']", $ns)
    if ($null -eq $fa) {
        $problemas.Add("clivus-exportar: o DAE nao tem geometria para a face $guid0 do desenho")
        return $false
    }

    $achou = $false
    $n = @($fa.InnerText.Trim() -split '\s+' | ForEach-Object { [double]::Parse($_, $invariante) })
    for ($i = 0; $i + 2 -lt $n.Count; $i += 3) {
        if ([math]::Abs($n[$i] - $v0x) -lt 0.001 -and [math]::Abs($n[$i + 1] - $v0y) -lt 0.001 -and [math]::Abs($n[$i + 2] - $v0z) -lt 0.001) {
            $achou = $true
            break
        }
    }

    if (-not $achou) {
        $problemas.Add("clivus-exportar: o vertice ($v0x, $v0y, $v0z) do desenho (menos a origem) nao esta na geometria da face $guid0")
        return $false
    }

    Write-Host "  (exportar: $faces faces no DAE, origem E=$origemX N=$origemY Z=$origemZ)" -ForegroundColor DarkGray
    return $true
}

<#
    O estado sujo (7.1): processa uma fileira, suja a primeira mesa e le do
    desenho: exatamente um contorno com suja=1 e motivo no XData; contorno,
    pilares e modulos dessa mesa vermelhos; nenhuma face vermelha; e o
    CLIVUS_ESTADO conta uma suja. Tudo lido em LISP pelo XData, nunca por camada.
#>
function Testar-Sujo {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sujo--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-sujo: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sujo' `
        -Script (Join-Path $PSScriptRoot 'clivus-sujo.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-sujo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'PENDENTE_GUID ([0-9a-fA-F-]+) pecas=(\d+)') {
        $problemas.Add("clivus-sujo: o comando nao sujou a mesa. Veja $($r.Saida)")
        return $false
    }

    $guidRelatado = $Matches[1].ToLowerInvariant()
    $pecasRelatadas = [int] $Matches[2]

    if ($r.Texto -notmatch '(?m)^ESTADO (\d+) mesa\(s\), (\d+) limpa\(s\), (\d+) pendente\(s\)') {
        $problemas.Add("clivus-sujo: CLIVUS_ESTADO nao respondeu. Veja $($r.Saida)")
        return $false
    }

    $mesasEstado = [int] $Matches[1]
    $limpasEstado = [int] $Matches[2]
    $sujasEstado = [int] $Matches[3]

    if ($r.Texto -notmatch 'CLIVUS_SUJO contornos=(\d+) sujas=(\d+) motivo=(sim|nao) pecas=(\d+) pecasverm=(\d+) faces=(\d+) facesverm=(\d+) outrasverm=(\d+) guid=([0-9a-fA-F-]+)') {
        $problemas.Add("clivus-sujo: nao consegui ler o estado em LISP. Veja $($r.Saida)")
        return $false
    }

    $contornos = [int] $Matches[1]
    $sujas = [int] $Matches[2]
    $motivo = $Matches[3]
    $pecas = [int] $Matches[4]
    $pecasVermelhas = [int] $Matches[5]
    $faces = [int] $Matches[6]
    $facesVermelhas = [int] $Matches[7]
    $outrasVermelhas = [int] $Matches[8]
    $guidLisp = $Matches[9].ToLowerInvariant()

    if ($guidLisp -ne $guidRelatado) {
        $problemas.Add("clivus-sujo: o comando disse ter sujado $guidRelatado e o XData mostra $guidLisp suja. Veja $($r.Saida)")
        return $false
    }

    if ($outrasVermelhas -ne 0) {
        $problemas.Add("clivus-sujo: $outrasVermelhas peca(s) de outras mesas ficaram vermelhas. Veja $($r.Saida)")
        return $false
    }

    if ($sujas -ne 1 -or $motivo -ne 'sim') {
        $problemas.Add("clivus-sujo: esperava exatamente 1 contorno sujo com motivo no XData; ha $sujas (motivo: $motivo). Veja $($r.Saida)")
        return $false
    }

    if ($sujasEstado -ne 1 -or $mesasEstado -ne $contornos -or $limpasEstado -ne ($contornos - 1)) {
        $problemas.Add("clivus-sujo: CLIVUS_ESTADO disse $mesasEstado mesa(s), $limpasEstado limpa(s), $sujasEstado suja(s); o desenho tem $contornos contorno(s). Veja $($r.Saida)")
        return $false
    }

    # Uma mesa de exemplo tem contorno + 7 pilares + 28 modulos = 36 pecas pintaveis.
    if ($pecas -lt 3 -or $pecasVermelhas -ne $pecas -or $pecasRelatadas -ne $pecas) {
        $problemas.Add("clivus-sujo: a mesa suja tem $pecas peca(s) pintavel(is), $pecasVermelhas vermelha(s), comando relatou $pecasRelatadas. Veja $($r.Saida)")
        return $false
    }

    if ($faces -lt 1 -or $facesVermelhas -ne 0) {
        $problemas.Add("clivus-sujo: a mesa suja tem $faces face(s) e $facesVermelhas vermelha(s); face nunca se pinta. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (sujo: 1 de $contornos mesas suja, $pecas pecas vermelhas, $faces faces intactas)" -ForegroundColor DarkGray
    return $true
}

<#
    O vigia (7.2): processa uma fileira e, com MOVE e ERASE do proprio
    AutoCAD, toca um pilar da mesa A e apaga o contorno da mesa B. Le do
    desenho: A suja no XData com o motivo "movida ou editada" e as pecas
    vermelhas; nenhuma outra mesa suja; um contorno a menos; CLIVUS_ESTADO
    lista B como removida; depois de U, o contorno de B volta, sai das
    removidas e B continua limpa.
#>
function Testar-Vigia {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-vigia--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-vigia: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-vigia' `
        -Script (Join-Path $PSScriptRoot 'clivus-vigia.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-vigia terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_VIGIA sujaa=(\d) motivoa=(.*?) pecasa=(\d+) vermelhasa=(\d+) outrassujas=(\d+) contornos=(\d+) labelb=([A-Za-z0-9.]+) sujab=([0-9a-z]+)') {
        $problemas.Add("clivus-vigia: nao consegui ler o resultado em LISP. Veja $($r.Saida)")
        return $false
    }

    $sujaA = $Matches[1]
    $motivoA = $Matches[2]
    $pecasA = [int] $Matches[3]
    $vermelhasA = [int] $Matches[4]
    $outrasSujas = [int] $Matches[5]
    $contornos = [int] $Matches[6]
    $labelB = $Matches[7]
    $sujaB = $Matches[8]

    if ($sujaA -ne '1' -or $motivoA -ne 'movida ou editada') {
        $problemas.Add("clivus-vigia: depois do MOVE a mesa A esta suja=$sujaA motivo='$motivoA'. Veja $($r.Saida)")
        return $false
    }

    if ($pecasA -lt 3 -or $vermelhasA -ne $pecasA) {
        $problemas.Add("clivus-vigia: a mesa A tem $pecasA peca(s) e $vermelhasA vermelha(s). Veja $($r.Saida)")
        return $false
    }

    if ($outrasSujas -ne 0) {
        $problemas.Add("clivus-vigia: $outrasSujas outra(s) mesa(s) ficaram sujas sem motivo. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch "(?m)^VIGIA \S+ pendente \(movida ou editada, comando MOVE\)") {
        $problemas.Add("clivus-vigia: o vigia nao anunciou a mesa movida. Veja $($r.Saida)")
        return $false
    }

    # Dois ESTADOs: depois do ERASE e depois do U.
    $estados = @([regex]::Matches($r.Texto, '(?m)^ESTADO (\d+) mesa\(s\), (\d+) limpa\(s\), (\d+) pendente\(s\)(, (\d+) com peças órfãs \(sem contorno\))?[\s\S]*?(\d+) removida\(s\)'))

    if ($estados.Count -ne 2) {
        $problemas.Add("clivus-vigia: esperava dois CLIVUS_ESTADO, achei $($estados.Count). Veja $($r.Saida)")
        return $false
    }

    $depoisDoErase = $estados[0]
    $depoisDoU = $estados[1]

    $orfas = if ($depoisDoErase.Groups[5].Success) { [int] $depoisDoErase.Groups[5].Value } else { 0 }
    if ([int] $depoisDoErase.Groups[1].Value -ne ($contornos - 1) -or [int] $depoisDoErase.Groups[3].Value -ne 1 -or $orfas -ne 1 -or [int] $depoisDoErase.Groups[6].Value -ne 1) {
        $problemas.Add("clivus-vigia: depois do ERASE, CLIVUS_ESTADO disse '$($depoisDoErase.Value)'; esperava $($contornos - 1) mesa(s), 1 suja, 1 orfa, 1 removida. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch "1 removida\(s\):\s*\r?\n\s*$([regex]::Escape($labelB)) removida em") {
        $problemas.Add("clivus-vigia: CLIVUS_ESTADO nao listou $labelB como removida. Veja $($r.Saida)")
        return $false
    }

    $orfasU = if ($depoisDoU.Groups[5].Success) { [int] $depoisDoU.Groups[5].Value } else { 0 }
    if ([int] $depoisDoU.Groups[1].Value -ne $contornos -or [int] $depoisDoU.Groups[3].Value -ne 1 -or $orfasU -ne 0 -or [int] $depoisDoU.Groups[6].Value -ne 0) {
        $problemas.Add("clivus-vigia: depois do U, CLIVUS_ESTADO disse '$($depoisDoU.Value)'; esperava $contornos mesa(s), 1 suja, 0 orfa, 0 removida. Veja $($r.Saida)")
        return $false
    }

    if ($sujaB -ne '0') {
        $problemas.Add("clivus-vigia: depois do U a mesa B esta suja=$sujaB; o desfazer nao pode sujar. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (vigia: A suja por MOVE com $pecasA pecas vermelhas; $labelB removida por ERASE e de volta com U)" -ForegroundColor DarkGray
    return $true
}

<#
    O Refazer: processa uma fileira, manda refazer a area inteira e le do
    desenho que a fileira antiga sumiu (7 pilares e 28 faces por contorno,
    nada em dobro), que ha mais de uma fileira, e que toda nota (cota,
    aviso) aponta para uma mesa existente.
#>
function Testar-Refazer {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-refazer--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-refazer: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-refazer' `
        -Script (Join-Path $PSScriptRoot 'clivus-refazer.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-refazer terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^REFAZER (\d+) mesa\(s\) apagada\(s\)') {
        $problemas.Add("clivus-refazer: o comando nao apagou. Veja $($r.Saida)")
        return $false
    }

    $apagadas = [int] $Matches[1]

    if ($r.Texto -notmatch 'CLIVUS_REFAZER antes=(\d+) contornos=(\d+) pilares=(\d+) faces=(\d+) notas=(\d+) notasorfas=(\d+)') {
        $problemas.Add("clivus-refazer: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $antes = [int] $Matches[1]
    $contornos = [int] $Matches[2]
    $pilares = [int] $Matches[3]
    $faces = [int] $Matches[4]
    $notas = [int] $Matches[5]
    $orfas = [int] $Matches[6]

    if ($apagadas -lt 1 -or $antes -lt 1) {
        $problemas.Add("clivus-refazer: nada havia para apagar ($antes entidades, $apagadas mesas). Veja $($r.Saida)")
        return $false
    }

    if ($contornos -lt 10 -or $pilares -ne 7 * $contornos -or $faces -ne 28 * $contornos) {
        $problemas.Add("clivus-refazer: $contornos contorno(s), $pilares pilar(es), $faces face(s); esperava 7 e 28 por contorno. Veja $($r.Saida)")
        return $false
    }

    # "Nada em dobro": o que esta no desenho e exatamente o que a usina
    # relatou ter desenhado; a fileira antiga nao sobrou.
    # A ultima linha "desenhado" e a da usina (a primeira e da fileira inicial).
    $resumos = @([regex]::Matches($r.Texto, 'desenhado: (\d+) mesa\(s\), (\d+) pilar\(es\), (\d+) módulo\(s\) com face'))
    if ($resumos.Count -lt 2) {
        $problemas.Add("clivus-refazer: esperava dois resumos de desenho (fileira e usina). Veja $($r.Saida)")
        return $false
    }

    $usina = $resumos[$resumos.Count - 1]
    if ([int] $usina.Groups[1].Value -ne $contornos -or [int] $usina.Groups[2].Value -ne $pilares -or [int] $usina.Groups[3].Value -ne $faces) {
        $problemas.Add("clivus-refazer: a usina desenhou $($usina.Groups[1].Value) mesa(s), $($usina.Groups[2].Value) pilar(es), $($usina.Groups[3].Value) modulo(s), mas o desenho tem $contornos, $pilares e ${faces}: sobrou algo da fileira antiga. Veja $($r.Saida)")
        return $false
    }

    # Desde o 8.8 o refazer desenha sem analise: as cotas que a fileira
    # antiga tinha (o script as pos com CLIVUS_ALTURAS_REGERAR) foram apagadas
    # com ela, e nenhuma nasceu de novo. Antes do 8.8 a conferencia era
    # "toda nota com dona e pelo menos uma por pilar".
    if ($notas -ne 0 -or $orfas -ne 0) {
        $problemas.Add("clivus-refazer: $notas nota(s), $orfas orfa(s); o refazer apaga as cotas antigas e nao poe novas (8.8). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (refazer: $apagadas mesa(s) apagada(s), $contornos redesenhada(s), cotas antigas apagadas e nenhuma nova)" -ForegroundColor DarkGray
    return $true
}

<#
    Apagar tudo (29/09/2026): a camada da area nasce branca antes do
    NETLOAD, como nos desenhos antigos; area, alinhamento, uma fileira e um
    grupo com todas as mesas; CLIVUS_APAGAR_TUDO_AUTO. Le do desenho que so
    ficaram a area e o alinhamento, que a marca do grupo sumiu, e que as
    camadas ficaram laranja (ACI 30) e amarela (ACI 2).
#>
function Testar-ApagarTudo {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-apagar-tudo--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-apagar-tudo: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-apagar-tudo' `
        -Script (Join-Path $PSScriptRoot 'clivus-apagar-tudo.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-apagar-tudo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^APAGAR (\d+) mesa\(s\) apagada\(s\)') {
        $problemas.Add("clivus-apagar-tudo: o comando nao apagou. Veja $($r.Saida)")
        return $false
    }

    $apagadas = [int] $Matches[1]

    if ($r.Texto -notmatch 'Grupo\(s\) sem mesa, apagado\(s\): Grupo do apagar') {
        $problemas.Add("clivus-apagar-tudo: o grupo que ficou sem mesa nao foi apagado. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_APAGAR_LISP antes=(\d+) marcasAntes=(\d+) areas=(\d+) alinhamentos=(\d+) sobrou=(\d+) marcas=(\d+) corArea=(\d+) corAlinhamento=(\d+)') {
        $problemas.Add("clivus-apagar-tudo: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $antes = [int] $Matches[1]; $marcasAntes = [int] $Matches[2]
    $areas = [int] $Matches[3]; $alinhamentos = [int] $Matches[4]
    $sobrou = [int] $Matches[5]; $marcas = [int] $Matches[6]
    $corArea = [int] $Matches[7]; $corAlinhamento = [int] $Matches[8]

    if ($apagadas -lt 1 -or $antes -lt 1 -or $marcasAntes -lt 1) {
        $problemas.Add("clivus-apagar-tudo: nada havia para apagar ($antes pecas, $apagadas mesas, $marcasAntes marcas). Veja $($r.Saida)")
        return $false
    }

    if ($sobrou -ne 0 -or $marcas -ne 0) {
        $problemas.Add("clivus-apagar-tudo: sobraram $sobrou peca(s) e $marcas entidade(s) de marca de grupo. Veja $($r.Saida)")
        return $false
    }

    if ($areas -ne 1 -or $alinhamentos -ne 1) {
        $problemas.Add("clivus-apagar-tudo: a area e o alinhamento tinham que ficar ($areas area(s), $alinhamentos alinhamento(s)). Veja $($r.Saida)")
        return $false
    }

    if ($corArea -ne 30 -or $corAlinhamento -ne 2) {
        $problemas.Add("clivus-apagar-tudo: cores das camadas $corArea (area, esperava 30) e $corAlinhamento (alinhamento, esperava 2). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (apagar tudo: $apagadas mesa(s), $antes peca(s) e o grupo; area laranja e alinhamento amarelo ficaram)" -ForegroundColor DarkGray
    return $true
}

<#
    Declividade (29/09/2026): uma fileira, a analise ligada em graus e em
    porcentagem, uma mesa recalculada e a analise desligada. Um texto por
    mesa, com o valor que o contorno da, nas duas unidades; nada em dobro;
    a mesa recalculada nasce com a seta; nada fora da cota das mesas;
    desligar apaga tudo.
#>
function Testar-Declividade {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-declividade--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-declividade: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-declividade' `
        -Script (Join-Path $PSScriptRoot 'clivus-declividade.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-declividade terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_DECLIV contornos=(\d+) graus=(\d+) okg=(\d+) porc=(\d+) okp=(\d+) recalc=(\d+) okr=(\d+) desligado=(\d+) fora=(\d+)') {
        $problemas.Add("clivus-declividade: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $contornos = [int] $Matches[1]
    $graus = [int] $Matches[2]; $okg = [int] $Matches[3]
    $porc = [int] $Matches[4]; $okp = [int] $Matches[5]
    $recalc = [int] $Matches[6]; $okr = [int] $Matches[7]
    $desligado = [int] $Matches[8]; $fora = [int] $Matches[9]

    if ($contornos -lt 2 -or $graus -ne $contornos -or $okg -ne $graus) {
        $problemas.Add("clivus-declividade: em graus, $graus texto(s) para $contornos mesa(s), $okg com o valor do contorno. Veja $($r.Saida)")
        return $false
    }

    if ($porc -ne $contornos -or $okp -ne $porc) {
        $problemas.Add("clivus-declividade: em porcentagem, $porc texto(s) para $contornos mesa(s), $okp com o valor do contorno (texto em dobro ou unidade errada). Veja $($r.Saida)")
        return $false
    }

    if ($recalc -ne $contornos -or $okr -ne $recalc) {
        $problemas.Add("clivus-declividade: depois do recalcular, $recalc texto(s) para $contornos mesa(s), $okr certos: a mesa recalculada nao nasceu com a seta. Veja $($r.Saida)")
        return $false
    }

    if ($fora -ne 0) {
        $problemas.Add("clivus-declividade: $fora ponto(s) da seta fora da cota das mesas (regra sagrada 5). Veja $($r.Saida)")
        return $false
    }

    if ($desligado -ne 0) {
        $problemas.Add("clivus-declividade: desligada, ainda ha $desligado entidade(s) na camada. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (declividade: $contornos mesas em graus e em porcentagem, valores do contorno, recalculada com seta, desligada limpa)" -ForegroundColor DarkGray
    return $true
}

<#
    Pontas a mao (27/09/2026): processa uma fileira e muda as pontas da F1.2
    (PB 0,55 no primeiro pilar e 1,10 no ultimo); depois trava a primeira e
    muda so a ultima para 0,70. A mesa continua a mesma (um contorno, mesmo
    GUID, 7 pilares), as alturas medidas nos pilares batem ao centimetro, e
    as pontas ficam gravadas no contorno.
#>
function Testar-Pontas {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-pontas--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-pontas: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-pontas' `
        -Script (Join-Path $PSScriptRoot 'clivus-pontas.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-pontas terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $leituras = @{}

    foreach ($m in [regex]::Matches($r.Texto, 'CLIVUS_PONTAS_LISP (\w+) guid=([-0-9a-fA-F]+) cont=(\d+) pilares=(\d+) pb1=([-\d.]+) pb7=([-\d.]+) m1=([-\d.]*) m2=([-\d.]*)')) {
        $leituras[$m.Groups[1].Value] = $m
    }

    foreach ($fase in 'antes', 'duas', 'travada') {
        if (-not $leituras.ContainsKey($fase)) {
            $problemas.Add("clivus-pontas: nao li a fase '$fase' em LISP. Veja $($r.Saida)")
            return $false
        }
    }

    function Numero($texto) { [double]::Parse($texto, $invariante) }

    $guid = $leituras['antes'].Groups[2].Value

    foreach ($fase in 'duas', 'travada') {
        $l = $leituras[$fase]

        if ($l.Groups[2].Value -ne $guid -or $l.Groups[3].Value -ne '1' -or $l.Groups[4].Value -ne '7') {
            $problemas.Add("clivus-pontas ($fase): a F1.2 tem guid $($l.Groups[2].Value) (antes $guid), $($l.Groups[3].Value) contorno(s), $($l.Groups[4].Value) pilar(es); esperava o mesmo guid, 1 e 7. Veja $($r.Saida)")
            return $false
        }
    }

    $antes = $leituras['antes']
    $duas = $leituras['duas']
    $travada = $leituras['travada']

    if ([math]::Abs((Numero $duas.Groups[5].Value) - 0.55) -gt 0.01 -or [math]::Abs((Numero $duas.Groups[6].Value) - 1.10) -gt 0.01) {
        $problemas.Add("clivus-pontas: pedi PB 0,55 e 1,10; os pilares 1 e 7 ficaram com $($duas.Groups[5].Value) e $($duas.Groups[6].Value). Veja $($r.Saida)")
        return $false
    }

    if ([math]::Abs((Numero $travada.Groups[5].Value) - 0.55) -gt 0.01 -or [math]::Abs((Numero $travada.Groups[6].Value) - 0.70) -gt 0.01) {
        $problemas.Add("clivus-pontas: travei a primeira (0,55) e pedi 0,70 na ultima; ficaram $($travada.Groups[5].Value) e $($travada.Groups[6].Value). Veja $($r.Saida)")
        return $false
    }

    if ($antes.Groups[7].Value -ne '' -or [math]::Abs((Numero $travada.Groups[7].Value) - 0.55) -gt 0.01 -or [math]::Abs((Numero $travada.Groups[8].Value) - 0.70) -gt 0.01) {
        $problemas.Add("clivus-pontas: as pontas gravadas no contorno sao '$($antes.Groups[7].Value)' antes e $($travada.Groups[7].Value)/$($travada.Groups[8].Value) depois; esperava vazio e 0,55/0,70. Veja $($r.Saida)")
        return $false
    }

    # Regra sagrada 5, lida da entidade e nao do relatorio: topo do pilar
    # menos o terreno gravado fecha com a altura livre; o terreno sob os
    # pilares e as cotas da borda baixa caem dentro da faixa do terreno.
    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-pontas: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $brasil = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $terrenoMin = [double]::Parse($Matches[1], $brasil)
    $terrenoMax = [double]::Parse($Matches[2], $brasil)

    foreach ($fase in 'duas', 'travada') {
        $c = [regex]::Match($r.Texto, "CLIVUS_PONTAS_COTA $fase z0=(-?[\d.]+) z1=(-?[\d.]+) chaoMin=(-?[\d.]+) chaoMax=(-?[\d.]+) fecha=(-?[\d.]+)")

        if (-not $c.Success) {
            $problemas.Add("clivus-pontas ($fase): nao li as cotas da entidade em LISP. Veja $($r.Saida)")
            return $false
        }

        $z0 = Numero $c.Groups[1].Value
        $z1 = Numero $c.Groups[2].Value
        $chaoMin = Numero $c.Groups[3].Value
        $chaoMax = Numero $c.Groups[4].Value
        $fecha = Numero $c.Groups[5].Value

        if ($fecha -gt 0.005 -or $chaoMin -lt $terrenoMin - 0.01 -or $chaoMax -gt $terrenoMax + 0.01 -or
            $z0 -lt $terrenoMin -or $z0 -gt $terrenoMax + 5 -or $z1 -lt $terrenoMin -or $z1 -gt $terrenoMax + 5) {
            $problemas.Add("clivus-pontas ($fase): cota fora do terreno ($terrenoMin a $terrenoMax): borda baixa $z0/$z1, chao $chaoMin a $chaoMax, topo-chao-P3 = $fecha. Veja $($r.Saida)")
            return $false
        }
    }

    Write-Host "  (pontas: F1.2 com PB $($duas.Groups[5].Value)/$($duas.Groups[6].Value), depois $($travada.Groups[5].Value)/$($travada.Groups[6].Value) com a primeira travada)" -ForegroundColor DarkGray
    return $true
}

<#
    Pintar estouros (27/09/2026): processa uma fileira e repinta todas as
    mesas como estao. Mesmas mesas, mesmos GUIDs, mesmas pecas: repintar nao
    move, nao duplica e nao troca identidade.
#>
function Testar-Pintar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-pintar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-pintar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-pintar' `
        -Script (Join-Path $PSScriptRoot 'clivus-pintar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-pintar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^PINTAR (\d+) mesa\(s\) repintada\(s\)') {
        $problemas.Add("clivus-pintar: o comando nao repintou. Veja $($r.Saida)")
        return $false
    }

    $antes = [regex]::Match($r.Texto, 'CLIVUS_PINTAR_LISP antes mesas=(\d+) pilares=(\d+) modulos=(\d+) faces=(\d+)')
    $depois = [regex]::Match($r.Texto, 'CLIVUS_PINTAR_LISP depois mesas=(\d+) pilares=(\d+) modulos=(\d+) faces=(\d+)')
    $guids = [regex]::Match($r.Texto, 'CLIVUS_PINTAR_GUIDS mesmos=(\d+) de=(\d+)')

    if (-not $antes.Success -or -not $depois.Success -or -not $guids.Success) {
        $problemas.Add("clivus-pintar: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $mesas = [int] $antes.Groups[1].Value

    if ($mesas -lt 2) {
        $problemas.Add("clivus-pintar: a fileira tem $mesas mesa(s); esperava pelo menos 2. Veja $($r.Saida)")
        return $false
    }

    foreach ($g in 1..4) {
        if ($antes.Groups[$g].Value -ne $depois.Groups[$g].Value) {
            $problemas.Add("clivus-pintar: antes $($antes.Value), depois $($depois.Value); repintar mudou a contagem. Veja $($r.Saida)")
            return $false
        }
    }

    if ($guids.Groups[1].Value -ne $guids.Groups[2].Value -or [int] $guids.Groups[2].Value -ne $mesas) {
        $problemas.Add("clivus-pintar: $($guids.Groups[1].Value) de $($guids.Groups[2].Value) GUIDs de mesa sobreviveram ao repintar. Veja $($r.Saida)")
        return $false
    }

    # "Nao move nada", lido da entidade: a cota dos cantos da borda baixa de
    # cada mesa e a mesma antes e depois de repintar.
    $cotas = [regex]::Match($r.Texto, 'CLIVUS_PINTAR_COTAS dz=(-?[\d.]+)')

    if (-not $cotas.Success -or [double]::Parse($cotas.Groups[1].Value, $invariante) -gt 0.0005) {
        $problemas.Add("clivus-pintar: a cota da borda baixa mudou ao repintar (dz=$($cotas.Groups[1].Value) m). Veja $($r.Saida)")
        return $false
    }

    # Um grupo por mesa (o clique pega a mesa inteira), e nenhum vazio
    # sobrando dos que foram repintados.
    $grupos = [regex]::Match($r.Texto, 'CLIVUS_PINTAR_GRUPOS (\d+)')

    if (-not $grupos.Success -or [int] $grupos.Groups[1].Value -ne $mesas) {
        $problemas.Add("clivus-pintar: $($grupos.Groups[1].Value) grupo(s) anonimo(s) para $mesas mesa(s); esperava um por mesa. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (pintar: $mesas mesas repintadas com os mesmos GUIDs, um grupo por mesa, $($depois.Groups[2].Value) pilares e $($depois.Groups[3].Value) modulos, nada em dobro)" -ForegroundColor DarkGray
    return $true
}

<#
    Recalcular (7.3/7.4): processa uma fileira, suja a primeira mesa e manda
    recalcular as sujas. O comando diz o GUID sujado; depois a mesa com esse
    GUID tem que existir limpa, com 7 pilares, 28 modulos e 28 faces, sem
    peca vermelha; o total de contornos nao muda; CLIVUS_ESTADO conta 0 sujas.
    Lido em LISP pelo XData (o proprio clivus-sujo.scr tem o padrao).
#>
function Testar-Recalcular {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-recalcular--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-recalcular: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-recalcular' `
        -Script (Join-Path $PSScriptRoot 'clivus-recalcular.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-recalcular terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'PENDENTE_GUID ([0-9a-fA-F-]+) pecas=(\d+)') {
        $problemas.Add("clivus-recalcular: nada foi sujado. Veja $($r.Saida)")
        return $false
    }

    $guid = $Matches[1]

    if ($r.Texto -notmatch '(?m)^RECALCULAR (\S+) refeita onde está') {
        $problemas.Add("clivus-recalcular: o comando nao refez a mesa. Veja $($r.Saida)")
        return $false
    }

    # Regra sagrada 6 (29/09/2026): a mesa recalculada sozinha fica com as
    # pontas presas na PB das vizinhas da fileira.
    if ($r.Texto -notmatch 'pontas presas nas vizinhas F\d+\.\d+ \(PB \d+,\d\d\)') {
        $problemas.Add("clivus-recalcular: a mesa nao prendeu as pontas nas vizinhas. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^RECALCULAR 1 de 1 mesa\(s\) recalculada\(s\)') {
        $problemas.Add("clivus-recalcular: esperava 1 de 1. Veja $($r.Saida)")
        return $false
    }

    # Depois do recalcular, ESTADO conta zero sujas.
    $estados = @([regex]::Matches($r.Texto, '(?m)^ESTADO (\d+) mesa\(s\), (\d+) limpa\(s\), (\d+) pendente\(s\)'))
    if ($estados.Count -lt 1 -or [int] $estados[$estados.Count - 1].Groups[3].Value -ne 0) {
        $problemas.Add("clivus-recalcular: CLIVUS_ESTADO ainda conta mesa suja. Veja $($r.Saida)")
        return $false
    }

    $mesasEstado = [int] $estados[$estados.Count - 1].Groups[1].Value

    if ($r.Texto -notmatch 'CLIVUS_RECALC_LISP antes=(\d+) contornos=(\d+) guid=([0-9a-fA-F-]+) cont=(\d+) suja=(\S+) pilares=(\d+) modulos=(\d+) faces=(\d+) vermelhas=(\d+) vermelhaslimpa=(-?\d+) notas=(\d+) viradas=(\d+)') {
        $problemas.Add("clivus-recalcular: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    # A reprovacao de 26/09: os textos de cota da mesa recalculada nunca de
    # cabeca para baixo (rumo em (-90, 90]).
    if ([int] $Matches[11] -lt 21 -or [int] $Matches[12] -ne 0) {
        $problemas.Add("clivus-recalcular: esperava >= 21 textos de cota na mesa recalculada, nenhum virado; deu $($Matches[11]) e $($Matches[12]) virados. Veja $($r.Saida)")
        return $false
    }

    $antes = [int] $Matches[1]
    $contornos = [int] $Matches[2]
    $guidLisp = $Matches[3].ToLowerInvariant()
    $cont = [int] $Matches[4]
    $suja = $Matches[5]
    $pil = [int] $Matches[6]
    $mod = [int] $Matches[7]
    $fac = [int] $Matches[8]
    $verm = [int] $Matches[9]
    $vermLimpa = [int] $Matches[10]

    if ($guidLisp -ne $guid.ToLowerInvariant()) {
        $problemas.Add("clivus-recalcular: o LISP achou suja a mesa $guidLisp e o comando sujou $guid. Veja $($r.Saida)")
        return $false
    }

    if ($contornos -ne $antes -or $contornos -ne $mesasEstado -or $contornos -lt 2) {
        $problemas.Add("clivus-recalcular: $antes contorno(s) antes, $contornos depois, $mesasEstado no ESTADO. Veja $($r.Saida)")
        return $false
    }

    # Vermelho: o da sujeira some; o das analises (modulo abaixo da faixa
    # numa mesa marcada, 29/09/2026) volta igual ao da mesa antes de suja.
    if ($cont -ne 1 -or $suja -ne '0' -or $pil -ne 7 -or $mod -ne 28 -or $fac -ne 28 -or $vermLimpa -lt 0 -or $verm -ne $vermLimpa) {
        $problemas.Add("clivus-recalcular: a mesa $guidLisp depois do recalcular tem $cont contorno(s) (suja=$suja), $pil pilar(es), $mod modulo(s), $fac face(s), $verm vermelha(s); esperava 1/0/7/28/28/$vermLimpa (as vermelhas da mesa limpa). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (recalcular: mesa $($guid.Substring(0,8)) refeita limpa, pontas presas nas vizinhas, $verm peca(s) pintadas como antes de suja; $contornos mesas no desenho)" -ForegroundColor DarkGray
    return $true
}

<#
    A copia (7.5): copia a mesa A inteira com o COPY do AutoCAD e le do
    desenho: o GUID de A continua num contorno so, limpo; a copia tem
    contorno com GUID novo, sujo "copiada", com 7 pilares, 28 modulos e 28
    faces apontando para ela; nenhum GUID de peca se repete. Depois renomeia
    o bloco do pilar com sufixo e o CLIVUS_RENOMEAR devolve o nome padrao.
#>
function Testar-Copia {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-copia--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-copia: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-copia' `
        -Script (Join-Path $PSScriptRoot 'clivus-copia.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-copia terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^VIGIA cópia de (\S+): (\d+) peça\(s\) com identidade nova') {
        $problemas.Add("clivus-copia: o vigia nao deu identidade a copia. Veja $($r.Saida)")
        return $false
    }

    $pecasNovas = [int] $Matches[2]

    if ($r.Texto -notmatch 'CLIVUS_COPIA copiadas=(\d+) conta=(\d+) sujaa=(\S+) contb=(\d+) sujab=(\S+) motivob=(.*?) pilb=(\d+) modb=(\d+) facb=(\d+) repetidos=(\d+)') {
        $problemas.Add("clivus-copia: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $copiadas = [int] $Matches[1]
    $conta = [int] $Matches[2]
    $sujaA = $Matches[3]
    $contB = [int] $Matches[4]
    $sujaB = $Matches[5]
    $motivoB = $Matches[6]
    $pilB = [int] $Matches[7]
    $modB = [int] $Matches[8]
    $facB = [int] $Matches[9]
    $repetidos = [int] $Matches[10]

    if ($copiadas -lt 36 -or $pecasNovas -ne $copiadas) {
        $problemas.Add("clivus-copia: $copiadas entidade(s) copiada(s), $pecasNovas com identidade nova. Veja $($r.Saida)")
        return $false
    }

    if ($conta -ne 1 -or $sujaA -ne '0') {
        $problemas.Add("clivus-copia: a mesa original tem $conta contorno(s) com o GUID dela (suja=$sujaA); esperava 1, limpa. Veja $($r.Saida)")
        return $false
    }

    if ($contB -ne 1 -or $sujaB -ne '1' -or $motivoB -ne 'copiada' -or $pilB -ne 7 -or $modB -ne 28 -or $facB -ne 28) {
        $problemas.Add("clivus-copia: a copia tem $contB contorno(s), suja=$sujaB ($motivoB), $pilB pilar(es), $modB modulo(s), $facB face(s); esperava 1/1/copiada/7/28/28. Veja $($r.Saida)")
        return $false
    }

    if ($repetidos -ne 0) {
        $problemas.Add("clivus-copia: $repetidos GUID(s) de peca repetido(s) no desenho (regra sagrada 3). Veja $($r.Saida)")
        return $false
    }

    # U: a copia some, a original fica unica, nada registrado como removida.
    if ($r.Texto -notmatch 'CLIVUS_COPIA_U conta=(\d+) contb=(\d+) repetidos=(\d+)') {
        $problemas.Add("clivus-copia: nao consegui ler o desenho depois do U. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] -ne 1 -or [int] $Matches[2] -ne 0 -or [int] $Matches[3] -ne 0) {
        $problemas.Add("clivus-copia: depois do U, original=$($Matches[1]) copia=$($Matches[2]) repetidos=$($Matches[3]); esperava 1/0/0. Veja $($r.Saida)")
        return $false
    }

    $estados = @([regex]::Matches($r.Texto, '(?m)^ESTADO [^\r\n]*[\s\S]*?(\d+) removida\(s\)'))
    if ($estados.Count -lt 2 -or [int] $estados[1].Groups[1].Value -ne 0) {
        $problemas.Add("clivus-copia: depois do U o ESTADO registra remocao (a original nao foi removida). Veja $($r.Saida)")
        return $false
    }

    # REDO: a copia volta, ainda sem GUID repetido.
    if ($r.Texto -notmatch 'CLIVUS_COPIA_REDO conta=(\d+) repetidos=(\d+)') {
        $problemas.Add("clivus-copia: nao consegui ler o desenho depois do REDO. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] -ne 1 -or [int] $Matches[2] -ne 0) {
        $problemas.Add("clivus-copia: depois do REDO, original=$($Matches[1]) repetidos=$($Matches[2]); esperava 1/0. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^RENOMEAR 0 definição\(ões\) renomeada\(s\), (\d+) referência\(s\) trocada\(s\) para o bloco padrão, 1 definição\(ões\) apagada\(s\)') {
        $problemas.Add("clivus-copia: o CLIVUS_RENOMEAR nao trocou as referencias para o padrao. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] -lt 7) {
        $problemas.Add("clivus-copia: so $($Matches[1]) referencia(s) trocada(s). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_RENOMEAR_LISP antes=1 padrao=1 sufixo=0') {
        $problemas.Add("clivus-copia: o CLIVUS_RENOMEAR nao devolveu o bloco ao padrao. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (copia: $copiadas pecas copiadas, todas com identidade nova; bloco renomeado de volta)" -ForegroundColor DarkGray
    return $true
}

<#
    Recontar (7.6): apaga o contorno de uma mesa e reconta. O total do
    RECONTAR tem que bater com o que o LISP conta pelo XData (mesas uma a
    menos, modulos e pilares iguais, uma orfa), a removida tem que ser
    listada, e o registro limpo (CLIVUS_ESTADO diz 0 removidas depois).
#>
function Testar-Recontar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-recontar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-recontar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-recontar' `
        -Script (Join-Path $PSScriptRoot 'clivus-recontar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-recontar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'RECONTAR_TOTAIS mesas=(\d+) modulos=(\d+) pilares=(\d+) orfas=(\d+)') {
        $problemas.Add("clivus-recontar: o comando nao contou. Veja $($r.Saida)")
        return $false
    }

    $mesas = [int] $Matches[1]
    $modulos = [int] $Matches[2]
    $pilares = [int] $Matches[3]
    $orfas = [int] $Matches[4]

    if ($r.Texto -notmatch 'CLIVUS_RECONTAR_LISP antes=(\d+) modantes=(\d+) pilantes=(\d+) mesas=(\d+) modulos=(\d+) pilares=(\d+)') {
        $problemas.Add("clivus-recontar: nao consegui ler o desenho em LISP. Veja $($r.Saida)")
        return $false
    }

    $antes = [int] $Matches[1]
    $modAntes = [int] $Matches[2]
    $pilAntes = [int] $Matches[3]
    $mesasLisp = [int] $Matches[4]
    $modulosLisp = [int] $Matches[5]
    $pilaresLisp = [int] $Matches[6]

    # Uma mesa perdeu so o contorno (orfa), outra foi apagada inteira (28 modulos, 7 pilares).
    if ($mesasLisp -ne ($antes - 2) -or $modulosLisp -ne ($modAntes - 28) -or $pilaresLisp -ne ($pilAntes - 7)) {
        $problemas.Add("clivus-recontar: o LISP conta $mesasLisp/$modulosLisp/$pilaresLisp depois de $antes/$modAntes/$pilAntes; esperava -2/-28/-7. Veja $($r.Saida)")
        return $false
    }

    if ($mesas -ne $mesasLisp -or $modulos -ne $modulosLisp -or $pilares -ne $pilaresLisp -or $orfas -ne 1) {
        $problemas.Add("clivus-recontar: RECONTAR disse $mesas/$modulos/$pilares/$orfas (mesas/modulos/pilares/orfas); o LISP conta $mesasLisp/$modulosLisp/$pilaresLisp. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '2 removida\(s\) desde a última recontagem: (\S+), (\S+)\. Registro limpo\.') {
        $problemas.Add("clivus-recontar: as duas removidas nao foram listadas e limpas. Veja $($r.Saida)")
        return $false
    }

    $estados = @([regex]::Matches($r.Texto, '(?m)^ESTADO [^\r\n]*[\s\S]*?(\d+) removida\(s\)'))
    if ($estados.Count -lt 1 -or [int] $estados[$estados.Count - 1].Groups[1].Value -ne 0) {
        $problemas.Add("clivus-recontar: depois do RECONTAR o ESTADO ainda lista removida. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (recontar: $mesas mesas, $modulos modulos, $pilares pilares, 1 orfa; 2 removidas consumidas)" -ForegroundColor DarkGray
    return $true
}

<#
    Validar (7.7): valida limpo, faz quatro estragos (area apagada com o
    registro ficando, mesa sujada, contorno apagado, mesa copiada) e valida
    de novo: 1 area faltando, 2 sujas, 0 duplicadas, 0 pecas repetidas, 1
    orfa, 1 removida nao recontada, terreno ok.
#>
function Testar-Validar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-validar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-validar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-validar' `
        -Script (Join-Path $PSScriptRoot 'clivus-validar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-validar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $totais = @([regex]::Matches($r.Texto, 'VALIDAR_TOTAIS areas=(\d+) alinhamentos=(\d+) pendentes=(\d+) movidas=(\d+) areasdup=(\d+) alinhdup=(\d+) duplicadas=(\d+) pecas=(\d+) orfas=(\d+) removidas=(\d+) terreno=(\w+)'))

    if ($totais.Count -ne 2) {
        $problemas.Add("clivus-validar: esperava duas validacoes, achei $($totais.Count). Veja $($r.Saida)")
        return $false
    }

    $limpa = $totais[0]
    if ($limpa.Value -notmatch 'areas=0 alinhamentos=0 pendentes=0 movidas=0 areasdup=0 alinhdup=0 duplicadas=0 pecas=0 orfas=0 removidas=0 terreno=ok') {
        $problemas.Add("clivus-validar: a primeira validacao devia ser limpa: '$($limpa.Value)'. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^VALIDAR nada a apontar') {
        $problemas.Add("clivus-validar: a validacao limpa nao disse 'nada a apontar'. Veja $($r.Saida)")
        return $false
    }

    $suja = $totais[1]
    if ($suja.Value -notmatch 'areas=1 alinhamentos=0 pendentes=2 movidas=1 areasdup=0 alinhdup=1 duplicadas=0 pecas=0 orfas=1 removidas=1 terreno=ok') {
        $problemas.Add("clivus-validar: depois dos estragos esperava areas=1 pendentes=2 movidas=1 alinhdup=1 orfas=1 removidas=1: '$($suja.Value)'. Veja $($r.Saida)")
        return $false
    }

    # A validacao ao carregar (o desenho ja aberto no Core Console) nao
    # imprime nada num desenho sem coisas nossas.
    if ($r.Texto -match '(?m)^AO ABRIR') {
        $problemas.Add("clivus-validar: a validacao ao abrir falou num desenho sem nada nosso. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'Area da validacao\)') {
        $problemas.Add("clivus-validar: a area apagada nao foi nomeada. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (validar: limpa antes; depois 1 area faltando, 1 alinhamento duplicado, 2 sujas, 1 movida, 1 orfa, 1 removida)" -ForegroundColor DarkGray
    return $true
}

<#
    A conta da selecao (7.8): duas mesas inteiras e um pilar de uma terceira
    na selecao previa dao 3 mesas, 84 modulos e 60,5 kWp; uma linha do
    usuario da "nenhuma mesa".
#>
function Testar-Selecao {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-selecao--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-selecao: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-selecao' `
        -Script (Join-Path $PSScriptRoot 'clivus-selecao.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-selecao terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_SELECAO_LISP selecionadas=(\d+)') {
        $problemas.Add("clivus-selecao: nao consegui montar a selecao em LISP. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] -lt 2 * 36 + 1) {
        $problemas.Add("clivus-selecao: so $($Matches[1]) entidade(s) selecionada(s). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^SELECAO 3 mesa\(s\), 84 módulo\(s\), 60,5 kWp') {
        $problemas.Add("clivus-selecao: esperava 'SELECAO 3 mesa(s), 84 módulo(s), 60,5 kWp'. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^SELECAO nenhuma mesa do plugin') {
        $problemas.Add("clivus-selecao: a linha do usuario devia dar 'nenhuma mesa'. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (selecao: 3 mesas, 84 modulos, 60,5 kWp; linha do usuario sem mesa)" -ForegroundColor DarkGray
    return $true
}

<#
    Grupos (7.9): cria "Bloco A" com F1.1 e F1.2 pela selecao previa; a
    lista diz 2 mesas, 56 modulos, 14 pilares e 40,3 kWp; sujar a F1.1 e
    recalcular o grupo refaz 2 de 2 e o ESTADO fica sem suja; selecionar o
    grupo poe as entidades das duas mesas na selecao; apagar a F1.1 faz a
    lista dizer que 1 nao esta mais no desenho e selecionar so da a F1.2;
    apagar o grupo pelo nome em minusculas deixa a lista vazia.
#>
function Testar-Grupos {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-grupos--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-grupos: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-grupos' `
        -Script (Join-Path $PSScriptRoot 'clivus-grupos.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-grupos terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^GRUPO \d+ "Bloco A" criado com 2 mesa\(s\)') {
        $problemas.Add("clivus-grupos: o grupo nao foi criado com 2 mesas. Veja $($r.Saida)")
        return $false
    }

    $listas = @([regex]::Matches($r.Texto, '(?m)^GRUPOS (\d+) grupo\(s\)\.'))
    if ($listas.Count -ne 3 -or [int] $listas[0].Groups[1].Value -ne 1 -or [int] $listas[1].Groups[1].Value -ne 1 -or [int] $listas[2].Groups[1].Value -ne 0) {
        $problemas.Add("clivus-grupos: esperava a lista com 1 grupo, 1 grupo e depois 0. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^\s+Bloco A: 1 mesa\(s\), 28 módulo\(s\), 7 pilar\(es\), 20,2 kWp, 1 que não está\(ão\) mais no desenho') {
        $problemas.Add("clivus-grupos: depois de apagar a F1.1 a lista devia dizer 1 mesa e 1 sumida. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^\s+Bloco A: 2 mesa\(s\), 56 módulo\(s\), 14 pilar\(es\), 40,3 kWp') {
        $problemas.Add("clivus-grupos: a contagem do grupo nao e 2/56/14/40,3. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^RECALCULAR 2 de 2 mesa\(s\) recalculada\(s\)') {
        $problemas.Add("clivus-grupos: o recalcular por grupo nao refez 2 de 2. Veja $($r.Saida)")
        return $false
    }

    $estados = @([regex]::Matches($r.Texto, '(?m)^ESTADO (\d+) mesa\(s\), (\d+) limpa\(s\), (\d+) pendente\(s\)'))
    if ($estados.Count -lt 1 -or [int] $estados[$estados.Count - 1].Groups[3].Value -ne 0) {
        $problemas.Add("clivus-grupos: depois do recalcular por grupo ainda ha mesa suja. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_GRUPOS_LISP mesas=(\d+) so2=(\d+) marca=(\d+) marcaApagada=(\d+)') {
        $problemas.Add("clivus-grupos: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $entidadesDasDuas = [int] $Matches[1]
    $entidadesDaF12 = [int] $Matches[2]
    $marca = [int] $Matches[3]
    $marcaApagada = [int] $Matches[4]

    # 02/10/2026: "o hachurado tem que ficar por cima" (acima do canto mais
    # alto das mesas, e nao mais que um metro acima: regra 5).
    if ($r.Texto -notmatch 'zhachura=(-?[\d.]+) zmesas=(-?[\d.]+)') {
        $problemas.Add("clivus-grupos: nao consegui ler a cota da hachura. Veja $($r.Saida)")
        return $false
    }

    $zHachura = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
    $zMesas = [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)

    if ($zHachura -le $zMesas -or $zHachura -gt $zMesas + 1) {
        $problemas.Add("clivus-grupos: a hachura do grupo esta na cota $zHachura e o canto mais alto das mesas em $zMesas; tinha que ficar logo acima. Veja $($r.Saida)")
        return $false
    }

    if ($marca -ne 3 -or $marcaApagada -ne 0) {
        $problemas.Add("clivus-grupos: a marca do grupo devia ter 3 entidades (contorno, hachura, numero) e sumir ao apagar; deu $marca e $marcaApagada. Veja $($r.Saida)")
        return $false
    }

    $selecoes = @([regex]::Matches($r.Texto, '(?m)^GRUPO "Bloco A": (\d+) entidade\(s\) selecionada\(s\)'))
    if ($selecoes.Count -ne 2 -or [int] $selecoes[0].Groups[1].Value -ne $entidadesDasDuas -or [int] $selecoes[1].Groups[1].Value -ne $entidadesDaF12) {
        $problemas.Add("clivus-grupos: selecionar o grupo devia dar $entidadesDasDuas e depois $entidadesDaF12 entidade(s). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^GRUPO "Bloco A" apagado') {
        $problemas.Add("clivus-grupos: o grupo nao foi apagado. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (grupos: Bloco A com 2 mesas, 56 modulos, 40,3 kWp, marca de 3 entidades; recalculado, selecionado, com 1 sumida e apagado)" -ForegroundColor DarkGray
    return $true
}

<#
    Numeracao (7.10): a usina inteira (80 mesas em 16 fileiras) renumerada
    com a F1.1 na antiga F16.5 e a ultima fileira na antiga F1.1: toda mesa
    F(r).(n) vira F(17-r).(6-n), 80 letreiros trocados, sem aviso; numerar
    de novo com os letreiros novos troca zero.
#>
function Testar-Numerar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numerar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-numerar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-numerar' `
        -Script (Join-Path $PSScriptRoot 'clivus-numerar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 9999)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-numerar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $numeracoes = @([regex]::Matches($r.Texto, '(?m)^NUMERAR (\d+) fileira\(s\), (\d+) mesa\(s\), (\d+) letreiro\(s\) trocado\(s\)'))
    if ($numeracoes.Count -ne 2) {
        $problemas.Add("clivus-numerar: esperava duas numeracoes. Veja $($r.Saida)")
        return $false
    }

    $fileiras = [int] $numeracoes[0].Groups[1].Value
    $mesas    = [int] $numeracoes[0].Groups[2].Value
    $trocados = [int] $numeracoes[0].Groups[3].Value

    if ($fileiras -ne 16 -or $mesas -ne 80 -or $trocados -ne 80) {
        $problemas.Add("clivus-numerar: esperava 16 fileiras, 80 mesas e 80 trocados; deu $fileiras/$mesas/$trocados. Veja $($r.Saida)")
        return $false
    }

    if ([int] $numeracoes[1].Groups[3].Value -ne 0) {
        $problemas.Add("clivus-numerar: numerar de novo com os letreiros novos devia trocar zero. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -match 'ATEN.{1,4}O: a mesa') {
        $problemas.Add("clivus-numerar: a numeracao avisou algo que nao devia. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_NUMERAR_LISP invertidas=(\d+) de (\d+) letreiros=(\d+) avisos=(\d+) de (\d+) intactas=(\d+)') {
        $problemas.Add("clivus-numerar: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $invertidas = [int] $Matches[1]
    $total      = [int] $Matches[2]
    $letreiros  = [int] $Matches[3]
    $avisos     = [int] $Matches[4]
    $notas      = [int] $Matches[5]
    $intactas   = [int] $Matches[6]

    if ($notas -lt 1) {
        $problemas.Add("clivus-numerar: o aviso NAO CABE plantado pelo LISP nao foi achado. Veja $($r.Saida)")
        return $false
    }

    if ($intactas -ne $total) {
        $problemas.Add("clivus-numerar: $intactas de $total mesas com os outros campos do XData intactos. Veja $($r.Saida)")
        return $false
    }

    if ($total -ne 80 -or $invertidas -ne 80 -or $letreiros -ne 80) {
        $problemas.Add("clivus-numerar: $invertidas de $total mesas com o letreiro invertido, $letreiros letreiros distintos. Veja $($r.Saida)")
        return $false
    }

    if ($avisos -ne 0) {
        $problemas.Add("clivus-numerar: $avisos de $notas aviso(s) NAO CABE sem o letreiro novo. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (numerar: 80 mesas em 16 fileiras invertidas, F16.5 virou F1.1, aviso NAO CABE renomeado; de novo, zero trocas)" -ForegroundColor DarkGray
    return $true
}

<#
    Alturas como analise (26/09/2026): a fileira desenha N entidades na
    camada das alturas; o usuario apaga todas e um texto orfao sem
    identidade e plantado; CLIVUS_ALTURAS_REGERAR deixa a camada com as mesmas
    N entidades, todas com identidade, nenhum texto de cabeca para baixo.
#>
function Testar-Alturas {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-alturas--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-alturas: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-alturas' `
        -Script (Join-Path $PSScriptRoot 'clivus-alturas.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-alturas terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_ALTURAS_LISP antes=(\d+) zerado=(\d+) comOrfao=(\d+) depois=(\d+) semId=(\d+) viradas=(\d+)') {
        $problemas.Add("clivus-alturas: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $antes    = [int] $Matches[1]
    $zerado   = [int] $Matches[2]
    $comOrfao = [int] $Matches[3]
    $depois   = [int] $Matches[4]
    $semId    = [int] $Matches[5]
    $viradas  = [int] $Matches[6]

    if ($antes -lt 30 -or $zerado -ne 0 -or $comOrfao -ne 1) {
        $problemas.Add("clivus-alturas: esperava a camada cheia, depois vazia, depois so com o orfao; deu $antes/$zerado/$comOrfao. Veja $($r.Saida)")
        return $false
    }

    if ($depois -ne $antes -or $semId -ne 0 -or $viradas -ne 0) {
        $problemas.Add("clivus-alturas: depois de regerar esperava $antes entidades com identidade e legiveis; deu $depois, $semId sem identidade, $viradas viradas. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch '(?m)^ALTURAS 1 entidade\(s\) apagada\(s\)') {
        $problemas.Add("clivus-alturas: o regerar devia apagar o orfao (1 entidade). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (alturas: $antes cotas apagadas a mao e regeradas, orfao apagado, nenhuma virada)" -ForegroundColor DarkGray
    return $true
}

<#
    As analises independentes (8.9 a 8.11): inserir PB, PA, pilar e
    declividade (um texto por pilar, um por mesa na declividade, cota dentro
    do terreno), pintar so a PB, pintar modulos, tirar cores e apagar a PB sem
    mexer na PA. Tudo contado em LISP, nas entidades.
#>
function Testar-Analises {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-analises--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-analises: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-analises: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $planilha = Join-Path $raiz 'artefatos\testes\clivus-analises.xlsx'
    if (Test-Path $planilha) { Remove-Item $planilha -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-analises' `
        -Script (Join-Path $PSScriptRoot 'clivus-analises.scr') `
        -Substituicoes @{
            '{{XLSX}}' = ($planilha -replace '\\', '/')
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-analises terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_AN_LISP pilares=(\d+) contornos=(\d+) modulos=(\d+) pb=(\d+) pa=(\d+) pilar=(\d+) decl=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-analises: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $pilares = [int] $Matches[1]; $contornos = [int] $Matches[2]
    $pb = [int] $Matches[4]; $pa = [int] $Matches[5]; $pi = [int] $Matches[6]; $decl = [int] $Matches[7]
    $zmin = [double]::Parse($Matches[8], $invariante); $zmax = [double]::Parse($Matches[9], $invariante)

    # 02/10/2026: as analises de pilar enterrado e comprimento total, um texto por pilar.
    if ($r.Texto -notmatch 'penterrado=(\d+) ptotal=(\d+)' -or [int] $Matches[1] -ne $pilares -or [int] $Matches[2] -ne $pilares) {
        $problemas.Add("clivus-analises: os textos de pilar enterrado e total nao deram um por pilar ($pilares). Veja $($r.Saida)")
        return $false
    }

    if ($pilares -lt 7 -or $pb -ne $pilares -or $pa -ne $pilares -or $pi -ne $pilares -or $decl -ne $contornos) {
        $problemas.Add("clivus-analises: $pilares pilar(es) e $contornos mesa(s) deram PB $pb, PA $pa, pilar $pi, declividade $decl; esperava um por pilar (e inserir duas vezes nao empilha) e um por mesa. Veja $($r.Saida)")
        return $false
    }

    # Regra sagrada 5: o texto nasce na mesa, e a mesa acompanha o terreno.
    if ($zmin -lt ($minima - 0.01) -or $zmax -gt ($maxima + 5)) {
        $problemas.Add("clivus-analises: os textos vao de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_AN_LISP2 pbpintados=(\d+) papintados=(\d+) modpintados=(\d+) pbsemcor=(\d+) modsemcor=(\d+) pbfim=(\d+) pafim=(\d+) modposPA=(\d+) magenta=(\d+) magentadepois=(\d+)') {
        $problemas.Add("clivus-analises: nao consegui ler a segunda parte do LISP. Veja $($r.Saida)")
        return $false
    }

    $pbPint = [int] $Matches[1]; $paPint = [int] $Matches[2]; $modPint = [int] $Matches[3]
    $pbSem = [int] $Matches[4]; $modSem = [int] $Matches[5]; $pbFim = [int] $Matches[6]; $paFim = [int] $Matches[7]
    $modDepoisDaPA = [int] $Matches[8]
    $magentaAntes = [int] $Matches[9]; $magentaDepois = [int] $Matches[10]

    # Tirar as cores devolve a cor de antes: o modulo da mesa marcada volta a magenta.
    if ($magentaDepois -ne $magentaAntes) {
        $problemas.Add("clivus-analises: antes da pintura havia $magentaAntes modulo(s) magenta (mesa que nao cabe); depois de tirar as cores, $magentaDepois. Tinham que voltar todos. Veja $($r.Saida)")
        return $false
    }

    if ($modDepoisDaPA -ne $modPint) {
        $problemas.Add("clivus-analises: tirar as cores da PA mexeu nos modulos que a PB pintou ($modPint antes, $modDepoisDaPA depois). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'QUANTIFICAR ponta baixa: (\d+) abaixo de [\d,]+ m, (\d+) dentro, (\d+) acima de [\d,]+ m') {
        $problemas.Add("clivus-analises: nao achei a quantificacao da ponta baixa. Veja $($r.Saida)")
        return $false
    }

    $fora = [int] $Matches[1] + [int] $Matches[3]

    if ($pbPint -ne $fora -or $paPint -ne 0) {
        $problemas.Add("clivus-analises: analisar a PB pintou $pbPint texto(s) da PB (a quantificacao diz $fora fora da faixa) e $paPint da PA (tinha que ser 0). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'ANALISE_TESTE textos=(\d+) pecas=(\d+)') {
        $problemas.Add("clivus-analises: nao achei o relatorio da pintura de modulos. Veja $($r.Saida)")
        return $false
    }

    # A regra do teste pega toda ponta: todo modulo, das mesas marcadas
    # tambem, sai pintado, e o plugin conta o mesmo que o desenho.
    $pecasRelatadas = [int] $Matches[2]
    $modulosLivres = 0
    if ($r.Texto -match 'CLIVUS_AN_LISP pilares=\d+ contornos=\d+ modulos=(\d+)') { $modulosLivres = [int] $Matches[1] }

    if ($modulosLivres -lt 1 -or $modPint -ne $modulosLivres -or $modPint -ne $pecasRelatadas) {
        $problemas.Add("clivus-analises: $modulosLivres modulo(s) no desenho, $modPint pintado(s) e o plugin disse ${pecasRelatadas}; tinham que ser todos. Veja $($r.Saida)")
        return $false
    }

    if ($pbSem -ne 0 -or $modSem -ne 0 -or $pbFim -ne 0 -or $paFim -ne $pilares) {
        $problemas.Add("clivus-analises: depois de tirar as cores sobraram $pbSem texto(s) e $modSem modulo(s) pintados; depois de apagar a PB ficaram $pbFim da PB e $paFim da PA (esperava 0 e $pilares). Veja $($r.Saida)")
        return $false
    }

    # Passo 8.12: o Excel. Abre o .xlsx (um zip de XML) e confere as abas,
    # o resumo contra o LISP e a linha da ponta baixa contra a quantificacao.
    if (-not (Test-Path $planilha)) {
        $problemas.Add("clivus-analises: o CLIVUS_EXCEL_AUTO nao gravou $planilha. Veja $($r.Saida)")
        return $false
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($planilha)
    try {
        function LerParte([string] $nome) {
            $entrada = $zip.GetEntry($nome)
            if ($null -eq $entrada) { return $null }
            $leitor = New-Object IO.StreamReader($entrada.Open())
            try { [xml] $leitor.ReadToEnd() } finally { $leitor.Dispose() }
        }

        $pasta = LerParte 'xl/workbook.xml'
        $abas = @($pasta.workbook.sheets.sheet | ForEach-Object { $_.name }) -join '|'
        $resumo = LerParte 'xl/worksheets/sheet1.xml'
        $analisesXml = LerParte 'xl/worksheets/sheet2.xml'
    }
    finally { $zip.Dispose() }

    function Valor($folha, [string] $ref) {
        $c = $folha.worksheet.sheetData.row.c | Where-Object { $_.r -eq $ref } | Select-Object -First 1
        if ($null -eq $c) { return $null }
        if ($c.v) { return [string] $c.v }
        return [string] $c.is.t.'#text'
    }

    $mesasNoExcel = Valor $resumo 'B2'
    $pilaresNoExcel = Valor $resumo 'B5'
    $pbAbaixo = Valor $analisesXml 'D2'
    $pbQuantificado = [regex]::Match($r.Texto, 'QUANTIFICAR ponta baixa: (\d+) abaixo').Groups[1].Value

    if ($abas -ne 'Resumo|Análises|Pilares|Compra de pilares' -or $mesasNoExcel -ne "$contornos" -or $pilaresNoExcel -ne "$pilares" -or $pbAbaixo -ne $pbQuantificado) {
        $problemas.Add("clivus-analises: o Excel tem abas [$abas], $mesasNoExcel mesa(s), $pilaresNoExcel pilar(es) e $pbAbaixo PB abaixo; esperava as quatro abas, $contornos, $pilares e a quantificacao. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (analises: $pilares PB/PA/pilar e $decl declividades, $fora PB fora da faixa pintada(s), $modPint modulo(s), cores tiradas, PB apagada e PA intacta; Excel com $mesasNoExcel mesas e $pilaresNoExcel pilares)" -ForegroundColor DarkGray
    return $true
}

<#
    O resumo do terreno (8.15): superficie, area em hectares que bate com a
    do processamento, cotas, e onde fica. Desenho sem georreferencia diz
    como definir; com ela, cidade, pais e fuso UTM.
#>
function Testar-TerrenoResumo {
    param([string] $Desenho)

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-terreno-resumo' `
                            -Script (Join-Path $PSScriptRoot 'clivus-terreno-resumo.scr')

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-terreno-resumo terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'RESUMO DO TERRENO' -or $r.Texto -notmatch '  Terreno: (.+)' -or $r.Texto -notmatch '  Área: ([\d.,]+) ha em planta') {
        $problemas.Add("clivus-terreno-resumo: o resumo nao trouxe terreno e area. Veja $($r.Saida)")
        return $false
    }

    $areaNoResumo = $Matches[1]

    # A area do resumo e a mesma do processamento ("area 2D: X ha").
    if ($r.Texto -match '(?i)área[^\n]*?([\d.,]+) ha' -and $r.Texto -notmatch [regex]::Escape("$areaNoResumo ha")) {
        $problemas.Add("clivus-terreno-resumo: a area do resumo ($areaNoResumo ha) nao aparece no processamento. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -match '  Localização: não definida') {
        Write-Host "  (terreno-resumo: $areaNoResumo ha; desenho sem georreferencia)" -ForegroundColor DarkGray
        return $true
    }

    if ($r.Texto -notmatch '  Cidade: (.+)' ) {
        $problemas.Add("clivus-terreno-resumo: com localizacao, faltou a cidade. Veja $($r.Saida)")
        return $false
    }

    $cidade = $Matches[1].Trim()

    if ($r.Texto -notmatch '  Fuso: (.*UTM.*)') {
        $problemas.Add("clivus-terreno-resumo: com localizacao, faltou o fuso UTM. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (terreno-resumo: $areaNoResumo ha; $cidade; $($Matches[1].Trim()))" -ForegroundColor DarkGray
    return $true
}

<#
    Os estilos do projeto (8.13): sem escolha, o estilo do Renan quando o
    desenho o tem; escolhido o Standard, os textos saem nele.
#>
function Testar-Estilos {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-estilos--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-estilos: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-estilos' `
        -Script (Join-Path $PSScriptRoot 'clivus-estilos.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-estilos terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_ESTILO_PADRAO estilo=(.+?) anotativo=(\S+) altura=([\d.]+)') {
        $problemas.Add("clivus-estilos: nao li o texto com o estilo padrao. Veja $($r.Saida)")
        return $false
    }

    $padrao = $Matches[1]; $anotativoPadrao = $Matches[2]; $alturaPadrao = $Matches[3]

    if ($r.Texto -notmatch 'CLIVUS_ESTILO_STANDARD estilo=(.+?) anotativo=(\S+) altura=([\d.]+)') {
        $problemas.Add("clivus-estilos: nao li o texto com o Standard. Veja $($r.Saida)")
        return $false
    }

    $standard = $Matches[1]; $anotativoStandard = $Matches[2]

    # Sem escolha vale o primeiro estilo anotativo proprio do desenho (o de
    # referencia, Itatiba, tem o do Renan): nem o Standard nem o "Annotative"
    # de fabrica.
    if ($padrao -in @('Standard', 'Annotative', '') -or $anotativoPadrao -ne '1' -or $standard -ne 'Standard' -or $anotativoStandard -eq '1') {
        $problemas.Add("clivus-estilos: sem escolha o texto saiu em [$padrao] (anotativo $anotativoPadrao), esperava um anotativo proprio do desenho; com Standard saiu em [$standard] (anotativo $anotativoStandard). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (estilos: sem escolha '$padrao' anotativo, altura $alturaPadrao; escolhido Standard, nao anotativo)" -ForegroundColor DarkGray
    return $true
}

<#
    As tags (8.14): uma por fileira, uma por mesa (inserir de novo nao
    empilha), uma por modulo, strings de 14 (inteiras) e de 20 (uma
    incompleta por mesa); apagar os modulos deixa as mesas. Cota na faixa do
    terreno (regra 5).
#>
function Testar-Tags {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-tags--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-tags: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-tags: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-tags' `
        -Script (Join-Path $PSScriptRoot 'clivus-tags.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-tags terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_TAGS mesas=(\d+) modulos=(\d+) fileiras=(\d+) tmesas=(\d+) tmodulos=(\d+) s14=(\d+) inc14=(\d+) s20=(\d+) inc20=(\d+) modfim=(\d+) mesasfim=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-tags: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $m = $Matches
    $mesas = [int] $m[1]; $modulos = [int] $m[2]
    $esperado = "fileiras=1 tmesas=$mesas tmodulos=$modulos s14=$($modulos / 14) inc14=0 s20=$(2 * $mesas) inc20=$mesas modfim=0 mesasfim=$mesas"
    $obtido = "fileiras=$($m[3]) tmesas=$($m[4]) tmodulos=$($m[5]) s14=$($m[6]) inc14=$($m[7]) s20=$($m[8]) inc20=$($m[9]) modfim=$($m[10]) mesasfim=$($m[11])"

    if ($mesas -lt 1 -or $obtido -ne $esperado) {
        $problemas.Add("clivus-tags: com $mesas mesa(s) e $modulos modulo(s) esperava [$esperado], deu [$obtido]. Veja $($r.Saida)")
        return $false
    }

    $zmin = [double]::Parse($m[12], $invariante); $zmax = [double]::Parse($m[13], $invariante)

    if ($zmin -lt ($minima - 0.01) -or $zmax -gt ($maxima + 5)) {
        $problemas.Add("clivus-tags: as tags vao de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). Veja $($r.Saida)")
        return $false
    }

    # Nenhuma tag abaixo da ponta mais alta da mesa dela (03/10/2026: "tags
    # ainda sendo cortadas" pela metade alta da mesa inclinada).
    if ($r.Texto -notmatch 'CLIVUS_TAGS_ALTURA abaixo=(\d+) mesas=(\d+)' -or [int] $Matches[2] -lt 1 -or [int] $Matches[1] -ne 0) {
        $problemas.Add("clivus-tags: tag abaixo do topo da mesa dela (cortada no sombreado). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (tags: 1 fileira, $mesas mesas, $modulos modulos, $($m[6]) strings de 14 e $($m[8]) de 20 com $($m[9]) incompletas)" -ForegroundColor DarkGray
    return $true
}

<#
    A usina mista (8.5 e 8.6): mesas de 28 e de 14 no desenho, em uso, numa
    area de 90 m de largura. Os dois tipos aparecem, cada contorno com o
    nome do perfil e a cor do tipo, os pilares batem com 7 por mesa de 28 e
    4 por mesa de 14, as faces com os modulos, e o relatorio da usina diz o
    mesmo total de modulos. Cota dos pilares na faixa do terreno (regra 5).
#>
function Testar-UsinaMista {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-usina-mista--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-usina-mista: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-usina-mista: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-usina-mista' `
        -Script (Join-Path $PSScriptRoot 'clivus-usina-mista.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -45 -50 0)
            '{{A2}}'   = (Ponto3  45 -50 0)
            '{{A3}}'   = (Ponto3  45  50 0)
            '{{A4}}'   = (Ponto3 -45  50 0)
            '{{L1}}'   = (Ponto3 -45 -50 0)
            '{{L2}}'   = (Ponto3 -45  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-usina-mista terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_MISTA n28=(\d+) n14=(\d+) outros=(\d+) comcor=(\d+) pilares=(\d+) faces=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)') {
        $problemas.Add("clivus-usina-mista: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $m = $Matches
    $n28 = [int] $m[1]; $n14 = [int] $m[2]; $outros = [int] $m[3]; $comCor = [int] $m[4]
    $pilares = [int] $m[5]; $faces = [int] $m[6]

    if ($n28 -lt 1 -or $n14 -lt 1 -or $outros -ne 0) {
        $problemas.Add("clivus-usina-mista: $n28 mesa(s) de 28, $n14 de 14 e $outros sem o nome do perfil; esperava os dois tipos, todas com nome. Veja $($r.Saida)")
        return $false
    }

    if ($pilares -ne (7 * $n28 + 4 * $n14) -or $faces -ne (28 * $n28 + 14 * $n14)) {
        $problemas.Add("clivus-usina-mista: $pilares pilar(es) e $faces face(s) para $n28 x 28 e $n14 x 14 (esperava $(7 * $n28 + 4 * $n14) e $(28 * $n28 + 14 * $n14)). Veja $($r.Saida)")
        return $false
    }

    # Mesa que nao cabe sai magenta, com cor propria tambem: so conferimos
    # que toda mesa que cabe tem a cor do tipo (comcor >= mesas - marcadas).
    if ($r.Texto -match 'desenhado: (\d+) mesa\(s\).*?(\d+) marcada\(s\)') {
        if ($comCor -lt ([int] $Matches[1] - [int] $Matches[2])) {
            $problemas.Add("clivus-usina-mista: so $comCor contorno(s) com cor; toda mesa que cabe tem a cor do tipo. Veja $($r.Saida)")
            return $false
        }
    }

    # Todo modulo na camada de modulo (fora das marcadas) com a cor do tipo:
    # em 03/10/2026 eles saiam cinza e so o contorno tinha cor.
    if ($r.Texto -notmatch 'CLIVUS_MISTA_MODULOS n=(\d+) comcor=(\d+)') {
        $problemas.Add("clivus-usina-mista: nao consegui ler a cor dos modulos. Veja $($r.Saida)")
        return $false
    }

    if ([int] $Matches[1] -lt 1 -or [int] $Matches[2] -ne [int] $Matches[1]) {
        $problemas.Add("clivus-usina-mista: $($Matches[2]) de $($Matches[1]) modulo(s) com a cor do tipo; todos deveriam ter. Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'USINA .*?(\d+) mesa\(s\), (\d+) módulo\(s\)' -or [int] $Matches[2] -ne $faces) {
        $problemas.Add("clivus-usina-mista: o relatorio da usina diz $($Matches[2]) modulo(s), o desenho tem $faces faces. Veja $($r.Saida)")
        return $false
    }

    $zmin = [double]::Parse($m[7], $invariante); $zmax = [double]::Parse($m[8], $invariante)

    if ($zmin -lt $minima -or $zmax -gt ($maxima + 5)) {
        $problemas.Add("clivus-usina-mista: os topos dos pilares vao de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (usina mista: $n28 mesas de 28 e $n14 de 14, $pilares pilares, $faces modulos)" -ForegroundColor DarkGray
    return $true
}

<#
    Trocar mesa e regerar fileira (9.2 e 9.3), na area da usina mista (90 m
    de largura, 28 e 14 em uso). A F1.2 vira duas de 14 (F1.2a e F1.2b, com o
    perfil de 14), com a cota no terreno; reespacar a fileira 1 mantem as
    mesas e deixa pelo menos o espacamento entre vizinhas; a fileira 2 pelo
    motor volta com o mesmo numero de mesas.
#>
function Testar-Trocar {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-trocar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-trocar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-trocar: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-trocar' `
        -Script (Join-Path $PSScriptRoot 'clivus-trocar.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -45 -50 0)
            '{{A2}}'   = (Ponto3  45 -50 0)
            '{{A3}}'   = (Ponto3  45  50 0)
            '{{A4}}'   = (Ponto3 -45  50 0)
            '{{L1}}'   = (Ponto3 -45 -50 0)
            '{{L2}}'   = (Ponto3 -45  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-trocar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    $padrao = 'CLIVUS_TROCAR total0=(\d+) total1=(\d+) total2=(\d+) f1antes=(\d+) f1troca=(\d+) f1manter=(\d+) f2antes=(\d+) f2motor=(\d+) ' +
              'perfil12=(.+?) p2a=(.+?) p2b=(.+?) sem12=(.+?) p2amanter=(.+?) vao0=(-?[\d.]+) vaotroca=(-?[\d.]+) vaomanter=(-?[\d.]+) zmin=(-?[\d.]+) zmax=(-?[\d.]+)'

    if ($r.Texto -notmatch $padrao) {
        $problemas.Add("clivus-trocar: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $m = $Matches
    $total0 = [int] $m[1]; $total1 = [int] $m[2]; $total2 = [int] $m[3]
    $f1antes = [int] $m[4]; $f1troca = [int] $m[5]; $f1manter = [int] $m[6]
    $f2antes = [int] $m[7]; $f2motor = [int] $m[8]
    $vao0 = [double]::Parse($m[14], $invariante)
    $vaoManter = [double]::Parse($m[16], $invariante)
    $zmin = [double]::Parse($m[17], $invariante); $zmax = [double]::Parse($m[18], $invariante)

    if ($total0 -lt 4 -or $total1 -ne ($total0 + 1) -or $f1troca -ne ($f1antes + 1)) {
        $problemas.Add("clivus-trocar: trocar 1 por 2 deveria somar uma mesa (antes $total0, depois $total1; fileira 1 de $f1antes para $f1troca). Veja $($r.Saida)")
        return $false
    }

    if ($m[10] -ne 'Mesa 2V14' -or $m[11] -ne 'Mesa 2V14' -or $m[12] -ne '-') {
        $problemas.Add("clivus-trocar: a F1.2 ($($m[9])) deveria virar F1.2a e F1.2b de 'Mesa 2V14' e sumir; deu F1.2a=$($m[10]), F1.2b=$($m[11]), F1.2=$($m[12]). Veja $($r.Saida)")
        return $false
    }

    if ($zmin -lt ($minima - 0.01) -or $zmax -gt ($maxima + 5)) {
        $problemas.Add("clivus-trocar: o contorno da F1.2a vai de $zmin a $zmax, fora da faixa do terreno ($minima a $maxima). Veja $($r.Saida)")
        return $false
    }

    # Reespacar mantem as mesas e os tipos e deixa o espacamento do motor. O
    # vao medido e entre contornos em planta (o plano dos modulos, menor que
    # a pegada): o do motor, antes da troca, e a referencia; a inclinacao de
    # cada mesa muda um pouco o comprimento em planta, dai a folga de 0,1 m.
    if ($f1manter -ne $f1troca -or $m[13] -ne 'Mesa 2V14' -or $vaoManter -le 0 -or $vaoManter -lt ($vao0 - 0.1)) {
        $problemas.Add("clivus-trocar: reespacar a fileira 1 deveria manter $f1troca mesas (deu $f1manter), a F1.2a de 14 (deu $($m[13])) e o vao do motor ($vao0 m, menos 0,1; deu $vaoManter). Veja $($r.Saida)")
        return $false
    }

    if ($f2motor -ne $f2antes -or $total2 -ne $total1) {
        $problemas.Add("clivus-trocar: a fileira 2 pelo motor deveria voltar com $f2antes mesas (deu $f2motor; total $total1 -> $total2). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (trocar: F1.2 ($($m[9])) virou 2 de 14, vao minimo $($m[15]) m (motor $vao0); reespacada com $vaoManter m; fileira 2 pelo motor com $f2motor mesas)" -ForegroundColor DarkGray
    return $true
}

<#
    A arvore (9.4): duas pelo comando, o pe no terreno (o clique vem com
    Z = 0); a primeira MOVIDA com +80 m de cota e a segunda COPIADA com -40 m
    voltam ao chao do lugar novo (regra 5), e a copia tem GUID proprio.
#>
function Testar-Arvore {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-arvore--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-arvore: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto([double] $dx, [double] $dy) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},0', $centroX + $dx, $centroY + $dy)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-arvore' `
        -Script (Join-Path $PSScriptRoot 'clivus-arvore.scr') `
        -Substituicoes @{ '{{P1}}' = (Ponto -20 -10); '{{P2}}' = (Ponto 10 5) }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-arvore terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_ARVORE_ANTES n=(\d+) z1=(-?[\d.]+) z2=(-?[\d.]+)' -or [int] $Matches[1] -ne 2) {
        $problemas.Add("clivus-arvore: esperava 2 arvores postas pelo comando. Veja $($r.Saida)")
        return $false
    }

    # O pe na cota do terreno, nunca a do clique (0).
    if ([math]::Abs([double]::Parse($Matches[2], $invariante)) -lt 1 -or [math]::Abs([double]::Parse($Matches[3], $invariante)) -lt 1) {
        $problemas.Add("clivus-arvore: arvore posta com a cota do clique (0) e nao a do terreno. Veja $($r.Saida)")
        return $false
    }

    $arvores = [regex]::Matches($r.Texto, '(?m)^CLIVUS_ARVORE_P i=\d+ z=(-?[\d.]+) guid=([0-9a-f-]+)')
    $chao = [regex]::Matches($r.Texto, '(?m)^\s+X [\d\.,]+\s+Y [\d\.,]+\s+Z ([\d\.,-]+)\s*$')

    if ($arvores.Count -ne 3 -or $chao.Count -ne 3) {
        $problemas.Add("clivus-arvore: depois de mover e copiar esperava 3 arvores e 3 cotas do terreno; deu $($arvores.Count) e $($chao.Count). Veja $($r.Saida)")
        return $false
    }

    for ($i = 0; $i -lt 3; $i++) {
        $z = [double]::Parse($arvores[$i].Groups[1].Value, $invariante)
        $terreno = [double]::Parse(($chao[$i].Groups[1].Value -replace '\.', ''), $ptbr)

        if ([math]::Abs($z - $terreno) -gt 0.01) {
            $problemas.Add("clivus-arvore: a arvore $($i + 1) ficou na cota $z e o terreno ali e $terreno; ela tinha de voltar ao chao. Veja $($r.Saida)")
            return $false
        }
    }

    $guids = $arvores | ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique
    if (@($guids).Count -ne 3) {
        $problemas.Add("clivus-arvore: a copia ficou com o GUID da original. Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (arvore: 2 postas no terreno, uma movida 10 m em planta e +80 m na cota, uma copiada -40 m; as 3 no chao, GUIDs proprios)" -ForegroundColor DarkGray
    return $true
}

<#
    As sombras pelo BOTAO da janela (04/10/2026, Renan: "nao esta pintando os
    modulos com sombra"). O botao roda fora de um comando; o vigia tomava a
    pintura por edicao do usuario e, no proximo comando, marcava as mesas
    pendentes e pintava os modulos de vermelho por cima do roxo. O teste de
    antes so usava a linha de comando (vigia calado) e a arvore em cima da
    mesa. Aqui: arvore fora da fileira, o mesmo caminho do botao, um REGEN
    depois. Exige modulos com cor de sombra, nenhum vermelho de pendente,
    nenhum "pendente" escrito e contornos de sombra sobre as mesas.
#>
function Testar-SombrasJanela {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sombras-janela--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-sombras-janela: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sombras-janela' `
        -Script (Join-Path $PSScriptRoot 'clivus-sombras-janela.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-sombras-janela terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_JANELA sombra=(\d+) vermelhos=(\d+) contornos=(\d+)') {
        $problemas.Add("clivus-sombras-janela: sem o resumo. Veja $($r.Saida)")
        return $false
    }

    $sombra = [int]$Matches[1]; $vermelhos = [int]$Matches[2]; $contornos = [int]$Matches[3]
    $depois = $r.Texto.Substring($r.Texto.IndexOf('CLIVUS_JANELA_ANTES'))

    $erros = @()
    if ($sombra -le 0) { $erros += 'nenhum modulo com cor de sombra' }
    if ($vermelhos -gt 0) { $erros += "$vermelhos modulo(s) vermelhos de pendente (o vigia tomou a sombra por edicao)" }
    if ($depois -match 'pendente') { $erros += 'o vigia escreveu "pendente" depois da sombra' }
    if ($contornos -le 2) { $erros += "so $contornos contorno(s): a sombra nao foi desenhada sobre as mesas" }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-sombras-janela: $($erros -join '; '). Veja $($r.Saida)")
        return $false
    }

    Write-Host "  (sombras pela janela: $sombra modulo(s) com sombra, nenhum pendente, $contornos contorno(s), no chao e sobre as mesas)" -ForegroundColor DarkGray
    return $true
}

<#
    Sombras (9.7 e 9.8): usina mista e uma arvore grande no meio da F1.3. As
    09:00 de 21/06/2026 a sombra e desenhada no terreno (cota na faixa dele,
    regra 5) e marca modulos; Apagar tira os contornos e devolve exatamente
    a cor de antes de cada modulo; o dia inteiro marca, pelo pior caso, pelo
    menos os modulos do instante.
#>
function Testar-Sombras {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sombras--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-sombras: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    if ($sonda.Texto -notmatch 'cotas:\s+(-?[\d.,]+) m a (-?[\d.,]+) m') {
        $problemas.Add("clivus-sombras: nao achei a faixa de cotas do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $minima = [double]::Parse($Matches[1], $ptbr)
    $maxima = [double]::Parse($Matches[2], $ptbr)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-sombras' `
        -Script (Join-Path $PSScriptRoot 'clivus-sombras.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-sombras terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_SOMBRAS instante=(\d+) zmin=(-?[\d.]+) zmax=(-?[\d.]+) marcados=(\d+) apagado=(\d+) marcadosdepois=(\d+) volta=(\d) dia=(\d+) marcadosdia=(\d+)') {
        $problemas.Add("clivus-sombras: nao consegui ler o LISP. Veja $($r.Saida)")
        return $false
    }

    $m = $Matches
    $contornos = [int] $m[1]; $zmin = [double]::Parse($m[2], $invariante); $zmax = [double]::Parse($m[3], $invariante)
    $marcados = [int] $m[4]; $marcadosDia = [int] $m[9]

    if ($contornos -lt 2 -or $marcados -lt 1) {
        $problemas.Add("clivus-sombras: as 09:00 esperava a sombra do tronco e da copa desenhadas e modulos marcados; deu $contornos contorno(s) e $marcados modulo(s). Veja $($r.Saida)")
        return $false
    }

    if ($zmin -lt ($minima - 0.01) -or $zmax -gt ($maxima + 0.1)) {
        $problemas.Add("clivus-sombras: a sombra vai da cota $zmin a $zmax, fora do terreno ($minima a $maxima). Veja $($r.Saida)")
        return $false
    }

    if ([int] $m[5] -ne 0 -or [int] $m[6] -ne 0 -or $m[7] -ne '1') {
        $problemas.Add("clivus-sombras: Apagar deveria tirar os contornos ($($m[5]) ficaram), as marcas ($($m[6]) ficaram) e devolver a cor de antes (volta=$($m[7])). Veja $($r.Saida)")
        return $false
    }

    if ([int] $m[8] -lt 2 -or $marcadosDia -lt $marcados) {
        $problemas.Add("clivus-sombras: o dia inteiro deveria desenhar a sombra do pior instante e marcar pelo menos os $marcados modulos das 09:00; deu $($m[8]) contorno(s) e $marcadosDia modulo(s). Veja $($r.Saida)")
        return $false
    }

    # As mesas tambem fazem sombra: as 07:30 a fileira da frente pega a de tras.
    if ($r.Texto -notmatch '07:30 \(fuso[^\r\n]*?(\d+) por .rvore, (\d+) por outra mesa, (\d+) pelo terreno' -or [int] $Matches[2] -lt 1) {
        $problemas.Add("clivus-sombras: as 07:30 esperava modulos na sombra de outra mesa (fileira na fileira). Veja $($r.Saida)")
        return $false
    }

    $porMesa = [int] $Matches[2]; $porTerreno = [int] $Matches[3]

    Write-Host "  (sombras: 09:00 com $contornos contornos e $marcados modulo(s); apagadas com a cor de volta; dia inteiro com $marcadosDia modulo(s) no pior caso; 07:30 com $porMesa por outra mesa e $porTerreno pelo terreno)" -ForegroundColor DarkGray
    return $true
}

<#
    O 3D no navegador (9.9): a pagina gravada e um arquivo so, sem script
    buscado na internet, com a three.js, tantas faces quantas 3DFACE o
    desenho tem, os pilares, a arvore, as sombras e o terreno em grade.
#>
function Testar-Ver3D {
    param([string] $Desenho)

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-3d--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-3d: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $html = Join-Path $saida 'clivus-3d.html'
    if (Test-Path $html) { Remove-Item $html -Force }

    $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-3d' `
        -Script (Join-Path $PSScriptRoot 'clivus-3d.scr') `
        -Substituicoes @{
            '{{A1}}'   = (Ponto3 -50 -50 0)
            '{{A2}}'   = (Ponto3  50 -50 0)
            '{{A3}}'   = (Ponto3  50  50 0)
            '{{A4}}'   = (Ponto3 -50  50 0)
            '{{L1}}'   = (Ponto3 -50 -50 0)
            '{{L2}}'   = (Ponto3 -50  50 0)
            '{{LADO}}' = (Ponto3   0   0 0)
            '{{P}}'    = (Ponto3 -30   0 0)
            '{{HTML}}' = $html
        }

    if ($r.Estourou -or $r.Codigo -ne 0) {
        $problemas.Add("clivus-3d terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
        return $false
    }

    if ($r.Texto -notmatch 'CLIVUS_3D faces=(\d+) pilares=(\d+)' -or -not (Test-Path $html)) {
        $problemas.Add("clivus-3d: a pagina nao foi gravada em $html. Veja $($r.Saida)")
        return $false
    }

    $faces = [int] $Matches[1]; $pilares = [int] $Matches[2]
    $texto = [IO.File]::ReadAllText($html, [Text.Encoding]::UTF8)

    if ($texto -notmatch 'const D = (\{.*?\});\r?\n') {
        $problemas.Add("clivus-3d: a cena nao esta na pagina. Veja $html")
        return $false
    }

    $cena = $Matches[1] | ConvertFrom-Json

    $erros = @()
    if ($texto -match '(?i)<script src|<link') { $erros += 'busca coisa de fora' }
    if ($texto -notmatch 'Copyright 2010-2021 Three.js Authors') { $erros += 'sem a three.js' }
    if (@($cena.faces).Count -ne $faces -or $faces -lt 1) { $erros += "$(@($cena.faces).Count) faces na pagina e $faces no desenho" }
    if (@($cena.pilares).Count -ne $pilares) { $erros += "$(@($cena.pilares).Count) pilares na pagina e $pilares no desenho" }
    if (@($cena.arvores).Count -ne 1) { $erros += "$(@($cena.arvores).Count) arvores (esperava 1)" }
    if (@($cena.sombras).Count -lt 2) { $erros += "$(@($cena.sombras).Count) sombras (esperava tronco e copa)" }
    if ($null -eq $cena.terreno -or @($cena.terreno.z | Where-Object { $null -ne $_ }).Count -lt 100) { $erros += 'terreno vazio' }

    if ($erros.Count -gt 0) {
        $problemas.Add("clivus-3d: $($erros -join '; '). Veja $html e $($r.Saida)")
        return $false
    }

    Write-Host "  (3d: $faces faces, $pilares pilares, 1 arvore, $(@($cena.sombras).Count) sombras, terreno $($cena.terreno.colunas) x $($cena.terreno.linhas); $([math]::Round((Get-Item $html).Length / 1MB, 1)) MB)" -ForegroundColor DarkGray
    return $true
}

<#
    O envio ao servidor 3D (plano/contrato-servidor-3d.md): o runner sobe o
    servidor falso local (servidor-falso.py) e publica uma fileira nele.
    Com a chave certa, o plugin escreve o link e o servidor recebe o corpo
    do contrato, com tantas faces quantas 3DFACE o desenho tem; com a chave
    errada, o plugin diz que a chave nao foi aceita.
#>
function Testar-Publicar3D {
    param([string] $Desenho)

    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) {
        $problemas.Add('clivus-publicar: python nao encontrado para subir o servidor falso.')
        return $false
    }

    $sonda = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-publicar--sonda' `
                                -Script (Join-Path $PSScriptRoot 'clivus-terreno.scr')

    if ($sonda.Texto -notmatch 'centroX=(-?[\d.]+) centroY=(-?[\d.]+)') {
        $problemas.Add("clivus-publicar: nao achei o centro do terreno. Veja $($sonda.Saida)")
        return $false
    }

    $invariante = [Globalization.CultureInfo]::InvariantCulture
    $centroX = [double]::Parse($Matches[1], $invariante)
    $centroY = [double]::Parse($Matches[2], $invariante)

    function Ponto3([double] $dx, [double] $dy, [double] $z) {
        [string]::Format($invariante, '{0:0.###},{1:0.###},{2:0.###}', $centroX + $dx, $centroY + $dy, $z)
    }

    $substituicoes = @{
        '{{A1}}'   = (Ponto3 -50 -50 0)
        '{{A2}}'   = (Ponto3  50 -50 0)
        '{{A3}}'   = (Ponto3  50  50 0)
        '{{A4}}'   = (Ponto3 -50  50 0)
        '{{L1}}'   = (Ponto3 -50 -50 0)
        '{{L2}}'   = (Ponto3 -50  50 0)
        '{{LADO}}' = (Ponto3   0   0 0)
    }

    $porta = 18765
    $chave = 'chave-de-teste-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $recebido = Join-Path $saida 'clivus-publicar-recebido.json'
    if (Test-Path $recebido) { Remove-Item $recebido -Force }

    $servidor = Start-Process -FilePath $python.Source -ArgumentList @((Join-Path $PSScriptRoot 'servidor-falso.py'), $porta, $chave, $recebido) `
                              -PassThru -WindowStyle Hidden
    $antes = @{ Servidor = $env:CLIVUS_SERVIDOR; Chave = $env:CLIVUS_SERVIDOR_CHAVE }

    try {
        # Espera o servidor falso responder.
        $noAr = $false
        for ($i = 0; $i -lt 40 -and -not $noAr; $i++) {
            try { $null = Invoke-WebRequest "http://127.0.0.1:$porta/api/v1/saude" -UseBasicParsing -TimeoutSec 1; $noAr = $true }
            catch { Start-Sleep -Milliseconds 250 }
        }

        if (-not $noAr) {
            $problemas.Add('clivus-publicar: o servidor falso nao subiu.')
            return $false
        }

        $env:CLIVUS_SERVIDOR = "http://127.0.0.1:$porta"

        # Chave errada: o plugin diz e nao publica.
        $env:CLIVUS_SERVIDOR_CHAVE = 'errada'
        $errado = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-publicar--chave-errada' `
                                     -Script (Join-Path $PSScriptRoot 'clivus-publicar.scr') -Substituicoes $substituicoes

        if ($errado.Texto -notmatch 'chave inv') {
            $problemas.Add("clivus-publicar: com a chave errada o plugin deveria dizer que ela nao foi aceita. Veja $($errado.Saida)")
            return $false
        }

        # Chave certa: o link volta e o servidor recebe a usina.
        $env:CLIVUS_SERVIDOR_CHAVE = $chave
        $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-publicar' `
                                -Script (Join-Path $PSScriptRoot 'clivus-publicar.scr') -Substituicoes $substituicoes

        if ($r.Estourou -or $r.Codigo -ne 0) {
            $problemas.Add("clivus-publicar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
            return $false
        }

        if ($r.Texto -notmatch "Link: (http://127\.0\.0\.1:$porta/3d/teste\d+)" ) {
            $problemas.Add("clivus-publicar: o plugin nao escreveu o link devolvido pelo servidor. Veja $($r.Saida)")
            return $false
        }

        $link = $Matches[1]

        if ($r.Texto -notmatch 'CLIVUS_PUBLICAR faces=(\d+)' -or -not (Test-Path $recebido)) {
            $problemas.Add("clivus-publicar: o servidor nao recebeu a usina. Veja $($r.Saida)")
            return $false
        }

        $faces = [int] $Matches[1]
        $corpo = [IO.File]::ReadAllText($recebido, [Text.Encoding]::UTF8) | ConvertFrom-Json

        if ($corpo.versao -ne 1 -or @($corpo.cena.faces).Count -ne $faces -or $faces -lt 1 -or @($corpo.cena.pilares).Count -lt 1 -or $null -eq $corpo.cena.terreno) {
            $problemas.Add("clivus-publicar: o corpo recebido nao bate com o contrato (versao $($corpo.versao), $(@($corpo.cena.faces).Count) faces para $faces no desenho). Veja $recebido")
            return $false
        }

        Write-Host "  (publicar: $faces faces recebidas pelo servidor falso, link $link; chave errada recusada)" -ForegroundColor DarkGray
        return $true
    }
    finally {
        $env:CLIVUS_SERVIDOR = $antes.Servidor
        $env:CLIVUS_SERVIDOR_CHAVE = $antes.Chave
        if ($servidor -and -not $servidor.HasExited) { Stop-Process -Id $servidor.Id -Force -ErrorAction SilentlyContinue }
    }
}

<#
    Os idiomas (etapa 10): o plugin aberto em ingles e em espanhol
    (CLIVUS_IDIOMA_TESTE, so no build Debug) atende pelo nome traduzido do
    comando e pelo global, e a mensagem sai no idioma, sem o portugues.
#>
function Testar-Idioma {
    param([string] $Desenho)

    $casos = @(
        @{ Idioma = 'en'; Nome = 'CLIVUS_HELLO'; Esperado = "Clivus Solar loaded, version $versao" },
        @{ Idioma = 'es'; Nome = 'CLIVUS_HOLA';  Esperado = "Clivus Solar cargado, versión $versao" }
    )
    $antes = $env:CLIVUS_IDIOMA_TESTE

    try {
        foreach ($caso in $casos) {
            $env:CLIVUS_IDIOMA_TESTE = $caso.Idioma
            $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo "clivus-idioma-$($caso.Idioma)" `
                                    -Script (Join-Path $PSScriptRoot 'clivus-idioma.scr') -Substituicoes @{ '{{NOME}}' = $caso.Nome }

            if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_IDIOMA_FIM') -lt 0) {
                $problemas.Add("clivus-idioma-$($caso.Idioma) terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
                return $false
            }

            $t = $r.Texto
            $traduzido = $t.Substring($t.IndexOf('CLIVUS_IDIOMA_TRADUZIDO')); $traduzido = $traduzido.Substring(0, $traduzido.IndexOf('CLIVUS_IDIOMA_GLOBAL'))
            $global = $t.Substring($t.IndexOf('CLIVUS_IDIOMA_GLOBAL')); $global = $global.Substring(0, $global.IndexOf('CLIVUS_IDIOMA_FIM'))

            $erros = @()
            if (-not $traduzido.Contains($caso.Esperado)) { $erros += "o nome $($caso.Nome) nao respondeu `"$($caso.Esperado)`"" }
            if (-not $global.Contains($caso.Esperado)) { $erros += "CLIVUS_OLA nao respondeu no idioma" }
            if ($t -match 'carregado, vers') { $erros += 'saiu mensagem em portugues' }

            if ($erros.Count -gt 0) {
                $problemas.Add("clivus-idioma-$($caso.Idioma): $($erros -join '; '). Veja $($r.Saida)")
                return $false
            }
        }

        Write-Host '  (idiomas: ingles e espanhol pelo nome traduzido e pelo global)' -ForegroundColor DarkGray
        return $true
    }
    finally {
        $env:CLIVUS_IDIOMA_TESTE = $antes
    }
}

<#
    A ativacao (plano/contrato-ativacao.md), com o servidor falso assinando
    licencas por uma chave gerada na hora. So o build Debug aceita a chave de
    teste pela variavel (o bundle instalado e Release). Sem licenca o comando
    e barrado; codigo errado e recusado; o certo ativa e o comando roda.
#>
function Testar-Ativar {
    param([string] $Desenho)

    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) {
        $problemas.Add('clivus-ativar: python nao encontrado para o servidor falso.')
        return $false
    }

    $pem = Join-Path $saida 'clivus-ativar-privada.pem'
    $licenca = Join-Path $saida 'clivus-ativar-licenca.txt'
    foreach ($a in $pem, $licenca) { if (Test-Path $a) { Remove-Item $a -Force } }

    $publica = (& $python.Source (Join-Path $PSScriptRoot 'servidor-falso.py') '--gerar-chave' $pem).Trim()
    $porta = 18766
    $servidor = Start-Process -FilePath $python.Source -ArgumentList @((Join-Path $PSScriptRoot 'servidor-falso.py'), $porta, 'x', (Join-Path $saida 'clivus-ativar-corpo.json'), $pem) `
                              -PassThru -WindowStyle Hidden
    $antes = @{ L = $env:CLIVUS_LICENCAS; C = $env:CLIVUS_LICENCA_CHAVE_TESTE; A = $env:CLIVUS_LICENCA_ARQUIVO_TESTE }

    try {
        $noAr = $false
        for ($i = 0; $i -lt 40 -and -not $noAr; $i++) {
            try { $null = Invoke-WebRequest "http://127.0.0.1:$porta/api/v1/saude" -UseBasicParsing -TimeoutSec 1; $noAr = $true }
            catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $noAr) { $problemas.Add('clivus-ativar: o servidor falso nao subiu.'); return $false }

        $env:CLIVUS_LICENCAS = "http://127.0.0.1:$porta"
        $env:CLIVUS_LICENCA_CHAVE_TESTE = "teste=$publica"
        $env:CLIVUS_LICENCA_ARQUIVO_TESTE = $licenca

        # Sem licenca: o veto interrompe o script, por isso a primeira parte
        # e uma execucao so dela.
        $barrado = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-ativar--barrado' -Script (Join-Path $PSScriptRoot 'clivus-ativar-barrado.scr')
        $antesDe = $barrado.Texto.Substring([Math]::Max(0, $barrado.Texto.IndexOf('CLIVUS_ATIVAR_ANTES')))

        $r = Invoke-CoreConsole -Desenho $Desenho -Rotulo 'clivus-ativar' -Script (Join-Path $PSScriptRoot 'clivus-ativar.scr')

        if ($r.Estourou -or $r.Codigo -ne 0 -or $r.Texto.IndexOf('CLIVUS_ATIVAR_FIM') -lt 0) {
            $problemas.Add("clivus-ativar terminou mal (codigo $($r.Codigo)). Veja $($r.Saida)")
            return $false
        }

        $t = $r.Texto
        $errado = $t.Substring($t.IndexOf('CLIVUS_ATIVAR_ERRADO')); $errado = $errado.Substring(0, $errado.IndexOf('CLIVUS_ATIVAR_CERTO'))
        $certo = $t.Substring($t.IndexOf('CLIVUS_ATIVAR_CERTO')); $certo = $certo.Substring(0, $certo.IndexOf('CLIVUS_ATIVAR_DEPOIS'))
        $depois = $t.Substring($t.IndexOf('CLIVUS_ATIVAR_DEPOIS')); $depois = $depois.Substring(0, $depois.IndexOf('CLIVUS_ATIVAR_FIM'))

        $erros = @()
        if ($antesDe -notmatch 'sem licen.a v.lida' -or $antesDe -match 'cotas:') { $erros += 'sem licenca o comando nao foi barrado' }
        if ($errado -notmatch 'N.o ativei: c.digo n.o encontrado') { $erros += 'o codigo errado nao foi recusado com a mensagem do servidor' }
        if ($certo -notmatch 'Clivus Solar ativado para teste@clivus' -or -not (Test-Path $licenca)) { $erros += 'o codigo certo nao ativou' }
        if ($depois -notmatch 'cotas:' -or $depois -match 'sem licen.a v.lida') { $erros += 'depois de ativado o comando continuou barrado' }

        if ($erros.Count -gt 0) {
            $problemas.Add("clivus-ativar: $($erros -join '; '). Veja $($r.Saida)")
            return $false
        }

        Write-Host '  (ativar: barrado sem licenca, codigo errado recusado, codigo certo ativou, comando liberado)' -ForegroundColor DarkGray
        return $true
    }
    finally {
        $env:CLIVUS_LICENCAS = $antes.L
        $env:CLIVUS_LICENCA_CHAVE_TESTE = $antes.C
        $env:CLIVUS_LICENCA_ARQUIVO_TESTE = $antes.A
        if ($servidor -and -not $servidor.HasExited) { Stop-Process -Id $servidor.Id -Force -ErrorAction SilentlyContinue }
        if (Test-Path $pem) { Remove-Item $pem -Force }
    }
}

# ---- a parte eletrica (plano/eletrica): os casos ficam em eletrica-*.ps1 ---

. (Join-Path $PSScriptRoot 'eletrica.ps1')

# ---- a licenca da rodada ---------------------------------------------------

# Com a chave publica de producao embutida, sem licenca todo comando do
# Clivus e barrado. A rodada inteira usa uma chave de teste gerada na hora e
# uma licenca assinada por ela para esta maquina (so o build Debug aceita a
# chave pela variavel). A revalidacao aponta para uma porta fechada do proprio
# computador: o teste nunca fala com o servidor de verdade nem mexe na
# licenca real do usuario. O Testar-Ativar troca por outra e devolve esta.
$licencaAntes = @{ L = $env:CLIVUS_LICENCAS; C = $env:CLIVUS_LICENCA_CHAVE_TESTE; A = $env:CLIVUS_LICENCA_ARQUIVO_TESTE }
$pythonDaLicenca = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonDaLicenca) {
    Write-Host '  python nao encontrado: sem ele nao ha licenca de teste e o nivel 2 nao roda.' -ForegroundColor Red
    exit 1
}
$pemDaRodada = Join-Path $saida 'rodada-privada.pem'
$licencaDaRodada = Join-Path $saida 'rodada-licenca.txt'
$guid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid).MachineGuid
$sha = [System.Security.Cryptography.SHA256]::Create()
$maquina = -join ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('clivus:' + $guid.Trim().ToLowerInvariant())) | ForEach-Object { $_.ToString('x2') })
$publicaDaRodada = (& $pythonDaLicenca.Source (Join-Path $PSScriptRoot 'servidor-falso.py') '--gerar-chave' $pemDaRodada).Trim()
[System.IO.File]::WriteAllText($licencaDaRodada, (& $pythonDaLicenca.Source (Join-Path $PSScriptRoot 'servidor-falso.py') '--licenca' $pemDaRodada $maquina).Trim())
Remove-Item $pemDaRodada -Force
$env:CLIVUS_LICENCAS = 'http://127.0.0.1:9'
$env:CLIVUS_LICENCA_CHAVE_TESTE = "teste=$publicaDaRodada"
$env:CLIVUS_LICENCA_ARQUIVO_TESTE = $licencaDaRodada

# ---- os casos --------------------------------------------------------------

$passaram = 0
$total = 0

$total++
if (Testar-Caso -Rotulo 'clivus-ola' -Desenho $desenhoVazio -Script (Join-Path $PSScriptRoot 'clivus-ola.scr') `
                -Esperados @([regex]::Escape("Clivus Solar carregado, versão $versao"))) {
    $passaram++
}

# A janela da mesa (3.7) e o unico WPF fora da ribbon. Se ela for nomeada sem
# cuidado, o NETLOAD inteiro cai num host sem interface - e o sintoma e o
# plugin sumir, nao a janela falhar.
$total++
if (Testar-Caso -Rotulo 'clivus-mesa-sem-interface' -Desenho $desenhoVazio `
                -Script (Join-Path $PSScriptRoot 'clivus-mesa-sem-interface.scr') `
                -Esperados @(
                    [regex]::Escape("Clivus Solar carregado, versão $versao"),
                    [regex]::Escape('A janela da mesa precisa da interface do Civil 3D'))) {
    $passaram++
}

# A configuracao do projeto (4.4) nao precisa de terreno: roda no desenho
# vazio, salvando e reabrindo.
$total++
if (Testar-Config -Desenho $desenhoVazio) { $passaram++ }

# A migracao do nome antigo para o Clivus Solar: um desenho antigo montado em
# LISP no desenho vazio (prefixo neutro terminado em _UFV); a camada do
# usuario fica; a segunda migracao nao acha mais nada.
$total++
if (Testar-Caso -Rotulo 'clivus-migrar' -Desenho $desenhoVazio -Script (Join-Path $PSScriptRoot 'clivus-migrar.scr') `
                -Esperados @(
                    [regex]::Escape('MIGRAR Desenho passado para o nome Clivus Solar'),
                    [regex]::Escape('MIGRAR Nada do nome antigo neste desenho.'),
                    [regex]::Escape('CLIVUS_MIGRAR_LISP camada=1 velha=0 linha=CLIVUS_MESA xdata=CLIVUS_ANALISE_BORDA xvelho=0 bloco=1 blocovelho=0 dic=1 dicvelho=0 reg=CLIVUS_ANALISE_BORDA usuario=1 app=0'))) {
    $passaram++
}

# O caso do terreno conta no total SEMPRE. Antes ele era simplesmente pulado
# quando o desenho nao estava la, e o placar saia "1/1 OK", verde, afirmando
# que tudo passou enquanto o unico teste que prova a leitura do desenho nao
# tinha rodado. Teste que some em silencio e pior que teste que falha.
if ($desenhos.Count -eq 0) {
    $total++
    $problemas.Add(
        'clivus-terreno nao rodou: nao ha desenho com superficie. Congele um em ' +
        'tests\acervo (ver plano\04-testes.md).')
}
else {
    if (-not $doAcervo) {
        Write-Host "  (desenho fora do acervo; usando $($desenhos[0]))" -ForegroundColor DarkGray
    }

    foreach ($desenho in $desenhos) {
        $total++
        $rotulo = 'clivus-terreno--' + [IO.Path]::GetFileNameWithoutExtension($desenho)
        if (Testar-CasoDoTerreno -Desenho $desenho -Rotulo $rotulo) { $passaram++ }
    }

    # O carimbo roda uma vez so: salvar e reabrir custa dois processos do
    # Core Console, e o que se testa e o formato, que nao muda de desenho
    # para desenho.
    $total++
    if (Testar-Carimbo -Desenho $desenhos[0]) { $passaram++ }

    # A consulta de cota, idem.
    $total++
    if (Testar-Coordenada -Desenho $desenhos[0] -Rotulo 'clivus-coord') { $passaram++ }

    # E a area, que tambem salva e reabre.
    $total++
    if (Testar-Area -Desenho $desenhos[0]) { $passaram++ }

    $total++
    if (Testar-Alinhamento -Desenho $desenhos[0]) { $passaram++ }

    # A fileira inteira no CAD: distribuicao, alinhamento, pilares, desenho.
    $total++
    if (Testar-Fileira -Desenho $desenhos[0]) { $passaram++ }

    # E a area inteira, com o tempo medido.
    $total++
    if (Testar-Usina -Desenho $desenhos[0]) { $passaram++ }

    # A exportacao para o PVsyst, lendo o DAE de volta.
    $total++
    if (Testar-Exportar -Desenho $desenhos[0]) { $passaram++ }

    # O estado sujo da mesa, lido do XData e da cor.
    $total++
    if (Testar-Sujo -Desenho $desenhos[0]) { $passaram++ }

    # O vigia: MOVE suja, ERASE registra.
    $total++
    if (Testar-Vigia -Desenho $desenhos[0]) { $passaram++ }

    # O Refazer: apaga por area e redesenha, sem dobro.
    $total++
    if (Testar-Refazer -Desenho $desenhos[0]) { $passaram++ }

    # Recalcular uma mesa suja onde ela esta, com o mesmo GUID.
    $total++
    if (Testar-Recalcular -Desenho $desenhos[0]) { $passaram++ }

    # As pontas a mao: duas alturas, depois uma travada.
    $total++
    if (Testar-Pontas -Desenho $desenhos[0]) { $passaram++ }

    # Pintar estouros: repinta sem mover, duplicar nem trocar identidade.
    $total++
    if (Testar-Pintar -Desenho $desenhos[0]) { $passaram++ }

    # A copia ganha identidade propria; blocos com sufixo voltam ao padrao.
    $total++
    if (Testar-Copia -Desenho $desenhos[0]) { $passaram++ }

    # Recontar: conta pelo XData e consome as removidas.
    $total++
    if (Testar-Recontar -Desenho $desenhos[0]) { $passaram++ }

    # Validar: registros, sujas, duplicadas, orfas, removidas, terreno.
    $total++
    if (Testar-Validar -Desenho $desenhos[0]) { $passaram++ }

    # A conta da selecao (a caixa flutuante usa a mesma).
    $total++
    if (Testar-Selecao -Desenho $desenhos[0]) { $passaram++ }

    # Grupos: criar, listar, recalcular, selecionar, apagar.
    $total++
    if (Testar-Grupos -Desenho $desenhos[0]) { $passaram++ }

    # Numerar: a usina invertida pela F1.1 e pela ultima fileira.
    $total++
    if (Testar-Numerar -Desenho $desenhos[0]) { $passaram++ }

    # Alturas: apagadas a mao e regeradas, sem orfao.
    $total++
    if (Testar-Alturas -Desenho $desenhos[0]) { $passaram++ }

    # Apagar tudo: so a area e o alinhamento ficam; camadas com cor.
    $total++
    if (Testar-ApagarTudo -Desenho $desenhos[0]) { $passaram++ }

    # Declividade: seta e valor por mesa, em graus e porcentagem.
    $total++
    if (Testar-Declividade -Desenho $desenhos[0]) { $passaram++ }

    # Analises independentes (8.9 a 8.11).
    $total++
    if (Testar-Analises -Desenho $desenhos[0]) { $passaram++ }

    # Resumo do terreno (8.15).
    $total++
    if (Testar-TerrenoResumo -Desenho $desenhos[0]) { $passaram++ }

    # Estilos do projeto (8.13).
    $total++
    if (Testar-Estilos -Desenho $desenhos[0]) { $passaram++ }

    # Tags (8.14).
    $total++
    if (Testar-Tags -Desenho $desenhos[0]) { $passaram++ }

    # Usina com dois tipos de mesa (8.5 e 8.6).
    $total++
    if (Testar-UsinaMista -Desenho $desenhos[0]) { $passaram++ }

    # Trocar mesa e regerar fileira (9.2 e 9.3).
    $total++
    if (Testar-Trocar -Desenho $desenhos[0]) { $passaram++ }

    # Arvore como objeto de sombra, acompanhando o terreno (9.4).
    $total++
    if (Testar-Arvore -Desenho $desenhos[0]) { $passaram++ }

    # Sombras num instante e no dia inteiro, pior caso (9.7 e 9.8).
    $total++
    if (Testar-Sombras -Desenho $desenhos[0]) { $passaram++ }

    # As sombras pelo botao da janela (fora de comando), arvore fora da fileira.
    $total++
    if (Testar-SombrasJanela -Desenho $desenhos[0]) { $passaram++ }

    # 3D no navegador (9.9).
    $total++
    if (Testar-Ver3D -Desenho $desenhos[0]) { $passaram++ }

    # Envio da usina ao servidor 3D (contrato), num servidor falso local.
    $total++
    if (Testar-Publicar3D -Desenho $desenhos[0]) { $passaram++ }

    # Ativacao com licenca assinada, num servidor falso local.
    $total++
    if (Testar-Ativar -Desenho $desenhos[0]) { $passaram++ }

    # Os idiomas: ingles e espanhol (etapa 10).
    $total++
    if (Testar-Idioma -Desenho $desenhos[0]) { $passaram++ }

    # A parte eletrica (plano/eletrica), um caso por passo.
    foreach ($caso in $CasosEletricos) {
        $total++
        if (& $caso -Desenho $desenhos[0]) { $passaram++ }
    }
}

# ---- veredito --------------------------------------------------------------

$env:CLIVUS_LICENCAS = $licencaAntes.L
$env:CLIVUS_LICENCA_CHAVE_TESTE = $licencaAntes.C
$env:CLIVUS_LICENCA_ARQUIVO_TESTE = $licencaAntes.A

if ($problemas.Count -eq 0 -and $passaram -eq $total) {
    Escrever-Linha 'Nivel 2' "$passaram/$total" 'OK'
    exit 0
}

Escrever-Linha 'Nivel 2' "$passaram/$total" 'FALHOU'
foreach ($p in $problemas) { Write-Host "  $p" -ForegroundColor Red }
exit 1
