# Feiticeiros & Maldições 2.5.2

O catálogo agora oferece **Feiticeiros & Maldições 2.5.2**, com template v1, renderizador próprio e executor independente `FmRulesPlugin`. A implementação usa os PDFs locais **Livro de Regras v2.5.2** (368 páginas) e **Grimório das Maldições v1 para F&M 2.5** (80 páginas). As páginas abaixo são as páginas do PDF. Os PDFs e suas ilustrações não são publicados pelo aplicativo.

**A cobertura ainda não é integral.** O índice de 437 nomes é uma ferramenta de localização, não uma implementação dos efeitos de 437 habilidades. O Grimório ainda não tem um executor completo de inimigos. Os campos de registro e as referências não devem ser apresentados como automação das regras que faltam.

## Como usar

1. Em **Sistemas → Criar sistema**, escolha o modelo Feiticeiros & Maldições 2.5.2, salve e associe-o à campanha.
2. Crie o personagem. Em **Editar ficha**, escolha origem, especialização, atributo da técnica e atributo-chave compatível com a especialização.
3. Opcionalmente, use a criação guiada em **Progressão** antes dos recursos iniciais. Distribua os valores na tabela e conclua. A ficha também conserva o fluxo de edição manual existente.
4. Distribua os treinamentos e escolhas; marque somente as aptidões adquiridas. Use **Aplicar recursos iniciais** uma única vez.
5. Use os botões da ficha ou o painel **Ações e recursos**. As ações aguardam o autosave e enviam a revisão atual. Ao evoluir nível/atributos/bônus, **Recalcular progressão** preserva os gastos.

As abas são Feiticeiro, Combate, Perfil Amaldiçoado, Progressão, Equipamento, Regras e índice, e Crônica. A ficha continua usando inventário, habilidades, histórico, impressão e OBS do produto.

## Cobertura executável

| Bloco | Implementação e fonte |
|---|---|
| Criação de atributos | Valores fixos; seis rolagens de 4d6 descartando o menor; compra com orçamento 17 e tabela específica de F&M. Confere conjunto, duplicatas, limites, bônus das sete origens e atributos elegíveis dos quatro clãs. Não aplica escolhas de talentos. PDF 17–19, 27–39. |
| Recursos e progressão | PV iniciais e ganhos fixos das seis especializações, Constituição retroativa, PE/Estamina, modificador inicial e multiclasse com pré-requisitos. Gojo acrescenta PE em níveis pares; Kamo recebe PV na progressão. Bônus/rolagens adicionais são registrados em campos explícitos. PDF 20–21, 30–31, 44–47. |
| Perícias e resistências | Vinte e uma linhas incluindo dois Ofícios e perícias complementares; cinco resistências; treino, maestria, vantagens/desvantagens e graus de TR. Perícias que exigem treino são bloqueadas na ação comum. PDF 276–288. |
| Combate | Orçamento comum/bônus/movimento/reação, ação completa, conversões, bloqueio por condições, início/fim de turno e sequência recebida. Ataques usam declaração do tipo e do estado do alvo. PDF 300–310. |
| Dano | Quinze tipos, RD por fonte, resistência, imunidade, vulnerabilidade ×1,5, dano antes/depois do ataque, crítico que dobra dados e não bônus fixos, munição carregada, camuflagem declarada. Dano é informado/aplicado ao alvo em ação separada; não há transação entre duas fichas. PDF 305–316, 365. |
| Alma e morte | TR de Integridade antes do dano; redução de PV atuais/máximos e integridade; estágios da alma; déficit negativo, falhas de morte persistentes, estabilização e morte massiva. Cura da alma registra uma fonte habilitada confirmada pela mesa. PDF 311–314. |
| Condições | Relações derivadas, imunidades específicas, penalidades numéricas, restrições de ação, sangramento, concentração e exaustão 0–6. Efeitos contra uma fonte particular precisam de contexto da mesa. PDF 211, 317–324. |
| Descansos | Recuperação curta/longa de PE ou Estamina, dados de vida, falhas de morte, exaustão e metade da perda de máximo causada por dano na alma. Descanso com fabricação tem recuperação reduzida. Tempo de jogo é confirmado, sem relógio real. PDF 335, 311, 324. |
| Especializações | Recursos básicos, parte dos efeitos concedidos de Lutador/Restringido, antecipação de níveis de feitiço de Técnica, Conjuração Aprimorada, Foco Amaldiçoado e O Honrado, cura-base de Suporte e sua concessão de Energia Reversa no nível 6. Demais características e escolhas individuais não estão todas executadas. PDF 49–51, 78–80, 102–103, 114–115. |
| Empolgação | Início, crescimento/queda por turno, teto 5, dados básicos/Máxima e início especial de Lutador 20. Escolha de manobras com orçamento por nível real; Ajuste, Desarme, Comando, Esquiva e Trabalho de Pés com um uso por rodada. Bônus de Defesa expiram no começo do próximo turno. Acertos de manobras externas são confirmados pela mesa. Comando cobra o PE do comandante e informa a reação do aliado; não executa o ataque de outra ficha. PDF 49–51. |
| Corpo Treinado / Superior | Ataque bônus desarmado após ataque marcial/desarmado; ataque livre de Lutador 20 por 2 PE, uma vez por rodada; dados desarmados e bônus de Fortitude de Gosto pela Luta. Escolha explícita no contexto do ataque. PDF 49–51. |
| Preparo | Reserva pelo nível efetivo de Combate + SAB; gasto preservado no recálculo, recuperação por análise/eliminação confirmada/descansos. Arremesso Ágil, Distração Letal, Execução Silenciosa, Golpe Descendente e Investida Imediata com custos e pré-requisitos. Movimento e efeitos na ficha-alvo são informados para aplicação pela mesa. PDF 64. |
| Repertório de Combate | Oito estilos básicos; orçamento de um estilo inicial e mais um em níveis reais 6 e 12. Defensivo, Arremessador, Duelista, Distante, Duplo e Massivo alteram Defesa/acerto/dano sob declaração de mãos e propriedades. Massivo rerrola cada 1/2 uma vez e aceita o novo resultado, inclusive pior. Interceptador/Protetor gastam a reação e informam a redução/vantagem para resolver no aliado. Escala pelo nível efetivo. PDF 63–64. |
| Golpe Especial | Combate 4; onze opções, limites de repetição, piso de custo, ação completa de Lento, margem de Letal, dado de Atroz e vantagem/custo crescente de Preciso por rodada. Amplo, Longo, Penetrante, Impactante e sangramento informam os efeitos nos alvos. Os 15 de dano de Sacrifício precisam ser resolvidos com a mesa e confirmados explicitamente, pois a opção não define um tipo de dano. PDF 65. |
| Renovação e Autossuficiente | Inimigo declarado: crítico de Combate 6+ recupera 1 PE. Confirmar o último alvo reduzido a 0 recupera 1 Preparo e PE, sem repetir a Renovação do mesmo crítico. A confirmação nativa é única e precisa ocorrer antes do próximo ataque. Combate 20 adiciona dado da arma e temporários exclusivos de Golpe Especial (3, ou 6 uma vez por cena), descartando sobras sem preencher a reserva comum. PDF 65. |
| Domínio dos Fundamentos | Sete mudanças, duas iniciais e uma em Técnica 12; Rápido exige nível real 6. Custos, CD de Cruel, precisão, mudança de ação e limites por rodada de Rápido/Duplicado são executados. Potente rerrola os menores dados até INT/SAB e mantém o melhor; Distante/Expansivo/Duplicado indicam alcance/área/alvo para a mesa. Rituais guardam escolhas e atributo, cobrando na conclusão. Rápido combinado com ritual e o custo por nível de Rápido/Duplicado na Máxima ficam para adjudicação; o índice interno 6 não é presumido como nível oficial. PDF 78–79. |
| Concessões e Adeptos | Acervo Amplo acrescenta estilo (Combate 6); Expansão dos Fundamentos acrescenta mudanças (Técnica 8 e 12). Adepto de Combate/Feitiçaria confere maestria, origem e Adeptos registrados, concede uma escolha separada mesmo fora da classe e controla o desconto de Feitiçaria BT vezes por cena. Somente o estilo concedido pelo Adepto escala pelo nível total. A passagem de tempo para trocar escolhas e o orçamento geral de talentos ainda são confirmados pela mesa. PDF 71, 87, 166. |
| Defesas ignoradas | O painel de dano recebido aceita a quantidade de RD ignorada e a confirmação de resistência ignorada. Preserva imunidade, vulnerabilidade, temporários e concentração; permite aplicar Penetrante/Raio Negro na ficha receptora sem alterar suas defesas permanentes. Não vincula automaticamente duas fichas. PDF 65, 192, 365. |
| Ataque Furtivo | Restringido 2+, dados por nível, crítico e confirmação de alvo Desprevenido/flanqueado. Nesta versão o uso reinicia no próprio turno; aplicação em turnos alheios precisa de adjudicação da mesa. PDF 115. |
| Feitiços | Acesso por nível, custo e piso, conjuração com ações, rolagem do efeito, sustento e concentração. Assistente sugere os dados-base por tipo. A distribuição em alvos e as customizações precisam da mesa. PDF 199–209, 228. |
| Rituais | Melhoria simples ou estendida; teste de Prestidigitação; cancelar/adiar em falha; preparação entre dois turnos, concentração e ações; bônus de dano, precisão e CD; conversão de sustento em concentração. Alterações espaciais e de efeito são indicadas no resultado para aplicação pela mesa. PDF 321–323. |
| Canalizar em Golpe | Limites por CL, custos, dado básico/Avançada/Máxima, persistência no erro, aplicação em um acerto e multiplicação dos dados durante o crítico. Avançada também permite escolher reação, consumindo a reação em vez do movimento; o gatilho de realizar um ataque é confirmado pela mesa. PDF 178. |
| Cobrir-se | Reação, custo limitado por CL, PV temporários próprios, Cobertura Avançada e comando de encerramento no fim do turno da fonte. Não aplica o teto dos temporários comuns. PDF 178. |
| Barreiras | Custo por parede, quantidade, PV e variantes Paredes Resistentes/Barreira Rápida. A posição e os PV de cada parede no campo são administrados pela mesa. PDF 188–189. |
| Domínio Simples | Concessão, custo, raio, durabilidade, desgaste e comportamento especial nas falhas de concentração. Exposição a um acerto garantido é um desgaste explícito, porque não existe vínculo automático com uma expansão inimiga. PDF 193. |
| Expansões | Requisitos de Incompleta/Completa/Acerto Garantido/Sem Barreiras, custo, duração, movimento e parte dos bônus de aptidão/custo, encerramento e exaustão de técnica. Rola confronto; o resultado entre usuários e efeitos particulares não são aplicados automaticamente. PDF 183–187, 239–246. |
| Técnica Máxima | Aptidão, maestria, acesso mínimo e custo 25; recarga de 6 − metade do BT em rodadas completas. O assistente usa a tabela de nível 5 enquanto o acesso está abaixo de 5. Os dados customizados da técnica são registrados no feitiço; atualize-os ao desbloquear nível 5. PDF 194. |
| Raio Negro | Ataque classificado como corpo a corpo, pré-requisitos, limiar, consciência absoluta, Faíscas Negras e recompensa permanente uma única vez. Multiplica o dano durante o ataque por 1,5, acrescenta o dano após em seguida e informa que ignora RD/resistência. O benefício de PE acompanha o nível sem preencher a reserva atual. PDF 192. |
| Origens | Energia Antinatural e Vigor Maldito; efeitos numéricos de heranças citados acima; restrições de Sem Técnica/Restringido; imunidades sintéticas de Corpo Mutante. As demais anatomias, núcleos e características têm registro/referência, não execução completa. PDF 27–41. |

## Estado, permissões e compatibilidade

O sistema usa `feiticeiros_maldicoes`, schema 1, edição `2.5.2`. Versões desconhecidas não habilitam regras; nenhum outro motor é usado como fallback. Os sistemas existentes conservam suas edições e renderizadores.

O executor recebe `RuleCommand` e usa as permissões, versão otimista, validação, histórico e commit já existentes em `ExecuteCharacterRule`. `jujutsu` é contexto declarativo opcional: tipo de ataque, vantagem/desvantagem, estados do alvo e escolhas de ritual. Não há execução de código recebido do cliente.

O estado reservado vive em `estado_regras`. Autosave/importação/restauração não podem reescrever contadores executados; importações novas devem iniciar sem esse estado. Enquanto há ritual preparado ou sustento ativo, a lista de feitiços não pode ser editada/reordenada. Custos e ações de comandos recusados não são persistidos.

Estilos e Fundamentos usam identificadores na lista `aptidoes` já existente, com controles próprios em **Progressão**. As fichas v1 existentes podem usar essas escolhas sem adicionar campos nem alterar snapshots de sistema. Na multiclasse, o atributo-chave adicional de Combate também é uma escolha explícita. Nomes de armas não são usados para inferir mãos, propriedade Pesada ou tipo de ataque.

Se um benefício já estava incluído em outros bônus manuais, retire essa parcela ao ativar sua execução nativa para evitar contagem duplicada. Os seletores dos Adeptos distinguem a opção concedida das escolhas da classe; isso preserva sua escala e seu desconto próprios.

Os recursos comuns ainda seguem o contrato do produto: cada valor fica entre zero e seu máximo. O déficit negativo de PV de F&M fica no estado reservado. O OBS projeta recursos e indicadores permitidos, sem transmitir a ficha inteira; heranças, votos e anotações não são expostos.

Não há migration SQL nova: os campos de sistema e personagem já usam o esquema JSON existente. Isso não dispensa as migrations anteriores exigidas pelo projeto.

## Pendências para cobertura integral

- Efeitos e pré-requisitos completos de todas as habilidades de especialização, talentos, aptidões, encantamentos e anatomias; estilos marciais de Restringido, posturas, artes adicionais e manobras finalizadoras. Repertório de Combate, Golpe Especial, Fundamentos, Preparo e Empolgação estão implementados no escopo da tabela acima.
- Orçamentos completos de treinamentos, talentos, feitiços e aptidões adquiridos por nível. A criação guiada confere atributos; não certifica todas as escolhas da ficha.
- Restauração de uma revisão que altere o estado reservado de combate. O histórico registra ações, mas essa restauração ainda é bloqueada pelo validador do plugin para impedir alteração silenciosa do ledger.
- Troca e desenvolvimento dos três núcleos de Corpo Mutante, incluindo recursos, ferimentos e morte separados.
- Catálogo completo de equipamentos e consumíveis, recarga/alcance/propriedades das armas, fabricação, cargas de encantamento e perda de membros com efeitos completos.
- Criação detalhada de feitiços auxiliares/especiais/passivos, reversões, condições por fonte e todos os benefícios/customizações; múltiplas fontes independentes de condições e seus TR de encerramento.
- Efeitos customizados das expansões, disputa entre duas fichas, barreiras com PV próprios, acerto garantido e demais anti-domínios.
- Criação/ações de invocações, domar maldições, hordas, limites, custo e vínculo com o controlador; as linhas atuais são registros.
- Etapas completas dos treinamentos, recompensas de interlúdio e execução dos votos. Votos discricionários precisam preservar a decisão do narrador, conforme o próprio livro.
- Gerador e executor de inimigos por ND/patamar do Grimório, características opcionais e fichas prontas. Os multiplicadores de dano reverso para natureza Maldição já existem, mas não equivalem a um motor do Grimório.

## Validação

- `node scripts/check-sheet-templates.mjs --export-fm` verifica cinco modelos, fórmulas, resolução e projeções. Exporta somente a definição autoral para `.quality-local/fm/template.json`.
- `dotnet run --project tests/Chronicles.Rules.Tests -- --fm-template .quality-local/fm/template.json` executa regressões e confere a definição real do frontend no validador de campos/layout/defaults da API.
- `FeiticeirosRulesTests` cobre recursos, multiclasse, morte, alma, defesa, custos, turnos, conjuração, domínio e proteção do estado. `FeiticeirosAdvancedTests` cobre criação guiada, rituais entre turnos, canalização, cobertura, durabilidade, imunidades, Raio Negro, contexto e recarga da Máxima.
- `FeiticeirosMartialTests` verifica Empolgação, orçamento e uso de manobras, Defesa com expiração, Corpo Treinado/Superior, artes e recuperação de Preparo, reação de canalização e Ataque Furtivo.
- `FeiticeirosClassOptionsTests` verifica os oito estilos, contextos incompatíveis, ação/custos/repetições de Golpe Especial, críticos, recibo de eliminação, Autossuficiente, aplicação de dano com defesas ignoradas, sete Fundamentos e preservação dos contextos em rituais.
- `dotnet run --project tests/Chronicles.Architecture.Tests` e `node --test scripts/architecture.test.mjs` verificam contratos e fronteiras. O teste de OBS inclui o novo perfil.
- `npm --prefix web run build` e `npm --prefix web run lint` verificam a aplicação Angular.
- `node web/fm-browser.mjs` usa o bundle de produção e fixtures isoladas de API. Verifica quatro resoluções, navegação, edição/autosave/reabertura simulados, assistente de feitiço, envio de versão, erros, busca e leitura de espectador. Capturas em `docs/visual-validation/rules/feiticeiros_maldicoes-*.png`.
- `SMOKE_BASE_URL=http://localhost:8081 node scripts/fm-smoke.mjs` usa API e PostgreSQL reais isolados no CI: catálogo, schema real, criação, recursos, contexto marcial, persistência do ledger, recusa de adulteração e concorrência otimista. O script recusa hosts de produção.

O navegador foi validado com fixtures, incluindo contexto de artes, sugestão da Máxima e preservação do inventário na impressão. Separadamente, a integração HTTP/PostgreSQL passou no CI com contas sintéticas e banco descartável; não foram usadas contas pessoais ou dados de campanha em produção. As fixtures de navegador não são evidência de persistência ou autorização no banco. O resultado da publicação é registrado em [2026-11-14-feiticeiros-deployment.md](architecture/releases/2026-11-14-feiticeiros-deployment.md).

O índice pode ser regenerado com `python scripts/index-fm-rulebook.py`, tendo `pypdf` e o PDF local correspondente. Ele extrai nomes, categorias e páginas; não infere efeitos de habilidades. Créditos: Setsugiri, Parker e colaboradores de Feiticeiros & Maldições; Gege Akutami por Jujutsu Kaisen.
