# Validação visual das fichas

Capturas reais do Angular local em Chrome headless, feitas com `web/visual-validation.mjs`. O navegador recebe respostas de API simuladas com personagens de ensaio; nenhum dado fictício é embutido nas fichas da aplicação. Para repetir: `cd web`, `npm start` e, em outro terminal, `npm run visual:capture`.

| Ficha | 1440×900 | 1920×1080 | 768×1024 | 390×844 |
| --- | --- | --- | --- | --- |
| Ordem Paranormal | [desktop](ordem-desktop.png) | [amplo](ordem-wide.png) | [tablet](ordem-tablet.png) | [mobile](ordem-mobile.png) |
| D&D 5e | [desktop](dnd-desktop.png) | [amplo](dnd-wide.png) | [tablet](dnd-tablet.png) | [mobile](dnd-mobile.png) |

## Comparação estrutural

- **Ordem Paranormal:** a imagem pública indexada do C.R.I.S. orientou a roda de cinco atributos interligados, barras de Vida/Sanidade/Esforço e perícias compactas em coluna à direita. A implementação mantém esse eixo visual e acrescenta controles próprios do Crônicas. No celular, a coluna de perícias passa abaixo dos recursos e pode rolar horizontalmente dentro da tabela.
- **D&D 5e:** a imagem pública indexada do D&D Beyond e a ficha oficial de 2024 orientaram o cabeçalho curto, seis caixas de atributo, resistências à esquerda, perícias no centro e combate à direita. No celular, o painel de combate sobe antes das listas para manter HP e AC acessíveis.
- Em ambas, a ficha começa abaixo da navegação global do Crônicas. A página não tem rolagem horizontal em nenhuma das quatro resoluções.

Os URLs diretos dos screenshots solicitados de C.R.I.S. e D&D Beyond retornaram erro de conexão em 28/10/2026. Não há comparação de pixels lado a lado com os arquivos originais nem alegação de equivalência visual exata. Foram consultadas as imagens indexadas e as fichas oficiais: [Ordem Paranormal](https://jamboeditora.com.br/wp-content/uploads/woocommerce_uploads/2023/02/jamboeditora-ordem-paranormal-ficha-de-personagem-oeuvst.pdf) e [D&D 2024](https://media.dndbeyond.com/compendium-images/free-rules/downloads/2024-character-sheet.pdf).

O script também verificou um ajuste rápido de recurso, uma rolagem e a edição de atributo de cada ficha, com salvamento simulado e leitura após recarregar. Isso testa a ligação da interface com o fluxo HTTP; o banco e a autenticação de produção não fizeram parte desta captura.
