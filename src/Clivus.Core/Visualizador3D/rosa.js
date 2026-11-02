// A rosa dos ventos do 3D (02/11/2026, Renan: "preciso que os 3Ds gerados
// tenham a rosa do vento, e conforme vou virando o desenho ela vai girando
// tambem"; "ela aponta para a seta que eu clicar, tipo, leste, ela vira para o
// leste (o leste fica pra cima)"; "e se clicar de novo ela solta").
//
// A rosa gira com a câmera. O clique num rumo vira a vista até ele ficar para
// cima e trava o giro ali: inclinar, arrastar e o zoom continuam. O clique no
// mesmo rumo, ou no cadeado do meio, solta.
//
// O mesmo arquivo serve os dois visualizadores: a página local (o
// Viewer3DPage do plugin o embute no HTML) e a do servidor
// (/estatico/rosa.js no repositório clivussolar, cópia deste). Nenhuma frase
// de usuário aqui: os textos vêm dos atributos data-* do #rosa.
(function (raiz) {
  'use strict';

  const SVG = 'http://www.w3.org/2000/svg';
  const VOLTA = 2 * Math.PI, GRAUS = 180 / Math.PI, DURACAO = 450;

  // O azimute do OrbitControls que põe cada rumo (norte, leste, sul, oeste)
  // para cima na tela. Ele é o giro da câmera em volta do alvo, a partir do +z
  // da three, que é o sul (o desenho tem x a leste e y ao norte; a three fica
  // com x, z e −y): 0 é a câmera ao sul olhando para o norte; −π/2, a oeste
  // olhando para o leste. A rosa gira na tela desse mesmo ângulo.
  const AZIMUTES = [0, -Math.PI / 2, Math.PI, Math.PI / 2];

  /** O ângulo a pelo lado mais curto, entre −π e π. */
  const curto = a => ((a + Math.PI) % VOLTA + VOLTA) % VOLTA - Math.PI;

  /** Começa e termina devagar. */
  const suave = k => (k < 0.5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2);

  const preencher = (modelo, valores) => String(modelo || '').replace(/\{(\w+)\}/g, (m, k) => (k in valores ? valores[k] : m));

  function no(nome, atributos, pai) {
    const e = document.createElementNS(SVG, nome);
    for (const k of Object.keys(atributos)) e.setAttribute(k, atributos[k]);
    if (pai) pai.appendChild(e);
    return e;
  }

  /**
   * Monta a rosa no #rosa e devolve { quadro, escolher, soltar }. O laço da
   * página chama quadro() a cada quadro, antes do controles.update(): é ali
   * que a virada anda e a rosa acompanha a câmera. Sem o #rosa, null.
   */
  function montar(controles) {
    const caixa = document.getElementById('rosa');
    if (!caixa || !controles) return null;
    const t = caixa.dataset;
    const letras = (t.letras || 'N E S W').split(' '), rumos = (t.rumos || 'north|east|south|west').split('|');

    const desenho = no('svg', { viewBox: '-50 -50 100 100', class: 'rosa-desenho', role: 'group', 'aria-label': t.titulo || '' });
    no('circle', { r: 48, class: 'rosa-fundo' }, desenho);
    const giro = no('g', {}, desenho);
    no('circle', { r: 21, class: 'rosa-anel' }, giro);
    for (let k = 0; k < 4; k++) no('path', { d: 'M0,-17L3,-3L0,0L-3,-3Z', class: 'rosa-diagonal', transform: `rotate(${45 + 90 * k})` }, giro);

    // Um botão por rumo: a ponta e a letra, que fica sempre em pé. O nome do
    // botão é o <title> (a ação: travar nesse rumo ou soltar), que é também a
    // dica do mouse. O segundo clique de um duplo clique e o Enter segurado
    // não contam: alternariam a trava e a soltura (revisão do 42.12).
    const botoes = [], letrasNaTela = [];
    for (let i = 0; i < 4; i++) {
      const botao = no('g', { class: 'rosa-rumo' + (i === 0 ? ' rosa-norte' : ''), transform: `rotate(${90 * i})`, role: 'button', tabindex: '0' }, giro);
      const dica = no('title', {}, botao);
      no('circle', { cx: 0, cy: -38, r: 11, class: 'rosa-alvo' }, botao);
      no('path', { d: 'M0,-28L-6,-6L0,0Z', class: 'rosa-ponta' }, botao);
      no('path', { d: 'M0,-28L6,-6L0,0Z', class: 'rosa-ponta rosa-sombra' }, botao);
      const letra = no('text', { x: 0, y: -38, class: 'rosa-letra', 'text-anchor': 'middle', 'dominant-baseline': 'central' }, botao);
      letra.textContent = letras[i] || '';
      botao.addEventListener('click', e => { if (!(e && e.detail > 1)) escolher(i); });
      botao.addEventListener('keydown', e => {
        if (e.repeat || (e.key !== 'Enter' && e.key !== ' ')) return;
        e.preventDefault();
        escolher(i);
      });
      botoes.push({ botao, dica });
      letrasNaTela.push(letra);
    }

    // O meio: com o giro travado, mostra o cadeado e solta no clique.
    const meio = no('g', { class: 'rosa-meio' }, desenho);
    no('circle', { r: 8 }, meio);
    const cadeado = no('g', { class: 'rosa-cadeado' }, meio);
    no('rect', { x: -3.4, y: -0.6, width: 6.8, height: 5, rx: 1 }, cadeado);
    no('path', { d: 'M-2,-0.6v-1.6a2,2 0 0 1 4,0v1.6' }, cadeado);
    meio.addEventListener('click', e => { if (travado >= 0 && !(e && e.detail > 1)) soltar(); });

    const aviso = document.createElement('div');
    aviso.className = 'rosa-aviso';
    aviso.setAttribute('role', 'status');
    caixa.append(desenho, aviso);
    let some = 0;
    function avisar(texto) {
      aviso.textContent = texto;
      aviso.classList.add('visivel');
      clearTimeout(some);
      some = setTimeout(() => aviso.classList.remove('visivel'), 4000);
    }

    let travado = -1, virada = null, mostrado = NaN;

    function marcar() {
      caixa.classList.toggle('travada', travado >= 0);
      botoes.forEach(({ botao, dica }, i) => {
        const ativo = i === travado;
        botao.classList.toggle('ativo', ativo);
        dica.textContent = preencher(ativo ? t.soltar : t.travar, { rumo: rumos[i] || '' });
      });
    }

    function escolher(i) {
      if (i === travado) return soltar();
      const de = controles.getAzimuthalAngle();
      virada = { de, delta: curto(AZIMUTES[i] - de), inicio: performance.now() };
      travado = i;
      marcar();
      avisar(preencher(t.travado, { rumo: rumos[i] || '', letra: letras[i] || '' }));
    }

    function soltar() {
      virada = null;
      // O arraste de lado com a trava fica guardado na inércia do OrbitControls
      // (o clamp só descarta o que já entrou): ao soltar, a vista girava
      // sozinha, até 39° logo depois de um arraste (revisão do 42.12). Um
      // update sem amortecer, ainda travado, gasta esse resto ali.
      const amortecer = controles.enableDamping;
      controles.enableDamping = false;
      controles.update();
      controles.enableDamping = amortecer;
      travado = -1;
      controles.minAzimuthAngle = -Infinity;
      controles.maxAzimuthAngle = Infinity;
      marcar();
      avisar(t.solto || '');
    }

    // A trava é o mínimo e o máximo do azimute no mesmo ângulo: o OrbitControls
    // segura a câmera ali no update. Na virada, esse ângulo anda até o rumo.
    function quadro() {
      if (virada) {
        const k = Math.min((performance.now() - virada.inicio) / DURACAO, 1);
        const a = k < 1 ? virada.de + virada.delta * suave(k) : AZIMUTES[travado];
        controles.minAzimuthAngle = controles.maxAzimuthAngle = a;
        if (k >= 1) virada = null;
      }
      const g = controles.getAzimuthalAngle() * GRAUS;
      if (Math.abs(g - mostrado) < 0.05) return;
      mostrado = g;
      giro.setAttribute('transform', `rotate(${g.toFixed(2)})`);
      letrasNaTela.forEach((letra, i) => letra.setAttribute('transform', `rotate(${(-g - 90 * i).toFixed(2)} 0 -38)`));
    }

    marcar();
    quadro();
    return { quadro, escolher, soltar };
  }

  const api = { montar, curto, AZIMUTES };

  if (typeof module === 'object' && module && module.exports) module.exports = api;
  else raiz.ClivusRosa = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
