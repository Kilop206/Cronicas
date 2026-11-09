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

**Limitação importante:** edição/schema são atualmente resolvidos pelo sistema da campanha; não são um vínculo imutável por personagem. `Character` não possui `CreatedAt` histórico nem FK própria para uma revisão imutável do template. O histórico existente preserva estado, não toda a definição de regras daquela data. A API v1 explicita a versão efetiva, não promete restauração de regras antigas.

Para concluir esse critério: adicionar colunas opcionais e tabela imutável de revisões de sistema; preencher vínculos somente em migração explicitamente autorizada ou na edição assistida; atualizar leitura/renderização/validação/restauração para o vínculo; manter fallback legado até cobrir todas as fichas. Não fazer backfill presumindo que a última versão sempre foi a versão original.

## Criação e salvamento extraídos — 09/11/2026

Não houve nova migration nesta fatia. `EfCharacterWriteStore` grava estado normalizado, histórico e eventos; compartilha leitura, locks de contexto e mapeamento com o adapter de regras. Papel ou schema alterado depois da validação invalida o commit. Saves simultâneos com a mesma revisão têm um vencedor; o índice único `IX_character_revisions_CharacterId_Version` também é reconhecido como conflito, pois seu INSERT pode anteceder o UPDATE otimista no lote EF. Outras violações de unicidade continuam sendo erros.

Cada save aceito mantém a semântica anterior de criar uma revisão, mesmo quando o cliente repete valores. Campos omitidos são preservados; campos históricos desconhecidos só são aceitos inalterados. Uma falha na projeção de condições do encontro desfaz estado/histórico/eventos e limpa mudanças pendentes do DbContext. A escrita v1 exige `version`; o contrato legado preserva sua opção de versão omitida.

## Cópia e restauração extraídas — 09/11/2026

Não alteram tabelas ou o formato JSON dos arquivos existentes (`CharacterSnapshot`). Histórico retorna até 100 metadados ordenados por versão decrescente; a carga do arquivo exige o par personagem/revisão e autorização. Restaurar cria uma nova revisão usando o mesmo commit de save, incluindo a projeção de condições em encontros. Campos históricos desconhecidos são aceitos somente com o valor arquivado; fórmulas e regras são as do template atual, não uma edição histórica presumida.

Cópia usa RepeatableRead e os locks de contexto compartilhados. Compara a versão/papel/sistema da origem antes de criar o novo personagem, com dono igual ao ator e versão 1. Todos os campos de inventário/habilidades são copiados em uma única transação, com novos UUIDs, versões e datas. Segredos, chave de OBS, favoritos, revisões e vínculos de encontro permanecem na origem. O snapshot permite uma visão consistente das coleções; não promete bloquear toda alteração posterior da origem. Um teste com exceção após SaveChanges verifica que a ausência de commit desfaz também filhos e eventos.
