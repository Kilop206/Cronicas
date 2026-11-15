"""Build the name/page index from the user's local rulebook (no prose/art is exported)."""
import json
import re
import unicodedata
from pathlib import Path
from pypdf import PdfReader

root = Path(__file__).resolve().parent.parent
source = next(root.glob('Feiticeiros*Livro*.pdf'))
groups = [(53, 61, 'Lutador'), (66, 77, 'Especialista em Combate'),
          (81, 89, 'Especialista em Técnica'), (93, 101, 'Controlador'),
          (105, 113, 'Suporte'), (118, 125, 'Restringido'),
          (163, 168, 'Talentos gerais'), (169, 170, 'Talentos de origem'),
          (174, 177, 'Aura'), (178, 181, 'Controle e Leitura'),
          (182, 184, 'Domínio'), (188, 188, 'Barreira'),
          (190, 191, 'Energia Reversa'), (192, 194, 'Aptidões especiais')]
reader = PdfReader(source)
entries = []
for start, end, category in groups:
    minimum = None
    for page in range(start, end + 1):
        for line in (reader.pages[page - 1].extract_text() or '').splitlines():
            title = ' '.join(line.split())
            level = re.fullmatch(r'HABILIDADES DE (\d+).*NÍVEL', title)
            if level:
                minimum = int(level.group(1))
            if not title.isupper() or not 4 <= len(title) <= 85:
                continue
            if title.startswith(('HABILIDADES ', 'APTIDÕES DE ', 'TALENTOS ', 'TABELA ', 'NÍVEL ', 'GANHOS ', 'PONTOS ', 'PRÉ-REQUISITO')):
                continue
            if title in {g[2].upper() for g in groups if g[2] != 'Energia Reversa'}:
                continue
            slug = re.sub(r'[^a-z0-9]+', '_', unicodedata.normalize('NFKD', title).encode('ascii', 'ignore').decode().lower()).strip('_')
            if not any(e['id'] == f'{category}:{slug}' for e in entries):
                entries.append({'id': f'{category}:{slug}', 'title': title, 'category': category, 'page': page, 'minimumLevel': minimum})
target = root / 'web/src/app/features/sheets/renderers/feiticeiros/fm-book-index.json'
target.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Indexed {len(entries)} names with rulebook page locators; no ability effects inferred.')
