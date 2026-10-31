# Progresso do primeiro cliente (etapa 43)

Status possíveis: PENDENTE, EM ANDAMENTO, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

- 43.1 Gestão de clientes e códigos de ativação pelo administrador (`/gestao`): AGUARDANDO VALIDAÇÃO — clivussolar `main` 5e9c352, publicado em 31/10/2026 (https://clivus.marcheng.com.br/gestao); gate `validate_clientes` (204 verificações no SQLite, 206 no Postgres 17)
- 43.2 A área do cliente completa (conta, senha, código e máquinas, baixar com SHA-256, projetos 3D, suporte): AGUARDANDO VALIDAÇÃO — o mesmo commit; a troca de senha no primeiro acesso (`/senha`)
- 43.3 O plugin do cliente se identifica pela licença no 3D e nos módulos: AGUARDANDO VALIDAÇÃO — servidor publicado (`Authorization: Licenca` em `/api/v1/cenas` e `/api/v1/modulos`); plugin no `bifila` (branch `cliente-plugin`, 3d94b66, juntado)
- 43.4 Atualizações: a versão publicada no servidor; o plugin avisa, baixa, confere e instala quando o Civil 3D fecha: AGUARDANDO VALIDAÇÃO — servidor publicado (`/gestao/versoes`, `GET /api/v1/versao`); plugin e instalador no `bifila` (o mesmo merge)
- 43.5 O executável de teste na KingHost (0.1.1) e a atualização de ponta a ponta: AGUARDANDO VALIDAÇÃO — a 0.1.1 (2f39320) no `/baixar` (`ClivusSolar-Setup-0.1.1.exe` e o nome fixo, SHA-256 5736b5f0…d027, 6.816.292 bytes) e publicada no servidor; fora do Civil 3D, provada de ponta a ponta; no PC do Renan, a 0.1.0 com o atualizador espera a tela

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

### 31/10/2026: o lado do plugin de 43.3 e 43.4 (branch `cliente-plugin`, a partir do `202d6d7`)

O contrato com o servidor está nos dois contratos, na seção "Etapa 43"
(`plano/contrato-ativacao.md` e `plano/contrato-servidor-3d.md`).

- **A licença no servidor (43.3).** `ServerAuthorization` (Core) escolhe o
  cabeçalho: com `CLIVUS_SERVIDOR_CHAVE`, `Bearer`; sem ela e com licença
  válida (ou passada de revalidar), `Licenca {o licenca.txt}`; senão nada. O
  `Publicador3D` e o cadastro de módulos (`FonteDeModulos`) usam o mesmo. A
  recusa sem o `erro` do servidor diz o que conferir (a chave ou ativar).
- **A atualização (43.4).** Ao abrir o Civil 3D (só com interface), no máximo
  uma vez por dia, `GET /api/v1/versao` em segundo plano (5 s). A última
  consulta fica em `preferencias.json` (`ultimaConsultaDeVersao`, UTC); o
  arquivo de antes continua valendo. Com versão maior (semver,
  `SemanticVersion`), na folga e com desenho aberto: a linha de comando avisa
  e abre a janela (notas, Atualizar agora, Depois). Versão `obrigatoria`
  avisa a cada abertura. O comando `CLIVUS_ATUALIZAR` (`CLIVUS_UPDATE`,
  `CLIVUS_ACTUALIZAR`) consulta na hora e roda sem licença; botão pequeno
  "Atualizar" no painel Configurações, embaixo do Ativar e do Sobre.
- **Atualizar agora.** Baixa para `%TEMP%\Clivus Solar\ClivusSolar-Setup-<versão>.exe`
  com a barra; só de HTTPS no host do servidor ou de
  `PluginInfo.EnderecosDeDownload` (o site de download e
  `https://clivus.marcheng.com.br`), cada redirecionamento pela mesma regra
  (até 3); confere o tamanho e o SHA-256 (diferente: apaga e avisa); confere
  de novo com o arquivo travado para leitura até o Windows começar o
  instalador; roda com `/aguardar-civil3d` e diz "Feche o Civil 3D: a
  atualização instala sozinha quando ele fechar". Só troca o plugin que o
  instalador pôs (`%APPDATA%\Autodesk\ApplicationPlugins\ClivusSolar.bundle`);
  carregado de outro lugar (Arquivos de Programas, a pasta do build), a
  abertura só avisa na linha de comando, e a janela (pelo botão) explica e
  não baixa.
- **O comando.** A consulta vai para outra linha; se demora mais de 0,7 s,
  abre o "Carregando" com Cancelar (o Civil 3D não fica parado até o
  tempo-limite de 15 s).
- **O instalador com `/aguardar-civil3d`.** Sem janela; espera `acad`,
  `accoreconsole` e `Clivus.Link` fecharem (olha a cada 5 s, instala com tudo
  fechado há 10 s, desiste em 12 h). Não instala com o Windows desligando,
  nem por cima de versão igual ou mais nova, nem de volta o que o usuário
  desinstalou enquanto esperava. Se a cópia falha, tenta de novo até 3
  vezes, sempre por inteiro (a falha pode ter deixado só o começo da versão
  nova); se não der, o aviso manda reinstalar pela área do cliente. Avisa
  numa janela simples do Windows, depois de soltar o mutex. Um só por sessão
  (mutex `ClivusSolar-AtualizacaoEsperando`), e uma instalação de cada vez
  em todos os modos (mutex `ClivusSolar-Instalacao`: a janela, o silencioso
  e a espera). O registro vai para `%LOCALAPPDATA%\Clivus Solar\atualizacao.log`,
  uma linha com a hora por acontecimento; a última traz o código e a frase,
  como no silencioso. Saídas novas: 7 (outro já espera), 8 (passou do
  limite), 9 (Windows desligando).
- **A versão numa fonte só.** O `<Version>` do `Directory.Build.props`. O
  `PackageContents.xml` do repositório traz `$(Version)`, e o build grava a
  cópia com o número na saída (alvo `CarimbarPackageContents`), que o
  `publicar-bundle.ps1` põe no bundle (ele confere o número). Subir para
  0.1.1: trocar o `<Version>` e rodar `tools\gerar-instalador.ps1`.
- **Traduções:** as 59 frases novas traduzidas no merge (31/10/2026). O "Atualizar" em
  inglês passou de "Refresh" para "Update": a mesma chave é o botão novo da ribbon e os
  de reler as janelas, e "Update" serve aos dois. No instalador, "não foi preciso" virou
  "não foi necessária".
- **Revisão (subagente, worktree isolada no `85b43bb`).** Corrigido: a
  tentativa depois de uma cópia que falhou lia "já está instalada" no
  descritor novo e saía com sucesso com o bundle pela metade; o comando
  gravava a consulta do dia com versão obrigatória pendente; a lista de
  hosts não aguentava a troca de domínio; o comando parava o Civil 3D até
  15 s; um 401 com `detail` mandava conferir a chave a quem usa licença;
  chave em branco não caía na licença; a gravação da consulta podia pôr o
  padrão por cima de preferências que não se leram; o `.parcial` podia
  sobrar; o mutex ficava preso durante o aviso do fim; a janela abria todo
  dia no PC de desenvolvimento; o BundleTests não olhava o caminho até o
  bundle. Cada correção tem teste, conferido com sabotagem.
- **Para o lado do servidor (avisar a sessão do clivussolar):** o
  `app/app/services/versoes.py` aceita `0.1` e `1.2.3.4` e recusa
  `-beta.1`; o plugin só aceita `MAIOR.MENOR.CORRECAO` (com pré-versão
  opcional) e ignora o resto. E a URL sai de `CLIVUS_SITE_URL` (padrão
  `https://clivussolar.com`, que não existe): a publicada tem de estar no
  sslip da landing, no host do app ou em `clivus.marcheng.com.br`, senão o
  plugin recusa.

Roteiro de tela (43.3 e 43.4), quando o servidor publicar a parte dele:

1. Na ribbon, painel Configurações: o botão pequeno Atualizar, com a seta
   âmbar, embaixo do Ativar e do Sobre. A dica explica a atualização.
2. Sem versão publicada no servidor: Atualizar diz "O servidor ainda não tem
   versão publicada. Esta é a 0.1.0."
3. PC do cliente (sem `CLIVUS_SERVIDOR_CHAVE`, ativado): 3D publica e abre o
   link; Mesa › carregar um .PAN que não está na biblioteca › mandar para o
   servidor. No PC do Renan (com a chave), os dois continuam iguais.
4. Instalar o `ClivusSolar-Setup-0.1.0.exe` desta branch num PC sem o bundle
   de Arquivos de Programas; publicar a 0.1.1 em `/gestao/versoes`; abrir o
   Civil 3D com um desenho: a linha de comando avisa e a janela abre com as
   notas. Atualizar agora: a barra anda, "Baixado e conferido", e "Feche o
   Civil 3D". Fechar: em uns 15 s a janela do Windows diz "Clivus Solar 0.1.1
   instalado". Abrir: Sobre mostra 0.1.1; o `atualizacao.log` tem as linhas.
5. Depois: a janela fecha e só volta no dia seguinte (ou pelo botão).
6. Publicar no servidor um SHA-256 errado: Atualizar agora diz que o SHA-256
   não confere e apaga o arquivo.
7. Sem internet, Atualizar: se a resposta demora, o "Carregando" aparece com
   Cancelar, e Cancelar devolve o Civil 3D na hora.
8. No PC de desenvolvimento (bundle em Arquivos de Programas ou DLL do build),
   com a 0.1.1 publicada: a abertura só avisa na linha de comando; o botão
   Atualizar abre a janela, que explica e não deixa baixar.

### 31/10/2026: 43.5, a 0.1.1 de teste

- O merge do `cliente-plugin` no `bifila` (2e81f03), as 59 frases traduzidas e a etapa 43 no
  placar (41e840e). Bateria completa verde antes de gerar: nível 1 (etapas 0 a 43, a 43 com
  130 testes), nível 2 105/105, serviço 53/53, Sol (JS), instalador e acervo.
- A 0.1.0 deste código (com o atualizador) foi instalada no PC do Renan às 03h23
  (`instalar.ps1`; só existe a cópia em `%APPDATA%`, a que a atualização troca). Ela ainda
  não consultou o servidor: a primeira abertura do Civil 3D procura e acha a 0.1.1.
- A 0.1.1 é o mesmo código com o número novo (2f39320). Foi gerada e enviada com
  `tools/gerar-instalador.ps1 -Enviar`, que conferiu o SHA-256 baixando pelo site. O envio
  pelo pipe falhou ("Invalid command": o BOM do PowerShell 5.1) e passou a usar arquivo de
  lote (4dc7718).
- Publicada no servidor pelo serviço de versões, dentro do contêiner (`versoes.registrar`,
  as mesmas regras do formulário de `/gestao/versoes`), com a conta Develop como autora:
  id 1, não obrigatória, notas em português.
- Ponta a ponta fora do Civil 3D:
  - Um programa de teste (fora do repositório) com as funções do Core que o `Atualizacao.cs`
    usa: a consulta em `https://clivus.marcheng.com.br/api/v1/versao` achou a 0.1.1; baixou da
    landing (host aceito); o tamanho e o SHA-256 bateram; um endereço de fora foi recusado.
  - O instalador baixado, com `/aguardar-civil3d` numa pasta de teste (`/teste=`) por cima de
    uma 0.1.0 instalada pelo instalador 0.1.0: esperou, viu tudo fechado, instalou em 10 s
    na primeira tentativa, e o bundle ficou 0.1.1. A pasta e a entrada de teste do Windows
    foram apagadas depois.

Roteiro de tela (43.1 a 43.5), o caminho do primeiro cliente:

1. Civil 3D: abrir com um desenho. A linha de comando avisa "Há uma versão nova do Clivus
   Solar: 0.1.1" e a janela "Clivus Solar — Atualização" abre com as notas.
2. Atualizar agora: a barra anda, "Baixado e conferido (tamanho e SHA-256)" e "Feche o
   Civil 3D: a atualização instala sozinha quando ele fechar".
3. Fechar o Civil 3D: em uns 15 s, uma janela do Windows diz "Clivus Solar 0.1.1 instalado".
4. Abrir de novo: o aviso da DLL não assinada (Always Load) e o Sobre com 0.1.1; o registro
   fica em `%LOCALAPPDATA%\Clivus Solar\atualizacao.log`. Daí em diante o PC fica com o
   bundle Release do instalador, até o próximo `instalar.ps1`.
5. Navegador: https://clivus.marcheng.com.br/entrar com a conta Develop › Gestão › Clientes ›
   Novo cliente (e-mail, empresa, plano Cliente, validade, 2 computadores, "Gerar já o
   código"). A senha inicial e o código aparecem uma vez só: copiar a "Mensagem pronta".
6. Com a conta nova (outra janela anônima): a troca de senha no primeiro acesso, o código, o
   download da 0.1.1 com o SHA-256 e a lista de computadores.
7. Num PC do cliente (ou o seu, depois de desinstalar): instalar a 0.1.1, ativar com o código
   e publicar um 3D; o link sai em clivus.marcheng.com.br e o projeto aparece na área do
   cliente da conta nova (não na Develop).

Pendente do Renan: o e-mail de suporte que a área do cliente mostra (`CLIVUS_SUPORTE_EMAIL`).
