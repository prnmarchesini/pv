"""Servidor falso para o nível 2: o 3D (plano/contrato-servidor-3d.md) e as
licenças (plano/contrato-ativacao.md).

Segue o contrato no que o plugin usa: GET /api/v1/saude, POST /api/v1/cenas
com Bearer e gzip. Guarda o último corpo recebido (já descomprimido) no
arquivo dado e responde 201 com o link. Uso:

    python servidor-falso.py PORTA CHAVE ARQUIVO_DO_CORPO [CHAVE_PRIVADA.pem]
    python servidor-falso.py --gerar-chave CHAVE_PRIVADA.pem   (imprime a pública)
    python servidor-falso.py --licenca CHAVE_PRIVADA.pem MAQUINA  (imprime uma licença válida)

Licenças: o código "CLV-TESTE-0001" ativa (kid "teste"); qualquer outro dá
404 "código não encontrado".
"""
import base64
import datetime
import gzip
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

if sys.argv[1] == '--gerar-chave':
    privada = ec.generate_private_key(ec.SECP256R1())
    with open(sys.argv[2], 'wb') as f:
        f.write(privada.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
    publica = privada.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    print(base64.b64encode(publica).decode())
    sys.exit(0)

PRIVADA = None
if sys.argv[1] != '--licenca':
    PORTA = int(sys.argv[1])
    CHAVE = sys.argv[2]
    SAIDA = sys.argv[3]
    if len(sys.argv) > 4:
        with open(sys.argv[4], 'rb') as f:
            PRIVADA = serialization.load_pem_private_key(f.read(), password=None)
contador = 0


def b64url(dados):
    return base64.urlsafe_b64encode(dados).rstrip(b'=').decode()


def licenca(maquina):
    agora = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0)
    iso = lambda d: d.strftime('%Y-%m-%dT%H:%M:%SZ')  # noqa: E731
    payload = json.dumps({
        'v': 1, 'kid': 'teste', 'licenca': 'lic_teste', 'conta': 'teste@clivus', 'plano': 'gratuito', 'maquina': maquina,
        'emitida_em': iso(agora), 'revalidar_em': iso(agora + datetime.timedelta(days=30)), 'expira_em': iso(agora + datetime.timedelta(days=45)),
    }, separators=(',', ':')).encode()
    r, s = decode_dss_signature(PRIVADA.sign(payload, ec.ECDSA(hashes.SHA256())))
    return b64url(payload) + '.' + b64url(r.to_bytes(32, 'big') + s.to_bytes(32, 'big'))


if sys.argv[1] == '--licenca':
    # A licença de toda a rodada do nível 2: o plugin Debug confere com a
    # chave de teste e os comandos não são barrados.
    with open(sys.argv[2], 'rb') as f:
        PRIVADA = serialization.load_pem_private_key(f.read(), password=None)
    print(licenca(sys.argv[3]))
    sys.exit(0)


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
        if self.path in ('/api/v1/licencas/ativar', '/api/v1/licencas/revalidar'):
            corpo = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))).decode('utf-8'))
            if self.path.endswith('ativar') and corpo.get('codigo') != 'CLV-TESTE-0001':
                self._responder(404, {'erro': 'código não encontrado'})
                return
            self._responder(200, {'licenca': licenca(corpo['maquina'])})
            return

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
