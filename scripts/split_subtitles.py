"""
split_subtitles.py — Parte 4 claves de subtítulo en dos: la clave original
conserva la 1ª frase, una nueva clave "{key}.Line2" recibe la 2ª.
Trabaja DIRECTAMENTE sobre los .resx (generate-resx.ps1 es destructivo, no se usa).
Idempotente: si Line2 ya existe, salta esa clave.
"""
import os, re, io, sys

BASE = os.path.join(os.path.dirname(__file__), '..', 'src', 'DexSuite.App', 'Resources')
KEYS = [
    "Modules.Subtitle",
    "Home.Card.QuickClean.Desc",
    "Settings.Subtitle",
    "Settings.Theme.Description",
]

def split_value(v):
    """Devuelve (parte1, parte2) o None si no se puede partir."""
    v = v.strip()
    # 1) salto de línea (textos viejos multi-línea)
    if "\n" in v:
        a, b = v.split("\n", 1)
        return a.strip(), b.strip()
    # 2) terminador asiático
    for term in ("。", "！", "？"):
        i = v.find(term)
        if 0 <= i < len(v) - 1:
            return v[:i+1].strip(), v[i+1:].strip()
    # 3) punto + espacio (textos humanizados es/en y derivados)
    m = re.search(r'\.\s+', v)
    if m and m.end() < len(v):
        return v[:m.start()+1].strip(), v[m.end():].strip()
    return None

def get_enc(path):
    with open(path, 'rb') as f:
        raw = f.read(3)
    return 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'

def esc(t):
    return t.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

def process(path):
    enc = get_enc(path)
    with io.open(path, 'r', encoding=enc) as f:
        content = f.read()
    changed = 0
    skipped = []
    for key in KEYS:
        line2_key = key + ".Line2"
        if f'name="{line2_key}"' in content:
            continue  # ya partido
        # capturar el bloque <data name="key" ...>...<value>X</value>...</data>
        pat = re.compile(
            r'(<data name="' + re.escape(key) + r'"[^>]*>\s*<value>)(.*?)(</value>\s*</data>)',
            re.DOTALL)
        m = pat.search(content)
        if not m:
            continue
        raw_val = m.group(2)
        # des-escapar mínimo para partir
        plain = raw_val.replace('&amp;', '&').replace('&lt;', '<').replace('&gt;', '>')
        parts = split_value(plain)
        if not parts:
            skipped.append(key)
            continue
        p1, p2 = parts
        # reemplazar valor original por p1
        new_block = m.group(1) + esc(p1) + m.group(3)
        # añadir nueva entrada Line2 justo después
        new_entry = f'\n  <data name="{line2_key}" xml:space="preserve">\n    <value>{esc(p2)}</value>\n  </data>'
        content = content[:m.start()] + new_block + new_entry + content[m.end():]
        changed += 1
    if changed or skipped:
        with io.open(path, 'w', encoding=enc) as f:
            f.write(content)
    return changed, skipped

total = 0
for fn in sorted(os.listdir(BASE)):
    if fn.startswith('Strings') and fn.endswith('.resx'):
        path = os.path.join(BASE, fn)
        changed, skipped = process(path)
        lang = fn.replace('Strings.', '').replace('.resx', '') or 'en(base)'
        status = f"{changed}/4"
        if skipped:
            status += f"  NO-SPLIT: {','.join(skipped)}"
        print(f"{lang}: {status}")
        total += changed
print(f"\nTotal claves partidas: {total}")
