# PROJECT CONTEXT
> Generado el 2026-06-09. Versión actual publicada: **v0.2.34**
> Propósito: memoria técnica completa para reanudar el desarrollo desde cero.

## Cómo reanudar sesión

Si es una sesión nueva, di a Claude:
> *"Lee el CONTEXT.md y continúa desde donde lo dejamos."*

Claude confirmará: versión actual + último bloque trabajado + próximo paso. Sin re-explicar nada.

---

## 1. Descripción del proyecto

**DexSuite** es una aplicación de escritorio Windows (WPF, .NET 8, C#) orientada a la optimización del sistema operativo. Permite ejecutar módulos de limpieza, ajuste de configuración, análisis de rendimiento y optimización de videojuegos, con un sistema de planes de licencia (Free / Avanzado / Pro) que desbloquea módulos progresivamente.

- Distribuida como instalador Velopack con auto-update vía GitHub Releases.
- Requiere permisos de administrador (UAC al arrancar).
- Soporta 30 idiomas con traducciones en archivos `.resx` compilados.
- Tiene un sistema de temas visuales (15 temas, la mayoría bloqueados por plan).
- Herramienta interna (`DexSuite.KeyGen`) para gestión del sistema de licencias RSA.

**Repositorios relacionados:**
- App: `https://github.com/SrDexterGF/DexSuite` (privado, releases en GitHub)
- Scripts de juegos: `https://github.com/SrDexterGF/Game_Configs` (público, scripts .ps1)
- Web de verificación Trustpilot: `https://srdextergf.github.io/dexsuite-site/`

**RRSS:** Instagram `@DexSuite`, Linktree `https://linktr.ee/DexSuite`, email `suitedex@gmail.com`

---

## 2. Stack tecnológico

| Componente | Detalle |
|---|---|
| Framework | .NET 8 / WPF (`net8.0-windows10.0.19041.0`) |
| Patrón UI | MVVM con CommunityToolkit.Mvvm 8.4.2 |
| UI Library | WPF-UI (Wpf.Ui) 4.3.0 — Fluent Design Dark |
| DI / Hosting | Microsoft.Extensions.Hosting 8.x |
| Base de datos | SQLite via EF Core 8.x (`DexSuiteDbContext`) |
| Logging | Serilog (file rolling diario, 7 días) |
| Auto-update | Velopack 0.0.1298 — GitHub Releases como fuente |
| Bandeja | H.NotifyIcon.Wpf 2.0.128 |
| Licencias | RSA-2048 + SHA-256, firma PKCS1; HWID binding via WMI |
| Seguridad build | ConfuserEx 2 (ofuscación) + `.integrity` file (firma SHA-256 del DLL) |
| DPAPI | `System.Security.Cryptography.ProtectedData` — clave privada KeyGen cifrada por CurrentUser |
| i18n | `.resx` satellite assemblies, 30 idiomas, markup extension `{loc:T Key=...}` |
| Herramienta dev | `DexSuite.KeyGen` — console app .NET 8 Windows, no se distribuye |

---

## 3. Estructura de carpetas

```
DexSuite (App)/
├── src/
│   └── DexSuite.App/
│       ├── Program.cs                    # Punto de entrada: Velopack → UAC → WPF
│       ├── App.xaml / App.xaml.cs        # Host DI, ciclo de vida, migraciones BD
│       ├── MainWindow.xaml / .xaml.cs    # FluentWindow, lógica bandeja de sistema
│       ├── GameSelectorWindow.xaml       # Modal de selección de videojuegos
│       ├── Assets/                       # AppIcon.ico/png, CatSuggestion.png, Wink.png
│       ├── Converters/                   # InverseBoolToVisibility, KeyToTranslation, LogConverters, SecurityKindConverter, LocFormatMultiConverter
│       ├── Data/
│       │   └── DexSuiteDbContext.cs      # EF Core: Logs, ModuleChanges, ModuleStates, Licenses
│       ├── Markup/
│       │   └── TExtension.cs            # Markup extension {loc:T Key=...} para i18n en XAML
│       ├── Models/
│       │   ├── AppSection.cs            # Enum: Home, Modules, Tuning, Log, Specs, Restore, Settings, Updates, About
│       │   ├── AppTheme.cs              # Enum: Default(0), Cybernetic(1), Redline(2), ZeroLag(3), Midas(4), gaming(100-109)
│       │   ├── CleanupModule.cs         # Record: Id, NameKey, DescriptionKey, Category, Tier, Reversible, Impact
│       │   ├── GameProfile.cs           # Perfil de juego con lista de GameVariant
│       │   ├── ImpactLevel.cs           # Enum: None, Soft, Notable, Strong, Extreme
│       │   ├── LicenseEntity.cs         # Entidad EF de la licencia activa
│       │   ├── LicensePayload.cs        # DTO JSON del payload RSA
│       │   ├── LogEntry.cs              # Entidad EF del historial interno
│       │   ├── ModuleChangeRecord.cs    # Entidad EF de un cambio revertible
│       │   ├── ModuleEnums.cs           # ModuleCategory (Cleanup/Settings/Hardware/Extras), ModuleTier (Free/Advanced/Pro)
│       │   ├── ModuleProgress.cs        # DTO de progreso streaming del runner
│       │   ├── ModuleRunEvent.cs        # Evento de ejecución de módulo
│       │   ├── ModuleStateRecord.cs     # Entidad EF: IsApplied persistido por módulo
│       │   ├── OptionDescriptor.cs      # Descriptor de opción de configuración
│       │   ├── PerformanceScore.cs      # DTO: Total, Verdict, Timestamp (baseline)
│       │   └── SystemInfo.cs            # DTO: CPU, GPU, RAM, OS
│       ├── Resources/
│       │   ├── Strings.resx             # Inglés (fallback)
│       │   └── Strings.{lang}.resx      # 29 idiomas adicionales (ar, bn, ca, cs, da, de, el, es, eu, fi, fr, gl, hi, id, it, ja, ko, nl, no, pl, pt, ro, ru, sv, tr, uk, ur, vi, zh)
│       ├── Services/
│       │   ├── AppLogService.cs         # Historial interno en SQLite, evento EntryAdded
│       │   ├── AppSelfCleanupService.cs # Limpieza de rastros de DexSuite en disco
│       │   ├── BugReportService.cs      # Abre cliente de correo para bugs/sugerencias
│       │   ├── ChangeTrackingService.cs # Registra/revierte cambios (registry, servicios, tareas)
│       │   ├── GameOptimizationService.cs # Catálogo de 26 juegos, lanza .ps1 desde Game_Configs via IWR
│       │   ├── HardwareIdProvider.cs    # HWID via WMI (CPU+Placa+UUID), SHA-256 → Base32 20 chars
│       │   ├── HelpService.cs           # Textos de ayuda contextuales
│       │   ├── IntegrityVerifier.cs     # Verifica .integrity (SHA-256 + firma RSA del DLL)
│       │   ├── LicenseService.cs        # Activar/revalidar/desactivar licencias RSA
│       │   ├── LocalizationService.cs   # Singleton, 30 idiomas, ResourceManager sobre .resx
│       │   ├── ModuleCatalog.cs         # Los 19 módulos del catálogo (M01-M19)
│       │   ├── ModuleStateService.cs    # Persiste IsApplied por módulo en SQLite
│       │   ├── NativeModuleRunner.cs    # Orquesta IModuleExecutor vía IAsyncEnumerable
│       │   ├── PerformanceAnalyzer.cs   # Mide CPU/RAM/disco → PerformanceScore
│       │   ├── PerformanceBaselineService.cs # Persiste/carga baseline.json
│       │   ├── QuickCleanService.cs     # Limpieza rápida de temp/thumbnails
│       │   ├── RestorePointService.cs   # Crea puntos de restauración de Windows
│       │   ├── SecurityCheckService.cs  # Ejecuta Defender/SFC/DISM/MRT
│       │   ├── SettingsService.cs       # settings.json con debounce 400ms, escritura atómica
│       │   ├── SystemInfoService.cs     # Lee CPU/GPU/RAM/OS vía WMI
│       │   ├── ThemeService.cs          # Aplica temas intercambiando ResourceDictionary, persiste theme.json
│       │   ├── ToastNotificationService.cs # Notificaciones toast WinRT (Win10 19041+)
│       │   ├── VelopackUpdateService.cs # GitHub Releases → UpdateManager, repo: SrDexterGF/DexSuite
│       │   ├── WingetService.cs         # winget upgrade --all con streaming de output
│       │   ├── Licensing/
│       │   │   ├── LicenseWatchdog.cs   # BackgroundService: re-verifica cada 10min ± 60s jitter
│       │   │   ├── PublicKeyAssembler.cs # Ensambla clave pública RSA de KeyPartA..D
│       │   │   └── Keys/
│       │   │       ├── KeyPartA.cs      # Parte 1/4 de la clave pública RSA embebida
│       │   │       ├── KeyPartB.cs      # Parte 2/4
│       │   │       ├── KeyPartC.cs      # Parte 3/4
│       │   │       └── KeyPartD.cs      # Parte 4/4
│       │   └── CleanupModules/
│       │       ├── IModuleExecutor.cs   # Interfaz: ModuleId + ExecuteAsync → IAsyncEnumerable<ModuleProgress>
│       │       ├── ModuleExecutorBase.cs # Base: helpers de filesystem, registry, servicios, procesos externos
│       │       ├── M01Prefetch.cs       # Prefetch + Temp + D3DSCache (Free)
│       │       ├── M02SystemLogs.cs     # Logs del sistema (Free)
│       │       ├── M03TempAndRecycle.cs # Papelera + temp usuario (Free)
│       │       ├── M04DeepCleanup.cs    # Limpieza profunda (Avanzado)
│       │       ├── M05WindowsUpdate.cs  # Caché Windows Update (Free)
│       │       ├── M06DismComponentStore.cs # DISM cleanup (Avanzado)
│       │       ├── M07BrowserCache.cs   # Caché de navegadores (Free)
│       │       ├── M08NetworkReset.cs   # Reset de red (Avanzado)
│       │       ├── M09StoreOneDriveTeams.cs # Caché Store/OneDrive/Teams (Avanzado)
│       │       ├── M10SfcDism.cs        # SFC + DISM /RestoreHealth (Avanzado)
│       │       ├── M11Peripherals.cs    # Drivers de periféricos (Avanzado)
│       │       ├── M12WingetUpgrade.cs  # Actualización de apps via winget (Free)
│       │       ├── M13CopilotCortanaTelemetry.cs # Deshabilita Copilot/Cortana/telemetría (Avanzado)
│       │       ├── M14PrivacyServices.cs # Servicios de privacidad (Pro)
│       │       ├── M15Performance.cs    # Tweaks extremos de rendimiento (Pro, ImpactLevel.Extreme)
│       │       ├── M16Ethernet.cs       # Optimización de red Ethernet (Pro)
│       │       ├── M17Security.cs       # Ajustes de seguridad (Pro)
│       │       ├── M18SsdTrim.cs        # TRIM manual SSD (Pro)
│       │       └── M19Drivers.cs        # Gestión de drivers (Pro)
│       ├── Themes/
│       │   ├── Default.xaml
│       │   ├── Cybernetic.xaml         # Paleta de marca: cian + violeta
│       │   ├── Redline.xaml, ZeroLag.xaml, Midas.xaml
│       │   └── Valor.xaml, Fortress.xaml, Counter.xaml, Legends.xaml, Crafter.xaml, Apex.xaml, Guardian.xaml, Rivals.xaml, Tenno.xaml, Divers.xaml
│       └── ViewModels/
│           ├── GameSelectorViewModel.cs # GameTileViewModel + filtrado por SearchText
│           ├── MainViewModel.cs         # ViewModel central (~2100 líneas), toda la lógica de UI
│           ├── ModuleItemViewModel.cs   # Wrapper de CleanupModule con estados Run/Applied/Error
│           └── ThemeItemViewModel.cs    # Item del selector de temas con IsUnlocked/IsComingSoon
├── tools/
│   └── DexSuite.KeyGen/
│       ├── DexSuite.KeyGen.csproj       # Console app, net8.0-windows, NO distribuida
│       └── Program.cs                   # Subcomandos: init, pubkey, gen, sign-integrity, verify
├── scripts/
│   ├── publish-release.ps1             # Pipeline completo: version bump → dotnet publish → ConfuserEx → .integrity → vpk pack → gh release create
│   ├── generate-resx.ps1               # Genera los .resx desde translations.json + modules.json
│   ├── generate-logo.ps1               # Generación de assets de logo
│   ├── devtools.ps1                     # Herramientas de desarrollo
│   ├── modules.json                     # Traducciones de los 19 módulos en 30 idiomas
│   ├── translations.json                # Traducciones generales de la app en 30 idiomas
│   └── extend_translations.py          # Script Python para extender traducciones
├── Releases/                           # Artefactos vpk generados localmente
├── publish/                            # Salida dotnet publish (temporal)
├── Confuser.crproj                     # Configuración ConfuserEx 2: control flow + anti-debug + anti-tamper (excluye Models/ViewModels de renaming)
├── DexSuite.sln                        # Solución con App + KeyGen
└── CONTEXT.md                          # Este archivo
```

---

## 4. Estado actual — Qué está implementado

### Web DexSuite
- Creada base `../DexSuite (Web)/` para la web oficial como proyecto hermano de la app: Next.js App Router, TypeScript, Tailwind CSS, shadcn/ui, next-intl ES/EN, rutas principales, sitemap/robots y `.env.example`.
- Implementada fase 1: layout global, header/footer, navegación responsive, selector ES/EN, botón base, preview visual de app, fondo Cybernetic y asset real `public/images/dexsuite-icon.png`.
- Implementada fase 2: contenido ES/EN para Inicio, DexSuite, Servicios, Descarga y Reseñas; componentes `FeatureGrid`, `SectionHeading`, `PlanCards`, `CtaBand`; datos visuales en `src/config/content.ts`.
- Pendiente instalar dependencias y validar build cuando Node.js/npm esten disponibles en la shell.

### Arquitectura y ciclo de vida
- Arranque en 3 pasos: `VelopackApp.Build().Run()` → UAC elevation → WPF (`Program.cs`)
- DI completa con `Microsoft.Extensions.Hosting`; todos los servicios registrados como Singleton (GameSelectorViewModel y GameSelectorWindow como Transient)
- Migración automática de datos de `%LocalAppData%\DexSuite` a `%AppData%\DexSuite` (colisión con carpeta Velopack)
- SQLite inicializado antes de `_host.Start()` para que `LicenseWatchdog` encuentre tablas listas
- `SettingsService` con debounce 400ms y escritura atómica (temp + rename)
- `FlushAsync()` en `OnExit` para no perder el último snapshot de settings

### Sistema de módulos (19 módulos, M01-M19)
- Catálogo en `ModuleCatalog.cs`: 6 Free, 7 Avanzado, 6 Pro
- Cada módulo es un `IModuleExecutor` con `ExecuteAsync` → `IAsyncEnumerable<ModuleProgress>`
- `NativeModuleRunner` orquesta la ejecución y garantiza la emisión de `Done` aunque el módulo no lo haga
- Streaming de progreso estructurado (Header/Step/Ok/Warn/Error/Done/Info/Heartbeat)
- Barra de progreso y etiqueta "Módulo X de Y" en tiempo real
- Cancelación con `CancellationTokenSource` (`Cancel` command)
- Spinner mínimo de 600ms por módulo para que la UI muestre el estado transitorio
- Flush de buffer cada 100ms (tope de 80.000 caracteres en OutputLog)
- Estado `IsApplied` persistido en SQLite entre sesiones (barra → tick)

### Sistema de licencias
- RSA-2048 + SHA-256 PKCS1 con HWID binding
- HWID: `Win32_Processor.ProcessorId` + `Win32_BaseBoard.SerialNumber` + `Win32_ComputerSystemProduct.UUID`, SHA-256 truncado → Base32 20 chars, formato visual `XXXX-XXXX-XXXX-XXXX-XXXX`
- Clave pública embebida en 4 partes separadas (`KeyPartA..D`) ensambladas por `PublicKeyAssembler`
- `LicenseService.ActivateAsync()` → verifica firma RSA → persiste en SQLite (solo 1 fila activa)
- `LicenseWatchdog`: BackgroundService, re-verifica cada 10min ± 60s jitter; reduce tier a Free si falla
- Re-validación inicial en `App.OnStartup` antes de mostrar ventana
- El tier NO se lee de `settings.json`; siempre viene de la firma RSA (anti-tamper)
- `DexSuite.KeyGen` (herramienta dev): `init` (genera par RSA-2048, clave privada cifrada con DPAPI CurrentUser), `pubkey` (extrae partes para pegar en KeyPartA..D), `gen` (genera licencia), `sign-integrity` (firma SHA-256 del DLL), `verify`
- DPAPI implementado: clave privada del KeyGen cifrada en disco en `%LocalAppData%\DexSuiteKeyGen\private.xml`, con migración automática de formato claro a cifrado

### Integridad del ejecutable
- `IntegrityVerifier` verifica `DexSuite.App.dll.integrity` (SHA-256 del DLL + firma RSA)
- Firma generada por `DexSuite.KeyGen sign-integrity` en el pipeline de release
- En builds Debug o sin clave pública configurada: omite la verificación
- Si falta el `.integrity` (build de desarrollo local): omite silenciosamente; solo bloquea si existe y la firma es inválida

### Sistema de temas
- 5 temas estándar: Default (Free), Cybernetic/Redline/ZeroLag/Midas (Pro)
- 10 temas gaming en Expander "Temas 😉": Valor/Fortress/Counter/Legends/Crafter/Apex/Guardian/Rivals/Tenno/Divers (todos Pro)
- Aplicación intercambiando el `ResourceDictionary` cuyo Source contiene `/Themes/`
- Persistido en `%AppData%\DexSuite\theme.json`
- Tarjeta "Coming Soon" al final del selector de temas normales (placeholder no seleccionable)
- `IsThemeUnlocked` calcula jerarquía: Free < Avanzado < Pro

### Sistema de localización (i18n)
- `LocalizationService.Instance` (singleton estático) + `TExtension` para binding en XAML
- `ResourceManager` sobre `DexSuite.App.Resources.Strings`
- 30 idiomas: es, gl, ca, eu, en, pt, fr, de, it, zh, ru, uk, ar, ja, ko, hi, bn, ur, id, tr, vi, nl, sv, ro, pl, cs, el, da, no, fi
- Al cambiar idioma: notifica `"Item[]"` → todos los `{loc:T}` se refrescan automáticamente
- Traducciones en `scripts/translations.json` + `scripts/modules.json`; compiladas con `generate-resx.ps1`

### Secciones de la app (sidebar)
- **Home**: bienvenida, estado del tier, análisis de rendimiento (baseline + comparación), quickclean, winget upgrade, security check, punto de restauración manual
- **Modules**: lista de módulos filtrable por tier (Free/Avanzado/Pro + ProExtra para gaming), selección individual o masiva, ejecutar/cancelar, búsqueda en tiempo real
- **Tuning**: seccion bloqueada con overlay "Coming Soon" (`TuningComingSoon = true` hardcoded en MainViewModel). Los botones de descarga de herramientas (URLs) están implementados en `OpenDownloadUrlAsync`. El módulo Tuning aparece en la sección "Extras" del sidebar con candado.
- **Log**: historial interno SQLite (últimas 500 entradas), export a .txt, botón limpiar
- **Specs**: specs del sistema (CPU/GPU/RAM/OS) cargados on-demand via WMI
- **Restore** (Revertir cambios): lista de cambios de registro/servicios/tareas pendientes de revertir, revertir uno o todos, botón "marcar como leído" pendiente (ver sección 5)
- **Settings**: idioma, tema, toggles (autoSelectRecommended, jumpToLogOnRun, warnBeforeNonReversible, createRestorePointBeforeRun, notifyOnFinish, autoUpdateEnabled, minimizeToTray), canal de actualización (Stable/Beta), activación de licencia (input + HWID display + copyHWID), selfCleanup, resetSettings
- **Updates**: check manual, auto-update toggle, barra de progreso de descarga, apply update (Velopack reinicia la app)
- **About**: ChangelogTitle con versión actual. (Sección incompleta — ver pendientes)

### Sistema de actualización (Velopack)
- GitHub Releases como fuente: `https://github.com/SrDexterGF/DexSuite`
- `CheckForUpdatesAsync` → `DownloadAndApplyAsync` → `ApplyUpdatesAndRestart`
- Badge verde en sidebar cuando `HasAvailableUpdate = true`
- Check automático al arrancar (fire-and-forget)
- En entorno dev (`!IsInstalledBuild`): omite check y muestra "Modo dev"

### Funcionalidades adicionales
- **Bandeja de sistema**: `H.NotifyIcon.Wpf`, menú contextual (Restaurar / separador / Cerrar DexSuite), doble clic restaura. Cuando `MinimizeToTray=true`: icono siempre visible, `ShowInTaskbar=false`, minimizar oculta ventana, X oculta en lugar de cerrar
- **Gaming** (Pro/Avanzado): `GameSelectorWindow` con 26 juegos, buscador en tiempo real (`ICollectionView` filtrado), variantes por ComboBox cuando hay múltiples, `IsConfigApplied=true` marca tick verde tras optimizar. Los scripts se descargan y ejecutan via `powershell.exe -ExecutionPolicy Bypass -Command "IWR | IEX"` desde `raw.githubusercontent.com/SrDexterGF/Game_Configs/main`
- **Análisis de rendimiento**: `PerformanceAnalyzer` → `PerformanceScore` (Total + Verdict), baseline persistido en `baseline.json`, comparación antes/después con delta
- **Change tracking + revert**: `ChangeTrackingService` registra cambios de registro, servicios, tareas programadas en SQLite; `RevertChangeAsync`/`RevertAllPendingAsync` los deshacen; `SyncAppliedStateAfterRevertAsync` actualiza `IsApplied` de los módulos afectados
- **Punto de restauración**: manual desde Settings, o automático antes de ejecutar (diálogo de 3 opciones: Sí/No/Cancelar); si falla el auto no aborta la ejecución
- **Winget upgrade**: streaming de output en tiempo real al historial interno
- **Security check**: DefenderQuick, SFC, DISM, MRT con streaming al historial
- **Bug report / Feature suggestion**: abre cliente de correo con destinatario/asunto predefinido
- **Self cleanup**: borra logs y rastros de DexSuite en disco
- **Toast notifications**: WinRT nativas (requiere Win10 19041+), se disparan al terminar un run

### Pipeline de release (`publish-release.ps1`)
1. Actualiza `<Version>` en .csproj (lectura/escritura UTF-8 explícita, evita inflado del archivo en PS 5.1)
2. `dotnet publish -c Release -r win-x64 --self-contained false`
3. ConfuserEx 2 (si está instalado): control flow, anti-debug, anti-tamper, anti-dump, anti-ildasm, constants, ref proxy, resources. Excluye renaming en Models/ViewModels
4. `DexSuite.KeyGen sign-integrity DexSuite.App.dll` (genera `DexSuite.App.dll.integrity`)
5. `vpk pack` → genera artefactos en `Releases/`
6. `gh release create v{Version}` → sube solo artefactos del canal actual

---

## 5. Estado actual — Qué falta por hacer

### Bloque 1 — Fixes de UI (6 items)
1. **Tarjeta Coming Soon texto inferior**: el texto descriptivo de la tarjeta Coming Soon en el selector de temas se corta o no se muestra correctamente
2. **Botón "marcar como leído" en Revertir cambios**: falta un botón que permita marcar entradas individuales como leídas/descartadas sin revertirlas
3. **Eliminar separador duplicado bajo Tuning**: hay un separador extra visible bajo el ítem Tuning en la sidebar
4. **Altura GameSelectorWindow = ventana principal (900px)**: la ventana modal de selección de juegos debe tener la misma altura que la ventana principal
5. **Icono bandeja condicional (solo cuando ventana oculta) + más opciones menú contextual**: actualmente el icono es siempre visible cuando `MinimizeToTray=true`; debería aparecer solo cuando la ventana está oculta. Además el menú contextual tiene solo 2 ítems, se necesitan más opciones
6. **Scroll en selector de idiomas + orden alfabético**: el ComboBox de idiomas en Settings no tiene scroll habilitado correctamente y los idiomas no están en orden alfabético (actualmente en orden de adición)

### Bloque 2 — Gaming
1. **Descargo de responsabilidad antes de abrir GameSelectorWindow**: mostrar un diálogo de aviso legal/disclaimer antes de la primera apertura de la ventana de juegos
2. **Botón Revertir por juego**: en cada `GameTileViewModel`, mostrar un botón Revertir visible cuando `IsConfigApplied=true` que permita deshacer la optimización del juego concreto

### Bloque 3 — Selector "Recomendado" adaptativo
- El botón "Seleccionar recomendados" (`SelectRecommended`) actualmente usa `m.Module.RecommendedDefault` fijo. Debe ser adaptativo según el plan: seleccionar diferentes conjuntos de módulos según si el usuario es Free, Avanzado o Pro

### Bloque 4 — Sección Acerca de
- Implementar contenido completo de la sección About:
  - Instagram: `@DexSuite`
  - Linktree: `https://linktr.ee/DexSuite`
  - Email: `suitedex@gmail.com`
  - Botones de RRSS con iconos
  - Changelog localizado de la versión actual

### Bloque 5 — GitHub
- Vaciar el README del repo público (ocultar información técnica)
- Vaciar o limpiar las notas de las releases publicadas

### Bloque 6 — Auditorías pendientes
1. **Seguridad**: revisar permisos, superficies de ataque, validaciones de entrada
2. **Traducciones**: verificar que los 30 idiomas tienen todas las claves completas, sin `[key]` visibles en producción
3. **Calidad visual**: revisión de UI en todos los temas, especialmente los de gaming
4. **Referencias obsoletas**: limpiar código muerto, servicios no usados, comentarios desactualizados
5. **Código muerto**: `ModuleStatus` en `ModuleItemViewModel` marcado como "legacy no usado por la UI nueva"

### Bloque 7 — UX / Textos (pendiente de implementar)
Ver prompt detallado generado en sesión 2026-06-03. Resumen de bloques:
- **A**: Comportamiento — corregir "Marcar como leído" (no debe vaciar la lista), scrollbar visual en idiomas, reordenar idiomas (es primero, luego latino-europeo, luego cirílico, luego RTL/asiático)
- **B**: Menú juegos — eliminar subtítulos de tiles, reescribir footer
- **C**: Inicio — reescribir App.Subtitle, descripciones de 6 tarjetas, tooltip (?) en seguridad, reescribir Log.Subtitle, corregir tooltip de impacto (menciona chip que no existe)
- **D**: Ajustes — eliminar "(futuro)" de WarnBeforeNonReversible, reescribir Settings.Subtitle/Theme.Description/Behavior, separar frases en Licencia/Activación/PuntoRestauración
- **E**: Acerca de — reescribir changelog (menos técnico), reescribir BugReport.Subtitle
- **F**: Layout — reducir espacio bajo Tuning en sidebar, separar botón Actualizar de descripción en Revertir, evaluar/cambiar título "Performance Series"

### Bloque 8 — Nuevos idiomas (paridad con Steam)
Añadir los siguientes idiomas que Steam soporta y DexSuite no tiene aún:
- `zh-TW` — 繁體中文 (chino tradicional)
- `th` — ไทย (tailandés)
- `bg` — Български (búlgaro)
- `hu` — Magyar (húngaro)
- `pt-BR` — Português do Brasil (variante separada de pt-PT)
Requiere: nuevos .resx, actualizar LocalizationService.cs, ComboBox de idiomas y pipeline de traducciones.

### Bloque 9 — Deuda técnica futura
- Auditoría general de la app (UI, código, flujos)
- Auditoría completa de traducciones (errores, localización, formatos, coherencia de IU)
- Dividir módulos en opciones individuales (un ajuste por opción, no conjuntos)
- Revisar distribución de colores por tema (coherencia muestra ↔ tema aplicado)
- Mejorar analizador de rendimiento (funcionalidad, puntuación, valor al usuario)
- Auditoría de código (código muerto, rutas obsoletas, optimizaciones)
- Humanizar todos los textos de la app (que parezca escrita por el desarrollador, no por IA)
- Eliminar cualquier mención a terceros (personas, empresas, IA, coautores) en app/código/GitHub
- Auditoría de seguridad completa
- **Mejorar protección anti-ingeniería-inversa**: la ofuscación actual (ctrl flow + constants + ref proxy) NO renombra (rename rompe Velopack/i18n/WPF), así que los nombres siguen legibles. Evaluar: licencia con validación server-side (la verificación local es bypasseable parcheando IL), o solución de ofuscación comercial compatible con .NET 8 + Velopack. Ver memoria confuserex-config.md.
- Revisión de cumplimiento legal (normas, leyes, copyright)
- Cobertura legal completa (EULA, privacidad, descargos, telemetría)
- Registro legal y formalización de la app como propiedad del desarrollador

### Bloque 10 — Funcionalidad futura (UX/módulos/web)
- **Vista avanzada de módulos**: además de la vista simple actual (opciones agrupadas), crear una vista avanzada/detallada con cada ajuste individual seleccionable. Alternativa preferida a separar todos los módulos.
- **Selector de vista en Ajustes**: opción para elegir si al abrir Módulos se muestra la vista simple o la avanzada por defecto.
- **Botón "Revertir" en Módulos**: junto a "Ejecutar", permitir seleccionar X módulos y revertirlos a su estado original (revertir cualquier cambio de la app desde la propia selección).
- **Acceso a la web desde la app**: cuando exista la web, enlazarla desde la zona de RRSS (Acerca de), al pulsar "Sr. Dexter" en créditos, y al pulsar el logo del menú Inicio y el logo del sidebar.

### Pantalla de bienvenida legal
- Mostrar Terms of Use la primera vez que arranca la app. Debe registrar en settings que el usuario aceptó para no mostrarla de nuevo.

### Trustpilot
- Botón "Dejar reseña" en la app
- Mostrar reseñas en la app vía widget o API
- **Bloqueo actual**: Trustpilot no puede verificar `dexsuite.carrd.co`. Se creó `https://srdextergf.github.io/dexsuite-site/` con la meta-etiqueta de verificación, pero Trustpilot sigue apuntando al dominio de Carrd (pendiente de resolución por parte de Trustpilot)

---

## 6. Decisiones técnicas importantes

### Clave pública en 4 partes (KeyPartA..D)
La clave pública RSA se parte en 4 fragmentos en archivos separados del código. Esto dificulta que un atacante la localice y sustituya tras la ofuscación de ConfuserEx: necesita encontrar las 4 partes Y el orden de concatenación. `PublicKeyAssembler.TryCreatePublicKey()` las concatena y llama a `RSA.ImportSubjectPublicKeyInfo` (formato SPKI Base64).

### El tier NO se guarda en settings.json
El `UserTier` solo proviene de la firma RSA verificada en tiempo de ejecución. Si se leyera de `settings.json`, un usuario podría editarlo a mano y obtener Pro sin clave válida. La fuente de verdad es siempre `LicenseService.CurrentTier`.

### Migración %LocalAppData% → %AppData%
Velopack instala en `%LocalAppData%\DexSuite`, lo que colisionaba con los datos de usuario. Se migró a `%AppData%\DexSuite` para que los datos sobrevivan las actualizaciones (Velopack limpia `%LocalAppData%\DexSuite\current` en cada update). La migración ocurre en cada arranque pero solo copia si el destino no existe.

### Orden obligatorio en Program.cs
`VelopackApp.Build().Run()` DEBE ir antes que cualquier otra cosa. Si va después de la verificación UAC o de la inicialización WPF, los hooks de instalación/desinstalación de Velopack no funcionan. El `app.manifest` usa `asInvoker` (no `requireAdministrator`) para que el launcher de Velopack pueda arrancar sin elevar; la elevación real se hace en `Program.Main` via `runas`.

### EnsureCreated + migraciones manuales
Se usa `EnsureCreated()` en lugar de EF Migrations para mantener la complejidad baja. Las tablas añadidas después de la primera versión (`ModuleChanges`, `ModuleStates`, `Licenses`) se crean con `CREATE TABLE IF NOT EXISTS` en `App.OnStartup` para compatibilidad con usuarios que tienen la BD de versiones anteriores.

### IDbContextFactory en lugar de DbContext singleton
Cada operación de BD crea su propio `DbContext` desde `IDbContextFactory<DexSuiteDbContext>`. Evita compartir contexto entre hilos (EF Core no es thread-safe).

### App.xaml carga Cybernetic en el slot de tema inicial
El `App.xaml` tiene `Cybernetic.xaml` como tema inicial en las `MergedDictionaries`, pero `DefaultStartupTheme = AppTheme.Default`. En la práctica, `ThemeService.ApplyTheme()` busca el `ResourceDictionary` cuyo Source contiene `/Themes/` y lo reemplaza, así que el orden en XAML no importa funcionalmente.

### TuningComingSoon es un flag estático hardcoded
`MainViewModel.TuningComingSoon { get; } = true` es un `static` intencional para dejarlo claro en el código: no es un toggle de usuario ni de licencia, es un bloqueo de desarrollo. Cuando la sección se desbloquee, basta con cambiarlo a `false`.

### Gaming: scripts externos sin verificación de integridad
`GameOptimizationService` descarga y ejecuta `.ps1` directamente desde GitHub raw sin verificar firma ni hash. Esto es una deuda técnica de seguridad: si el repo `Game_Configs` fuera comprometido, los usuarios ejecutarían código malicioso. El disclaimer previo (Bloque 2) mitiga el riesgo pero no lo elimina.

### ConfuserEx excluye renaming en Models/ViewModels
WPF resuelve bindings por reflexión sobre nombres de propiedades. Si ConfuserEx renombrara `IsRunning` o `CurrentSection`, los bindings XAML dejarían de funcionar. El `Confuser.crproj` excluye `DexSuite.App.Models` y `DexSuite.App.ViewModels` del control-flow agresivo pero mantiene anti-tamper y constants.

### Spinner mínimo de 600ms
Los módulos de limpieza (borrar archivos de log) terminan en milisegundos. Sin el dwell mínimo, WPF pintaría Header y Done casi simultáneamente y el usuario nunca vería el spinner de "aplicando". El `MinSpinnerVisibleMs = 600` garantiza al menos un fotograma visible del estado transitorio.

### _settingsHydrated flag anti-escritura en arranque
Durante el constructor de `MainViewModel`, se hidratan todos los ObservableProperty desde `settings.json`. Cada asignación dispararía `PersistSettings()`. El flag `_settingsHydrated = false` durante la hidratación evita N escrituras en disco; solo se activa (`= true`) al terminar. Los `partial void OnXxxChanged` comprueban este flag antes de llamar a `ScheduleSave`.

---

## 7. Problemas conocidos o deuda técnica

1. **Gaming sin revert real**: cuando un usuario aplica la configuración de un juego, `IsConfigApplied=true` es solo en memoria (sesión actual). No hay integración con `ChangeTrackingService` para los scripts .ps1. El botón Revertir pendiente (Bloque 2) requeriría que los scripts tengan un comando de deshacer, o que se creen puntos de restauración automáticos.

2. **Scripts de juegos sin verificación de integridad**: ver sección 6. Riesgo si el repo Game_Configs es comprometido.

3. **Traducciones parciales**: `modules.json` tiene solo 10 idiomas completos (los principales); los 20 adicionales pueden tener claves ausentes que se mostrarán como `[key]` en la UI.

4. **ModuleStatus legacy**: el enum `ModuleStatus` (Pending/Running/Completed/Skipped/Failed) en `ModuleItemViewModel` está marcado como "no usado por la UI nueva". La UI nueva usa `ModuleRunStatus` (Idle/Running/Success/Error) y `ModuleVisualState`. El código legacy permanece por compatibilidad pero debería limpiarse.

5. **Trustpilot bloqueado**: la verificación de dominio no se puede completar porque Trustpilot apunta a `dexsuite.carrd.co` y no acepta el dominio de GitHub Pages como alternativa. Requiere acción externa (contactar soporte Trustpilot o conseguir dominio propio).

6. **ConfuserEx como herramienta opcional**: si no está instalado, el pipeline salta la ofuscación sin error fatal. Las builds publicadas sin ConfuserEx son más fáciles de decompilar.

7. **Revert de archivos no implementado**: `ChangeTrackingService` soporta revertir registro, servicios y tareas programadas, pero NO archivos (requeriría backup/snapshot previo al módulo). El tipo `ChangeType.File` existe en el modelo pero no tiene implementación de reversión.

8. **Log interno tope en 500 entradas en memoria**: el `ObservableCollection<LogEntry>` en UI tiene un tope de 500 entradas. El SQLite tiene todas las entradas. Para historial largo puede ser insuficiente.

9. **MinimizeToTray siempre visible**: actualmente cuando `MinimizeToTray=true` el icono de bandeja está SIEMPRE visible (no solo cuando la ventana está oculta). Esto es contraintuitivo. Ver Bloque 1, item 5.

10. **Sección About vacía**: la sección About existe en el enum `AppSection` y en el sidebar, pero no tiene contenido implementado (RRSS, changelog real, etc.).

11. **Separador duplicado bajo Tuning**: visible en el sidebar, pendiente de fix de XAML.

---

## 8. Próximos pasos

### Inmediatos (UI / UX — menor esfuerzo, mayor impacto visual)
1. **Bloque 1 completo** (6 fixes de UI descritos en sección 5)
2. **Sección About** con RRSS e info de contacto (Bloque 4)

### Funcionalidad nueva
3. **Disclaimer gaming** (Bloque 2, item 1) — diálogo previo a `GameSelectorWindow`
4. **Revertir por juego** (Bloque 2, item 2) — botón en `GameTileViewModel` cuando `IsConfigApplied=true`
5. **Selector Recomendado adaptativo** (Bloque 3) — 3 conjuntos según tier

### Legal / Confianza
6. **Pantalla de bienvenida legal** (Terms of Use, primera ejecución) — persiste flag en `settings.json`
7. **Trustpilot**: resolver la verificación de dominio; mientras tanto, botón estático "Dejar reseña" en About que abra la URL de Trustpilot en navegador

### Limpieza / Calidad
8. **Bloque 6 — Auditorías**: traducciones, seguridad, visual, referencias obsoletas, código muerto
9. **GitHub**: limpiar README y notas de release (Bloque 5)

### Infraestructura futura
10. **Web DexSuite fase 3**: implementar formulario de contacto con Resend.
11. **Sección Tuning real**: cuando se desbloquee, cambiar `TuningComingSoon = false` en `MainViewModel` y añadir handlers para resolución, frecuencia, aceleración de ratón, etc. Cada cambio debe usar `IChangeTrackingService` para aparecer en Revertir cambios.
12. **Revert de archivos**: implementar backup pre-módulo para soportar `ChangeType.File` en el sistema de reversión.

---

## 9. Comandos esenciales

### Desarrollo local
```powershell
# Compilar en Debug (sin UAC en IDE, sin verificación de integridad)
dotnet build "src\DexSuite.App\DexSuite.App.csproj" -c Debug

# Compilar en Release (sin publish ni vpk)
dotnet build "src\DexSuite.App\DexSuite.App.csproj" -c Release
```

### Publicar una nueva release
```powershell
# Desde la raíz del proyecto — working directory debe ser la raíz
.\scripts\publish-release.ps1 -Version 0.2.24
# O con canal beta:
.\scripts\publish-release.ps1 -Version 0.2.24 -Channel beta
# Con notas de release:
.\scripts\publish-release.ps1 -Version 0.2.24 -Notes "Fix para X"

# Requisitos previos:
# - dotnet SDK 8.0+
# - vpk CLI:        dotnet tool install -g vpk
# - gh CLI autenticado: gh auth login
# - ConfuserEx CLI (opcional): dotnet tool install -g ConfuserEx.CLI
# - DexSuite.KeyGen inicializado: ver abajo
```

### DexSuite.KeyGen (sistema de licencias)
```powershell
$Root       = "C:\Users\mgf74\Documents\Claude Environment W11\DexSuite (App)"
$KeyGenBuild = "$Root\tools\DexSuite.KeyGen\bin\Release\net8.0"
$KeyGenDll  = "$KeyGenBuild\DexSuite.KeyGen.dll"

# Compilar KeyGen
dotnet build "$Root\tools\DexSuite.KeyGen\DexSuite.KeyGen.csproj" -c Release -o $KeyGenBuild /p:UseAppHost=false --nologo

# Inicializar (genera par RSA-2048, cifra private.xml con DPAPI)
# CUIDADO: SOLO HACER UNA VEZ — invalidaría todas las licencias existentes si se rehace
dotnet exec $KeyGenDll init

# Extraer partes de clave pública para pegar en KeyPartA..D.cs
dotnet exec $KeyGenDll pubkey

# Generar licencia para un HWID específico
# HWID = el que aparece en la app en Settings > Licencia (formato XXXX-XXXX-XXXX-XXXX-XXXX)
dotnet exec $KeyGenDll gen <HWID> <tier>
# tier: free | advanced | pro

# Firmar integridad del DLL (lo hace automáticamente publish-release.ps1)
dotnet exec $KeyGenDll sign-integrity "$Root\publish\win-x64\DexSuite.App.dll"

# Verificar un blob de licencia
dotnet exec $KeyGenDll verify <blob_de_licencia>
```

### Regenerar .resx (traducciones)
```powershell
# Requiere PowerShell y los archivos translations.json + modules.json en scripts/
.\scripts\generate-resx.ps1
# Salida: src\DexSuite.App\Resources\Strings.{lang}.resx para cada idioma
```

### Base de datos (SQLite)
```
Ubicación en producción:
  %AppData%\DexSuite\dexsuite.db

Tablas:
  Logs           — historial interno de la app
  ModuleChanges  — cambios revertibles registrados por los módulos (registry, servicios, tareas)
  ModuleStates   — IsApplied por módulo (int ModuleId PK)
  Licenses       — licencia activa (máximo 1 fila)

NO hay EF Migrations. Las tablas post-primera-versión se crean con
CREATE TABLE IF NOT EXISTS en App.OnStartup (EnsureModuleChangesTable,
EnsureModuleStatesTable, EnsureLicensesTable).
```

### Archivos de configuración del usuario
```
Todos en %AppData%\DexSuite\
  settings.json  — preferencias de usuario (idioma, toggles, canal de actualización)
  theme.json     — tema seleccionado
  baseline.json  — baseline de rendimiento persistido (PerformanceScore)
  dexsuite.db    — base de datos SQLite

Logs en %LocalAppData%\DexSuite\logs\
  dexsuite-YYYYMMDD.log (rolling diario, 7 días de retención, UTF-8)
```

### Rutas clave en código
```csharp
// Repo Velopack (VelopackUpdateService.cs):
const string RepoUrl = "https://github.com/SrDexterGF/DexSuite";

// Scripts de juegos (GameOptimizationService.cs):
const string BaseRawUrl = "https://raw.githubusercontent.com/SrDexterGF/Game_Configs/main";

// Clave privada KeyGen (en equipo del desarrollador):
// %LocalAppData%\DexSuiteKeyGen\private.xml (cifrada con DPAPI CurrentUser)
```
