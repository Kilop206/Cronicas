# Fichas de ameaças · Ordem Paranormal 1.3

O módulo de NPCs oferece uma ficha própria para inimigos, sem usar NEX, SAN, bloqueio, esquiva ou contra-ataque de agentes. Nenhum bestiário é cadastrado automaticamente. O mestre informa as estatísticas de suas ameaças e pode salvar modelos reutilizáveis. Os testes usam conteúdo autoral de ensaio, apenas no banco local.

## Criar e usar

1. Associe a campanha a um sistema identificado como `ordem_paranormal` / `1.3`.
2. Em **NPCs e ameaças**, use **Novo inimigo**. Também é possível acrescentar uma ficha de inimigo a um NPC narrativo existente.
3. Preencha os bônus finais da ameaça, seus atributos, PV, Defesa, sentidos, deslocamentos, ataques e habilidades. Os bônus de tamanho/equipamento já presentes nessas estatísticas não são somados novamente.
4. Salve para criar. Depois disso, a edição tem autosave com debounce de 700 ms. Alterações digitadas durante uma requisição aguardam o salvamento seguinte; os controles mantêm o foco.
5. Na ficha, selecione privacidade e, se necessário, DT ou Defesa do alvo. Execute testes, sequências de ataque, habilidades e ações de recursos. **Somente mestres** é o padrão de rolagem.

**Criar cópia nova** prepara uma nova instância com PV/PE completos, usos restaurados e sem condições/exposições da cena anterior. A munição cadastrada é copiada como está; não existe um máximo de munição inferido pelo nome da arma. **Salvar como modelo** fica disponível ao dono do sistema, até doze modelos por sistema. O editor do sistema permite alterar a ficha desses modelos com os mesmos componentes visuais.

## Dados e seleção de renderizadores

O NPC mantém sua identidade, campanha, visibilidade, retrato, narrativa, segredos, PV e versão nas entidades existentes. A migração aditiva `20261104120000_NpcThreatSheets` acrescenta:

- `SheetJson` (`jsonb`, opcional): ficha mecânica versionada.
- `HistoryJson` (`jsonb`): até vinte snapshots anteriores do NPC completo, reservados aos mestres.

NPCs existentes continuam sem ficha mecânica até uma conversão explícita. Atualizações de clientes antigos que omitem `sheet` preservam a ficha já associada. As consultas de listagem/leitura não carregam os snapshots; o histórico só é lido quando necessário para edição, execução ou consulta privada. A exportação de conta do dono da campanha inclui os dados novos.

O envelope identifica `templateId = ordem_paranormal`, `rulesetVersion = 1.3`, `version = 1` e `systemId`. Na criação, a API vincula `systemId` ao sistema real da campanha. A origem não pode ser trocada silenciosamente em uma edição. Mudanças posteriores do sistema da campanha não apagam a ficha da ameaça; a Presença só atua em personagens de uma campanha compatível com Ordem 1.3.

`resolveThreatRenderer` escolhe o componente usando o envelope, sem comparar nomes livres. `ParanormalThreatSchema` valida a definição; `ParanormalThreatRules` executa regras puras; `NpcSheetEndpoints` verifica acesso, versões e persistência; os componentes Angular cuidam da edição/apresentação. O schema admite os cinco atributos, tipos de ameaça/tamanho/elemento, perícias, ações, ataques, defesas, imunidades, Presença, Cura Acelerada, Enigma e estado da cena. A ficha mecânica tem limite de 60 mil caracteres, com limites próprios por coleção.

Para acrescentar outra versão, registre explicitamente sua validação, resolução de regras e renderizador. Uma migração deve converter uma cópia do JSON, preservar os identificadores de ataques/habilidades/fontes e guardar um snapshot antes da gravação. Não reinterpretar versões desconhecidas nem deduzir regras do nome de um inimigo.

## Cobertura mecânica

| Área | Comportamento executável |
| --- | --- |
| Testes | Dados por atributo; atributo zero conserva o menor de 2d20. Perícias usam os bônus finais configurados. Condições alteram a quantidade de dados e as falhas automáticas pertinentes. |
| Ataques | De um a dez ataques por ação, cada um com teste e dano próprios; munição e PE consumidos na mesma transação. Margem/multiplicador configuráveis. Crítico multiplica os dados-base; bônus fixos e dados extras não são multiplicados. |
| Habilidades | Custo, usos limitados/ilimitados, recuperação manual/por turno/por cena e rolagem aritmética limitada com referências numéricas. Descrições não executam código nem efeitos sobre alvos. |
| Dano | Um tipo por comando, resistência bem-sucedida antes dos demais ajustes, vulnerabilidade antes da RD, imunidade e exceções por tipo. Perdas diretas por fome/sede/sufocamento não são tratadas como dano reduzível. |
| Fontes | A estatística base usa a maior RD aplicável já consolidada. Itens, rituais e aliados não acumulam dentro de suas categorias. Habilidades com chaves distintas acumulam; linhas da mesma habilidade usam a maior aplicável. Vulnerabilidades configuradas respeitam os grupos e a combinação aditiva de multiplicadores. |
| Origem Paranormal | Imunidade a dano mental, condições mentais/de medo e rituais de Medo para a categoria paranormal; ameaças humanas/animais perdem PV com dano mental. Não se aplica imunidade apenas por um nome livre. |
| Condições | Aplicação, remoção, agravamentos, duração em rodadas/cena/persistente, modificadores de Defesa e deslocamento, impedimentos de ação, chamas, sangramento e asfixia. Deslocamento aceita valores fracionários. |
| Cura Acelerada | Cura no início de turno, sem presumir ressurreição. Registra PV perdidos para tipos que impedem essa cura e para fome/sede/sufocamento. Cura comum e edições de PV reconciliam esse saldo. |
| Enigma | Confirmação do mestre desativa somente as defesas marcadas como dependentes do Enigma. Não remove automaticamente Origem Paranormal nem interpreta o texto do enigma. |
| Imunidade a críticos | O recebimento de um crítico exige informar também o dano normal; o motor não tenta adivinhar os dados adicionais a remover de um total. |

As fórmulas usam `DamageFormula` e `DiceExpression`, com operações/dados/referências permitidos e limites de tamanho. Não há `eval`, JavaScript ou scripts fornecidos pelo usuário.

## Presença Perturbadora

O mestre configura DT, expressão de dano mental e limiar opcional de imunidade por NEX. Selecionar um personagem resolve Vontade no servidor, verifica NEX, reduz o dano à metade em sucesso e altera a SAN persistida. O personagem precisa pertencer à mesma campanha e ter os campos estruturados compatíveis. A alteração cria histórico da ficha; a rolagem segue a privacidade selecionada.

Em uma cena com várias criaturas, o mestre seleciona a de maior VD e declara as adicionais; cada adicional acrescenta 1d6. O módulo não identifica automaticamente quais criaturas foram vistas nem gerencia todas as cenas do mundo. Registra até cem alvos por ameaça/cena para evitar resolver a mesma exposição duas vezes. Confirmar uma nova cena limpa esse registro. A versão da ameaça e a versão do alvo são verificadas antes e durante a gravação, incluindo o caso de imunidade sem rolagem.

## Defesas especiais de ameaças

Em **Editar NPC**, configure **Incorpóreo**, **Fortificação (%)** e as categorias em **Imunidades a condições**. São extensões aditivas do schema 1: fichas anteriores recebem os valores neutros `false`, `0` e `[]`. Não há alteração de edição, substituição de dados ou nova migração SQL. Modelos, histórico e importação/exportação incluem os campos novos.

- **Incorporeidade:** o servidor impede dano de origem comum. O tipo de dano sozinho não define a origem; quem resolve informa item amaldiçoado, ritual ou outra ameaça incorpórea. Essas origens continuam sujeitas às demais imunidades, à RD e à Origem Paranormal. Perdas diretas por fome, sede e sufocamento mantêm seu processamento separado. Ameaças incorpóreas não fazem testes de Força; ataques usam Agilidade com o bônus final cadastrado. O atributo anterior fica guardado para uma eventual desativação, sem virar um teste normal de atributo zero. Fórmulas que ainda referenciem Força precisam ser corrigidas antes de ativar a habilidade.
- **Condições e origem:** ao aplicar um efeito, selecione sua origem. Efeitos comuns não afetam incorpóreos; rituais de Medo não afetam criaturas de origem paranormal, mesmo que causem uma condição de outra categoria. **Ajuste direto pelo mestre** opera o estado sem inferir uma fonte, mas respeita as imunidades da condição. Chamas guardam a origem aceita para o dano dos turnos seguintes. Atravessar sólidos, não manipular objetos e demais interações espaciais são adjudicados na cena; não existe simulação física/mapa automatizada.
- **Fortificação:** informe o percentual final somado das fontes aplicáveis, limitado a 100% porque esse valor já dá proteção completa. Cada comando de dano representa um acerto. De 1% a 99%, uma rolagem d100 no servidor determina se a proteção desse acerto impede os adicionais de crítico e/ou furtivo; 100% dispensa rolagem. O histórico de dados usa a privacidade selecionada. Imunidade somente a críticos não concede imunidade a furtivos.
- **Composição do dano:** **Valor** inclui todo o dano do tipo informado. **Dano sem crítico (ainda com furtivo)** remove apenas os dados adicionais do crítico e é obrigatório se a proteção puder impedi-lo. **Dano furtivo já incluído no total** é a parcela furtiva, não um bônus a somar. Por exemplo, total 25, sem crítico 15 e furtivo 5 resultam em 10 antes de RD se ambos forem impedidos. A proteção não elimina o dano normal do acerto. Fontes imunes não fazem rolagens de fortificação desnecessárias. Dano de tipos mistos ainda exige a conferência indicada nos limites abaixo.
- **Categorias de condição:** mentais, medo, paralisia, fadiga, sentidos ou todas as condições. A composição segue as categorias do apêndice da edição 1.3. Imunidades também filtram consequências derivadas: imune a paralisia pode continuar cego e desprevenido, mas não fica lento pela cegueira. Um agravamento para uma condição imune conserva a condição anterior. Ao configurar imunidade no editor, efeitos explícitos agora impedidos são removidos com aviso. Importações que tentem guardar esses efeitos são rejeitadas pelo servidor. Imunidade não permite agir a zero PV nem presume uma regra de retorno à vida.

`ParanormalThreatDefenses` mantém a seleção de fontes e a proteção de dano separadas do HTTP. `RuleState.ConditionImmunities` é uma projeção transitória das imunidades configuradas, sem novos marcadores persistidos na ficha do jogador. Personagens dos outros sistemas conservam o conjunto vazio padrão. A projeção Angular recebe o mesmo conjunto para manter Defesa, deslocamento e condições coerentes com o servidor.

## Permissões, histórico e encontros

Somente dono, mestre e co-mestre criam, editam ou operam ameaças. NPCs privados continuam invisíveis a jogadores. Em um NPC apresentado à campanha, jogadores recebem os dados narrativos e recursos que já eram públicos, mas não recebem `sheet`, segredos ou histórico. Modelos de ameaça dentro do sistema também são filtrados para jogadores; seus Enigmas não ficam expostos pelo endpoint de sistemas. Modelos narrativos mantêm sua visibilidade anterior.

`POST /api/npcs/{id}/rules` exige `{expectedVersion, command}` e usa o mesmo limitador por conta das rolagens de personagem. Atualizações e restaurações verificam versão. Um conflito retorna 409, conserva o rascunho e exige recarregar; não há sobrescrita automática. A navegação e o fechamento da página avisam sobre edições não salvas.

PV e condições mecânicas da ameaça sincronizam com seus participantes em encontros ainda não concluídos. Editar PV no encontro atualiza o NPC e seu histórico. Condições de ameaças são operadas na ficha para respeitar imunidades e modificadores; a lista do encontro oferece o link **Ficha**. Encontros concluídos conservam seu registro anterior. Para representar duas criaturas independentes, crie duas instâncias, em vez de colocar o mesmo NPC em dois combates e esperar recursos independentes.

O histórico permite restaurar a ficha completa, incluindo ataques, munição, recursos, narrativa e segredos. A versão substituída também é capturada, permitindo desfazer a restauração enquanto estiver entre as vinte versões retidas.

## Templates, importação e exportação

O arquivo de NPC usa `{format: "cronicas.npc", version: 1, data: {...}}`. A importação valida a ficha na API antes de renderizá-la e mantém a versão de concorrência do NPC que está sendo editado. A campanha/sistema de origem são vinculados pelo servidor; exposições de outra cena não são reaproveitadas. Segredos importados continuam privados. O download é uma ação explícita do mestre.

`NpcTemplate.sheet` faz parte dos snapshots e da exportação/importação dos sistemas existentes. O editor universal pode acrescentar uma ficha de ameaça a um modelo de NPC sem código. Arquivos de sistema têm validação estrutural antes de montar o editor e validação de regras no servidor ao salvar. Criar uma ameaça a partir de um modelo limpa estado de combate e restaura recursos configurados.

## Conferência e limites

Fontes locais: PDF Ordem Paranormal v1.3, páginas PDF 92–93 (dano/crítico), 188–190 (fichas, ações, imunidades, incorporeidade, RD, Cura Acelerada e Presença), 320–323 (condições, suas categorias, fortificação e acumulação). Os PDFs, textos integrais e fichas oficiais não entram no Git ou no site.

Este bloco **não representa todas as regras de Ordem implementadas**. Permanecem pendentes, entre outras:

- Catálogo e efeitos individuais das criaturas, poderes e rituais; não são copiados nem inferidos de descrições.
- Aplicação automática do ataque a uma ficha-alvo, defesas em fontes com vários tipos simultâneos e controle completo de ações/movimento/alcance/manobras. O recebimento atual calcula um tipo por comando; fontes mistas exigem adjudicação da RD da fonte pelo mestre.
- Sentidos especiais e exceções de habilidades com efeitos próprios; esses textos são consultáveis, sem automatização presumida. Interações espaciais de incorporeidade continuam sob adjudicação do mestre.
- Interações particulares de habilidades que substituam as categorias e imunidades configuradas; o motor não deduz exceções de textos narrativos.
- Gestão global de cenas/exposições, venenos/doenças individuais, inventário próprio de NPCs e apresentação específica de inimigos no OBS. O OBS existente de personagens é preservado.
- Regras específicas de morte/retorno e exceções individuais: zero PV é mostrado como fora de combate; o mestre decide o estado narrativo e os efeitos pertinentes.

## Validação reproduzível

- `dotnet run --project tests/Chronicles.Rules.Tests`: regras puras, entradas inválidas e correspondência entre modelo EF e snapshot da migração.
- `npm run lint`, `npm run build` e `node scripts/check-sheet-templates.mjs`.
- API e PostgreSQL locais, Angular na porta 4200: `node threats-integration.mjs` em `web`. Criação real, autosave com resposta atrasada/conflito, ataques, privacidade, encontros, SAN, imunidade, histórico, modelos e importação/exportação.
- `node rules-integration.mjs` e smoke geral preservam a regressão dos quatro sistemas.

Capturas reais ficam em `docs/visual-validation/rules/ordem_paranormal-threat-*.png`, em 1440×900, 1920×1080, 768×1024 e 390×844; o editor também possui capturas desktop/mobile. Nenhuma captura usa dados fixos embutidos no componente.
