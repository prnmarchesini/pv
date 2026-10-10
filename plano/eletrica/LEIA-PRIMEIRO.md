# Clivus Solar: parte elétrica. Instruções para o Claude Code

> **Adaptado em 04/10/2026** do plano que o Renan deixou em `0 - Assets/Nova pasta`
> (o original fica lá, intocado): o software é o **Clivus Solar** (o plano dizia
> "Livros"); projetos `Clivus.*` e prefixo `CLIVUS` no lugar de `UFV.*` e do
> dicionário antigo; as etapas elétricas viraram **11 a 16** (no original, 8 a
> 13), porque as etapas 8, 9 e 10 do projeto já existem (menus; sombras e 3D;
> idiomas). Toda frase de tela nova segue a etapa 10: em `Tr.T`/`Tr.F`, com
> tradução em inglês e espanhol.


Você está construindo o módulo ELÉTRICO do plugin de AutoCAD Civil 3D que já faz o layout de usinas fotovoltaicas (o módulo de layout está pronto e funcionando). O software se chama **Clivus Solar**.

O plano de requisitos do layout está em:
https://claude.ai/code/artifact/3668dee4-233d-4a4c-b596-262f1f089c09

Este diretório é o plano de EXECUÇÃO da parte elétrica. Vale tudo que já valia no módulo de layout: as regras sagradas, a arquitetura em camadas, o protocolo por passo e o acervo de testes imutável. Releia os arquivos do plano de layout se precisar; aqui só repetimos o que muda ou se soma.

Leia nesta ordem antes de qualquer código:

1. `01-regras-eletricas.md` (invioláveis da parte elétrica, somam-se às regras sagradas do layout)
2. `02-arquitetura-eletrica.md` (onde a elétrica encaixa nas camadas existentes)
3. `03-protocolo-de-passo.md` (o mesmo ritual de sempre, repetido aqui por conveniência)
4. `04-modelo-de-dados.md` (as entidades elétricas e a cadeia de vínculo)
5. `PROGRESSO.md` (onde você está)
6. O arquivo da etapa atual em `etapas/`

## Regra de ouro

**Um passo por vez. Ao terminar um passo, PARE e espere o Renan validar.** Não comece o seguinte, não adiante código, não saia do escopo. Achou algo fora do escopo, anota em `PROGRESSO.md` na seção Observações e segue.

## A cadeia elétrica (o conceito central do módulo)

Tudo neste módulo existe para montar e representar uma cadeia de vínculo elétrico, de ponta a ponta:

**unidade consumidora (subestação) -> transformador -> inversor -> string -> módulos**

O vínculo elétrico é a verdade. A posição física em campo (os retângulos no desenho) é uma representação por cima do vínculo. Os dois existem juntos, mas quem manda é o vínculo. Nunca derive vínculo a partir de proximidade física no desenho.

## Ordem das abas no software

A parte de corrente contínua (strings) é um universo. A configuração elétrica é outro, com abas, nesta ordem:

1. Subestação
2. Transformador (trafo)
3. Inversor
4. Numeração das strings

São três universos distintos de ponta a ponta: corrente contínua, baixa tensão e média tensão, cada um com rota própria. Este plano cobre a corrente contínua (strings) e a configuração elétrica até a numeração. Roteamento de cabo e combiner box estão em `plano/roteamento/` (etapas 17 a 24).

## Idioma

Código, nomes de classe e método em inglês. Comentários, mensagens ao usuário, commits e relatórios em português do Brasil.

## Nunca

- Editar arquivos dentro de `tests/acervo/`.
- Afrouxar ou apagar teste para passar.
- Referenciar DLL de AutoCAD/Civil 3D em `Clivus.Core` ou `Clivus.Geo`.
- Renomear, renumerar, realocar ou apagar entidade do usuário sem comando explícito dele.
- Apagar string do desenho ao desalocar de um inversor. Desalocar solta o vínculo, nunca apaga a geometria.
- Marcar passo como `VALIDADO`. Só o Renan faz isso.
