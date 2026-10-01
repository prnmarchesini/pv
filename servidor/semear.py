"""Põe os módulos iniciais no banco vazio. Roda depois do `alembic upgrade head`."""

from app.db import Sessao
from app.main import semear

with Sessao() as sessao:
    quantos = semear(sessao)

print(f"{quantos} módulo(s) iniciais gravados." if quantos else "Banco já tinha módulos; nada gravado.")
