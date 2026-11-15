# Gestão de fichas e controles

A lista de personagens permite buscar nomes ignorando acentos e ordenar por nome ou atualização. A busca não altera dados. Criar um personagem abre sua ficha; falhas de carregamento têm uma ação de tentar novamente.

O botão **Excluir ficha** aparece no topo para quem pode editá-la: proprietário da ficha e mestres autorizados da campanha. Espectadores e jogadores de outras fichas não recebem autorização pela API. A confirmação pede o nome exato, preserva a versão revisada e aceita cancelamento/Escape com retorno do foco ao botão. A operação fica indisponível enquanto há alterações locais pendentes, salvamento, ação de regras, anotações secretas não salvas, migração ou conflito remoto.

`DELETE /api/characters/{id}?version=N` exige sessão e versão positiva. A aplicação verifica permissões/versão; o adaptador repete as verificações dentro da transação e bloqueia a linha da ficha. Uma revisão desatualizada retorna 409. Um vínculo com encontro também retorna 409, mantendo a ficha e o encontro: o participante precisa ser removido pelo fluxo de encontros antes da exclusão.

A exclusão é permanente. As relações já existentes removem histórico, inventário, habilidades, favoritos e notas privadas, e a URL de OBS perde o acesso. A transação publica `CharacterDeleted` pelo outbox existente. Não há migration SQL nova nem exclusão de dados pessoais de produção durante os testes.

Os grupos de ações receberam espaçamento; os controles principais usam alvos de pelo menos 44 px em telas pequenas/dispositivos de toque. Exportar/importar, recuperação de erros, ações da ficha e a confirmação de exclusão foram incluídos nessa revisão. A impressão oculta controles de gestão.

Validação: `CharacterDeletionTests` verifica a política da aplicação. `scripts/character-lifecycle-smoke.mjs` verifica o contrato HTTP/PostgreSQL, versões, vínculos com encontros, exclusões concorrentes e indisponibilidade das relações/OBS após a exclusão. `web/character-management-browser.mjs` usa fixtures isoladas para busca/ordenação, criação, confirmação, cancelamento/Escape/foco, conflito de versão, retorno ao elenco, leitura e tamanho/espaçamento dos botões no celular. As fixtures não comprovam persistência no banco.
