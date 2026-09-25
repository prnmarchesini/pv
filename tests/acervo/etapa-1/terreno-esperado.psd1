<#
    Resultado esperado da leitura de cada desenho do acervo.

    Congelado em 25/09/2026. O Renan decidiu nesse dia que so valida o que se
    ve no Civil 3D; arquivos de acervo e manifesto passaram a ser do Claude
    Code. Enquanto estiver em tests/proposto, o teste de nivel 2 usa estes
    valores e AVISA que ainda nao foram congelados.

    De onde vieram, e isto muda o valor de cada linha:

    - Curvas Itatiba: das propriedades da superficie no proprio Civil 3D,
      conferidas pelo Renan em 22/09/2026 na aba Statistics. Sao numeros
      independentes do plugin, e por isso valem como gabarito de verdade.

    - Porto Feliz: da saida do proprio plugin. NAO foram conferidos contra o
      Civil 3D, e nao vao ser: o Renan nao confere tabela de numeros. Servem
      para travar regressao — se a leitura mudar, o teste acusa — mas nao
      provam que a leitura esta certa, porque um numero tirado do plugin
      concorda com o plugin mesmo quando ele erra. O campo Conferido fica
      em $false de proposito, para ninguem ler isto como gabarito.

    Conferencia do Itatiba, feita na tela:
      Number of points ....... 12621
      Number of triangles .... 25107
      Minimum elevation ...... 707.000 m
      Maximum elevation ...... 764.000 m
      2D surface area ........ 114854.46 m2
      3D surface area ........ 116763.13 m2

    A tolerancia e de 0,01 nas areas e nas cotas, que e a ultima casa que o
    Civil 3D mostra. Triangulos e pontos sao exatos.

    Superficie processada: o comando automatico pega a primeira em ordem
    alfabetica, que e a mesma ordem da janela de escolha.
#>
@{
    'Curvas Itatiba' = @{
        # Conferido contra o Civil 3D pelo Renan em 22/09/2026.
        Conferido         = $true
        Superficies       = 1
        Superficie        = 'Topografo (1)'
        Pontos            = 12621
        Triangulos        = 25107
        CotaMinima        = 707.000
        CotaMaxima        = 764.000
        AreaEmPlanta      = 114854.46
        AreaDoTerreno     = 116763.13
    }

    'Porto Feliz - 2 Superficies' = @{
        # NAO conferido contra o Civil 3D: sai do proprio plugin. Trava regressao, nao prova acerto.
        Conferido         = $false
        Superficies       = 2
        Superficie        = 'Sul - Hibrido'
        Pontos            = 342
        Triangulos        = 650
        CotaMinima        = 510.000
        CotaMaxima        = 534.000
        AreaEmPlanta      = 14741.45
        AreaDoTerreno     = 15079.27
    }
}
