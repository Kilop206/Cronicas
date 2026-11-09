# Release da refatoração arquitetural

Preparação local; produção não foi modificada. A branch é `refactor/modular-rpg-architecture`, separada de main. Não publicar a nova aplicação sem preparar seu schema.

## SQL em ordem

1. `integration-outbox-up.sql`: da migration `20261104120000_NpcThreatSheets` até `20261104172257_IntegrationOutbox`.
2. `character-system-versions-up.sql`: até `20261109121911_CharacterSystemVersions`, adicionando tabela, coluna nullable, FKs/índices e trigger de imutabilidade.

Ambos são idempotentes, gerados pelo EF e executados duas vezes na suíte PostgreSQL em banco temporário. Nenhum deles atribui versões a personagens ou altera valores existentes. Conferir migrations realmente aplicadas antes de decidir quais scripts são necessários; não assumir o estado de produção pelo estado local.

## Publicação

Revisar SQL, backup restaurável, configuração e janela de mudança. Validar a sequência e o comportamento de startup em staging. Aplicar somente com autorização para o banco de destino; produção não executa migrations automaticamente. Publicar API, verificar `/health/ready`, leitura/escrita, autorizações e rotas v1. Depois publicar frontend: ele usa `/api/characters/{id}/system` e precisa da API correspondente. Conferir refresh de SPA e assets.

Antes de habilitar o uso da fixação em produção, conservar um binário compatível para retorno. A ação é opt-in por mestre; não executar chamadas em massa para preencher fichas existentes. Verificar uma ficha legada, uma fixada, cópia, OBS e edição do template sem alterar a ficha fixada.

## Retorno

Antes de qualquer captura, o Down de versões pode ser validado contra tabela vazia. Após haver capturas, ele aborta deliberadamente e não deve ser contornado. Rollback de aplicação precisa continuar reconhecendo os vínculos; um binário anterior a esse recurso pode ignorá-los e migrar campos de fichas fixadas. Não usar esse binário para edição após fixações.

Outbox também deve ser mantida ao voltar um binário; não executar seu Down com eventos pendentes. Os arquivos Down são referências técnicas para cenários vazios controlados, não comandos de limpeza de produção. Não apagar capturas, remover triggers ou desfixar dados para facilitar um deploy.
