# Extração futura sem reescrita

Não há motivo comprovado para transformar cada pasta atual em serviço. A base será um monólito modular até existirem métricas de carga, equipes/propriedade e requisitos de disponibilidade que justifiquem a separação.

| Candidato | Motivo verificável para extrair | Pré-requisitos e contrato |
| --- | --- | --- |
| Realtime/session tools | Conexões simultâneas/latência exigirem escala independente | Fanout compartilhado, autenticação/revogação propagadas, API de refetch/revisão e teste multi-instância |
| Media | Upload/processamento/armazenamento passarem a existir | Portas de objetos/ACL, S3 compatível, URLs assinadas, cotas, limites e eventos de processamento |
| Rules workers | Plugin confiável caro ou plugin não confiável precisar isolamento | Contratos determinísticos/versionados, snapshots mínimos, quotas/timeout, aprovação transacional do resultado no servidor |
| Notifications | Entrega externa real precisar retry/backpressure independente | Outbox → broker, consumidor com inbox, preferências/consentimento e deduplicação |
| Marketplace | Produto e equipe próprios efetivamente existirem | Catálogo/assinatura/moderação/instalação separados de execução e dados de campanha |

Identity, Campaign e Character permanecem juntos enquanto as invariantes exigirem joins/transações locais. Extrair primeiro seus contratos internos, não suas tabelas. Banco compartilhado entre serviços não representa isolamento concluído.

## Infraestrutura por necessidade

- **Redis/backplane:** antes de escalar sockets, suportar broadcast para todas as réplicas e revogação. Persistência de jogo permanece PostgreSQL; cache nunca substitui autorização/versão.
- **Kafka ou outro broker:** adaptar `IGameEventHandler`/outbox quando houver consumidor externo. Chave por agregado, contratos versionados, retries, inbox/DLQ e política de replay. Sem broker para desenvolvimento hoje.
- **S3:** acompanhar Media real; guardar metadados relacionais e referências, nunca objetos privados em URLs públicas permanentes.
- **OpenTelemetry:** exportar instrumentos `Chronicles.Http`, `Chronicles.Outbox` e tracing ASP.NET/EF para collector, com amostragem/remoção de atributos sensíveis. Instrumentos locais existem; exporter ainda não.
- **Prometheus/Grafana:** observar p95/p99, 409/429/5xx, idade/erros do outbox, conexões e saturação do banco. Definir SLO antes de alarmes/autoscaling.
- **Kubernetes/Argo CD:** só após múltiplos workloads justificarem sua operação. Readiness/liveness separados, migrations como release job revisado, deploy gradual e rollback de binário; segredos fora do Git.
- **Gateway/BFF/gRPC:** introduzir quando resolver roteamento, autenticação/políticas ou agregação demonstráveis. Não criar proxies internos que apenas encaminham chamadas.

## Gate para múltiplas réplicas

Mesmo com `SKIP LOCKED`, o dispatcher atual só conhece watchers locais. Não escalar API horizontalmente antes de um teste com duas instâncias comprovar que ambas recebem invalidações e revogações. Sticky sessions por si só não distribuem eventos. Testes atuais de claims concorrentes verificam banco, não fanout distribuído.
