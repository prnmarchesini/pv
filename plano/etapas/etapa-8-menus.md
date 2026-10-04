# Etapa 8: Remodelagem dos menus (Melhorias.docx, 01/10/2026)

Origem: `0 - Assets/Melhorias.docx`, escrito pelo Renan em 01/10/2026 ("vamos
remodelar os menus, tá zuado"). Cada passo abaixo cita o trecho do Word de
onde saiu. Ordem: primeiro o que é modelo (Core, sem tela), depois as
janelas, por último a ribbon nova, que só junta o que já existe.

Regras que valem para a etapa inteira (do Word):
- **Todo botão e todo campo tem texto explicativo ao passar o mouse.** Teste
  automático confere que nenhum botão da ribbon fica sem dica.
- **Toda análise tem botão de apagar** o que ela pôs (textos, cores). Análises
  são independentes: rodar uma não mexe no que outra pôs.
- **Gerar a usina não analisa.** Sai mesa, pilar e módulo, sem cor de análise,
  sem cota, sem seta. Análise é só pelo menu Análises.

## 8.1 Vãos personalizados e enterro mínimo na estrutura (Core)
Word, Menu 1, itens a e b. A estrutura ganha:
- vãos entre pilares por par (P1-P2, P2-P3, ...), opcionais: sem lista, vale o
  vão-alvo de hoje (`PillarTable.Distribute`); com lista, a tabela é a lista;
- a conta do que os pilares precisam cobrir: módulos × largura + espaçamentos
  − passantes das duas pontas; a soma dos vãos é comparada com ela e o
  resultado diz quanto falta ou sobra;
- enterro mínimo do pilar (T3) no perfil da mesa.
Validação: automática (só modelo).

## 8.2 Janela da estrutura (tela)
Word, Menu 1. "Profundidade" do pilar vira "largura" (as duas medidas da
seção passam a ser larguras, com o sentido escrito); campo de enterro mínimo
(T3) e o T3 desenhado no croqui junto do símbolo do solo; botão "Vãos..." que
abre janela com P1-P2, P2-P3..., soma, e se bate com o que os pilares precisam
cobrir.

## 8.3 Serviço local de módulos
Word, Menu 1, item c. Pasta `servidor/` no padrão do meuPlano (FastAPI,
SQLAlchemy, Alembic, Postgres; SQLite quando não há `DATABASE_URL`), pronto
para subir no Coolify. Rotas de módulos (listar, cadastrar, editar). O plugin
lê os módulos do serviço e, sem serviço no ar, da biblioteca embutida de hoje.
Chave/licença fica fora (decisão de 30/09: só quando o Renan pedir).

## 8.4 Cadastro de módulo (tela)
Botão "Cadastrar módulo" na janela da estrutura, gravando no serviço local.

## 8.5 Mesas do desenho (Core + XData)
Word, Menu 2. As mesas (perfis) passam a ser gravadas NO DESENHO, cada uma com
uma cor e a marca "usar nesta usina". Os perfis de `%LOCALAPPDATA%` continuam
como biblioteca para trazer para o desenho.

## 8.6 Motor com mais de um tipo de mesa (Core)
Word, Menu 2: "posso ter uma mesa de 28 módulos e uma de 14, aí o sistema vê o
que vai encaixar melhor". O preenchimento da fileira escolhe, entre as mesas
marcadas, a combinação que põe mais módulos no trecho, respeitando as regras
sagradas 1 a 6. Validação: automática (Core) + tela no 8.9.

## 8.7 Janela de Configurações com abas (tela)
Word, início e Menus 1 a 3. Um botão abre um modal com abas (como o Chrome):
Estruturas (8.2), Escolha das estruturas (8.5, lista com cor e marca),
Parâmetros (azimute, pitch, degrau mín/máx, espaçamento entre mesas,
espaçamento que quebra a fileira, altura livre mín/máx, limitar declividade e
o valor), Projeto (8.12). Substitui a janela de Mesa, a de Configuração e a de
Parâmetros das análises.

## 8.8 Gerar sem análise
Word, Menu 3, a. `LayoutDrawer.Draw` deixa de pintar, cotar e pôr seta.
Nível 2: depois de CLIVUS_USINA, nenhuma entidade nas camadas de análise, de
alturas e de seta, e nenhuma cor diferente da da camada.

## 8.9 Análise: altura das pontas
Word, Análises, 1. Inserir: textos de todas as pontas baixas, de todas as
altas; apagar. Analisar: ponta baixa abaixo de X pinta de uma cor, acima de X
de outra (o mesmo para a ponta alta); opção de pintar também os módulos ou só
os textos; apagar textos; tirar as cores.

## 8.10 Análise: declividade
Word, Análises, 2. Inserir todas as declividades; abaixo de X uma cor, acima
de X outra; apagar textos; tirar cores.

## 8.11 Análise: pilares
Word, Análises, 2 (pilares). Inserir o comprimento acima do terreno de todos os
pilares; maior que X uma cor, menor que X outra; apagar.

## 8.12 Quantificar e exportar para Excel
Word, Análises. Em cada análise, botão que conta o que caiu em cada faixa. Um
botão exporta tudo para .xlsx: as contagens das análises e o quantitativo
(módulos, mesas, pilares com comprimento total = enterrado + acima).

## 8.13 Estilos do projeto
Word, Tags. Configuração do projeto escolhe o text style, o dimension style e
o multileader style (anotativos) que o plugin usa; todo texto do plugin passa
a nascer com esse estilo e anotativo. Padrão: os estilos anotativos próprios
do desenho (no Itatiba, os de texto, cota e chamada do Renan).

## 8.14 Tags
Word, Tags. Numeração visível de fileiras, mesas, módulos e strings. Cada uma
com inserir e apagar.

## 8.15 Terreno
Word, Terreno. Aba própria: escolher e trocar o terreno, mostrar qual está
escolhido, resumo com área, cidade, país e fuso UTM SIRGAS 2000 (Brasil e
América do Sul).

## 8.16 Ribbon nova
Word, final. Abas/painéis: Configurações, Terreno, Processar, Análises, Tags,
Edição (compacta), PVsyst. Some: Olá, Mesa, Configuração, Parâmetros (viram
o modal do 8.7). Regerar fica (o Word não fala dele). Teste: todo botão com
dica não vazia.
