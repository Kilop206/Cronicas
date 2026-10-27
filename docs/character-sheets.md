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

As opções iniciais de aventura fantástica e investigação são genéricas. Não contêm regras, textos, marcas visuais ou fichas oficiais dos sistemas citados.

## Como configurar

Em **Seus sistemas**, crie uma ficha livre ou diagramada. Um sistema existente pode ser representado com o nome e os campos que a mesa tiver direito de usar. Para progressão, crie campos como `nivel` e `experiencia`; um terceiro campo numérico pode usar `nivel * 100` como cálculo automático. Fórmulas aceitam números inteiros, chaves de outros campos numéricos, parênteses e `+`, `-`, `*`, `/`. A divisão é inteira. Campos calculados ficam somente para leitura e a API refaz o cálculo ao salvar. Dependências circulares, chaves inexistentes, divisão por zero e resultados fora dos limites são rejeitados.

Uma rolagem pode usar `1d20+3` ou um modificador de campo, como `1d20+{bonus}`. Ela passa pelo serviço de dados da campanha, que registra o resultado compartilhado. Inventário e habilidades podem ser adicionados ao layout como blocos; em fichas livres ficam nas abas próprias.

O personagem pertence ao jogador que o criou. Dono da campanha, mestre e co-mestre podem editá-lo. Espectadores só leem. Notas secretas usam endpoint separado e são acessíveis apenas a esses papéis de mestre. O editor salva a ficha após uma pausa de 900 ms e usa versão para sinalizar conflitos de edição.
