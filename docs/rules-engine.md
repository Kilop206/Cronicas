# Ações de regras por edição

O motor em `SheetRuleActions` resolve o identificador estável, a versão do template e a edição. As primeiras versões com ações no servidor são Ordem v6/1.3, D&D v3/2014, Tormenta20 v3/Jogo do Ano v1.3 e CoC v4/7e. Versões anteriores continuam abrindo e só recebem a estrutura nova após atualização explícita pelo dono do sistema. D&D 2024 não recebe regras de 2014.

## Fluxo integrado

`GET /api/characters/{id}/rules` devolve indicadores, ações e configurações daquela edição. `POST` no mesmo endereço recebe `{expectedVersion, command: {action, amount, option, visibility}}`. O servidor carrega os valores persistidos, verifica propriedade e papel na campanha, executa a regra, valida o resultado contra o schema e salva uma revisão. Os dados usados pelas ações aleatórias são gerados no servidor e registrados na campanha com a visibilidade escolhida. A alteração da ficha, o histórico e as rolagens são gravados no mesmo `SaveChanges`, em uma transação do EF. Um conflito de versão retorna 409, sem aplicar novamente a ação.

A interface aguarda o autosave antes de permitir comandos e bloqueia a edição da ficha durante uma ação. O resultado substitui os dados locais pela resposta do servidor. Importação, atualização comum e restauração também verificam as configurações reservadas do motor; jogadores não conseguem ativar a regra opcional de sorte de CoC por esses caminhos.

O estado fica no campo tabelado `estado_regras`, com pares `chave`/`valor`, integrado ao JSON, aos snapshots e à exportação já existentes. Esse campo comporta até 100 indicadores, mantendo o limite de tamanho total do documento. As tabelas comuns continuam limitadas a 30 linhas. Fórmulas e comandos não executam código enviado pelo usuário.

## Cobertura implementada

| Edição | Ações e indicadores |
| --- | --- |
| Ordem 1.3 | PE por turno, exceção de custo mínimo, início de turno/cena, três turnos morrendo ou enlouquecendo, estabilização, dano massivo com teste de Fortitude, cura, dano mental, dormir e relaxar uma vez por interlúdio, recuperação por conforto, carga e DT de rituais, custo paranormal de ritual conhecido, perdas permanentes de SAN e custos de Medo por forma. |
| D&D 2014 | PV temporários sem acúmulo, dano e excesso letal, dano em PV zero, testes contra a morte e resultados naturais 1/20, estabilização, cura, dados de vida por classe e Constituição, recuperação de dados no descanso longo, espaços de conjuração por nível e multiclasse, conjuradores de 1/2 e 1/3, espaços de pacto separados, descanso curto/longo, PV médios de criação com primeira classe e níveis subsequentes. |
| CoC 7e | Movimento humano e envelhecimento, limite de SAN por Mitos, ferimento grave, CON para consciência e morte, cura diária/semanal com 1d3 ou 2d3 em extremo, perdas de SAN por evento e por dia, teste de INT para insanidade temporária, PM e pagamento excepcional em PV, recuperação horária, gasto opcional de sorte autorizado pelo mestre, marcação e desenvolvimento de perícias. |
| Tormenta20 JdA | PV/PM básicos das 14 classes, primeira classe e multiclasse, ajustes de origem/poderes/atributo-chave, recuperação por conforto, limite de PM pelo nível da classe, saldo de PV negativos, cura que paga esse saldo, sangramento com Constituição CD 15 e perda de 1d6, estabilização, morte e carga. |

Os comandos que confirmam passagem de tempo ou efeitos externos exigem a confirmação contextual do usuário. O servidor não presume que uma hora de tempo real equivale a uma hora de jogo. Ataques recebidos usam dano já ajustado por resistência/proteção; a ficha não infere tipo de dano ou imunidades a partir de texto livre. As configurações de classe usam chaves estáveis e não tentam adivinhar classes a partir dos nomes digitados.

`SheetWeaponListComponent` apresenta os ataques dentro de Actions em D&D, Combate em Ordem e CoC e nas áreas de ataque de Tormenta20. As listas antigas continuam disponíveis quando têm dados. O editor oferece uma lista dos atributos/perícias do schema para escolher a base do ataque; a seleção guarda a chave estável sem exigir que o jogador a digite. A composição dos dados, o consumo de munição e as falhas são apresentados no próprio componente.

## Conferência e diferenças entre fontes

As versões Ordem v7, D&D v4, Tormenta20 v4 e CoC v5 acrescentam ataques estruturados com cálculo de dano no servidor. O campo `ataques_regras` é adicionado na atualização explícita, preservando as tabelas antigas de armas e ataques. A base usa uma chave numérica do próprio schema. Em Ordem, o servidor inclui treino e outros modificadores da tabela de perícias e trata atributo zero com dois dados conservando o menor.

D&D dobra os dados base e extras no crítico; Ordem e Tormenta20 multiplicam os dados base conforme a arma, mantendo dados extras e bônus fixos. CoC usa testes percentuais, mau funcionamento, dano máximo e empalamento, distinguindo armas a distância. Munição é descontada na mesma transação da rolagem, inclusive em falhas; −1 representa ataques sem consumo controlado. O alvo recebe o dano por uma ação separada, depois de aplicar proteção, resistências e imunidades. O motor ainda não infere oposição, alcance ou elegibilidade apenas do nome da arma.

O interpretador `DamageFormula` aceita até 12 termos de dados/números e referências a campos numéricos, com limites de quantidade; não aceita funções, objetos, acesso a arquivos ou execução de código. Dano base e extra ficam separados para que os multiplicadores de cada edição sejam aplicados corretamente. Os testes verificam críticos, consumo de munição, privacidade, persistência e recarga usando a API real de teste.

Em D&D, configurar níveis também atualiza o nível total e os espaços disponíveis da ficha. Gastar um espaço ou concluir descanso longo atualiza tanto o estado do motor quanto os campos da aba de magias. A escolha de classe conjuradora preenche atributo, CD e bônus de ataque, sem misturar o atributo de classes diferentes.

Os capítulos completos dos quatro livros básicos locais foram extraídos para arquivos temporários para conferência. Esses arquivos e os PDFs não são publicados. Ações são código e matemática; não incluem descrições de poderes, magias, aventuras ou imagens dos livros.

O Livro Básico de CoC 7e consultado trata dano **maior** que PV máximos como morte imediata; o quick-start usa **maior ou igual**. O motor segue o livro básico e tem teste específico para o caso de igualdade. A recuperação semanal em extremo usa 2d3, também conferida na [referência oficial da Chaosium](https://callofcthulhuwiki.chaosium.com/rules/hit-points-wounds-and-healing.html). Movimento 9 exige FOR e DES estritamente maiores que TAM; valores iguais resultam em 8.

## Validação reproduzível

### Condições de Ordem 1.3 e Tormenta20 Jogo do Ano

As relações entre condições ficam em `D20Conditions`; `D20Checks` resolve testes conforme a edição. Ordem retira um ou dois dados, enquanto Tormenta20 aplica −2 ou −5. Penalidades equivalentes usam a mais severa. As dependências de atributo vêm da expressão ou fórmula do campo, por chave estável. Os botões nativos de atributos e perícias usam o servidor, incluindo treinamento de Ordem. `RuleView.checks` fornece prévias das expressões para a interface mostrar os dados/modificadores efetivos sem reimplementar o algoritmo no navegador.

Condições podem durar até o fim da cena, um número explícito de rodadas ou até remoção. A ação de concluir rodada avança somente as durações por rodada; iniciar cena encerra somente as condições de cena. Reaplicar uma duração curta não apaga uma persistente. Agravamentos de medo, fraqueza, frustração e fadiga preservam condições subjacentes. Reflexos falha automaticamente quando indefeso; Percepção pode informar se exige visão ou audição. Agarrado exige confirmar arma leve. Incapacidade impede ataques; paralisia ainda permite o comando de habilidade puramente mental.

Defesa e deslocamento consideram as relações implícitas, inclusive inconsciência, lentidão, vulnerabilidade e sobrecarga. A Defesa de caído em Tormenta20 acumula com outras condições, conforme seu apêndice. A ficha, o painel de indicadores e a resposta pública do OBS mostram a Defesa ajustada corpo a corpo; os controles de regras também mostram o valor contra distância. Editar o campo altera a base, preservando a fórmula e os valores anteriores. A sobrecarga é aplicada explicitamente; ainda não é inferida da lista de itens.

O início de turno resolve confusão (1d6 e direção em 1d8 quando pertinente), chamas com resistência configurada e sangramento. Ordem usa Vigor DT 20; Tormenta20 usa Constituição CD 15. Asfixia em Ordem permite prender a respiração por Vigor rodadas, depois testa Fortitude com DT crescente; a falha causa inconsciência e perdas por rodada. Voltar a respirar remove apenas essa fonte de inconsciência. Petrificação protege contra o dano de chamas. A chance de falha por camuflagem usa 1d10, inclusive quando o ataque obtém crítico, e pode ser dispensada por uma habilidade confirmada no contexto.

Alquebrado aumenta o custo de PE/PM. Tormenta20 distingue o limite de habilidade de classe, o nível total para poderes gerais/origem/raça e a exceção de custo mínimo. Conjuração conhecida tem comandos próprios: situações ruins/terríveis e dano durante a execução exigem Vontade; uma falha perde o recurso. Ordem executa em seguida o custo paranormal apenas se o ritual não foi perdido. Tormenta20 oferece o teste de Misticismo para magia arcana sujeita à armadura e dispensa essa restrição para magia de raça/poder/item. O usuário deve declarar a situação e as exceções pertinentes; nomes digitados não ativam regras automaticamente. Isso não implementa os efeitos individuais das magias ou rituais.

Fontes locais: Ordem, páginas impressas 11, 53, 119 e 310–312; Tormenta20, páginas impressas 153, 170–171, 224 e 394–395. O texto de Ordem descreve como inverter a escolha quando uma penalidade reduz uma quantidade positiva de dados abaixo de um. A combinação desse procedimento com o atributo já igual a zero não é inequívoca no material consultado. Nesse caso, o motor pede uma quantidade explícita decidida pela mesa e conserva o menor resultado; não aplica silenciosamente a regra de atributos negativos de outro suplemento.

### Condições de D&D 2014

O motor registra as 14 condições do apêndice e os seis níveis de exaustão. Ações próprias aplicam e removem condições. Ataques combinam fontes de vantagem/desvantagem sem empilhá-las; perícias, atributos e resistências usam os efeitos pertinentes. Paralisia, petrificação, atordoamento e inconsciência provocam falha automática em resistências de FOR/DES. Incapacidade bloqueia ataques e encerra concentração. O contexto permite informar se a fonte do medo está fora de vista, se o alvo vê invisibilidade, se o alvo é o encantador e qual sentido é necessário à Percepção.

Os botões nativos de atributos, perícias, resistências, iniciativa e ações com d20 usam o motor da edição. Rolagens de teste sem alteração de estado ficam no registro de dados e não criam revisões redundantes da ficha. A concentração exige Constituição após dano, inclusive absorvido por PV temporários, e é encerrada ao falhar ou ficar incapacitado. Exaustão altera testes, deslocamento e PV máximos; reduzir a exaustão restaura o máximo sem fornecer cura gratuita. Descanso longo com comida/água reduz um nível. A percepção passiva e o deslocamento da interface refletem os modificadores pertinentes.

Recursos agora aceitam máximo zero no cliente e servidor. O OBS e as barras tratam divisão por zero. Alterar um máximo usa o valor concluído ao sair do controle, evitando cortar recursos durante a digitação de um novo valor com vários dígitos. Um máximo em branco não é convertido silenciosamente em zero.

- `dotnet run --project tests/Chronicles.Rules.Tests`: testes determinísticos de custos, recursos, morte, descanso, espaços, multiclasse, ferimentos, sanidade e desenvolvimento.
- `node scripts/check-sheet-templates.mjs`: schemas, fórmulas, versões e resolução dos quatro renderizadores.
- `npm run build` e `npm run lint` em `web`.
- API local com PostgreSQL de teste, frontend na porta 4200 e `node rules-integration.mjs` em `web`: persistência real, autenticação, permissões, versão otimista, histórico e ações no navegador, com screenshots nas quatro resoluções em `docs/visual-validation/rules`.

## Regras pendentes — não declarar cobertura integral

- Aplicação de dano diretamente ao alvo, resistências/imunidades por tipo, alcance, munições e propriedades especiais de cada arma. Ataques, críticos e múltiplos componentes de dano já estão integrados ao motor.
- Condições específicas dos alvos e suas fontes, múltiplas fontes independentes com duração própria, ação obrigatória por medo/confusão, orçamento de ações (incluindo enjoado), venenos/doenças com efeitos individuais, imunidades e exceções de classe. Os modificadores, relações, durações, concentração e efeitos de turno descritos acima já estão integrados para Ordem/Tormenta20; as condições de D&D afetam testes, ataques e concentração.
- Catálogos de escolhas de origem, espécie/raça, classe, trilha/subclasse, poderes, talentos, equipamentos e magias/rituais; pré-requisitos e escolhas em cada nível.
- Conjuração completa: preparação/conhecimento, componentes, concentração, sustentação, alcance, áreas e efeitos específicos; recuperação de recursos de características individuais.
- Criação de investigador por idade/ocupação, distribuição de pontos, perícias especializadas, tratamentos, crises de insanidade, perseguições e combate percentual completo.
- Exploração, sobrevivência, ambientes, viagens, fabricação, economia, regras opcionais e adversários dos suplementos.

Uma interface com campo livre para registrar uma exceção não constitui implementação automática dessa regra. O checklist só deve mudar quando houver regra executável, integração persistida e teste verificável.
