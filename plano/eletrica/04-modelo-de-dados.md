# Modelo de dados da parte elétrica

As entidades e como se amarram. Tudo em `Clivus.Core`, sem CAD. GUID próprio por entidade, vínculo guardado em dado, nunca em posição.

## Entidades

### String
- GUID, sequência de módulos que a compõem (podem ser de mais de uma mesa).
- Traçado: lista ordenada de módulos com polaridade por ponta (positivo/negativo).
- Tipo de traçado por trecho: convencional ou leapfrog.
- Vínculo: a qual inversor pertence (ou nenhum).
- Tag (preenchida na numeração).

### Tipo de string (modelo, na biblioteca)
- Nome (modelo 1, 2, 3).
- Assinatura de arranjo: quantidade e disposição das mesas que ele cobre (ex. uma mesa de 28, duas mesas de 14), para o gerador casar só com grupos iguais.
- Traçado do modelo: onde fica positivo e negativo, trechos convencionais e leapfrog, traçado livre.
- Operações: editar, apagar, clonar, espelhar (inverte positivo com negativo).

### Inversor (modelo + instância)
- Modelo: nome (genérico do cliente ou cadastrado, ex. Huawei 250), nº de MPPT e a lista das entradas de cada MPPT (ex.: 5 MPPTs com 4, 4, 4, 5 e 5; pedido do Renan em 05/10/2026: cada MPPT tem a sua quantidade), total de entradas = a soma. Sem balanceamento por ora, mas estrutura preparada para crescer.
- Dimensão física (largura, comprimento, altura) para o retângulo 3D.
- Instância: pertence a um modelo; lista de strings alocadas; posição em campo; tag; vínculo com o trafo (via agrupamento).

### Transformador (trafo)
- Cadastro genérico: transformador, apelido, tensão de entrada, tensão de saída, potência, e campos elétricos (ex. 13.800 V, fator K, impedância). Tabela de padrões, mas mais cadastro livre que catálogo.
- Dimensão física (largura, comprimento, altura) para o retângulo 3D.
- Apelido serve de tag (editável).
- Posição em campo.
- Vínculo: recebe grupos de inversores (o skid).

### Unidade consumidora (subestação)
- Dois modos: compartilhada (C1, C2... cada uma associada a um ou mais trafos numa tabela, com nome da UC) ou unitária (vários bloquinhos independentes, cada um com vínculo).
- Dimensão física por bloquinho, nome por bloquinho, posição em campo.
- Vínculo: liga a trafo(s).

## A cadeia
UC -> trafo -> inversor -> string -> módulos. Cada elo guarda o GUID do elo acima. O resumo do sistema e qualquer consulta andam por esses vínculos, nunca por distância no desenho.

## Representação física
Todo equipamento (UC, trafo, inversor) tem um retângulo alocado em campo, dimensão vinda do cadastro, tag visível de cima, travado numa altura padrão (ex. 80 cm) no 3D. É representação; o vínculo é independente dela.
