# Clivus Solar: roteamento de cabo. Instruções para o Claude Code

> **Adaptado em 10/10/2026** do plano que o Renan fez num planejador com as
> informações antigas (artifact `Rv3v3xvinbMku1QefhBENN`): o software é o
> **Clivus Solar** (o plano dizia "Livros"); projetos `Clivus.*` e dicionário
> `CLIVUS` no lugar de `UFV.*` e `MARCHENG_UFV`; o plano elétrico é o de
> `plano/eletrica/` (etapas **11 a 16**; no original, 8 a 13), e as etapas do
> roteamento viraram **17 a 24** (no original, 14 a 21). Toda frase de tela nova
> segue a etapa 10: em `Tr.T`/`Tr.F`, com tradução em inglês e espanhol.

Você está construindo o módulo de ROTEAMENTO DE CABO do plugin de AutoCAD Civil 3D (software **Clivus Solar**). O módulo de layout está pronto e funcionando. O módulo de configuração elétrica (strings, subestação, trafo, inversor, numeração, resumo) está em `plano/eletrica/`, etapas 11 a 16.

Plano de requisitos do layout:
https://claude.ai/code/artifact/3668dee4-233d-4a4c-b596-262f1f089c09

Este diretório é o plano de EXECUÇÃO do roteamento. Vale tudo que já valia: as regras sagradas do layout, as regras elétricas, a arquitetura em camadas, o protocolo por passo e o acervo de testes imutável. Aqui só está o que muda ou se soma.

Leia nesta ordem antes de qualquer código:

1. `01-regras-roteamento.md`
2. `02-arquitetura-roteamento.md`
3. `03-protocolo-de-passo.md`
4. `04-bibliotecas-e-calculos.md`
5. `PROGRESSO.md`
6. O arquivo da etapa atual em `etapas/`

## Regra de ouro

**Um passo por vez. Ao terminar um passo, PARE e espere o Renan validar.** Não comece o seguinte, não adiante código, não saia do escopo. Achou algo fora do escopo, anota em `PROGRESSO.md` na seção Observações e segue.

## O que é o roteamento

Desenhar em 3D o cabo de cada trecho da cadeia elétrica, seguindo valas que o Renan desenha à mão, contar o comprimento de cada lance e montar tabelas que viram memorial de cálculo.

A cadeia de trechos:

**string -> combiner -> inversor -> trafo -> subestação**

Quando não há combiner box, o trecho é string -> inversor direto.

Cada trecho só é possível quando os dois lados já existem no desenho:

- rota CC: precisa de string e inversor (ou string e combiner, e combiner e inversor)
- rota CA: precisa de inversor e trafo
- rota MT: precisa de trafo e subestação

Cada rota tem **layer própria e cor própria** para identificar o cabo.

## Botão e abas

Botão **Rota de cabos** no menu. Abre com abas: **CC**, **Combiner**, **CA**, **MT**.
Todas as abas têm exatamente a mesma mecânica: profundidade da vala, selecionar a vala, escolher o cabo, gerar, ver cabos (tabela), apagar. Cada aba com os seus próprios valores, independentes das outras.

## O que este módulo NÃO faz

**Não dimensiona cabo automaticamente.** O sistema não escolhe bitola, não decide se o cabo aguenta e não aprova nada. O Renan fornece todos os inputs (cabo e suas especificações, arquivo PAN do módulo, método de instalação, temperaturas) e o sistema só faz as contas com o que recebeu e mostra a tabela. Quem avalia o resultado é o Renan.

## Idioma

Código, nomes de classe e método em inglês. Comentários, mensagens ao usuário, commits e relatórios em português do Brasil.

## Nunca

- Alterar um arquivo que já está em `tests/acervo/` (acrescentar arquivo novo e declarar o hash no manifesto pode, ver `plano/04-testes.md`).
- Afrouxar ou apagar teste para passar.
- Referenciar DLL de AutoCAD/Civil 3D em `Clivus.Core` ou `Clivus.Geo`.
- Traçar cabo cortando o meio da mesa.
- Falhar calado quando não achar vala. Sempre avisar e pintar o que não conseguiu traçar.
- Marcar como `VALIDADO` um passo que tem comando na tela. Só o Renan faz isso. Passo que é só modelo, arquivo ou número (nada para ver no Civil 3D) fecha com os testes automáticos como `VALIDADO (automático)`, registrando no `PROGRESSO.md` o que assumiu (decisão do Renan em 25/09/2026).
