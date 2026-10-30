# Auditoria das regras nos PDFs locais

Os PDFs na raiz do projeto foram consultados localmente em 28/10/2026. Permanecem fora do Git e do site. O código guarda apenas campos funcionais, fórmulas curtas e comportamento de rolagem; não incorpora texto dos livros, tabelas extensas, ilustrações, fichas originais ou opções de personagem.

| Sistema e edição identificada | Material consultado | Mecânica incorporada nesta etapa |
| --- | --- | --- |
| Ordem Paranormal RPG, 3ª edição v1.3 (dezembro de 2024) | `ordem-paranormal-rpg-v1-3-lyfxjj (1).pdf`, capítulos de personagem e testes; ficha automática OP como conferência de campos | Atributo 0 usa 2d20 e conserva o menor; atributos positivos usam quantidade de d20 igual ao valor e conservam o maior. Defesa já derivava de 10 + AGI + modificadores. O template v5 acrescenta classe estruturada e sugere PV, PE e SAN básicos por classe e NEX, incluindo o avanço de 95% para 99%. Aplicar os valores é uma ação explícita. |
| D&D 5e, edição 2014 | `dd-5e-livro-do-jogador-fundo-branco-biblioteca-c3a9lfica.pdf`, criação de personagem e avanço; ficha automática como conferência de campos | Modelo novo marcado como 2014 (v2), com modificadores de atributo, proficiência, resistências e perícias já calculados. A ficha permite editar XP e mostra o limiar para o próximo nível da edição 2014. O modelo v1, marcado 2024, continua disponível para fichas existentes e não herda regras específicas de 2014. |
| Tormenta20, Jogo do Ano, 2ª edição, 3ª impressão v1.3 (outubro de 2024) | `T20 - Livro Básico.pdf`, capítulos de personagem, perícias e combate; ficha automática Jogo do Ano como conferência | Modelo v2 calcula bônus de treinamento por faixa (2/4/6), perícia = metade do nível + atributo + treino e Defesa = 10 + DES + armadura + escudo + outros. O botão T em cada perícia altera o treino e dispara salvamento. |
| Call of Cthulhu, 7ª edição | `chamado-de-cthulhu-livro-básico 7E.pdf`, criação do investigador; `Guia do Investigador - 7E.pdf` e ficha editável moderna como conferência | Além de metade/quinto e testes percentuais existentes, o modelo sugere PV inicial = piso((CON + SIZ)/10), PM inicial = piso(POW/5) e SAN inicial = POW. O jogador aplica os valores à ficha explicitamente durante a criação. A v3 calcula bônus de dano e porte a partir de FOR + TAM e permite rolar o bônus de dados na sessão. |

O PDF `chamado-de-cthulhu-livro do guardião-7-edicao.pdf` foi localizado, mas nenhuma regra exclusiva dele foi adicionada. `pdfcoffee.com_t20-ameaas-de-arton-pdf-free.pdf` é um suplemento de criaturas; não foi usado para adicionar NPCs ou ameaças automaticamente. As fichas editáveis foram usadas apenas para conferir agrupamento de campos, não como fonte de cálculo.

## Versionamento e compatibilidade

Os testes da ficha especializada de Call of Cthulhu 7e classificam 01 como crítico e 100 como falha crítica; com alvo inferior a 50, 96–100 são falhas críticas. Os demais resultados mantêm os graus regular, difícil e extremo. A classificação usa o alvo enviado ao teste; a interface atual usa dificuldade regular. A conferência foi feita no capítulo de sistema de jogo do PDF local, páginas impressas 89–90. Consequências de falhas críticas continuam sob decisão do Guardião.

Um sistema salvo na campanha conserva `templateId`, `templateVersion` e `rulesetVersion`. As versões antigas continuam no registro de renderizadores. O Crônicas não altera automaticamente a edição nem converte bônus de perícia gravados em fichas antigas para fórmulas novas. Ativar o visual especializado de um modelo já associado preserva a versão existente; associar outro modelo é uma ação explícita do dono do sistema.

Na edição de um sistema existente, o dono pode preparar a atualização de Ordem Paranormal, Tormenta20 ou Call of Cthulhu para o template mais recente e revisar os campos antes de salvar. A API migra e recalcula os personagens com histórico de alterações. Em Tormenta20, a atualização explícita substitui os bônus manuais de perícia e Defesa por fórmulas; os valores anteriores ficam no histórico. A edição 2024 marcada no antigo template de D&D não é convertida para a edição 2014, pois são regras distintas.

As fórmulas novas passam pelo mesmo interpretador limitado no cliente e na API. A função `min(a,b)` foi acrescentada à lista permitida para representar os patamares de Tormenta20 sem código executável. O servidor recalcula valores derivados ao salvar; o cliente mostra o resultado enquanto se edita.

## Regras ainda não automatizadas

Em 30/10/2026, os apêndices de condições foram conferidos integralmente: Ordem, páginas impressas 310–312, e Tormenta20, 394–395. O motor agora distingue penalidades em dados e penalidades numéricas, mantém durações explícitas, resolve efeitos de turno e integra prévias de testes, Defesa e OBS. As regras de conjuração adversa foram conferidas em Ordem p. 119 e Tormenta20 p. 170–171. A ambiguidade de atributo zero com penalidade em Ordem exige decisão explícita da mesa, documentada em `rules-engine.md`.

Atualização de 30/10/2026: o motor de ações no servidor acrescentou dano, recuperação, morte, custos, conjuração por espaços, multiclasse, ferimentos, sanidade e desenvolvimento. Consulte a cobertura e as pendências atuais em [rules-engine.md](rules-engine.md). A lista abaixo registra o ponto de partida da auditoria e não substitui esse acompanhamento.

- Ordem Paranormal: totais básicos de PV, PE e SAN agora são sugeridos; bônus de origem, poderes, transcendência, trilhas, rituais, dano e condições exigem escolhas e exceções que não podem ser inferidas apenas do nome da classe. Personagens existentes preservam seus recursos até o jogador selecionar a classe estruturada e aplicar os valores.
- D&D 5e: PV por classe, equipamentos, magia por classe, talentos, ações e progressão completa; diferenças exclusivas de 2024 seguem sem implementação neste material de 2014.
- Tormenta20: PV/PM por classe, treino concedido por classe/origem, restrições de perícias, penalidades de armadura, ataques e poderes. Outros modificadores de perícia continuam configuráveis pelo autor do sistema.
- Call of Cthulhu: criação por ocupação/idade, movimento, desenvolvimento, sorte opcional, ferimentos e sanidade máxima por Mitos. O bônus de dano exibido aplica-se a combate corpo a corpo; a ficha não o acrescenta automaticamente a ataques cadastrados. O botão de valores iniciais não deve ser usado para redefinir um investigador já progredido.

Essas regras devem entrar em versões novas dos templates, com campos estruturados para as escolhas necessárias e testes de migração antes de atualizar fichas existentes.
