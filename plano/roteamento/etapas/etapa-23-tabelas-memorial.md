# Etapa 23: tabelas e memorial de cálculo

**O sistema não dimensiona, ele multiplica.** Todos os inputs vêm do Renan; o sistema aplica as fórmulas e mostra os números lado a lado. Nenhuma aprovação, nenhum veredito, nenhuma escolha de bitola.

Fórmulas e detalhes em `04-bibliotecas-e-calculos.md`.

### 23.1 Tensões do CC

Para cada string, com os módulos em série que ela já carrega, o PAN e as temperaturas do projeto:

- **Voc na temperatura mínima**: Voc do módulo corrigida pelo coeficiente de temperatura até a temperatura mínima, vezes os módulos em série. No frio a tensão **sobe** acima da nominal; é o pior caso e o número que se compara com a janela do inversor.
- **Tensão de operação na temperatura máxima**: Vmp corrigida até a temperatura máxima, vezes os módulos em série.
**Validação do Renan:** confere os dois números contra um cálculo manual de uma string.

### 23.2 Correntes do CC

Imp (corrente de MPPT) e Isc, do PAN. Em série, a corrente da string é a do módulo.
**Validação do Renan:** confere contra o datasheet.

### 23.3 Queda de tensão no CC

Com a resistência do cabo, o comprimento do lance (positivo mais negativo, ida e volta) e a corrente de operação: queda de tensão em volts e em percentual sobre a tensão de operação.
Nomenclatura correta, que costuma confundir: o que **sobe** é a Voc no frio, efeito de temperatura. No cabo em operação sempre há **queda**, nunca aumento.
**Validação do Renan:** confere a queda de uma string contra cálculo manual.

### 23.4 Tabela do CC

Uma linha por string: tag, módulos em série, comprimento do lance vermelho, comprimento do lance preto, total, cabo usado, Voc na mínima, tensão de operação na máxima, corrente de MPPT, corrente de operação, queda em V e em %. Totalização no rodapé.
**Validação do Renan:** abre a tabela numa usina montada e confere amostras linha a linha.

### 23.5 Cálculos de CA e MT

Do inversor vêm tensão de saída, potência e se é trifásico; daí sai a corrente. Com a corrente, o comprimento e o cabo, saem queda de tensão em V e em %. Com o método de instalação escolhido, a tabela mostra **lado a lado** a corrente calculada e a corrente admissível daquele método para aquele cabo. Só mostra; não aprova.
**Validação do Renan:** confere um trecho CA contra cálculo manual e confere que o sistema não emitiu nenhum veredito.

### 23.6 Tabelas de CA e MT

Uma linha por trecho: origem, destino, comprimento, cabo, corrente calculada, corrente admissível do método, queda em V e em %. Totalização no rodapé.
**Validação do Renan:** confere as duas tabelas.

### 23.7 Recalcular ao trocar bitola

Trocar o cabo e ver a tabela inteira recalcular, sem mexer no traçado. É o fluxo de trabalho principal do memorial: testar 6, testar 10, comparar.
**Validação do Renan:** troca a bitola e confere que todas as colunas calculadas mudaram e os comprimentos não.

### 23.8 Exportar

Exportar cada tabela (CSV no mínimo) para o memorial de cálculo e para a lista de material.
**Validação do Renan:** exporta e abre no Excel.
