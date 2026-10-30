"""As frases do Clivus Solar que precisam de tradução (etapa 10).

Lê do código toda frase passada a Tr.T("..."), Tr.F("...") ou Tr.N("..."), a mesma regra
do teste TranslationCatalogTests, e compara com os catálogos
src/Clivus.Core/Translations/{en,es}.json.

    python tools/traducoes.py              lista quantas faltam e quantas sobram
    python tools/traducoes.py --faltam X   grava em X (JSON) as frases sem tradução
                                           em algum dos dois idiomas
    python tools/traducoes.py --juntar Y   junta ao catálogo as traduções de Y
                                           (JSON {"frase": {"en": ..., "es": ...}})
    python tools/traducoes.py --limpar     tira dos catálogos as frases que saíram do código

Os catálogos ficam em ordem alfabética, um por linha, UTF-8 sem BOM.
"""
import json
import os
import re
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PASTAS = ['src/Clivus.Core', 'src/Clivus.Plugin', 'src/Clivus.Instalador', 'src/Clivus.Link']
CHAMADA = re.compile(r'\bTr\.[TFN]\(\s*(\$?)"((?:[^"\\]|\\.)*)"')
ESCAPES = {'n': '\n', 't': '\t', 'r': '\r', '0': '\0'}


def desescapar(s):
    saida, i = [], 0
    while i < len(s):
        if s[i] == '\\' and i + 1 < len(s):
            i += 1
            saida.append(ESCAPES.get(s[i], s[i]))
        else:
            saida.append(s[i])
        i += 1
    return ''.join(saida)


def frases_do_codigo():
    frases = set()
    for pasta in PASTAS:
        for base, dirs, arquivos in os.walk(os.path.join(RAIZ, pasta)):
            dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
            for nome in arquivos:
                if not nome.endswith('.cs'):
                    continue
                with open(os.path.join(base, nome), encoding='utf-8-sig') as f:
                    for m in CHAMADA.finditer(f.read()):
                        frase = desescapar(m.group(2)).strip()
                        if frase:
                            frases.add(frase)
    return frases


def caminho(codigo):
    return os.path.join(RAIZ, 'src', 'Clivus.Core', 'Translations', codigo + '.json')


def ler(codigo):
    with open(caminho(codigo), encoding='utf-8-sig') as f:
        return json.load(f)


def gravar(codigo, catalogo):
    with open(caminho(codigo), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(dict(sorted(catalogo.items())), f, ensure_ascii=False, indent=1)
        f.write('\n')


def destino_do_faltam(args):
    """O caminho do --faltam, conferido antes de gravar.

    Ele GRAVA no caminho que recebe: em 30/10/2026 uma revisão passou os
    fontes ali e nove .cs viraram "[]". Só aceita um .json fora de src/,
    tests/ e plano/, e só sobrescreve uma lista gravada por ele mesmo.
    """
    if len(args) != 2:
        sys.exit('uso: python tools/traducoes.py --faltam X.json (um caminho só)')

    alvo = os.path.abspath(args[1])
    if not alvo.lower().endswith('.json'):
        sys.exit(f'--faltam só grava em .json: {args[1]}')

    for pasta in ('src', 'tests', 'plano'):
        if alvo.lower().startswith((os.path.join(RAIZ, pasta) + os.sep).lower()):
            sys.exit(f'--faltam não grava dentro de {pasta}/ do repositório: {args[1]}')

    if os.path.exists(alvo):
        try:
            with open(alvo, encoding='utf-8-sig') as f:
                anterior = json.load(f)
        except (OSError, ValueError):
            anterior = None
        if not isinstance(anterior, list) or not all(isinstance(x, str) for x in anterior):
            sys.exit(f'--faltam não sobrescreve {args[1]}: o arquivo existe e não é uma lista do --faltam')

    return alvo


def main(args):
    if args[:1] == ['--faltam']:
        destino = destino_do_faltam(args)

    frases = frases_do_codigo()
    catalogos = {c: ler(c) for c in ('en', 'es')}

    if args[:1] == ['--faltam']:
        faltam = sorted(f for f in frases if any(not catalogos[c].get(f) for c in catalogos))
        with open(destino, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(faltam, f, ensure_ascii=False, indent=1)
        print(f'{len(faltam)} frase(s) gravadas em {args[1]}')
        return

    if args[:1] == ['--juntar']:
        with open(args[1], encoding='utf-8-sig') as f:
            novas = json.load(f)
        for frase, traducoes in novas.items():
            for c in catalogos:
                if traducoes.get(c):
                    catalogos[c][frase] = traducoes[c]
        for c, cat in catalogos.items():
            gravar(c, cat)
        print(f'{len(novas)} frase(s) juntadas')
        return

    if args[:1] == ['--limpar']:
        for c, cat in catalogos.items():
            gravar(c, {k: v for k, v in cat.items() if k in frases})
        print('catálogos limpos')
        return

    for c, cat in catalogos.items():
        faltam = sum(1 for f in frases if not cat.get(f))
        sobram = sum(1 for k in cat if k not in frases)
        print(f'{c}: {len(frases)} frase(s) no código, {faltam} sem tradução, {sobram} sobrando')


if __name__ == '__main__':
    main(sys.argv[1:])
