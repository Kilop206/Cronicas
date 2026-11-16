# Revisão visual e responsiva — 15/11/2026

Implementação em `ba82665a805024ddf789097b2286297adc4b2725`; acabamento do login e ampliação da regressão em `ea2270f561d604a84b3683f79532230ba55a2d7d`.

## Correções

- Busca de campanhas com largura flexível, fonte própria e contador em uma nova linha quando necessário.
- Perfil com quebra de nome/email longos; painéis e formulário de encontros respeitando a largura disponível.
- Cards de campanha com rodapés alinhados e títulos que podem quebrar sem empurrar o layout.
- Controles das cinco fichas com altura mínima de 32 px em desktop e 44 px em telas pequenas/toque. Ajustes nos grids, recursos, atributos, testes de morte e botões de rolagem para acomodar os controles.
- Barra de edição da ficha em fluxo normal no celular, menus com alvos maiores e cabeçalhos recolhíveis com área de interação adequada.
- Campos com tamanho de fonte de 16 px em celulares, inclusive autenticação; arquivos marcados como `hidden` continuam ocultos ao abrir as ferramentas de importação.
- Recuperação de senha separada do botão Entrar por margem positiva; links de ação com alvo de 44 px no celular. A auditoria inclui a distância entre esses dois controles.
- Campos pessoais da ficha livre identificados por chave no `ngFor`. Isso evita recriação contínua dos controles e preserva a digitação/foco.

Tabelas extensas de perícias podem rolar dentro do painel; a página permanece dentro da largura da tela. As regras de impressão não recebem os tamanhos mínimos destinados à tela.

## Validação reproduzível

`npm --prefix web run test:visual:browser` serve a compilação de produção em loopback e usa respostas de API sintéticas. Não consulta nem altera campanhas pessoais.

A matriz cobre 66 telas/estados em 320, 390, 768 e 1440 px: entrada/cadastro/recuperação, páginas públicas, painel, campanhas, fichas livres e cinco sistemas em leitura/edição, NPCs/ameaças, encontros, universos, textos, pesquisa, perfil, preferências, convites, sala, navegação aberta e abas/ferramentas das fichas.

O teste verifica largura do documento, área mínima dos controles, interseções entre controles visíveis, erros JavaScript, importação oculta e digitação/foco da ficha livre. Conteúdo de `details` fechado e elementos destinados à impressão são excluídos da medição. Capturas e relatório JSON ficam em `.quality-local/visual-audit/`; `AUDIT_SCREENSHOTS=0` desativa apenas as imagens no CI. `AUDIT_FILTER` e `AUDIT_WIDTHS` permitem repetir um subconjunto.

As 264 combinações passaram, sem overflow horizontal da página, controles abaixo do limite medido, interseções entre controles ou erros JavaScript. A matriz completa passou localmente na implementação principal e no CI da versão final, incluindo a nova regressão do login. Build Angular, lint e regressões de autenticação, gestão de fichas e F&M também passaram. As capturas de referência de F&M e exclusão foram atualizadas.

O [CI da versão final](https://github.com/Kilop206/Cronicas/actions/runs/43776896020) concluiu com sucesso, incluindo regras dos cinco sistemas, arquitetura, persistência PostgreSQL e testes de integração.

## Publicação e conferência

Frontend publicado no [Pages](https://cronicas-bsy.pages.dev) e no [Worker alternativo](https://cronicas-fallback.guilhermextra118.workers.dev), a partir da compilação de `ea2270f`. O deploy Pages é `7972cb2e.cronicas-bsy.pages.dev` e a versão do Worker é `971fb257-dea5-4b36-ae0d-2ee6591a32bd`. Os arquivos modificados no workspace durante a publicação eram somente documentação e capturas geradas pelos testes; os uploads foram identificados como dirty por esse motivo.

Artefatos: `main-TFCXSHPO.js`, `styles-OSSVIJFC.css` e `chunk-2VJWPPA6.js`. SHA-256 do índice: `25C4EA8448B897DEAFF997833727B3D2AF2D2935258DD2C6145FB90199CE753D`. SHA-256 do CSS: `47A5F5489D146BEB9F4FBC55C998BED2F1293E4E9B0C839A61ADC3552ED070AF`.

Em produção, início e login passaram nas quatro larguras em ambos os hosts: 16 verificações com HTTP 200, sem overflow ou erros JavaScript, e CSS idêntico ao artefato local. O teste também confirmou a distância entre Entrar e recuperação de senha. Uma identidade aleatória `example.invalid` por host recebeu 401 JSON e “Email ou senha incorretos.”, mantendo o formulário e sem erro de navegação. A entrega não exigiu deploy da API.
