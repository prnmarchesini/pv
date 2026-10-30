"""Formato das requisições e respostas.

Os limites são os mesmos de `Clivus.Core.SolarModule`: o plugin recusaria um
módulo fora deles, então o serviço recusa antes de gravar.
"""

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

MENOR_MEDIDA = 0.005
MAIOR_MEDIDA = 3.0
MENOR_POTENCIA = 1.0
MAIOR_POTENCIA = 2000.0


class EletricaDoModulo(BaseModel):
    """A parte elétrica do .PAN (passo 38.6), como no plugin (`Clivus.Core.PanModule`).

    Tensões em V e correntes em A; os coeficientes da Voc e da Isc em V/°C e
    A/°C, o da potência em %/°C. Os limites são os do plugin: ele recusaria
    um módulo fora deles, e a lista inteira cairia para a biblioteca embutida.
    """

    # Sem NaN nem infinito: um só módulo assim derrubava a lista de todos
    # (o banco grava NULL, a resposta não valida). Revisão do 38.6.
    voc: float = Field(gt=0, le=3000, allow_inf_nan=False)
    isc: float = Field(gt=0, le=100, allow_inf_nan=False)
    vmp: float = Field(gt=0, le=3000, allow_inf_nan=False)
    imp: float = Field(gt=0, le=100, allow_inf_nan=False)
    mu_voc: float = Field(ge=-10, le=10, allow_inf_nan=False, description="Coeficiente da Voc, V/°C")
    mu_isc: float = Field(ge=-1, le=1, allow_inf_nan=False, description="Coeficiente da Isc, A/°C")
    noct: float | None = Field(default=None, ge=0, le=100, allow_inf_nan=False)
    mu_pmp: float | None = Field(default=None, ge=-5, le=5, allow_inf_nan=False, description="Coeficiente da potência, %/°C")

    @model_validator(mode="after")
    def pontos_do_datasheet(self):
        # Mesma recusa do plugin (PanModule.WhyInvalid).
        if self.vmp > self.voc or self.imp > self.isc:
            raise ValueError("Vmp maior que Voc ou Imp maior que Isc")
        return self


class ModuloBase(BaseModel):
    marca: str = Field(min_length=1, max_length=120)
    modelo: str = Field(min_length=1, max_length=120)
    potencia_w: float = Field(ge=MENOR_POTENCIA, le=MAIOR_POTENCIA, description="Potência em Wp")
    altura_m: float = Field(ge=MENOR_MEDIDA, le=MAIOR_MEDIDA, description="Lado maior, em metro")
    largura_m: float = Field(ge=MENOR_MEDIDA, le=MAIOR_MEDIDA, description="Lado menor, em metro")
    espessura_m: float = Field(ge=MENOR_MEDIDA, le=MAIOR_MEDIDA, description="Espessura, em metro")

    @field_validator("marca", "modelo")
    @classmethod
    def sem_espacos_nas_pontas(cls, valor: str) -> str:
        valor = valor.strip()
        if not valor:
            raise ValueError("não pode ficar em branco")
        return valor

    @model_validator(mode="after")
    def altura_maior_que_largura(self):
        # Mesma recusa do plugin (SolarModule.LooksSwapped): módulo mais largo
        # que alto é quase sempre os dois campos trocados, e a mesa sairia com
        # metade do comprimento.
        if self.largura_m > self.altura_m:
            raise ValueError("a largura está maior que a altura; confira se os dois campos não foram trocados")
        # Mesma regra de SolarModule.IsValid: a espessura é a menor medida.
        # Sem isto o serviço gravaria um módulo que o plugin recusa, e a
        # lista inteira cairia para a biblioteca embutida.
        if self.espessura_m >= self.largura_m:
            raise ValueError("a espessura precisa ser menor que a largura")
        return self


class ModuloEntrada(ModuloBase):
    """O módulo que chega: as medidas e, quando ele veio de um .PAN, a parte
    elétrica e o arquivo (o texto como o usuário mandou, até 500 mil caracteres)."""

    eletrica: EletricaDoModulo | None = None
    pan: str | None = Field(default=None, max_length=500_000)
    pan_nome: str | None = Field(default=None, max_length=255)

    @field_validator("pan")
    @classmethod
    def sem_nul(cls, valor: str | None) -> str | None:
        return None if valor is None else valor.replace("\x00", "")


class ModuloSaida(ModuloBase):
    """O módulo que sai: sem o texto do .PAN (ele vem em /modulos/{id}/pan)."""

    model_config = ConfigDict(from_attributes=True)

    id: str
    eletrica: EletricaDoModulo | None = None
    tem_pan: bool = False
    pan_nome: str | None = None
