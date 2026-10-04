<#
    Os casos de nivel 2 da parte eletrica (plano/eletrica). O rodar.ps1 carrega
    este arquivo; ele carrega cada eletrica-*.ps1 da pasta, e cada um
    acrescenta os seus casos em $CasosEletricos (o nome da funcao). Assim cada
    passo eletrico mexe so no arquivo dele.
#>
$script:CasosEletricos = @()

foreach ($arquivo in Get-ChildItem (Join-Path $PSScriptRoot 'eletrica-*.ps1') | Sort-Object Name) {
    . $arquivo.FullName
}
