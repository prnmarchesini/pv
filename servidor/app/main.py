"""Serviço do plugin UFV: biblioteca de módulos.

Passo 8.3 (Melhorias.docx, 01/10/2026). Roda no localhost agora e sobe no
Coolify depois, sem mudar o código: só a `DATABASE_URL`.
"""

import json
from pathlib import Path

from fastapi import Depends, FastAPI, HTTPException, status
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.db import obter_sessao
from app.models import Modulo, chave_do_modelo
from app.schemas import ModuloEntrada, ModuloSaida

VERSAO = "1"

MODULOS_INICIAIS = Path(__file__).with_name("modulos_iniciais.json")

app = FastAPI(title="UFV — serviço do plugin", version=VERSAO)


@app.get("/saude")
def saude() -> dict:
    """O plugin chama isto para saber se o serviço está no ar."""
    return {"ok": True, "versao": VERSAO}


@app.get("/modulos", response_model=list[ModuloSaida])
def listar_modulos(sessao: Session = Depends(obter_sessao)):
    return sessao.scalars(select(Modulo).order_by(Modulo.marca, Modulo.potencia_w, Modulo.modelo)).all()


@app.get("/modulos/{modulo_id}", response_model=ModuloSaida)
def obter_modulo(modulo_id: str, sessao: Session = Depends(obter_sessao)):
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Módulo não encontrado.")
    return modulo


@app.post("/modulos", response_model=ModuloSaida, status_code=status.HTTP_201_CREATED)
def cadastrar_modulo(entrada: ModuloEntrada, sessao: Session = Depends(obter_sessao)):
    _recusar_modelo_repetido(sessao, entrada)
    modulo = Modulo(**entrada.model_dump(), chave=chave_do_modelo(entrada.modelo))
    sessao.add(modulo)
    _gravar(sessao, entrada)
    return modulo


@app.put("/modulos/{modulo_id}", response_model=ModuloSaida)
def alterar_modulo(modulo_id: str, entrada: ModuloEntrada, sessao: Session = Depends(obter_sessao)):
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Módulo não encontrado.")

    _recusar_modelo_repetido(sessao, entrada, menos=modulo_id)

    for campo, valor in entrada.model_dump().items():
        setattr(modulo, campo, valor)
    modulo.chave = chave_do_modelo(entrada.modelo)

    _gravar(sessao, entrada)
    return modulo


@app.delete("/modulos/{modulo_id}", status_code=status.HTTP_204_NO_CONTENT)
def apagar_modulo(modulo_id: str, sessao: Session = Depends(obter_sessao)):
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Módulo não encontrado.")
    sessao.delete(modulo)
    sessao.commit()


def _recusar_modelo_repetido(sessao: Session, entrada: ModuloEntrada, menos: str | None = None) -> None:
    """Modelo igual ignorando maiúscula é o mesmo módulo para o plugin.

    Só para a mensagem sair antes do INSERT; quem garante, inclusive com dois
    cadastros ao mesmo tempo, é a restrição única em `chave`.
    """
    consulta = select(Modulo.id).where(Modulo.chave == chave_do_modelo(entrada.modelo))
    if menos is not None:
        consulta = consulta.where(Modulo.id != menos)
    if sessao.scalar(consulta.limit(1)) is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, f"Já existe o módulo {entrada.modelo}.")


def _gravar(sessao: Session, entrada: ModuloEntrada) -> None:
    try:
        sessao.commit()
    except IntegrityError:
        sessao.rollback()
        raise HTTPException(
            status.HTTP_409_CONFLICT,
            f"Já existe o módulo {entrada.modelo}.",
        ) from None


def semear(sessao: Session) -> int:
    """Põe no banco vazio os módulos que vinham embutidos no plugin.

    Só roda com a tabela vazia: depois disso o banco é a fonte, e apagar um
    módulo não o faz voltar sozinho.
    """
    if sessao.scalar(select(Modulo.id).limit(1)) is not None:
        return 0

    entradas = json.loads(MODULOS_INICIAIS.read_text(encoding="utf-8"))
    for e in entradas:
        sessao.add(
            Modulo(
                marca=e["brand"],
                modelo=e["model"],
                chave=chave_do_modelo(e["model"]),
                potencia_w=e["powerWatts"],
                altura_m=e["height"],
                largura_m=e["width"],
                espessura_m=e["thickness"],
            )
        )
    sessao.commit()
    return len(entradas)
