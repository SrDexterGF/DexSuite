"""
inject_translations.py
Traduce automáticamente las claves faltantes en cada Strings.{lang}.resx
usando Google Translate (deep_translator). Solo añade claves; nunca sobreescribe las existentes.

Uso:
    python inject_translations.py
"""

import os, re, time, xml.etree.ElementTree as ET
from deep_translator import GoogleTranslator

# Ruta base del proyecto (ajustar si se mueve el script)
BASE = os.path.join(os.path.dirname(__file__), '..', 'src', 'DexSuite.App', 'Resources')

# Mapa de código interno → código de idioma que acepta Google Translate
LANG_MAP = {
    'gl': 'gl', 'ca': 'ca', 'eu': 'eu', 'pt': 'pt', 'fr': 'fr',
    'de': 'de', 'it': 'it', 'zh': 'zh-CN', 'ru': 'ru', 'uk': 'uk',
    'ar': 'ar', 'ja': 'ja', 'ko': 'ko', 'hi': 'hi', 'bn': 'bn',
    'ur': 'ur', 'id': 'id', 'tr': 'tr', 'vi': 'vi', 'nl': 'nl',
    'sv': 'sv', 'ro': 'ro', 'pl': 'pl', 'cs': 'cs', 'el': 'el',
    'da': 'da', 'no': 'no', 'fi': 'fi',
    'bg': 'bg', 'hu': 'hu', 'pt-BR': 'pt', 'th': 'th', 'zh-TW': 'zh-TW',
}

# Claves a NO traducir: nombres de marca / textos técnicos que se dejan en inglés
SKIP_TRANSLATE = {
    'About.Social.Instagram', 'About.Social.Linktree',
    'Theme.Name.Midas', 'Theme.Name.Valor', 'Theme.Name.Fortress',
    'Theme.Name.Counter', 'Theme.Name.Legends', 'Theme.Name.Crafter',
    'Theme.Name.Apex', 'Theme.Name.Guardian', 'Theme.Name.Rivals',
    'Theme.Name.Tenno', 'Theme.Name.Divers',
    'Settings.Theme.GameThemesHeader',  # tiene emoji en el header, se queda en inglés
}


def load_resx_keys(path):
    """Devuelve dict {nombre: valor} de un archivo .resx."""
    try:
        tree = ET.parse(path)
        root = tree.getroot()
        return {d.get('name'): (d.find('value').text or '') for d in root.findall('data') if d.find('value') is not None}
    except Exception:
        return {}


def get_english_value(key, en_keys):
    return en_keys.get(key, '')


def append_resx_entry(resx_path, key, value):
    """Añade una entrada <data> al final del .resx (antes de </root>)."""
    # Detectar BOM y usar la codificación correcta
    with open(resx_path, 'rb') as fb:
        raw = fb.read(3)
    enc = 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'

    with open(resx_path, 'r', encoding=enc) as f:
        content = f.read()

    entry = f'\n  <data name="{key}" xml:space="preserve">\n    <value>{escape_xml(value)}</value>\n  </data>'

    content = content.rstrip()
    if content.endswith('</root>'):
        content = content[:-7] + entry + '\n</root>'
    else:
        content = content + entry + '\n</root>'

    with open(resx_path, 'w', encoding=enc) as f:
        f.write(content)


def escape_xml(text):
    return (text or '').replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')


def translate_text(text, target_lang):
    """Traduce text de inglés al idioma target. Preserva placeholders {0}, {1}, etc."""
    if not text or not text.strip():
        return text

    # Preservar placeholders antes de traducir
    placeholders = re.findall(r'\{[0-9]+\}', text)
    working = text
    tokens = {}
    for i, ph in enumerate(placeholders):
        token = f'XPLACEHOLDERX{i}X'
        working = working.replace(ph, token, 1)
        tokens[token] = ph

    try:
        translated = GoogleTranslator(source='en', target=target_lang).translate(working)
        if translated is None:
            return text
    except Exception as e:
        print(f'    ERROR traduciendo a {target_lang}: {e}')
        return text

    # Restaurar placeholders
    for token, ph in tokens.items():
        translated = translated.replace(token, ph)

    return translated


def main():
    en_path = os.path.join(BASE, 'Strings.resx')
    en_keys = load_resx_keys(en_path)
    print(f'Base inglés: {len(en_keys)} claves')

    for lang_code, gt_code in LANG_MAP.items():
        resx_path = os.path.join(BASE, f'Strings.{lang_code}.resx')
        if not os.path.exists(resx_path):
            print(f'[SKIP] {lang_code}: archivo no existe')
            continue

        existing = load_resx_keys(resx_path)
        missing = [k for k in en_keys if k not in existing]

        if not missing:
            print(f'[OK]   {lang_code}: sin claves faltantes')
            continue

        print(f'\n[{lang_code}] {len(missing)} claves faltantes — traduciendo...')
        added = 0
        errors = 0

        for key in missing:
            en_value = get_english_value(key, en_keys)
            if not en_value:
                continue

            if key in SKIP_TRANSLATE:
                value = en_value  # mantener en inglés
            else:
                value = translate_text(en_value, gt_code)
                time.sleep(0.15)  # respetar rate limit de Google Translate

            try:
                append_resx_entry(resx_path, key, value)
                added += 1
                print(f'  + {key}: {value[:60]}{"..." if len(value) > 60 else ""}')
            except Exception as e:
                print(f'  ERROR insertando {key}: {e}')
                errors += 1

        print(f'  >> {added} añadidas, {errors} errores')

    print('\nListo.')


if __name__ == '__main__':
    main()
