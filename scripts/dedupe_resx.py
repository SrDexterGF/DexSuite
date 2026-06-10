"""
dedupe_resx.py  —  Elimina entradas duplicadas en archivos .resx.
Conserva la PRIMERA aparición de cada clave (las originales están primero).
"""
import os, re

BASE = os.path.join(os.path.dirname(__file__), '..', 'src', 'DexSuite.App', 'Resources')

def dedupe_file(path):
    with open(path, 'rb') as f:
        raw = f.read(3)
    enc = 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'

    with open(path, 'r', encoding=enc) as f:
        content = f.read()

    # Extraer todas las entradas <data>
    pattern = re.compile(
        r'(\s*<data\s+name="([^"]+)"[^>]*>.*?</data>)',
        re.DOTALL)

    seen = set()
    def replace_dupes(m):
        key = m.group(2)
        if key in seen:
            return ''   # eliminar duplicado
        seen.add(key)
        return m.group(1)

    new_content = pattern.sub(replace_dupes, content)

    if new_content != content:
        removed = len(pattern.findall(content)) - len(pattern.findall(new_content))
        with open(path, 'w', encoding=enc) as f:
            f.write(new_content)
        return removed
    return 0

for f in sorted(os.listdir(BASE)):
    if not (f.startswith('Strings.') and f.endswith('.resx') and f != 'Strings.resx'):
        continue
    path = os.path.join(BASE, f)
    removed = dedupe_file(path)
    lang = f.replace('Strings.', '').replace('.resx', '')
    if removed > 0:
        print(f'{lang}: {removed} duplicados eliminados')
    else:
        print(f'{lang}: OK')

print('Listo.')
