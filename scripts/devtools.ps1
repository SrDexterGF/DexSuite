# =============================================================
#  devtools.ps1
#  Herramienta interna de DexSuite para publicar releases y
#  generar claves de licencia. No requiere argumentos; todo es
#  interactivo con menu de colores.
#
#  Uso:
#    .\scripts\devtools.ps1
#
#  Requisitos:
#    - dotnet SDK 8.0+
#    - velopack CLI:  dotnet tool install -g vpk
#    - gh CLI autenticado con permisos de release sobre el repo
#    - Clave privada KeyGen en %LocalAppData%\DexSuiteKeyGen\private.xml
# =============================================================

$ErrorActionPreference = "Stop"

$Root          = Resolve-Path "$PSScriptRoot\.."
$Csproj        = Join-Path $Root "src\DexSuite.App\DexSuite.App.csproj"
$PublishScript = Join-Path $Root "scripts\publish-release.ps1"

$KeyGenProj     = Join-Path $Root "tools\DexSuite.KeyGen\DexSuite.KeyGen.csproj"
$KeyGenBuildDir = Join-Path $Root "tools\DexSuite.KeyGen\bin\Release\net8.0"
$KeyGenDll      = Join-Path $KeyGenBuildDir "DexSuite.KeyGen.dll"

# ------------ helpers de UI --------------------------------------------------

function Write-Banner {
    param([string]$Text, [ConsoleColor]$Color = [ConsoleColor]::Magenta)
    Write-Host ""
    Write-Host ("=" * 62) -ForegroundColor $Color
    Write-Host "  $Text" -ForegroundColor $Color
    Write-Host ("=" * 62) -ForegroundColor $Color
    Write-Host ""
}

function Write-Step {
    param([string]$Text)
    Write-Host "  >> $Text" -ForegroundColor Cyan
}

function Write-OK {
    param([string]$Text)
    Write-Host "  OK  $Text" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Text)
    Write-Host "  !!  $Text" -ForegroundColor Yellow
}

function Read-NonEmpty {
    param([string]$Prompt, [string]$Default = "")
    while ($true) {
        if ($Default) {
            $val = Read-Host "  $Prompt [$Default]"
            if ([string]::IsNullOrWhiteSpace($val)) { return $Default }
            return $val.Trim()
        } else {
            $val = Read-Host "  $Prompt"
            if (-not [string]::IsNullOrWhiteSpace($val)) { return $val.Trim() }
            Write-Warn "Campo obligatorio, intentalo de nuevo."
        }
    }
}

# ------------ version del .csproj --------------------------------------------

function Get-CurrentVersion {
    $xml = [xml](Get-Content $Csproj -Raw)
    return $xml.Project.PropertyGroup.Version | Select-Object -First 1
}

function Bump-PatchVersion {
    param([string]$Version)
    $parts = $Version -split '\.'
    if ($parts.Count -lt 3) { return "$Version.1" }
    $major = $parts[0]; $minor = $parts[1]; $patch = [int]$parts[2]
    return "$major.$minor.$($patch + 1)"
}

# ------------ compilar DexSuite.KeyGen.dll si no existe ----------------------

function Ensure-KeyGenDll {
    if (-not (Test-Path $KeyGenDll)) {
        Write-Step "Compilando DexSuite.KeyGen (primera vez)..."
        & dotnet build $KeyGenProj -c Release -o $KeyGenBuildDir /p:UseAppHost=false --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Error compilando DexSuite.KeyGen" }
    }
}

# ------------ FLUJO 1: Nueva release -----------------------------------------

function Invoke-NewRelease {
    Write-Banner "Nueva Release" Magenta

    $current   = Get-CurrentVersion
    $suggested = Bump-PatchVersion $current
    Write-Host "  Version actual en .csproj : " -NoNewline
    Write-Host $current   -ForegroundColor Yellow
    Write-Host "  Sugerencia (patch +1)     : " -NoNewline
    Write-Host $suggested -ForegroundColor Green
    Write-Host ""

    $version = Read-NonEmpty "Version a publicar (Semver: mayor.menor.parche)" $suggested

    Write-Host ""
    Write-Host "  Canal de distribucion:" -ForegroundColor White
    Write-Host "    [1] stable  -- para todos los usuarios (recomendado)" -ForegroundColor Green
    Write-Host "    [2] beta    -- pruebas, clientes de confianza"         -ForegroundColor Yellow
    $channelChoice = Read-NonEmpty "Elige canal (1/2)" "1"
    $channel = if ($channelChoice -eq "2") { "beta" } else { "stable" }

    Write-Host ""
    $notes = Read-Host "  Notas del release (Enter para autogenerar desde commits)"
    if ([string]::IsNullOrWhiteSpace($notes)) { $notes = $null }

    Write-Host ""
    Write-Host ("  " + ("-" * 50)) -ForegroundColor DarkGray
    Write-Host "  Version : " -NoNewline; Write-Host $version -ForegroundColor Cyan
    Write-Host "  Canal   : " -NoNewline; Write-Host $channel -ForegroundColor Cyan
    Write-Host "  Notas   : " -NoNewline
    if ($notes) { Write-Host $notes -ForegroundColor Cyan }
    else        { Write-Host "(autogeneradas)" -ForegroundColor DarkGray }
    Write-Host ("  " + ("-" * 50)) -ForegroundColor DarkGray
    Write-Host ""

    $confirm = Read-Host "  Publicar ahora? (s/N)"
    if ($confirm -notmatch '^[sS]$') {
        Write-Warn "Publicacion cancelada."
        return
    }

    Write-Host ""
    $publishArgs = @("-Version", $version, "-Channel", $channel)
    if ($notes) { $publishArgs += @("-Notes", $notes) }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PublishScript @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  La publicacion fallo (codigo $LASTEXITCODE)." -ForegroundColor Red
    } else {
        Write-Host ""
        Write-OK "Release v$version ($channel) publicada correctamente en GitHub."
        if ($channel -eq "beta") {
            Write-Host ""
            Show-BetaInstructions $version
        }
    }
}

# ------------ FLUJO 4: Instrucciones para beta testers -----------------------

function Show-BetaInstructions {
    param([string]$Version = "")

    $versionStr = if ($Version) { " v$Version" } else { "" }
    $downloadUrl = "https://github.com/SrDexterGF/DexSuite/releases/latest/download/DexSuite-beta-Setup.exe"

    Write-Banner "Instrucciones para beta testers" Yellow
    Write-Host "  Copia y pega este mensaje para enviarlo a tus testers:" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host ("  " + ("=" * 56)) -ForegroundColor DarkGray

$msg = @"

Hola, te mando la beta$versionStr de DexSuite para que la pruebes.

PASO 1 - Solo la primera vez (si Windows la bloquea):
Antes de instalar, desactiva Smart App Control:
Configuracion > Privacidad y seguridad > Seguridad de Windows
> Control de aplicaciones y explorador
> Smart App Control > Desactivado

PASO 2 - Instalar:
Descarga e instala desde este enlace:
$downloadUrl

Si aparece una advertencia de "publicador desconocido",
haz clic en "Mas informacion" y luego en "Ejecutar de todas formas".

Las actualizaciones futuras llegaran automaticamente.

"@
    Write-Host $msg -ForegroundColor White
    Write-Host ("  " + ("=" * 56)) -ForegroundColor DarkGray
    Write-Host ""
}

# ------------ FLUJO 2: Generar licencia --------------------------------------

function Invoke-GenerateLicense {
    Write-Banner "Generar Licencia" Cyan

    $keyPath = Join-Path $env:LOCALAPPDATA "DexSuiteKeyGen\private.xml"
    if (-not (Test-Path $keyPath)) {
        Write-Host ""
        Write-Host "  ERROR: No se encuentra la clave privada en:" -ForegroundColor Red
        Write-Host "    $keyPath"                                   -ForegroundColor Red
        Write-Host ""
        Write-Host "  Ejecuta primero la opcion [3] Inicializar KeyGen." -ForegroundColor Yellow
        return
    }

    Ensure-KeyGenDll

    Write-Host "  El HWID aparece en DexSuite > Ajustes > Licencia." -ForegroundColor DarkGray
    Write-Host "  Formato: XXXX-XXXX-XXXX-XXXX-XXXX (con o sin guiones)" -ForegroundColor DarkGray
    Write-Host ""
    $hwid = Read-NonEmpty "HWID del cliente"

    Write-Host ""
    Write-Host "  Nivel de licencia:" -ForegroundColor White
    Write-Host "    [1] Advanced  -- funciones avanzadas" -ForegroundColor Green
    Write-Host "    [2] Pro       -- todas las funciones + futuras" -ForegroundColor Magenta
    $tierChoice = Read-NonEmpty "Elige nivel (1/2)" "1"
    $tier = if ($tierChoice -eq "2") { "Pro" } else { "Advanced" }

    Write-Host ""
    Write-Step "Generando clave para HWID=$hwid, Tier=$tier..."
    Write-Host ""

    & dotnet exec $KeyGenDll gen --hwid $hwid --tier $tier
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  ERROR al generar la licencia (codigo $LASTEXITCODE)." -ForegroundColor Red
        Write-Host "  Comprueba que el HWID es correcto (20 chars sin guiones)." -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-OK "Licencia generada. Copia el blob y enviaselo al cliente."
    }
}

# ------------ FLUJO 3: Inicializar KeyGen ------------------------------------

function Invoke-InitKeyGen {
    Write-Banner "Inicializar KeyGen" Yellow

    $keyPath = Join-Path $env:LOCALAPPDATA "DexSuiteKeyGen\private.xml"
    if (Test-Path $keyPath) {
        Write-Warn "Ya existe una clave privada en:"
        Write-Host "    $keyPath" -ForegroundColor Yellow
        Write-Warn "Sobrescribirla invalidaria TODAS las licencias emitidas."
        $confirm = Read-Host "  Escribe SI para confirmar la regeneracion"
        if ($confirm -ne "SI") {
            Write-Warn "Operacion cancelada."
            return
        }
        Remove-Item $keyPath -Force
    }

    Ensure-KeyGenDll

    Write-Step "Generando par de claves RSA-2048..."
    & dotnet exec $KeyGenDll init
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR al inicializar KeyGen." -ForegroundColor Red
        return
    }

    Write-Host ""
    Write-OK "Clave privada creada."
    Write-Host ""

    $appSrc   = Join-Path $Root "src\DexSuite.App"
    $confirm2 = Read-Host "  Actualizar los KeyPart*.cs en el proyecto? (s/N)"
    if ($confirm2 -match '^[sS]$') {
        & dotnet exec $KeyGenDll pubkey --update-app $appSrc
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  ERROR al actualizar KeyPart*.cs." -ForegroundColor Red
        } else {
            Write-OK "KeyPart*.cs actualizados. Recuerda compilar y publicar nueva release."
            Write-Warn "IMPORTANTE: haz backup de $keyPath ahora."
        }
    } else {
        Write-Warn "Recuerda ejecutar pubkey --update-app antes de publicar."
    }
}

# ------------ MENU PRINCIPAL -------------------------------------------------

Clear-Host
Write-Banner "DexSuite DevTools" Magenta

while ($true) {
    Write-Host "  Que quieres hacer?" -ForegroundColor White
    Write-Host ""
    Write-Host "    [1]  Nueva release  (publicar en GitHub)" -ForegroundColor Green
    Write-Host "    [2]  Generar licencia para un cliente"     -ForegroundColor Cyan
    Write-Host "    [3]  Inicializar / regenerar KeyGen"       -ForegroundColor Yellow
    Write-Host "    [4]  Ver instrucciones para beta testers"  -ForegroundColor Yellow
    Write-Host "    [0]  Salir"                                -ForegroundColor DarkGray
    Write-Host ""

    $choice = Read-Host "  Opcion"

    switch ($choice) {
        "1" { Invoke-NewRelease }
        "2" { Invoke-GenerateLicense }
        "3" { Invoke-InitKeyGen }
        "4" { Show-BetaInstructions }
        "0" { Write-Host ""; exit 0 }
        default { Write-Warn "Opcion no valida. Elige 0, 1, 2, 3 o 4." }
    }

    Write-Host ""
    Write-Host ("-" * 62) -ForegroundColor DarkGray
    Write-Host ""
}
