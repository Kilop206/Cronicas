# Validação visual das fichas

## Validação integrada de regras — 30/10/2026

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
