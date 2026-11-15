# Barreiras, anti-domínios e gestão de fichas — 15/11/2026

Implementação em `2b32b3ce177216eccef3ecde9d6627d4247a8466`; correção do teste/limpeza do CI em `247b1c142a8ad066a05745ab4b9250eb9b29faa9`; correção de concorrência em `47b4b927cdecc3067790be3fa691d4415bd2cced`.

## Funcionalidades e correções

- Exclusão no topo da ficha, com confirmação pelo nome, cancelamento/Escape/foco e versão da ficha revisada. Permissões são verificadas na aplicação e novamente na transação. Encontros vinculados impedem a exclusão com 409; relações privadas, histórico, inventário, habilidades, favoritos e OBS deixam de existir após uma exclusão aceita. O outbox registra `CharacterDeleted`.
- Busca de personagens ignorando acentos, ordenação por nome/atualização, mensagem sem resultados, recuperação de carregamento e criação abrindo diretamente a ficha.
- Espaçamento e alvos de toque maiores nos controles principais, separação do botão Salvar, estilo dos botões de barreiras e bloqueio da rolagem ao fundo da confirmação.
- Paredes novas e Cortinas com PV individuais, criações coexistentes, dano final/destruição e ação de manipular paredes. Custos e variantes de barreira continuam aplicados.
- Cesta Oca de Vime, pré-requisitos de História/época, bônus/reação, concentração, durabilidade, exposição no começo do turno e selo com duas mãos. Domínio Simples passa a aceitar reação e exposição declarada no começo do turno. Troca/incapacidade/encerramento de concentração limpam o anti-domínio anterior.

Regras consultadas no Livro de Regras 2.5.2, páginas PDF 188–189 e 193. Geometria, condições de Cortinas e efeitos de acerto garantido nos alvos são resolvidos pela mesa. O texto consultado não define a categoria de ação de Cortina; o executor exige uma ação explicitamente acordada. A [cobertura de F&M](../../feiticeiros-maldicoes.md) permanece parcial. A [gestão de fichas](../../character-management.md) detalha o comportamento de exclusão.

Não há migration SQL, alteração de snapshots ou reescrita de campanhas. As escolhas usam `aptidoes` e os indicadores usam o estado protegido existente. Paredes de ações antigas que não tinham PV rastreados não são reconstruídas automaticamente.

## Validação

Build Angular, lint, build Release da API sem avisos, testes de regras dos cinco sistemas, arquitetura .NET/Node e verificação dos cinco modelos passaram localmente. A suíte nova verifica PV separados, custos/ações, reação fora do turno, selo/exposição e concentração. A política de exclusão verifica membros, proprietários/mestres e versão revisada.

Os navegadores usam fixtures isoladas: quatro resoluções F&M, escolhas/salvamento, barreiras/Cortinas, reação/selo, leitura; busca/ordenação, criação, exclusão/cancelamento/Escape/foco, conflito de versão e retorno ao elenco. A confirmação foi inspecionada em desktop e celular; o teste mede alvos e espaçamento no celular. As regressões de login também passaram. Capturas em `docs/visual-validation/quality/character-delete-*.png` e `docs/visual-validation/rules/feiticeiros_maldicoes-barreiras-*.png`.

O primeiro [CI 43764222699](https://github.com/Kilop206/Cronicas/actions/runs/43764222699) encontrou uma corrida no teste de ordenação, que lia o DOM antes da atualização. O teste passou a esperar a ordem visível esperada. A limpeza do CI agora informa `.env.example` mesmo quando a integração ainda não criou `.env`.

O [CI 43764626108](https://github.com/Kilop206/Cronicas/actions/runs/43764626108) aprovou navegadores, regras, arquitetura e migrations, e encontrou um erro 500 em uma de duas exclusões concorrentes. A correção reconhece conflitos conhecidos na cadeia de exceções; a estratégia padrão do [Npgsql 10.0.3](https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlExecutionStrategy.cs) pode encapsular falhas transitórias em `InvalidOperationException`. A resposta é de conflito, permitindo revisar a ficha novamente.

O [CI final 43765847601](https://github.com/Kilop206/Cronicas/actions/runs/43765847601) concluiu com **sucesso**: build/lint, três suítes de navegador, regras dos cinco sistemas, arquitetura, migrations/outbox e todos os smokes HTTP/PostgreSQL. `CharacterDeletionPersistenceTests` força SQLSTATE 40001, verifica rollback de ficha/filhos/outbox, preserva erros desconhecidos e confere exclusão concorrente, cascatas e um único evento durável. O smoke HTTP verifica versões, permissões, vínculo com encontro e concorrência. O smoke F&M verifica paredes/Cortinas, exposição/selo da Cesta e persistência do estado.

## Publicação

| Destino | Identificador | Verificação |
| --- | --- | --- |
| Render `cronicas-api` | `dep-dbt2lcu7bikc73cs8nf0`, commit `47b4b92` | `live` em 15/11/2026 às 21:57:56 UTC; `/health/ready` 200 `Healthy` |
| Cloudflare Pages `cronicas` | `bc3b78b0.cronicas-bsy.pages.dev`, branch `main`, commit `47b4b92` | `https://cronicas-bsy.pages.dev` verificado |
| Worker `cronicas-fallback` | `5a50fd99-a752-40a9-b81f-fa6d017b2fe1` | `https://cronicas-fallback.guilhermextra118.workers.dev` verificado |

Bundle publicado: `main-O4DUEF4G.js`, `chunk-J5TDYAQ6.js` e `styles-JC7ARHXT.css`. SHA-256 do `index.html` local: `50CE6764AB788CB3C3CCD30215D8ECCF1DDBBF482C4BAD1224F9477ABF2011BF`. Durante o envio, somente capturas e este registro estavam sem commit; os fontes publicados correspondem ao commit aprovado. O Render mantém deploy automático desligado.

Nos dois domínios, `/login` e o bundle novo retornaram 200, com as funcionalidades novas presentes e sem erros JavaScript observados. Sessão/catálogo autenticado retornaram 401 anonimamente. Uma requisição anônima de exclusão com GUID nulo retornou 401 em ambos, sem acessar ficha pessoal. Uma tentativa de login com identidade aleatória `example.invalid` por domínio retornou 401 JSON e “Email ou senha incorretos.”, mantendo o formulário e sem erro de navegação.

Os testes autenticados e de exclusão usaram somente bancos/contas sintéticos isolados do CI. Nenhuma ficha pessoal, conta ou campanha foi criada ou apagada em produção. A consulta de erros de aplicação entre 21:57:56 e 22:02:44 UTC não retornou entradas; isso não comprova ausência de erros fora da janela nem certifica todas as combinações das regras ou uma futura hibernação do serviço.
