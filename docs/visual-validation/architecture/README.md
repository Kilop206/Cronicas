# Evidências da refatoração — 05/11/2026

Capturas feitas por `web/architecture-browser.mjs` no Chrome headless, usando os Dockerfiles reais da API e do frontend, Nginx e PostgreSQL local. Não há interceptação de API nem dados fixos substituindo persistência. Contas, campanhas, sistemas e personagens de teste são criados pelo backend local.

| Ficha | Desktop | Amplo | Tablet | Mobile |
| --- | --- | --- | --- | --- |
| Ordem Paranormal | [1440×900](ordem_paranormal-1440x900.png) | [1920×1080](ordem_paranormal-1920x1080.png) | [768×1024](ordem_paranormal-768x1024.png) | [390×844](ordem_paranormal-390x844.png) |
| D&D | [1440×900](dnd_5e-1440x900.png) | [1920×1080](dnd_5e-1920x1080.png) | [768×1024](dnd_5e-768x1024.png) | [390×844](dnd_5e-390x844.png) |

São capturas full-page com o viewport indicado; a altura do arquivo pode ser maior. A composição anterior foi preservada: roda e perícias em OP, seis atributos e colunas próprias em D&D. Não houve nova reconstrução de design nesta fatia.

O mesmo teste abriu duas janelas autenticadas, confirmou assinatura SignalR com refetch real e verificou que uma alteração remota atualizou a ficha sem edição pendente. Na outra janela, um rascunho inválido foi mantido, o autosave foi pausado e a navegação pôde ser cancelada. [Rascunho preservado em conflito](ordem-conflict-preserved.png).

Depois de recarregar explicitamente, o teste editou o nome, aguardou PUT/autosave real, verificou a persistência e a atualização da outra janela. A aba Combate foi mantida durante a troca de inputs do renderer; não houve recriação do componente. Os testes verificaram ausência de overflow horizontal e de erros JavaScript nas duas fichas.

Também passaram separadamente as suites reais de regras para OP/D&D/T20/CoC e o smoke de autorizações/revogação/reconexão do hub. Essas evidências são locais; não demonstram um deploy em produção nem fanout entre múltiplas réplicas.

## Modelo fixado — 09/11/2026

`web/character-systems-integration.mjs` capturou uma ficha personalizada real após o mestre fixar o modelo, editar o template de origem e remover o sistema da campanha. O jogador manteve o atributo original e a fórmula ×2, salvou 4/8, recarregou e exportou a definição fixada. Não houve mock de API nem reconstrução dos layouts especializados nesta etapa.

| Desktop | Amplo | Tablet | Mobile |
| --- | --- | --- | --- |
| [1440×900](system-binding-1440x900.png) | [1920×1080](system-binding-1920x1080.png) | [768×1024](system-binding-768x1024.png) | [390×844](system-binding-390x844.png) |

Capturas full-page; dimensões acima são o viewport. Desktop e mobile foram abertos para inspeção: indicação de modelo fixado legível, campos preservados e reorganização sem overflow. O teste verifica as quatro larguras. A composição genérica existente foi preservada; essas imagens demonstram o fluxo de versionamento, não uma nova comparação com C.R.I.S./D&D Beyond. Regenerações das capturas anteriores durante regressões foram mantidas fora deste commit.

## Assistente de migração — 09/11/2026

Capturas reais de `web/character-migrations-integration.mjs`, com API Docker/Nginx/PostgreSQL e Chrome, sem mocks. O mestre seleciona o modelo revisado, mapeia `score → new_score` e revisa a preservação de `score` e a fórmula `new_score * 3`. O botão Aplicar permanece bloqueado antes da confirmação.

| Desktop | Amplo | Tablet | Mobile |
| --- | --- | --- | --- |
| [1440×900](assisted-migration-1440x900.png) | [1920×1080](assisted-migration-1920x1080.png) | [768×1024](assisted-migration-768x1024.png) | [390×844](assisted-migration-390x844.png) |

Viewport é o tamanho da janela; a captura inclui a página inteira. A inspeção inicial identificou rolagem horizontal na comparação mobile; o layout foi corrigido para mostrar Campo/Alteração/Antes/Depois empilhados e o teste foi repetido. Não há overflow horizontal da página nas quatro resoluções. A ficha personalizada de teste preserva o layout anterior; este fluxo não redesenha as fichas especializadas.

O mesmo script aplica um ajuste validado, verifica persistência/reload e atualização da janela do jogador, abre recibos e retorna à captura original por uma nova prévia/confirmacão. Jogador não tem ação de migração e rascunho privado impede o mestre de iniciá-la. Capturas de diagnóstico de execuções intermediárias ficam fora do commit.
