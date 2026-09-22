# Arquitetura

## Alvo
Civil 3D **2026** (confirmado em 22/09/2026 na máquina do Renan: `acmgd.dll` 25.1.74.0.0, `AeccDbMgd.dll` build 946_20250729_1140). AutoCAD/Civil 3D 2026 roda em **.NET 8**. Se a versão for outra, pare e avise: pode mudar o framework.

DLLs de referência, todas com `Copy Local = false`:

```
C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll
C:\Program Files\Autodesk\AutoCAD 2026\acdbmgd.dll
C:\Program Files\Autodesk\AutoCAD 2026\acmgd.dll
C:\Program Files\Autodesk\AutoCAD 2026\C3D\AeccDbMgd.dll
```

## Solução

```
UFV.sln
src/
  UFV.Core/        net8.0      Motor de cálculo. SEM referência de CAD.
  UFV.Geo/         net8.0      Terreno, TIN, índice espacial, drapeamento. SEM referência de CAD.
  UFV.Cli/         net8.0      Executável de linha de comando que roda o motor em lote (JSON entra, JSON sai).
  UFV.Plugin/      net8.0-windows  Casca fina. Única que referencia AcCoreMgd, AcDbMgd, AcMgd, AeccDbMgd.
tests/
  UFV.Core.Tests/  xUnit
  UFV.Geo.Tests/   xUnit
  UFV.Integration/ scripts para o Core Console (accoreconsole.exe)
  acervo/          IMUTÁVEL. Entradas, desenhos de referência e resultados esperados congelados.
tools/
  rodar-testes.ps1 Roda tudo e imprime o placar por etapa.
  checar-acervo.ps1 Confere o hash de cada arquivo do acervo.
```

## Dependências permitidas

```
UFV.Plugin -> UFV.Core -> UFV.Geo
UFV.Cli    -> UFV.Core -> UFV.Geo
```

Nada aponta para `UFV.Plugin`. `UFV.Core` e `UFV.Geo` não sabem que o AutoCAD existe. Um teste de arquitetura (passo 0.2) falha se isso for violado.

## Convenções

- Unidade interna: **metro**, `double`. Ângulos internos em **radianos**; graus só na interface.
- Tolerância geométrica padrão: 1e-6 m em comparações, 1 mm em verificadores de regra.
- Mesa construída em coordenadas locais (origem, plano horizontal, azimute zero) e posicionada por **uma matriz** (tilt, azimute, translação). Nada de trigonometria canto a canto.
- Identidade: XData / extension dictionary sob `MARCHENG_UFV` (tipo, GUID, nome, parâmetros) + handle + dicionário nomeado central. **Layer nunca é fonte de verdade.**
- Escrita no desenho: uma transação por operação do usuário, nunca uma por mesa.
- Terreno amostrado uma vez e guardado; o motor não volta à superfície durante a otimização.
