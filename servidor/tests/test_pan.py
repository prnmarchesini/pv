"""Passo 38.6: o módulo cadastrado com o .PAN (Renan, 30/10/2026: "esse arquivo
pan já subir no servidor, esse módulo já ficaria salvo no servidor [...] é
usuário subindo pan para a gente").

O serviço guarda o arquivo como veio e a parte elétrica lida dele pelo plugin
(Clivus.Core.PanModule); a lista devolve a parte elétrica, e o arquivo sai
pelo endereço próprio do módulo.
"""

import pytest

from tests.test_modulos import cliente, fabrica  # noqa: F401 (fixtures)

PAN = """PVObject_=pvModule
  Version=7.4.3
  PVObject_Commercial=pvCommercial
    Manufacturer=JA Solar
    Model=JAM66D46-720/LB
    Width=1.303
    Height=2.384
    Depth=0.033
  End of PVObject pvCommercial
  PNom=720.0
  Isc=18.590
  Voc=49.00
  Imp=17.480
  Vmp=41.19
  muISC=8.11
  muVocSpec=-119.5
  muPmpReq=-0.293
End of PVObject pvModule
"""

JA = {
    "marca": "JA Solar",
    "modelo": "JAM66D46-720/LB",
    "potencia_w": 720,
    "altura_m": 2.384,
    "largura_m": 1.303,
    "espessura_m": 0.033,
    "eletrica": {
        "voc": 49.0,
        "isc": 18.59,
        "vmp": 41.19,
        "imp": 17.48,
        "mu_voc": -0.1195,
        "mu_isc": 0.00811,
        "noct": None,
        "mu_pmp": -0.293,
    },
    "pan": PAN,
    "pan_nome": "JAM66D46-720_LB.PAN",
}


def test_cadastra_com_o_pan_e_a_lista_traz_a_eletrica(cliente):  # noqa: F811
    resposta = cliente.post("/modulos", json=JA)
    assert resposta.status_code == 201, resposta.text

    corpo = resposta.json()
    assert corpo["tem_pan"] is True
    assert corpo["pan_nome"] == "JAM66D46-720_LB.PAN"
    assert "pan" not in corpo

    lista = cliente.get("/modulos").json()
    assert len(lista) == 1
    assert lista[0]["eletrica"]["voc"] == 49.0
    assert lista[0]["eletrica"]["mu_isc"] == pytest.approx(0.00811)
    assert lista[0]["tem_pan"] is True
    assert "pan" not in lista[0]


def test_o_pan_volta_como_foi(cliente):  # noqa: F811
    criado = cliente.post("/modulos", json=JA).json()

    resposta = cliente.get(f"/modulos/{criado['id']}/pan")

    assert resposta.status_code == 200
    assert resposta.text == PAN
    assert "JAM66D46-720_LB.PAN" in resposta.headers["content-disposition"]


def test_sem_pan_o_modulo_continua_como_antes(cliente):  # noqa: F811
    sem = {k: v for k, v in JA.items() if k not in ("eletrica", "pan", "pan_nome")}
    criado = cliente.post("/modulos", json=sem).json()

    assert criado["eletrica"] is None
    assert criado["tem_pan"] is False
    assert cliente.get(f"/modulos/{criado['id']}/pan").status_code == 404


@pytest.mark.parametrize(
    "campo, valor",
    [("vmp", 50.0), ("imp", 19.0), ("voc", 0), ("isc", -1)],
)
def test_eletrica_impossivel_e_recusada(cliente, campo, valor):  # noqa: F811
    errado = {**JA, "eletrica": {**JA["eletrica"], campo: valor}}

    assert cliente.post("/modulos", json=errado).status_code == 422


def test_alterar_troca_a_eletrica_e_o_pan(cliente):  # noqa: F811
    criado = cliente.post("/modulos", json=JA).json()
    novo = {**JA, "eletrica": {**JA["eletrica"], "voc": 49.5}, "pan": PAN.replace("49.00", "49.50")}

    resposta = cliente.put(f"/modulos/{criado['id']}", json=novo)

    assert resposta.status_code == 200, resposta.text
    assert resposta.json()["eletrica"]["voc"] == 49.5
    assert "49.50" in cliente.get(f"/modulos/{criado['id']}/pan").text
