# Textos longos e estados de interface — 16/11/2026

Continuação da revisão visual: nomes válidos com 80 caracteres sem espaços, mensagens de erro, listas vazias, paginação, formulários preenchidos, relações entre artigos e confirmação de exclusão em viewports de 360 e 560 px de altura.

## Correções

- Textos dinâmicos podem quebrar dentro das páginas, formulários de autenticação e avisos. Um nome longo em uma mensagem de erro deixa de alargar o login.
- O conteúdo das listas de personagens, inventário e habilidades pode encolher para a largura disponível, preservando as ações.
- Relações entre artigos podem ocupar mais de uma linha, com título, descrição da relação e botão Remover dentro do painel.
- O botão Tentar novamente tem altura mínima de 32 px no desktop; o limite de 44 px no celular continua aplicado.
- Cabeçalhos de seção mantêm distância dos botões mesmo quando a ação passa para a linha seguinte.
- No diálogo de exclusão, o corpo pode rolar enquanto as ações permanecem visíveis, inclusive após a resposta de erro. A região de conteúdo aceita foco para rolagem pelo teclado.

## Verificação

A auditoria passa de 66 para 89 telas/estados, em quatro larguras: 356 combinações. Além das verificações anteriores, mede texto cortado dentro dos painéis e a distância vertical entre títulos e ações. Diálogos nativos abertos são medidos isoladamente do conteúdo que fica atrás deles.

As novas respostas de API são sintéticas. Os nomes e rótulos respeitam os limites dos campos; notas privadas da fixture são normalizadas como no servidor. As falhas de salvamento e exclusão são simuladas, sem alterações em contas ou campanhas reais.

A matriz local completa passou nas 356 combinações, sem overflow horizontal da página, texto cortado nos painéis verificados, controles abaixo do limite, interseções entre controles, cabeçalhos encostados nas ações ou erros JavaScript. Build Angular, lint e regressões de autenticação, gestão de fichas e F&M também passaram.

Capturas selecionadas em `docs/visual-validation/quality/`: `long-login-error-mobile.png`, `long-login-error-desktop.png`, `unbroken-campaign-mobile.png`, `world-links-mobile.png` e `short-delete-error-mobile.png`.

As correções visuais foram implementadas em `7f14a2f`, `1a1b73e` e `fa4c8e6`. O [CI da versão conjunta com convites compartilhados](https://github.com/Kilop206/Cronicas/actions/runs/43976474134) passou também nas 356 combinações de interface e nos demais checks. A reabertura da confirmação volta ao título; a rolagem por teclado e a visibilidade das ações antes/depois do erro foram verificadas em 360/560 px de altura.

Publicação conjunta concluída em `985b534`, com os [convites compartilhados](2026-11-16-campaign-invitations.md): Pages `d0c74012.cronicas-bsy.pages.dev`, Worker `bbc50433-0fe5-4455-b017-6be3ddf785c9` e API Render `dep-dbtotltckfvc7392nte0`. A conferência pública validou versão/hash, início/login e estados simulados de erro/convite nas quatro larguras dos dois hosts, sem overflow ou erros JavaScript.
