# Etapa 0: Fundação

Objetivo: plugin instala, carrega no Civil 3D e responde. Nenhuma regra de negócio.

### 0.1 Solução e projetos vazios
Criar a estrutura de `02-arquitetura.md`. Todos os projetos compilam. Plugin referencia as DLLs do Civil 3D 2026 com `Copy Local = false`.
**Validação do Renan:** confirma a versão do Civil 3D; abre a solução e vê compilar.

### 0.2 Teste de arquitetura e placar
Teste que falha se `UFV.Core` ou `UFV.Geo` referenciar qualquer assembly `Ac*` ou `Aecc*`. Criar `rodar-testes.ps1`, `checar-acervo.ps1` e `MANIFESTO.sha256` vazio.
**Validação do Renan:** roda o placar e vê a etapa 0 verde.

### 0.3 Comando hello world
Comando `UFV_OLA` que escreve na linha de comando "Plugin UFV carregado, versão X". Carregado via NETLOAD.
**Validação do Renan:** NETLOAD, digita UFV_OLA, lê a mensagem.

### 0.4 Carregamento automático
Bundle em `ApplicationPlugins` com `PackageContents.xml`, carrega sozinho ao abrir o Civil 3D. Barra de ferramentas (ribbon) com a aba "UFV" e só o botão Olá.
**Validação do Renan:** fecha e abre o Civil 3D, a aba aparece sem NETLOAD.

### 0.5 Core Console fumaça
Script `.scr` que abre um desenho vazio no Core Console, roda UFV_OLA e grava a saída num arquivo. Primeiro teste de nível 2, entra no placar.
**Validação do Renan:** roda o placar, nível 2 aparece verde.

### 0.6 Instalador
Instalador que detecta a versão do Civil 3D instalada, avisa se não é compatível, e copia o bundle.
**Validação do Renan:** desinstala, instala pelo instalador numa máquina limpa (ou outra conta), abre e funciona.
