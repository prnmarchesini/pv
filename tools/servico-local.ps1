<#
.SYNOPSIS
    Sobe o serviço de módulos (servidor/) no localhost:8765, sem Docker.

.DESCRIPTION
    Passo 8.3. Instala as dependências, cria ou atualiza o banco SQLite
    (servidor/ufv.db), põe os módulos iniciais no banco vazio e abre o
    serviço numa janela própria. Com o serviço no ar, a janela de Mesa do
    plugin lista os módulos dele; fechada a janela, o plugin volta para a
    biblioteca embutida.

    Com Docker (Postgres), use em vez disto: docker compose up -d --build
    dentro de servidor/.

.EXAMPLE
    .\tools\servico-local.ps1
#>
$ErrorActionPreference = 'Stop'

$servidor = Join-Path (Split-Path $PSScriptRoot -Parent) 'servidor'

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python não encontrado. Instale o Python 3.12 ou mais novo.'
}

Push-Location $servidor
try {
    python -m pip install -q -r requirements.txt
    if ($LASTEXITCODE -ne 0) { throw 'pip falhou.' }

    python -m alembic upgrade head
    if ($LASTEXITCODE -ne 0) { throw 'alembic falhou.' }

    python semear.py
    if ($LASTEXITCODE -ne 0) { throw 'semear.py falhou.' }
}
finally { Pop-Location }

Start-Process python -ArgumentList '-m', 'uvicorn', 'app.main:app', '--host', '127.0.0.1', '--port', '8765' `
    -WorkingDirectory $servidor

Write-Host 'Serviço de módulos em http://localhost:8765 (feche a janela dele para parar).'
