# Serviço do Clivus Solar. Mesmo desenho do backend do meuPlano: sobe no
# Coolify com a DATABASE_URL do Postgres; sem ela, usa SQLite no container.
FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8765
CMD ["/bin/sh", "-c", "alembic upgrade head && python semear.py && uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8765}"]
