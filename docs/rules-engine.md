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

## Conferência e diferenças entre fontes

As versões Ordem v7, D&D v4, Tormenta20 v4 e CoC v5 acrescentam ataques estruturados com cálculo de dano no servidor. O campo `ataques_regras` é adicionado na atualização explícita, preservando as tabelas antigas de armas e ataques. A base usa uma chave numérica do próprio schema. Em Ordem, o servidor inclui treino e outros modificadores da tabela de perícias e trata atributo zero com dois dados conservando o menor.

D&D dobra os dados base e extras no crítico; Ordem e Tormenta20 multiplicam os dados base conforme a arma, mantendo dados extras e bônus fixos. CoC usa testes percentuais, mau funcionamento, dano máximo e empalamento, distinguindo armas a distância. Munição é descontada na mesma transação da rolagem, inclusive em falhas; −1 representa ataques sem consumo controlado. O alvo recebe o dano por uma ação separada, depois de aplicar proteção, resistências e imunidades. O motor ainda não infere oposição, alcance ou elegibilidade apenas do nome da arma.

O interpretador `DamageFormula` aceita até 12 termos de dados/números e referências a campos numéricos, com limites de quantidade; não aceita funções, objetos, acesso a arquivos ou execução de código. Dano base e extra ficam separados para que os multiplicadores de cada edição sejam aplicados corretamente. Os testes verificam críticos, consumo de munição, privacidade, persistência e recarga usando a API real de teste.

Em D&D, configurar níveis também atualiza o nível total e os espaços disponíveis da ficha. Gastar um espaço ou concluir descanso longo atualiza tanto o estado do motor quanto os campos da aba de magias. A escolha de classe conjuradora preenche atributo, CD e bônus de ataque, sem misturar o atributo de classes diferentes.

Os capítulos completos dos quatro livros básicos locais foram extraídos para arquivos temporários para conferência. Esses arquivos e os PDFs não são publicados. Ações são código e matemática; não incluem descrições de poderes, magias, aventuras ou imagens dos livros.

O Livro Básico de CoC 7e consultado trata dano **maior** que PV máximos como morte imediata; o quick-start usa **maior ou igual**. O motor segue o livro básico e tem teste específico para o caso de igualdade. A recuperação semanal em extremo usa 2d3, também conferida na [referência oficial da Chaosium](https://callofcthulhuwiki.chaosium.com/rules/hit-points-wounds-and-healing.html). Movimento 9 exige FOR e DES estritamente maiores que TAM; valores iguais resultam em 8.

## Validação reproduzível

- `dotnet run --project tests/Chronicles.Rules.Tests`: testes determinísticos de custos, recursos, morte, descanso, espaços, multiclasse, ferimentos, sanidade e desenvolvimento.
- `node scripts/check-sheet-templates.mjs`: schemas, fórmulas, versões e resolução dos quatro renderizadores.
- `npm run build` e `npm run lint` em `web`.
- API local com PostgreSQL de teste, frontend na porta 4200 e `node rules-integration.mjs` em `web`: persistência real, autenticação, permissões, versão otimista, histórico e ações no navegador, com screenshots nas quatro resoluções em `docs/visual-validation/rules`.

## Regras pendentes — não declarar cobertura integral

- Aplicação de dano diretamente ao alvo, resistências/imunidades por tipo, alcance, munições e propriedades especiais de cada arma. Ataques, críticos e múltiplos componentes de dano já estão integrados ao motor.
- Condições com modificadores aplicados automaticamente a cada teste, ações disponíveis, deslocamento e duração; efeitos acumulados e exceções de classe.
- Catálogos de escolhas de origem, espécie/raça, classe, trilha/subclasse, poderes, talentos, equipamentos e magias/rituais; pré-requisitos e escolhas em cada nível.
- Conjuração completa: preparação/conhecimento, componentes, concentração, sustentação, alcance, áreas e efeitos específicos; recuperação de recursos de características individuais.
- Criação de investigador por idade/ocupação, distribuição de pontos, perícias especializadas, tratamentos, crises de insanidade, perseguições e combate percentual completo.
- Exploração, sobrevivência, ambientes, viagens, fabricação, economia, regras opcionais e adversários dos suplementos.

Uma interface com campo livre para registrar uma exceção não constitui implementação automática dessa regra. O checklist só deve mudar quando houver regra executável, integração persistida e teste verificável.
