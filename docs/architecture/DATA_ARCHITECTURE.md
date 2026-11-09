# Dados, revisões e migrations

PostgreSQL continua sendo a autoridade. Usuários, sessões, campanhas, participação, personagens, NPCs, inventário, habilidades e rolagens mantêm tabelas relacionais/FKs. Dados específicos de sistemas continuam JSONB: `characters.FieldsJson`, `rpg_systems.SchemaJson`, snapshots de histórico e fichas de ameaças. As entidades EF ficam em `modules/Chronicles.Infrastructure/Persistence/Models/*`.

## Mudança nesta entrega

Migration `20261104172257_IntegrationOutbox` adiciona somente `integration_outbox`: UUID PK, payload JSONB, ocorrência, disponibilidade, tentativas e processamento, com índice parcial para pendentes. Nenhuma coluna/tabela de jogo foi removida ou renomeada. A extração de assemblies preserva todos os IDs de migrations antigas e namespaces dos modelos.

SQL de revisão: `releases/integration-outbox-up.sql` (idempotente a partir da migration anterior) e `integration-outbox-down.sql` (rollback técnico). A suíte PostgreSQL gera esses arquivos e verifica a cadeia completa, snapshot do EF, preservação de sentinela/dados, downgrade e re-upgrade em banco descartável.

Startup em Development/Testing aplica migrations para desenvolvimento. **Production não aplica migrations:** verifica pendências e interrompe o início com erro explícito se o release não foi preparado. Nunca executar `database update` contra produção por efeito colateral de deploy. Revisar SQL, backup, janela e plano de retorno primeiro.

Rollback preferível: voltar o binário e deixar a tabela aditiva. Só executar o SQL Down depois de drenar/exportar eventos e parar consumidores, pois ele remove eventos técnicos. Dados de personagens/histórico permanecem, mas eventos removidos não podem ser recuperados sem backup.

## Concorrência e transações

Personagens usam `Version` como token EF e recebem histórico antes da alteração. O caso de uso de regras exige `expectedVersion`; o adaptador revalida acesso/versão/sistema em transação RepeatableRead, bloqueia participação/campanha/sistema com `FOR SHARE`, e salva recursos, condições de encontros, histórico, rolagens e eventos juntos. Conflitos EF/serialização retornam 409; não há retry automático de uma ação com nova sorte/rolagem.

NPCs, habilidades, inventário, segredos e schema mantêm os mecanismos anteriores de revisão. O endpoint PUT legado aceita a versão opcional por compatibilidade; clientes atuais a enviam. Migrar clientes antigos antes de torná-la obrigatória em todo contrato legado.

## Envelope de ficha

`GET /api/v1/characters/{id}/sheet` identifica proprietário/personagem/campanha, UUID do sistema registrado, ID estável do RPG, edição, schema, revisão do template, estado, revisão da ficha e atualização. ETag corresponde à revisão; resposta privada/no-store. Não inventa datas de criação ausentes.

Desde 09/11/2026, `SystemVersionId` identifica uma captura imutável opcional por personagem. Quando null, o comportamento legado continua: sistema corrente da campanha. Quando preenchido, definição, edição, schema, apresentação e fórmulas vêm de `rpg_system_versions`, inclusive se a campanha trocar/remover seu sistema. A API v1 expõe o vínculo e a revisão efetiva. O editor consulta `GET /api/characters/{id}/system`, não deduz o modelo pelo sistema atual da campanha.

O passado não foi preenchido por inferência. `Character` ainda não possui CreatedAt histórico confiável, e históricos antigos não capturavam a definição de regras. As novas revisões registram o ID de versão existente na captura; restaurar estado mantém o vínculo atual e valida contra ele. Não há restauração automática de uma edição passada nem migração entre versões fixadas nesta fatia.

## Criação e salvamento extraídos — 09/11/2026

Não houve nova migration nesta fatia. `EfCharacterWriteStore` grava estado normalizado, histórico e eventos; compartilha leitura, locks de contexto e mapeamento com o adapter de regras. Papel ou schema alterado depois da validação invalida o commit. Saves simultâneos com a mesma revisão têm um vencedor; o índice único `IX_character_revisions_CharacterId_Version` também é reconhecido como conflito, pois seu INSERT pode anteceder o UPDATE otimista no lote EF. Outras violações de unicidade continuam sendo erros.

Cada save aceito mantém a semântica anterior de criar uma revisão, mesmo quando o cliente repete valores. Campos omitidos são preservados; campos históricos desconhecidos só são aceitos inalterados. Uma falha na projeção de condições do encontro desfaz estado/histórico/eventos e limpa mudanças pendentes do DbContext. A escrita v1 exige `version`; o contrato legado preserva sua opção de versão omitida.

## Cópia e restauração extraídas — 09/11/2026

Não alteram tabelas ou o formato JSON dos arquivos existentes (`CharacterSnapshot`). Histórico retorna até 100 metadados ordenados por versão decrescente; a carga do arquivo exige o par personagem/revisão e autorização. Restaurar cria uma nova revisão usando o mesmo commit de save, incluindo a projeção de condições em encontros. Campos históricos desconhecidos são aceitos somente com o valor arquivado; fórmulas e regras são as do template atual, não uma edição histórica presumida.

Cópia usa RepeatableRead e os locks de contexto compartilhados. Compara a versão/papel/sistema da origem antes de criar o novo personagem, com dono igual ao ator e versão 1. Todos os campos de inventário/habilidades são copiados em uma única transação, com novos UUIDs, versões e datas. Segredos, chave de OBS, favoritos, revisões e vínculos de encontro permanecem na origem. O snapshot permite uma visão consistente das coleções; não promete bloquear toda alteração posterior da origem. Um teste com exceção após SaveChanges verifica que a ausência de commit desfaz também filhos e eventos.

## Escrita de inventário e habilidades — 09/11/2026

Schema e dados existentes não mudaram. `EfCharacterInventoryStore`/`EfCharacterAbilityStore` consultam filhos com personagem e participação; usam o token Version já existente para edição/exclusão. A criação sempre atribui UUID/datas/versão 1 no servidor. Validação e normalização ficam nos valores puros do domínio, chamadas pela aplicação antes de persistir.

`CharacterAssetPersistence` inicia ReadCommitted, bloqueia a participação do ator com FOR SHARE e o personagem com FOR UPDATE e reconsulta a autorização. A contagem de 200 itens/100 habilidades acontece depois do lock. ReadCommitted permite que uma operação que aguardou o lock veja as inclusões confirmadas pela anterior; um snapshot RepeatableRead estabelecido antes da espera poderia usar a contagem antiga. O lock dura até o commit/rollback. Criações, edições e exclusões são atômicas; falhas de concorrência conhecidas retornam 409, outros erros permanecem erros.

A decisão e seus custos estão registrados em [ADR 0002](decisions/0002-character-asset-transactions.md).

Coleções mantêm suas próprias revisões. Não criam artificialmente uma revisão de ficha nem seu histórico/evento; alterações dessas coleções ainda não possuem histórico e invalidação realtime próprios. O teste PostgreSQL confirma limites sob duas inclusões concorrentes e rollback de criação/edição/exclusão mesmo após SaveChanges. Como antes, timestamps persistidos têm precisão de microssegundos, inferior ao relógio .NET da resposta imediata.

## Favoritos, segredos e capacidades OBS — 09/11/2026

Mesmas tabelas/FKs/índices, sem migration adicional. Os três fluxos usam o protocolo ReadCommitted com locks de participação/personagem e uma exigência de acesso explícita. Favoritos verificam a chave composta após o lock; dois PUTs ou DELETEs simultâneos terminam idempotentemente. Notas privadas revalidam mestre e revisão; erros de integridade desconhecidos não viram 409 por um catch genérico.

Por compatibilidade, notas ausentes retornam revisão 1/updatedAt null; a primeira gravação também mantém revisão 1. O PUT aceita `expectedExists?: boolean`: false exige ausência e true exige existência, tanto na aplicação quanto no commit. O Angular envia esse valor a partir de updatedAt, protegendo inclusive o primeiro rascunho obsoleto. O campo é opcional para clientes antigos; sua omissão mantém a ambiguidade inicial anterior, que um contrato futuro obrigatório deve eliminar. Dois snapshots de ausência preparados antes da escrita também são comparados no adaptador para impedir dupla criação.

OBS continua com uma capacidade de 32 bytes aleatórios representada em 64 dígitos hexadecimais, normalizada e armazenada somente como SHA-256. A resposta com a chave em claro só ocorre depois do commit. Rotação substitui o hash anterior atomicamente; rollback mantém a URL antiga. Não há histórico da chave recuperável nem nova revisão de ficha por essa operação. A ponte de eventos existente ainda observa a alteração da entidade; não se promete uma nova revisão ou um canal de notas/OBS em tempo real.

A leitura do overlay busca personagem e sistema efetivo em um SQL e devolve somente a projeção permitida. Perfis especializados usam ID/edição/schema do registry; layout explícito ou edição desconhecida usa os blocos configurados. Não executar regras de outra edição para compor indicadores. A fonte continua atualizada por polling do navegador. O ADR 0003 detalha os contratos e limites.

## Captura e fixação — migration 20261109121911

`rpg_system_versions` guarda UUID, sistema de origem, nome/descrição capturados, definição JSONB, fingerprint SHA-256, revisão de origem e data da captura. A definição é copiada profundamente, sem o histórico mutável nem catálogo de NPCs. O índice `(RpgSystemId, ContentHash)` deduplica a mesma captura. EF e trigger SQL rejeitam atualização/exclusão. FKs Restrict preservam versões referenciadas e sistemas de origem; sistemas com capturas não podem ser excluídos pela API.

`POST /api/v1/characters/{id}/system-version` exige mestre, expectedVersion, registrySystemId e templateRevision. A transação ReadCommitted bloqueia participação/campanha e exclusivamente o sistema de origem antes de revalidar e deduplicar. Captura, vínculo, nova revisão e outbox são atômicos. Edição/restauração de sistema também bloqueia a origem antes de consultar fichas, excluindo as já fixadas das migrações de campos. Fichas criadas sem optar pela fixação continuam no fallback anterior.

Leitura, validação, rolagens, cópias, OBS, condições de encontros e alvos de ações de NPCs resolvem a definição efetiva do personagem. Não se executam regras de outra edição por causa de mudança na campanha. Cópia conserva o vínculo; restauração de estado não o troca. Exportação de conta inclui capturas utilizadas pelos personagens próprios; exportação individual leva o ID e a definição efetiva. Importar estado não concede permissão para escolher um ID de vínculo arbitrário.

## Migração assistida de capturas

O novo fluxo usa as mesmas tabelas. `CharacterSnapshot.Migration` é opcional no JSONB histórico e guarda origem, destino, digest revisado, mapeamentos, chaves ajustadas e instante; leitores antigos ignoram essa propriedade. O snapshot guarda o estado anterior, sem alterar capturas imutáveis. Nenhuma nova migration SQL é necessária. O contrato de histórico legado permanece só com metadados; o assistente retorna até 50 recibos, exclusivamente a mestres autorizados.

`MigrateCharacterSystem` valida a mesma origem/edição e reconstitui o plano. `EfCharacterSystemMigrationStore` bloqueia participação/campanha/origem/personagem, confere o digest novamente, cria/reutiliza a captura de destino e confirma vínculo, campos, histórico e outbox em uma transação. Modelo atual usa revisão e fingerprint para impedir uma mudança entre prévia e confirmação. Valores retirados permanecem; colisões e limites bloqueiam a operação em vez de truncar dados. Retorno ao modelo anterior é outra migração explícita. Não há conversão entre edições ou backfill histórico. Ver ADR 0005.

SQL revisável: `releases/character-system-versions-up.sql` e Down correspondente. Up é aditivo e não faz UPDATE/backfill de personagens. Down só permite retornar quando a tabela de capturas está vazia e nenhum vínculo existe; caso contrário aborta antes de remover objetos. Depois de usar a fixação, um rollback de aplicação precisa preservar suporte aos vínculos, pois um binário antigo pode ignorá-los mesmo mantendo as colunas. Não automatizar esse retorno. As versões congelam definição de dados, não binários dos executores ou conteúdo remoto de imagens. Ver ADR 0004.
