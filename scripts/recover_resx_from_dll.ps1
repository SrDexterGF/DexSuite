# Recupera todos los .resx desde los .nupkg de v0.2.27 publicado.
# Lee los recursos embebidos en cada DexSuite.App.resources.dll satellite
# (y DexSuite.App.dll para inglés base) y genera los .resx correspondientes
# con la cabecera estándar.
#
# Uso: .\recover_resx_from_dll.ps1
# Requiere: .nupkg de v0.2.27 ya extraído en %TEMP%\dexsuite_recover

$ErrorActionPreference = "Stop"

$extract = "$env:TEMP\dexsuite_recover"
$resourcesDir = (Resolve-Path (Join-Path $PSScriptRoot "..\src\DexSuite.App\Resources")).Path

$header = @'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'@

function Escape-Xml($s) {
    return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function Export-Resx($dllPath, $resourceName, $outFile) {
    $asm = [System.Reflection.Assembly]::LoadFrom($dllPath)
    $stream = $asm.GetManifestResourceStream($resourceName)
    if ($null -eq $stream) {
        Write-Host "  ERR: recurso $resourceName no encontrado en $dllPath" -ForegroundColor Red
        return 0
    }
    $reader = New-Object System.Resources.ResourceReader($stream)
    $entries = @()
    foreach ($e in $reader) {
        $key = $e.Key
        $val = if ($null -eq $e.Value) { "" } else { $e.Value.ToString() }
        $entries += [PSCustomObject]@{ Key = $key; Value = $val }
    }
    $reader.Dispose()

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append($header)
    foreach ($entry in ($entries | Sort-Object Key)) {
        [void]$sb.Append("`r`n  <data name=`"$(Escape-Xml $entry.Key)`" xml:space=`"preserve`">`r`n")
        [void]$sb.Append("    <value>$(Escape-Xml $entry.Value)</value>`r`n")
        [void]$sb.Append("  </data>")
    }
    [void]$sb.Append("`r`n</root>`r`n")
    [System.IO.File]::WriteAllText($outFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    return $entries.Count
}

Write-Host "Recuperando .resx desde DLLs de v0.2.27..." -ForegroundColor Cyan

# Inglés base
$baseDll = "$extract\lib\app\DexSuite.App.dll"
$count = Export-Resx $baseDll "DexSuite.App.Resources.Strings.resources" "$resourcesDir\Strings.resx"
Write-Host ("  OK  Strings.resx          (" + $count + " keys)")

# Satellites
$langDirs = Get-ChildItem "$extract\lib\app" -Directory | Where-Object { $_.Name -match '^[a-z]{2}(-[A-Z]{2})?$' }
foreach ($dir in $langDirs) {
    $lang = $dir.Name
    $dll = Join-Path $dir.FullName "DexSuite.App.resources.dll"
    if (-not (Test-Path $dll)) { continue }
    $resName = "DexSuite.App.Resources.Strings.$lang.resources"
    $outFile = "$resourcesDir\Strings.$lang.resx"
    $count = Export-Resx $dll $resName $outFile
    Write-Host ("  OK  Strings." + $lang + ".resx" + (" " * (12 - $lang.Length)) + " (" + $count + " keys)")
}

Write-Host "`nListo. .resx restaurados en $resourcesDir" -ForegroundColor Green
