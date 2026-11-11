# Verificação da refatoração

## Comandos

```text
npm --prefix web run lint
npm run test:plugins
npm run test:architecture
npm --prefix web run build
npm run test:pages-proxy
npm run test:spa-routes
dotnet build api/Chronicles.Api -c Release
dotnet run --project tests/Chronicles.Rules.Tests
dotnet run --project tests/Chronicles.Architecture.Tests
```

Persistência real, em PostgreSQL local: definir `ARCHITECTURE_TEST_DB` com conexão de administração ao banco `postgres`, host **127.0.0.1/localhost**, e executar `dotnet run --project tests/Chronicles.Persistence.Tests`. A suíte cria `cronicas_architecture_<guid>`, usa somente esse banco e o remove no finally. Recusa hosts externos e banco administrativo com outro nome. Não apontar esse teste para produção.

Exemplo local usado: `Host=127.0.0.1;Port=5544;Database=postgres;Username=cronicas_rules;Password=local-rules-test-only`. Os SQL de release são gerados em `docs/architecture/releases/`.

Com API 5099 e Angular 4200 configurados conforme LOCAL_SETUP:

```text
SMOKE_BASE_URL=http://127.0.0.1:5099 node scripts/sheet-events-smoke.mjs
SMOKE_BASE_URL=http://127.0.0.1:5099 node scripts/smoke.mjs
SMOKE_BASE_URL=http://127.0.0.1:5099 node scripts/room-smoke.mjs
```

Em PowerShell, defina a variável com `$env:SMOKE_BASE_URL='...'` antes do comando. Testes de navegador ficam em `web/`: `node rules-integration.mjs`, `node threats-integration.mjs`, `node threat-perception-integration.mjs`, `node threat-maneuvers-integration.mjs`. Requerem Playwright e Chrome instalado, usam servidor local e contas descartáveis. Não executá-los contra produção. Evite iniciar todas as suites de cadastro simultaneamente e ultrapassar o limite legítimo de autenticação.

## Cobertura das novas fronteiras

- Arquitetura .NET: domínio/aplicação sem frameworks, grafo acíclico de projetos, registry por edição/schema, autorização/conflito e plugin novo fora do switch legado.
- Arquitetura TS: grafo de imports sem ciclos, core sem feature, editor sem imports de layouts concretos, fontes internas sem ASP.NET/EF.
- PostgreSQL: cadeia completa, upgrade de dados existentes, snapshot exato, SQL aditivo, rollback/re-upgrade, estado/histórico/outbox atômicos, IDOR, escrita obsoleta, retry/idempotência e dois consumidores SKIP LOCKED.
- HTTP/SignalR: catálogo e envelope v1, ETag/correlação, autenticação, 404/403/409, execução de OP, payload sem estado, revogação de participação em socket já conectado e ressincronização.
- Regressões existentes: dados seguros, fórmulas, edições dos quatro RPGs, condições, ameaças, percepção/manobras, limites de requisição, convites e mídia/OBS.

## Evidência e limites

Em 05/11/2026 passaram os testes de arquitetura, a suíte PostgreSQL e o smoke HTTP/SignalR; a integração de regras com API/PostgreSQL/Chrome também passou para OP, D&D, T20 e CoC, capturando quatro resoluções por ficha. Falhas encontradas durante desenvolvimento (snapshot EF manual divergente e versões EF transitivas) foram corrigidas, sem suprimir avisos/testes.

Capturas de integração não certificam produção nem ausência de todas as regressões. A documentação deve registrar separadamente a execução do build Docker, smoke completo e testes de NPCs quando terminarem. Não declarar teste de carga, fanout em múltiplas instâncias, pentest ou execução de plugins comunitários: não foram realizados/implementados.

`docs/visual-validation/rules` contém capturas das suites existentes, com dados/rolagens aleatórios. Não comitar regenerações sem relação com a mudança. As capturas específicas desta migração devem ir para `docs/visual-validation/architecture/`, acompanhadas de um registro dos cenários reais utilizados.

## Verificações finais em containers

Também passaram os dois Dockerfiles (API Release e Angular/Nginx), readiness e os smokes completos de aplicação, mídia/OBS e API v1/SignalR usando as imagens reais em portas locais. `web/architecture-browser.mjs` passou com duas janelas, assinatura do hub/refetch, preservação de rascunho, cancelamento de navegação, autosave sem perder aba e oito capturas OP/D&D nas quatro resoluções. O registro está em `docs/visual-validation/architecture/README.md`.

`scripts/export-rpg-openapi.mjs` exporta os schemas reais do servidor local, filtra as rotas v1 e anota autenticação por cookie (o middleware existente não usa metadados ASP.NET Authorize). Resultado revisável em `docs/contracts/rpg-api-v1.openapi.json`.

O build Docker reportou 22 advisories moderados na árvore de desenvolvimento; `npm audit --omit=dev --json` retornou zero vulnerabilidades de runtime nesta verificação. Atualização de toolchain/dependências de desenvolvimento exige trabalho separado, sem `audit fix --force` ou troca de stack nesta migração.

As três suites de inimigos também passaram nas imagens Docker: criação/importação/autosave/defesas, sentidos e manobras. A suite de importação precisou aguardar a resposta de validação e a atualização do input; a asserção anterior podia ler o zero do formulário em branco antes do resultado assíncrono. Foi ajustada a espera, mantendo a expectativa de fortificação 100 e os demais critérios.

## Criação/leitura/salvamento — 09/11/2026

`CharacterWriteTests` na suíte de arquitetura verifica ownership, GM/jogador/espectador, campos de mestre, fórmulas em cópia, campos históricos/omitidos, revisão obrigatória v1, compatibilidade legada e invariantes de um plugin injetado. Também prova que ler a ficha não chama descrição/execução de regras.

`CharacterWritePersistenceTests` usa o mesmo banco descartável da suíte PostgreSQL: confirma criação/reabertura, histórico/eventos exatos, duas gravações concorrentes com um vencedor, papel/template alterados entre leitura e commit e rollback quando a projeção de condições do encontro excede o limite. O teste revelou uma colisão no índice único do histórico; ela é tratada especificamente como conflito, sem converter outros erros de integridade.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-writes-smoke.mjs` verifica os contratos reais. Inclui POST autorizado, PUT v1 com revisão, erros 400/401/403/404/409, JSON inválido com correlação, preservação de campos/fórmulas e concorrência com histórico exato. A mesma suite foi adicionada ao smoke de CI. Não precisa de nova migration.

Em 09/11/2026 passaram as suites .NET de regras/arquitetura/persistência, os cinco testes de fronteira Node, o build Docker da API, os smokes HTTP/SignalR e a regressão de navegador dos quatro RPGs. O cenário de duas janelas OP/D&D também passou, verificando autosave, rascunho, navegação, resync e quatro viewports. As capturas regeneradas ficaram no disco, fora dos commits desta etapa; nenhuma alteração de layout foi introduzida.

## Lista, cópia e histórico/restauração — 09/11/2026

`CharacterLifecycleTests` verifica políticas e casos de uso sem EF: permissões individuais na lista, limite do histórico, dono da cópia, conflitos, arquivos de outro personagem, preservação de campos históricos e validação dos campos de mestre contra o estado atual.

`CharacterLifecyclePersistenceTests`, executado no mesmo banco descartável, verifica consultas autorizadas, recuperação de campos retirados, arquivos anteriores preservados e duplicação de todos os campos das coleções. Um interceptor lança uma falha após SaveChanges para comprovar rollback real de personagem/inventário/habilidades/outbox. Também testa origem/template modificados e permissão revogada entre leitura e cópia.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-lifecycle-smoke.mjs` verifica os contratos existentes via HTTP: ownership da lista, privacidade/metadados do histórico, rejeição de revisão estrangeira/obsoleta, bloqueio de alteração ou remoção de campo de mestre, cópias independentes e isolamento de segredos/favoritos/OBS. A suite está incluída no CI.

Nesta fatia passaram o build Docker Release, as suites .NET de regras/arquitetura/PostgreSQL, os seis testes Node de fronteiras e os smokes HTTP de lifecycle, escrita e SignalR. O OpenAPI v1 foi exportado novamente sem alteração de contrato. `RULES_TEST_URL=http://127.0.0.1:8082 node web/threat-maneuvers-integration.mjs` passou em Chrome contra a nova API, incluindo restauração de personagem e projeção de condições no encontro. As quatro capturas regeneradas dessa suite permanecem no disco, fora do commit; nenhum layout foi alterado. A execução local não confirma o resultado do CI remoto nem um deploy de produção.

## Inventário e habilidades — 09/11/2026

`CharacterAssetTests` cobre validação/normalização dos valores, precisão de peso, dados seguros, dono/mestre/espectador e revisão em edição/exclusão. `CharacterAssetPersistenceTests` usa o banco descartável existente e comprova isolamento dos filhos por personagem, reabertura dos valores, um vencedor em edições concorrentes, capacidades exatas após inclusões simultâneas, revogação entre leitura e commit e rollback de criação/edição/exclusão após SaveChanges. Verifica também que alterações de filhos mantêm a revisão/histórico da ficha.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-assets-smoke.mjs` cobre DTOs/Location, matriz de acesso, valores nulos/inválidos, conflitos, dados recarregados e exclusão com revisão nas duas coleções. Está no CI. O teste compara instantes conforme a precisão JavaScript, pois o PostgreSQL armazena microssegundos enquanto a resposta .NET imediata pode conter sete dígitos fracionários.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/character-assets-integration.mjs` usa Chrome local: inventário em desktop, conflito sem perder rascunho, recarga do estado do servidor, habilidades em 390 × 844, CRUD e rolagem persistida na campanha. A espera de atualização de ngModel após clicar em Editar é explícita; ler o campo imediatamente podia capturar o valor padrão anterior. O teste não captura screenshots e não certifica uma comparação visual, pois esta etapa não altera layouts.

Passaram build Release .NET e Docker, as suites .NET de regras/arquitetura/persistência, sete testes Node de fronteiras, os smokes HTTP de assets/lifecycle/aplicação completa/SignalR e o teste de navegador acima. OpenAPI v1 foi exportado sem mudança de contrato. Não houve migration adicional, execução em produção ou confirmação de CI remoto. Histórico e eventos/realtime de coleções ainda são pendências explícitas.

## Favoritos, notas e OBS — 09/11/2026

`CharacterCompanionTests` cobre políticas distintas, privacidade, hash-only no port, validação de chaves, perfis OP/D&D/T20/CoC e D&D 2024 legado, edição desconhecida/layout personalizado e Defesa com condições. A projeção não altera estado nem repassa campos não selecionados. `CharacterCompanionPersistenceTests` cobre favoritos pessoais/idempotentes sob concorrência, notas só para mestre, criação concorrente preparada, revisões, revogação antes do commit, rotação de chave e rollback após SaveChanges. Erro inesperado de banco é propagado, não disfarçado de conflito.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-companions-smoke.mjs` verifica HTTP real: favoritos/dashboard, owner/gm/co-gm/jogador/espectador, 404/409/validação, no-store, perfis dos quatro sistemas e projeção segura sem IDs/campos/notas. Foi incluído no CI.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/character-companions-integration.mjs` passou em Chrome com API/frontend recompilados: favorito após reload; primeiro rascunho privado obsoleto rejeitado com expectedExists false e preservado no textarea; gravação após reload com expectedExists true; fonte OBS anônima em 390 × 844; rotação observada por polling; ausência das notas no contexto do jogador. O teste seleciona o painel pela legenda Fontes para OBS, pois a classe CSS obs-actions também é usada pelo painel de exportação. Não há capturas nesta suite nem mudança visual para comparar.

Passaram lint, oito fronteiras Node, testes .NET de regras/aplicação/arquitetura/PostgreSQL, builds Docker de API e Angular, smoke completo, regressões HTTP de coleções/cópia/SignalR e a suite acima. OpenAPI v1 foi exportado sem alteração desses contratos. O novo campo expectedExists está no endpoint legado de notas e é opcional; clientes antigos continuam aceitos. Produção e CI remoto não foram certificados por esses testes locais.

## Fixação de versões — 09/11/2026

`CharacterSystemTests` verifica captura profunda/fingerprint, exclusão de catálogo privado, permissão de mestre e identidade/revisões do comando. `CharacterSystemPersistenceTests` usa PostgreSQL descartável para duas fixações concorrentes com deduplicação, fallback legado, mudança de campanha/template, fórmulas e executor OP fixados, cópia/restauração, imutabilidade em EF e SQL, rejeição de Down com dados e rollback de captura/vínculo/histórico/outbox. Também testa alteração de template e revogação de papel antes do commit.

A suite verifica o snapshot EF e o SQL aditivo/idempotente, gera scripts separados de outbox e versões e executa upgrade/Down vazio/re-upgrade. O teste de bloqueio de Down com capturas exige erro deliberado P0001. A fixture OP declara as colunas chave/valor do estado de regras: a ausência dessas colunas causou corretamente falha de validação, sem desabilitar o validador.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-systems-smoke.mjs` passou: 401/403/404/409, campos obrigatórios do POST, fixação opt-in, renomes apenas nas fichas legadas, fórmulas fixadas, troca/remoção do sistema da campanha, leitura de snapshot restrita ao personagem, cópia/histórico e exportação de conta. Está no CI. OpenAPI inclui o POST `/api/v1/characters/{id}/system-version` e o ID opcional de vínculo no documento/envelope.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/character-systems-integration.mjs` usa duas contas: mestre fixa, jogador recebe resync, campos/fórmulas antigos permanecem após alteração e remoção do sistema da campanha, edição/reload e exportação com definição. Também verifica bloqueio de fixação com nota privada pendente e a cena de câmera OBS mantendo somente HP para D&D fixado após remoção do sistema da campanha. O teste usa o rótulo acessível para aguardar ngModel; `[name]` da diretiva não garante atributo nativo name no input. Capturou 1440×900, 1920×1080, 768×1024 e 390×844, sem overflow horizontal. Desktop/mobile foram inspecionados visualmente; evidências no README de architecture.

Também passaram regressões de manobras/inimigos/encontros e de duas janelas OP/D&D, além de regras, nove fronteiras Node, lint e builds Docker API/Angular. As capturas novas de binding são intencionais; regenerações antigas de regras/OP/D&D ficam fora do commit. Não houve validação de carga ou produção. A etapa de fixação originalmente não certificava migração entre versões; o fluxo foi implementado e testado na seção abaixo. Recuperação do schema de históricos nunca capturados continua indisponível.

Na cena de câmera, a projeção aguarda o modelo antes de mostrar contadores. O teste confere o perfil HP fixado, sem simular webcam ou certificar transmissão WebRTC de vídeo. Essa parte de mídia permanece coberta apenas pelos fluxos existentes, que não foram substituídos.

## Migração assistida entre versões — 09/11/2026

`Chronicles.Architecture.Tests/CharacterSystemMigrationTests.cs` verifica cópias sem mutação, defaults/fórmulas, colisões, ajuste de tipos, schemas não suportados, bloqueio de perda de executor/edição, preservação de tabelas retiradas e digest canônico ligado a ator/estado/plano. Jogador e ficha não fixada são rejeitados.

`Chronicles.Persistence.Tests/CharacterSystemMigrationPersistenceTests.cs` usa o banco temporário da suíte: prévia sem escrita, confirmação concorrente com vencedor único, recibo e estado anterior, retorno por captura antiga, destino obsoleto, permissão revogada dentro do commit e falha forçada após SaveChanges. Verifica rollback de captura/vínculo/campos/recibo/outbox, preservação de inventário/recursos/OBS e restrição de futuras capturas privadas após desvincular a campanha. A cadeia de migrations e seu rollback/re-upgrade continuam passando; não há migration SQL adicional.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/character-migrations-smoke.mjs` verifica contratos HTTP obrigatórios, 401/403/404/409, prévia somente de leitura, validação de ajustes, plano/digest adulterados, confirmação concorrente, retorno e histórico legado. Incluído no CI. A API é reiniciada entre grupos locais do CI para separar quotas das contas descartáveis, sem reduzir rate limits de produção.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/character-migrations-integration.mjs` usa Chrome e duas contas reais no backend local. Verifica bloqueio por nota privada pendente, ação exclusiva do mestre, seleção/mapeamento, confirmação obrigatória, invalidação após editar o plano, valor inválido/correção, persistência após reload, resync do jogador, recibos e retorno à versão original. As quatro capturas são full-page nos viewports 1440×900, 1920×1080, 768×1024 e 390×844; a comparação vira blocos empilhados no celular. Evidências em `docs/visual-validation/architecture`.

Passaram build Docker da API/Angular, lint, dez fronteiras Node, suites .NET de aplicação/regras/persistência e exportação do OpenAPI real com seis caminhos v1. Limitações: não houve execução em produção ou CI remoto, teste de carga, conversor entre edições nem reconstrução de schemas anteriores à captura. Os testes não usam interceptação de API. A configuração local deve usar `ALLOWED_ORIGINS`, conforme Program.cs; variáveis `AllowedOrigins__0` não configuram a aplicação.

## Campanhas, convites e participantes — 10/11/2026

`CampaignApplicationTests` verifica normalização/limites de detalhes, status/papéis, escopo de leitura, owner protegido, formato/hash/expiração de convite e efeitos realtime somente após commit. As onze fronteiras Node incluem a ausência de EF/políticas/geração de tokens nos três adaptadores HTTP de campanha.

`CampaignPersistenceTests`, no banco temporário da suíte PostgreSQL, verifica criação com participação/eventos, leituras por membro, busca/paginação, disputa do mesmo token por pessoas distintas e de tokens distintos pela mesma pessoa, preservação do token não consumido, snapshots obsoletos, concorrência de papéis, no-op sem evento, rollback forçado após SaveChanges, revogação do emissor antes do commit, saída e preservação de personagens. A cadeia de migrations/rollback continua passando sem SQL novo.

`CampaignRoomAccessTests` verifica papel/adesão da conexão, reentrega idempotente, evento atrasado após reentrada com o mesmo papel e falha de transporte depois da remoção do registry. A pendência precisa efetivamente concluir grupo/notificação na próxima tentativa, mantendo uma reentrada autenticada. A suíte de regras existente também passou.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/campaigns-smoke.mjs` valida os contratos antigos, escopo/401/403/404, convites/no-store, concorrência HTTP sem 500, papel, recurso privado, saída e personagem preservado. Incluído no CI junto com `room-smoke.mjs`, que passou com conexões WebSocket reais, permissão de espectador, remoção/promoção e revogação de sessão.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/campaigns-integration.mjs` cria campanha e convite pela interface, abre link sem sessão, verifica login/returnUrl, volta ao convite, recarrega diretamente, aceita, recarrega a campanha, altera papel como owner e sai como participante. Reutilização do link usado é negada. Chrome capturou 1440×900 e 390×844 e não encontrou overflow ou erros JavaScript. Os screenshots estão no README de validação visual. Não há mock/interceptação de API nem alteração de frontend nesta fatia.

Operação local: o Docker Desktop estava parado e foi iniciado; só os containers de validação do Crônicas foram utilizados. Após trocar o container da API, reiniciar o Nginx local atualiza sua resolução do hostname `api` e evita testar contra um IP antigo. O smoke novo tem timeout de rede. Não houve teste de carga, CI remoto, produção ou certificação multi-instância.

## Associações de campanha — 11/11/2026

`CampaignAssociationTests` verifica owner real/papel, 404 de escopo, GUID vazio, comandos tipados, remoção e propagação de conflito. A fronteira Node adicionada impede que Campaign volte a consultar tabelas/repositórios internos de Registry/Worlds ou que as duas rotas voltem aos catálogos. São doze verificações de arquitetura.

`CampaignAssociationPersistenceTests` usa o banco descartável da suíte: destino próprio, snapshots fixados/estado/revisões preservados, no-op, concorrência do mesmo vínculo, mudanças independentes, outbox e rollback após SaveChanges, autoridade revogada no commit. Dois probes tentam FOR UPDATE sob lock_timeout de 150ms enquanto o contrato mantém FOR SHARE; o PostgreSQL deve recusar e a associação deve concluir após liberar o gate. EF encapsula o 55P03 em InvalidOperationException; o teste reconhece só esse erro esperado. A cadeia de migrations/rollback e as demais suítes de persistência passaram.

`SMOKE_BASE_URL=http://127.0.0.1:8082 node scripts/campaign-associations-smoke.mjs` verifica rotas/400/401/404, destinos privados, grant/revogação de leitura do universo, fichas fixadas, estado/revisão intactos, no-op e a disputa HTTP real entre excluir/associar para ambos os tipos. SignalR confere o payload de metadados e a exclusão das fichas fixadas. Incluído no CI.

`ARCHITECTURE_BROWSER_URL=http://127.0.0.1:8082 node web/campaign-associations-integration.mjs` utiliza quatro páginas Chrome com API real: proprietário associa pelos controles existentes, rascunho de ficha e notas privadas são mantidos, recarga explícita obtém novo modelo, troca seguinte limpa recarrega automaticamente e captura fixada permanece inalterada. Confere remoção do sistema, valores/revisões e universo após refresh. Capturas reais desktop/mobile/rascunho estão no README visual, sem mock ou interceptação de API.

Regressões de fixação, migração assistida e OBS passaram por HTTP/Chrome. `sheet-events-smoke.mjs` agora aguarda especificamente `CharacterSheetUpdated` nos testes de revisão de valores: a primeira notificação pode ser `CampaignSystemChanged` com revision null, inclusive de um evento anterior ainda na outbox. O teste mantém a revisão 2/3, igualdade de EventId entre destinatários, ETag, privacidade e revogação; o smoke de associações valida separadamente o novo tipo/payload. As capturas antigas regeneradas pelas regressões ficam fora do commit.

Build nativo .NET e build Angular/Docker passaram, além de lint/aplicação/fronteiras. O download da base mutável atual .NET estava lento; o build Docker de validação da API foi concluído com arquivo local ignorado que conserva as mesmas instruções e fixa os digests já utilizados: SDK `212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`, ASP.NET `1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c`. O Dockerfile versionado não mudou. Não foi certificado o novo conteúdo da tag mutável, CI remoto, carga, replay offline completo ou produção. Revalidar a imagem escolhida no release/staging.
