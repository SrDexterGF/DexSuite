$ErrorActionPreference = "Stop"
$resDir = Join-Path $PSScriptRoot "..\src\DexSuite.App\Resources"
$enPath = Join-Path $resDir "Strings.resx"
$esPath = Join-Path $resDir "Strings.es.resx"

# key | es | en
$rows = @"
Modules.AdvancedView|Vista avanzada|Advanced view
Modules.AdvancedView.Hint|Activa cada ajuste por separado para que ningún clic aplique varios cambios a la vez.|Toggle each setting on its own so no single click applies more than one change.
Settings.ModuleView.Title|Vista avanzada de módulos por defecto|Advanced module view by default
Settings.ModuleView.Desc|Al abrir Módulos, muestra cada ajuste individual en lugar de las opciones agrupadas.|When opening Modules, show every individual setting instead of the grouped options.
"@ -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }

function Add-Keys($path, $valIdx) {
    [xml]$doc = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $n = 0
    foreach ($row in $rows) {
        $p = $row -split "\|"
        if ($p.Count -lt 3) { continue }
        $key = $p[0]; $val = $p[$valIdx]
        $node = $doc.root.data | Where-Object { $_.name -eq $key } | Select-Object -First 1
        if ($node) { $node.value = $val; $n++; continue }
        $data = $doc.CreateElement("data")
        $data.SetAttribute("name", $key)
        $data.SetAttribute("xml:space", "preserve")
        $value = $doc.CreateElement("value"); $value.InnerText = $val
        [void]$data.AppendChild($value); [void]$doc.root.AppendChild($data); $n++
    }
    $doc.Save((Resolve-Path $path).Path)
    Write-Host "  $path -> $n claves"
}
Add-Keys $enPath 2
Add-Keys $esPath 1
Write-Host "Hecho."
