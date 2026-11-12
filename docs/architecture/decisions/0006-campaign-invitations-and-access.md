# ADR 0006 — Campanhas, convites e participação transacionais

Estado: implementado e validado localmente em 10/11/2026.

## Contexto

Onze handlers de campanhas/convites/participantes continham EF e políticas HTTP misturados. Convites distintos para a mesma pessoa podiam disputar a PK da participação. A criação do convite e as mudanças de papel verificavam autoridade fora de uma transação. A revogação da sala ocorria somente depois de SaveChanges, sem entrega durável.

## Fronteiras implementadas

`Domain/Campaigns/CampaignValues` define detalhes normalizados, estados/papéis permitidos, proteção do proprietário e hash/validade do token. `Application/Campaigns` contém `ReadCampaigns`, `ManageCampaigns`, `ManageCampaignMembers`, `ManageCampaignInvites` e portas específicas. `Infrastructure/Campaigns` implementa persistência, geração criptográfica e locks. Os três adaptadores HTTP de `Features/Campaigns` traduzem contratos; as cinco rotas básicas saíram de Program.cs. Composição de DI permanece no projeto API.

Rotas/formatos anteriores foram preservados: lista paginada de 20, busca, criação/leitura/edição/status, participantes/papéis/saída e convites. Nenhuma exclusão de campanha ou transferência de proprietário foi criada. O proprietário não pode remover/rebaixar a si mesmo por essas operações. Associação de sistema/universo, dashboard, identidade e outros contextos ainda usam adaptadores legados e não são declarados migrados nesta etapa.

## Consistência

Criação confirma campanha, participação owner e outbox em transação explícita. Edição/status bloqueiam participação e campanha e conferem novamente autoridade e o snapshot carregado pelo caso de uso. Alterações de participantes bloqueiam linhas em ordem estável de UserId antes da campanha; conferem proprietário, papel e JoinedAt antes de gravar. Leituras são limitadas pela participação.

O aceite bloqueia o convite, a participação do emissor e a campanha. A linha da campanha serializa adesões, inclusive quando a futura linha de participação ainda não existe e os tokens são diferentes. Após esperar os locks, são conferidos validade/uso/expiração e autoridade atual do emissor. Uma participação já existente retorna 409 sem consumir o convite. Duas pessoas usando um token têm um vencedor; dois tokens para a mesma pessoa produzem um aceite e um conflito, sem 500. A campanha é bloqueada por pouco tempo em cada aceite; isso privilegia uma invariante local simples sobre locks distribuídos artificiais.

O formato legado é mantido: 24 bytes aleatórios, 48 caracteres hexadecimais, sete dias, case-insensitive. Só SHA-256 é persistido. Prévia requer autenticação e a posse do link; só proprietário cria convites. Papel inválido, emissor sem autoridade, token expirado/usado não dão acesso. Respostas de convites têm private/no-store. Sair da campanha não apaga personagens nem coleções; escritas futuras são negadas.

## Efeitos após commit

Mudança de papel e saída emitem `CampaignMemberRoleChanged`/`CampaignMemberLeft`, envelope versão 1, na outbox existente. Payload contém IDs/metadados, nunca token ou perfil completo. Uma porta solicita reconciliação imediata da sala após commit. Falha de transporte é registrada e a outbox continua tentando. Falhas antes do commit não revogam conexões nem deixam eventos.

O consumidor durável consulta a participação atual pela porta pública. `RoomRegistry` compara papel e o JoinedAt associado à conexão, dentro do lock local, removendo apenas conexões desatualizadas. Entrega duplicada/atrasada não remove uma conexão que já representa a mesma participação e papel atuais. Uma saída seguida de nova entrada com o mesmo papel tem outra data de adesão, invalidando a conexão anterior. O estado de mídia consulta o papel atual no banco; não confia só no papel em memória. A sala continua single-instance: estes eventos não implementam backplane distribuído.

Uma remoção cujo envio falhou fica pendente em memória mesmo depois de sair do registry. A reentrega durável tenta completar remoção de grupo/notificação; só limpa a pendência após sucesso ou nova entrada autenticada da conexão. Testes forçam falha de transporte e conferem que a próxima tentativa realmente termina o trabalho. Essa fila é local aos sockets do processo; reinício encerra os próprios sockets.

## Dados e limites

Não há migration SQL, novos campos ou backfill. PK/FKs e datas existentes permanecem. Rollback de falha após SaveChanges foi verificado para criação, remoção e aceite, incluindo consumo do token e outbox.

O cliente legado não envia ETag/revisão obrigatória de campanha/participação. A conferência do snapshot protege a disputa entre leitura e commit do caso de uso, não detecta todo formulário antigo enviado depois de uma alteração já concluída. Uma API v1 desses fluxos deve estabelecer esse contrato antes de prometer proteção completa contra sobrescrita de rascunhos. Isolamento completo entre Identity/Campaign/Character ainda exige substituir os joins legados por contratos; esta etapa não introduz serviços remotos.

## Evidências

Testes de aplicação, PostgreSQL descartável, fronteiras, HTTP e SignalR passaram. Chrome executou criação pelo formulário, link real, login preservando returnUrl, refresh direto do convite, aceite, refresh da campanha, mudança de papel, saída e rejeição de replay. As capturas verificam a interface existente em 1440×900 e 390×844, sem mudança de design. Não houve deploy/alteração de produção ou certificação de CI remoto.
