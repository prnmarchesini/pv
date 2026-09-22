# Etapa 6: Exportação para o PVsyst

O PVsyst importa cena 3D em 3DS, DAE e PVC (Arquivo > Importar > Importar cena 3D). PVC é PV Collada, derivado do DAE, que carrega também dados de módulos e mesas.

### 6.1 Escritor DAE puro
Em `UFV.Core`: recebe lista de faces superiores de módulo e escreve Collada 1.4 válido.
Testes: XML bem formado, contagem de faces, coordenadas conferidas.

### 6.2 Botão Exportar para PVsyst
Libera seleção, usuário marca a área, plugin recolhe os módulos dentro dela, pergunta o formato, grava o arquivo.

### 6.3 Validação no PVsyst
**Validação do Renan:** importa no PVsyst, indica a layer do módulo, confere contagem e orientação.

### 6.4 Estudo do formato PVC
Antes de codar: levantar a especificação do PVC (documentação do PVsyst e arquivos exportados pelo PVcase como exemplo). Entregar um resumo ao Renan e PARAR.

### 6.5 Escritor PVC
Implementar com base no estudo.
**Validação do Renan:** importa no PVsyst e vê mesas e módulos reconhecidos sem indicar layer.
