# Arquitetura e plano verificável

## Fase 1: fundação

- Angular standalone, TypeScript estrito e estilos SCSS com tokens globais. O frontend só chama `/api`; um proxy local, nginx ou Pages Function encaminha ao backend. Assim, a sessão fica em cookie HttpOnly do mesmo domínio.
- ASP.NET Core 10 Minimal API, serviços de autenticação e persistência via EF Core e PostgreSQL. Os endpoints são pequenos nesta fase; ao ampliar o domínio, extrair regras de campanha para serviços e políticas dedicadas.
- Tabelas iniciais: usuários, sessões, campanhas e participações. UUIDs, índices e chaves estrangeiras estão na migração inicial.
- Senhas com `PasswordHasher<T>` (PBKDF2), sessões com 256 bits aleatórios e apenas hash SHA-256 armazenado no banco. Cookie HttpOnly, Secure em produção, SameSite Lax. Rate limit nos endpoints de login e cadastro. Requisições de escrita exigem JSON, e Origins não autorizadas são recusadas.
- Recuperação de senha por código aleatório de 80 bits, válido por 15 minutos e armazenado apenas como hash. O código é de uso único; a troca da senha revoga as sessões existentes. Mailpit captura emails no ambiente local, enquanto produção exige SMTP configurado.
- A cor de destaque do arquivo pessoal é persistida no usuário e aplicada após login em qualquer dispositivo.
- Toda leitura e escrita de campanha filtra por participação. Apenas proprietário altera detalhes e status. Os papéis de mestre, co-mestre, jogador e espectador serão aplicados nas operações adicionadas nas próximas fases.

## Fluxos críticos da fase

1. Cadastro cria usuário e sessão; o dashboard usa o cookie sem expor credenciais à aplicação JavaScript.
2. Login verifica hash, inicia sessão persistida, logout a revoga.
3. Criação de campanha grava proprietário e participação na mesma transação de `SaveChanges`.
4. Listagem e detalhes nunca retornam campanhas sem participação; edição exige papel de proprietário.
5. Migração aplica o esquema no início da API; `/health` verifica acesso ao banco.
6. Recuperação envia código via SMTP configurado, aceita o código uma vez e revoga sessões anteriores.
7. Preferência de cor é salva no perfil e reaplicada após novo login.

## Próximas fases

| Fase | Entrega verificável |
| --- | --- |
| 2 | Convites, RBAC completo, personagens, ficha persistida e sistema genérico. |
| 3 | Universos, wiki, documentos com visibilidade no servidor, pistas e sessões. |
| 4 | Parser de dados, histórico, chat e sala SignalR autorizada por campanha. |
| 5 | Editor de sistemas com fórmulas tipadas, mapas e exportação. |
| 6 | Testes de integração e E2E completos, acessibilidade, auditoria e publicação. |

## Limites atuais

Convites, personagens, sistemas e conteúdo narrativo ainda não foram implementados. O dashboard exibe apenas informações reais disponíveis. Não há dados fictícios ou ações simuladas. Para produção, configurar backup do PostgreSQL, observabilidade, SMTP transacional e política de retenção antes de abrir o cadastro publicamente.
