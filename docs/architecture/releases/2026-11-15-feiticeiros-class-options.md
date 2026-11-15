# F&M: estilos, Golpe Especial e Fundamentos — 15/11/2026

## Escopo

Commit `3bf5cf387e91656d46f163ae177e31eb2727bca1`.

- Oito estilos básicos de Combate, com escolhas por nível, declaração de mãos/propriedades, escala, reações e rerrolagem de Massivo que aceita o resultado novo.
- Onze opções de Golpe Especial, ações, custos, repetições, margem crítica e temporários exclusivos de Autossuficiente. Renovações por crítico/eliminação usam confirmação única do último alvo inimigo.
- Sete mudanças de Domínio dos Fundamentos, com custos, limites, CD/acerto, ação e rerrolagem. Rituais preservam escolhas e atributo até a conclusão.
- Acervo Amplo, Expansão dos Fundamentos e Adeptos de Combate/Feitiçaria: pré-requisitos, escolhas concedidas e desconto limitado por cena. Somente o estilo concedido pelo Adepto usa o nível total do personagem.
- Dano recebido com quantidade explícita de RD ignorada e resistência ignorada, preservando imunidade, vulnerabilidade, temporários e concentração.
- Foco da Destruição conta os dados adicionais do crítico sem dobrar os bônus fixos.

Fontes: Livro de Regras 2.5.2, páginas PDF 63–65, 71, 78–79, 87, 166 e 365. A ficha usa resumos autorais e o índice de referência; os PDFs não são distribuídos. A [cobertura detalhada](../../feiticeiros-maldicoes.md) continua parcial.

## Compatibilidade e limites

As escolhas usam `aptidoes`, já existente no schema 1. Não houve migration SQL, alteração de snapshots nem reescrita de campanhas. Bônus manuais que já representavam um estilo devem ser removidos pelo usuário ao habilitar sua execução para evitar duplicação.

Distância, área, segundo alvo, sangramento, empurrão e efeitos em aliados são informados para resolução pela mesa; não há transação automática entre fichas. Sacrifício exige confirmar os 15 de dano previamente resolvidos com a mesa. Rápido com ritual e o custo por nível de Rápido/Duplicado na Técnica Máxima exigem adjudicação e são recusados pelo executor nesta etapa. Orçamento geral de talentos e tempo para trocar escolhas não são certificados.

## Validação

- Build Angular e lint; API Release sem erros ou avisos.
- Regressões de regras dos cinco sistemas, incluindo `FeiticeirosClassOptionsTests`, e arquitetura .NET/Node.
- Catálogo/schema real, projeção da Defesa com estilo de classe/Adepto e contratos de proxy/rotas/qualidade.
- Fixtures de navegador em quatro resoluções: autosave, orçamento de estilos, declaração de ataque, Golpe Especial, defesas ignoradas, Fundamentos, concessão/remoção de Adepto, rituais, erros, impressão e acesso somente leitura.
- Regressões do login, incluindo falhas de rede, resposta de desafio do provedor e corrida da verificação de sessão cancelada.
- CI de integração: [execução 43731102024](https://github.com/Kilop206/Cronicas/actions/runs/43731102024), concluída com sucesso, incluindo migrations/outbox e todos os smokes HTTP/PostgreSQL.

As fixtures não demonstram persistência no banco. O smoke HTTP/PostgreSQL do CI usa contas sintéticas e banco descartável, incluindo custos de Golpe Especial, Defesa, ledger, adulteração e conflito de revisão. Nenhuma conta pessoal ou campanha de produção foi usada nesses testes.

## Publicação

| Destino | Identificador | Resultado |
| --- | --- | --- |
| Render `cronicas-api` | `dep-dbst78mi0phs73e31cs0`, commit `3bf5cf3` | `live` em 15/11/2026 às 15:46:46 UTC; `/health/ready` 200 `Healthy` |
| Cloudflare Pages `cronicas` | `55cda6e2.cronicas-bsy.pages.dev`, branch `main`, commit `3bf5cf3` | Principal `https://cronicas-bsy.pages.dev` verificado |
| Worker `cronicas-fallback` | `17f633ed-74a7-40d2-bdec-e303ac219576` | `https://cronicas-fallback.guilhermextra118.workers.dev` verificado |

Bundle publicado: `main-ZRD4EIZ6.js` e `chunk-7B75Z6RG.js`; SHA-256 do `index.html` local: `56E80611DA2B549B6925677C344AC7A014C9427C23BA71B390789722F03BB42B`. O arquivo de registro desta entrega estava sem commit durante o envio; os fontes do bundle correspondem ao commit aprovado.

Nos dois domínios, `/login` e os bundles retornaram 200, sem erros JavaScript observados. A consulta anônima de sessão e do catálogo autenticado retornou 401. Uma tentativa por domínio com identidade aleatória `example.invalid` retornou 401 JSON e “Email ou senha incorretos.”, mantendo o formulário e sem erro de navegação. Não houve uso de credenciais pessoais nem criação de conta em produção.

A consulta de logs de aplicação de severidade `error`, entre 15:46:46 e 15:48:05 UTC, não retornou entradas. Isso não comprova ausência de erros fora dessa janela, nem certifica login após uma futura hibernação do serviço. A verificação pública confirma entrega e disponibilidade; as regras autenticadas foram verificadas no ambiente isolado do CI.
