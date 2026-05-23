# DexSuite

App de escritorio Windows (WPF / .NET 8) que limpia, optimiza y asegura el sistema.

En esta primera version la app es un **front-end moderno** del script
[`DexSuite_CleanUp_v0.9.0.bat`](../DexSuite%20(Script)/DexSuite_CleanUp_v0.9.0.bat):
- Te muestra los 20 modulos del .bat con un toggle por cada uno.
- Cuando pulsas "Ejecutar", lanza el .bat heredando privilegios de admin
  y conduce su menu por stdin para correr solo lo que has marcado.
- La salida del .bat se ve en tiempo real dentro de la propia app.

A partir de aqui, cada modulo se ira reescribiendo en C# nativo para soportar
historial de cambios y revertir.

## Stack

- .NET 8 (LTS) + WPF
- [`WPF-UI`](https://github.com/lepoco/wpfui) (Lepoco) — estilo Windows 11 (Mica + Fluent)
- [`CommunityToolkit.Mvvm`](https://github.com/CommunityToolkit/dotnet) — MVVM con source generators
- [`Microsoft.Extensions.Hosting`](https://learn.microsoft.com/dotnet/core/extensions/generic-host) — DI + lifetime
- [`Microsoft.EntityFrameworkCore.Sqlite`](https://learn.microsoft.com/ef/core/) — persistencia
- [`Serilog`](https://serilog.net) — logging a archivo
- [`Velopack`](https://github.com/velopack/velopack) — auto-update (cuando empecemos a distribuir)

## Estructura

```
DexSuite/
  DexSuite.sln
  src/
    DexSuite.App/
      app.manifest          UAC requireAdministrator + DPI per-monitor
      App.xaml(.cs)         Host + DI + Serilog
      MainWindow.xaml(.cs)  Ventana principal (FluentWindow + Mica)
      Models/               CleanupModule, ModuleCategory, ModuleTier
      ViewModels/           MainViewModel, ModuleItemViewModel
      Services/             IModuleCatalog/ModuleCatalog, IBatRunner/BatRunner
```

## Como ejecutar (desarrollo)

```powershell
cd "C:\Users\mgf74\Documents\Claude Environment W11\DexSuite"
dotnet build
dotnet run --project src\DexSuite.App
```

Al arrancar saldra el cuadro de UAC pidiendo permisos administrativos.
