# ADR 0004 — Versão de sistema fixada explicitamente por personagem

Estado: implementado e validado localmente em 09/11/2026.

## Contexto e decisão

A campanha aponta para um sistema mutável; o histórico anterior guardava somente valores da ficha. Alterar campos, fórmulas ou a edição podia mudar todos os personagens. Não há evidência suficiente para reconstruir qual definição cada ficha antiga utilizou no passado.

Adicionar `rpg_system_versions` e o FK opcional `characters.SystemVersionId`. Null conserva o comportamento antigo. Somente um mestre pode fixar a definição efetiva atual, informando revisão da ficha, UUID de origem e revisão do template. Não fixar automaticamente fichas antigas ou recém-criadas. Não oferecer uma escolha de ID arbitrário que permita vincular modelos de outra campanha.

A captura copia profundamente nome, descrição e definição. Exclui histórico mutável e catálogo de NPCs, mantendo schemas, layout, fórmulas, edição e ocupações configuradas. Fingerprint mais sistema de origem deduplicam capturas iguais; EF e trigger SQL impedem mutação/remoção. Fonte e FK usam Restrict. A transação ReadCommitted bloqueia participação/campanha/sistema, revalida o contexto e salva captura, vínculo, revisão e outbox juntos. A edição/restauração do sistema bloqueia a origem e migra somente personagens não fixados.

## Integração

`ReadCharacterSystem` fornece o modelo efetivo ao Angular. Regras, campos, OBS, cópia, encontros e alvos de NPCs resolvem o mesmo vínculo. Invalidação remota que muda o vínculo recarrega o modelo quando não há rascunho; com rascunho, o protocolo de conflito anterior preserva os dados locais. A ação fica na aba principal e bloqueada durante alterações pendentes da ficha ou notas privadas. Exportações incluem a referência e a definição; importar valores não autoriza alterar o vínculo.

## Alternativas e limites

Não usar somente o histórico de dez itens do editor: ele é limitado, mutável e não preserva uma identidade relacional. Não copiar a definição em cada save: duplicaria dados e dificultaria saber qual versão é autoridade. Não fazer backfill para a versão mais recente presumindo que sempre foi a original.

Esta etapa originalmente permitia fixar uma vez. A ADR 0005 acrescentou migração assistida entre capturas autorizadas da mesma origem/edição: diferenças, transformações explícitas, validação de permissões/regras e recibo de origem/destino sem alterar capturas. Desfixação e conversões de edição continuam indisponíveis. Restaurar valores históricos mantém o vínculo atual e valida contra ele; histórias anteriores à captura não recuperam automaticamente uma edição antiga. Datas de criação desconhecidas não são inventadas. Definição imutável não congela código binário nem imagens remotas.

Up é aditivo. Down só funciona sem capturas/vínculos; havendo dados, aborta antes de remover objetos. Depois de usar fixações, rollback de aplicação requer um binário que reconheça os vínculos. Um binário antigo pode ignorar a coluna e migrar campos indevidamente: manter a tabela sozinha não resolve isso. Não automatizar downgrade ou publicação em produção.

## Evidência

Testes verificam deduplicação concorrente, exclusão de catálogos privados, alteração de template/campanha, fórmula e executor fixados, cópia/histórico/exportação, revogação de mestre, template alterado antes do commit, imutabilidade EF/SQL, rollback atômico e recusa do downgrade destrutivo. API e Chrome com duas contas validam fixação explícita, atualização remota, edição persistida e quatro viewports reais.
