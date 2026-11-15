# Revisão visual e responsiva — 15/11/2026

## Correções

- Busca de campanhas com largura flexível, fonte própria e contador em uma nova linha quando necessário.
- Perfil com quebra de nome/email longos; painéis e formulário de encontros respeitando a largura disponível.
- Cards de campanha com rodapés alinhados e títulos que podem quebrar sem empurrar o layout.
- Controles das cinco fichas com altura mínima de 32 px em desktop e 44 px em telas pequenas/toque. Ajustes nos grids, recursos, atributos, testes de morte e botões de rolagem para acomodar os controles.
- Barra de edição da ficha em fluxo normal no celular, menus com alvos maiores e cabeçalhos recolhíveis com área de interação adequada.
- Campos com tamanho de fonte de 16 px em celulares, inclusive autenticação; arquivos marcados como `hidden` continuam ocultos ao abrir as ferramentas de importação.
- Campos pessoais da ficha livre identificados por chave no `ngFor`. Isso evita recriação contínua dos controles e preserva a digitação/foco.

Tabelas extensas de perícias podem rolar dentro do painel; a página permanece dentro da largura da tela. As regras de impressão não recebem os tamanhos mínimos destinados à tela.

## Validação reproduzível

`npm --prefix web run test:visual:browser` serve a compilação de produção em loopback e usa respostas de API sintéticas. Não consulta nem altera campanhas pessoais.

A matriz cobre 66 telas/estados em 320, 390, 768 e 1440 px: entrada/cadastro/recuperação, páginas públicas, painel, campanhas, fichas livres e cinco sistemas em leitura/edição, NPCs/ameaças, encontros, universos, textos, pesquisa, perfil, preferências, convites, sala, navegação aberta e abas/ferramentas das fichas.

O teste verifica largura do documento, área mínima dos controles, interseções entre controles visíveis, erros JavaScript, importação oculta e digitação/foco da ficha livre. Conteúdo de `details` fechado e elementos destinados à impressão são excluídos da medição. Capturas e relatório JSON ficam em `.quality-local/visual-audit/`; `AUDIT_SCREENSHOTS=0` desativa apenas as imagens no CI. `AUDIT_FILTER` e `AUDIT_WIDTHS` permitem repetir um subconjunto.

Resultado final e publicação serão registrados após a execução da matriz completa.
