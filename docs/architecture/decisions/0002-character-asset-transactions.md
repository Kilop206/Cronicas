# ADR 0002 — Transações das coleções de personagens

Estado: implementado e validado localmente em 09/11/2026.

## Contexto

Inventário e habilidades usam tabelas e tokens Version independentes da ficha. Os endpoints antigos consultavam a autorização e o total de filhos antes de salvar, permitindo alteração de permissão entre verificação e escrita e inclusões simultâneas acima dos limites de 200 itens/100 habilidades. Não é necessário mudar tabelas, regras de RPG ou contratos HTTP para corrigir essas corridas.

## Decisão

Valores e limites ficam em Domain, orquestração em Application e operações atômicas em duas portas/adapters específicos. Cada escrita usa ReadCommitted e obtém, nesta ordem, FOR SHARE na participação do ator e FOR UPDATE no personagem. Reconsulta propriedade/papel/campanha e verifica a revisão do filho ou a capacidade antes de SaveChanges/commit. O lock do personagem serializa as escritas das duas coleções daquele personagem; outros personagens não usam esse mesmo lock.

ReadCommitted é necessário para contar depois de aguardar uma escrita anterior. Um snapshot RepeatableRead iniciado antes de aguardar o lock pode continuar vendo a contagem antiga de filhos; não basta copiar o protocolo de edição da ficha. A transação mantém a autorização até o commit e faz rollback de todos os efeitos se houver falha. SQL bruto é parametrizado e limitado aos locks do adaptador, sem vazar para Application/Domain.

Mantêm-se as versões independentes e os DTOs antigos. Não incrementar a versão da ficha para simular eventos de coleção: isso provocaria conflitos de rascunhos sem representar uma alteração no documento de personagem. Histórico e eventos de coleções serão uma evolução explícita com contratos e consumidores próprios.

## Consequências e evidência

Transações são curtas e nenhuma chamada de rede/rolagem ocorre sob lock. Duas escritas do mesmo personagem podem aguardar uma à outra; a correção do limite exige essa serialização. Deadlocks e conflitos EF conhecidos retornam 409; erros inesperados não são convertidos em sucesso. O servidor não refaz automaticamente ações concorrentes.

`CharacterAssetPersistenceTests` usa PostgreSQL local descartável, duas gravações concorrentes, capacidades exatas, revogação de papel e falhas após SaveChanges. Testes HTTP e Chrome confirmam os contratos existentes, edição/exclusão por versão, recarga e rascunho preservado após conflito. Não houve migration ou alteração automática de dados de produção.
