# Etapa 22: bibliotecas de cabo e de módulo

Detalhe completo em `04-bibliotecas-e-calculos.md`. Aqui, os passos.

### 22.1 Biblioteca de cabos

Biblioteca editável com cabos CC, CA e MT. Campos: nome, tipo, bitola, tensão de isolamento, resistência ôhmica a 20 °C, resistência na temperatura de operação, reatância indutiva, capacidade de condução por método de instalação, material do condutor e da isolação, formação.
O Claude Code **preenche uma biblioteca inicial** com valores de catálogo comuns no Brasil. O Renan revisa e corrige depois. Deixe claro na interface que são valores de partida.
**Validação do Renan:** abre a biblioteca, confere os valores iniciais e edita um cabo.

### 22.2 Escolha do cabo por rota e troca barata

Na aba de cada rota, escolher qual cabo da biblioteca vale para aquele trecho. Trocar o cabo e refazer as contas tem que ser rápido: o Renan vai testar 6 mm², depois 10, e comparar os resultados.
Trocar o cabo **não precisa redesenhar o traçado**, só recalcular a tabela, já que o percurso não mudou.
**Validação do Renan:** troca de 6 para 10 e confere que a tabela recalculou sem refazer o desenho.

### 22.3 Biblioteca de módulos com arquivo PAN

Adendo ao cadastro de módulo do plano de layout. A biblioteca de módulos passa a aceitar upload de **arquivo .PAN** (PVsyst). Parser em `Clivus.Core`, sem CAD.
Campos lidos: Voc, Isc, Vmp, Imp, Pmax, coeficientes de temperatura de tensão e de corrente, NOCT quando presente.
PAN ilegível ou incompleto: avisa **qual campo faltou**. Nunca inventar valor nem assumir padrão silenciosamente.
**Validação do Renan:** sobe um PAN real e confere os campos lidos contra o PVsyst.

### 22.4 Temperaturas nas configurações do projeto

Aba de configurações do projeto ganha temperatura ambiente **máxima** e **mínima**. São elas que levam Voc e tensão de operação para as condições reais.
**Validação do Renan:** põe as temperaturas e confere que entram nos cálculos da etapa 23.

### 22.5 Método de instalação da NBR 5410

Cadastro do método de instalação, com a tabela de capacidade de condução de corrente correspondente. O Renan escolhe o método por rota.
**Validação do Renan:** escolhe um método e confere a corrente admissível que o sistema mostrou para o cabo escolhido.
