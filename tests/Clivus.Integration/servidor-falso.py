"""Servidor 3D falso para o nível 2 (plano/contrato-servidor-3d.md).

Segue o contrato no que o plugin usa: GET /api/v1/saude, POST /api/v1/cenas
com Bearer e gzip. Guarda o último corpo recebido (já descomprimido) no
arquivo dado e responde 201 com o link. Uso:

    python servidor-falso.py PORTA CHAVE ARQUIVO_DO_CORPO
"""
import gzip
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

PORTA = int(sys.argv[1])
CHAVE = sys.argv[2]
SAIDA = sys.argv[3]
contador = 0


class Tratador(BaseHTTPRequestHandler):
    def _responder(self, status, corpo):
        dados = json.dumps(corpo, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(dados)))
        self.end_headers()
        self.wfile.write(dados)

    def do_GET(self):
        if self.path == '/api/v1/saude':
            self._responder(200, {'ok': True})
        else:
            self._responder(404, {'erro': 'não existe'})

    def do_POST(self):
        global contador
        if self.path != '/api/v1/cenas':
            self._responder(404, {'erro': 'não existe'})
            return

        if self.headers.get('Authorization') != f'Bearer {CHAVE}':
            self._responder(401, {'erro': 'chave inválida'})
            return

        bruto = self.rfile.read(int(self.headers.get('Content-Length', '0')))
        if self.headers.get('Content-Encoding') == 'gzip':
            bruto = gzip.decompress(bruto)

        try:
            corpo = json.loads(bruto.decode('utf-8'))
            assert corpo['versao'] == 1 and isinstance(corpo['cena']['faces'], list)
        except Exception as erro:  # noqa: BLE001 - qualquer coisa fora do contrato é 400
            self._responder(400, {'erro': f'corpo fora do contrato: {erro}'})
            return

        with open(SAIDA, 'wb') as f:
            f.write(bruto)

        contador += 1
        ident = f'teste{contador}'
        self._responder(201, {'id': ident, 'url': f'http://127.0.0.1:{PORTA}/3d/{ident}', 'expira_em': '2026-11-03T12:00:00Z'})

    def log_message(self, *args):
        pass


HTTPServer(('127.0.0.1', PORTA), Tratador).serve_forever()
