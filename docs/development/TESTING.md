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
