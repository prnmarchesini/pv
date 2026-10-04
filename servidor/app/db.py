"""Conexão com o banco.

Mesmo padrão do meuPlano: `DATABASE_URL` manda; sem ela, SQLite num arquivo
ao lado, para rodar no localhost sem subir Postgres.
"""

import os

from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase, sessionmaker


def url_do_banco() -> str:
    url = os.getenv("DATABASE_URL") or "sqlite:///./clivus.db"

    # O Coolify (como o Railway e o Heroku) entrega "postgres://", que o
    # SQLAlchemy 2 não aceita mais.
    if url.startswith("postgres://"):
        url = url.replace("postgres://", "postgresql://", 1)

    return url


def criar_motor(url: str):
    argumentos = {"check_same_thread": False} if url.startswith("sqlite") else {}
    return create_engine(url, connect_args=argumentos, pool_pre_ping=True)


motor = criar_motor(url_do_banco())
Sessao = sessionmaker(bind=motor, autoflush=False, expire_on_commit=False)


class Base(DeclarativeBase):
    pass


def obter_sessao():
    sessao = Sessao()
    try:
        yield sessao
    finally:
        sessao.close()
