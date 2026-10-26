# Arquitetura e entregas

## Núcleo atual

- Angular standalone, TypeScript estrito e SCSS com tokens. O frontend chama `/api` na mesma origem da aplicação.
- ASP.NET Core 10 Minimal API, EF Core e PostgreSQL. Cloudflare Pages serve os assets Angular. A Pages Function encaminha `/api/*` por HTTPS à API .NET no Render Free. O banco Neon é independente do serviço web e precisa de backup externo. O disco do Render é efêmero; nenhum dado durável fica nele.
- Cookies HttpOnly, Secure em produção e SameSite Lax guardam sessões aleatórias. O banco armazena somente o hash da chave. A conta lista sessões ativas e permite encerrar acessos antigos; logout, revogação e troca de senha retiram suas conexões da sala. Senhas usam `PasswordHasher<T>` e recuperação por código de uso único.
- Campanhas exigem associação no servidor. O proprietário pode gerar convites de uso único, válidos por sete dias, para mestre, co-mestre, jogador ou espectador. A API guarda apenas o hash do token do convite e aceita o link em transação com bloqueio da linha para impedir uso duplo. Proprietários podem alterar papéis e remover participantes; a API revoga o participante de mídia associado.
- Personagens pertencem a uma campanha e a um usuário. O proprietário da ficha ou um mestre autorizado pode editar e duplicar; qualquer membro pode consultar os campos públicos. Campanhas novas não recebem sistema padrão. Sem sistema, ou com sistema de ficha livre, os campos manuais são validados por tipo primitivo, tamanho e chave. Sistemas estruturados definem campos tipados e um layout versionado no JSON existente: até oito páginas, blocos em grade de 12 colunas ou posicionamento livre percentual, imagem HTTPS, proporção, estilos e cor. O servidor limita posições aos limites da página e URLs de fundo a HTTPS. Os blocos de recurso podem apontar para campos numéricos do sistema. O editor visual e o JSON manual são validados no servidor sem executar código do usuário. Esquemas antigos armazenados como array de campos continuam legíveis. A ficha tem controle de versão, histórico e restauração. Inventário e habilidades são persistidos com versões e permissões da ficha; a duplicação copia ambos. Expressões de habilidades passam pelo parser seguro de dados e rolam no histórico da campanha. A impressão do navegador permite salvar PDF. Anotações secretas ficam em tabela e endpoints exclusivos para mestres; campos secretos dinâmicos definidos pelo sistema ainda não existem.
- NPCs têm fichas simplificadas com aparência, personalidade, motivação, relações, localização, vida e estado narrativo. Somente mestres criam e editam; jogadores recebem apenas NPCs publicados e nunca o campo de segredos. A lista é paginada, busca por nome e controla versões nas edições. NPCs podem participar de encontros; quando o encontro é ativado, a iniciativa revela nome e recursos aos jogadores, inclusive de NPCs privados, sem revelar os segredos da ficha. Alterações de vida no encontro atualizam o NPC.
- A URL de OBS contém uma chave aleatória de 256 bits; seu hash é persistido. Ela revela somente nome, portrait, fundo e recursos, pode ser revogada e não dá acesso à campanha.
- Universos podem ser associados a várias campanhas. Artigos possuem visibilidade de campanha, privada ou de mestre; relações persistidas oferecem backlinks e respeitam a visibilidade de ambas as pontas. Documentos, sessões e wiki da campanha possuem edição autorizada e revelação de pistas. Dados são analisados por parser restrito, rolados com geração criptográfica e guardados com idempotência; chat e encontros também são persistidos.
- A pesquisa global consulta campanhas, personagens, NPCs, universos, artigos e registros narrativos com filtros de autorização aplicados na consulta ao banco.
- A sala usa SignalR para presença e sinalização WebRTC, com associação à campanha verificada no servidor. Cada navegador estabelece conexões diretas por participante usando STUN público. Câmeras oferecem perfis de 360p a 720p; telas usam 15 quadros por segundo. Cada fonte OBS indica um personagem, e o servidor só autoriza sinalização com o dono dele. Essas conexões negociam apenas recepção da câmera, evitando enviar áudio e tela a fontes que não usam essas faixas. O registro de presença em memória atende à instância única do Render Free; revogação de associação ou papel remove as conexões do grupo. OBS entra como conexão somente de leitura e compõe câmera, portrait e recursos. Não há tabletop, VTT, mapas táticos ou tokens de tabuleiro.
- Migrações aplicam o esquema no início da API; `/health` verifica PostgreSQL. Antes de ampliar o número de instâncias, migrar em etapa controlada.

## Publicação

`web/wrangler.jsonc` configura Pages. `API_ORIGIN` aponta a Pages Function para uma origem HTTPS fixa do Render. `render.yaml` configura um Web Service Docker Free com deploy automático desligado; a conexão com Neon e a origem permitida são preenchidas como secrets no Render. O Render Free hiberna após inatividade, de modo que a primeira requisição pode demorar. A autorização permanece na API. Cloudflare Containers foram removidos do caminho ativo para evitar Workers Paid.

O armazenamento de imagens ainda aceita URLs HTTPS externas. O próximo passo é armazenamento configurável de arquivos, com upload autenticado, validação de formato e tamanho, ownership e URLs de leitura controladas. A configuração local continua em Docker Compose para desenvolvimento.

## Fases seguintes

| Fase | Entrega verificável |
| --- | --- |
| 2 | Campos secretos dinâmicos, modelos de ficha e permissões mais granulares. |
| 3 | Links internos no texto com autocompletar, anexos e mídia de documentos, pistas associadas a sessões. |
| 4 | Validar mídia com participantes reais, reconexão, múltiplos compartilhamentos e cenas individuais para OBS; sincronização de eventos da campanha. |
| 5 | Fórmulas tipadas e progressão para sistemas, imagens e tipografia configuráveis por ficha, upload autenticado. Não há módulo de tabletop. |
| 6 | Testes de integração e E2E completos, acessibilidade, auditoria, observabilidade e publicação. |

## Limites atuais

A mídia WebRTC ainda precisa de teste real com dois navegadores, reconexão e OBS Browser Source. Sem TURN, algumas redes e firewalls impedem conexão direta. Cada participante envia um fluxo para cada outro, então o uso de CPU e banda cresce com o tamanho do grupo. O hub mantém presença em memória e exige uma única instância; escalar requer registro compartilhado. A fonte pública de portrait consulta recursos a cada três segundos. Mapas táticos e tabletop estão fora do escopo.
