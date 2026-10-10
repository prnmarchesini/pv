# Bibliotecas e cálculos

Tudo em `Clivus.Core`, sem CAD. **O sistema não dimensiona: ele multiplica.** Os inputs são todos do Renan; o sistema aplica as fórmulas e mostra a tabela. Quem avalia é ele.

## Biblioteca de cabos

Uma biblioteca editável, com cabos de CC, CA e MT. O Claude Code cria uma biblioteca inicial preenchida com valores de catálogo comuns no Brasil; o Renan depois revisa e edita tudo.

Campos de cada cabo:

- nome e tipo (CC, CA, MT)
- bitola (mm²)
- tensão de isolamento (ex. 1,8 kV CC; 0,6/1 kV; 8,7/15 kV; 12/20 kV; 20/35 kV)
- resistência ôhmica a 20 °C (ohm/km)
- resistência ôhmica na temperatura de operação (ohm/km)
- reatância indutiva (ohm/km)
- capacidade de condução de corrente por método de instalação (ver abaixo)
- número de condutores / formação, quando aplicável
- material do condutor (cobre, alumínio) e da isolação (EPR, XLPE, PVC)

Na hora de gerar a rota, o Renan escolhe qual cabo da biblioteca vale para aquele trecho (ex. tudo em 6 mm²). Trocar o cabo e regerar a tabela tem que ser barato: ele vai querer testar 6, depois 10, e comparar.

## Biblioteca de módulos com arquivo PAN

Adendo ao cadastro de módulo do plano de layout. A biblioteca de módulos passa a aceitar **arquivo .PAN** (formato do PVsyst). O Renan sobe o PAN e o módulo carrega as características elétricas dele.

Do PAN interessam pelo menos:

- Voc (tensão de circuito aberto, STC)
- Isc (corrente de curto-circuito, STC)
- Vmp e Imp (ponto de máxima potência, STC)
- Pmax
- coeficientes de temperatura: de tensão (mV/°C ou %/°C) e de corrente
- NOCT, quando presente

Se o PAN não for lido ou vier incompleto, avisar qual campo faltou. Não inventar valor.

## Configurações do projeto

Aba de configurações do projeto ganha:

- temperatura ambiente **máxima**
- temperatura ambiente **mínima**

São elas que levam a Voc e a tensão de operação para as condições reais.

## As contas

A string já carrega quantos módulos tem em série. Com o PAN, a bitola, o comprimento do lance e as temperaturas, o sistema calcula e mostra:

**Tensão de circuito aberto na temperatura mínima.** Voc do módulo corrigida pelo coeficiente de temperatura até a temperatura mínima, multiplicada pelo número de módulos em série. É o pior caso de tensão: no frio a Voc **sobe** acima da nominal. É esse número que se compara com a janela do inversor.

**Tensão de operação na temperatura máxima.** Vmp corrigida pelo coeficiente de temperatura até a temperatura máxima, multiplicada pelos módulos em série. É o pior caso de tensão baixa em operação.

**Correntes.** Imp (corrente de MPPT) e Isc, do PAN. No CC, a corrente da string é a do módulo, já que estão em série.

**Variação de tensão no cabo.** Com a resistência do cabo, o comprimento do lance (ida e volta, positivo mais negativo) e a corrente de operação, calcula a **queda de tensão** no percurso e o percentual sobre a tensão de operação. Atenção à nomenclatura, que o Renan costuma confundir e está certa assim: a tensão que **sobe** é a Voc no frio, efeito de temperatura, não de cabo. No cabo em operação sempre há **queda**, nunca aumento.

**CA e MT.** Do inversor vêm tensão de saída, potência e se é trifásico. Com isso sai a corrente. Com a corrente, o comprimento do lance e as características do cabo, saem a queda de tensão e o percentual. E com a tabela de capacidade de condução por **método de instalação da NBR 5410** (que o Renan escolhe), a tabela mostra lado a lado a corrente calculada e a corrente que aquele método admite para aquele cabo. O sistema **mostra os dois números**; não emite aprovação.

## A tabela de resumo

Uma tabela por tipo de rota. No CC, uma linha por string:

- tag da string
- módulos em série
- comprimento do lance vermelho (positivo)
- comprimento do lance preto (negativo)
- comprimento total
- cabo usado (bitola)
- Voc na temperatura mínima
- tensão de operação na temperatura máxima
- corrente de MPPT e corrente de operação
- queda de tensão (V e %)

No CA e no MT, uma linha por trecho, com a corrente, o comprimento, o cabo, a queda de tensão e a capacidade de condução do método escolhido.

Com totalização no rodapé. Isso é, na prática, o memorial de cálculo, e alimenta a aba de resumo da usina.
