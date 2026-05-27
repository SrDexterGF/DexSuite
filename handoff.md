# DexSuite — Handoff Document

> Generado: 2026-05-27

---

## Objetivo

Construir **DexSuite**, una app WPF/.NET 8 de optimización extrema de rendimiento para Windows.  
No es un limpiador — es un sistema de optimización orientado a FPS máximos y latencia mínima.  
Monetización por tiers: **Free / Avanzado / Pro**.  
El usuario es principiante absoluto: la IA escribe todo el código, él solo prueba y reporta.

Stack: `.NET 8 + WPF + Wpf.Ui (Lepoco) 4.3.0 + CommunityToolkit.Mvvm + EF Core SQLite + Serilog + Velopack`  
GitHub: `https://github.com/SrDexterGF/DexSuite`

---

## Estado actual

La app **compila y funciona** (0 errores, 0 warnings en el último build).  
Se han implementado bloques de features 0–4 de un total de 9 bloques que el usuario tiene preparados.  
Los bloques 5–9 **no han sido compartidos todavía**.

### Features implementadas (resumen)

| Feature | Estado |
|---|---|
| MVVM completo + DI (Host + Services) | ✅ |
| Sidebar con navegación, acento cian, glow | ✅ |
| Vista Home con 6 cards (2 filas × 3) | ✅ |
| Vista Módulos con búsqueda incremental | ✅ |
| Barra de herramientas en Módulos (QuickClean, Recomendado, SelectAll, DeselectAll) | ✅ |
| Vista Log interno (SQLite, visible en la app, no .txt) | ✅ |
| Vista Especificaciones del sistema | ✅ |
| Vista Revertir cambios (ChangeTrackingService + tabla de pendientes) | ✅ |
| Vista Settings (tema, idioma, auto-start, etc.) | ✅ |
| Vista Updates (Velopack) | ✅ |
| Vista About (bug report vía mailto) | ✅ |
| i18n: 393 claves × 30 idiomas (es, gl, ca, eu, en, pt, fr, de, it, zh + más) | ✅ |
| Banderas PNG (flagcdn.com + Wikipedia Commons para gl/ca/eu) | ✅ |
| Tiers Free/Avanzado/Pro con candado visual | ✅ |
| Badge PRO en sidebar footer | ✅ |
| Score de rendimiento estabilizado con mediana + baseline persistido | ✅ |
| QuickCleanService (papelera, temps, prefetch, etc.) | ✅ |
| WingetService (upgrade --all) | ✅ |
| SecurityCheckService (Defender Quick, SFC, DISM, MRT) | ✅ |
| ChangeTrackingService + ModuleChangeRecord (SQLite) | ✅ |
| BugReportService (mailto con logs + specs) | ✅ |
| Punto de restauración de Windows antes de optimizar | ✅ |
| Juegos: todas las variantes separadas en cards individuales (sin ComboBox) | ✅ |
| GameOptimizationService con perfiles por juego | ✅ |

---

## Archivos activos (modificados o creados recientemente)

```
src/DexSuite.App/
├── MainWindow.xaml                         ← ÚLTIMO ARCHIVO EDITADO (vista principal, todas las vistas)
├── MainWindow.xaml.cs                      ← OnQuestionMarkPreventClick handler
├── App.xaml.cs                             ← DI registration + DB init
├── ViewModels/MainViewModel.cs             ← ViewModel principal (todo el estado)
├── Services/
│   ├── ILocalizationService.cs             ← LanguageOption con FlagImageUrl (PNG flags)
│   ├── LocalizationService.cs
│   ├── GameOptimizationService.cs          ← Perfiles de juego separados
│   ├── WingetService.cs                    ← Nuevo
│   ├── SecurityCheckService.cs             ← Nuevo
│   ├── ChangeTrackingService.cs            ← Nuevo
│   ├── BugReportService.cs                 ← Nuevo
│   └── [otros servicios existentes]
├── Models/
│   └── ModuleChangeRecord.cs               ← Entidad EF para reversión
├── Data/DexSuiteDbContext.cs               ← + DbSet<ModuleChangeRecord>
├── Converters/SecurityKindConverter.cs     ← SecurityKindConverter + ChangeTypeConverter
└── Markup/TExtension.cs                    ← Markup extension {loc:T}

scripts/
└── translations.json                       ← 393 claves × 30 idiomas (GRANDE)
```

---

## Qué ha cambiado esta sesión

### Fixes visuales en Home view y barra de Módulos

**Problema reportado por el usuario** (con screenshots):
- Las 3 cards de la fila 2 (QuickClean, WingetUpgrade, SecurityCheck) eran más altas que las de la fila 1
- Los botones en la fila 2 aparecían descentrados o cortados
- El `(?)` de QuickClean y Recomendado estaba FUERA del botón, no dentro
- El tooltip del `(?)` se mostraba al pasar el ratón por TODO el botón, no solo por el `(?)`
- Clicar el `(?)` activaba el comando del botón padre (no debería)

**Solución aplicada:**

1. **Todas las cards (filas 1 y 2)** — reemplazado `StackPanel` por `Grid` con 4 filas:
   ```
   Row 0: Auto → Icono
   Row 1: Auto → Título
   Row 2: *    → Descripción (se expande, empuja el botón al fondo)
   Row 3: Auto → Botón (siempre en la parte baja de la card)
   ```

2. **Card SecurityCheck** — ComboBox + Botón ahora apilados **verticalmente** (antes horizontal → overflow y botón cortado)

3. **Card QuickClean (home) + botón QuickClean (toolbar de Módulos)** — `(?)` movido DENTRO del contenido del botón usando `StackPanel Orientation="Horizontal"` con `PreviewMouseLeftButtonDown="OnQuestionMarkPreventClick"` en la burbuja

4. **Botón Recomendado (toolbar de Módulos)** — eliminado `ToolTipService.*` y `<ui:Button.ToolTip>` del botón padre; tooltip movido al `Border` del `(?)` interior; añadido `Cursor="Help"` y `PreviewMouseLeftButtonDown`

**Resultado del build:** ✅ `0 Advertencia(s) / 0 Errores`

---

## Qué ha fallado

- **Primer intento Edit en Recomendado**: `old_string not found` porque el Grep inicial mostraba `<\ui:Button.ToolTip>` (con barra invertida) como typo, pero el archivo real ya tenía `</ui:Button.ToolTip>` correctamente. Se resolvió releyendo el bloque exacto antes del Edit.

- **Banderas de idioma**: las emoji de bandera no renderizan en color en Windows. Solucionado usando imágenes PNG de `flagcdn.com` para idiomas estándar y URLs de Wikipedia Commons para gl/ca/eu (que no tienen código ISO-3166 propio).

---

## Qué planear después

**Inmediato — el usuario tiene los bloques 5–9 sin compartir.**  
La siguiente acción es **pedir al usuario que comparta el Bloque 5** y evaluar modelo/esfuerzo antes de ejecutar.

Antes de empezar cada bloque:
1. Pedir el bloque completo
2. Recomendar `[Modelo] · esfuerzo [nivel]` según complejidad (regla CLAUDE.md)
3. Esperar confirmación antes de escribir código

Posibles temas pendientes según el contexto original del proyecto:
- Más módulos nativos C# (migración del .bat)
- Mejoras visuales adicionales (sliders, menús más impactantes)
- Sistema de monetización real (activación de tiers)
- Velopack update flow completo y probado
- Más ajustes de tier: qué va en Free vs Avanzado vs Pro

---

## Reglas permanentes del proyecto

- Respuestas **siempre en español**; código en inglés
- Siempre recomendar modelo/esfuerzo antes de ejecutar (formato exacto del CLAUDE.md)
- No ejecutar hasta confirmación del usuario
- Código 100% escrito por la IA; el usuario solo prueba
- Todos los textos nuevos deben añadirse a `translations.json` (393 claves actuales)
- Nuevas opciones deben incluir: idiomas, ortografía, botón `(?)`, descripción de impacto, tier correcto
- Usar `LocalizationService.Instance.Get(key)` para textos en código C#
- Usar `{loc:T Key=...}` en XAML
- Singleton services, MVVM, `[RelayCommand]`, `[ObservableProperty]`
