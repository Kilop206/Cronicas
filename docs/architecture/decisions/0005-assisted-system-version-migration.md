# ADR 0005 — Migração assistida de versões fixadas

Estado: implementado em 09/11/2026. Complementa a ADR 0004; não altera a imutabilidade das capturas.

## Decisão

Mestres podem migrar uma ficha fixada para o modelo atual de sua origem ou outra captura autorizada da mesma origem. O sistema e a edição (`TemplateId`/`RulesetVersion`) precisam permanecer iguais. Não há conversão oficial presumida entre edições, troca entre RPGs, desfixação nem execução de código fornecido por usuários.

O assistente possui três operações v1: `GET system-migrations/targets`, `POST system-migrations/preview`, `POST system-migrations`, sob `/api/v1/characters/{id}`. A primeira retorna destinos e os últimos 50 recibos de migração. Os contratos HTTP obrigatórios são distintos dos comandos da aplicação. Todas as respostas são privadas/no-store.

Uma prévia compara as definições, mostra valores anteriores/posteriores, aplica defaults declarados, valida tipos/limites/regras e recalcula fórmulas com o parser existente. Mapeamentos copiam um valor existente para uma chave editável desocupada. Origens continuam preservadas; destinos repetidos, ocupados ou calculados e ambiguidades com ajustes são erros. Ajustes explícitos só podem atingir campos editáveis do destino. Campos desconhecidos ou retirados permanecem inalterados. Limites existentes de 100 campos/20.000 caracteres continuam válidos; excedê-los bloqueia a migração, sem truncamento.

O digest SHA-256 é calculado sobre JSON canônico contendo ator, personagem/campanha/vínculo, estado/revisão, destino e plano. Não é um token de autorização nem comprova revisão humana. A UI exige confirmação depois da prévia; o backend exige digest correspondente, autoridade de mestre e revisões atuais. Nunca aceita o estado calculado pelo cliente. A aplicação recompõe a prévia; o adapter recompõe novamente sob locks antes de persistir. Mudança no estado, destino, ator ou plano exige nova prévia.

## Transação e histórico

`ReadCommitted`: locks de participação/campanha, origem exclusiva e personagem exclusivo; revalidação de acesso/contexto; resolução do destino; novo cálculo; deduplicação/criação da captura; histórico com recibo; aplicação dos campos e novo vínculo; sincronização das condições de encontros; SaveChanges e commit. A outbox existente participa da mesma transação. Duas confirmações concorrentes produzem um único vencedor. Falha após SaveChanges desfaz captura, recibo, vínculo, estado e eventos.

O JSONB já existente em `character_revisions.SnapshotJson` recebe `Migration` opcional: origem, destino, digest, mapeamentos, chaves ajustadas e instante. O snapshot continua contendo o estado anterior completo. Não há migration SQL adicional nem alteração dos quatro campos do endpoint legado de histórico. Capturas anteriores são imutáveis e não são removidas. Retornar a uma versão anterior exige outra prévia/confirmacão; a restauração comum de valores mantém o vínculo atual.

Inventário, habilidades, notas privadas e capacidade OBS não são regravados. Recursos centrais são preservados. A invalidação existente da ficha faz clientes sem rascunho buscarem o novo modelo; o protocolo de conflito preserva rascunhos.

## Escopo de acesso

Mestre da campanha vinculada à origem, ou mestre que também é dono da origem, pode consultar as capturas dessa origem e seu modelo atual. Após desvincular a campanha, um mestre que não possui a origem conserva acesso somente à captura atual da ficha e às capturas referenciadas em seu próprio histórico/recibos. Conhecer um UUID não concede acesso a modelos futuros privados ou outro RPG. Jogadores não enumeram destinos/recibos e não migram nem com um digest de mestre.

## Extensão futura

Uma conversão entre edições deverá ser um contrato compilado, versionado e testado, registrado no backend. Deve produzir um plano revisável e validar os invariantes da edição destino. Não ampliar a comparação por nomes nem usar fórmulas arbitrárias como conversores. Ainda faltam conversores entre edições e arquivos anteriores à primeira captura; não é possível reconstruir automaticamente schemas antigos desconhecidos. Ajustes de tabelas/listas neste assistente usam JSON validado, sem substituir o editor visual das fichas.
