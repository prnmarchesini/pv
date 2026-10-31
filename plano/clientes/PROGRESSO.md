# Progresso do primeiro cliente (etapa 43)

Status possíveis: PENDENTE, EM ANDAMENTO, AGUARDANDO VALIDAÇÃO, VALIDADO, REPROVADO.
Só o Renan marca VALIDADO (exceto `VALIDADO (automático)`).

- 43.1 Gestão de clientes e códigos de ativação pelo administrador (`/gestao`): EM ANDAMENTO (web, branch `clientes` do clivussolar)
- 43.2 A área do cliente completa (conta, senha, código e máquinas, baixar com SHA-256, projetos 3D, suporte): EM ANDAMENTO (web)
- 43.3 O plugin do cliente se identifica pela licença no 3D e nos módulos: EM ANDAMENTO (servidor); AGUARDANDO VALIDAÇÃO (plugin, branch `cliente-plugin`)
- 43.4 Atualizações: a versão publicada no servidor; o plugin avisa, baixa, confere e instala quando o Civil 3D fecha: EM ANDAMENTO (servidor); AGUARDANDO VALIDAÇÃO (plugin e instalador, branch `cliente-plugin`)
- 43.5 O executável de teste na KingHost (0.1.1) e a atualização de ponta a ponta: PENDENTE

## Observações

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
  com a barra; só de HTTPS no host do servidor ou do site de download
  (`PluginInfo.SiteDeDownload`), cada redirecionamento pela mesma regra (até
  3); confere o tamanho e o SHA-256 (diferente: apaga e avisa); roda o
  instalador com `/aguardar-civil3d` e diz "Feche o Civil 3D: a atualização
  instala sozinha quando ele fechar". Só troca o plugin que o instalador pôs
  (`%APPDATA%\Autodesk\ApplicationPlugins\ClivusSolar.bundle`); carregado de
  outro lugar (Arquivos de Programas, a pasta do build), a janela explica e
  não baixa.
- **O instalador com `/aguardar-civil3d`.** Sem janela; espera `acad`,
  `accoreconsole` e `Clivus.Link` fecharem (olha a cada 5 s, instala com tudo
  fechado há 10 s, desiste em 12 h), não instala com o Windows desligando
  nem por cima de versão igual ou mais nova, tenta de novo até 3 vezes se a
  cópia falhar, e avisa numa janela simples do Windows. Um só por sessão
  (mutex). O registro vai para `%LOCALAPPDATA%\Clivus Solar\atualizacao.log`,
  a última linha como sempre (`{código} {frase}`). Saídas novas: 7 (outro já
  espera), 8 (passou do limite), 9 (Windows desligando).
- **A versão numa fonte só.** O `<Version>` do `Directory.Build.props`. O
  `PackageContents.xml` do repositório traz `$(Version)`, e o build grava a
  cópia com o número na saída (alvo `CarimbarPackageContents`), que o
  `publicar-bundle.ps1` põe no bundle (ele confere o número). Subir para
  0.1.1: trocar o `<Version>` e rodar `tools\gerar-instalador.ps1`.
- **Traduções:** 53 frases novas sem inglês e espanhol; o
  `TranslationCatalogTests` falha até o Renan traduzir.

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
