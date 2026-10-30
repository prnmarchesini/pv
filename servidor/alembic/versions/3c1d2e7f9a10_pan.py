"""a parte elétrica e o .PAN do módulo (passo 38.6)

Revision ID: 3c1d2e7f9a10
Revises: 7b6ef2a96f07
Create Date: 2026-10-30 16:00:00

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


revision: str = '3c1d2e7f9a10'
down_revision: Union[str, Sequence[str], None] = '7b6ef2a96f07'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None

_FLUTUANTES = ('voc', 'isc', 'vmp', 'imp', 'mu_voc', 'mu_isc', 'noct', 'mu_pmp')


def upgrade() -> None:
    """Upgrade schema."""
    with op.batch_alter_table('modulos') as tabela:
        for coluna in _FLUTUANTES:
            tabela.add_column(sa.Column(coluna, sa.Float(), nullable=True))
        tabela.add_column(sa.Column('pan', sa.Text(), nullable=True))
        tabela.add_column(sa.Column('pan_nome', sa.String(length=200), nullable=True))


def downgrade() -> None:
    """Downgrade schema."""
    with op.batch_alter_table('modulos') as tabela:
        tabela.drop_column('pan_nome')
        tabela.drop_column('pan')
        for coluna in reversed(_FLUTUANTES):
            tabela.drop_column(coluna)
