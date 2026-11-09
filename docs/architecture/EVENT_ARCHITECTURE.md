# Eventos e outbox

`Domain/Events/GameEvent` é o envelope v1: EventId, Type, ContractVersion, OccurredAt, AggregateId, CampaignId opcional, Revision opcional e CorrelationId. Não contém resultados privados de dados, campos, segredos, emails ou tokens. Contratos v2 incompatíveis precisarão de um novo leitor; o dispatcher recusa versões desconhecidas e mantém o evento pendente.

| Evento v1 | Disparo real atual |
| --- | --- |
| CharacterCreated / CharacterUpdated / CharacterDeleted | Alteração EF concluída de personagem |
| CharacterSheetUpdated | Personagem modificado, para invalidação da ficha |
| CampaignCreated | Campanha inserida |
| PlayerJoinedCampaign | Participação inserida, inclusive proprietário inicial |
| DiceRolled | Rolagem inserida; envelope não revela resultado/visibilidade |
| RPGSystemRegistered | Sistema registrado pelo usuário; não significa instalar código de plugin |

## Publicação consistente

Uma ponte de transição (`LegacyDomainEvents`) detecta alterações persistentes e adiciona eventos ao DbContext antes do SaveChanges. Escrita e outbox pertencem à mesma transação. Nenhum evento é publicado no hub antes do commit. Rollback/conflito também desfaz os eventos. A ponte existe porque há caminhos de escrita legados; casos de uso futuros podem produzir eventos de domínio explícitos sem depender de detecção EF.

Inventário/habilidades têm casos de uso extraídos, mas ainda não emitem eventos próprios nem incrementam a revisão da ficha. Não reutilizar CharacterSheetUpdated com uma revisão antiga: o cliente descarta revisões já vistas. Uma evolução dessas coleções deverá definir seus eventos/revisões, invalidar o cache adequado e acrescentar histórico se necessário. A extração atual preserva o contrato existente, sem prometer sincronização simultânea dessas listas.

```mermaid
sequenceDiagram
  participant API
  participant UC as Use case
  participant DB as PostgreSQL
  participant D as Dispatcher
  participant H as Handler SignalR
  API->>UC: comando + expectedVersion
  UC->>DB: validar versão e acesso; estado + histórico + outbox
  DB-->>API: commit / conflito
  D->>DB: FOR UPDATE SKIP LOCKED
  D->>H: envelope v1
  H->>DB: revalidar sessão e participação
  H-->>API: invalidação aos sockets autorizados
  D->>DB: marcar processamento e commit
```

`OutboxDispatcher` processa lotes limitados, claim por linha e backoff exponencial até 300 segundos. Erros mantêm eventos pendentes com contador de tentativas; não os descarta depois de um limite. Operação deve observar tentativas/idade da fila e investigar poison messages. Ainda não há tela de operação, alarme externo ou retenção automática de processados.

Entrega é **ao menos uma vez**: falha depois do envio e antes do commit pode repetir. O cliente mantém uma janela de EventIds e ignora revisões antigas; invalidação é idempotente. Um consumidor futuro com efeitos persistentes deverá gravar inbox/deduplicação e seu efeito na mesma transação. Não há garantia de exactly once.

## Interno versus integração

O envelope é seguro para uma integração futura, mas o transporte atual é interno, via `IGameEventHandler`. Não existe Kafka ou publicação externa habilitada. Um adaptador de broker deve confirmar publicação antes de marcar outbox; consumidor externo precisa de inbox, política de ordenação por agregado e DLQ/reprocessamento explícitos.

Claims de outbox suportam concorrência no PostgreSQL. **Fanout SignalR atual é de uma instância:** watchers vivem em memória. Para múltiplas réplicas, usar backplane/serviço de realtime e transporte que distribua a invalidação para todos os processos com sockets, antes de habilitar escala horizontal. Não confundir múltiplos consumidores seguros com broadcast distribuído completo.
