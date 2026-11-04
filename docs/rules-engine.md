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

### Criação por características e desenvolvimento em CoC 7e

O método aleatório do livro básico gera cinco características por 3d6×5 e três por (2d6+6)×5. A ação só aparece quando as oito características estão zeradas e a geração ainda não foi usada; não substitui características de um investigador existente. A idade é informada na própria ação. Adolescentes recebem duas rolagens de sorte, conservando a maior, e a redução de EDU; adultos recebem as melhorias sequenciais de EDU e ajustes de aparência da faixa etária. O jogador distribui o saldo de redução física entre os atributos permitidos. Finalizar essa etapa exige saldo zero e aplica PV, PM e SAN iniciais uma única vez, com histórico e dados registrados no servidor.

A tabela local cobre explicitamente 15–89 anos; geração em outras idades permanece dependente de decisão do Guardião. Constituição e Tamanho não podem ser reduzidos a zero pela criação de um investigador vivo. O método por compra, as ocupações, as especializações e a distribuição dos pontos de perícia ainda são etapas pendentes; concluir características não significa concluir o personagem. O indicador de interesses pessoais calcula INT×2 para essa etapa futura.

O desenvolvimento mantém o aumento de 1d10 e permite ultrapassar 100%, como estabelece o livro. A primeira passagem de uma perícia de menos de 90 para 90 ou mais recupera 2d6 SAN, respeitando os limites. A recompensa não se repete em desenvolvimentos posteriores da mesma perícia. Testes de CON/INT continuam tratando 01 como sucesso e 100 como falha, mesmo com características altas; as rolagens de melhoria usam seu procedimento próprio.

A ficha oferece dificuldade regular, difícil ou extrema. O alvo usa o valor completo, metade ou quinto arredondados para baixo; a classificação não divide a dificuldade duas vezes. A API percentual e a ficha aceitam valores acima de 100 sem truncar os limiares: uma perícia de 105% exige 52 para um teste difícil. O 100 continua sendo falha crítica. Essas regras foram conferidas no PDF local, páginas PDF 31–37, 91–92 e 95 (numeração impressa uma página menor), incluindo o exemplo que altera o intervalo de falha crítica conforme a dificuldade.

### Recursos de classe de D&D 2014 — 03/11/2026

`Dnd2014ClassResources` define reservas com identificadores estáveis, capacidade por nível de classe, saldo e forma de recuperação. Os níveis são configurados dentro da ficha por classe; o nome livre da identidade não determina regras. `RuleView.resources` fornece os dados ao painel nativo `DndClassResourcesComponent` e aos controles comuns, inclusive quando o sistema usa um layout personalizado. O painel permite registrar gastos, escolher quantidades, executar os efeitos próprios implementados e selecionar descansos. Os comandos passam pelo mesmo controle de versão, permissões, histórico e transação das demais ações. Rolagens de Retomar o Fôlego e Indomável respeitam a visibilidade escolhida, inclusive exclusiva dos mestres.

| Reserva | Cálculo e recuperação |
| --- | --- |
| Fúria | 2/3/4/5/6 usos nos patamares 1/3/6/12/17; ilimitada em bárbaro 20; descanso longo. |
| Inspiração de Bardo | Modificador de CAR, mínimo 1; d6/d8/d10/d12 nos níveis 1/5/10/15; descanso longo e, a partir de bardo 5, também curto. Bardo 20 recupera um uso ao rolar iniciativa se estiver vazio. |
| Canalizar Divindade | Reserva compartilhada de clérigo/paladino; 1 uso, 2 em clérigo 6, 3 em clérigo 18; descanso curto/longo. Multiclasse não soma reservas. |
| Forma Selvagem | Dois usos a partir de druida 2; ilimitada em druida 20; descanso curto/longo. O consumo não substitui os atributos pela ficha de uma besta. |
| Retomar o Fôlego | Um uso; descanso curto/longo. A ação própria rola 1d10 + nível de guerreiro, cura o próprio personagem e registra o dado. |
| Surto de Ação | Um uso em guerreiro 2, dois em guerreiro 17; descanso curto/longo; no máximo um por turno. O início de turno é confirmado por comando. |
| Indomável | 1/2/3 usos nos níveis de guerreiro 9/13/17; descanso longo. Executa uma nova resistência, considerando as condições, e exige usar esse novo resultado. Não rerrola uma falha automática. |
| Ki/Chi | Nível de monge a partir do 2; descanso com pelo menos 30 minutos de meditação. A confirmação de descanso comum não presume meditação. Monge 20 recupera quatro pontos na iniciativa se estiver vazio. |
| Cura pelas Mãos / Sentido Divino | Nível de paladino × 5 pontos; 1 + modificador de CAR usos de Sentido Divino, mínimo técnico zero; descanso longo. Cura própria tem ação específica; gasto destinado a outra criatura apenas registra o consumo, sem alterar o alvo. |
| Pontos de Feitiçaria | Nível de feiticeiro a partir do 2; descanso longo. Feiticeiro 20 recupera até quatro no descanso curto. |
| Arcana Mística / Mestre Místico | Arcanas de níveis 6/7/8/9 são reservas independentes, adquiridas em bruxo 11/13/15/17 e recuperadas no descanso longo. Mestre Místico em bruxo 20 restaura pacto após confirmar um minuto e gasta seu uso até o descanso longo. |

Conjuração flexível cria espaços de níveis 1–5 por 2/3/5/6/7 pontos, inclusive um nível acima da progressão normal se houver pontos suficientes. Espaços criados ficam separados em `slot_criado_N`, são utilizáveis pela ação comum de conjurar e desaparecem no descanso longo. Converter um espaço comum ou de pacto consome o espaço na mesma transação e recupera pontos até o limite da reserva; o nível do pacto é calculado pelo servidor. O saldo manual da aba de magias é reconciliado para que criar um espaço não restaure outros já gastos.

Recuperação Arcana exige o descanso curto confirmado, permite recuperar espaços gastos até a soma de metade do nível de mago arredondada para cima e proíbe espaços de nível 6 ou superior. A seleção é uma etapa transitória: outra atividade encerra o orçamento não utilizado. A edição local consultada diz **uma vez por dia**; um novo descanso longo não zera esse indicador automaticamente. Há um comando separado para confirmar um novo dia de jogo. O motor não confunde tempo real com tempo do personagem.

As reservas novas começam completas quando não há gastos anteriores registrados. Fichas já jogadas devem conferir os saldos; o motor não tenta inferir usos antigos a partir de anotações. O registro de gasto de uma reserva não equivale à implementação de todos os efeitos da habilidade: efeitos de Fúria, domínios, formas de besta, alvos de cura, Metamágicas específicas, ação bônus e demais requisitos contextuais ainda precisam de regras próprias. Esse bloco não se aplica a D&D 2024.

Conferência no Livro do Jogador local: tabelas de classe e seções de características; páginas PDF 46–47 (bárbaro), 53–54 (bardo), 58 (bruxo), 64–65 (clérigo), 72–73 (druida), 78–79 (feiticeiro), 84–85 (guerreiro), 96 (mago), 103–104 (monge), 109–111 (paladino) e 165 (multiclasse). Os PDFs e as descrições integrais continuam fora do repositório e da publicação.

- `dotnet run --project tests/Chronicles.Rules.Tests`: testes determinísticos de custos, recursos, morte, descanso, espaços, multiclasse, ferimentos, sanidade e desenvolvimento.
- `node scripts/check-sheet-templates.mjs`: schemas, fórmulas, versões e resolução dos quatro renderizadores.
- `npm run build` e `npm run lint` em `web`.
- API local com PostgreSQL de teste, frontend na porta 4200 e `node rules-integration.mjs` em `web`: persistência real, autenticação, permissões, versão otimista, histórico e ações no navegador, com screenshots nas quatro resoluções em `docs/visual-validation/rules`.

### Ocupações e distribuição de pontos em CoC 7e · template v6

A aba **Criação** distribui pontos profissionais e interesses pessoais depois da geração aleatória de características e dos ajustes de idade. O template inicial contém Professor, Bibliotecário, Médico e duas variantes de Investigador privado, para escolher explicitamente FOR ou DES no orçamento. São quatro ocupações, representadas por cinco perfis; não representam um catálogo completo. Trinta perícias com bases identificadas, incluindo especializações concretas de idioma, ciência e arte, acompanham esses perfis. Novas especializações podem ser adicionadas pelo criador do sistema.

`cthulhuOccupations` pertence à definição versionada do sistema e aos seus snapshots, importação e exportação. Cada perfil tem identificador estável, nome, fórmula de pontos, faixa de Crédito e oito posições de escolha. Uma posição pode aceitar uma lista restrita de chaves de perícia ou qualquer perícia elegível da configuração. O editor visual permite criar ocupações, configurar as escolhas e editar as bases/fórmulas, sem alterar o código do motor. Mitos não é elegível. Fórmulas usam o interpretador permitido de `SheetFormula`, com validação de referências numéricas; não executam JavaScript.

Os comandos são:

| Comando | Entrada e efeito |
| --- | --- |
| `occupation-start` | `option`: identificador do perfil. Exige `criacao_fase = 2`, perícias/Crédito zerados e ausência de distribuição anterior. Aplica bases, fixa orçamento profissional e INT × 2 e grava a impressão SHA-256 da definição. |
| `occupation-choice` | `option`: posição; `expression`: chave da perícia. Rejeita escolhas incompatíveis e duplicadas. Uma escolha com investimento profissional deve ser reembolsada antes da troca. |
| `occupation-allocate` | `option`: perícia ou `credito`; `expression`: `occupation` ou `interest`; `amount`: investimento absoluto, inteiro, incluindo zero para devolução. Atualiza a perícia real para base + profissionais + interesses, verificando ambos os orçamentos. |
| `occupation-finish` | Exige oito escolhas distintas e Crédito na faixa. Arquiva a criação; pontos não gastos são descartados. Desenvolvimento posterior altera a perícia atual sem reescrever o investimento inicial. |
| `occupation-reset` | Disponível apenas durante a distribuição. Devolve os pontos e restaura perícias/Crédito ao estado zerado anterior à ocupação; mantém as características. Permite escolher outro perfil. |

O livro básico foi conferido nas páginas PDF 37–43 (impressas 36–42), e bases no capítulo de perícias, PDF 57–58. O método de pontos do livro básico não é o método de valores predefinidos do quick-start. Nenhum teto adicional de 75%/90% é presumido; a mesa pode configurar `skillCap`. O perfil padrão destina apenas pontos profissionais ao Crédito; `personalCredit` permite mudar explicitamente essa configuração da mesa, sem alegar uma proibição universal para interesses pessoais. Os idiomas escolhidos e especialidades adicionais precisam ser identificados pelo jogador/mestre; a interface não supõe qual idioma estrangeiro alguém conhece.

`criacao_pericias` registra `chave`, `base`, `ocupacao` e `interesse`; `criacao_escolhas` registra `slot` e `pericia`. O ledger admite até 100 linhas, mantendo o limite global de tamanho JSON da ficha e as 30 linhas das tabelas comuns. O servidor valida as tabelas e seus controles reservados no autosave, importação e restauração. Esses caminhos não podem mudar a etapa ou o orçamento de uma ficha existente; durante a distribuição, também não podem alterar o total de uma perícia por fora dos comandos. Um personagem novo importado pode trazer dados estruturados válidos, sem representar uma certificação de origem ou de todas as escolhas anteriores.

Os controles aguardam o autosave e usam a versão otimista da ficha. Os comandos geram histórico na transação existente. Uma definição alterada durante a distribuição é detectada e bloqueia novos gastos: reinicie a distribuição ou restaure a revisão anterior do sistema. Se campos reservados tiverem sido removidos, restaure primeiro o modelo compatível. Fichas concluídas mantêm o ledger histórico. Investigadores com perícias já preenchidas não são reiniciados nem recebem bases sobre os valores existentes. Templates v1–v5 continuam no registro; atualizar para v6 é uma ação explícita do dono do sistema.

`CthulhuOccupationTests` cobre orçamentos, escolhas, bases, Crédito, devoluções, configuração de teto, mudança de definição, dados malformados e bloqueio de sobrescrita. A integração real testa também o editor, snapshots e exportação/importação de regras. Capturas da distribuição em 1440×900, 1920×1080, 768×1024 e 390×844 estão em `docs/visual-validation/rules/call_of_cthulhu_7e-occupation-*.png`.

### Dano por tipo e Fúria em D&D 2014 · template v5

`Dnd2014Damage` processa uma única fonte de dano com até 12 componentes. Cada componente declara tipo, valor já rolado, origem mágica e, quando pertinente, arma de prata ou adamante. O evento informa se é um ataque e se o valor já inclui um crítico. O marcador de crítico afeta os testes contra a morte; não dobra novamente o dano recebido. O comando usa `action: "damage-typed"` e a propriedade `damage`:

```json
{"expectedVersion":7,"command":{"action":"damage-typed","amount":0,"option":null,"visibility":"private","damage":{"attack":true,"critical":false,"parts":[{"type":"slashing","amount":9,"magical":true,"silvered":false,"adamantine":false},{"type":"fire","amount":11,"magical":true}]}}}
```

O campo tabelado `defesas_dano` registra `tipo`, `efeito`, `condicao` e `fonte`. Os treze tipos da edição e a opção `all` são identificadores estáveis; efeitos permitidos são `resistance`, `immunity` e `vulnerability`. Condições disponíveis são aplicação geral, dano não mágico, ataques não mágicos e ataques não mágicos de armas sem prata ou adamante. A declaração de ataque impede que uma proteção exclusiva contra ataques reduza automaticamente uma queda ou outro efeito sem ataque. O motor não concede uma defesa apenas por reconhecer o nome de uma raça, item ou criatura.

Componentes do mesmo tipo sujeitos às mesmas defesas são somados antes do arredondamento. Resistências e vulnerabilidades repetidas não se acumulam. Imunidade zera o componente; resistência divide por dois, arredondando para baixo; vulnerabilidade é aplicada em seguida. Outras reduções e ajustes da fonte devem estar incluídos no valor informado. Petrificação concede resistência geral e imunidade a veneno. A Fúria elegível concede resistência a concussão, perfurante e cortante, inclusive mágicos, sem duplicar uma resistência já cadastrada.

O total final passa uma única vez pelo processamento de PV temporários, PV, morte instantânea e testes contra a morte. Uma fonte composta exige um único teste de concentração calculado sobre seu dano final; ataques separados são eventos separados. Imunidade com dano final zero não quebra concentração nem mantém Fúria por dano recebido. PV temporários absorvem o total depois das defesas e seu saldo deve ficar entre 0 e 9999. O comando antigo `damage` continua recebendo um valor **já ajustado**, preservando o fluxo existente e evitando aplicar defesas duas vezes. A resposta nova inclui `damage.raw`, `damage.total` e `damage.groups`, com os valores e fontes de cada grupo. A composição aparece na ação; os snapshots conservam a ficha antes/depois, sem constituir ainda um diário estruturado de todos os eventos de dano.

`Dnd2014Rage` acrescenta ativação de Fúria com confirmação da armadura e consumo de um uso. Sem armadura pesada, testes e resistências de FOR recebem vantagem e ataques registrados como arma corpo a corpo com base `mod_forca` recebem +2/+3/+4 nos níveis 1/9/16. Esse bônus fixo não é multiplicado por crítico. Ataques com DES, à distância, arremessados ou sem tipo declarado não recebem o bônus. A ativação encerra concentração; conjurar, usar Magia de Pacto/Arcana Mística ou iniciar concentração durante a Fúria é recusado no servidor. Incapacidade também impede essas ações.

O jogador declara que um alvo é hostil no controle de ataques, ou confirma um ataque anterior, incluindo erro ou agarrão. O motor não presume a relação entre participantes a partir de nomes. Dano efetivamente recebido, inclusive absorvido por PV temporários, também registra atividade. Concluir um turno encerra Fúria se não houve atividade, salvo Fúria Persistente a partir de bárbaro 15. Inconsciência e morte encerram a Fúria; alterar a armadura suspende/restaura seus benefícios. A duração usa até dez turnos normais confirmados, equivalentes a um minuto de combate; o controle de turno extra verifica encerramento antecipado sem consumir esse contador. A mesa controla a passagem de tempo de jogo, sem relação com o relógio real. O início de turno libera Surto, respeitando a reserva de usos existente; não implementa ainda todo o orçamento de ações e reações.

Em bárbaro 11 ou superior, a ativação pode autorizar tentativas automáticas de Fúria Implacável. Cair a zero sem morrer provoca resistência de CON CD 10; cada tentativa seguinte aumenta a CD em 5 até um descanso curto/longo. Sucesso deixa o personagem com 1 PV e mantém a Fúria. Morte instantânea não permite o teste. A escolha automática é explícita, e suas rolagens seguem a privacidade do comando. Bárbaro 20 mantém usos ilimitados sem criar consumo fictício. O gasto avulso da reserva pelo seletor genérico continua disponível para registrar efeitos externos, mas não equivale à ativação mecânica.

A ficha especializada recebe a aba **Defenses**, o editor de defesas, componentes de dano, resultado discriminado e controles de turno/Fúria. A ativação fica junto à reserva de classe. Inputs aguardam o autosave e comandos usam a versão otimista da ficha. A coluna `tipo` é acrescentada ao modelo de ataques durante atualização explícita; linhas antigas permanecem sem classificação até o jogador escolher, preservando seu dano e conteúdo. O painel para OBS reconhece o template v5 e mantém os recursos próprios de D&D. Templates anteriores e a edição 2024 continuam separados. Modelos legados sem layout agora distribuem seus campos em páginas de até 40 blocos ao abrir o editor, permitindo atualizar a ficha extensa sem apagar campos.

Fontes: Livro do Jogador 2014 local, seções de Fúria/Fúria Implacável/Fúria Persistente e dano, páginas PDF 47–48 e 197. A conferência pública usa [combate de 2014](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/combat) e [classes de 2014](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/classes). Nenhum texto integral, ilustração ou catálogo protegido foi incorporado.

`DndDamageRageTests` cobre agrupamento/arredondamento, defesa condicional, imunidade, vulnerabilidade, concentração, morte, armadura, bônus e duração de Fúria, vantagens, Fúria Implacável e preservação das versões. A integração real valida edição, persistência após refresh, privacidade, permissões, conflitos e atualização v4→v5 pelo editor. As capturas reais estão em `docs/visual-validation/rules/dnd_5e-damage-rage-*.png`, nas quatro resoluções documentadas.

Validação em 04/11/2026: testes determinísticos .NET, smoke da API com PostgreSQL local, regressão Playwright dos quatro sistemas, verificação de templates, lint e build passaram. As capturas foram inspecionadas; o painel de combate se reorganiza para uma coluna no celular e antecede perícias no tablet. A largura do documento foi conferida em 1440×900, 1920×1080, 768×1024 e 390×844. O banco usado nessa validação é isolado da produção.

### Ataque Descuidado e Crítico Brutal · D&D 2014 · 04/11/2026

`Dnd2014BarbarianAttacks` complementa o combate do template v5, sem acrescentar campos de personagem nem reinterpretar templates antigos. A identificação continua sendo `dnd_5e` / v5 / `2014`. `RuleCommand.weapon` recebe `{reckless, outsideTurn, brutalDie}`; os valores não executam expressões nem código.

Ataque Descuidado é escolhido no primeiro ataque do próprio turno, a partir de bárbaro 2. O servidor conta os ataques da lista nativa, incluindo erros, e a confirmação de ataque hostil anterior. A escolha concede vantagem apenas a ataques com arma corpo a corpo usando `mod_forca`. Vantagem e desvantagem se cancelam pelo mesmo motor de condições, sem empilhamento. A exposição do bárbaro persiste no estado e aparece na ficha até o início do próximo turno. O fim do turno retira apenas o benefício ofensivo; ataques declarados fora do turno não o recebem nem consomem a escolha do primeiro ataque. Início de turno e descanso limpam esses indicadores.

A confirmação de turnos é explícita. Ataques resolvidos fora da lista nativa e do comando de confirmação não são deduzidos de textos ou rolagens livres. A vantagem dos atacantes contra o bárbaro fica indicada para a mesa: o motor atual ainda não associa automaticamente um atacante a uma ficha-alvo. O usuário declara esse contexto no modo de ataque ao resolver o ataque recebido.

Crítico Brutal acrescenta um, dois ou três dados nos níveis de bárbaro 9, 13 e 17. Exige ataque com arma corpo a corpo, mas não exige Força nem Fúria. Uma arma `2d6` acrescenta somente `1d6` no primeiro patamar; os dados adicionais não são dobrados novamente, não multiplicam o modificador fixo e não copiam dados de efeitos extras. Ataques comuns, à distância ou arremessados não recebem esses dados. Sem dado de arma no dano base, o motor não inventa um. Fórmulas base com dados de faces diferentes exigem escolher explicitamente uma face presente na arma antes de rolar; efeitos adicionais devem ser registrados em `dano_extra`.

O componente nativo de ataques oferece as escolhas de Ataque Descuidado, reação fora do turno e dado da arma. Os comandos usam as mesmas permissões, privacidade, transação, histórico e controle de versão do combate. Recarregar a ficha conserva a exposição e a decisão já usada. Os dados extras aparecem na composição da rolagem. Nenhuma opção de classe é concedida pelo nome livre do personagem ou da arma.

Fontes locais: Livro do Jogador 2014, seções Ataque Descuidado e Crítico Brutal, páginas PDF 47–48; conferência na [referência pública de classes de 2014](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/classes). `DndBarbarianAttackTests` cobre os patamares, tipos de arma, dados-base versus extras, cancelamento de vantagem, primeiro ataque, reação, expiração e compatibilidade com v4. `web/rules-integration.mjs` verifica o fluxo nativo e os controles compartilhados para layouts personalizados, com API e banco locais, e captura `dnd_5e-barbarian-attacks-*.png` em quatro resoluções. A regressão completa dos quatro sistemas e o smoke passaram em 04/11/2026.

## Regras pendentes — não declarar cobertura integral

- Aplicação automática de dano ao alvo a partir do ataque, defesas por tipo dos demais sistemas, alcance, munições e propriedades especiais de cada arma. D&D 2014 já processa componentes de dano recebidos e suas defesas conforme a seção acima; ataques e críticos dos quatro sistemas permanecem integrados ao motor.
- Condições específicas dos alvos e suas fontes, múltiplas fontes independentes com duração própria, ação obrigatória por medo/confusão, orçamento de ações (incluindo enjoado), venenos/doenças com efeitos individuais, imunidades e exceções de classe. Os modificadores, relações, durações, concentração e efeitos de turno descritos acima já estão integrados para Ordem/Tormenta20; as condições de D&D afetam testes, ataques e concentração.
- Catálogos de escolhas de origem, espécie/raça, classe, trilha/subclasse, poderes, talentos, equipamentos e magias/rituais; pré-requisitos e escolhas em cada nível.
- Conjuração completa: preparação/conhecimento, componentes, concentração dos demais sistemas, sustentação, alcance, áreas e efeitos específicos; características de classe ainda não descritas, reservas de subclasses e suas exceções. As reservas básicas de D&D 2014, suas recuperações, Fúria, Ataque Descuidado e Crítico Brutal descritos acima já estão implementados.
- Demais ocupações e especialidades além dos perfis configurados, métodos alternativos de criação, envelhecimento entre aventuras, tratamentos, crises de insanidade, perseguições e combate percentual completo. A geração aleatória, ajustes iniciais por idade e distribuição por ocupação/interesses descritos acima já estão implementados.
- Exploração, sobrevivência, ambientes, viagens, fabricação, economia, regras opcionais e adversários dos suplementos.

Uma interface com campo livre para registrar uma exceção não constitui implementação automática dessa regra. O checklist só deve mudar quando houver regra executável, integração persistida e teste verificável.
