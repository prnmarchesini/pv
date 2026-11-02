// Confere a rosa dos ventos do 3D (src/Clivus.Core/Visualizador3D/rosa.js,
// passo 42.12) com a three r128 e o OrbitControls de verdade, num DOM mínimo:
// a seta N da rosa aponta para o norte da tela; cada rumo clicado fica para
// cima, de qualquer começo (inclusive ±π); travado, o arraste de lado não gira
// e o de cima inclina; ao soltar, a vista não gira sozinha (a inércia do
// arraste travado); o duplo clique e o Enter segurado não alternam a trava; o
// meio solta. Veio da simulação da revisão do 42.12. O rodar-testes.ps1 roda
// quando há node.
//
//     node tools/conferir-rosa.js        # sai 1 se algo falhar
'use strict';
const caminho = require('path');
const fs = require('fs');
const vm = require('vm');

const pasta = caminho.resolve(__dirname, '..', 'src', 'Clivus.Core', 'Visualizador3D');
const THREE = require(caminho.join(pasta, 'three.min.js'));
globalThis.THREE = THREE;

// ---- um DOM mínimo, o que o OrbitControls e o rosa.js usam -------------------
class Classes {
  constructor() { this.s = new Set(); }
  add(c) { this.s.add(c); }
  remove(c) { this.s.delete(c); }
  contains(c) { return this.s.has(c); }
  toggle(c, f) { const sim = f === undefined ? !this.s.has(c) : !!f; if (sim) this.s.add(c); else this.s.delete(c); return sim; }
}
class No {
  constructor(tag) { this.tag = tag; this.attrs = {}; this.children = []; this.ouvintes = {}; this.classList = new Classes(); this.dataset = {}; this.textContent = ''; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  getAttribute(k) { return this.attrs[k]; }
  appendChild(c) { this.children.push(c); return c; }
  append(...cs) { cs.forEach(c => this.appendChild(c)); }
  addEventListener(t, f) { (this.ouvintes[t] = this.ouvintes[t] || []).push(f); }
  removeEventListener(t, f) { const l = this.ouvintes[t] || []; const i = l.indexOf(f); if (i >= 0) l.splice(i, 1); }
  disparar(t, e) { for (const f of (this.ouvintes[t] || []).slice()) f(e); }
  set className(v) { v.split(/\s+/).filter(Boolean).forEach(c => this.classList.add(c)); }
}
const doc = new No('document');
const caixa = new No('div');
caixa.dataset = {
  titulo: 'Rosa dos ventos', letras: 'N L S O', rumos: 'norte|leste|sul|oeste',
  travar: 'Pôr o {rumo} para cima e travar o giro', soltar: 'Soltar o giro',
  travado: 'Giro travado com o {rumo} para cima. Para soltar, clique de novo no {letra}.', solto: 'Giro solto.',
};
globalThis.document = {
  getElementById: id => (id === 'rosa' ? caixa : null),
  createElementNS: (ns, tag) => new No(tag),
  createElement: tag => new No(tag),
  addEventListener: (t, f) => doc.addEventListener(t, f),
  removeEventListener: (t, f) => doc.removeEventListener(t, f),
};
globalThis.window = globalThis;
let agora = 1000;
Object.defineProperty(globalThis, 'performance', { value: { now: () => agora }, configurable: true, writable: true });
globalThis.setTimeout = () => 0;
globalThis.clearTimeout = () => {};

const tela = new No('canvas');
tela.clientHeight = 800; tela.clientWidth = 1200;
tela.ownerDocument = globalThis.document;
tela.focus = () => {};

vm.runInThisContext(fs.readFileSync(caminho.join(pasta, 'OrbitControls.js'), 'utf8'), { filename: 'OrbitControls.js' });
const Rosa = require(caminho.join(pasta, 'rosa.js'));

// ---- a cena -------------------------------------------------------------------
const camera = new THREE.PerspectiveCamera(45, 1.5, 0.5, 50000);
const controles = new THREE.OrbitControls(camera, tela);
controles.enableDamping = true;
const rosa = Rosa.montar(controles);

const desenho = caixa.children[0];                       // svg: fundo, giro, meio
const giro = desenho.children[1], meio = desenho.children[2];
const botoes = giro.children.filter(c => c.tag === 'g');
const anguloDaRosa = () => Number(/rotate\(([-\d.]+)\)/.exec(giro.attrs.transform)[1]);
const tituloDe = b => b.children.find(c => c.tag === 'title').textContent;

const quadro = () => { agora += 1000 / 60; rosa.quadro(); controles.update(); };
const quadros = n => { for (let i = 0; i < n; i++) quadro(); };

// A câmera no azimute theta (o da three), a 100 m do alvo, inclinada phi.
function por(theta, phi = 1.0) {
  controles.target.set(0, 0, 0);
  camera.position.set(100 * Math.sin(phi) * Math.sin(theta), 100 * Math.cos(phi), 100 * Math.sin(phi) * Math.cos(theta));
  controles.update();
}

// Os rumos do desenho (x leste, y norte) na three, (x, 0, −y), e o ângulo de
// cada um na tela: 0 para cima, positivo à direita (horário), pelo chão.
const RUMOS = [[0, 0, -1], [1, 0, 0], [0, 0, 1], [-1, 0, 0]];
const NOMES = ['N', 'L', 'S', 'O'];
function naTela(v) {
  camera.updateMatrixWorld(true);
  const e = camera.matrixWorld.elements;
  const direita = new THREE.Vector3(e[0], e[1], e[2]);
  const frente = new THREE.Vector3(-e[8], 0, -e[10]).normalize();
  const d = new THREE.Vector3(...v);
  return Math.atan2(d.dot(direita), d.dot(frente)) * 180 / Math.PI;
}
const graus = g => ((g % 360) + 540) % 360 - 180;

function arrastar(dx, dy, passos) {
  tela.disparar('pointerdown', { pointerType: 'mouse', button: 0, clientX: 500, clientY: 400, preventDefault() {} });
  for (let p = 1; p <= passos; p++) {
    doc.disparar('pointermove', { pointerType: 'mouse', clientX: 500 + dx * p / passos, clientY: 400 + dy * p / passos, preventDefault() {} });
    quadro();
  }
  doc.disparar('pointerup', { pointerType: 'mouse', clientX: 500 + dx, clientY: 400 + dy, preventDefault() {} });
}
const soltarSeTravado = () => { if (caixa.classList.contains('travada')) rosa.soltar(); };

const falhas = [];
const confere = (ok, msg) => { if (!ok) falhas.push(msg); };

// 1) A seta N da rosa aponta para onde o norte está na tela, em qualquer vista.
for (let k = 0; k < 200; k++) {
  const theta = (Math.random() * 2 - 1) * Math.PI, phi = 0.05 + Math.random() * 1.4;
  por(theta, phi); quadros(1);
  const seta = graus(anguloDaRosa()), norte = graus(naTela(RUMOS[0]));
  confere(Math.abs(graus(seta - norte)) < 0.2, `a seta N a ${seta.toFixed(2)}° e o norte na tela a ${norte.toFixed(2)}° (theta ${theta.toFixed(3)})`);
}

// 2) Cada rumo clicado fica para cima, de 13 começos (inclusive ±π), sem salto.
for (const comeco of [0, 0.3, -0.3, Math.PI / 2, -Math.PI / 2, 3.0, -3.0, Math.PI, -Math.PI, Math.PI - 1e-9, -Math.PI + 1e-9, 2.2, -2.2]) {
  for (let i = 0; i < 4; i++) {
    soltarSeTravado(); quadros(200);
    por(comeco); quadros(2);
    const inicio = controles.getAzimuthalAngle();
    let antes = inicio, salto = 0, andou = 0;
    botoes[i].disparar('click', { detail: 1 });
    for (let f = 0; f < 40; f++) {
      quadro();
      const a = controles.getAzimuthalAngle(), passo = Math.abs(Rosa.curto(a - antes));
      salto = Math.max(salto, passo); andou += passo; antes = a;
    }
    const onde = `do começo ${comeco.toFixed(3)} para o ${NOMES[i]}`;
    // A virada vai pelo lado mais curto, sem passar do rumo.
    confere(andou <= Math.abs(Rosa.curto(Rosa.AZIMUTES[i] - inicio)) + 1e-6, `${onde}: a vista andou ${andou.toFixed(3)} rad, mais que o caminho curto`);
    confere(Math.abs(Rosa.curto(controles.getAzimuthalAngle() - Rosa.AZIMUTES[i])) < 1e-9, `${onde}: o azimute ficou em ${controles.getAzimuthalAngle()}`);
    confere(Math.abs(graus(naTela(RUMOS[i]))) < 1e-6, `${onde}: o rumo ficou a ${graus(naTela(RUMOS[i])).toFixed(4)}° do alto da tela`);
    confere(Math.abs(graus(anguloDaRosa() + 90 * i)) < 0.06, `${onde}: a seta da rosa ficou a ${graus(anguloDaRosa() + 90 * i)}°`);
    // Meia volta em 0,45 s chega a uns 0,35 rad num quadro, no meio; a volta errada em ±π seria uns 6.
    confere(salto < 0.5, `${onde}: salto de ${salto.toFixed(3)} rad num quadro`);
    confere(tituloDe(botoes[i]) === 'Soltar o giro' && tituloDe(botoes[(i + 1) % 4]).startsWith('Pôr o '), `${onde}: os nomes dos botões não acompanham a trava`);
  }
}

// 3) Travado: o arraste de lado não gira, o de cima inclina.
soltarSeTravado(); quadros(200); por(0.4); quadros(2);
botoes[1].disparar('click', { detail: 1 }); quadros(40);
const inclinacao = controles.getPolarAngle();
arrastar(300, 0, 15);
confere(Math.abs(Rosa.curto(controles.getAzimuthalAngle() + Math.PI / 2)) < 1e-9, 'travado no L, o arraste de lado girou a vista');
arrastar(0, 80, 10); quadros(60);
confere(Math.abs(controles.getPolarAngle() - inclinacao) > 0.05, 'travado, o arraste para cima não inclinou');

// 4) Ao soltar logo depois de um arraste travado, a vista não gira sozinha.
for (const espera of [10, 20, 30, 60, 120]) {
  soltarSeTravado(); quadros(300); por(0.4); quadros(2);
  botoes[1].disparar('click', { detail: 1 }); quadros(40);
  arrastar(300, 0, 15); quadros(espera);
  const antes = controles.getAzimuthalAngle();
  rosa.soltar(); quadros(300);
  const sozinha = Rosa.curto(controles.getAzimuthalAngle() - antes) * 180 / Math.PI;
  confere(Math.abs(sozinha) < 0.05, `solto ${(espera / 60).toFixed(2)} s depois do arraste travado, a vista girou sozinha ${sozinha.toFixed(2)}°`);
}

// 5) O duplo clique trava e fica travado (o segundo clique não solta).
soltarSeTravado(); quadros(300); por(0.4); quadros(2);
botoes[2].disparar('click', { detail: 1 }); quadros(6); botoes[2].disparar('click', { detail: 2 }); quadros(60);
confere(caixa.classList.contains('travada') && Math.abs(Rosa.curto(controles.getAzimuthalAngle() - Math.PI)) < 1e-9, 'o duplo clique no S não deixou a vista travada no sul');

// 6) O Enter segurado não alterna; o Enter de novo solta.
soltarSeTravado(); quadros(300);
const tecla = (b, repeat) => b.disparar('keydown', { key: 'Enter', repeat, preventDefault() {} });
tecla(botoes[3], false); quadros(5); tecla(botoes[3], true); quadros(60);
confere(caixa.classList.contains('travada') && Math.abs(Rosa.curto(controles.getAzimuthalAngle() - Math.PI / 2)) < 1e-9, 'o Enter segurado no O alternou a trava');
tecla(botoes[3], false);
confere(!caixa.classList.contains('travada') && controles.minAzimuthAngle === -Infinity, 'o Enter de novo no O não soltou');

// 7) O meio: sem trava, nada; travado, solta.
quadros(10); meio.disparar('click', { detail: 1 });
confere(controles.minAzimuthAngle === -Infinity && controles.maxAzimuthAngle === Infinity, 'o clique no meio sem trava mexeu na câmera');
botoes[0].disparar('click', { detail: 1 }); quadros(40); meio.disparar('click', { detail: 1 });
confere(controles.minAzimuthAngle === -Infinity && !caixa.classList.contains('travada'), 'o clique no meio travado não soltou');

// 8) O nome do botão é o <title>, sem aria-label repetindo nem aria-pressed.
confere(botoes.every(b => b.attrs['aria-label'] === undefined && b.attrs['aria-pressed'] === undefined && b.attrs.role === 'button'),
  'os botões da rosa voltaram a ter aria-label ou aria-pressed');

if (falhas.length) {
  console.log(`rosa.js: ${falhas.length} falha(s)`);
  for (const f of falhas.slice(0, 20)) console.log('  ' + f);
  process.exit(1);
}
console.log('rosa.js: a seta em 200 vistas, 52 viradas, a trava, a soltura sem giro, o duplo clique, o Enter e o meio conferem');
