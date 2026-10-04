"""Formato das requisições e respostas.

Os limites são os mesmos de `Clivus.Core.SolarModule`: o plugin recusaria um
módulo fora deles, então o serviço recusa antes de gravar.
"""

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

MENOR_MEDIDA = 0.005
MAIOR_MEDIDA = 3.0
MENOR_POTENCIA = 1.0
MAIOR_POTENCIA = 2000.0


class ModuloEntrada(BaseModel):
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


class ModuloSaida(ModuloEntrada):
    model_config = ConfigDict(from_attributes=True)

    id: str
