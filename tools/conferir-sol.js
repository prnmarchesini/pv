// Confere o sol do 3D (src/Clivus.Core/Visualizador3D/sol.js, o porte do
// SolarCalculator para o navegador, passo 42.2) contra a tabela que o C# gera
// (tests/dados/sol-referencia.json). O teste do Core
// OSolDoCSharpBateComATabelaDoNavegador garante que a tabela é o C# de hoje;
// este, que o JS dá o mesmo. O rodar-testes.ps1 roda quando há node.
//
//     node tools/conferir-sol.js        # sai 1 se algo não bate
const caminho = require('path');
const fs = require('fs');

const raiz = caminho.resolve(__dirname, '..');
const Sol = require(caminho.join(raiz, 'src', 'Clivus.Core', 'Visualizador3D', 'sol.js'));
const tabela = JSON.parse(fs.readFileSync(caminho.join(raiz, 'tests', 'dados', 'sol-referencia.json'), 'utf8'));

// O C# e o JS fazem as mesmas contas em double: a diferença é de arredondamento.
const TOLERANCIA = 1e-7;
const difAngulo = (a, b) => { const d = Math.abs(a - b) % 360; return Math.min(d, 360 - d); };
const falhas = [];
let pior = 0;

for (const [lat, lon, fuso, ano, mes, dia, minutos, az, el, dec, eot] of tabela.instantes) {
  const p = Sol.posicao(lat, lon, ano, mes, dia, minutos, fuso);
  const d = p ? Math.max(difAngulo(p.azimute, az), Math.abs(p.elevacao - el), Math.abs(p.declinacao - dec), Math.abs(p.equacaoDoTempo - eot)) : Infinity;
  pior = Math.max(pior, d);
  if (!(d <= TOLERANCIA)) falhas.push(`${lat}, ${lon}, UTC${fuso}, ${ano}-${mes}-${dia} ${minutos} min: C# [${az}, ${el}] JS ${p ? `[${p.azimute}, ${p.elevacao}]` : 'null'}`);
}

if (tabela.instantes.length < 40) falhas.push(`a tabela tem só ${tabela.instantes.length} instantes`);

if (falhas.length) {
  console.log(`O sol.js não bate com o C# em ${falhas.length} caso(s):`);
  for (const f of falhas.slice(0, 10)) console.log('  ' + f);
  process.exit(1);
}

console.log(`${tabela.instantes.length} instantes, maior diferença ${pior.toExponential(1)}°`);
