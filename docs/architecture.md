# Arquitetura e entregas

## Núcleo atual

- Angular standalone, TypeScript estrito e SCSS com tokens. O frontend chama `/api` na mesma origem da aplicação.
- ASP.NET Core 10 Minimal API, EF Core e PostgreSQL. Cloudflare Pages serve os assets Angular. A Pages Function encaminha `/api/*` por service binding ao Worker que executa a API .NET em um Cloudflare Container. O banco é PostgreSQL gerenciado fora do contêiner e precisa de backups próprios. O Container é efêmero; nenhum dado durável fica em seu disco.
- Cookies HttpOnly, Secure em produção e SameSite Lax guardam sessões aleatórias. O banco armazena somente o hash da chave. Senhas usam `PasswordHasher<T>`, recuperação por código de uso único e sessões antigas são revogadas depois da troca.
- Campanhas exigem associação no servidor. O proprietário pode gerar convites de uso único, válidos por sete dias, para mestre, co-mestre, jogador ou espectador. A API guarda apenas o hash do token do convite e aceita o link em transação com bloqueio da linha para impedir uso duplo. Personagens pertencem a uma campanha e a um usuário. O proprietário da ficha ou um mestre autorizado pode editar; qualquer membro pode consultar os campos públicos atuais. Campos secretos ainda não existem.
- A ficha inicial persiste nome, URLs HTTPS do portrait/fundo, vida, sanidade e mana. A URL de OBS contém uma chave aleatória de 256 bits; seu hash é persistido. Ela revela somente esses dados visuais, pode ser revogada e não dá acesso à campanha.
- RealtimeKit provê WebRTC, compartilhamento de tela e câmera. O backend cria a reunião por campanha, verifica associação e solicita o token do participante com credenciais Cloudflare guardadas no servidor. O token de participante fica apenas na memória do cliente durante a sala. O participante é persistido para reutilização em reconexões. O preset RealtimeKit de jogadores deve permitir câmera e compartilhamento, com `max_screenshare_count` maior que 1 para telas simultâneas; espectadores exigem um preset separado. Não há tabletop, VTT, mapas táticos ou tokens de tabuleiro.
- Migrações aplicam o esquema no início da API; `/health` verifica PostgreSQL. Em produção, migrar em etapa controlada antes de ampliar o número de Containers.

## Cloudflare

`web/wrangler.jsonc` configura Pages e o service binding. `wrangler.jsonc` configura o Worker e a Durable Object que inicia o Container .NET quando necessário. O Worker injeta strings de conexão, origem pública, SMTP e credenciais RealtimeKit via secrets. A primeira requisição após inatividade pode aguardar o início do Container. O Worker contém somente roteamento; a autorização permanece na API.

O armazenamento de imagens ainda aceita URLs HTTPS externas. O próximo passo é upload autenticado em R2 com validação de formato, tamanho, ownership e URLs de leitura controladas. A configuração local continua em Docker Compose para desenvolvimento.

## Fases seguintes

| Fase | Entrega verificável |
| --- | --- |
| 2 | Permissões por recurso mais completas, editor de ficha dinâmica e sistema genérico. Convites e a ficha visual/OBS atual são o primeiro recorte. |
| 3 | Universos, wiki, documentos com visibilidade no servidor, pistas e sessões. |
| 4 | Dados com parser seguro, chat, presença e eventos da sala. Validar mídia com participantes reais, reconexão e cenas individuais para OBS. |
| 5 | Editor de sistemas com fórmulas tipadas, upload R2, personalização de layouts e exportação. Não há módulo de tabletop. |
| 6 | Testes de integração e E2E completos, acessibilidade, auditoria, observabilidade e publicação. |

## Limites atuais

A integração de mídia não foi exercitada sem uma conta, app, token e preset RealtimeKit. O SDK e o UI Kit oficial têm versões fixadas no build Angular e a sala carrega seu bundle sob demanda. Ainda é preciso validar comportamento em Chrome, Firefox e OBS Browser Source. A URL pública de portrait consulta recursos a cada três segundos. A fonte autenticada de câmera + portrait entra na mesma reunião e seleciona o vídeo do dono da ficha; sua reconexão e apresentação em OBS precisam de teste com mídia real. Tokens RealtimeKit duram até 100 dias: quando a remoção de membros for implementada, a API deverá expulsar e excluir o participante correspondente para revogá-los. Outros módulos narrativos continuam planejados.
