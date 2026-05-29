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
# ConfuserEx 2 se instala como dotnet tool global. Si no está, el paso se salta.
$ConfuserExe   = "ConfuserEx"   # dotnet tool: 'dotnet tool install -g ConfuserEx.CLI'

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host "  DexSuite — Publicando v$Version  ($Channel)" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host ""

# 1) Actualiza la versión del .csproj. Mantiene compatibilidad con Velopack
#    que lee la versión del manifest del .exe.
Write-Host "[1/4] Actualizando <Version> en .csproj a $Version..." -ForegroundColor Cyan
$csprojText = Get-Content $Csproj -Raw
$csprojText = [regex]::Replace($csprojText, '<Version>[^<]+</Version>', "<Version>$Version</Version>")
[System.IO.File]::WriteAllText($Csproj, $csprojText, [System.Text.UTF8Encoding]::new($false))

# 2) Publica el binario en publish\win-x64
Write-Host ""
Write-Host "[2/6] Publicando binarios (dotnet publish)..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
& dotnet publish $Csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir `
    /p:Version=$Version `
    /p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló con código $LASTEXITCODE" }

# 3) Ofuscación con ConfuserEx 2 (CAPA 3). Si no está instalado, se omite
#    con un warning — la app sigue funcionando pero sin la capa de ofuscación.
Write-Host ""
Write-Host "[3/6] Ofuscando con ConfuserEx 2..." -ForegroundColor Cyan
$confuserCmd = Get-Command $ConfuserExe -ErrorAction SilentlyContinue
if (-not $confuserCmd) {
    Write-Warning "ConfuserEx 2 no encontrado en PATH. Instala con:"
    Write-Warning "  dotnet tool install -g ConfuserEx.CLI"
    Write-Warning "Se omite la ofuscación; la build seguirá adelante sin CAPA 3."
} elseif (-not (Test-Path $ConfuserProj)) {
    Write-Warning "$ConfuserProj no existe. Se omite la ofuscación."
} else {
    & $confuserCmd $ConfuserProj
    if ($LASTEXITCODE -ne 0) { throw "ConfuserEx falló con código $LASTEXITCODE" }
}

# 4) Genera el archivo .integrity (CAPA 2) firmando el SHA-256 del DLL principal.
#    Firmamos DexSuite.App.dll (no el .exe) porque Velopack usa un stub nativo como
#    proceso raíz — Environment.ProcessPath apuntaría al stub, no a DexSuite.App.exe.
#    Assembly.Location en cambio siempre apunta al DLL real en current\.
Write-Host ""
Write-Host "[4/6] Firmando archivo de integridad (.integrity)..." -ForegroundColor Cyan
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
Write-Host ""
Write-Host "[5/6] Empaquetando con vpk pack..." -ForegroundColor Cyan
if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir | Out-Null }
& vpk pack `
    --packId DexSuite `
    --packVersion $Version `
    --packDir $PublishDir `
    --mainExe "DexSuite.App.exe" `
    --packTitle "DexSuite" `
    --packAuthors "Sr. Dexter" `
    --channel $Channel `
    --outputDir $ReleasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack falló con código $LASTEXITCODE" }

# 6) Crea release en GitHub y sube solo los artefactos del canal actual.
#    Excluye archivos de versiones/canales anteriores que puedan estar en el
#    directorio de Releases.
Write-Host ""
Write-Host "[6/6] Creando GitHub Release v$Version..." -ForegroundColor Cyan

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

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Release v$Version publicada correctamente." -ForegroundColor Green
Write-Host "  Los usuarios verán la actualización en la pestaña 'Actualizaciones'" -ForegroundColor Green
Write-Host "  en cuanto Velopack consulte GitHub (hasta 5 minutos por caché)." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
