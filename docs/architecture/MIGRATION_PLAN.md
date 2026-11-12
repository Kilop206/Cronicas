# Migração incremental — situação efetiva

Auditoria inicial: 04/11/2026, base `f3f51ae`. Validação mais recente: 11/11/2026. Não interpretar a migração de personagens, regras e fluxos de campanhas como conclusão de todos os contextos da plataforma.

| Marco | Implementado | Trabalho ainda necessário |
| --- | --- | --- |
| 1 Auditoria | Stack, fluxos, banco, auth, realtime, CI e riscos inspecionados; CURRENT_ARCHITECTURE | Atualizar métricas conforme outras fatias mudarem |
| 2 Modularização | Assemblies Domain/Application/Infrastructure; fontes e modelos agrupados; Angular organizado por features/core/routing/layouts | Isolar fisicamente cada bounded context, eliminar navegações/repositórios compartilhados legados |
| 3 Hexagonal | Personagens, histórico, coleções, favoritos, segredos, OBS, regras, campanhas/convites/participantes e associações sistema/universo usam portas e casos de uso | Migrar conta, dashboard, catálogos, dados e NPCs; substituir joins entre contextos |
| 4 Plugins | Catálogo versionado, regras compiladas, OP e outros motores existentes adaptados; host de apresentações exclusivas; teste de novo plugin sem alterar switch legado | Empacotamento/assinatura comunitária e sandbox de terceiros não implementados |
| 5 Persistência | EF extraído; outbox; transação/revisão/histórico; versões imutáveis; migração assistida com prévia/mapeamento/recibo/retorno; SQL/rollback testados | Conversores entre edições, data de criação histórica confiável e tratamento de todo histórico anterior à captura |
| 6 Comunicação | API v1 de leitura/escrita/catálogo, revisão obrigatória na escrita v1, eventos duráveis, SignalR autorizado e resync com rascunho preservado | Fanout entre réplicas; migração gradual dos demais contratos legados e eventos explícitos em todos os casos de uso |
| 7 Infraestrutura | Docker compatível com projetos extraídos, CI ampliada, logs JSON/correlação, health checks e instrumentos de métricas | Exporter/collector, alertas, retenção do outbox, validação em staging/produção |
| 8 Validação | Testes de domínio, arquitetura, migrations/outbox, HTTP/SignalR e navegador executados; evidências em TESTING | Teste de carga, auditoria completa de segurança e migração de todas as funcionalidades |

## Ordem para as próximas fatias

1. Concluído em 09/11/2026: fluxos existentes de personagens, histórico, inventário/habilidades, favoritos, segredos e OBS usam portas, validação e projeções de aplicação. Rotas e formatos legados foram preservados; PUT v1 exige revisão. Notas aceitam `expectedExists` opcional, enviado pelo editor atual para distinguir notas vazias da primeira revisão gravada. A auditoria confirmou que não existe endpoint de exclusão de personagem; a menção anterior a uma extração desse fluxo estava incorreta. Não foi introduzida uma operação destrutiva nova.
2. Implementados captura imutável, vínculo opcional, leitura efetiva e migração assistida entre capturas da mesma origem/edição. Prévia, mapeamentos sem colisões, ajustes validados, digest de revisão e recibo atômico preservam campos legados. Retorno é outra migração explícita. Conversores de edição continuam pendentes; não inferir edições por nome nem desfixar fichas automaticamente. Ver ADR 0005.
3. Campanhas básicas, convites e participantes foram extraídos em 10/11/2026. Associações com sistema/universo passaram a contratos públicos em 11/11/2026, com referências protegidas, transações e notificações para fichas não fixadas. Extrair Identity e dashboard; depois catálogos/NPCs/dados. Substituir joins internos restantes por portas públicas. Ver ADRs 0006/0007.
4. Dividir projetos por bounded context somente após remover acoplamento real. Acrescentar testes de fronteira para cada contrato migrado e reduzir a lista de exceções legadas.
5. Criar staging com snapshot anonimizado; revisar migrations e rollback; só então preparar release de produção.

## Release seguro desta fatia

O release inclui outbox e versões imutáveis, além da política de migrations de startup. **Não publicar apenas o frontend nem reiniciar a nova API contra banco sem migrations.** Validar os dois SQL em staging, ter backup restaurável, aplicar o SQL revisado, publicar API e verificar `/health/ready` e contratos; então publicar frontend e verificar assets/rotas. O roteiro concreto está em `releases/README.md`.

Produção não foi alterada automaticamente nesta implementação. O SQL está revisável em `releases/`. Antes de qualquer fixação, o binário anterior pode continuar usando os dados legados; depois de fixar fichas, rollback deve manter um binário que entenda os vínculos. Não executar Down com versões capturadas ou eventos pendentes. A autorização histórica para deploy não substitui a restrição deste pedido contra mudanças automáticas nos dados de produção.

## Critérios que ainda impedem declarar migração completa

Isolamento total de domínios, controllers sem regra de negócio em toda a API, conversão entre edições e operação realtime em múltiplas réplicas permanecem pendentes. Vínculo opcional e migração assistida de capturas estão funcionais; não são um backfill do passado. Os testes asseguram as fronteiras novas e a compatibilidade verificada.

## Segunda fatia — 09/11/2026

Criação, leitura individual e salvamento de personagem foram extraídos. O adapter revalida papel, revisão e definição de sistema antes de salvar; mantém histórico, condições de encontros e outbox na transação. O teste concorrente encontrou colisão no índice único de revisões antes do UPDATE do personagem: essa colisão específica agora retorna conflito, sem mascarar outros erros de integridade.

`PUT /api/v1/characters/{id}/sheet` usa DTO HTTP próprio com `version` obrigatório; o PUT antigo segue aceitando versão omitida por compatibilidade. Campos omitidos mantêm valores anteriores, inclusive campos de mestre; fórmulas são recalculadas em cópia independente. Leitura/resync não executam nem descrevem regras do RPG. Nenhuma migration nova foi necessária e produção permanece sem alterações.

## Terceira fatia — ciclo de vida de personagens, 09/11/2026

`ReadCharacterLifecycle`, `DuplicateCharacter` e `RestoreCharacter` extraem os quatro fluxos existentes. O adapter HTTP não contém EF nem validações de negócio. `ICharacterLifecycleStore` expõe consultas específicas e cópia transacional; restauração reutiliza `ICharacterWriteStore.Save`, mantendo histórico, encontros e outbox no mesmo commit.

Histórico é limitado às últimas 100 revisões e exige dono/mestre, retornando somente metadados. Restaurar exige a revisão atual e valida os campos contra o template atual: preserva campos históricos desconhecidos inalterados, recalcula fórmulas e verifica os campos de mestre contra o estado atual. A versão avança; não é rebobinada. Não há restauração da definição antiga de regras.

Duplicação lê personagem e coleções em snapshot transacional consistente, revalida acesso/versão/template e atribui a propriedade ao ator autenticado. Inventário e habilidades recebem identidades/revisões novas. Segredos, OBS, favoritos e histórico não são copiados. Testes PostgreSQL incluem conflito da origem e falha após SaveChanges, comprovando rollback de personagem, filhos e outbox. Os smokes HTTP e a regressão de manobras/encontros passaram. Nenhuma migration adicional ou alteração de produção foi necessária.

## Quarta fatia — inventário e habilidades, 09/11/2026

Oito rotas existentes de listagem/criação/edição/exclusão foram mantidas e extraídas para casos de uso, valores puros e duas portas específicas. A contagem antiga antes do INSERT permitia ultrapassar capacidades sob concorrência; agora cada escrita bloqueia a participação e o personagem, revalida acesso e conta dentro da transação ReadCommitted. Quantidade/peso/textos/expressões mantêm as validações anteriores. Atualizações e exclusões exigem a revisão do filho.

Testes passaram com PostgreSQL real, HTTP e Chrome: permissões, IDs vinculados ao personagem, concorrência, limites, rollback, rascunho após conflito, recarga e rolagem de habilidade. Não há nova migration, mudanças de frontend ou eventos novos de coleções nesta fatia. Revisões do inventário/habilidades continuam independentes da ficha, sem invalidar rascunhos do personagem. Histórico/eventos específicos dessas coleções ainda precisam de contrato e integração próprios; não estão concluídos.

## Quinta fatia — favoritos, notas privadas e OBS, 09/11/2026

Os sete handlers restantes de personagem foram extraídos para casos de uso/portas. Favoritos são pessoais e continuam disponíveis a espectadores; inclusões/remoções simultâneas são idempotentes sob lock. Segredos exigem mestre, incluindo revalidação no commit; erros inesperados de banco deixaram de ser classificados indiscriminadamente como conflitos. A revisão inicial legada foi preservada, com um campo opcional de existência utilizado pelo Angular para bloquear a sobrescrita do primeiro rascunho.

OBS usa RNG criptográfico, hash no banco, rotação exclusiva do dono e projeção pública limitada. Seus perfis agora são resolvidos pelo registry de ID/edição/schema, com layout configurado como fallback para edição desconhecida. Segredos e chaves têm no-store. Testes de aplicação/PostgreSQL/HTTP/Chrome e builds passaram; nenhuma migration nova foi necessária. Histórico/realtime de segredos e coleções, versão imutável de schema e extração dos outros contextos continuam pendentes. Produção não foi alterada.

## Sexta fatia — versões imutáveis de ficha, 09/11/2026

Migration `20261109121911_CharacterSystemVersions` adiciona `rpg_system_versions` e `characters.SystemVersionId` nullable, com FKs/índice de deduplicação. Nenhuma ficha é preenchida automaticamente. Mestre fixa explicitamente o modelo atual com revisão da ficha, UUID do sistema e revisão do template; a captura/vínculo/histórico/outbox confirmam juntos. O editor usa o sistema efetivo da ficha e preserva o vínculo em cópias, regras, OBS e exportações.

Edição/restauração do template da campanha migra apenas fichas não fixadas. A captura é independente de alterações posteriores, exclui histórico mutável e catálogo privado de NPCs, e é imutável por EF e trigger SQL. O Down recusa apagar qualquer versão capturada. Estado histórico pode ser restaurado validando contra o vínculo atual; esta fatia não altera o vínculo por restauração. A migração assistida entre vínculos foi implementada na sétima fatia; desfixação permanece indisponível.

Passaram testes de unidade/aplicação/PostgreSQL, HTTP, Chrome com duas contas e quatro resoluções, regressões OP/D&D/inimigos/encontros, builds e lint. SQL e capturas estão concretos; aplicação em produção e CI remoto não foram certificados.

## Sétima fatia — migração assistida, 09/11/2026

Prévia somente de leitura, mapeamentos sem colisões, ajustes explícitos, preservação de campos retirados, fórmulas/defaults/validação do plugin e confirmação do plano. Origem e edição precisam ser iguais. Revisões/fingerprint/digest são revalidados sob locks; confirmação concorrente tem um único vencedor. Histórico anterior e recibo confirmam com vínculo, estado e outbox. Retorno usa o mesmo assistente. Destinos privados fora da origem autorizada permanecem ocultos, inclusive após desvincular a campanha. Não há migration SQL nova, desfixação ou conversor oficial entre edições.

Testes próprios em Domain/Application, PostgreSQL descartável (incluindo rollback após SaveChanges e permissão revogada), HTTP e duas janelas Chrome verificam o fluxo. Ver ADR 0005, TESTING e capturas do assistente. Produção não foi modificada.

## Oitava fatia — campanhas e acesso, 10/11/2026

Onze handlers preservam as rotas e usam casos de uso/portas de Campaigns. Detalhes, status, papéis, proteção do owner e tokens têm valores/políticas puros. Criação, edição, participação e aceite confirmam sob transações, revalidando autoridade. Corridas de convites distintos para a mesma pessoa retornam um aceite e um 409, sem violação de PK/500 ou consumo indevido de outro convite.

Mudança de papel/saída têm eventos duráveis e reconciliação imediata da sala. Papel e adesão associados à conexão são comparados à participação atual; eventos repetidos não derrubam uma conexão já atualizada. A publicação de mídia consulta o papel atual. PostgreSQL, HTTP, SignalR, Chrome/login/refresh e onze fronteiras passaram. Não há nova migration SQL. Associação de sistema/universo e Identity permanecem pendentes; não há ETag obrigatório de campanha no cliente legado. Nenhuma alteração de produção.

## Nona fatia — associações entre módulos, 11/11/2026

`ManageCampaignAssociations` e contratos públicos de Registry/Worlds substituem as duas associações legadas. Propriedade e referência são conferidas sob locks na transação da campanha; exclusões adquirem a referência antes da checagem de uso. Um no-op não grava; disputas sobre o mesmo vínculo retornam 409, alterações independentes preservam os dois vínculos. Não se modificam valores/revisões de personagens. Capturas fixadas permanecem isoladas.

Eventos de configuração confirmam com a associação. Fichas não fixadas observadas recebem invalidação autorizada; o editor recarrega o modelo ou protege rascunhos da ficha/notas até recarga explícita. Aplicação, PostgreSQL, HTTP, SignalR, Chrome e doze fronteiras passaram. Sem SQL adicional. Catálogos completos, Identity/dashboard, referências de sistema obrigatórias em escritas legadas e operação distribuída/offline continuam pendentes; detalhes na ADR 0007.
