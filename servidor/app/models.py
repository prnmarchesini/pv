"""Tabelas do banco."""

import uuid
from datetime import datetime, timezone

from sqlalchemy import DateTime, Float, String, Text, UniqueConstraint
from sqlalchemy.orm import Mapped, deferred, mapped_column

from app.db import Base


def _agora() -> datetime:
    return datetime.now(timezone.utc)


class Modulo(Base):
    """Um módulo fotovoltaico da biblioteca.

    Medidas em metro e potência em watt-pico, como no plugin
    (`Clivus.Core.SolarModule`). Marca e modelo identificam o módulo: é por eles
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
    # A parte elétrica do .PAN (passo 38.6), quando o módulo veio de um: em V
    # e A, os coeficientes em V/°C e A/°C, o da potência em %/°C.
    voc: Mapped[float | None] = mapped_column(Float, nullable=True)
    isc: Mapped[float | None] = mapped_column(Float, nullable=True)
    vmp: Mapped[float | None] = mapped_column(Float, nullable=True)
    imp: Mapped[float | None] = mapped_column(Float, nullable=True)
    mu_voc: Mapped[float | None] = mapped_column(Float, nullable=True)
    mu_isc: Mapped[float | None] = mapped_column(Float, nullable=True)
    noct: Mapped[float | None] = mapped_column(Float, nullable=True)
    mu_pmp: Mapped[float | None] = mapped_column(Float, nullable=True)
    # O arquivo como o usuário mandou. Adiado: a lista não carrega o texto.
    pan: Mapped[str | None] = deferred(mapped_column(Text, nullable=True))
    pan_nome: Mapped[str | None] = mapped_column(String(200), nullable=True)
    criado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_agora)
    alterado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_agora, onupdate=_agora)


    @property
    def eletrica(self) -> dict | None:
        """A parte elétrica, ou None quando o módulo não veio de um .PAN."""
        if self.voc is None:
            return None
        return {
            "voc": self.voc, "isc": self.isc, "vmp": self.vmp, "imp": self.imp,
            "mu_voc": self.mu_voc, "mu_isc": self.mu_isc, "noct": self.noct, "mu_pmp": self.mu_pmp,
        }

    @property
    def tem_pan(self) -> bool:
        return self.pan_nome is not None

    def guardar_eletrica(self, eletrica, pan: str | None, pan_nome: str | None) -> None:
        """Grava a parte elétrica e o .PAN que vieram (ou limpa, se não vieram)."""
        campos = ("voc", "isc", "vmp", "imp", "mu_voc", "mu_isc", "noct", "mu_pmp")
        for campo in campos:
            setattr(self, campo, getattr(eletrica, campo) if eletrica is not None else None)
        self.pan = pan
        self.pan_nome = (pan_nome or "modulo.pan") if pan is not None else None


def chave_do_modelo(modelo: str) -> str:
    """O modelo como o plugin compara: sem espaço nas pontas, sem maiúscula."""
    return modelo.strip().casefold()
