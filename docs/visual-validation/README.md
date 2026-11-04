# Validação visual das fichas

## Ameaças de Ordem Paranormal · 04/11/2026

Capturas reais de NPCs gravados na API e no PostgreSQL de teste, feitas por `web/threats-integration.mjs` com Chrome/Playwright. A ficha utiliza o agrupamento de estatísticas de ameaças do PDF 1.3: nome/VD, descritores, atributos, sentidos/testes, defesas, PV e ações. O cabeçalho do módulo fica compacto ao abrir um inimigo; o elenco pode ser recolhido no celular. Não usa a estrutura de ficha de agente nem reproduz arte ou fichas oficiais.

| Interface | 1440×900 | 1920×1080 | 768×1024 | 390×844 |
| --- | --- | --- | --- | --- |
| Ameaça | [desktop](rules/ordem_paranormal-threat-desktop.png) | [amplo](rules/ordem_paranormal-threat-wide.png) | [tablet](rules/ordem_paranormal-threat-tablet.png) | [mobile](rules/ordem_paranormal-threat-mobile.png) |
| Editor | [desktop](rules/ordem_paranormal-threat-editor-desktop.png) | — | — | [mobile](rules/ordem_paranormal-threat-editor-mobile.png) |
| Incorporeidade, fortificação e imunidades | [desktop](rules/ordem_paranormal-threat-defenses-desktop.png) | [amplo](rules/ordem_paranormal-threat-defenses-wide.png) | [tablet](rules/ordem_paranormal-threat-defenses-tablet.png) | [mobile](rules/ordem_paranormal-threat-defenses-mobile.png) |
| Percepção e contexto de oponente | [desktop](rules/ordem_paranormal-threat-perception-desktop.png) | [amplo](rules/ordem_paranormal-threat-perception-wide.png) | [tablet](rules/ordem_paranormal-threat-perception-tablet.png) | [mobile](rules/ordem_paranormal-threat-perception-mobile.png) |

As capturas de percepção usam `web/threat-perception-integration.mjs`, Chrome e a API/PostgreSQL locais, com criação real de uma ameaça autoral pela interface. O painel está aberto para mostrar configuração e benefícios contra um oponente invisível a 4,5m; normalmente ele permanece recolhido durante o jogo. As quatro resoluções passaram pela checagem de overflow; desktop e mobile foram inspecionados visualmente. O teste confere prévia versus resultado do servidor, alcance, origem, sentidos indisponíveis, dados de ataque/Reflexos, rascunho inválido preservado, conflito, sigilo, recarga, exportação e templates.

As capturas de defesas especiais também usam uma ameaça autoral persistida pela API local. A interface configura e conserva incorporeidade, fortificação e imunidade a paralisia após recarregar; o teste confirma a ausência da condição derivada lento e usa os controles reais para dano comum bloqueado e dano de item amaldiçoado com adicionais de crítico/furtivo impedidos. As quatro resoluções foram capturadas e verificadas quanto a overflow; desktop e mobile receberam inspeção visual direta.

A inspeção considera hierarquia, densidade, reorganização das colunas e controles utilizáveis. O teste verifica ausência de overflow horizontal do documento, criação pela interface, autosave com resposta real atrasada, foco, conflitos, erros sem descarte do rascunho, sequências de ataque, privacidade de estatísticas/modelos, Presença e SAN, sincronização de encontros, histórico/restauração, templates e importação/exportação. O schema/motor também têm testes determinísticos e a migração foi aplicada no banco local existente. A documentação de [arquitetura, cobertura e pendências](../paranormal-threats.md) distingue regras executáveis de descrições consultáveis.

## Validação integrada de regras — 30/10/2026

Atualização de 04/11/2026: D&D 2014 v5 foi validado com dano por tipo, defesas, Fúria e seus controles de turno, Ataque Descuidado e Crítico Brutal. As capturas abaixo usam personagens gravados na API e no PostgreSQL local, com Chrome/Playwright. A regressão dos quatro sistemas, testes .NET, smoke, templates, lint e build passaram. A inspeção verificou reorganização do combate no tablet/celular, legibilidade e ausência de overflow do documento. A correspondência estrutural com as referências mantém as limitações descritas adiante.

| D&D 2014 integrado | 1440×900 | 1920×1080 | 768×1024 | 390×844 |
| --- | --- | --- | --- | --- |
| Dano, defesas e Fúria | [desktop](rules/dnd_5e-damage-rage-desktop.png) | [amplo](rules/dnd_5e-damage-rage-wide.png) | [tablet](rules/dnd_5e-damage-rage-tablet.png) | [mobile](rules/dnd_5e-damage-rage-mobile.png) |
| Ataques do bárbaro | [desktop](rules/dnd_5e-barbarian-attacks-desktop.png) | [amplo](rules/dnd_5e-barbarian-attacks-wide.png) | [tablet](rules/dnd_5e-barbarian-attacks-tablet.png) | [mobile](rules/dnd_5e-barbarian-attacks-mobile.png) |

Os testes de navegador verificam também privacidade de Fúria Implacável, permanência do primeiro ataque após refresh, reação sem vantagem ofensiva, fim/início de turno, controles compartilhados para layouts personalizados e migração explícita v4→v5 preservando armas, PV e moedas. Testes determinísticos cobrem os críticos e suas quantidades de dados sem depender de sorte no navegador. A cobertura mecânica e as pendências estão em [rules-engine.md](../rules-engine.md).

Atualização de 03/11/2026: [recursos de classe de D&D](rules/dnd_5e-class-resources-desktop.png) incluem configuração de níveis na ficha, reservas calculadas no servidor, cura por Retomar o Fôlego, rolagem exclusiva do mestre, consumo de Ki, meditação e criação/expiração de espaços por feitiçaria. O teste integrado executa essas ações e recarrega a página para verificar os dados persistidos.

As capturas atuais em [rules](rules/) foram feitas com `web/rules-integration.mjs`, Chrome, a API .NET local e PostgreSQL de teste. Esse conjunto usa autenticação e persistência reais e cobre os quatro sistemas nas resoluções 1440×900, 1920×1080, 768×1024 e 390×844. O script verifica permissões, conflito de versões, histórico, ataques privados, consumo de munição, aplicação de condições pelo formulário, rolagens nativas e leitura após recarregar. Em Ordem, a coluna de dados usa a prévia do servidor; em Tormenta20, o modificador da perícia considera as condições. As capturas não utilizam contas de produção.

## Capturas iniciais de composição

Capturas reais do Angular local em Chrome headless, feitas com `web/visual-validation.mjs`. O navegador recebe respostas de API simuladas com personagens de ensaio; nenhum dado fictício é embutido nas fichas da aplicação. Para repetir: `cd web`, `npm start` e, em outro terminal, `npm run visual:capture`.

| Ficha | 1440×900 | 1920×1080 | 768×1024 | 390×844 |
| --- | --- | --- | --- | --- |
| Ordem Paranormal | [desktop](ordem-desktop.png) | [amplo](ordem-wide.png) | [tablet](ordem-tablet.png) | [mobile](ordem-mobile.png) |
| D&D 5e | [desktop](dnd-desktop.png) | [amplo](dnd-wide.png) | [tablet](dnd-tablet.png) | [mobile](dnd-mobile.png) |
| Call of Cthulhu 7e | [desktop](coc-desktop.png) | [amplo](coc-wide.png) | [tablet](coc-tablet.png) | [mobile](coc-mobile.png) |
| Tormenta20 | [desktop](t20-desktop.png) | [amplo](t20-wide.png) | [tablet](t20-tablet.png) | [mobile](t20-mobile.png) |

## Comparação estrutural

- **Ordem Paranormal:** a imagem pública indexada do C.R.I.S. orientou a roda de cinco atributos interligados, barras de Vida/Sanidade/Esforço e perícias compactas em coluna à direita. A implementação mantém esse eixo visual e acrescenta controles próprios do Crônicas. No celular, a tabela passa abaixo dos recursos e comprime as colunas para manter todas as rolagens visíveis.
- **D&D 5e:** a imagem pública indexada do D&D Beyond e a ficha oficial de 2024 orientaram o cabeçalho curto, seis caixas de atributo, resistências à esquerda, perícias no centro e combate à direita. No celular, o painel de combate sobe antes das listas para manter HP e AC acessíveis.
- **Call of Cthulhu 7e:** a [ficha oficial da Chaosium](https://www.chaosium.com/content/FreePDFs/CoC/Character%20Sheets/V2/CoC7%20PC%20Sheet%20-%20Auto-Fill%20-%201920s%20-%20Standard%20-%20Color.pdf) orientou o dossiê de investigador, características com metade e quinto, recursos de sanidade/sorte e perícias percentuais. A implementação usa um layout documental próprio, com teste percentual e dados de bônus ou penalidade.
- **Tormenta20:** a [ficha oficial da Jambô](https://jamboeditora.com.br/wp-content/uploads/2023/07/Tormenta20-Jogo-do-Ano-Ficha.pdf) orientou identidade, modificadores de atributo, PV/PM, Defesa, ataques e perícias. O template persistido atual contém o total de cada perícia; sua decomposição em treino, atributo e nível ainda não é armazenada.
- Em ambas, a ficha começa abaixo da navegação global do Crônicas. A página não tem rolagem horizontal em nenhuma das quatro resoluções.

Os URLs diretos dos screenshots solicitados de C.R.I.S. e D&D Beyond retornaram erro de conexão em 28/10/2026. Não há comparação de pixels lado a lado com os arquivos originais nem alegação de equivalência visual exata. Foram consultadas as imagens indexadas e as fichas oficiais: [Ordem Paranormal](https://jamboeditora.com.br/wp-content/uploads/woocommerce_uploads/2023/02/jamboeditora-ordem-paranormal-ficha-de-personagem-oeuvst.pdf) e [D&D 2024](https://media.dndbeyond.com/compendium-images/free-rules/downloads/2024-character-sheet.pdf).

O script também verificou um ajuste rápido de recurso, uma rolagem e a edição de atributo de cada ficha, com salvamento simulado e leitura após recarregar. Isso testa a ligação da interface com o fluxo HTTP; o banco e a autenticação de produção não fizeram parte desta captura.
