# Progresso do primeiro cliente (etapa 43)

Status possíveis: PENDENTE, EM ANDAMENTO, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

- 43.1 Gestão de clientes e códigos de ativação pelo administrador (`/gestao`): AGUARDANDO VALIDAÇÃO — clivussolar `main` 5e9c352, publicado em 31/10/2026 (https://clivus.marcheng.com.br/gestao); gate `validate_clientes` (204 verificações no SQLite, 206 no Postgres 17)
- 43.2 A área do cliente completa (conta, senha, código e máquinas, baixar com SHA-256, projetos 3D, suporte): AGUARDANDO VALIDAÇÃO — o mesmo commit; a troca de senha no primeiro acesso (`/senha`)
- 43.3 O plugin do cliente se identifica pela licença no 3D e nos módulos: servidor feito (`Authorization: Licenca` em `/api/v1/cenas` e `/api/v1/modulos`); plugin EM ANDAMENTO (branch `cliente-plugin`)
- 43.4 Atualizações: a versão publicada no servidor; o plugin avisa, baixa, confere e instala quando o Civil 3D fecha: servidor feito (`/gestao/versoes`, `GET /api/v1/versao`); plugin e instalador EM ANDAMENTO (branch `cliente-plugin`)
- 43.5 O executável de teste na KingHost (0.1.1) e a atualização de ponta a ponta: PENDENTE

## Observações

- 43.1 e 43.2 (web, 31/10/2026), feitos por subagente no branch `clientes` e juntados na
  `main` (avanço direto), com revisão de segurança por outro subagente. O que a revisão achou
  e foi corrigido:
  - formulário vindo de outra página (o cookie `SameSite=Lax` não barra outro `*.sslip.io`)
    leva 403, pelo `Sec-Fetch-Site` ou pelo `Origin`;
  - a URL do instalador só vale em `<CLIVUS_SITE_URL>/baixar/`;
  - a validade vai até 31/12/2100 (31/12/9999 derrubava as páginas);
  - reativar a conta não devolve as sessões de antes;
  - o `Bearer` não publica com a Develop desativada ou vencida;
  - pedidos simultâneos: trava de linha no Postgres.
- Decisões do agente, seguindo o que existia:
  - o cliente gera o próprio código, a não ser que a gestão desmarque isso;
  - revogar fica sempre com o cliente;
  - conta vencida não gera código;
  - a licença nunca passa do fim da validade (o último dia vale inteiro, até a meia-noite de
    Brasília) e, perto do fim, revalida aos 2/3 do prazo.
- No Coolify:
  - `CLIVUS_ADM_EMAILS` vazia: a gestão é a conta Develop;
  - `CLIVUS_SUPORTE_EMAIL` não existe: a área do cliente não mostra contato de suporte.
    Falta o Renan dizer qual e-mail usar.
- Fica de fora (anotado pelo agente):
  - limite de tentativas no `/entrar`, no `/senha` e no `/ativar`;
  - cota por conta para cenas e módulos;
  - quem copiar o `licenca.txt` publica naquela conta até a licença ser cortada;
  - sem internet, o plugin só para na próxima revalidação (até 30 dias);
  - as notas da versão são num idioma só, e o tamanho do instalador é digitado em bytes.
- O app atende em `https://clivus.marcheng.com.br` desde 31/10/2026, às 02h25, e o sslip
  continua no mesmo app. O plugin usa o domínio novo desde o 9c3bf25, e a "área do cliente"
  da landing aponta para ele (clivussolar e598e98).
