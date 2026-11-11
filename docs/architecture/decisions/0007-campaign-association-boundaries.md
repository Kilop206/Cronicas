# ADR 0007 — Associações de campanha com sistemas e universos

Estado: implementado e validado localmente em 11/11/2026.

## Decisão e contratos

As duas rotas existentes `PATCH /api/campaigns/{id}/system` e `/world` passaram dos endpoints de catálogo para `CampaignAssociationEndpoints`. `ManageCampaignAssociations` exige a identidade real do proprietário e papel owner; membro/co-GM não recebe esse poder. Os contratos mantêm `systemId`/`worldId` nullable, 204, 401, 404 e validação 400. Uma disputa detectada após a leitura do caso de uso retorna 409. Null remove somente a associação escolhida.

Campaign não consulta repositórios internos de Registry ou Worlds. `ICampaignAssociationStore` usa os contratos públicos `IRpgSystemCampaignAccess` e `IWorldCampaignAccess`. Seus adapters locais conferem propriedade e mantêm a referência protegida até terminar a transação. O domínio/aplicação não conhece EF, DbContext ou DTO HTTP. Os contratos são usados de fato na gravação, não são interfaces vazias para serviços futuros.

## Transação e concorrência

O adapter bloqueia participação e campanha, confere novamente autoridade e a associação relevante, adquire a referência própria do destino e grava FK, UpdatedAt e evento na mesma transação ReadCommitted. Decisões obsoletas sobre o mesmo vínculo não sobrescrevem outro vínculo. Mudanças independentes de sistema e universo podem confirmar juntas sem apagar a outra associação. Repetir o mesmo destino é no-op, sem regravar timestamp/evento, mas ainda exige um destino autorizado.

Os adapters de Registry/Worlds exigem transação local ativa e fazem `FOR SHARE` na referência de propriedade do ator. Uma exclusão simultânea não pode invalidá-la entre verificação e commit. Os endpoints legados de exclusão desses catálogos também passaram a adquirir `FOR UPDATE` antes de verificar referências. Se a associação vencer, excluir retorna 409; se excluir vencer, associar devolve destino indisponível (400). PKs/FKs continuam como última garantia. A extração integral dos catálogos e suas exclusões ainda não foi concluída.

## Fichas e eventos

Associar não transforma estado, inventário, habilidades, recursos, histórico ou revisão dos personagens. Fichas não fixadas continuam seguindo o sistema atual da campanha; fichas fixadas mantêm sua captura imutável, inclusive ao remover o sistema da campanha. Para converter valores/modelos fixados, continua sendo necessário o assistente da ADR 0005.

`CampaignSystemChanged` e `CampaignWorldChanged` usam o envelope existente versão 1, apenas metadados, sem nomes, campos ou definições privadas. O primeiro é consumido pelo handler de fichas: consulta quais personagens atualmente observados pertencem à campanha e não têm captura fixada; revalida sessão/participação antes de enviar invalidation por personagem, revision null. Não publica o catálogo ao grupo inteiro. O evento de universo registra a mudança para consumidores; a autorização de leitura do mundo continua sendo verificada no banco.

No Angular, uma notificação de sistema recarrega o modelo efetivo quando não há rascunho. Havendo edição pendente da ficha ou notas privadas, mantém os valores locais e pausa salvamento; o usuário precisa revisar/copiar/exportar e confirmar a recarga. Um save já em voo não limpa essa pausa de modelo. O teste Chrome cobre os dois tipos de rascunho, troca limpa automática, isolamento da ficha fixada e persistência após refresh.

## Dados, limites e evolução

Nenhuma migration SQL, preenchimento retroativo, template oficial novo ou serviço remoto foi criado. Os contratos locais de referência dependem da mesma unidade de trabalho/banco; não podem ser substituídos por RPC dentro da transação sem rever a consistência. Para distribuir Registry/Worlds, definir um protocolo de referência/versionamento e a estratégia de indisponibilidade antes de extrair processos.

O cliente legado não envia uma revisão obrigatória da configuração da campanha nem a referência de sistema esperada em toda escrita de ficha. A conferência no commit detecta alterações entre a leitura do caso de uso e a gravação; não identifica todo rascunho antigo enviado por um cliente que perdeu a notificação. Recarregar a página recupera o modelo efetivo. Fanout multi-instância e replay completo após períodos offline continuam pendentes; não se declara essa migração distribuída concluída.

## Evidências

Passaram aplicação, doze fronteiras, PostgreSQL descartável, HTTP/SignalR e Chrome. Testes verificam propriedade, no-op, conflitos, gravações independentes, proteção das duas referências contra lock de exclusão, rollback após SaveChanges, autoridade revogada no commit e estado fixado preservado. A disputa real HTTP de associar/excluir foi verificada para ambos os tipos. Capturas desktop/mobile e de rascunho estão em visual-validation/architecture. Não houve alteração de produção.
