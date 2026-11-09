# Desenvolvimento local

Requisitos efetivos: Node 24, SDK .NET 10 e Docker com Linux containers. Não são necessários Kafka, Redis ou Kubernetes. Em instalações Windows via dotnet-install, o executável pode estar em `$env:USERPROFILE/.dotnet/dotnet.exe`; em instalações normais use `dotnet` do PATH.

## Compose completo

Copie `.env.example` para `.env`, ajuste uma senha local e execute `docker compose up -d --build`. Web: http://localhost:8081; PostgreSQL: localhost:5433; Mailpit: http://localhost:8025. Não comitar `.env`. A API em Development aplica migrations no banco local do Compose.

Os projetos .NET internos ficam dentro do contexto Docker `api/Chronicles.Api`. O Dockerfile copia seus csproj antes do restore e `.dockerignore` exclui bin/obj recursivamente. Não mudar o contexto do serviço no Render apenas para mover bibliotecas.

## API e Angular fora do container

Exemplo PowerShell, usando o banco local de validação já provisionado (troque por seu banco/segredo local):

```powershell
$env:ConnectionStrings__Default='Host=127.0.0.1;Port=5544;Database=cronicas_rules;Username=cronicas_rules;Password=local-rules-test-only'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:5099'
$env:ALLOWED_ORIGINS='http://127.0.0.1:4200,http://localhost:4200'
dotnet run --project api/Chronicles.Api
```

Em outro terminal: `npm --prefix web ci`, depois `npm --prefix web start -- --port 4200`. O proxy Angular encaminha `/api`, WebSockets e `/health` para 5099. Se SignalR indicar indisponibilidade mas REST funcionar, confira primeiro a allowlist de Origin e o proxy `ws: true`; não desative autenticação.

Health: `/health/live` processo, `/health/ready` e `/health` PostgreSQL. OpenAPI em Development: `/openapi/v1.json`. API v1: GET `/api/v1/rpg-plugins` e GET/PUT `/api/v1/characters/{id}/sheet`, autenticados. O PUT v1 exige `version` no JSON e devolve envelope/ETag atualizados. Rotas `/api` anteriores continuam disponíveis; o PUT legado mantém a versão opcional.

## Ferramentas EF

Instale `dotnet-ef` 10.0.13 em tool path local ou em sua instalação de ferramentas. Configure `ConnectionStrings__Default` explicitamente; a factory de design não carrega credenciais de produção. Projeto de migrations: `api/Chronicles.Api/modules/Chronicles.Infrastructure`; startup: `api/Chronicles.Api`.

```text
dotnet ef migrations add Nome --project api/Chronicles.Api/modules/Chronicles.Infrastructure --startup-project api/Chronicles.Api --output-dir Persistence/Migrations --namespace Chronicles.Api.Migrations
```

Se usar `--no-build`, informe a mesma configuração do build (`--configuration Release` para binários Release); apontar para Debug antigo pode gerar uma migration vazia. Sempre verificar `HasPendingModelChanges` e o SQL no banco temporário antes de publicar. A migration `20261109121911_CharacterSystemVersions` é aditiva, sem backfill; não executar seu Down com capturas. Ver `docs/architecture/releases/README.md` para ordem e limites de rollback.

Nunca suprimir `PendingModelChangesWarning` para fazer uma migration passar. Gere snapshot/migration corretos e execute a suíte de persistência.

## Produção

Não usar Development em produção para contornar a política de migrations. Revisar SQL/backup/staging antes de aplicar a migration de outbox; a nova API para com erro claro se houver migration pendente. A aplicação anterior pode continuar em operação até o release preparado. Nenhuma credencial externa é necessária para desenvolver/testar esta refatoração local.

## Exercitar a migração de versões

Com API/frontend locais e as migrations existentes aplicadas, crie um sistema próprio, associe-o à campanha, crie uma ficha e fixe seu modelo como mestre. Altere o sistema e abra a ficha fixada: Migrar versão da ficha oferece destino atual/arquivado, mapeamentos, ajustes e prévia. Confirmar cria outra revisão; para voltar, escolha a captura anterior no mesmo assistente. Nunca automatize esse procedimento sobre personagens reais. Os scripts e as limitações estão em TESTING e ADR 0005.

O nome efetivo da configuração de origens é `ALLOWED_ORIGINS`, uma lista separada por vírgulas, por exemplo `http://127.0.0.1:8082,http://localhost:8082`. Sem a origem do frontend local, escritas e negociação SignalR pelo navegador recebem 403, mesmo quando chamadas HTTP sem Origin passam. Não remova a checagem de origem para executar os testes.
