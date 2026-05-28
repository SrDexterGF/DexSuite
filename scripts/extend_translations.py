# -*- coding: utf-8 -*-
"""
extend_translations.py
======================
Inyecta traducciones de los 20 idiomas nuevos en translations.json
para las claves de UI más visibles. El resto queda con fallback a
inglés (el resx generator lo gestiona).

Ejecutar:
    python scripts/extend_translations.py
Luego:
    pwsh scripts/generate-resx.ps1
"""
import json
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TR_PATH = os.path.join(SCRIPT_DIR, "translations.json")

# Idiomas nuevos (en el mismo orden que LocalizationService)
NEW_LANGS = [
    "ru", "uk", "ar", "ja", "ko", "hi", "bn", "ur", "id", "tr", "vi",
    "nl", "sv", "ro", "pl", "cs", "el", "da", "no", "fi",
]

# Traducciones por clave. Estructura: { key: { lang_code: translation } }
# Solo claves de alta visibilidad. El resto queda con fallback a inglés.
TRANSLATIONS = {

    # ===== App / Tagline =====
    "App.Tagline": {
        "ru": "ПРЕДЕЛЫ ВОЗМОЖНОГО", "uk": "ПОЗА МЕЖАМИ", "ar": "إلى الحد الأقصى",
        "ja": "限界を超えろ", "ko": "한계를 넘어라", "hi": "सीमाओं को पार करें",
        "bn": "সীমা ছাড়িয়ে", "ur": "حدود سے پار", "id": "LAMPAUI BATAS",
        "tr": "SINIRLARI ZORLA", "vi": "VƯỢT GIỚI HẠN", "nl": "VOORBIJ DE GRENS",
        "sv": "PRESSA GRÄNSEN", "ro": "DINCOLO DE LIMITĂ", "pl": "PRZEKRACZAJ GRANICE",
        "cs": "ZA HRANICE", "el": "ΣΤΟ ΟΡΙΟ", "da": "OVER GRÆNSEN",
        "no": "OVER GRENSEN", "fi": "RAJAN YLI",
    },
    "App.Subtitle": {
        "ru": "Очистка, оптимизация и безопасность для Windows",
        "uk": "Очищення, оптимізація та безпека для Windows",
        "ar": "تنظيف وتحسين وأمان لنظام Windows",
        "ja": "Windows のクリーンアップ、最適化、セキュリティ",
        "ko": "Windows를 위한 정리, 최적화, 보안",
        "hi": "Windows के लिए सफाई, अनुकूलन और सुरक्षा",
        "bn": "Windows-এর জন্য পরিষ্কার, অপ্টিমাইজেশন ও নিরাপত্তা",
        "ur": "Windows کیلئے صفائی، اصلاح اور سکیورٹی",
        "id": "Pembersihan, optimasi dan keamanan untuk Windows",
        "tr": "Windows için temizleme, optimizasyon ve güvenlik",
        "vi": "Dọn dẹp, tối ưu và bảo mật cho Windows",
        "nl": "Opschoning, optimalisatie en beveiliging voor Windows",
        "sv": "Rensning, optimering och säkerhet för Windows",
        "ro": "Curățare, optimizare și securitate pentru Windows",
        "pl": "Czyszczenie, optymalizacja i bezpieczeństwo dla Windows",
        "cs": "Vyčištění, optimalizace a zabezpečení pro Windows",
        "el": "Καθαρισμός, βελτιστοποίηση και ασφάλεια για Windows",
        "da": "Oprydning, optimering og sikkerhed til Windows",
        "no": "Opprydding, optimalisering og sikkerhet for Windows",
        "fi": "Siivous, optimointi ja tietoturva Windowsille",
    },

    # ===== Common =====
    "Common.Cancel": {
        "ru": "Отмена", "uk": "Скасувати", "ar": "إلغاء", "ja": "キャンセル",
        "ko": "취소", "hi": "रद्द करें", "bn": "বাতিল", "ur": "منسوخ",
        "id": "Batal", "tr": "İptal", "vi": "Hủy", "nl": "Annuleren",
        "sv": "Avbryt", "ro": "Anulează", "pl": "Anuluj", "cs": "Zrušit",
        "el": "Ακύρωση", "da": "Annuller", "no": "Avbryt", "fi": "Peruuta",
    },
    "Common.Apply": {
        "ru": "Применить", "uk": "Застосувати", "ar": "تطبيق", "ja": "適用",
        "ko": "적용", "hi": "लागू करें", "bn": "প্রয়োগ", "ur": "لاگو کریں",
        "id": "Terapkan", "tr": "Uygula", "vi": "Áp dụng", "nl": "Toepassen",
        "sv": "Tillämpa", "ro": "Aplică", "pl": "Zastosuj", "cs": "Použít",
        "el": "Εφαρμογή", "da": "Anvend", "no": "Bruk", "fi": "Käytä",
    },
    "Common.Close": {
        "ru": "Закрыть", "uk": "Закрити", "ar": "إغلاق", "ja": "閉じる",
        "ko": "닫기", "hi": "बंद करें", "bn": "বন্ধ", "ur": "بند کریں",
        "id": "Tutup", "tr": "Kapat", "vi": "Đóng", "nl": "Sluiten",
        "sv": "Stäng", "ro": "Închide", "pl": "Zamknij", "cs": "Zavřít",
        "el": "Κλείσιμο", "da": "Luk", "no": "Lukk", "fi": "Sulje",
    },
    "Common.OK": {
        "ru": "ОК", "uk": "ОК", "ar": "موافق", "ja": "OK", "ko": "확인",
        "hi": "ठीक है", "bn": "ঠিক আছে", "ur": "ٹھیک ہے", "id": "OK",
        "tr": "Tamam", "vi": "OK", "nl": "OK", "sv": "OK", "ro": "OK",
        "pl": "OK", "cs": "OK", "el": "Εντάξει", "da": "OK", "no": "OK", "fi": "OK",
    },
    "Common.Run": {
        "ru": "Запустить", "uk": "Запустити", "ar": "تشغيل", "ja": "実行",
        "ko": "실행", "hi": "चलाएं", "bn": "চালান", "ur": "چلائیں",
        "id": "Jalankan", "tr": "Çalıştır", "vi": "Chạy", "nl": "Uitvoeren",
        "sv": "Kör", "ro": "Execută", "pl": "Uruchom", "cs": "Spustit",
        "el": "Εκτέλεση", "da": "Kør", "no": "Kjør", "fi": "Suorita",
    },
    "Common.Open": {
        "ru": "Открыть", "uk": "Відкрити", "ar": "فتح", "ja": "開く",
        "ko": "열기", "hi": "खोलें", "bn": "খুলুন", "ur": "کھولیں",
        "id": "Buka", "tr": "Aç", "vi": "Mở", "nl": "Openen",
        "sv": "Öppna", "ro": "Deschide", "pl": "Otwórz", "cs": "Otevřít",
        "el": "Άνοιγμα", "da": "Åbn", "no": "Åpne", "fi": "Avaa",
    },
    "Common.Reset": {
        "ru": "Сбросить", "uk": "Скинути", "ar": "إعادة تعيين", "ja": "リセット",
        "ko": "재설정", "hi": "रीसेट करें", "bn": "রিসেট", "ur": "ری سیٹ",
        "id": "Atur ulang", "tr": "Sıfırla", "vi": "Đặt lại", "nl": "Resetten",
        "sv": "Återställ", "ro": "Resetează", "pl": "Resetuj", "cs": "Obnovit",
        "el": "Επαναφορά", "da": "Nulstil", "no": "Tilbakestill", "fi": "Palauta",
    },
    "Common.Never": {
        "ru": "Никогда", "uk": "Ніколи", "ar": "أبدًا", "ja": "なし",
        "ko": "없음", "hi": "कभी नहीं", "bn": "কখনো না", "ur": "کبھی نہیں",
        "id": "Tidak pernah", "tr": "Hiç", "vi": "Chưa bao giờ", "nl": "Nooit",
        "sv": "Aldrig", "ro": "Niciodată", "pl": "Nigdy", "cs": "Nikdy",
        "el": "Ποτέ", "da": "Aldrig", "no": "Aldri", "fi": "Ei koskaan",
    },

    # ===== Navigation =====
    "Navigation.Panel": {
        "ru": "ПАНЕЛЬ", "uk": "ПАНЕЛЬ", "ar": "اللوحة", "ja": "パネル",
        "ko": "패널", "hi": "पैनल", "bn": "প্যানেল", "ur": "پینل",
        "id": "PANEL", "tr": "PANEL", "vi": "BẢNG", "nl": "PANEEL",
        "sv": "PANEL", "ro": "PANOU", "pl": "PANEL", "cs": "PANEL",
        "el": "ΠΑΝΕΛ", "da": "PANEL", "no": "PANEL", "fi": "PANEELI",
    },
    "Navigation.System": {
        "ru": "СИСТЕМА", "uk": "СИСТЕМА", "ar": "النظام", "ja": "システム",
        "ko": "시스템", "hi": "सिस्टम", "bn": "সিস্টেম", "ur": "سسٹم",
        "id": "SISTEM", "tr": "SİSTEM", "vi": "HỆ THỐNG", "nl": "SYSTEEM",
        "sv": "SYSTEM", "ro": "SISTEM", "pl": "SYSTEM", "cs": "SYSTÉM",
        "el": "ΣΥΣΤΗΜΑ", "da": "SYSTEM", "no": "SYSTEM", "fi": "JÄRJESTELMÄ",
    },
    "Navigation.Home": {
        "ru": "Главная", "uk": "Головна", "ar": "الرئيسية", "ja": "ホーム",
        "ko": "홈", "hi": "होम", "bn": "হোম", "ur": "ہوم",
        "id": "Beranda", "tr": "Ana sayfa", "vi": "Trang chính", "nl": "Start",
        "sv": "Hem", "ro": "Acasă", "pl": "Strona główna", "cs": "Domů",
        "el": "Αρχική", "da": "Hjem", "no": "Hjem", "fi": "Etusivu",
    },
    "Navigation.Modules": {
        "ru": "Модули", "uk": "Модулі", "ar": "الوحدات", "ja": "モジュール",
        "ko": "모듈", "hi": "मॉड्यूल", "bn": "মডিউল", "ur": "ماڈیولز",
        "id": "Modul", "tr": "Modüller", "vi": "Mô-đun", "nl": "Modules",
        "sv": "Moduler", "ro": "Module", "pl": "Moduły", "cs": "Moduly",
        "el": "Ενότητες", "da": "Moduler", "no": "Moduler", "fi": "Moduulit",
    },
    "Navigation.Log": {
        "ru": "Журнал", "uk": "Журнал", "ar": "السجل", "ja": "ログ",
        "ko": "로그", "hi": "लॉग", "bn": "লগ", "ur": "لاگ",
        "id": "Log", "tr": "Günlük", "vi": "Nhật ký", "nl": "Logboek",
        "sv": "Logg", "ro": "Jurnal", "pl": "Dziennik", "cs": "Protokol",
        "el": "Καταγραφή", "da": "Log", "no": "Logg", "fi": "Loki",
    },
    "Navigation.Settings": {
        "ru": "Настройки", "uk": "Налаштування", "ar": "الإعدادات", "ja": "設定",
        "ko": "설정", "hi": "सेटिंग्स", "bn": "সেটিংস", "ur": "ترتیبات",
        "id": "Pengaturan", "tr": "Ayarlar", "vi": "Cài đặt", "nl": "Instellingen",
        "sv": "Inställningar", "ro": "Setări", "pl": "Ustawienia", "cs": "Nastavení",
        "el": "Ρυθμίσεις", "da": "Indstillinger", "no": "Innstillinger", "fi": "Asetukset",
    },
    "Navigation.Updates": {
        "ru": "Обновления", "uk": "Оновлення", "ar": "التحديثات", "ja": "アップデート",
        "ko": "업데이트", "hi": "अपडेट", "bn": "আপডেট", "ur": "اپ ڈیٹس",
        "id": "Pembaruan", "tr": "Güncellemeler", "vi": "Cập nhật", "nl": "Updates",
        "sv": "Uppdateringar", "ro": "Actualizări", "pl": "Aktualizacje", "cs": "Aktualizace",
        "el": "Ενημερώσεις", "da": "Opdateringer", "no": "Oppdateringer", "fi": "Päivitykset",
    },
    "Navigation.About": {
        "ru": "О приложении", "uk": "Про застосунок", "ar": "حول",
        "ja": "情報", "ko": "정보", "hi": "के बारे में", "bn": "সম্পর্কে",
        "ur": "بارے میں", "id": "Tentang", "tr": "Hakkında", "vi": "Giới thiệu",
        "nl": "Over", "sv": "Om", "ro": "Despre", "pl": "Informacje",
        "cs": "O aplikaci", "el": "Σχετικά", "da": "Om", "no": "Om", "fi": "Tietoja",
    },
    "Navigation.Specs": {
        "ru": "Характеристики", "uk": "Характеристики", "ar": "المواصفات",
        "ja": "システム情報", "ko": "사양", "hi": "विवरण", "bn": "স্পেক্স",
        "ur": "تفصیلات", "id": "Spesifikasi", "tr": "Özellikler", "vi": "Thông số",
        "nl": "Specs", "sv": "Specifikationer", "ro": "Specificații",
        "pl": "Specyfikacja", "cs": "Specifikace", "el": "Προδιαγραφές",
        "da": "Specifikationer", "no": "Spesifikasjoner", "fi": "Tiedot",
    },

    # ===== Settings core =====
    "Settings.Title": {
        "ru": "Настройки", "uk": "Налаштування", "ar": "الإعدادات", "ja": "設定",
        "ko": "설정", "hi": "सेटिंग्स", "bn": "সেটিংস", "ur": "ترتیبات",
        "id": "Pengaturan", "tr": "Ayarlar", "vi": "Cài đặt", "nl": "Instellingen",
        "sv": "Inställningar", "ro": "Setări", "pl": "Ustawienia", "cs": "Nastavení",
        "el": "Ρυθμίσεις", "da": "Indstillinger", "no": "Innstillinger", "fi": "Asetukset",
    },
    "Settings.Language.Title": {
        "ru": "Язык", "uk": "Мова", "ar": "اللغة", "ja": "言語",
        "ko": "언어", "hi": "भाषा", "bn": "ভাষা", "ur": "زبان",
        "id": "Bahasa", "tr": "Dil", "vi": "Ngôn ngữ", "nl": "Taal",
        "sv": "Språk", "ro": "Limbă", "pl": "Język", "cs": "Jazyk",
        "el": "Γλώσσα", "da": "Sprog", "no": "Språk", "fi": "Kieli",
    },
    "Settings.Tier.Title": {
        "ru": "Активный план", "uk": "Активний план", "ar": "الخطة النشطة",
        "ja": "アクティブプラン", "ko": "활성 플랜", "hi": "सक्रिय योजना",
        "bn": "সক্রিয় প্ল্যান", "ur": "فعال پلان", "id": "Paket aktif",
        "tr": "Aktif plan", "vi": "Gói đang dùng", "nl": "Actief plan",
        "sv": "Aktiv plan", "ro": "Plan activ", "pl": "Aktywny plan",
        "cs": "Aktivní plán", "el": "Ενεργό πλάνο", "da": "Aktiv plan",
        "no": "Aktiv plan", "fi": "Aktiivinen taso",
    },
    "Settings.Theme.Title": {
        "ru": "Тема", "uk": "Тема", "ar": "السمة", "ja": "テーマ",
        "ko": "테마", "hi": "थीम", "bn": "থিম", "ur": "تھیم",
        "id": "Tema", "tr": "Tema", "vi": "Chủ đề", "nl": "Thema",
        "sv": "Tema", "ro": "Temă", "pl": "Motyw", "cs": "Motiv",
        "el": "Θέμα", "da": "Tema", "no": "Tema", "fi": "Teema",
    },
    "Settings.Reset": {
        "ru": "Сбросить настройки", "uk": "Скинути налаштування",
        "ar": "إعادة تعيين الإعدادات", "ja": "設定をリセット",
        "ko": "설정 재설정", "hi": "सेटिंग्स रीसेट करें",
        "bn": "সেটিংস রিসেট", "ur": "ترتیبات ری سیٹ", "id": "Atur ulang pengaturan",
        "tr": "Ayarları sıfırla", "vi": "Đặt lại cài đặt",
        "nl": "Instellingen resetten", "sv": "Återställ inställningar",
        "ro": "Resetează setările", "pl": "Resetuj ustawienia",
        "cs": "Obnovit nastavení", "el": "Επαναφορά ρυθμίσεων",
        "da": "Nulstil indstillinger", "no": "Tilbakestill innstillinger",
        "fi": "Palauta asetukset",
    },

    # ===== Modules core =====
    "Modules.Title": {
        "ru": "Доступные модули", "uk": "Доступні модулі", "ar": "الوحدات المتاحة",
        "ja": "利用可能なモジュール", "ko": "사용 가능한 모듈", "hi": "उपलब्ध मॉड्यूल",
        "bn": "উপলব্ধ মডিউল", "ur": "دستیاب ماڈیولز", "id": "Modul tersedia",
        "tr": "Mevcut modüller", "vi": "Mô-đun có sẵn", "nl": "Beschikbare modules",
        "sv": "Tillgängliga moduler", "ro": "Module disponibile",
        "pl": "Dostępne moduły", "cs": "Dostupné moduly",
        "el": "Διαθέσιμες ενότητες", "da": "Tilgængelige moduler",
        "no": "Tilgjengelige moduler", "fi": "Saatavilla olevat moduulit",
    },
    "Modules.SelectAll": {
        "ru": "Выбрать все", "uk": "Вибрати все", "ar": "تحديد الكل",
        "ja": "すべて選択", "ko": "모두 선택", "hi": "सभी चुनें",
        "bn": "সব নির্বাচন করুন", "ur": "سب کا انتخاب", "id": "Pilih semua",
        "tr": "Tümünü seç", "vi": "Chọn tất cả", "nl": "Alles selecteren",
        "sv": "Markera alla", "ro": "Selectează tot", "pl": "Zaznacz wszystko",
        "cs": "Vybrat vše", "el": "Επιλογή όλων", "da": "Vælg alle",
        "no": "Velg alle", "fi": "Valitse kaikki",
    },
    "Modules.DeselectAll": {
        "ru": "Снять выделение", "uk": "Зняти виділення", "ar": "إلغاء التحديد",
        "ja": "選択解除", "ko": "선택 해제", "hi": "अचयन करें",
        "bn": "নির্বাচন বাতিল", "ur": "انتخاب ختم", "id": "Batalkan pilihan",
        "tr": "Seçimi kaldır", "vi": "Bỏ chọn", "nl": "Deselecteer",
        "sv": "Avmarkera", "ro": "Deselectează", "pl": "Odznacz wszystko",
        "cs": "Zrušit výběr", "el": "Αποεπιλογή", "da": "Fravælg",
        "no": "Fjern valg", "fi": "Poista valinta",
    },
    "Modules.SelectRecommended": {
        "ru": "Рекомендуемое", "uk": "Рекомендовано", "ar": "موصى به",
        "ja": "推奨", "ko": "권장", "hi": "अनुशंसित",
        "bn": "সুপারিশকৃত", "ur": "تجویز کردہ", "id": "Direkomendasikan",
        "tr": "Önerilen", "vi": "Khuyên dùng", "nl": "Aanbevolen",
        "sv": "Rekommenderat", "ro": "Recomandat", "pl": "Zalecane",
        "cs": "Doporučeno", "el": "Συνιστάται", "da": "Anbefalet",
        "no": "Anbefalt", "fi": "Suositeltu",
    },
    "Modules.Safety.Safe": {
        "ru": "Безопасно", "uk": "Безпечно", "ar": "آمن", "ja": "安全",
        "ko": "안전", "hi": "सुरक्षित", "bn": "নিরাপদ", "ur": "محفوظ",
        "id": "Aman", "tr": "Güvenli", "vi": "An toàn", "nl": "Veilig",
        "sv": "Säkert", "ro": "Sigur", "pl": "Bezpieczne", "cs": "Bezpečné",
        "el": "Ασφαλές", "da": "Sikker", "no": "Trygt", "fi": "Turvallinen",
    },
    "Modules.Safety.Risk": {
        "ru": "Риск", "uk": "Ризик", "ar": "مخاطرة", "ja": "リスク",
        "ko": "위험", "hi": "जोखिम", "bn": "ঝুঁকি", "ur": "خطرہ",
        "id": "Risiko", "tr": "Risk", "vi": "Rủi ro", "nl": "Risico",
        "sv": "Risk", "ro": "Risc", "pl": "Ryzyko", "cs": "Riziko",
        "el": "Ρίσκο", "da": "Risiko", "no": "Risiko", "fi": "Riski",
    },
    "Modules.Impact.Soft": {
        "ru": "Слабый", "uk": "Слабкий", "ar": "خفيف", "ja": "弱",
        "ko": "약함", "hi": "हल्का", "bn": "হালকা", "ur": "ہلکا",
        "id": "Ringan", "tr": "Hafif", "vi": "Nhẹ", "nl": "Licht",
        "sv": "Lätt", "ro": "Ușor", "pl": "Lekki", "cs": "Lehký",
        "el": "Ήπιο", "da": "Let", "no": "Lett", "fi": "Lievä",
    },
    "Modules.Impact.Notable": {
        "ru": "Заметный", "uk": "Помітний", "ar": "ملحوظ", "ja": "中",
        "ko": "주목", "hi": "उल्लेखनीय", "bn": "উল্লেখযোগ্য", "ur": "نمایاں",
        "id": "Cukup", "tr": "Belirgin", "vi": "Đáng kể", "nl": "Merkbaar",
        "sv": "Märkbart", "ro": "Notabil", "pl": "Zauważalny", "cs": "Patrný",
        "el": "Σημαντικό", "da": "Mærkbart", "no": "Merkbart", "fi": "Huomattava",
    },
    "Modules.Impact.Strong": {
        "ru": "Сильный", "uk": "Сильний", "ar": "قوي", "ja": "強",
        "ko": "강함", "hi": "तीव्र", "bn": "তীব্র", "ur": "مضبوط",
        "id": "Kuat", "tr": "Güçlü", "vi": "Mạnh", "nl": "Sterk",
        "sv": "Starkt", "ro": "Puternic", "pl": "Silny", "cs": "Silný",
        "el": "Έντονο", "da": "Stærk", "no": "Sterk", "fi": "Vahva",
    },
    "Modules.Impact.Extreme": {
        "ru": "ЭКСТРИМ", "uk": "ЕКСТРИМ", "ar": "متطرف", "ja": "極限",
        "ko": "극한", "hi": "अत्यधिक", "bn": "চরম", "ur": "انتہائی",
        "id": "EKSTREM", "tr": "EKSTREM", "vi": "CỰC ĐỈNH", "nl": "EXTREEM",
        "sv": "EXTREMT", "ro": "EXTREM", "pl": "EKSTREMALNY", "cs": "EXTRÉMNÍ",
        "el": "ΑΚΡΑΙΟ", "da": "EKSTREM", "no": "EKSTREM", "fi": "ÄÄRIMMÄINEN",
    },
    "Modules.Tier.Advanced": {
        "ru": "ПРОДВИНУТЫЙ", "uk": "ПРОСУНУТИЙ", "ar": "متقدم", "ja": "アドバンス",
        "ko": "고급", "hi": "उन्नत", "bn": "উন্নত", "ur": "ایڈوانس",
        "id": "LANJUTAN", "tr": "GELİŞMİŞ", "vi": "NÂNG CAO", "nl": "GEAVANCEERD",
        "sv": "AVANCERAD", "ro": "AVANSAT", "pl": "ZAAWANSOWANY", "cs": "POKROČILÝ",
        "el": "ΠΡΟΧΩΡΗΜΕΝΟ", "da": "AVANCERET", "no": "AVANSERT", "fi": "EDISTYNYT",
    },

    # ===== Home =====
    "Home.WhatDoYouWantTo": {
        "ru": "Что вы хотите сделать?", "uk": "Що ви хочете зробити?",
        "ar": "ماذا تريد أن تفعل؟", "ja": "何をしたいですか？",
        "ko": "무엇을 하시겠습니까?", "hi": "आप क्या करना चाहते हैं?",
        "bn": "আপনি কী করতে চান?", "ur": "آپ کیا کرنا چاہتے ہیں؟",
        "id": "Apa yang ingin Anda lakukan?", "tr": "Ne yapmak istersiniz?",
        "vi": "Bạn muốn làm gì?", "nl": "Wat wil je doen?",
        "sv": "Vad vill du göra?", "ro": "Ce vrei să faci?",
        "pl": "Co chcesz zrobić?", "cs": "Co chcete udělat?",
        "el": "Τι θέλεις να κάνεις;", "da": "Hvad vil du gøre?",
        "no": "Hva vil du gjøre?", "fi": "Mitä haluat tehdä?",
    },
    "Home.Card.Analyze.Title": {
        "ru": "Анализ производительности", "uk": "Аналіз продуктивності",
        "ar": "تحليل الأداء", "ja": "パフォーマンスを分析",
        "ko": "성능 분석", "hi": "प्रदर्शन का विश्लेषण करें",
        "bn": "পারফরম্যান্স বিশ্লেষণ", "ur": "کارکردگی کا تجزیہ",
        "id": "Analisis kinerja", "tr": "Performansı analiz et",
        "vi": "Phân tích hiệu suất", "nl": "Prestaties analyseren",
        "sv": "Analysera prestanda", "ro": "Analizează performanța",
        "pl": "Analizuj wydajność", "cs": "Analyzovat výkon",
        "el": "Ανάλυση απόδοσης", "da": "Analysér ydeevne",
        "no": "Analyser ytelse", "fi": "Analysoi suorituskyky",
    },
    "Home.Card.Analyze.Button": {
        "ru": "Анализ", "uk": "Аналіз", "ar": "تحليل", "ja": "分析",
        "ko": "분석", "hi": "विश्लेषण", "bn": "বিশ্লেষণ", "ur": "تجزیہ",
        "id": "Analisis", "tr": "Analiz", "vi": "Phân tích", "nl": "Analyseren",
        "sv": "Analysera", "ro": "Analizează", "pl": "Analizuj", "cs": "Analyzovat",
        "el": "Ανάλυση", "da": "Analysér", "no": "Analyser", "fi": "Analysoi",
    },
    "Home.Card.Optimize.Title": {
        "ru": "Оптимизация системы", "uk": "Оптимізація системи",
        "ar": "تحسين النظام", "ja": "システムを最適化",
        "ko": "시스템 최적화", "hi": "सिस्टम अनुकूलित करें",
        "bn": "সিস্টেম অপ্টিমাইজ", "ur": "سسٹم اصلاح",
        "id": "Optimasi sistem", "tr": "Sistemi optimize et",
        "vi": "Tối ưu hệ thống", "nl": "Systeem optimaliseren",
        "sv": "Optimera systemet", "ro": "Optimizează sistemul",
        "pl": "Optymalizuj system", "cs": "Optimalizovat systém",
        "el": "Βελτιστοποίηση συστήματος", "da": "Optimer systemet",
        "no": "Optimaliser systemet", "fi": "Optimoi järjestelmä",
    },
    "Home.Card.Log.Title": {
        "ru": "Просмотр журнала", "uk": "Переглянути журнал",
        "ar": "عرض السجل", "ja": "ログを表示",
        "ko": "로그 보기", "hi": "लॉग देखें",
        "bn": "লগ দেখুন", "ur": "لاگ دیکھیں",
        "id": "Lihat log", "tr": "Günlüğü görüntüle",
        "vi": "Xem nhật ký", "nl": "Logboek bekijken",
        "sv": "Visa logg", "ro": "Vezi jurnalul",
        "pl": "Pokaż dziennik", "cs": "Zobrazit protokol",
        "el": "Προβολή καταγραφής", "da": "Vis log",
        "no": "Vis logg", "fi": "Näytä loki",
    },
    "Home.AnalyzeAgain": {
        "ru": "Анализировать снова", "uk": "Аналізувати знову",
        "ar": "تحليل مرة أخرى", "ja": "再度分析",
        "ko": "다시 분석", "hi": "फिर से विश्लेषण",
        "bn": "আবার বিশ্লেষণ", "ur": "دوبارہ تجزیہ",
        "id": "Analisis lagi", "tr": "Tekrar analiz et",
        "vi": "Phân tích lại", "nl": "Opnieuw analyseren",
        "sv": "Analysera igen", "ro": "Analizează din nou",
        "pl": "Analizuj ponownie", "cs": "Analyzovat znovu",
        "el": "Ανάλυση ξανά", "da": "Analysér igen",
        "no": "Analyser igjen", "fi": "Analysoi uudelleen",
    },
    "Home.ResetScores": {
        "ru": "Сбросить баллы", "uk": "Скинути бали",
        "ar": "إعادة تعيين النقاط", "ja": "スコアをリセット",
        "ko": "점수 재설정", "hi": "स्कोर रीसेट करें",
        "bn": "স্কোর রিসেট", "ur": "اسکور ری سیٹ",
        "id": "Atur ulang skor", "tr": "Skoru sıfırla",
        "vi": "Đặt lại điểm", "nl": "Scores resetten",
        "sv": "Återställ poäng", "ro": "Resetează scorurile",
        "pl": "Resetuj wyniki", "cs": "Resetovat skóre",
        "el": "Επαναφορά βαθμολογίας", "da": "Nulstil score",
        "no": "Tilbakestill poeng", "fi": "Palauta pisteet",
    },

    # ===== Verdict =====
    "Verdict.Excellent": {
        "ru": "Отлично", "uk": "Відмінно", "ar": "ممتاز", "ja": "優秀",
        "ko": "탁월함", "hi": "उत्कृष्ट", "bn": "চমৎকার", "ur": "بہترین",
        "id": "Sangat baik", "tr": "Mükemmel", "vi": "Xuất sắc",
        "nl": "Uitstekend", "sv": "Utmärkt", "ro": "Excelent",
        "pl": "Doskonały", "cs": "Vynikající", "el": "Άριστο",
        "da": "Fremragende", "no": "Utmerket", "fi": "Erinomainen",
    },
    "Verdict.Good": {
        "ru": "Хорошо", "uk": "Добре", "ar": "جيد", "ja": "良好",
        "ko": "양호", "hi": "अच्छा", "bn": "ভালো", "ur": "اچھا",
        "id": "Baik", "tr": "İyi", "vi": "Tốt", "nl": "Goed",
        "sv": "Bra", "ro": "Bun", "pl": "Dobry", "cs": "Dobré",
        "el": "Καλό", "da": "Godt", "no": "Bra", "fi": "Hyvä",
    },
    "Verdict.Acceptable": {
        "ru": "Приемлемо", "uk": "Прийнятно", "ar": "مقبول", "ja": "可",
        "ko": "양호", "hi": "स्वीकार्य", "bn": "গ্রহণযোগ্য", "ur": "قابل قبول",
        "id": "Diterima", "tr": "Kabul edilebilir", "vi": "Chấp nhận được",
        "nl": "Aanvaardbaar", "sv": "Godtagbart", "ro": "Acceptabil",
        "pl": "Akceptowalny", "cs": "Přijatelné", "el": "Αποδεκτό",
        "da": "Acceptabelt", "no": "Akseptabelt", "fi": "Hyväksyttävä",
    },
    "Verdict.Improvable": {
        "ru": "Можно улучшить", "uk": "Можна покращити", "ar": "قابل للتحسين",
        "ja": "改善可能", "ko": "개선 필요", "hi": "सुधार योग्य",
        "bn": "উন্নতিযোগ্য", "ur": "بہتری ممکن", "id": "Bisa diperbaiki",
        "tr": "İyileştirilebilir", "vi": "Có thể cải thiện",
        "nl": "Voor verbetering vatbaar", "sv": "Förbättringsbart",
        "ro": "Îmbunătățibil", "pl": "Do poprawy", "cs": "Zlepšit",
        "el": "Βελτιώσιμο", "da": "Kan forbedres", "no": "Kan forbedres",
        "fi": "Parannettavissa",
    },
    "Verdict.Critical": {
        "ru": "Критическое", "uk": "Критично", "ar": "حرج", "ja": "重大",
        "ko": "심각", "hi": "गंभीर", "bn": "গুরুতর", "ur": "نازک",
        "id": "Kritis", "tr": "Kritik", "vi": "Nguy cấp", "nl": "Kritiek",
        "sv": "Kritiskt", "ro": "Critic", "pl": "Krytyczny", "cs": "Kritické",
        "el": "Κρίσιμο", "da": "Kritisk", "no": "Kritisk", "fi": "Kriittinen",
    },

    # ===== Quick clean =====
    "QuickClean.Button": {
        "ru": "Быстрая очистка", "uk": "Швидке очищення",
        "ar": "تنظيف سريع", "ja": "クイッククリーン",
        "ko": "빠른 정리", "hi": "त्वरित सफाई",
        "bn": "দ্রুত পরিষ্কার", "ur": "فوری صفائی",
        "id": "Bersihkan cepat", "tr": "Hızlı temizle",
        "vi": "Dọn nhanh", "nl": "Snel opschonen",
        "sv": "Snabbrensning", "ro": "Curățare rapidă",
        "pl": "Szybkie czyszczenie", "cs": "Rychlé čištění",
        "el": "Γρήγορος καθαρισμός", "da": "Hurtig oprydning",
        "no": "Rask opprydding", "fi": "Pikasiivous",
    },

    # ===== Updates core =====
    "Updates.CheckForUpdates": {
        "ru": "Проверить обновления", "uk": "Перевірити оновлення",
        "ar": "البحث عن تحديثات", "ja": "更新を確認",
        "ko": "업데이트 확인", "hi": "अपडेट जांचें",
        "bn": "আপডেট পরীক্ষা", "ur": "اپ ڈیٹ چیک کریں",
        "id": "Periksa pembaruan", "tr": "Güncellemeleri kontrol et",
        "vi": "Kiểm tra cập nhật", "nl": "Updates zoeken",
        "sv": "Sök efter uppdateringar", "ro": "Verifică actualizările",
        "pl": "Sprawdź aktualizacje", "cs": "Zkontrolovat aktualizace",
        "el": "Έλεγχος ενημερώσεων", "da": "Søg efter opdateringer",
        "no": "Se etter oppdateringer", "fi": "Tarkista päivitykset",
    },
    "Updates.UpdateNow": {
        "ru": "Обновить сейчас", "uk": "Оновити зараз",
        "ar": "تحديث الآن", "ja": "今すぐ更新",
        "ko": "지금 업데이트", "hi": "अभी अपडेट करें",
        "bn": "এখনই আপডেট", "ur": "ابھی اپ ڈیٹ",
        "id": "Perbarui sekarang", "tr": "Şimdi güncelle",
        "vi": "Cập nhật ngay", "nl": "Nu bijwerken",
        "sv": "Uppdatera nu", "ro": "Actualizează acum",
        "pl": "Aktualizuj teraz", "cs": "Aktualizovat",
        "el": "Ενημέρωση τώρα", "da": "Opdater nu",
        "no": "Oppdater nå", "fi": "Päivitä nyt",
    },
    "Updates.NewVersionAvailable": {
        "ru": "Доступна новая версия!", "uk": "Доступна нова версія!",
        "ar": "إصدار جديد متاح!", "ja": "新しいバージョンが利用可能!",
        "ko": "새 버전이 사용 가능합니다!", "hi": "नया संस्करण उपलब्ध!",
        "bn": "নতুন সংস্করণ উপলব্ধ!", "ur": "نیا ورژن دستیاب!",
        "id": "Versi baru tersedia!", "tr": "Yeni sürüm mevcut!",
        "vi": "Có phiên bản mới!", "nl": "Nieuwe versie beschikbaar!",
        "sv": "Ny version tillgänglig!", "ro": "Versiune nouă disponibilă!",
        "pl": "Dostępna nowa wersja!", "cs": "K dispozici je nová verze!",
        "el": "Διαθέσιμη νέα έκδοση!", "da": "Ny version tilgængelig!",
        "no": "Ny versjon tilgjengelig!", "fi": "Uusi versio saatavilla!",
    },

    # ===== About =====
    "About.Project.Title": {
        "ru": "О проекте", "uk": "Про проект", "ar": "المشروع",
        "ja": "プロジェクト", "ko": "프로젝트", "hi": "परियोजना",
        "bn": "প্রকল্প", "ur": "پروجیکٹ", "id": "Proyek",
        "tr": "Proje", "vi": "Dự án", "nl": "Het project",
        "sv": "Projektet", "ro": "Proiectul", "pl": "Projekt",
        "cs": "Projekt", "el": "Το έργο", "da": "Projektet",
        "no": "Prosjektet", "fi": "Projekti",
    },
    "About.Credits.Title": {
        "ru": "Авторы", "uk": "Автори", "ar": "اعتمادات",
        "ja": "クレジット", "ko": "크레딧", "hi": "श्रेय",
        "bn": "ক্রেডিট", "ur": "کریڈٹس", "id": "Kredit",
        "tr": "Katkıda bulunanlar", "vi": "Tác giả",
        "nl": "Credits", "sv": "Medverkande", "ro": "Mențiuni",
        "pl": "Twórcy", "cs": "Poděkování", "el": "Συντελεστές",
        "da": "Bidragsydere", "no": "Bidragsytere", "fi": "Tekijät",
    },

    # ===== Status core =====
    "Status.Ready": {
        "ru": "Готово. Выберите модули и нажмите Запустить.",
        "uk": "Готово. Виберіть модулі та натисніть Запустити.",
        "ar": "جاهز. اختر الوحدات واضغط تشغيل.",
        "ja": "準備完了。モジュールを選択して実行をクリック。",
        "ko": "준비 완료. 모듈을 선택하고 실행을 클릭하세요.",
        "hi": "तैयार। मॉड्यूल चुनें और चलाएं दबाएं।",
        "bn": "প্রস্তুত। মডিউল নির্বাচন করে চালান টিপুন।",
        "ur": "تیار۔ ماڈیولز چنیں اور چلائیں دبائیں۔",
        "id": "Siap. Pilih modul dan klik Jalankan.",
        "tr": "Hazır. Modülleri seçin ve Çalıştır'a tıklayın.",
        "vi": "Sẵn sàng. Chọn mô-đun và bấm Chạy.",
        "nl": "Klaar. Selecteer modules en klik Uitvoeren.",
        "sv": "Klart. Välj moduler och klicka Kör.",
        "ro": "Gata. Selectează module și apasă Execută.",
        "pl": "Gotowe. Wybierz moduły i kliknij Uruchom.",
        "cs": "Připraveno. Vyberte moduly a klikněte na Spustit.",
        "el": "Έτοιμο. Επιλέξτε ενότητες και πατήστε Εκτέλεση.",
        "da": "Klar. Vælg moduler og klik Kør.",
        "no": "Klar. Velg moduler og klikk Kjør.",
        "fi": "Valmis. Valitse moduulit ja paina Suorita.",
    },

    # ===== Search =====
    "Search.Placeholder": {
        "ru": "Поиск настройки…", "uk": "Пошук налаштування…",
        "ar": "البحث عن إعداد…", "ja": "設定を検索…",
        "ko": "설정 검색…", "hi": "सेटिंग खोजें…",
        "bn": "সেটিং খুঁজুন…", "ur": "ترتیب تلاش کریں…",
        "id": "Cari pengaturan…", "tr": "Ayar ara…",
        "vi": "Tìm cài đặt…", "nl": "Instelling zoeken…",
        "sv": "Sök inställning…", "ro": "Caută setare…",
        "pl": "Szukaj ustawienia…", "cs": "Hledat nastavení…",
        "el": "Αναζήτηση ρύθμισης…", "da": "Søg indstilling…",
        "no": "Søk innstilling…", "fi": "Hae asetusta…",
    },

    # ===== Log core =====
    "Log.Title": {
        "ru": "Журнал выполнения", "uk": "Журнал виконання",
        "ar": "سجل التنفيذ", "ja": "実行ログ",
        "ko": "실행 로그", "hi": "निष्पादन लॉग",
        "bn": "এক্সিকিউশন লগ", "ur": "ایگزیکیوشن لاگ",
        "id": "Log eksekusi", "tr": "Yürütme günlüğü",
        "vi": "Nhật ký thực thi", "nl": "Uitvoeringslog",
        "sv": "Körningslogg", "ro": "Jurnal de execuție",
        "pl": "Dziennik wykonania", "cs": "Protokol provedení",
        "el": "Καταγραφή εκτέλεσης", "da": "Udførselslog",
        "no": "Utførelseslogg", "fi": "Suorituksen loki",
    },
    "Log.Empty.Title": {
        "ru": "Пока нет событий", "uk": "Подій ще немає",
        "ar": "لا توجد أحداث بعد", "ja": "まだイベントがありません",
        "ko": "아직 이벤트가 없습니다", "hi": "अभी तक कोई घटना नहीं",
        "bn": "এখনো কোনো ইভেন্ট নেই", "ur": "ابھی تک کوئی ایونٹ نہیں",
        "id": "Belum ada peristiwa", "tr": "Henüz olay yok",
        "vi": "Chưa có sự kiện nào", "nl": "Nog geen gebeurtenissen",
        "sv": "Inga händelser än", "ro": "Niciun eveniment încă",
        "pl": "Brak zdarzeń", "cs": "Zatím žádné události",
        "el": "Καμία ενέργεια ακόμη", "da": "Ingen hændelser endnu",
        "no": "Ingen hendelser ennå", "fi": "Ei vielä tapahtumia",
    },

    # ===== Specs =====
    "Specs.Title": {
        "ru": "Характеристики системы", "uk": "Характеристики системи",
        "ar": "مواصفات النظام", "ja": "システム情報",
        "ko": "시스템 사양", "hi": "सिस्टम विवरण",
        "bn": "সিস্টেম স্পেসিফিকেশন", "ur": "سسٹم تفصیلات",
        "id": "Spesifikasi sistem", "tr": "Sistem özellikleri",
        "vi": "Thông số hệ thống", "nl": "Systeemspecificaties",
        "sv": "Systemspecifikationer", "ro": "Specificații sistem",
        "pl": "Specyfikacja systemu", "cs": "Specifikace systému",
        "el": "Προδιαγραφές συστήματος", "da": "Systemspecifikationer",
        "no": "Systemspesifikasjoner", "fi": "Järjestelmän tiedot",
    },
    "Specs.CPU": {
        "ru": "Процессор", "uk": "Процесор", "ar": "المعالج",
        "ja": "プロセッサ", "ko": "프로세서", "hi": "प्रोसेसर",
        "bn": "প্রসেসর", "ur": "پروسیسر", "id": "Prosesor",
        "tr": "İşlemci", "vi": "Bộ xử lý", "nl": "Processor",
        "sv": "Processor", "ro": "Procesor", "pl": "Procesor",
        "cs": "Procesor", "el": "Επεξεργαστής", "da": "Processor",
        "no": "Prosessor", "fi": "Suoritin",
    },
    "Specs.RAM": {
        "ru": "Память", "uk": "Пам'ять", "ar": "الذاكرة",
        "ja": "メモリ", "ko": "메모리", "hi": "मेमोरी",
        "bn": "মেমরি", "ur": "میموری", "id": "Memori",
        "tr": "Bellek", "vi": "Bộ nhớ", "nl": "Geheugen",
        "sv": "Minne", "ro": "Memorie", "pl": "Pamięć",
        "cs": "Paměť", "el": "Μνήμη", "da": "Hukommelse",
        "no": "Minne", "fi": "Muisti",
    },
    "Specs.GPU": {
        "ru": "Видеокарта", "uk": "Відеокарта", "ar": "بطاقة الرسوميات",
        "ja": "グラフィック", "ko": "그래픽", "hi": "ग्राफिक्स",
        "bn": "গ্রাফিক্স", "ur": "گرافکس", "id": "Grafis",
        "tr": "Ekran kartı", "vi": "Đồ hoạ", "nl": "Grafische kaart",
        "sv": "Grafikkort", "ro": "Placă grafică", "pl": "Karta graficzna",
        "cs": "Grafika", "el": "Κάρτα γραφικών", "da": "Grafik",
        "no": "Grafikk", "fi": "Näytönohjain",
    },
    "Specs.OS": {
        "ru": "Операционная система", "uk": "Операційна система",
        "ar": "نظام التشغيل", "ja": "オペレーティングシステム",
        "ko": "운영 체제", "hi": "ऑपरेटिंग सिस्टम",
        "bn": "অপারেটিং সিস্টেম", "ur": "آپریٹنگ سسٹم",
        "id": "Sistem operasi", "tr": "İşletim sistemi",
        "vi": "Hệ điều hành", "nl": "Besturingssysteem",
        "sv": "Operativsystem", "ro": "Sistem de operare",
        "pl": "System operacyjny", "cs": "Operační systém",
        "el": "Λειτουργικό σύστημα", "da": "Operativsystem",
        "no": "Operativsystem", "fi": "Käyttöjärjestelmä",
    },
    "Specs.Storage": {
        "ru": "Накопитель", "uk": "Сховище", "ar": "التخزين",
        "ja": "ストレージ", "ko": "저장소", "hi": "स्टोरेज",
        "bn": "স্টোরেজ", "ur": "اسٹوریج", "id": "Penyimpanan",
        "tr": "Depolama", "vi": "Lưu trữ", "nl": "Opslag",
        "sv": "Lagring", "ro": "Stocare", "pl": "Magazyn",
        "cs": "Úložiště", "el": "Αποθήκευση", "da": "Lager",
        "no": "Lagring", "fi": "Tallennustila",
    },
    "Specs.Refresh": {
        "ru": "Обновить", "uk": "Оновити", "ar": "تحديث",
        "ja": "更新", "ko": "새로 고침", "hi": "ताज़ा करें",
        "bn": "রিফ্রেশ", "ur": "تازہ کریں", "id": "Segarkan",
        "tr": "Yenile", "vi": "Làm mới", "nl": "Vernieuwen",
        "sv": "Uppdatera", "ro": "Reîmprospătează", "pl": "Odśwież",
        "cs": "Obnovit", "el": "Ανανέωση", "da": "Opdater",
        "no": "Oppdater", "fi": "Päivitä",
    },
}


def main():
    with open(TR_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    keys = data["keys"]

    added = 0
    missing_keys = []
    for key, translations in TRANSLATIONS.items():
        if key not in keys:
            missing_keys.append(key)
            continue
        entry = keys[key]
        for lang in NEW_LANGS:
            if lang in translations:
                entry[lang] = translations[lang]
                added += 1

    with open(TR_PATH, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"Added {added} translations across {len(TRANSLATIONS)} keys × {len(NEW_LANGS)} languages.")
    if missing_keys:
        print(f"WARNING: {len(missing_keys)} keys not found in translations.json:")
        for k in missing_keys:
            print(f"  - {k}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
