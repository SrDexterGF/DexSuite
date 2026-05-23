# Genera Strings.resx (neutro = ingles) y los 9 satellites Strings.<lang>.resx
# leyendo las traducciones de translations.json (UTF-8). Idempotente:
# para anadir/cambiar claves, editar el JSON y volver a ejecutar.

$ErrorActionPreference = "Stop"

$outDir = Join-Path $PSScriptRoot "..\src\DexSuite.App\Resources"
$jsonPath = Join-Path $PSScriptRoot "translations.json"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Read JSON as UTF-8 (PowerShell 5.1 needs explicit encoding)
$jsonText = [System.IO.File]::ReadAllText($jsonPath, [System.Text.Encoding]::UTF8)
$data = $jsonText | ConvertFrom-Json

$languages = $data.languages

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

function Escape-Xml([string]$s) {
    return ($s -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;')
}

# Order properties by name for deterministic output
$keyNames = $data.keys.PSObject.Properties.Name | Sort-Object

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

foreach ($lang in $languages) {
    $filename = if ($lang -eq "en") { "Strings.resx" } else { "Strings.$lang.resx" }
    $path = Join-Path $outDir $filename

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine($header)
    foreach ($key in $keyNames) {
        $entry = $data.keys.$key
        $raw = $entry.$lang
        if ([string]::IsNullOrEmpty($raw)) {
            $raw = $entry.en
        }
        $val = Escape-Xml $raw
        [void]$sb.AppendLine("  <data name=`"$key`" xml:space=`"preserve`">")
        [void]$sb.AppendLine("    <value>$val</value>")
        [void]$sb.AppendLine("  </data>")
    }
    [void]$sb.AppendLine("</root>")

    [System.IO.File]::WriteAllText($path, $sb.ToString(), $utf8NoBom)
    Write-Host "  OK  $filename  ($($keyNames.Count) keys)"
}

Write-Host ""
Write-Host "$($languages.Count) archivos .resx generados en: $outDir"
