// O resumo da usina no 3D (passo 42.2, Renan em 31/10/2026: "Nas camadas,
// falta a opção de resumo, onde liga e desliga o mostrar de uma tabela com a
// quantidade de módulos e potência dc"). Os números vêm do plugin (a cena leva
// "resumo": o total e, quando há, as linhas por área e por tipo de mesa, pela
// conta da janela Áreas); aqui só se monta o quadro.
//
// O mesmo arquivo serve os dois visualizadores: a página local (o
// Viewer3DPage do plugin o embute no HTML) e a do servidor
// (/estatico/resumo.js no repositório clivussolar, cópia deste). Nenhuma frase
// de usuário aqui: os textos vêm dos atributos data-* do #resumoQuadro.
(function (raiz) {
  'use strict';

  const numero = v => typeof v === 'number' && Number.isFinite(v) && v >= 0;
  const linhaValida = l => !!l && numero(l.mesas) && numero(l.modulos) && numero(l.kwp);

  /**
   * Monta o quadro (#resumoQuadro) com D.resumo e liga a ele o item
   * #vResumo das camadas (o rótulo #itemResumo). Sem o resumo na cena, o item
   * continua escondido. ligado: se o quadro abre aparecendo.
   */
  function montar(D, ligado) {
    const quadro = document.getElementById('resumoQuadro'), item = document.getElementById('itemResumo'), caixa = document.getElementById('vResumo');
    const R = D && D.resumo;
    if (!quadro || !item || !caixa || !R || !linhaValida(R.total)) return false;

    const t = quadro.dataset;
    const lingua = document.documentElement.lang || undefined;
    const inteiro = new Intl.NumberFormat(lingua, { maximumFractionDigits: 0 });
    const umaCasa = new Intl.NumberFormat(lingua, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    const numeros = l => [inteiro.format(l.mesas), inteiro.format(l.modulos), umaCasa.format(l.kwp)];

    const tabela = document.createElement('table');
    tabela.createCaption().textContent = t.titulo;

    function linha(classe, celulas, tag) {
      const tr = tabela.insertRow();
      if (classe) tr.className = classe;
      for (const texto of celulas) {
        const c = document.createElement(tag || 'td');
        c.textContent = texto;
        tr.appendChild(c);
      }
      return tr;
    }

    function secao(titulo) {
      const c = linha('secao', [titulo]).cells[0];
      c.colSpan = 4;
    }

    linha('', ['', t.mesas, t.modulos, 'kWp'], 'th');
    linha('total', [t.total, ...numeros(R.total)]);

    // Por área (com a linha das mesas fora das áreas, no idioma da página) e por tipo de mesa.
    const areas = Array.isArray(R.areas) ? R.areas.filter(linhaValida) : [];
    const tipos = Array.isArray(R.tipos) ? R.tipos.filter(linhaValida) : [];

    if (areas.length) {
      secao(t.area);
      for (const a of areas) linha(a.fora ? 'fora' : '', [a.fora ? t.fora : String(a.nome || ''), ...numeros(a)]);
    }

    if (tipos.length) {
      secao(t.tipo);
      for (const a of tipos) linha('', [String(a.nome || ''), ...numeros(a)]);
    }

    quadro.replaceChildren(tabela);
    item.hidden = false;
    caixa.checked = !!ligado;
    quadro.hidden = !ligado;
    caixa.addEventListener('change', () => { quadro.hidden = !caixa.checked; });
    return true;
  }

  const api = { montar, linhaValida };

  if (typeof module === 'object' && module && module.exports) module.exports = api;
  else raiz.ClivusResumo = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
