# ADR 0001 — Monólito modular com extração vertical

Status: adotada em 04–05/11/2026.

## Contexto

O produto possui Angular/.NET/PostgreSQL funcionais, quatro fichas específicas, regras puras misturadas com DTO HTTP, migrations em uso e deploy Docker com contexto fixo. Uma reescrita ou extração simultânea de serviços arriscaria dados e compatibilidade.

## Decisão

Extrair Domain/Application/Infrastructure em bibliotecas .NET no contexto Docker atual. Começar pela execução/consulta de regras, removendo o DTO aninhado do endpoint. Manter schemas flexíveis, edições explícitas e renderizadores reais. Usar contratos de plugin confiável, outbox PostgreSQL e evento de invalidação, sem broker obrigatório. Preservar namespaces de entidades EF e IDs de migrations; separar fonte não renomeia tabelas.

Não executar migrations em produção no startup. O release requer SQL revisado. Manter wrappers/contratos HTTP legados e a fachada `web/src/app/api.ts` para reduzir ruptura durante migração.

## Consequências

As novas fronteiras são compiláveis e testáveis. O restante dos CRUDs ainda precisa de extração de casos de uso e APIs entre contextos; pastas não são anunciadas como DDD completo. A duplicação de compatibilidade dos motores legados deve diminuir gradualmente, sem reimplementar cálculos já testados.

O realtime atual é de uma instância; outbox ao menos uma vez exige consumidores idempotentes. Não há suporte a código comunitário não confiável dentro do processo.

## Alternativas rejeitadas

Trocar .NET/Angular por outra stack, adotar Spring Modulith sem Java, instalar Kafka/Kubernetes apenas por arquitetura, usar um formulário genérico para todas as fichas, publicar eventos antes de commit ou carregar JavaScript arbitrário enviado pelo cliente.
