# O primeiro cliente (etapa 43)

Pedido do Renan, 31/10/2026: "Quero também que deixe 100% útil a área do cliente e a parte de
geração de código de ativação pelo painel administrativo, vou passar para o primeiro cliente,
quero que o cliente receba atualizações também, veja a melhor forma, pode gerar um executável
para teste que fica na kinghost."

## O que já existia

- **App** (`prnmarchesini/clivussolar`, pasta `app/`): login, a página da conta, o código
  gerado pelo próprio cliente, as licenças ECDSA (`plano/contrato-ativacao.md`), as cenas 3D
  e a biblioteca de módulos com a gestão.
- **Contas:** uma só, a Develop.
- **Instalador:** o `tools/gerar-instalador.ps1` gera o `ClivusSolar-Setup-<versão>.exe`, que
  sobe por SFTP (usuário `clivus-upload`) para o `/baixar/` do site.
- **O furo para um cliente:** o plugin publica o 3D e os módulos com a chave única
  `CLIVUS_SERVIDOR_CHAVE`, que só existe no PC do Renan.

## As decisões (minhas, para o Renan confirmar)

1. **A gestão fica no app**, em `/gestao` (onde a gestão dos módulos já está), só para
   `CLIVUS_ADM_EMAILS`. O `adm/` continua a página de espera.
2. **O administrador cria o cliente:** e-mail, plano, validade e quantas máquinas.
   - A senha inicial aparece uma vez para ele passar ao cliente; o cliente troca no primeiro
     acesso. Não há e-mail configurado.
   - O administrador gera o código de ativação.
   - A validade da conta vale na ativação e na revalidação.
3. **O plugin do cliente se identifica pela própria licença** (`Authorization: Licenca`),
   assinada pelo servidor. A chave única fica para a conta Develop.
4. **Atualizações:**
   - O administrador publica a versão em `/gestao/versoes` (número, URL do instalador no
     `/baixar/`, SHA-256, notas).
   - `GET /api/v1/versao` diz a versão publicada.
   - O plugin consulta ao abrir (uma vez por dia). Com versão nova, avisa e dá o botão
     Atualizar: baixa o instalador, confere o SHA-256 e o deixa esperando o Civil 3D fechar
     para instalar sozinho.
   - A área do cliente mostra a mesma versão para baixar.
5. **O executável de teste** é uma versão nova (`0.1.1`), gerada e subida para o `/baixar/` da
   KingHost, para provar a atualização de ponta a ponta.

## Os passos

Ver `PROGRESSO.md`.
