"""Gera todo o material visual do Clivus Solar dentro do CAD.

Reprodutível (identidade visual de 04/10/2026): nunca edite PNG à mão;
mude o desenho aqui (ou o SVG de src/, que este script regrava) e rode:

    pip install cairosvg pillow
    python tools/build_icons.py

Saídas, em src/Clivus.Plugin/Resources/:
- Icons/src/<nome>.svg e <nome>-16.svg: os SVG-fonte (grade 32x32, traço na
  cor TRACO, que vira #0F2533 no tema claro e #FFFFFF no escuro);
- Icons/Light|Dark/<nome>_16.png, _32.png, _16@2x.png (32 px), _32@2x.png (64 px);
- Branding/: os logos SVG do pacote, clivus.ico (16 a 256 px), o logo da
  janela Sobre em 480 e 960 px e o símbolo da ribbon;
- tools/icons_preview.html: todos os ícones em 16 e 32 px, sobre fundo
  claro (#F0F0F0) e escuro (#3B4453), para revisar.

Regras dos ícones (do pacote de identidade): traço único de 2,5 px em 32 px,
pontas e cantos arredondados, sem preenchimento grande; âmbar #F4A51C só no
elemento que é a ação ou o módulo FV (no máximo um por ícone); margem de 2 px;
nada de texto. Em 16 px o desenho é simplificado (menos linhas, traço de 1,5 px),
não só reduzido.
"""
import base64
import io
import math
import os

import cairosvg
from PIL import Image

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RECURSOS = os.path.join(RAIZ, 'src', 'Clivus.Plugin', 'Resources')
ICONES = os.path.join(RECURSOS, 'Icons')
MARCA = os.path.join(RECURSOS, 'Branding')

PETROLEO = '#0F2533'
AMBAR = '#F4A51C'
BRANCO = '#FFFFFF'
TEMAS = {'Light': PETROLEO, 'Dark': BRANCO}

# ------------------------------------------------------------ primitivas
# Cada ícone é uma lista de elementos SVG em grade 32x32. "T" é o traço
# (tema), "A" o âmbar. O traço de 32 px é 2,5; o de 16 px, 3 na grade de 32
# (1,5 px depois de reduzir).


def traco(d, largura):
    return f'<path d="{d}" fill="none" stroke="T" stroke-width="{largura}" stroke-linecap="round" stroke-linejoin="round"/>'


def ambar_cheio(d):
    return f'<path d="{d}" fill="{AMBAR}" stroke="{AMBAR}" stroke-width="1" stroke-linejoin="round"/>'


def ambar_traco(d, largura):
    return f'<path d="{d}" fill="none" stroke="{AMBAR}" stroke-width="{largura}" stroke-linecap="round" stroke-linejoin="round"/>'


def circulo(cx, cy, r):
    return f'M{cx - r},{cy} a{r},{r} 0 1,0 {2 * r},0 a{r},{r} 0 1,0 {-2 * r},0'


def modulo(cx, cy, w=8, h=5, ang=-22):
    """O retângulo inclinado do logo: o módulo FV."""
    a = math.radians(ang)
    c, s = math.cos(a), math.sin(a)
    pts = []
    for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append((cx + dx * c - dy * s, cy + dx * s + dy * c))
    return 'M' + ' L'.join(f'{x:.2f},{y:.2f}' for x, y in pts) + ' Z'


def ciclo(cx, cy, r, inicio=200, fim=500):
    """Seta circular (regerar, recalcular): arco e a ponta."""
    a0, a1 = math.radians(inicio), math.radians(fim)
    x0, y0 = cx + r * math.cos(a0), cy + r * math.sin(a0)
    x1, y1 = cx + r * math.cos(a1), cy + r * math.sin(a1)
    grande = 1 if (fim - inicio) % 360 > 180 else 0
    tx, ty = -math.sin(a1), math.cos(a1)
    p = 3.2
    ponta = (f' M{x1 - p * tx - p * 0.7 * math.cos(a1):.2f},{y1 - p * ty - p * 0.7 * math.sin(a1):.2f}'
             f' L{x1:.2f},{y1:.2f} L{x1 - p * tx + p * 0.7 * math.cos(a1):.2f},{y1 - p * ty + p * 0.7 * math.sin(a1):.2f}')
    return f'M{x0:.2f},{y0:.2f} A{r},{r} 0 {grande},1 {x1:.2f},{y1:.2f}' + ponta


def mesa(x, y, w=16, h=7):
    """Uma mesa em planta inclinada (paralelogramo)."""
    return f'M{x},{y + h} L{x + 3},{y} L{x + w + 3},{y} L{x + w},{y + h} Z'


def folha(x=7, y=3, w=17, h=24, dobra=5):
    return f'M{x},{y} H{x + w - dobra} L{x + w},{y + dobra} V{y + h} H{x} Z M{x + w - dobra},{y} V{y + dobra} H{x + w}'


# ------------------------------------------------------------- os ícones
# nome -> (elementos de 32, elementos de 16)


def icones():
    L, S = 2.5, 3.0
    T = lambda d: traco(d, L)  # noqa: E731
    t = lambda d: traco(d, S)  # noqa: E731
    gear_dentes = ' '.join(
        f'M{16 + 9 * math.cos(math.radians(a)):.2f},{16 + 9 * math.sin(math.radians(a)):.2f} L{16 + 12.5 * math.cos(math.radians(a)):.2f},{16 + 12.5 * math.sin(math.radians(a)):.2f}'
        for a in range(0, 360, 45))
    gear4 = ' '.join(
        f'M{16 + 8 * math.cos(math.radians(a)):.2f},{16 + 8 * math.sin(math.radians(a)):.2f} L{16 + 12.5 * math.cos(math.radians(a)):.2f},{16 + 12.5 * math.sin(math.radians(a)):.2f}'
        for a in range(0, 360, 90))
    curvas = ['M3,24 C9,18 15,27 21,21 S27,16 29,18', 'M3,16 C9,10 15,19 21,13 S27,8 29,10', 'M6,8 C11,4 17,10 23,6']
    arvore32 = [T('M16,17 V29'), T('M9,4 H23 a4,4 0 0,1 4,4 V13 a4,4 0 0,1 -4,4 H9 a4,4 0 0,1 -4,-4 V8 a4,4 0 0,1 4,-4 Z'), T('M8,29 H24')]
    arvore16 = [t('M16,17 V29'), t('M5,4 H27 V17 H5 Z')]

    return {
        'configuracoes': ([T(circulo(16, 16, 5.5)), T(circulo(16, 16, 9)), T(gear_dentes)],
                          [t(circulo(16, 16, 6)), t(gear4)]),
        'terreno': ([T(c) for c in curvas], [t(curvas[0]), t(curvas[1])]),
        'terreno_resumo': ([T(folha()), T('M10,14 C13,11 16,16 20,13'), T('M10,21 C13,18 16,23 20,20')],
                           [t(folha()), t('M10,17 C13,13 16,20 20,16')]),
        'coordenada': ([T('M16,3 V10 M16,22 V29 M3,16 H10 M22,16 H29'), T(circulo(16, 16, 6)), ambar_cheio(circulo(16, 16, 2.2))],
                       [t('M16,3 V11 M16,21 V29 M3,16 H11 M21,16 H29'), ambar_cheio(circulo(16, 16, 3))]),
        'area': ([T('M5,23 L11,6 L21,10 L27,5 L28,25 L6,27 Z')], [t('M5,24 L11,6 L27,6 L28,26 Z')]),
        'alinhamento': ([T('M5,27 L27,5'), T('M15,25 L25,15 M21,15 H25 V19'), ambar_cheio(modulo(9, 11))],
                        [t('M5,27 L27,5'), ambar_cheio(modulo(10, 11, 9, 6))]),
        'usina': ([T(modulo(7, 18, 7, 5)), ambar_cheio(modulo(16, 15.5, 7, 5)), T(modulo(25, 13, 7, 5)), T('M3,27 H29')],
                  [t(modulo(8, 16, 10, 7)), ambar_cheio(modulo(22, 13, 10, 7))]),
        'regerar_area': ([T('M4,17 L8,5 L16,7 L18,17 Z'), T(ciclo(21, 21, 7)), ambar_cheio(modulo(21, 21, 5, 3.5))],
                         [t('M4,16 L8,4 L17,6 L18,16 Z'), t(ciclo(21, 21, 7))]),
        'regerar_todas': ([T('M3,13 L6,4 L12,6 L13,13 Z'), T('M15,10 L18,3 L25,5 L26,10 Z'), T(ciclo(19, 22, 6.5)), ambar_cheio(modulo(19, 22, 5, 3.5))],
                          [t('M3,13 L6,4 L13,6 L14,13 Z'), t(ciclo(19, 22, 7))]),
        'recalcular': ([T(mesa(4, 5, 16, 7)), T(ciclo(21, 22, 6.5)), ambar_cheio(modulo(21, 22, 5, 3.5))],
                       [t(mesa(4, 5, 16, 7)), t(ciclo(21, 22, 6.5))]),
        'recalcular_sujas': ([T(mesa(4, 5, 16, 7)), T(ciclo(21, 22, 6.5)), ambar_cheio(circulo(6, 22, 2.5))],
                             [t(mesa(4, 5, 16, 7)), ambar_cheio(circulo(7, 22, 3)), t(ciclo(22, 22, 6))]),
        'pontas': ([T('M3,26 C10,22 20,28 29,23'), T('M6,13 L26,9'), T('M6,13 V22 M26,9 V21'), ambar_cheio(modulo(16, 11, 8, 3.5, -11))],
                   [t('M4,26 L28,23'), t('M6,12 L26,8'), t('M6,12 V24 M26,8 V22')]),
        'validar': ([T(circulo(16, 16, 12)), ambar_traco('M10,16.5 L14.5,21 L22.5,11.5', L)],
                    [t(circulo(16, 16, 12)), ambar_traco('M10,16.5 L14.5,21 L22.5,11.5', S)]),
        'recontar': ([T('M4,8 H17 M4,16 H17 M4,24 H17'), T('M21,6 V12 M24,6 V12 M27,6 V12 M20,13 L28,5'), ambar_cheio(modulo(24, 22, 8, 5))],
                     [t('M4,9 H16 M4,17 H16 M4,25 H16'), ambar_cheio(modulo(24, 17, 9, 6))]),
        'renomear': ([T('M4,10 H18 V24 H4 Z'), T('M15,25 L27,13 L30,16 L18,28 L14,29 Z'), ambar_cheio('M15,25 L18,28 L14,29 Z')],
                     [t('M4,10 H18 V24 H4 Z'), t('M16,26 L28,14')]),
        'estado': ([T('M13,8 H28 M13,16 H28 M13,24 H28'), T('M3,8 L5,10 L9,6 M3,16 L5,18 L9,14'), ambar_cheio(circulo(6, 24, 2.3))],
                   [t('M13,9 H28 M13,17 H28 M13,25 H28'), ambar_cheio(circulo(6, 9, 2.6))]),
        'sujar': ([T(mesa(3, 12, 18, 9)), ambar_cheio(circulo(25, 9, 3.5))],
                  [t(mesa(3, 12, 18, 9)), ambar_cheio(circulo(25, 8, 4))]),
        'trocar': ([T('M3,6 H29 V11 H3 Z'), T('M3,21 H14 V26 H3 Z'), ambar_cheio('M18,21 H29 V26 H18 Z'), T('M9,13 V18 M7,16 L9,18 L11,16 M23,18 V13 M21,15 L23,13 L25,15')],
                   [t('M3,6 H29 V11 H3 Z'), t('M3,21 H14 V26 H3 Z'), ambar_cheio('M18,21 H29 V26 H18 Z')]),
        'regerar_fileira': ([T(modulo(6.5, 8, 5.5, 4)), T(modulo(16, 8, 5.5, 4)), T(modulo(25.5, 8, 5.5, 4)), T(ciclo(16, 22, 6.5)), ambar_cheio(modulo(16, 22, 5, 3.5))],
                            [t(modulo(8, 8, 9, 5)), t(modulo(24, 8, 9, 5)), t(ciclo(16, 22, 6.5))]),
        'grupos': ([T('M5,4 H27 a2,2 0 0,1 2,2 V26 a2,2 0 0,1 -2,2 H5 a2,2 0 0,1 -2,-2 V6 a2,2 0 0,1 2,-2 Z').replace('stroke-linecap', 'stroke-dasharray="3 3" stroke-linecap'), T(modulo(11, 18, 8, 5)), ambar_cheio(modulo(21, 14, 8, 5))],
                   [t('M4,4 H28 V28 H4 Z'), ambar_cheio(modulo(16, 16, 12, 7))]),
        'analises': ([T('M4,28 H28'), T('M8,24 V15 M14,24 V8'), ambar_traco('M20,24 V12', L), T('M26,24 V18')],
                     [t('M4,28 H28'), t('M9,24 V14'), ambar_traco('M16,24 V8', S), t('M23,24 V17')]),
        'tags': ([T('M4,6 V15 L17,28 L28,17 L15,4 H6 a2,2 0 0,0 -2,2 Z'), ambar_cheio(circulo(10, 10, 2.3))],
                 [t('M4,5 V15 L17,28 L28,17 L15,4 Z'), ambar_cheio(circulo(10, 10, 2.8))]),
        'arvore': (arvore32, arvore16),
        'sombras': ([ambar_cheio(circulo(7, 7, 3.5)), T('M17,17 V26'), T('M12,6 H22 V16 H12 Z'), T('M17,26 L29,22'), T('M4,28 H29')],
                    [ambar_cheio(circulo(7, 7, 4)), t('M18,18 V27'), t('M13,6 H23 V17 H13 Z')]),
        'pvsyst': ([T(folha(4, 3, 17, 24)), ambar_cheio(modulo(12, 18, 8, 5)), T('M22,16 H29 M26,13 L29,16 L26,19')],
                   [t(folha(4, 3, 17, 24)), t('M22,16 H29 M26,13 L29,16 L26,19')]),
        'excel': ([T(folha(4, 3, 17, 24)), T('M8,13 H17 M8,18 H17 M8,23 H17 M12,13 V23'), ambar_traco('M22,16 H29 M26,13 L29,16 L26,19', L)],
                  [t(folha(4, 3, 17, 24)), ambar_traco('M22,16 H29 M26,13 L29,16 L26,19', S)]),
        'ver3d': ([ambar_cheio('M16,4 L27,10 L16,16 L5,10 Z'), T('M16,4 L27,10 L27,22 L16,28 L5,22 L5,10 Z M5,10 L16,16 L27,10 M16,16 V28')],
                  [t('M16,4 L27,10 L27,22 L16,28 L5,22 L5,10 Z M5,10 L16,16 L27,10 M16,16 V28')]),
        'edicao': ([T(mesa(3, 17, 15, 8)), T('M15,17 L25,7 L28,10 L18,20 L14,21 Z'), ambar_cheio('M15,17 L18,20 L14,21 Z')],
                   [t(mesa(3, 17, 15, 8)), t('M16,17 L26,7')]),
        'objetos': (arvore32[:2] + [ambar_cheio(modulo(26, 26, 6, 4))], arvore16),
    }


# ----------------------------------------------------------------- saída


def svg(elementos, cor):
    corpo = ''.join(elementos).replace('stroke="T"', f'stroke="{cor}"')
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">{corpo}</svg>'


def png(svg_texto, lado):
    return cairosvg.svg2png(bytestring=svg_texto.encode('utf-8'), output_width=lado, output_height=lado)


def gravar(caminho, dados):
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    modo = 'wb' if isinstance(dados, bytes) else 'w'
    with open(caminho, modo, **({} if modo == 'wb' else {'encoding': 'utf-8', 'newline': '\n'})) as f:
        f.write(dados)


def gerar_icones():
    nomes = []
    for nome, (de32, de16) in icones().items():
        nomes.append(nome)
        gravar(os.path.join(ICONES, 'src', f'{nome}.svg'), svg(de32, 'currentColor').replace('currentColor', 'T'))
        gravar(os.path.join(ICONES, 'src', f'{nome}-16.svg'), svg(de16, 'currentColor').replace('currentColor', 'T'))

        for tema, cor in TEMAS.items():
            s32, s16 = svg(de32, cor), svg(de16, cor)
            pasta = os.path.join(ICONES, tema)
            gravar(os.path.join(pasta, f'{nome}_16.png'), png(s16, 16))
            gravar(os.path.join(pasta, f'{nome}_32.png'), png(s32, 32))
            gravar(os.path.join(pasta, f'{nome}_16@2x.png'), png(s16, 32))
            gravar(os.path.join(pasta, f'{nome}_32@2x.png'), png(s32, 64))
    return nomes


# ---------------------------------------------------------- a marca (logos)

SIMBOLO = ('<path d="M88.64 34.29A40 40 0 1 0 88.64 85.71" fill="none" stroke="{c}" stroke-width="6" stroke-linecap="round"/>'
           '<path d="M80.98 40.72A30 30 0 1 0 80.98 79.28" fill="none" stroke="{c}" stroke-width="6" stroke-linecap="round"/>'
           '<path d="M73.32 47.14A20 20 0 1 0 73.32 72.86" fill="none" stroke="{c}" stroke-width="6" stroke-linecap="round"/>'
           '<rect x="49.0" y="54.0" width="20" height="12" rx="2" fill="#F4A51C" transform="rotate(-22 59 60)"/>')

RIBBON = ('<path d="M91.71 31.72A44 44 0 1 0 91.71 88.28" fill="none" stroke="{c}" stroke-width="11" stroke-linecap="round"/>'
          '<path d="M77.92 43.29A26 26 0 1 0 77.92 76.71" fill="none" stroke="{c}" stroke-width="11" stroke-linecap="round"/>'
          '<rect x="47.0" y="52.0" width="24" height="16" rx="2" fill="#F4A51C" transform="rotate(-22 59 60)"/>')

# O nome "Clivus" em Sora 700, já em contorno (do pacote de identidade).
NOME = ('M140.225 89.482Q134.81 89.482 130.8295 87.6865Q126.849 85.891 124.1985 82.8415Q121.548 79.792 120.237 75.954Q118.926 72.116 118.926 68.05V66.568'
        'Q118.926 62.293 120.294 58.398Q121.662 54.503 124.3505 51.4725Q127.039 48.442 131.01 46.675Q134.981 44.908 140.187 44.908Q145.792 44.908 150.1145 47.093'
        'Q154.437 49.278 157.021 53.135Q159.605 56.992 159.909 62.046H148.015Q147.711 59.215 145.7445 57.2675Q143.778 55.32 140.187 55.32Q137.109 55.32 135.1045 56.84'
        'Q133.1 58.36 132.112 61.039Q131.124 63.718 131.124 67.309Q131.124 70.691 132.0265 73.3795Q132.929 76.068 134.943 77.569Q136.957 79.07 140.225 79.07'
        'Q142.676 79.07 144.386 78.196Q146.096 77.322 147.103 75.7545Q148.11 74.187 148.357 72.116H160.251Q159.966 77.303 157.306 81.198Q154.646 85.093 150.2475 87.2875'
        'Q145.849 89.482 140.225 89.482ZM166.236 88.0V32.52H178.434V88.0ZM161.296 41.488V32.52H178.434V41.488ZM189.302 88.0V46.39H201.5V88.0ZM183.754 55.358V46.39H201.5V55.358Z'
        'M194.0615 42.248Q190.632 42.248 188.998 40.4715Q187.364 38.695 187.364 35.921Q187.364 33.147 189.0048 31.3705Q190.6456 29.594 194.033 29.594Q197.491 29.594 199.0965 31.3705'
        'Q200.702 33.147 200.702 35.921Q200.702 38.695 199.0965 40.4715Q197.491 42.248 194.0615 42.248ZM218.524 88.0 206.06 46.39H218.562L230.323 88.0ZM222.286 88.0V77.892H234.579V88.0Z'
        'M226.808 88.0 236.878 46.39H248.582L237.828 88.0ZM267.582 89.33Q260.419 89.33 256.5145 84.6465Q252.61 79.963 252.61 70.501V46.371H264.808V71.147Q264.808 74.453 266.708 76.4385'
        'Q268.608 78.424 271.819 78.424Q275.068 78.424 277.101 76.3815Q279.134 74.339 279.134 70.843V46.371H291.332V88.0H281.68V70.425H282.478Q282.478 76.771 280.8535 80.97'
        'Q279.229 85.169 276.0465 87.2495Q272.864 89.33 268.114 89.33ZM316.07 89.33Q307.235 89.33 302.2095 85.6535Q297.184 81.977 296.861 75.365H307.691Q307.957 77.379 310.0565 78.9275'
        'Q312.156 80.476 316.298 80.476Q319.509 80.476 321.6085 79.393Q323.708 78.31 323.708 76.239Q323.708 74.415 322.093 73.294Q320.478 72.173 316.393 71.774L313.125 71.432'
        'Q305.639 70.653 301.877 67.2615Q298.115 63.87 298.115 58.531Q298.115 54.085 300.319 51.14Q302.523 48.195 306.418 46.6845Q310.313 45.174 315.329 45.174Q323.366 45.174 328.306 48.6985'
        'Q333.246 52.223 333.512 58.911H322.682Q322.435 56.878 320.5825 55.453Q318.73 54.028 315.215 54.028Q312.384 54.028 310.7215 55.092Q309.059 56.156 309.059 57.961Q309.059 59.728 310.503 60.64'
        'Q311.947 61.552 315.177 61.932L318.445 62.274Q326.121 63.072 330.3865 66.511Q334.652 69.95 334.652 75.669Q334.652 79.868 332.372 82.9365Q330.092 86.005 325.9215 87.6675'
        'Q321.751 89.33 316.07 89.33Z')


def gerar_marca():
    def caixa(w, h, corpo):
        return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{corpo}</svg>'

    logo_claro = caixa(359, 120, SIMBOLO.format(c=PETROLEO) + f'<path d="{NOME}" fill="{PETROLEO}"/>')
    logo_escuro = caixa(359, 120, f'<rect width="359" height="120" rx="20" fill="{PETROLEO}"/>' + SIMBOLO.format(c=BRANCO) + f'<path d="{NOME}" fill="{BRANCO}"/>')
    icone_claro = caixa(120, 120, f'<rect width="120" height="120" rx="28" fill="{BRANCO}"/>' + SIMBOLO.format(c=PETROLEO))
    icone_escuro = caixa(120, 120, f'<rect width="120" height="120" rx="28" fill="{PETROLEO}"/>' + SIMBOLO.format(c=BRANCO))
    simbolo = caixa(120, 120, SIMBOLO.format(c=PETROLEO))

    arquivos = {
        'clivus-logo-horizontal-claro.svg': logo_claro,
        'clivus-logo-horizontal-escuro.svg': logo_escuro,
        'clivus-icone-claro.svg': icone_claro,
        'clivus-icone-escuro.svg': icone_escuro,
        'clivus-simbolo.svg': simbolo,
    }
    for tema, cor in TEMAS.items():
        nome = 'claro' if tema == 'Light' else 'escuro'
        arquivos[f'clivus-ribbon-32-tema-{nome}.svg'] = ('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="8 10 100 100">'
                                                         + RIBBON.format(c=cor) + '</svg>')

    for nome, texto in arquivos.items():
        gravar(os.path.join(MARCA, nome), texto)

    # O símbolo vira o ícone "sobre" da ribbon, nos dois temas.
    for tema, cor in TEMAS.items():
        nome = 'claro' if tema == 'Light' else 'escuro'
        s = arquivos[f'clivus-ribbon-32-tema-{nome}.svg']
        pasta = os.path.join(ICONES, tema)
        gravar(os.path.join(pasta, 'sobre_16.png'), png(s, 16))
        gravar(os.path.join(pasta, 'sobre_32.png'), png(s, 32))
        gravar(os.path.join(pasta, 'sobre_16@2x.png'), png(s, 32))
        gravar(os.path.join(pasta, 'sobre_32@2x.png'), png(s, 64))

    # O logo da janela Sobre (480 e 960 px de largura).
    for largura, nome in ((480, 'clivus-logo-480.png'), (960, 'clivus-logo-960.png')):
        gravar(os.path.join(MARCA, nome), cairosvg.svg2png(bytestring=logo_claro.encode(), output_width=largura, output_height=round(largura * 120 / 359)))

    # clivus.ico: janelas, paleta e o pacote .bundle.
    grande = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=icone_escuro.encode(), output_width=256, output_height=256)))
    caminho = os.path.join(MARCA, 'clivus.ico')
    os.makedirs(MARCA, exist_ok=True)
    grande.save(caminho, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (256, 256)])
    gravar(os.path.join(MARCA, 'clivus-icone-1024.png'), cairosvg.svg2png(bytestring=icone_escuro.encode(), output_width=1024, output_height=1024))


# ------------------------------------------------------------------ prévia


def gerar_previa(nomes):
    def img(tema, nome, sufixo):
        with open(os.path.join(ICONES, tema, f'{nome}{sufixo}.png'), 'rb') as f:
            return 'data:image/png;base64,' + base64.b64encode(f.read()).decode()

    linhas = []
    for nome in nomes + ['sobre']:
        celulas = ''.join(
            f'<td style="background:{fundo}"><img src="{img(tema, nome, "_16")}" width="16" height="16"> '
            f'<img src="{img(tema, nome, "_32")}" width="32" height="32"> '
            f'<img src="{img(tema, nome, "_32@2x")}" width="64" height="64"></td>'
            for tema, fundo in (('Light', '#F0F0F0'), ('Dark', '#3B4453')))
        linhas.append(f'<tr><th>{nome}</th>{celulas}</tr>')

    html = ('<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Clivus Solar — ícones</title>'
            '<style>body{font:14px "Segoe UI",sans-serif;background:#fff;color:#0F2533;margin:24px}'
            'table{border-collapse:collapse}th,td{padding:8px 14px;text-align:left}th{font-weight:600}'
            'td img{vertical-align:middle;margin-right:10px;image-rendering:auto}</style></head><body>'
            '<h1>Clivus Solar — ícones da ribbon</h1>'
            '<p>Cada linha: 16 px, 32 px e 32@2x, no tema claro (#F0F0F0) e no escuro (#3B4453). Gerado por tools/build_icons.py.</p>'
            '<table><tr><th>ícone</th><th>tema claro</th><th>tema escuro</th></tr>' + ''.join(linhas) + '</table></body></html>')
    gravar(os.path.join(RAIZ, 'tools', 'icons_preview.html'), html)


if __name__ == '__main__':
    nomes = gerar_icones()
    gerar_marca()
    gerar_previa(nomes)
    print(f'{len(nomes) + 1} ícones em 2 temas e 4 tamanhos; marca e prévia gravadas.')
