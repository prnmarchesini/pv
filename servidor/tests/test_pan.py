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


@pytest.mark.parametrize("campo", ["mu_voc", "mu_isc", "noct", "mu_pmp", "voc"])
@pytest.mark.parametrize("valor", [float("nan"), float("inf"), float("-inf")])
def test_nan_e_infinito_sao_recusados(cliente, campo, valor):  # noqa: F811
    """Revisão do 38.6: um só módulo com NaN derrubava a lista de todos."""
    import json

    # Corpo cru: o json.dumps do Python escreve NaN e Infinity (o cliente de teste recusaria).
    errado = json.dumps({**JA, "eletrica": {**JA["eletrica"], campo: valor}})

    assert cliente.post("/modulos", content=errado, headers={"Content-Type": "application/json"}).status_code == 422
    assert cliente.get("/modulos").status_code == 200


def test_alterar_sem_o_pan_mantem_o_pan(cliente):  # noqa: F811
    """Revisão do 38.6: a lista não devolve o texto, e GET, editar, PUT o apagava."""
    criado = cliente.post("/modulos", json=JA).json()
    sem_pan = {k: v for k, v in criado.items() if k not in ("id", "eletrica", "tem_pan", "pan_nome")}

    resposta = cliente.put(f"/modulos/{criado['id']}", json={**sem_pan, "potencia_w": 715})

    assert resposta.status_code == 200, resposta.text
    assert resposta.json()["tem_pan"] is True
    assert resposta.json()["eletrica"]["voc"] == 49.0
    assert cliente.get(f"/modulos/{criado['id']}/pan").text == PAN


@pytest.mark.parametrize("nome", ["JAM72S30–545.PAN", "晶科 JKM.PAN", 'aspas "x".PAN'])
def test_o_nome_do_arquivo_com_qualquer_caractere(cliente, nome):  # noqa: F811
    criado = cliente.post("/modulos", json={**JA, "pan_nome": nome}).json()

    resposta = cliente.get(f"/modulos/{criado['id']}/pan")

    assert resposta.status_code == 200
    assert "filename*=UTF-8''" in resposta.headers["content-disposition"]
    assert resposta.text == PAN


def test_o_nul_sai_do_texto(cliente):  # noqa: F811
    criado = cliente.post("/modulos", json={**JA, "pan": PAN + "\x00"}).json()

    assert "\x00" not in cliente.get(f"/modulos/{criado['id']}/pan").text


def test_a_lista_bate_com_o_contrato_do_plugin(cliente):  # noqa: F811
    """O contrato do plugin (tests/dados/servico-modulos-com-pan.json, lido também
    pelo Core): o que o serviço devolve na lista, campo a campo."""
    import json
    from pathlib import Path

    contrato = json.loads((Path(__file__).resolve().parents[2] / "tests" / "dados" / "servico-modulos-com-pan.json").read_text(encoding="utf-8"))
    risen = {k: v for k, v in contrato[1].items() if k not in ("eletrica", "tem_pan", "pan_nome")}
    cliente.post("/modulos", json=JA)
    cliente.post("/modulos", json=risen)

    lista = [{k: v for k, v in m.items() if k != "id"} for m in cliente.get("/modulos").json()]

    assert lista == contrato

