# Auditoria de acabamento e qualidade — Crônicas

Data: 12/11/2026. Escopo: código e ambiente local; nenhuma alteração em produção. As alterações preexistentes em SQL de release e capturas visuais foram preservadas.

## Fase 1 — diagnóstico

Arquitetura: SPA Angular 21/TypeScript/RxJS; API ASP.NET Core 10 com módulos Domain/Application/Infrastructure, EF Core/Npgsql/PostgreSQL; SignalR e WebRTC; Cloudflare Pages/Worker → Render → Neon. Cookies próprios de sessão, SMTP transacional, imagens externas por URL HTTPS. Sem upload de arquivos ao servidor, publicidade, analytics, pagamentos, PWA ou service worker identificados. Não há AGENTS.md aplicável no repositório.

| Área | Situação inicial / evidência | Prioridade e tratamento |
| --- | --- | --- |
| Branding | SVG e cartão social PNG/SVG existentes; faltam PNGs de favicon e Apple Touch | P2: completar derivados do símbolo existente |
| SEO | index.html possui metadados/canonical/OG/X e robots/sitemap; todas as rotas mantêm o título da home; middleware só adiciona noindex HTTP | P1: metadados por rota, remover canonical privado, noindex inclusive HTML e previews |
| Erros | 404 Angular existe; fallback de boot e erro de carregamento de chunks ausentes; desconhecidas retornam 200 | P1: 404 HTTP no host operacional, 500 controlado, recuperação de navegação/boot |
| Privacidade | Não há documentos/rodapé; exportação e correção de perfil já existem; somente cookie essencial `chronicles_session` | P1: páginas técnicas transparentes, cookies e controles reais; validar identificação/canal/bases legais/retenção antes do lançamento |
| Autenticação | Cookies HttpOnly/Secure/Lax, token criptográfico com hash no banco, expiração de 14 dias; recuperação com hash, 15 min e revogação; limites por IP/conta | P1: sessão expirada/falha transitória no frontend, limitar entradas de login; enumeração no cadastro exige decisão de produto |
| Autorização | Backend aplica políticas por recurso/conta/papel; testes de aplicação/persistência/HTTP existentes | Preservar e executar regressões; auditoria não é prova exaustiva de ausência de IDOR |
| UX | Muitos estados de erro/vazio/loading, preservação de rascunho e conflitos já existem nas fichas; cópia sem feedback, campanha perde filtros/página, envio depende só de botão desabilitado | P1/P2: corrigir fluxos verificáveis e testar comportamento |
| Acessibilidade | Foco visível e layout responsivo existentes; falta skip link, anúncio de navegação, aria-current em menus, redução de movimento | P1/P2: correções transversais e validação navegador/teclado/telas pequenas |
| Rede | Mensagem 401 sempre diz credenciais inválidas; erros de servidor podem repassar mensagens internas; HTTP sem timeout; busca aceita resposta antiga | P1: padronizar erros, timeout REST, cancelamento e sessão |
| Segurança HTTP | CORS explícito/checagem de Origin e rejeição de formulários; headers/CSP ausentes; API pode ser cacheada fora do proxy | P1: headers em hosts/API, proteger documentos privados contra cache e referências externas |
| Dados | EF parametriza consultas; Angular interpola texto, sem bypass de sanitização encontrado; log HTTP usa template de rota sem tokens/query | Preservar; retenção/backup/limpeza de sessões e códigos expirados pendentes de operação |
| Emails | Texto simples, assunto e código existentes; sem versão HTML; SMTP opcional e falha registrada sem expor código | P2: multipart HTML/texto; SPF/DKIM/DMARC e entrega real dependem do remetente |
| Performance | Hashing, budgets Angular e lazy loading de áreas pesadas existentes; Google Fonts carrega na entrada; sem medidas de CWV de usuários reais | Evitar reestruturação; avaliar privacidade/fontes e manter cache de bundles com hash |
| Manutenção | README/env/CI, lint/typecheck/build e suites .NET/Node/HTTP já existem | Acrescentar verificação das correções ao CI e registrar somente comandos executados |

## Fase 2 — identidade, navegação e páginas públicas

- Mantido o favicon SVG e o cartão social existentes. Gerados PNGs de 16/32/48 px, Apple Touch Icon de 180 px e ICO com os três tamanhos. `node web/generate-brand-icons.mjs` permite regenerar os derivados com o Playwright/Chrome já utilizados no projeto, sem dependência nova.
- Títulos, descriptions, OG e X específicos por rota; rótulos nunca incorporam nomes de campanha, buscas ou tokens. O middleware serve esses metadados antes de executar JavaScript, e o Angular os atualiza durante a navegação.
- Canonical/OG URL e WebSite structured data ficam restritos à home. Contas, documentos legais em validação, campanhas, convites, OBS e previews recebem noindex no HTML e por header. Sitemap e robots existentes foram mantidos: somente a home é indexável.
- URLs desconhecidas retornam 404 no Pages e no Worker alternativo; a interface 404 existente foi preservada. O antigo catch-all de campanha, que mostrava um módulo fictício em preparação para qualquer seção, foi removido. `/error` serve a página 500. `/index.html` e barras finais de rotas conhecidas são redirecionadas para evitar cópias.
- Rodapé com ano dinâmico e links reais para privacidade, termos e cookies. Não é inserido em fontes OBS.

## Fase 3 — UX, acessibilidade e privacidade

- Páginas `/privacy`, `/terms` e `/cookies` acessíveis sem login, com aviso explícito de validação pendente. Descrevem dados, permissões, cookie de sessão de 14 dias, serviços de hospedagem, fontes/imagens externas, SMTP, WebRTC/STUN, links OBS, exportação, correção do perfil e encerramento de sessões. Não prometem exclusão automática ou um canal de contato inexistente.
- Nenhum banner foi acrescentado: o código não integra cookies de analytics/publicidade. Preferências de cor continuam no perfil. Não houve introdução de PWA, manifesto, service worker ou tratamento fictício de consentimento.
- Avisos próximos ao cadastro, avatar, exportação de dados, transmissão e fontes OBS. A cópia de convites/OBS anuncia sucesso ou orienta a cópia manual quando o navegador nega acesso à área de transferência. Rotação de URL já gerada exige confirmação.
- Skip link, foco no conteúdo após navegação, aria-current, nomes acessíveis na sidebar recolhida, fechamento de menus com Escape/restauração de foco, mensagens de estado e respeito a reduced motion. Rolagem volta ao topo nas novas páginas e é restaurada ao usar Voltar.
- Cinco cores secundárias/placeholder apresentavam contraste entre 3,40:1 e 4,41:1 nos fundos relevantes. Foram substituídas pelo token muted; o par muted/surface mede 6,03:1. Bordas de campos e foco de teclado foram reforçados; as medições no navegador verificam texto ≥4,5:1 e borda ≥3:1 nos elementos selecionados. Isso não certifica todos os renderizadores como WCAG AA.
- Correção do link longo de convite a 320 px, com coluna que pode encolher e quebra de texto. Home, cadastro, privacidade e campanha foram exercitados de 320 a 2560 px.
- Busca/página de campanhas ficam na URL, incluindo retorno pelo histórico. Requisições antigas de campanhas e pesquisa são canceladas e timers/subscriptions são encerrados. Estados de carregamento/vazio de universos/artigos foram separados.
- Formulários principais impedem reenvio durante a operação; emails têm limite, validação Angular e normalização de espaços. Senhas preservam espaços e têm limite de 128 caracteres. Não há retry automático de escritas.
- Campanhas, perfil, sistemas, registros narrativos e artigos de universo confirmam saída com rascunho pendente; fichas passaram a considerar também notas secretas não salvas. Cancelar a saída mantém o conteúdo. A proteção de NPCs existente foi preservada.
- Corrigido um erro real de horário: editar uma sessão usava `slice()` sobre UTC como hora local e, ao salvar, deslocava o evento. A conversão agora usa o fuso do navegador. Chrome em America/Sao_Paulo verificou que 21:30Z aparece como 18:30 e permanece 21:30Z após salvar.

## Fase 4 — segurança e robustez

- Sessão expirada é diferenciada de senha incorreta. Um 401 em operação autenticada invalida o estado local e mostra aviso sem redirecionar nem descartar o rascunho. Falha 5xx/rede ao verificar a conta não é memorizada como logout. Verificações simultâneas de sessão compartilham a requisição.
- Timeout REST de 90 segundos, compatível com o wake-up do Render Free. Mensagens de timeout/5xx orientam conferir o resultado antes de repetir uma escrita. Erros 5xx nunca repassam detail/message do backend para o usuário.
- Recuperação de falha de navegação/chunk e de boot independente dos bundles Angular. Recarregamento é manual e pede confirmação sobre rascunhos; não existe loop automático de reload.
- CSP sem scripts inline/eval, nosniff, proteção de framing, referrer-policy no-referrer e Permissions-Policy que preserva câmera/microfone/tela do próprio site. HSTS é enviado pelo middleware dos hosts Cloudflare. API recebe no-store e noindex inclusive para respostas rejeitadas. Nginx local possui política equivalente de conteúdo e revalidação do HTML.
- O primeiro teste visual mostrou que a otimização de CSS do Angular gerava um onload inline bloqueado pela CSP. `inlineCritical` foi desativado e o CSS permanece minificado. A otimização de fontes durante build foi desativada: o build não precisa buscar Google Fonts. As fontes da interface continuam externas em runtime, conforme a página de privacidade informa.
- Fetch Metadata complementa a checagem de Origin/SameSite: escritas cross-site sem origem confiável são bloqueadas. A allowlist CORS explícita continua autorizada; clientes não navegador continuam compatíveis. CORS, cookies e autorização no backend não foram substituídos por verificações de UI.
- Login limita o tamanho de entrada antes de calcular hash e atualiza hash antigo quando o hasher solicita. Contratos de cadastro/login, persistência e esquema foram mantidos.
- Redefinição de senha invalida os demais códigos pendentes na mesma transação, além de revogar sessões. O teste PostgreSQL verifica senha persistida, replay e rejeição de outro código ainda não usado.
- Email de recuperação passa a incluir versões texto e HTML UTF-8, com identidade existente, código escapado, prazo e orientação de uso. Sem contatos, links comerciais ou remetentes inventados.

## Arquivos principais

- Branding/build: `web/src/index.html`, `web/public/favicon*`, `web/public/apple-touch-icon.png`, `web/public/boot-recovery.js`, `web/angular.json`, `web/generate-brand-icons.mjs`.
- Metadados/hosts: `web/src/app/core/seo/`, `web/functions/_middleware.ts`, `web/security-headers.mjs`, `web/fallback-worker.mjs`, `web/nginx.conf`.
- Interface: `web/src/app/app.component.ts`, `web/src/app/layouts/`, `web/src/app/features/legal/`, `web/src/app/features/errors/`, `web/src/app/routing/routes.ts`, `web/src/app/views.ts`, `web/src/styles.scss`, editores/contas/busca/convites em `features/` e helpers em `core/`.
- API: `api/Chronicles.Api/Program.cs`, `Features/Identity/HttpResponseProtection.cs`, `Services/PasswordRecoveryService.cs`.
- Validação: `scripts/quality.test.mjs`, `scripts/quality-preview.mjs`, `web/quality-browser.mjs`, `web/character-companions-integration.mjs`, `tests/Chronicles.Rules.Tests/HttpQualityTests.cs`, `tests/Chronicles.Persistence.Tests/IdentityRecoveryTests.cs`, scripts npm e `.github/workflows/ci.yml`.

## Limites e decisões pendentes

- Controlador (nome/razão social, identificação/endereço aplicáveis), contato de privacidade, responsável, bases legais por finalidade, retenção/backup, transferências internacionais e procedimento de exclusão: não constam no repositório. Documentos publicados nesta etapa precisam explicitar essa pendência, sem inventar contato ou prometer atendimento inexistente.
- Exclusão de conta requer política de destino de campanhas compartilhadas/autoria/histórico; não executar cascata destrutiva sem esse desenho. Exportar dados e editar perfil já são possíveis.
- Não adicionar banner de consentimento/PWA sem necessidade. Novos rastreadores exigirão nova análise e bloqueio até escolha, conforme [guia da ANPD](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia_orientativo_cookies_e_protecao_de_dados_pessoais).
- Referência de proteção CSRF: [OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html). JSON, origem e cookie SameSite precisam funcionar em conjunto, incluindo proxies e WebSockets.
- Não certificar conformidade LGPD/WCAG integral, desempenho de produção, OBS/WebRTC real, infraestrutura ou CI remoto a partir de testes locais.

### Pendências priorizadas e justificativas

| Prioridade | Pendência | Por que não foi automatizada |
| --- | --- | --- |
| P0, antes da abertura pública | Validar documentos e fornecer identidade/canal do controlador, bases legais, retenção, transferências e procedimento de direitos | Informações jurídicas/empresariais ausentes; inventá-las seria incorreto. As páginas permanecem em validação e noindex |
| P1 | Definir exclusão de conta e destino de campanhas, autoria, notas, histórico e backups | Cascata poderia apagar conteúdo de outros participantes. Exige política de produto e desenho de retenção; não é um pequeno ajuste seguro |
| P1 | Reduzir enumeração no cadastro e avaliar diferenças de tempo de autenticação/recuperação | Cadastro retorna 409 para email existente e não verifica propriedade de email. Ocultar o conflito mantendo criação/login automático não resolve a enumeração; exige fluxo de verificação e contratos próprios. Mensagens de login/recuperação são genéricas, mas não há promessa de tempo constante |
| P1 | Validar backup/restauração, retenção/limpeza de sessões/códigos, regiões e acesso a logs | Não há política nem autorização para alterar dados/infraestrutura de produção. Expirar acesso não exclui automaticamente registros antigos |
| P1 | Validar SMTP, remetente e SPF/DKIM/DMARC e testar entrega/compatibilidade em clientes reais | Nenhum domínio/remetente real fornecido. Testados conteúdo/multipart e segurança da recuperação, não entrega externa |
| P2 | Testes manuais com leitores de tela, zoom real de 200%, Firefox/Safari e renderizadores RPG completos | Chrome/teclado/contraste selecionado/viewports não substituem esses ambientes nem uma auditoria integral WCAG 2.2 AA |
| P2 | Medir CWV/LCP/INP/CLS de usuários reais, carga, cenários de múltiplas instâncias e CVEs das imagens base Docker | Não há telemetria de campo/carga/scanner de imagem configurados. Auditorias de pacotes não certificam o sistema operacional das imagens |
| P2 | Avaliar hospedagem local das fontes e revisão das URLs de imagens de terceiros | Fontes atuais preservadas; provedores externos estão informados. Mudanças de distribuição exigem preservar subconjuntos/licenças e validar renderização |
| P2 | Ampliar idempotência no servidor e cancelamento/paginação URL em outros módulos | Dados/chat e fluxos versionados já possuem proteções. Não foi alterado o contrato de todas as escritas; timeout não garante que uma gravação falhou. A UI avisa para conferir o resultado |

### Não aplicável nesta versão

Sem analytics/publicidade integrados que justifiquem banner, modo offline/PWA solicitado, pagamentos, campanhas de email promocional, upload remoto de arquivos ou flag operacional de manutenção. Importação local de JSON é um recurso existente e distinto de upload ao servidor. Não foram inventados redes sociais, suporte, instituição, moeda comercial ou schema de Organization/avaliações. O Nginx do Compose continua um host de desenvolvimento com fallback SPA; status 404/metadados por HTTP foram implementados nos hosts Cloudflare de produção.

## Fase 5 — validação

Ambiente: Windows, Node 24.11.1, Chrome instalado via Playwright, SDK .NET 10.0.402 do perfil, PostgreSQL 17 em contêiner exclusivo `cronicas-quality-db` na porta 5545. API local em 5099 e preview do build de produção em 8082. O preview usa o middleware Pages real para HTML/headers e encaminha REST ao backend local; não simula persistência. WebSockets foram testados diretamente na API local.

| Comando executado | Resultado real |
| --- | --- |
| `npm --prefix web run lint` | Passou |
| `npx tsc --noEmit -p tsconfig.app.json` em `web` | Passou |
| `npm --prefix web run build` | Passou; bundle inicial de 573,15 kB, transferência estimada de 137,32 kB; dentro dos budgets de 750 kB/1 MB |
| `node --test scripts/architecture.test.mjs scripts/pages-proxy.test.mjs scripts/spa-routes.test.mjs scripts/proxy-identity.test.mjs scripts/quality.test.mjs` | 31 testes passaram; nove novos testes de qualidade |
| `npm run test:plugins` | Catálogos sincronizados; quatro templates e fórmulas/rolagens representativas válidos |
| `dotnet build api/Chronicles.Api/Chronicles.Api.csproj -c Release --no-restore` | Passou com o SDK do perfil; a API também foi recompilada pela suíte final de regras |
| `dotnet run --project tests/Chronicles.Rules.Tests -c Release` | Passou; inclui os novos probes HTTP/CORS/cache e email multipart |
| `dotnet run --project tests/Chronicles.Architecture.Tests -c Release` | Passou: aplicação, políticas e fronteiras |
| `dotnet run --project tests/Chronicles.Persistence.Tests -c Release` com `ARCHITECTURE_TEST_DB` local | Passou: cadeia de migrations, upgrade/rollback, concorrência/IDOR/outbox e nova recuperação de senha. A suíte criou e removeu seu próprio banco temporário |
| `SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/smoke.mjs` | Passou: contas, campanhas, sistemas, fichas/coleções, NPCs, universos/relações, pesquisa, convites, narrativa, dados, chat, encontros, OBS, permissões e logout |
| `SEO_BASE_URL=http://127.0.0.1:8082 node scripts/seo-smoke.mjs` | Passou: home/metadados/sitemap/robots/rotas privadas/imagem social |
| `SMOKE_BASE_URL=http://127.0.0.1:5099 node scripts/room-smoke.mjs` | Passou com WebSockets reais: sinalização, autorização, espectador e revogação |
| `node web/quality-browser.mjs` | Passou: Chrome, 320/390/768/1440/2560 px, 404/500, teclado/reduced motion, contraste selecionado, falha de sessão/rede, envio duplo (uma requisição), dados preservados, filtros/Voltar, copiar link, IDOR, fuso horário, rascunhos em sistemas/universos, offline e JSON inválido. Timeout validado com relógio virtual de 90 s; sem esperar 90 s reais. Sem erros JS não capturados ou violações CSP nos percursos |
| `node web/character-companions-integration.mjs` | Passou: favoritos/reload, conflito/rascunho privado, bloqueio de saída, gravação, OBS anônimo, rotação/revogação e isolamento do jogador |
| `npm audit --json` na raiz e em `web`; `npm audit --omit=dev --json` em `web` | Zero vulnerabilidades sinalizadas pelos advisories consultados |
| `dotnet list api/Chronicles.Api/Chronicles.Api.csproj package --vulnerable --include-transitive --format json` | Nenhum pacote sinalizado pela consulta ao NuGet |
| `npx wrangler pages functions build web/functions --outdir .quality-local/pages` | Worker compilado; nenhum deploy |
| `git diff --check` | Sem erros de whitespace; Git avisou sobre normalização LF/CRLF em arquivos existentes |

Houve falhas intermediárias reais: `dotnet` do PATH não tinha SDK; processos Node/Chrome foram bloqueados por EPERM no sandbox e repetidos com autorização; o teste novo de persistência precisou de referência ao assembly da API; a própria API de teste bloqueou o DLL em uma recompilação Windows e foi encerrada antes da repetição; seletores/expectativas da suíte browser foram ajustados ao shell e viewport reais. O teste visual detectou o conflito de CSP/CSS descrito acima, que foi corrigido. Não se contabiliza nenhuma dessas tentativas como aprovação de teste.

Capturas novas em [visual-validation/quality/README.md](visual-validation/quality/README.md). A comparação visual foi feita sobre home, privacidade e campanha, preservando cores, tipografia e composição existentes. Não houve execução em produção, certificação de carga, teste externo de SMTP/DNS nem confirmação de CI remoto.

Ao encerrar, a API e o preview locais foram interrompidos e o contêiner exclusivo `cronicas-quality-db` foi removido. As fixtures da auditoria não ficaram em um banco de produção. Não foram adicionadas dependências de runtime nem migrações de schema.
