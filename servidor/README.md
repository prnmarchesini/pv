# Serviço do Clivus Solar

Biblioteca de módulos do plugin (passo 8.3, Melhorias.docx de 01/10/2026).
FastAPI, SQLAlchemy e Alembic, no padrão do meuPlano. Sobe no Coolify com a
`DATABASE_URL` do Postgres; no localhost, com Docker ou sem.

## Rodar no localhost

Com Docker (Postgres de verdade):

    docker compose up -d --build

Sem Docker (SQLite em `clivus.db`):

    pip install -r requirements-dev.txt   # com os de teste
    alembic upgrade head
    python semear.py
    uvicorn app.main:app --port 8765

Desde o 38.8 (30/10/2026), o plugin lê os módulos do **servidor do Clivus**
(produção, `/api/v1/modulos`), onde o módulo mandado fica pendente até a gestão
validar. Para usar este serviço local, defina a variável de ambiente
`CLIVUS_SERVICO=http://localhost:8765` **antes** de abrir o Civil 3D; sem ela,
um cadastro de teste vai para a produção. Sem serviço no ar, o plugin usa a
biblioteca embutida e avisa.

## Rotas

| Método | Rota | O que faz |
|---|---|---|
| GET | `/saude` | serviço no ar |
| GET | `/modulos` | lista |
| GET | `/modulos/{id}` | um módulo |
| POST | `/modulos` | cadastra |
| PUT | `/modulos/{id}` | altera |
| DELETE | `/modulos/{id}` | apaga |
| GET | `/modulos/{id}/pan` | o .PAN do módulo, como foi cadastrado (38.6) |

Medidas em metro, potência em Wp, os mesmos limites do plugin. Modelo é
único (ignorando maiúscula).

## Testes

    python -m pytest -q
