"""Passo 8.3: a biblioteca de módulos no serviço local."""

import subprocess
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine, inspect
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

from app.db import Base, obter_sessao
from app.main import app, semear

RAIZ = Path(__file__).resolve().parent.parent

RISEN = {
    "marca": "Risen",
    "modelo": "RSM132-8-720BHDG",
    "potencia_w": 720,
    "altura_m": 2.384,
    "largura_m": 1.303,
    "espessura_m": 0.033,
}


@pytest.fixture()
def fabrica():
    motor = create_engine("sqlite://", connect_args={"check_same_thread": False}, poolclass=StaticPool)
    Base.metadata.create_all(motor)
    return sessionmaker(bind=motor, autoflush=False, expire_on_commit=False)


@pytest.fixture()
def cliente(fabrica):
    def sessao_de_teste():
        sessao = fabrica()
        try:
            yield sessao
        finally:
            sessao.close()

    app.dependency_overrides[obter_sessao] = sessao_de_teste
    yield TestClient(app)
    app.dependency_overrides.clear()


def test_saude(cliente):
    resposta = cliente.get("/saude")
    assert resposta.status_code == 200
    assert resposta.json()["ok"] is True


def test_cadastra_e_lista(cliente):
    criado = cliente.post("/modulos", json=RISEN)
    assert criado.status_code == 201
    assert criado.json()["id"]

    lista = cliente.get("/modulos").json()
    assert len(lista) == 1
    assert lista[0]["modelo"] == "RSM132-8-720BHDG"
    assert lista[0]["largura_m"] == 1.303


@pytest.mark.parametrize(
    "variacao",
    [{"potencia_w": 730}, {"marca": "Outra"}, {"modelo": "rsm132-8-720bhdg"}],
)
def test_modelo_repetido_e_recusado(cliente, variacao):
    assert cliente.post("/modulos", json=RISEN).status_code == 201

    repetido = cliente.post("/modulos", json={**RISEN, **variacao})

    assert repetido.status_code == 409
    assert "Já existe" in repetido.json()["detail"]


def test_modelo_com_acento_em_maiuscula_e_repetido(cliente):
    assert cliente.post("/modulos", json={**RISEN, "modelo": "ÉCOLE-1"}).status_code == 201
    assert cliente.post("/modulos", json={**RISEN, "modelo": "école-1"}).status_code == 409


def test_a_restricao_unica_segura_mesmo_sem_a_checagem(fabrica):
    """Dois cadastros simultâneos passam juntos pela checagem; o banco não deixa."""
    from sqlalchemy.exc import IntegrityError

    from app.models import Modulo, chave_do_modelo

    with fabrica() as sessao:
        for modelo in ("ABC", "abc"):
            sessao.add(Modulo(marca="X", modelo=modelo, chave=chave_do_modelo(modelo),
                              potencia_w=700, altura_m=2.3, largura_m=1.1, espessura_m=0.03))
        with pytest.raises(IntegrityError):
            sessao.commit()


def test_espessura_maior_que_largura_e_recusada(cliente):
    assert cliente.post("/modulos", json={**RISEN, "espessura_m": 1.4}).status_code == 422


def test_largura_maior_que_altura_e_recusada(cliente):
    trocado = cliente.post("/modulos", json={**RISEN, "altura_m": 1.303, "largura_m": 2.384})
    assert trocado.status_code == 422


def test_marca_e_modelo_sao_aparados(cliente):
    criado = cliente.post("/modulos", json={**RISEN, "marca": "  Risen  "}).json()
    assert criado["marca"] == "Risen"


@pytest.mark.parametrize(
    "campo,valor",
    [
        ("potencia_w", 0.72),  # digitou em kW
        ("potencia_w", 5000),  # digitou a potência da string
        ("largura_m", 130.3),  # digitou em cm
        ("altura_m", 0),
        ("espessura_m", -0.03),
        ("marca", "   "),
        ("modelo", ""),
    ],
)
def test_medida_impossivel_e_recusada(cliente, campo, valor):
    resposta = cliente.post("/modulos", json={**RISEN, campo: valor})
    assert resposta.status_code == 422


def test_altera(cliente):
    id_ = cliente.post("/modulos", json=RISEN).json()["id"]

    alterado = cliente.put(f"/modulos/{id_}", json={**RISEN, "potencia_w": 725})

    assert alterado.status_code == 200
    assert cliente.put(f"/modulos/{id_}", json={**RISEN, "potencia_w": 725}).status_code == 200
    assert cliente.get(f"/modulos/{id_}").json()["potencia_w"] == 725


def test_alterar_para_um_que_ja_existe_e_recusado(cliente):
    cliente.post("/modulos", json=RISEN)
    outro = cliente.post("/modulos", json={**RISEN, "modelo": "RSM132-8-730BHDG"}).json()["id"]

    resposta = cliente.put(f"/modulos/{outro}", json=RISEN)

    assert resposta.status_code == 409


def test_apaga(cliente):
    id_ = cliente.post("/modulos", json=RISEN).json()["id"]

    assert cliente.delete(f"/modulos/{id_}").status_code == 204
    assert cliente.get(f"/modulos/{id_}").status_code == 404
    assert cliente.delete(f"/modulos/{id_}").status_code == 404


def test_inexistente_da_404(cliente):
    assert cliente.get("/modulos/nao-existe").status_code == 404
    assert cliente.put("/modulos/nao-existe", json=RISEN).status_code == 404


def test_semear_poe_os_modulos_do_plugin_so_no_banco_vazio(fabrica):
    with fabrica() as sessao:
        assert semear(sessao) == 3
        assert semear(sessao) == 0


def test_os_modulos_iniciais_sao_os_do_plugin():
    """O semeador lê uma cópia do modulos.json do Core; as duas não podem divergir."""
    do_core = (RAIZ.parent / "src" / "Clivus.Core" / "modulos.json").read_text(encoding="utf-8")
    do_servico = (RAIZ / "app" / "modulos_iniciais.json").read_text(encoding="utf-8")
    assert do_core == do_servico


def test_a_migracao_cria_a_mesma_tabela_do_modelo(tmp_path):
    banco = tmp_path / "migracao.db"
    url = f"sqlite:///{banco.as_posix()}"

    subprocess.run(
        [sys.executable, "-m", "alembic", "upgrade", "head"],
        cwd=RAIZ,
        env={**__import__("os").environ, "DATABASE_URL": url},
        check=True,
        capture_output=True,
    )

    colunas = {c["name"] for c in inspect(create_engine(url)).get_columns("modulos")}
    assert colunas == set(Base.metadata.tables["modulos"].columns.keys())
