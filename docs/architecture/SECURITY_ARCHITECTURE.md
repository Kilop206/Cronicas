# Segurança e confiança

## Identidade e autorização preservadas

Sessões usam cookie HttpOnly, Secure fora de Development, SameSite=Lax e tokens aleatórios de 256 bits; banco guarda hash SHA-256, expiração e revogação. PasswordHasher existente permanece. Autenticação acontece antes da partição de rate limit por conta. O proxy assina a identidade do visitante; headers não assinados não podem trocar a partição para contornar limites. O teste dessa regressão continua na suíte de regras.

Conhecer um UUID não concede acesso: o novo store consulta personagem condicionado à participação da campanha. Um membro sem edição pode ler a mesma visão já permitida pelo contrato anterior; proprietário e owner/gm/co-gm editam, spectator não edita nem o próprio personagem. Novas ações revalidam revisão, papel e sistema antes de commit. A ativação de sorte opcional e rolagens GM exigem mestre no backend.

Campos `editRole: gm` restringem escrita, não prometem sigilo de leitura. Segredos usam recurso separado protegido. Não tratar esses campos como um mecanismo novo de ocultação nem publicar estados completos via eventos.

## Realtime

Novo hub `/api/hubs/sheets` exige cookie/sessão ativos na inscrição e participação na campanha. Cada conexão acompanha no máximo uma ficha. Em cada entrega, o handler consulta novamente expiração/revogação/participação; remover membro bloqueia notificações mesmo se o socket continuar aberto. A mensagem só leva `eventId`, `type`, `characterId`, `revision`. A API continua autorizando o refetch.

Origins de todos os hubs são comparados à allowlist; REST mantém a proteção existente de origens/métodos e rejeição de formulários. Configure os endereços reais de desenvolvimento/produção em `ALLOWED_ORIGINS`. Não liberar `*` com credenciais.

## Concorrência e rascunhos

409 não é convertido em sobrescrita automática. O editor pausa salvamento ao receber revisão remota durante edição, preserva rascunho e permite exportação antes de recarga. Navegação/fechamento com alterações pendentes têm confirmação de perda de dados. Reconexão busca versão atual quando não há edição local.

Desde 09/11/2026, criação/salvamento comuns usam casos de uso com autorização e validador do plugin, além das ações de regras. O store revalida acesso e schema no commit. A revisão é obrigatória no contrato v1 de escrita; a omissão continua permitida no PUT legado apenas por compatibilidade. A leitura individual não executa regras, permitindo consultar/corrigir estado antes de acionar o motor.

Lista por campanha exige participação e calcula `canEdit` por proprietário/papel. Histórico/restauração exigem dono ou mestre, excluindo espectadores; acesso indevido retorna 404. Uma revisão de outro personagem não pode ser aplicada. Restaurar verifica campos `editRole: gm` contra os valores atuais, inclusive presença/remoção; possuir uma revisão antiga não autoriza reverter uma decisão atual do mestre. Duplicar exige dono ou mestre, revalida o contexto na transação e não transporta segredos, favoritos nem a capacidade de OBS. Esses fluxos têm testes de aplicação, PostgreSQL e HTTP reais.

Inventário/habilidades usam casos de uso com a mesma matriz: membros leem; dono ou mestre escreve; espectador não escreve; não membro recebe 404. IDs de filhos são vinculados ao personagem em toda consulta/mutação. A transação de escrita bloqueia participação/personagem e reconsulta acesso, impedindo que uma revogação concluída após a leitura inicial seja ignorada no commit. Campos de valores não aceitam propriedade, campanha, IDs ou revisão enviada como novo estado confiável. Expressões de habilidades seguem o parser seguro, sem código executável. Pesos precisam de duas casas decimais e categorias/quantidades/textos preservam seus limites.

Favoritos exigem participação, mas não edição da ficha: espectadores podem manter sua lista pessoal. Notas privadas exigem owner/gm/co-gm da campanha, mesmo para o dono do personagem, e acesso indevido retorna 404. O PUT suporta `expectedExists` opcional; o editor atual o envia para impedir a sobrescrita entre ausência e primeira gravação, ambas historicamente revisão 1. Clientes legados sem esse campo mantêm essa limitação inicial.

Somente o dono autenticado, participante e não espectador gera/rotaciona a chave OBS. A capacidade permite ler só a projeção pública; não dá acesso à ficha completa, campanha, campos não selecionados ou segredos. A chave em claro não é persistida; hash e fonte são isolados por portas. Rotação só devolve a nova chave após commit. Como no contrato anterior, remover/rebaixar um membro impede nova rotação, mas não revoga automaticamente uma capacidade já emitida; ela permanece válida até substituição do hash. Não confundir a URL de OBS com sessão de usuário. Notas/chaves têm Cache-Control private,no-store e a fonte tem no-store.

## Código e dados não confiáveis

Fórmulas e dados usam parsers existentes com operadores/limites explícitos, sem eval. Schema/template é JSON declarativo; não instala assembly, script, componente remoto ou código no backend. Plugins executáveis desta entrega são compilados e revisados junto ao servidor e bundle Angular. Estratégia para terceiros está no PLUGIN_CONTRACT.

Não foi criado serviço de upload: imagens/fundos continuam URLs HTTPS limitadas. Limites de upload, antivírus, URLs assinadas e ACL de objetos deverão acompanhar uma implementação real de Media, sem declarar proteção inexistente.

## Auditoria e observabilidade

Histórico existente e eventos metadados acompanham transações. Middleware próprio registra rota parametrizada, status, duração e correlação; não registra corpo, query, cookies ou tokens de convite/OBS. Scopes automáticos de console ficam desabilitados para não incluir RequestPath cru. EF não usa sensitive-data logging. Nunca adicionar segredos de Render/Cloudflare/banco aos documentos/fixtures.

Erros de binding/JSON do cliente preservam seu status HTTP e, para 400, usam erro de validação estruturado; não viram 500 por causa do handler central. O cabeçalho de correlação é aplicado em OnStarting, inclusive quando o tratamento de exceções limpa a resposta. O teste HTTP cobre esse caso.

## Pendências explícitas

Revisar todos os endpoints legados com matriz IDOR/papel, incluir testes adicionais por recurso e separar autorização de EF. Não há pentest externo nem isolamento de processo para plugins comunitários. Não há uploads/marketplace público para certificar. Medidas contra negação de serviço devem ser acompanhadas por métricas reais; não reduzir limites apenas por pressuposto, preservando a correção do problema de “muitas tentativas”.

Na instalação limpa do Docker foram reportados 22 advisories moderados na árvore de desenvolvimento. A auditoria de dependências de runtime (`npm audit --omit=dev`) retornou zero vulnerabilidades em 05/11/2026. Não foi executado upgrade forçado de toolchain; a dívida de desenvolvimento permanece explícita.
