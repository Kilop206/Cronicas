# ADR 0003 — Notas privadas e capacidades de OBS

Estado: implementado e validado localmente em 09/11/2026.

## Decisão

Separar favoritos, segredos e OBS em três casos de uso/portas do contexto de personagens, reutilizando o protocolo transacional com exigências distintas: membro para favoritos, mestre para notas e dono não espectador para rotação OBS. Leituras de favoritos/notas incluem autorização e conteúdo no mesmo comando SQL. Writes revalidam o contexto sob locks; falhas não confirmam estado parcial.

Favoritos continuam pessoais e idempotentes. Notas mantêm revisão independente. O contrato antigo usa revisão 1 tanto para ausência quanto primeira gravação: mudar a contagem quebraria clientes. Acrescenta-se `expectedExists` opcional; o editor envia false quando updatedAt é null, e true depois de persistir. O servidor verifica existência e revisão antes e durante o commit. Clientes antigos preservam sua semântica; uma futura API estrita deve exigir o indicador ou um ETag que distinga esses estados.

OBS é uma capacidade pública restrita: RNG criptográfico de 256 bits em Infrastructure; validação/normalização/hash com bibliotecas base; banco guarda apenas hash; chave em claro retorna depois do commit. A projeção de Application não expõe IDs, coleções, campos completos ou notas. Perfis usam o registry por ID/edição/schema, com fallback para layout configurado. Nenhuma regra proprietária nova foi introduzida. Notas/chaves/fontes não devem ser armazenadas em cache.

## Consequências e limites

Rotação substitui o hash de forma atômica; falha mantém a fonte anterior. A leitura por capacidade não exige sessão e continua válida após mudança de papel até a chave ser substituída, conforme o contrato existente. Não foram adicionados TTL, revogação administrativa, histórico de notas ou eventos específicos. O overlay segue por polling. Esses contratos exigem evolução própria, sem usar a revisão do personagem como se cobrisse recursos independentes.

Testes de aplicação/PostgreSQL/HTTP/Chrome cobrem isolamento de favoritos/notas, primeiro rascunho obsoleto, concorrência em revisão persistida, papel revogado antes do commit, hashes/rotação, rollback, perfis dos quatro RPGs e fonte anônima mobile. Não foi necessária migration nova nem alteração em produção.
