"""Serviço do Clivus Solar: biblioteca de módulos.

Passo 8.3 (Melhorias.docx, 01/10/2026). Roda no localhost agora e sobe no
Coolify depois, sem mudar o código: só a `DATABASE_URL`.
"""

import json
from pathlib import Path
from urllib.parse import quote

import math

from fastapi import Depends, FastAPI, HTTPException, Request, status
from fastapi.encoders import jsonable_encoder
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, PlainTextResponse
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.db import obter_sessao
from app.models import Modulo, chave_do_modelo
from app.schemas import ModuloEntrada, ModuloSaida

VERSAO = "2"

MODULOS_INICIAIS = Path(__file__).with_name("modulos_iniciais.json")

app = FastAPI(title="Clivus Solar — serviço do plugin", version=VERSAO)


@app.exception_handler(RequestValidationError)
async def recusar(_: Request, erro: RequestValidationError) -> JSONResponse:
    """O 422 de sempre, com o valor recusado em texto quando ele é NaN ou
    infinito: o JSON não os escreve, e o 422 virava 500 (revisão do 38.6)."""

    def limpo(item: dict) -> dict:
        valor = item.get("input")
        if isinstance(valor, float) and not math.isfinite(valor):
            item = {**item, "input": str(valor)}
        return item

    return JSONResponse(status_code=422, content={"detail": jsonable_encoder([limpo(e) for e in erro.errors()])})


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
    modulo = Modulo(**entrada.model_dump(exclude=_DO_PAN), chave=chave_do_modelo(entrada.modelo))
    modulo.guardar_eletrica(entrada.eletrica, entrada.pan, entrada.pan_nome)
    sessao.add(modulo)
    _gravar(sessao, entrada)
    return modulo


@app.put("/modulos/{modulo_id}", response_model=ModuloSaida)
def alterar_modulo(modulo_id: str, entrada: ModuloEntrada, sessao: Session = Depends(obter_sessao)):
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Módulo não encontrado.")

    _recusar_modelo_repetido(sessao, entrada, menos=modulo_id)

    for campo, valor in entrada.model_dump(exclude=_DO_PAN).items():
        setattr(modulo, campo, valor)
    # Sem a parte elétrica e sem o PAN, o módulo fica com os que tem: a lista
    # não devolve o texto do PAN, e um GET, editar, PUT o apagaria.
    if entrada.eletrica is not None or entrada.pan is not None:
        modulo.guardar_eletrica(entrada.eletrica, entrada.pan, entrada.pan_nome)
    modulo.chave = chave_do_modelo(entrada.modelo)

    _gravar(sessao, entrada)
    return modulo


@app.get("/modulos/{modulo_id}/pan", response_class=PlainTextResponse)
def baixar_pan(modulo_id: str, sessao: Session = Depends(obter_sessao)):
    """O .PAN do módulo, como o usuário mandou (passo 38.6)."""
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None or modulo.pan is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Este módulo não tem .PAN.")
    return PlainTextResponse(modulo.pan, headers={"Content-Disposition": _anexo(modulo.pan_nome or "modulo.pan")})


def _anexo(nome: str) -> str:
    """O cabeçalho do anexo com qualquer nome: o ASCII (sem aspas nem controle) e o UTF-8."""
    ascii_ = "".join(c if 32 <= ord(c) < 127 and c not in '"\\' else "_" for c in nome)
    return f"attachment; filename=\"{ascii_}\"; filename*=UTF-8''{quote(nome, safe='')}"


@app.delete("/modulos/{modulo_id}", status_code=status.HTTP_204_NO_CONTENT)
def apagar_modulo(modulo_id: str, sessao: Session = Depends(obter_sessao)):
    modulo = sessao.get(Modulo, modulo_id)
    if modulo is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Módulo não encontrado.")
    sessao.delete(modulo)
    sessao.commit()


# Os campos da entrada que não são colunas com o mesmo nome (o .PAN e a parte elétrica).
_DO_PAN = {"eletrica", "pan", "pan_nome"}


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
