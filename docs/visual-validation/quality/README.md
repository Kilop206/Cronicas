# Acabamento — 12/11/2026

Capturas do Chrome headless, build Angular de produção e API/PostgreSQL locais. Contas, campanhas e links são fixtures descartáveis. A suíte está em `web/quality-browser.mjs`.

- Home: [390 px](home-390.png), [1440 px](home-1440.png).
- Privacidade: [390 px](privacy-390.png), [1440 px](privacy-1440.png).
- Cadastro: [390 px](register-390.png), [1440 px](register-1440.png).
- Campanha com convite/feedback e configurações: [390 px](campaign-390.png).

Os viewports de 320, 768 e 2560 px também foram verificados sem overflow horizontal nos fluxos selecionados, sem gerar capturas adicionais. Foco/teclado, menus, mensagens, formulários, rascunhos e falhas de rede possuem asserções no script. A suíte controla algumas respostas HTTP para simular 500/401/JSON inválido/timeout e controla clipboard; os percursos de conta, campanha, permissões, artigos, sessões e persistência usam a API real. O relógio virtual acelera o cenário de timeout.

As capturas não certificam WCAG integral, zoom real, navegadores diferentes ou transmissão de mídia em OBS. As capturas preexistentes em `architecture/` e `rules/` não fazem parte desta alteração visual.

## Convites ativos — 23/11/2026

[Celular](campaign-invites-managed-mobile.png) e [desktop](campaign-invites-managed-desktop.png): painel dos assets publicados no Worker, com API sintética no navegador. Mostram dois convites ativos e a ação de desativar; não representam campanhas reais. A varredura completa de 384 casos e os testes PostgreSQL/HTTP passaram no [CI](https://github.com/Kilop206/Cronicas/actions/runs/45279812691). Consulte os limites e destinos no [registro do release](../../architecture/releases/2026-11-23-invitation-management.md).
