"""Tabelas do banco."""

import uuid
from datetime import datetime, timezone

from sqlalchemy import DateTime, Float, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from app.db import Base


def _agora() -> datetime:
    return datetime.now(timezone.utc)


class Modulo(Base):
    """Um módulo fotovoltaico da biblioteca.

    Medidas em metro e potência em watt-pico, como no plugin
    (`UFV.Core.SolarModule`). Marca e modelo identificam o módulo: é por eles
    que o perfil de mesa o encontra de volta. O modelo sozinho já é único,
    como na biblioteca do plugin (`ModuleLibrary`), que procura só por ele,
    ignorando maiúscula: a unicidade fica em `chave`, o modelo normalizado em
    Python, e não num `lower()` do banco, que no SQLite só converte ASCII.
    """

    __tablename__ = "modulos"
    __table_args__ = (UniqueConstraint("chave", name="uq_modulos_chave"),)

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    marca: Mapped[str] = mapped_column(String(120))
    modelo: Mapped[str] = mapped_column(String(120))
    chave: Mapped[str] = mapped_column(String(120))
    potencia_w: Mapped[float] = mapped_column(Float)
    altura_m: Mapped[float] = mapped_column(Float)
    largura_m: Mapped[float] = mapped_column(Float)
    espessura_m: Mapped[float] = mapped_column(Float)
    criado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_agora)
    alterado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_agora, onupdate=_agora)


def chave_do_modelo(modelo: str) -> str:
    """O modelo como o plugin compara: sem espaço nas pontas, sem maiúscula."""
    return modelo.strip().casefold()
