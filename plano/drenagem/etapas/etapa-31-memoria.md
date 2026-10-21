# Etapa 31: Memória › cálculo de drenagem

Pedido do Renan: menu de cálculo de água, "olhe o memorial de cálculo, temos
tudo". Fórmulas e números de ouro em `02-calculos-hidraulicos.md`.

### 31.1 Motor hidráulico (Core)

`Rainfall` (IDF, Kirpich, Kerby do memorial, tc adotado, V superficial,
método racional) e `Hydraulics` (Manning nas duas seções, lâmina normal por
bisseção, bordo livre, altura sugerida, dissipador). Todos os números de ouro
das Tabelas 1, 3, 5, 8, 9 e 11 como teste.
Fecha automático.

### 31.2 Parâmetros do projeto

Janela Memória, seção Parâmetros (salvos no desenho): K, a, b, c (com a
localidade, ex. "Plúvio 2.1, Valparaiso-SP"), T, tc mínimo, C (lista DNIT 39),
cobertura do terreno (lista DNIT 31), n e velocidade máxima do revestimento,
velocidade mínima de autolimpeza (1,0 m/s), declividade mínima (0,005).
Padrões = memorial de Valparaiso.
**Validação do Renan:** abre e confere os padrões.

### 31.3 Tabelas na tela

Abas iguais às do memorial: Bacias (Tabela 1), Chuva (Tabela 3, T × t),
Vazão (Tabela 5), Canaletas (Tabela 8: b, z, L, Imin, Imax, n, Q, vmax, hmax, f),
Dimensões (Tabela 9 com y sugerido e y adotado editável), Dissipadores
(Tabela 11). Alertas da regra 4 em vermelho, com o motivo. Sempre recalcula
antes de mostrar (bacias, perfis e seções do desenho atual).
**Validação do Renan:** confere com o memorial no desenho da Valparaiso.

### 31.4 Exportar Excel

Uma planilha por tabela + Parâmetros + Alertas, no padrão do Exportar elétrico
(cabeçalho, unidades, totais, larguras, impressão). Teste: abre o xlsx gerado
e confere as células de ouro.
Fecha automático (arquivo), com conferência visual opcional do Renan.

### 31.5 Seção a partir da conta

Botão "aplicar y sugerido": grava o y adotado na seção da canaleta e refaz o
3D e o perfil. É ação do Renan, nunca automática (regra 5).
**Validação do Renan:** aplica e vê a canaleta crescer.

### 31.6 Dissipador e transposição

Dissipador (DES) no pé de cada canaleta com dimensões do item 7, como bloco 3D
simples. Transposição (TSS) onde a canaleta cruza via (layer de via
configurável), modelo escolhido pela tabela 12 do memorial digitada como lista
editável.
**Pergunta aberta 3:** desenhar os dois agora ou só listar na memória?
**Validação do Renan:** confere dissipador e transposição no desenho.

### Fora de escopo, para depois

- Interseções e junções de canaletas (vazão somada a jusante): o Renan disse
  "depois a gente bola".
- Memorial em Word gerado pelo sistema (o Excel já leva todos os números).
