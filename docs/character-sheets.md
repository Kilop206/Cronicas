# Fichas dinâmicas

O módulo usa o sistema da campanha como template. Um template define campos tipados, seções, páginas e blocos; cada personagem guarda apenas seus valores. Campos numéricos podem ter fórmulas e botões de rolagem. Inventário, habilidades, versões da ficha e notas exclusivas do mestre continuam separados do esquema.

## Referências funcionais

| Plataforma | Padrão observado | Aplicação no Crônicas |
| --- | --- | --- |
| [C.R.I.S.](https://cris-ordem-paranormal.web.app/) | Ficha digital, dados e acesso do mestre à mesa | Ficha da campanha, dados compartilhados e permissão por papel |
| [D&D Beyond](https://www.dndbeyond.com/characters/builder) | Recursos, inventário, progressão e cálculos durante a criação | Campos de progressão e fórmulas definidas pelo autor do template |
| [Roll20](https://wiki.roll20.net/Character_Sheets) | Campos de ficha ligados a atributos e rolagens | Chaves de campo reutilizadas em fórmulas e modificadores de dados |
| [Quest Portal](https://www.questportal.com/documentation) | Fichas universais e templates reaproveitáveis | Template livre ou diagramado por sistema da campanha |
| [FichasOP](https://beta.fichasop.com/) | Fichas digitais voltadas a um sistema específico | Editor que aceita sistemas existentes sem incluir regras ou conteúdo de terceiros |
| [Dark Age RPG](https://darkagerpg.com/sobre) | Ficha universal, cálculo e inventário no navegador | Composição sem esquema fixo e coleções independentes |

As opções iniciais de aventura fantástica e investigação são genéricas. O catálogo inclui modelos não oficiais de [Ordem Paranormal](https://jamboeditora.com.br/wp-content/uploads/woocommerce_uploads/2023/02/jamboeditora-ordem-paranormal-ficha-de-personagem-oeuvst.pdf), [D&D 5e](https://media.dndbeyond.com/compendium-images/free-rules/downloads/2024-character-sheet.pdf), [Tormenta20](https://jamboeditora.com.br/wp-content/uploads/2023/06/Tormenta20-Resumo-de-Regras.pdf) e [Call of Cthulhu 7e](https://www.chaosium.com/content/FreePDFs/CoC/CHA23131%20Call%20of%20Cthulhu%207th%20Edition%20Quick-Start%20Rules.pdf). Eles usam campos funcionais curtos e a interface própria do Crônicas, sem reproduzir fichas oficiais, textos extensos, arte ou identidade visual de terceiros.

## Catálogo de modelos

Em **Seus sistemas**, escolha um modelo e salve uma cópia editável. O mestre associa essa cópia à campanha. Personagens existentes seguem o sistema já escolhido para a campanha; criar um modelo novo não altera campanhas automaticamente.

Os modelos ficam em `web/src/app/system-templates.ts`. Cada entrada define metadados, campos e uma composição própria de páginas e blocos, usando o mesmo esquema que o editor e a API validam. Ordem Paranormal tem dossiê e áreas de investigação; D&D agrupa atributos, resistências e treino; Tormenta20 separa combate, poderes e mochila; Call of Cthulhu mostra cada característica junto aos limiares derivados. As aparências também são escolhidas por modelo, dentro da linguagem visual do Crônicas. Para adicionar um sistema, acrescente uma entrada com seus campos, páginas, blocos, recursos e aparência, e execute `node scripts/check-sheet-templates.mjs`. O arquivo de teste verifica limites, disposição dos campos, fórmulas e rolagens.

Uma cópia já salva conserva sua composição até que o dono escolha **Usar composição do modelo** no editor e salve. Essa ação altera apenas layout e aparência; os campos e valores existentes permanecem. O catálogo é um ponto de partida: custos, proficiências, bônus e progressão que dependem da classe, edição ou mesa podem ser ajustados no editor.

Na ficha em jogo, atributos podem aparecer como valores em destaque e perícias como linhas compactas com rolagem ao lado. Recursos têm ajustes rápidos de um ponto e permanecem visíveis no resumo ao trocar de página. O dono alterna entre visualização e edição; as alterações continuam sendo salvas automaticamente. Esses padrões de consulta rápida seguem funcionalidades descritas pelo [C.R.I.S.](https://cris-ordem-paranormal.web.app/), [D&D Beyond](https://dndbeyond-support.wizards.com/hc/en-us/articles/7747193946388-Sheet-Sections), [Quest Portal](https://www.questportal.com/library/systems) e [Dark Age RPG](https://darkagerpg.com/sobre). Os estilos `score` e `compact` podem ser usados por qualquer sistema futuro no compositor, com a aparência própria do Crônicas.

## Como configurar

Em **Seus sistemas**, crie uma ficha livre ou diagramada. Para progressão, crie campos como `nivel` e `experiencia`; um terceiro campo numérico pode usar `nivel * 100` como cálculo automático. Fórmulas aceitam números inteiros, chaves de outros campos numéricos, parênteses, `+`, `-`, `*`, `/` e `floordiv(a,b)` para divisão arredondada para baixo. A divisão comum é inteira e trunca em direção a zero. Campos podem ter valor inicial. Campos calculados ficam somente para leitura e a API refaz o cálculo ao salvar. Dependências circulares, chaves inexistentes, divisão por zero e resultados fora dos limites são rejeitados.

Uma rolagem pode usar `1d20+3`, um modificador de campo como `1d20+{bonus}` ou uma quantidade dinâmica de dados como `{agilidade}d20kh1+{luta}`. Ela passa pelo serviço de dados da campanha, que registra o resultado compartilhado. Inventário e habilidades podem ser adicionados ao layout como blocos; em fichas livres ficam nas abas próprias.

O personagem pertence ao jogador que o criou. Dono da campanha, mestre e co-mestre podem editá-lo. Espectadores só leem. Notas secretas usam endpoint separado e são acessíveis apenas a esses papéis de mestre. O editor salva a ficha após uma pausa de 900 ms e usa versão para sinalizar conflitos de edição.
