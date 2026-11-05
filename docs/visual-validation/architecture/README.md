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
