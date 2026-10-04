# Segurança do Clivus Solar

Pedido do Renan (04/10/2026): "planeje tudo o que for possível dentro do bom
senso sobre segurança e ataque cibernético". Este é o plano para o produto
inteiro: o plugin e o instalador (agente do plugin) e o servidor, o site, as
contas e a VPS (agente do servidor). Os dois leem; mudança se combina no
`CANAL.md`.

Bom senso aqui quer dizer: proteger de verdade o que machuca se vazar (dados
dos clientes, contas, a chave que assina licenças, a VPS) e não gastar com o
que não muda o resultado (ofuscar a DLL contra quem quer piratear um plugin
gratuito, por exemplo).

## 1. O que proteger, do mais grave para o menos

1. **A VPS e o Coolify.** Hoje dividem máquina com os serviços do Squeeze: quem
   entra num pode alcançar o outro.
2. **A chave privada que assina licenças.** Com ela, qualquer um emite licença.
3. **As contas** (e-mail, senha) e, mais tarde, pagamento.
4. **Os projetos dos clientes** que chegam ao servidor 3D: terreno, posição e
   desenho de uma usina são informação comercial sigilosa (onde o cliente vai
   construir antes de anunciar).
5. **O instalador e o plugin** que as pessoas baixam: se alguém trocar o
   arquivo, instala código malicioso dentro do AutoCAD de cada cliente
   (ataque à cadeia de suprimentos).
6. **Os repositórios no GitHub** (código, `CANAL.md`, histórico).

## 2. Ataques que valem a pena considerar

| Ataque | Onde | Defesa |
|---|---|---|
| Senha fraca/vazada, força bruta no login | site | argon2id, limite de tentativas, 2FA opcional, aviso de login novo |
| Cadastro em massa por robô | site | limite por IP, confirmação de e-mail, captcha simples só se precisar |
| Roubo de sessão, XSS, CSRF | site | cookies `HttpOnly`/`Secure`/`SameSite`, CSP, escapar tudo que vem do usuário |
| Ler a usina de outra pessoa pelo link | 3D | id aleatório longo (≥ 128 bits), expiração, link privado por padrão |
| JSON malicioso, "bomba" de gzip | API 3D | limite comprimido E descomprimido, validação estrita do formato |
| Título/nome do desenho com script | página 3D | escapar no HTML; o plugin já escapa no 3D local |
| Licença forjada ou copiada | plugin | assinatura ECDSA, máquina na licença, revalidação, revogação |
| Vazamento da chave privada | servidor | só em segredo do Coolify, cópia offline cifrada, rotação por `kid` |
| Instalador trocado | download | HTTPS, hash SHA-256 publicado, assinatura de código (quando comprar) |
| Plugin abrindo link falso | plugin | só abrir link do mesmo endereço do servidor configurado, em HTTPS |
| Entrada na VPS por SSH ou painel | infra | SSH só por chave, sem root por senha, painel do Coolify fechado, 2FA |
| Banco exposto na internet | infra | Postgres só na rede interna do Docker, nunca porta pública |
| Dependência com falha conhecida | todos | versões travadas, `pip-audit`, `dotnet list package --vulnerable`, Dependabot |
| Segredo no git | repos | nada de chave no código nem no canal; varredura de segredos no GitHub |
| Perda de dados (ransomware, disco) | infra | backup diário cifrado fora da VPS, teste de restauração |

## 3. Plugin e instalador (agente do plugin)

Já feito:
- Nenhum segredo no código: o plugin só tem a chave **pública** da licença.
- Respostas do servidor conferidas (link http/https absoluto, erro como texto).
- Página 3D local escapa título e `</script>`; three.js embutida (sem CDN).
- Envio com tempo máximo (90 s) e chave por variável de ambiente.

A fazer agora:
- **Só HTTPS** para servidor e licenças (http só em `localhost`, para teste).
- **Só abrir o link do próprio servidor**: o plugin confere que a `url`
  devolvida é do mesmo host do servidor, em HTTPS, antes de abrir no
  navegador (contra um servidor comprometido mandando para phishing).
- **Licença com `kid`**: o plugin aceita mais de uma chave pública, para o
  servidor trocar de chave sem deixar ninguém sem licença.
- **Endereço do servidor embutido** na versão de produção (a variável de
  ambiente fica só para teste): menos chance de alguém apontar o plugin para
  um servidor falso.
- **Instalador por usuário, sem pedir administrador**; não baixa a
  segurança do AutoCAD (nada de `SECURELOAD = 0`; quando houver certificado,
  o bundle assinado carrega sem perguntar).
- **Hash SHA-256 do instalador** gerado no build, para a página de download
  publicar ao lado do link.
- **Registro de diagnóstico sem dado sensível** (sem licença inteira, sem
  coordenada de cliente).
- **Privacidade da usina**: propor no canal que a cena publicada vá sem a
  `origem` absoluta (o 3D não precisa; a posição real no mapa fica no PC).

Não vale o esforço agora: ofuscação forte da DLL (.NET se lê de qualquer
jeito; a licença assinada já impede o que importa), anti-depuração.

Quando houver dinheiro em jogo: certificado de assinatura de código (em nome
da empresa) para o instalador e a DLL.

## 4. Servidor, site e contas (agente do servidor)

- **HTTPS** em tudo, HSTS; nada em http além do redirecionamento.
- **Contas**: senha com argon2id (ou bcrypt); confirmação de e-mail;
  recuperação por link de uso único que vence em 30 min; mesma resposta para
  e-mail que existe e que não existe (não revela quem é cliente); 2FA (TOTP)
  opcional; aviso por e-mail em login de máquina nova.
- **Limites de taxa**: login, cadastro, recuperação, ativar licença e publicar
  cena (por IP e por conta).
- **API 3D**: chave/conta obrigatória para publicar; corpo até 50 MB
  descomprimido (contar ao descomprimir, não confiar no cabeçalho); validar
  o formato campo a campo; ids aleatórios de pelo menos 128 bits (o `k3f9x2`
  do contrato é só exemplo); cena vence (30 dias) e é apagada; o dono pode
  apagar antes; link privado por padrão.
- **Licenças**: código com pelo menos 60 bits de acaso, guardado como hash no
  banco; chave privada só em segredo do Coolify, com `kid`; cópia offline
  cifrada da chave (fora da VPS); registro de cada ativação (conta, máquina,
  IP, data); revogar código e liberar máquina pela conta.
- **Cabeçalhos**: CSP (sem script de fora; a página 3D com a three.js
  servida pelo próprio site), `X-Content-Type-Options: nosniff`,
  `Referrer-Policy`, `frame-ancestors 'none'`, CORS só para o próprio domínio.
- **LGPD**: termo de uso e política de privacidade na landing page; coletar
  só o necessário (nome, e-mail, empresa); apagar a conta e os dados a pedido;
  dizer quanto tempo a usina publicada fica guardada.
- **Logs**: sem senha, sem licença, sem corpo de cena; guardar acessos e
  erros por 90 dias.

## 5. VPS e Coolify (agente do servidor, com o Renan)

- SSH só por chave, `PermitRootLogin prohibit-password` (ou um usuário com
  sudo), `PasswordAuthentication no`, fail2ban.
- Firewall: abertas só 80/443 (e 22, de preferência restrita); o painel do
  Coolify só por túnel/IP do Renan, com 2FA (as notas antigas dizem que as
  portas 8000/6001/6002 já estão fechadas para a internet: conferir).
- Atualizações automáticas de segurança do Ubuntu; Coolify atualizado.
- Clivus e Squeeze separados: redes Docker próprias, sem volume compartilhado,
  limites de CPU/memória, segredos separados. A seguir, avaliar uma VPS só
  para o Clivus quando houver clientes.
- Postgres sem porta pública; senhas fortes e únicas por serviço.
- Backup diário do banco, cifrado, fora da VPS; restauração testada uma vez
  por mês.
- Monitoramento de "no ar" (o `/api/v1/saude`) e de disco cheio.

## 6. GitHub (o Renan)

- 2FA na conta; repositórios privados; proteção da branch `main` no
  repositório do servidor (deploy só do que passou nos testes).
- Varredura de segredos e Dependabot ligados.
- Chaves de deploy do Coolify só de leitura.

## 7. Se acontecer um incidente

1. Tirar do ar o que vazou (desligar o serviço, revogar a chave do Coolify).
2. Trocar todos os segredos (chave de licença com `kid` novo, senhas do banco,
   tokens), revogar sessões.
3. Avaliar o que vazou; se houver dado pessoal, avisar os usuários e a ANPD
   (LGPD: em prazo razoável, a orientação é 3 dias úteis).
4. Registrar no `CANAL.md` o que aconteceu e o que mudou.

## 8. Ordem

**Antes do primeiro link público (fase 1, só o Renan usa):** HTTPS; chave
obrigatória para publicar; ids longos; limites de tamanho; VPS endurecida;
plugin só HTTPS e só abre link do próprio servidor.

**Antes de abrir o cadastro (fase 2):** contas com argon2id, e-mail
confirmado, limites de taxa, LGPD (termo e política), licenças com `kid`,
backup e restauração testada, hash do instalador na página.

**Antes de cobrar (fase 3):** certificado de assinatura de código, VPS própria,
2FA para todos, revisão de segurança por terceiro (teste de invasão leve).
