"""
remove_keys.py — Elimina claves <data> obsoletas de todos los Strings*.resx.
Lista de claves a borrar definida en KEYS_TO_REMOVE.
"""
import os, re, io

BASE = os.path.join(os.path.dirname(__file__), '..', 'src', 'DexSuite.App', 'Resources')
KEYS_TO_REMOVE = [
    "Gaming.Disclaimer.Title",
    "Gaming.Disclaimer.Message",
    "Gaming.Disclaimer.Accept",
]

def get_enc(path):
    with open(path, 'rb') as f:
        raw = f.read(3)
    return 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'

total = 0
for fn in sorted(os.listdir(BASE)):
    if not (fn.startswith('Strings') and fn.endswith('.resx')):
        continue
    path = os.path.join(BASE, fn)
    enc = get_enc(path)
    with io.open(path, 'r', encoding=enc) as f:
        content = f.read()
    removed = 0
    for key in KEYS_TO_REMOVE:
        pat = re.compile(
            r'\n?[ \t]*<data name="' + re.escape(key) + r'"[^>]*>.*?</data>',
            re.DOTALL)
        content, n = pat.subn('', content)
        removed += n
    if removed:
        with io.open(path, 'w', encoding=enc) as f:
            f.write(content)
        total += removed
        lang = fn.replace('Strings.', '').replace('.resx', '') or 'en(base)'
        print(f"{lang}: {removed} eliminadas")
print(f"\nTotal eliminadas: {total}")
