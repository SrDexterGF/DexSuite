# =============================================================
#  publish-release.ps1
#  Publica una nueva versión de DexSuite en GitHub Releases.
#  Hace: dotnet publish + vpk pack + gh release create.
#
#  Uso:
#    .\scripts\publish-release.ps1 -Version 0.2.0
#    .\scripts\publish-release.ps1 -Version 0.2.0 -Channel beta
#    .\scripts\publish-release.ps1 -Version 0.2.0 -Notes "Cambios..."
#
#  Requisitos en la máquina del release:
#    - dotnet SDK 8.0+
#    - velopack CLI:  dotnet tool install -g vpk
#    - gh CLI autenticado con permisos de release sobre el repo
# =============================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("stable", "beta")]
    [string]$Channel = "stable",

    [string]$Notes = $null
)

$ErrorActionPreference = "Stop"

$Root        = Resolve-Path "$PSScriptRoot\.."
$Csproj      = Join-Path $Root "src\DexSuite.App\DexSuite.App.csproj"
$PublishDir  = Join-Path $Root "publish\win-x64"
$ReleasesDir = Join-Path $Root "Releases"
$Repo        = "SrDexterGF/DexSuite"

# F7 — Sistema de licencias: rutas del pipeline de seguridad.
$KeyGenProj    = Join-Path $Root "tools\DexSuite.KeyGen\DexSuite.KeyGen.csproj"
$ConfuserProj  = Join-Path $Root "Confuser.crproj"
# ConfuserEx 2 (fork mkaring) — CLI portable en tools\ConfuserEx\.
# Descargado de github.com/mkaring/ConfuserEx/releases (v1.6.0). Si no está, el paso se salta.
$ConfuserExe   = Join-Path $Root "tools\ConfuserEx\Confuser.CLI.exe"

Write-Output ""
Write-Output "=========================================================="
Write-Output "  DexSuite — Publicando v$Version  ($Channel)"
Write-Output "=========================================================="
Write-Output ""

# 1) Actualiza la versión del .csproj. Mantiene compatibilidad con Velopack
#    que lee la versión del manifest del .exe.
Write-Output "[1/4] Actualizando <Version> en .csproj a $Version..."
# Lectura/escritura en UTF8 explicito y consistente. Antes se usaba
# Get-Content -Raw, que en Windows PowerShell 5.1 lee como ANSI y, al
# reescribir en UTF8, duplicaba los bytes de cualquier caracter acentuado
# en cada publicacion (el .csproj llego a inflarse a 11 MB). ReadAllText
# sin BOM asume UTF8, igual que WriteAllText, cerrando el ciclo.
$csprojText = [System.IO.File]::ReadAllText($Csproj)
$csprojText = [regex]::Replace($csprojText, '<Version>[^<]+</Version>', "<Version>$Version</Version>")
[System.IO.File]::WriteAllText($Csproj, $csprojText, [System.Text.UTF8Encoding]::new($false))

# 2) Publica el binario en publish\win-x64
Write-Output ""
Write-Output "[2/6] Publicando binarios (dotnet publish)..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
# PublishReadyToRun=false OBLIGATORIO: ConfuserEx reescribe el IL y el código
# nativo precompilado de R2R quedaría desincronizado → crash al cargar (rompe el
# hook de instalación de Velopack). Sin R2R el arranque es marginalmente más lento
# pero la app funciona y se puede ofuscar.
& dotnet publish $Csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir `
    /p:Version=$Version `
    /p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló con código $LASTEXITCODE" }

# 3) Ofuscación con ConfuserEx 2 (CAPA 3). Si no está instalado, se omite
#    con un warning — la app sigue funcionando pero sin la capa de ofuscación.
Write-Output ""
Write-Output "[3/6] Ofuscando con ConfuserEx 2..."
if (-not (Test-Path $ConfuserExe)) {
    Write-Warning "ConfuserEx no encontrado en $ConfuserExe."
    Write-Warning "Descarga ConfuserEx-CLI.zip de github.com/mkaring/ConfuserEx/releases"
    Write-Warning "y extráelo en tools\ConfuserEx\. Se omite la ofuscación (sin CAPA 3)."
} elseif (-not (Test-Path $ConfuserProj)) {
    Write-Warning "$ConfuserProj no existe. Se omite la ofuscación."
} else {
    # Resolver la versión instalada del runtime .NET 8 y reescribir los <probePath>
    # del .crproj (la app es framework-dependent; ConfuserEx necesita esas rutas
    # para resolver las dependencias del framework). Evita hardcodear la versión.
    $netCore = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.NETCore.App" -Directory `
        | Where-Object { $_.Name -like "8.*" } | Sort-Object Name -Descending | Select-Object -First 1
    $netWpf  = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App" -Directory `
        | Where-Object { $_.Name -like "8.*" } | Sort-Object Name -Descending | Select-Object -First 1
    if ($netCore -and $netWpf) {
        $crproj = Get-Content $ConfuserProj -Raw -Encoding UTF8
        $crproj = $crproj -replace '(<probePath>).*Microsoft\.NETCore\.App.*(</probePath>)',        ('${1}' + $netCore.FullName + '${2}')
        $crproj = $crproj -replace '(<probePath>).*Microsoft\.WindowsDesktop\.App.*(</probePath>)', ('${1}' + $netWpf.FullName  + '${2}')
        Set-Content $ConfuserProj -Value $crproj -Encoding UTF8 -NoNewline
        Write-Output "  probePath -> $($netCore.Name)"
    } else {
        Write-Warning "No se encontró el runtime .NET 8 compartido; ConfuserEx puede fallar al resolver dependencias."
    }

    # -n (no pause): sin él, ConfuserEx CLI espera 'press any key' y colgaría el pipeline.
    & $ConfuserExe -n $ConfuserProj
    if ($LASTEXITCODE -ne 0) { throw "ConfuserEx falló con código $LASTEXITCODE" }
}

# 4) Genera el archivo .integrity (CAPA 2) firmando el SHA-256 del DLL principal.
#    Firmamos DexSuite.App.dll (no el .exe) porque Velopack usa un stub nativo como
#    proceso raíz — Environment.ProcessPath apuntaría al stub, no a DexSuite.App.exe.
#    Assembly.Location en cambio siempre apunta al DLL real en current\.
Write-Output ""
Write-Output "[4/6] Firmando archivo de integridad (.integrity)..."
$DllPath = Join-Path $PublishDir "DexSuite.App.dll"
if (-not (Test-Path $DllPath)) { throw "No se encuentra $DllPath tras publish/obfuscate" }

# Usamos 'dotnet build' + 'dotnet exec DLL' en lugar de 'dotnet run' para evitar
# que Smart App Control bloquee el .exe compilado en entornos con App Control activo.
$KeyGenBuildDir = Join-Path $Root "tools\DexSuite.KeyGen\bin\Release\net8.0"
$KeyGenDll      = Join-Path $KeyGenBuildDir "DexSuite.KeyGen.dll"
& dotnet build $KeyGenProj -c Release -o $KeyGenBuildDir /p:UseAppHost=false --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet build KeyGen falló con código $LASTEXITCODE" }

& dotnet exec $KeyGenDll sign-integrity $DllPath
if ($LASTEXITCODE -ne 0) {
    Write-Warning "DexSuite.KeyGen sign-integrity devolvió $LASTEXITCODE."
    Write-Warning "Si la herramienta no está inicializada, ejecuta:"
    Write-Warning "  dotnet exec $KeyGenDll init"
    throw "No se pudo generar .integrity (CAPA 2)"
}

# 5) Empaqueta con Velopack
Write-Output ""
Write-Output "[5/6] Empaquetando con vpk pack..."
if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir | Out-Null }
# --framework: Velopack comprueba en la instalación si el .NET 8 Desktop Runtime
# está presente. Si falta, lo descarga del sitio oficial de Microsoft y lo instala
# automáticamente (con su propio aviso) antes de arrancar DexSuite. Evita el error
# en PCs sin el runtime. La app sigue publicándose como framework-dependent (ligera).
& vpk pack `
    --packId DexSuite `
    --packVersion $Version `
    --packDir $PublishDir `
    --mainExe "DexSuite.App.exe" `
    --packTitle "DexSuite" `
    --packAuthors "Sr. Dexter" `
    --framework "net8.0-desktop" `
    --channel $Channel `
    --outputDir $ReleasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack falló con código $LASTEXITCODE" }

# 6) Crea release en GitHub y sube solo los artefactos del canal actual.
#    Excluye archivos de versiones/canales anteriores que puedan estar en el
#    directorio de Releases.
Write-Output ""
Write-Output "[6/6] Creando GitHub Release v$Version..."

# Solo los archivos generados por este vpk pack (contienen la versión o el canal
# en el nombre, más los JSON de índice del canal).
$assets = @(Get-ChildItem $ReleasesDir |
    Where-Object { $_.Name -match $Version -or $_.Name -match "-$Channel" -or $_.Name -match "\.$Channel\." } |
    ForEach-Object { $_.FullName })

# Usa un fichero vacío para evitar que el string vacío se pierda en splatting de PS.
$_tmpNotes    = [System.IO.Path]::GetTempFileName()
$noteArg      = @("--notes-file", $_tmpNotes)
$preReleaseArg = if ($Channel -eq "beta") { @("--prerelease") } else { @() }

$ghArgs = @("release", "create", "v$Version",
    "--repo", $Repo,
    "--title", "DexSuite v$Version") + $noteArg + $preReleaseArg + $assets

& gh @ghArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create falló con código $LASTEXITCODE" }

Write-Output ""
Write-Output "=========================================================="
Write-Output "  Release v$Version publicada correctamente."
Write-Output "  Los usuarios verán la actualización en la pestaña 'Actualizaciones'"
Write-Output "  en cuanto Velopack consulte GitHub (hasta 5 minutos por caché)."
Write-Output "=========================================================="
