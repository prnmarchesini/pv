// O sol do 3D do Clivus Solar (passo 42.2, Renan em 31/10/2026: "quero que vc
// tambem leve a simulacao de sombra para o 3D no navegador"). O navegador
// calcula, em tempo real: a usina não manda nada pesado e não há o que esperar.
//
// - A posição do sol: porte fiel do SolarCalculator do plugin
//   (src/Clivus.Core/SolarPosition.cs, o algoritmo do NOAA), com as mesmas
//   constantes e as contas na mesma ordem.
// - A luz: a DirectionalLight da página vai para onde o sol está, com sombra
//   (shadow map) ajustada à usina, e os controles de data, hora, animação e
//   atalhos dos equinócios e solstícios.
//
// O mesmo arquivo serve os dois visualizadores: a página local (o
// Viewer3DPage do plugin o embute no HTML) e a do servidor (/estatico/sol.js
// no repositório clivussolar, cópia deste). Nenhuma frase de usuário aqui: os
// textos vêm da página, nos atributos data-* do #solPainel, no idioma dela.
(function (raiz) {
  'use strict';

  // ------------------------------------------------------- a posição do sol

  const rad = g => g * Math.PI / 180;
  const grau = r => r * 180 / Math.PI;
  const mod = (v, m) => ((v % m) + m) % m;

  /** Se o sol sai do lugar: os limites em que o C# não lança exceção. */
  function lugarValido(latitude, longitude, fuso) {
    return Number.isFinite(latitude) && latitude >= -90 && latitude <= 90
      && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180
      && Number.isFinite(fuso) && fuso >= -14 && fuso <= 14;
  }

  /** O dia juliano (DiaJuliano do C#): o dia leva a fração. */
  function diaJuliano(ano, mes, dia) {
    if (mes <= 2) {
      ano--;
      mes += 12;
    }

    const a = Math.trunc(ano / 100);
    const b = 2 - a + Math.trunc(a / 4);

    return Math.floor(365.25 * (ano + 4716)) + Math.floor(30.6001 * (mes + 1)) + dia + b - 1524.5;
  }

  /** A refração atmosférica aproximada do NOAA, em graus (Refracao do C#). */
  function refracao(elevacao) {
    if (elevacao > 85) return 0;

    const tanE = Math.tan(elevacao * Math.PI / 180);
    let segundos;

    if (elevacao > 5) segundos = 58.1 / tanE - 0.07 / Math.pow(tanE, 3) + 0.000086 / Math.pow(tanE, 5);
    else if (elevacao > -0.575) segundos = 1735 + elevacao * (-518.2 + elevacao * (103.4 + elevacao * (-12.79 + elevacao * 0.711)));
    else segundos = -20.772 / tanE;

    return segundos / 3600;
  }

  /**
   * A posição do sol (SolarCalculator.Compute): azimute (do norte, sentido
   * horário) e elevação acima do horizonte em graus, com a refração; null se
   * o lugar não serve. Ano, mês (1 a 12), dia e minutos desde a meia-noite são
   * a hora do relógio do lugar; o fuso em horas (−3 em Brasília).
   */
  function posicao(latitude, longitude, ano, mes, dia, minutos, fuso) {
    if (!lugarValido(latitude, longitude, fuso) || !Number.isFinite(minutos)) return null;

    // A hora UTC: o relógio do lugar menos o fuso (o dia vira sozinho).
    const utc = new Date(Date.UTC(ano, mes - 1, dia) + Math.round((minutos - 60 * fuso) * 60000));
    const fracao = (utc.getUTCHours() * 3600000 + utc.getUTCMinutes() * 60000 + utc.getUTCSeconds() * 1000 + utc.getUTCMilliseconds()) / 86400000;

    // Dia juliano (UTC) e século juliano desde J2000.
    const jd = diaJuliano(utc.getUTCFullYear(), utc.getUTCMonth() + 1, utc.getUTCDate() + fracao);
    const t = (jd - 2451545.0) / 36525.0;

    const l0 = mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
    const m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
    const e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

    const c = Math.sin(rad(m)) * (1.914602 - t * (0.004817 + 0.000014 * t))
      + Math.sin(rad(2 * m)) * (0.019993 - 0.000101 * t)
      + Math.sin(rad(3 * m)) * 0.000289;

    const longitudeVerdadeira = l0 + c;
    const omega = 125.04 - 1934.136 * t;
    const longitudeAparente = longitudeVerdadeira - 0.00569 - 0.00478 * Math.sin(rad(omega));

    const obliquidadeMedia = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
    const obliquidade = obliquidadeMedia + 0.00256 * Math.cos(rad(omega));

    const declinacao = grau(Math.asin(Math.sin(rad(obliquidade)) * Math.sin(rad(longitudeAparente))));

    const y = Math.pow(Math.tan(rad(obliquidade / 2)), 2);
    const equacaoDoTempo = 4 * grau(
      y * Math.sin(2 * rad(l0))
      - 2 * e * Math.sin(rad(m))
      + 4 * e * y * Math.sin(rad(m)) * Math.cos(2 * rad(l0))
      - 0.5 * y * y * Math.sin(4 * rad(l0))
      - 1.25 * e * e * Math.sin(2 * rad(m)));

    // localTime.TimeOfDay do C#: a hora do relógio, sem o dia.
    const minutosDoDia = mod(minutos, 1440);
    const tempoSolarVerdadeiro = mod(minutosDoDia + equacaoDoTempo + 4 * longitude - 60 * fuso, 1440);
    const anguloHorario = tempoSolarVerdadeiro / 4 < 0 ? tempoSolarVerdadeiro / 4 + 180 : tempoSolarVerdadeiro / 4 - 180;

    const cosZenite = Math.sin(rad(latitude)) * Math.sin(rad(declinacao))
      + Math.cos(rad(latitude)) * Math.cos(rad(declinacao)) * Math.cos(rad(anguloHorario));
    const zenite = grau(Math.acos(Math.min(1, Math.max(-1, cosZenite))));
    const elevacao = 90 - zenite;

    // Azimute, do norte, horário.
    let azimute;
    const denominador = Math.cos(rad(latitude)) * Math.sin(rad(zenite));

    if (Math.abs(denominador) < 1e-12) {
      azimute = latitude > declinacao ? 180 : 0;
    } else {
      const cosAz = Math.min(1, Math.max(-1, (Math.sin(rad(latitude)) * Math.cos(rad(zenite)) - Math.sin(rad(declinacao))) / denominador));
      const a = grau(Math.acos(cosAz));
      azimute = anguloHorario > 0 ? mod(a + 180, 360) : mod(540 - a, 360);
    }

    return { azimute, elevacao: elevacao + refracao(elevacao), declinacao, equacaoDoTempo };
  }

  /** O vetor unitário que aponta para o sol: [leste, norte, cima] (SunPosition.Direction). */
  function direcao(azimute, elevacao) {
    const az = rad(azimute), el = rad(elevacao);
    return [Math.sin(az) * Math.cos(el), Math.cos(az) * Math.cos(el), Math.sin(el)];
  }

  /** Os dias do ano, em [mês, dia]. */
  function diasDoAno(ano) {
    const dias = [];
    for (let ms = Date.UTC(ano, 0, 1); new Date(ms).getUTCFullYear() === ano; ms += 86400000) {
      const d = new Date(ms);
      dias.push([d.getUTCMonth() + 1, d.getUTCDate()]);
    }
    return dias;
  }

  /**
   * Os equinócios e os solstícios do ano no lugar, em [mês, dia], nesta
   * ordem: março, junho, setembro, dezembro. É o dia da declinação do sol
   * ao meio-dia do relógio mais perto de zero (março e setembro), a maior
   * (junho) e a menor (dezembro): a data muda de um ano para outro.
   */
  function marcos(latitude, longitude, ano, fuso) {
    const dias = diasDoAno(ano).map(([mes, dia]) => ({ mes, dia, dec: posicao(latitude, longitude, ano, mes, dia, 720, fuso).declinacao }));
    const perto = (de, ate) => dias.filter(x => x.mes >= de && x.mes <= ate).reduce((a, b) => (Math.abs(b.dec) < Math.abs(a.dec) ? b : a));
    const maior = dias.reduce((a, b) => (b.dec > a.dec ? b : a));
    const menor = dias.reduce((a, b) => (b.dec < a.dec ? b : a));
    return [perto(2, 4), maior, perto(8, 10), menor].map(x => [x.mes, x.dia]);
  }

  /**
   * O nascer e o pôr do sol no relógio do lugar, em minutos do dia: [nascer,
   * pôr], o primeiro e o último minuto com o sol acima do horizonte (a
   * elevação com a refração maior que zero, como SunPosition.IsUp). Com o
   * fuso longe da longitude, o dia claro pode passar da meia-noite do
   * relógio: aí o nascer vem depois do pôr. [0, 1439] no sol da meia-noite;
   * null se o sol não nasce (noite polar).
   */
  function diaClaro(latitude, longitude, ano, mes, dia, fuso) {
    const acima = min => posicao(latitude, longitude, ano, mes, dia, mod(min, 1440), fuso).elevacao > 0;
    const passo = 10, amostras = [];
    for (let min = 0; min < 1440; min += passo) amostras.push(acima(min));

    if (!amostras.includes(true)) return null;
    if (!amostras.includes(false)) return [0, 1439];

    // O nascer: a amostra acima depois de uma abaixo; o pôr: a acima antes de
    // uma abaixo (em volta do dia, para o dia claro que passa da meia-noite).
    const n = amostras.length;
    let nascer = 0, por = 0;
    for (let k = 0; k < n; k++) {
      if (amostras[k] && !amostras[(k + n - 1) % n]) nascer = k * passo;
      if (amostras[k] && !amostras[(k + 1) % n]) por = k * passo;
    }

    // De 10 em 10 minutos achou; o minuto certo está nos 10 de antes e de depois.
    for (let i = 0; i < passo && acima(nascer - 1); i++) nascer = mod(nascer - 1, 1440);
    for (let i = 0; i < passo && acima(por + 1); i++) por = mod(por + 1, 1440);

    return [nascer, por];
  }

  /** Se o minuto do relógio cai fora do dia claro [nascer, pôr] (que pode passar da meia-noite). */
  function deNoite(claro, minutos) {
    return claro[0] <= claro[1] ? minutos < claro[0] || minutos > claro[1] : minutos > claro[1] && minutos < claro[0];
  }

  // ------------------------------------ os momentos da análise de sombras

  const QUANDO = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/;

  /**
   * Os momentos da análise de sombras do CAD (cena.momentos: [{quando:
   * 'AAAA-MM-DDTHH:MM', modulos: N}], no relógio da usina) que servem, na
   * ordem do tempo: [{indice, ano, mes, dia, minutos, modulos}], o indice o
   * da lista da cena (o f[13] das faces). O que não serve fica de fora.
   */
  function lerMomentos(lista) {
    const saida = [];
    if (!Array.isArray(lista)) return saida;

    lista.forEach((m, indice) => {
      const achado = m && typeof m.quando === 'string' ? QUANDO.exec(m.quando) : null;
      if (!achado || !Number.isInteger(m.modulos) || m.modulos < 1) return;

      const [ano, mes, dia, hora, minuto] = achado.slice(1).map(Number);
      const data = new Date(Date.UTC(ano, mes - 1, dia));
      if (ano < 1000 || data.getUTCMonth() !== mes - 1 || data.getUTCDate() !== dia || hora > 23 || minuto > 59) return;

      saida.push({ indice, ano, mes, dia, minutos: 60 * hora + minuto, modulos: m.modulos });
    });

    // O contrato já manda em ordem; aqui é só a garantia.
    return saida.sort((a, b) => a.ano - b.ano || a.mes - b.mes || a.dia - b.dia || a.minutos - b.minutos || a.indice - b.indice);
  }

  /** O modelo de texto da página com os campos {nome} trocados; o campo que não veio fica como está. */
  function preencher(modelo, campos) {
    return String(modelo || '').replace(/\{(\w+)\}/g, (x, nome) => (Object.prototype.hasOwnProperty.call(campos, nome) ? campos[nome] : x));
  }

  // ------------------------------------------------- a luz e os controles

  // A animação: uma hora do dia por segundo (um dia de 12 h em 12 s).
  const MINUTOS_POR_SEGUNDO = 60;

  // Metros por texel do mapa de sombra, quando o mapa máximo alcança.
  const TEXEL_ALVO = 0.1;

  /**
   * Monta o sol na cena, se ela tem o lugar (D.local) e a página tem o
   * #solPainel; senão nada muda (a luz de sempre) e devolve null.
   *
   * o.THREE, o.render (WebGLRenderer), o.cena, o.mundo (o grupo da usina, com
   * o exagero vertical em scale.y), o.luz (a DirectionalLight da página),
   * o.ceu (a HemisphereLight), o.camadas ({nome: Group}), o.usina (os nomes
   * das camadas que a sombra cobre), o.forcaSol e o.forcaCeu (as intensidades
   * com o sol). Devolve { refazer, quadro }: refazer() quando o exagero ou as
   * camadas mudam; quadro() a cada quadro (a animação).
   *
   * Os momentos da análise de sombras do CAD (31/10/2026): com o.D.momentos e
   * os controles na página (#solMomentos, com o #solMomento, o
   * #solMomentoVer e, se quiser, #solMomentoAntes, #solMomentoDepois e
   * #solMomentoTexto), o sol vai ao momento escolhido e os módulos dele
   * (o.D.faces com f[13]) se destacam na camada o.camadas.mesas. Sem os
   * controles, nada disso; o mesmo com o texto do botão de animar
   * (#solTocarIcone e #solTocarTexto, dentro do #solTocar).
   */
  function montar(o) {
    const L = o.D && o.D.local;
    const painel = typeof document === 'undefined' ? null : document.getElementById('solPainel');
    if (!painel || !L || !lugarValido(L.latitude, L.longitude, L.fuso)) return null;

    const THREE = o.THREE, render = o.render, cena = o.cena, mundo = o.mundo, luz = o.luz;
    const el = id => document.getElementById(id);
    const cSombras = el('solSombras'), cData = el('solData'), cHora = el('solHora'),
      cHoraTexto = el('solHoraTexto'), cTocar = el('solTocar'), cTexto = el('solTexto');
    const cTocarIcone = el('solTocarIcone'), cTocarTexto = el('solTocarTexto');
    const botoes = Array.from(painel.querySelectorAll('[data-marco]'));
    const lingua = document.documentElement.lang || undefined;
    const umaCasa = new Intl.NumberFormat(lingua, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    const inteiro = new Intl.NumberFormat(lingua, { maximumFractionDigits: 0 });
    const diaMes = new Intl.DateTimeFormat(lingua, { day: '2-digit', month: '2-digit', timeZone: 'UTC' });
    const dois = v => String(v).padStart(2, '0');

    // O fuso como o relógio mostra: UTC−3, UTC+5:30.
    const fh = Math.floor(Math.abs(L.fuso)), fm = Math.round((Math.abs(L.fuso) - fh) * 60);
    const fusoTexto = 'UTC' + (L.fuso < 0 ? '−' : '+') + fh + (fm ? ':' + dois(fm) : '');

    // O céu mais fraco que na luz de sempre: com o sol de um lado, a sombra aparece.
    o.ceu.intensity = o.forcaCeu;

    // A sombra. O mapa só é refeito quando algo muda (o sol, as camadas, o
    // exagero), não a cada quadro: a usina parada não custa nada a mais.
    render.shadowMap.enabled = true;
    render.shadowMap.type = THREE.PCFSoftShadowMap;
    render.shadowMap.autoUpdate = false;
    luz.castShadow = cSombras.checked;
    if (!luz.target.parent) cena.add(luz.target);

    // O ponto que a sombra consulta no mapa é afastado da superfície ao longo
    // da normal (contra a acne). A r128 usa a normal guardada no vértice, que
    // numa face dupla (DoubleSide) não vira com o lado que o sol ilumina: a
    // parede de canaleta com os triângulos ao contrário, ou o verso do
    // módulo, faria sombra nela mesma de frente para o sol. Nos materiais
    // Lambert (os da cena; o vértice deles conhece a luz) o afastamento vai
    // sempre para o lado do sol.
    const AFASTAMENTO = 'shadowWorldNormal * directionalLightShadows[ i ].shadowNormalBias';
    const trecho = THREE.ShaderChunk.shadowmap_vertex;
    const ladoDoSol = trecho.includes(AFASTAMENTO)
      ? shader => {
        shader.vertexShader = shader.vertexShader.replace('#include <shadowmap_vertex>', trecho.replace(AFASTAMENTO,
          'shadowWorldNormal * ( dot( transformedNormal, directionalLights[ i ].direction ) < 0.0 ? - 1.0 : 1.0 ) * directionalLightShadows[ i ].shadowNormalBias'));
      }
      : null;

    // O que só marca (as hachuras de sombra do CAD, o destaque dos módulos)
    // leva userData.semSombra: não faz nem recebe a sombra do sol.
    mundo.traverse(x => {
      if (x.isMesh && !x.userData.semSombra) {
        x.castShadow = true;
        x.receiveShadow = true;
        if (ladoDoSol && x.material.isMeshLambertMaterial) x.material.onBeforeCompile = ladoDoSol;
      }
    });

    // As caixas: a da usina (o que a câmera da sombra enxerga de lado) e a do
    // mundo, com o terreno (a profundidade: o morro entre o sol e a usina
    // também faz sombra nela).
    const usina = new THREE.Box3(), tudo = new THREE.Box3(), b = new THREE.Box3(), m4 = new THREE.Matrix4();
    const v = new THREE.Vector3(), centro = new THREE.Vector3(), d = new THREE.Vector3();

    /**
     * Junta à caixa o que o objeto tem, visível ou não. A three r128 não
     * conhece a caixa das instâncias (os pilares): a de cada uma vem da
     * matriz dela. Caixa com número que não serve (um NaN na cena) fica de
     * fora: estragaria a sombra da cena inteira.
     */
    function juntar(caixa, objeto) {
      const serve = () => [b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z].every(Number.isFinite);

      objeto.traverse(x => {
        const g = x.geometry;
        if (!g || !(x.isMesh || x.isLine || x.isPoints)) return;
        if (!g.boundingBox) g.computeBoundingBox();

        if (x.isInstancedMesh) {
          for (let i = 0; i < x.count; i++) {
            x.getMatrixAt(i, m4);
            b.copy(g.boundingBox).applyMatrix4(m4.premultiply(x.matrixWorld));
            if (serve()) caixa.union(b);
          }
        } else {
          b.copy(g.boundingBox).applyMatrix4(x.matrixWorld);
          if (serve()) caixa.union(b);
        }
      });
    }

    function medir() {
      mundo.updateMatrixWorld(true);
      tudo.makeEmpty();
      juntar(tudo, mundo);
      usina.makeEmpty();
      for (const nome of o.usina) if (o.camadas[nome]) juntar(usina, o.camadas[nome]);
      if (usina.isEmpty()) usina.copy(tudo);
      if (usina.isEmpty()) return;

      // Uma folga em volta: a sombra da beira e o barranco logo fora da usina.
      usina.getSize(v);
      const folga = Math.max(10, 0.05 * Math.hypot(v.x, v.z));
      usina.expandByVector(v.set(folga, 0, folga));
      tudo.union(usina);
    }

    medir();

    // O mapa: uns 10 cm por texel, até 4096 (no celular 2048: o de 4096 ocupa
    // uns 100 MB da placa) e o que a placa aceita. Fica do mesmo tamanho o
    // tempo todo: trocar de mapa a cada passo do sol pesaria.
    usina.getSize(v);
    const celular = typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;
    const teto = Math.min(render.capabilities.maxTextureSize || 2048, celular ? 2048 : 4096);
    let lado = Math.min(1024, teto);
    while (lado * 2 <= teto && Math.hypot(v.x, v.z) / lado > TEXEL_ALVO) lado *= 2;
    luz.shadow.mapSize.set(lado, lado);

    /** Leva a luz para a direção do sol ([leste, norte, cima]) e ajusta a câmera da sombra à usina. */
    function apontar(leste, norte, cima) {
      if (usina.isEmpty()) return;

      // Desenho (x leste, y norte, z cima) -> three (x, z, -y), como a cena.
      // O exagero vertical estica a direção como estica a cena: a sombra cai
      // no chão onde cairia sem ele.
      d.set(leste, cima * mundo.scale.y, -norte).normalize();
      usina.getCenter(centro);
      luz.target.position.copy(centro);
      luz.position.copy(centro).addScaledVector(d, tudo.getSize(v).length() + 10);
      luz.updateMatrixWorld();
      luz.target.updateMatrixWorld();

      const sombra = luz.shadow, cam = sombra.camera;
      sombra.updateMatrices(luz);

      let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity, z0 = Infinity, z1 = -Infinity;
      for (let i = 0; i < 8; i++) {
        v.set(i & 1 ? usina.max.x : usina.min.x, i & 2 ? usina.max.y : usina.min.y, i & 4 ? usina.max.z : usina.min.z)
          .applyMatrix4(cam.matrixWorldInverse);
        x0 = Math.min(x0, v.x); x1 = Math.max(x1, v.x);
        y0 = Math.min(y0, v.y); y1 = Math.max(y1, v.y);

        v.set(i & 1 ? tudo.max.x : tudo.min.x, i & 2 ? tudo.max.y : tudo.min.y, i & 4 ? tudo.max.z : tudo.min.z)
          .applyMatrix4(cam.matrixWorldInverse);
        z0 = Math.min(z0, v.z); z1 = Math.max(z1, v.z);
      }

      cam.left = x0; cam.right = x1; cam.bottom = y0; cam.top = y1;
      cam.near = Math.max(0.05, -z1 - 1); cam.far = -z0 + 1;
      cam.updateProjectionMatrix();

      // O viés: o bastante para a superfície não fazer sombra nela mesma
      // (acne), pouco para a sombra não descolar do pé do que a faz.
      const texel = Math.max(x1 - x0, y1 - y0) / sombra.mapSize.x;
      sombra.normalBias = texel;
      sombra.bias = -0.5 * texel / (cam.far - cam.near);
      render.shadowMap.needsUpdate = true;
    }

    // O dia e a hora do relógio da usina.
    let ano, mes, dia, minutos, datas = [], claro = null, tocando = false, antes = null;

    // Os momentos da análise de sombras do CAD (31/10/2026, Renan: "ver
    // momento que causa sombras"): os instantes, no relógio da usina, em que
    // módulos tiveram a pior sombra na análise do CAD. O sol vai para o
    // momento escolhido e os módulos dele (a face com f[13] = o índice do
    // momento) ficam em magenta: a sombra do sol tem de cair sobre eles.
    const momentos = lerMomentos(o.D.momentos);
    const cMomentos = el('solMomentos'), cMomento = el('solMomento'), cVer = el('solMomentoVer'),
      cAntes = el('solMomentoAntes'), cDepois = el('solMomentoDepois'), cMomentoTexto = el('solMomentoTexto');
    const comMomentos = momentos.length > 0 && !!(cMomentos && cMomento && cVer);
    let destaque = null, atual = 0;

    // As faces de cada momento, juntadas uma vez.
    const facesDoMomento = new Map();
    if (comMomentos) {
      for (const f of (Array.isArray(o.D.faces) ? o.D.faces : [])) {
        if (!Array.isArray(f) || f.length < 15 || !Number.isInteger(f[13])) continue;
        const lista = facesDoMomento.get(f[13]);
        if (lista) lista.push(f);
        else facesDoMomento.set(f[13], [f]);
      }
    }

    // O destaque: magenta translúcido por cima das faces (polygonOffset mais
    // forte que o delas) e o contorno de cada módulo, sem fazer nem receber
    // sombra, na camada das mesas (some com elas). O preenchimento é leve: a
    // sombra do sol no módulo tem de aparecer por baixo dele.
    const materiaisDoDestaque = comMomentos
      ? [new THREE.MeshBasicMaterial({
        color: 0xff00ff, transparent: true, opacity: 0.35, depthWrite: false, side: THREE.DoubleSide,
        polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -4,
      }), new THREE.LineBasicMaterial({ color: 0xff40ff, depthWrite: false })]
      : null;
    // A linha não tem polygonOffset: o mesmo empurrão para a câmera vai no
    // shader (uns 4 passos da profundidade de 24 bits), senão o contorno some
    // dentro do módulo visto de longe.
    if (comMomentos) {
      materiaisDoDestaque[1].onBeforeCompile = shader => {
        shader.vertexShader = shader.vertexShader.replace('#include <project_vertex>', '#include <project_vertex>\n\tgl_Position.z -= 5.0e-7 * gl_Position.w;');
      };
    }

    /** Tira o destaque e o texto dele (a pessoa mexeu no dia, na hora ou na animação). */
    function apagarDestaque() {
      if (destaque) {
        destaque.parent.remove(destaque);
        destaque.traverse(x => { if (x.geometry) x.geometry.dispose(); });
        destaque = null;
      }
      if (cMomentoTexto) cMomentoTexto.hidden = true;
      if (comMomentos) cVer.setAttribute('aria-pressed', 'false');
    }

    /** Destaca os módulos do momento m e escreve quantos são. */
    function destacar(m) {
      apagarDestaque();

      const pos = [], bordas = [];
      for (const f of facesDoMomento.get(m.indice) || []) {
        // Desenho (x leste, y norte, z cima) -> three (x, z, -y), como a cena.
        const v = [0, 1, 2, 3].map(k => [f[3 * k], f[3 * k + 2], -f[3 * k + 1]]);
        for (const k of [0, 1, 2, 0, 2, 3]) pos.push(v[k][0], v[k][1], v[k][2]);
        for (const k of [0, 1, 1, 2, 2, 3, 3, 0]) bordas.push(v[k][0], v[k][1], v[k][2]);
      }

      if (pos.length) {
        const g = new THREE.BufferGeometry(), b = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
        b.setAttribute('position', new THREE.Float32BufferAttribute(bordas, 3));
        destaque = new THREE.Group();
        destaque.add(new THREE.Mesh(g, materiaisDoDestaque[0]), new THREE.LineSegments(b, materiaisDoDestaque[1]));
        destaque.traverse(x => { x.userData.semSombra = true; x.renderOrder = 2; });
        ((o.camadas && o.camadas.mesas) || mundo).add(destaque);
      }

      if (cMomentoTexto) {
        const t = cMomentos.dataset;
        cMomentoTexto.textContent = preencher(m.modulos === 1 ? t.destaqueum : t.destaque, { n: inteiro.format(m.modulos) });
        cMomentoTexto.hidden = !cMomentoTexto.textContent;
      }
      cVer.setAttribute('aria-pressed', 'true');
    }

    /** Hoje, agora, no relógio da usina (não no do navegador); de noite, 10h. */
    function hoje() {
      const agora = new Date(Date.now() + L.fuso * 3600000);
      ano = agora.getUTCFullYear(); mes = agora.getUTCMonth() + 1; dia = agora.getUTCDate();
      minutos = agora.getUTCHours() * 60 + agora.getUTCMinutes() - agora.getUTCMinutes() % 5;
      if (!(posicao(L.latitude, L.longitude, ano, mes, dia, minutos, L.fuso).elevacao > 0)) minutos = 600;
    }

    /** Os atalhos com a data deles no ano escolhido. */
    function rotular() {
      datas = marcos(L.latitude, L.longitude, ano, L.fuso);
      for (const b of botoes) {
        const marco = datas[Number(b.dataset.marco)];
        if (marco) b.textContent = diaMes.format(new Date(Date.UTC(ano, marco[0] - 1, marco[1])));
      }
    }

    function mostrarData() {
      cData.value = String(ano).padStart(4, '0') + '-' + dois(mes) + '-' + dois(dia);
    }

    /** O sol do dia e da hora escolhidos: a luz, a sombra e o texto. */
    function atualizar() {
      const p = posicao(L.latitude, L.longitude, ano, mes, dia, minutos, L.fuso);
      const acima = p.elevacao > 0;

      // Do nascer até uns 6° de altura a luz direta cresce (o ar grosso do horizonte).
      luz.intensity = o.forcaSol * Math.min(1, Math.max(0, p.elevacao / 6));
      if (acima) {
        const [leste, norte, cima] = direcao(p.azimute, p.elevacao);
        apontar(leste, norte, cima);
      }

      cHora.value = String(Math.round(minutos));
      cHoraTexto.textContent = dois(Math.floor(minutos / 60)) + ':' + dois(Math.floor(minutos) % 60) + ' ' + fusoTexto;
      cTexto.textContent = acima
        ? painel.dataset.posicao.replace('{0}', umaCasa.format(p.elevacao)).replace('{1}', umaCasa.format(p.azimute))
        : painel.dataset.abaixo;
    }

    /** Liga ou pausa a animação do dia: do nascer ao pôr do sol, e de novo. */
    function tocar(sim) {
      if (sim) {
        claro = diaClaro(L.latitude, L.longitude, ano, mes, dia, L.fuso);
        if (!claro) sim = false;     // noite polar: nada a animar
        else if (deNoite(claro, minutos) || minutos === claro[1]) {
          minutos = claro[0];
          atualizar();
        }
      }

      tocando = sim;
      antes = null;
      (cTocarIcone || cTocar).textContent = sim ? '❚❚' : '▶';
      cTocar.title = sim ? painel.dataset.pausar : painel.dataset.animar;
      if (cTocarTexto) cTocarTexto.textContent = cTocar.title;
      cTocar.setAttribute('aria-label', cTocar.title);
      cTocar.setAttribute('aria-pressed', String(sim));
    }

    /** Leva o sol ao dia e à hora do relógio da usina, parado: a luz, a sombra, a data e a régua. */
    function irPara(a, m, d, min) {
      const outroAno = a !== ano;
      ano = a; mes = m; dia = d; minutos = min;
      if (outroAno) rotular();
      mostrarData();
      tocar(false);
      atualizar();
    }

    function setas() {
      if (cAntes) cAntes.disabled = atual <= 0;
      if (cDepois) cDepois.disabled = atual >= momentos.length - 1;
    }

    /** O momento k (na ordem do tempo): o sol vai para lá e os módulos dele se destacam. */
    function verMomento(k) {
      atual = Math.max(0, Math.min(momentos.length - 1, k));
      const m = momentos[atual];
      cMomento.value = String(atual);
      setas();
      irPara(m.ano, m.mes, m.dia, m.minutos);
      destacar(m);
    }

    function quadro() {
      if (!tocando) return;

      const agora = performance.now();
      if (antes !== null) {
        // Aba escondida e de volta: no máximo um décimo de segundo de um quadro para o outro.
        minutos = mod(minutos + Math.min(agora - antes, 100) / 1000 * MINUTOS_POR_SEGUNDO, 1440);
        if (deNoite(claro, minutos)) minutos = claro[0];
        atualizar();
      }
      antes = agora;
    }

    cSombras.addEventListener('change', () => {
      luz.castShadow = cSombras.checked;
      render.shadowMap.needsUpdate = true;
    });

    // Mexer na hora, na data, na animação ou nos atalhos tira o destaque do momento.
    cHora.addEventListener('input', () => {
      apagarDestaque();
      minutos = Number(cHora.value);
      atualizar();
    });

    cData.addEventListener('change', () => {
      apagarDestaque();
      const [a, m, dd] = cData.value.split('-').map(Number);
      // Data apagada ou fora do lugar: volta a que vale.
      if (!(a >= 1000 && a <= 9999 && m >= 1 && m <= 12 && dd >= 1 && dd <= 31)) {
        mostrarData();
        return;
      }

      const outroAno = a !== ano;
      ano = a; mes = m; dia = dd;
      if (outroAno) rotular();
      if (tocando) tocar(true);
      atualizar();
    });

    cTocar.addEventListener('click', () => {
      apagarDestaque();
      tocar(!tocando);
    });

    for (const b of botoes) {
      b.addEventListener('click', () => {
        const marco = datas[Number(b.dataset.marco)];
        if (!marco) return;
        apagarDestaque();
        [mes, dia] = marco;
        mostrarData();
        if (tocando) tocar(true);
        atualizar();
      });
    }

    // Os momentos na lista, na ordem do tempo, com o de mais módulos já
    // escolhido (no empate, o primeiro); o sol só vai quando a pessoa pede.
    if (comMomentos) {
      const t = cMomentos.dataset;
      cMomento.replaceChildren(...momentos.map((m, k) => {
        const opcao = document.createElement('option');
        opcao.value = String(k);
        opcao.textContent = preencher((m.modulos === 1 ? t.opcaoum : t.opcao) || '{ano}-{mes}-{dia} {hora} · {n}', {
          dia: dois(m.dia), mes: dois(m.mes), ano: String(m.ano),
          hora: dois(Math.floor(m.minutos / 60)) + ':' + dois(m.minutos % 60), n: inteiro.format(m.modulos),
        });
        return opcao;
      }));
      atual = momentos.reduce((melhor, m, k) => (m.modulos > momentos[melhor].modulos ? k : melhor), 0);
      cMomento.value = String(atual);
      setas();

      cMomento.addEventListener('change', () => verMomento(Number(cMomento.value)));
      cVer.addEventListener('click', () => verMomento(Number(cMomento.value)));
      if (cAntes) cAntes.addEventListener('click', () => verMomento(atual - 1));
      if (cDepois) cDepois.addEventListener('click', () => verMomento(atual + 1));
      cMomentos.hidden = false;
    }

    hoje();
    rotular();
    mostrarData();
    tocar(false);
    atualizar();
    painel.hidden = false;

    return {
      refazer() {
        medir();
        atualizar();
        render.shadowMap.needsUpdate = true;
      },
      quadro,
    };
  }

  const api = { posicao, direcao, marcos, diaClaro, lugarValido, montar };

  if (typeof module === 'object' && module && module.exports) module.exports = api;
  else raiz.ClivusSol = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
