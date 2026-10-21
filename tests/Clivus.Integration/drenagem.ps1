<#
    Os casos de nivel 2 da drenagem (plano/drenagem, etapas 25 a 31). O
    rodar.ps1 carrega este arquivo; ele carrega cada drenagem-*.ps1 da pasta,
    e cada um acrescenta os seus casos em $CasosDrenagem (o nome da funcao).
#>
$script:CasosDrenagem = @()

foreach ($arquivo in Get-ChildItem (Join-Path $PSScriptRoot 'drenagem-*.ps1') | Sort-Object Name) {
    . $arquivo.FullName
}
