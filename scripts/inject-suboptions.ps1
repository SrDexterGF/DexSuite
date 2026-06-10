# =============================================================
#  inject-suboptions.ps1  (one-off)
#  Inyecta las claves i18n de las sub-opciones de módulos
#  (vista avanzada) en Strings.resx (EN neutral) y Strings.es.resx.
#  SEGURO: solo AÑADE claves que no existan; no toca las demás.
#  No usa generate-resx.ps1 (que es destructivo).
# =============================================================
$ErrorActionPreference = "Stop"
$resDir = Join-Path $PSScriptRoot "..\src\DexSuite.App\Resources"
$enPath = Join-Path $resDir "Strings.resx"
$esPath = Join-Path $resDir "Strings.es.resx"

# subId | es_name | es_desc | en_name | en_desc
$rows = @"
M01_prefetch|Prefetch|Borra la carpeta Prefetch de Windows.|Prefetch|Clears the Windows Prefetch folder.
M01_win_temp|Temporales de Windows|Vacía la carpeta Temp del propio Windows.|Windows Temp|Empties the Windows Temp folder.
M01_user_temp|Temporales del usuario|Vacía las carpetas Temp de tu cuenta de usuario.|User Temp folders|Empties your user account's Temp folders.
M01_thumb_icon|Caché de miniaturas e iconos|Borra la caché de miniaturas e iconos del Explorador.|Thumbnail and icon cache|Clears Explorer's thumbnail and icon cache.
M01_d3ds|Caché de shaders DirectX|Borra la caché de shaders de DirectX (D3DSCache).|DirectX shader cache|Clears the DirectX shader cache (D3DSCache).
M02_logs_root|Logs en la raíz de Windows|Borra los archivos .log en la carpeta de Windows.|Logs in the Windows root|Deletes .log files in the Windows folder.
M02_logs_sys32|Logs de System32|Borra los archivos .log de System32.|System32 logs|Deletes .log files in System32.
M02_logs_softwdist|Logs de Windows Update|Borra los logs de SoftwareDistribution y Windows Update.|Windows Update logs|Deletes SoftwareDistribution and Windows Update logs.
M02_logs_inf|Logs de instalación de drivers|Borra los archivos .log de instalación de drivers (carpeta INF).|Driver install logs|Deletes driver install logs (INF folder).
M02_logs_cbs|Logs de CBS y DISM|Borra los logs de CBS, DISM, Windows Update y MoSetup.|CBS and DISM logs|Deletes CBS, DISM, Windows Update and MoSetup logs.
M02_wer|Informes de errores (WER)|Vacía las carpetas de Windows Error Reporting.|Error reports (WER)|Empties the Windows Error Reporting folders.
M03_temp_aggressive|Carpetas Temp|Borra el contenido de las carpetas Temp del sistema y del usuario.|Temp folders|Clears system and user Temp folders.
M03_recent|Archivos recientes|Limpia el historial de archivos abiertos recientemente.|Recent files|Clears the recently opened files history.
M03_recycle|Papelera de reciclaje|Vacía la papelera de reciclaje sin pedir confirmación.|Recycle Bin|Empties the Recycle Bin without confirmation.
M03_windows_old|Carpeta Windows.old|Borra Windows.old, los restos de la instalación anterior.|Windows.old folder|Deletes Windows.old, leftovers from the previous install.
M04_crash_dumps|Volcados de memoria|Elimina minidumps y volcados de memoria por fallos del sistema.|Crash dumps|Removes minidumps and memory dump files from crashes.
M04_spooler|Cola de impresión|Vacía la cola de impresión y reinicia el servicio Spooler.|Print queue|Clears the print queue and restarts the Spooler service.
M04_wmi|Base de datos WMI|Compacta el repositorio WMI del sistema.|WMI database|Compacts the system's WMI repository.
M04_event_log|Visor de eventos|Vacía todos los registros del Visor de eventos.|Event Viewer|Clears all Event Viewer logs.
M05_stop_services|Detener servicios de Update|Detiene los servicios de Windows Update antes de limpiar.|Stop Update services|Stops Windows Update services before cleaning.
M05_cache_dl|Caché de descargas|Borra la carpeta de descargas de Windows Update.|Download cache|Deletes the Windows Update download folder.
M05_restart_services|Reiniciar servicios de Update|Vuelve a arrancar los servicios de Windows Update.|Restart Update services|Restarts the Windows Update services.
M05_delivery_opt|Delivery Optimization|Limpia la caché de Delivery Optimization.|Delivery Optimization|Clears the Delivery Optimization cache.
M06_analyze|Analizar Component Store|Analiza el almacén de componentes con DISM.|Analyze Component Store|Analyzes the component store with DISM.
M06_cleanup|Limpiar componentes obsoletos|Elimina componentes de Windows obsoletos con DISM.|Clean obsolete components|Removes obsolete Windows components with DISM.
M07_edge|Microsoft Edge|Borra la caché de Microsoft Edge.|Microsoft Edge|Clears the Microsoft Edge cache.
M07_chrome|Google Chrome|Borra la caché de Google Chrome.|Google Chrome|Clears the Google Chrome cache.
M07_firefox|Mozilla Firefox|Borra la caché de todos los perfiles de Firefox.|Mozilla Firefox|Clears the cache of all Firefox profiles.
M07_brave|Brave|Borra la caché del navegador Brave.|Brave|Clears the Brave browser cache.
M07_opera|Opera y Opera GX|Borra la caché de Opera y Opera GX.|Opera and Opera GX|Clears the Opera and Opera GX cache.
M07_vivaldi|Vivaldi|Borra la caché del navegador Vivaldi.|Vivaldi|Clears the Vivaldi browser cache.
M08_dns_flush|Vaciar caché DNS|Vacía la caché de resolución DNS.|Flush DNS cache|Flushes the DNS resolver cache.
M08_dns_register|Registrar en DNS|Vuelve a registrar el equipo en el DNS.|Register in DNS|Re-registers the computer in DNS.
M08_gpupdate|Actualizar directivas de grupo|Fuerza la actualización de las directivas de grupo.|Update group policies|Forces a group policy update.
M09_store|Caché de Microsoft Store|Resetea la caché de Microsoft Store.|Microsoft Store cache|Resets the Microsoft Store cache.
M09_onedrive|Logs de OneDrive|Borra los archivos de log de OneDrive.|OneDrive logs|Deletes OneDrive log files.
M09_teams|Caché de Microsoft Teams|Borra la caché de Microsoft Teams.|Microsoft Teams cache|Clears the Microsoft Teams cache.
M10_sfc|SFC (reparar archivos)|Escanea y repara archivos del sistema con SFC.|SFC (repair files)|Scans and repairs system files with SFC.
M10_dism|DISM (reparar imagen)|Repara la imagen de Windows con DISM RestoreHealth.|DISM (repair image)|Repairs the Windows image with DISM RestoreHealth.
M11_mouse_accel|Aceleración del ratón|Desactiva la aceleración del puntero del ratón.|Mouse acceleration|Disables mouse pointer acceleration.
M11_double_click|Velocidad de doble clic|Ajusta el doble clic a una respuesta más rápida (200 ms).|Double-click speed|Sets double-click to a faster response (200 ms).
M11_keyboard|Respuesta del teclado|Pone el retardo del teclado al mínimo y la velocidad al máximo.|Keyboard response|Sets keyboard delay to minimum and speed to maximum.
M11_monitor_hz|Detectar Hz del monitor|Detecta los hercios máximos de cada monitor (solo informa).|Detect monitor Hz|Detects each monitor's maximum refresh rate (info only).
M11_mouse_curve|Curva del puntero lineal|Aplica una curva de puntero 1:1 para movimiento lineal.|Linear pointer curve|Applies a 1:1 pointer curve for linear movement.
M13_copilot|Desactivar Copilot|Desactiva Windows Copilot y su botón en la barra de tareas.|Disable Copilot|Disables Windows Copilot and its taskbar button.
M13_cortana|Desactivar Cortana|Desactiva Cortana y la búsqueda web del sistema.|Disable Cortana|Disables Cortana and system web search.
M13_telemetry|Telemetría al mínimo|Pone la telemetría de Windows en el nivel más bajo posible.|Minimum telemetry|Sets Windows telemetry to the lowest possible level.
M13_telemetry_svc|Servicios de telemetría|Desactiva los servicios de telemetría (DiagTrack y otros).|Telemetry services|Disables telemetry services (DiagTrack and others).
M13_timeline|Timeline y publicidad|Desactiva Timeline, el ID de publicidad y el programa CEIP.|Timeline and ads|Disables Timeline, advertising ID and CEIP.
M14_silent_apps|Apps sugeridas|Desactiva la instalación silenciosa de apps sugeridas.|Suggested apps|Disables silent install of suggested apps.
M14_location|Rastreo de ubicación|Desactiva el rastreo de ubicación del sistema.|Location tracking|Disables system location tracking.
M14_widgets|Widgets de la barra|Quita los Widgets de la barra de tareas.|Taskbar widgets|Removes the taskbar Widgets.
M14_store_search|Store en el buscador|Quita los resultados de la Store del buscador de Windows.|Store in search|Removes Store results from Windows search.
M14_wpbt|WPBT (BIOS)|Desactiva WPBT para evitar reinstalación de bloatware por BIOS.|WPBT (BIOS)|Disables WPBT to stop BIOS reinstalling bloatware.
M14_services|Servicios innecesarios|Desactiva servicios remotos y de mapas innecesarios.|Unneeded services|Disables unneeded remote and maps services.
M14_svchost|Ajuste de SvcHost|Ajusta el umbral de SvcHost según la RAM instalada.|SvcHost tuning|Tunes the SvcHost threshold to the installed RAM.
M14_ps7_telemetry|Telemetría de PowerShell 7|Desactiva la telemetría de PowerShell 7 si está instalado.|PowerShell 7 telemetry|Disables PowerShell 7 telemetry if installed.
M14_sticky_keys|Sticky Keys|Desactiva las teclas especiales y atajos de accesibilidad molestos.|Sticky Keys|Disables sticky keys and annoying accessibility shortcuts.
M15_power_throttle|Power Throttling|Desactiva la limitación de potencia de la CPU.|Power Throttling|Disables CPU power throttling.
M15_mmcss|MMCSS para juegos|Da máxima prioridad a juegos y multimedia (MMCSS).|MMCSS for games|Gives top priority to games and multimedia (MMCSS).
M15_gamemode|Modo Juego|Activa el Modo Juego de Windows.|Game Mode|Enables Windows Game Mode.
M15_fse|Optimizaciones de pantalla completa|Desactiva las Full Screen Optimizations.|Fullscreen optimizations|Disables Full Screen Optimizations.
M15_timeouts|Tiempos de espera|Reduce los tiempos de espera del sistema al cerrar apps.|System timeouts|Reduces system timeouts when closing apps.
M15_maintenance|Mantenimiento automático|Mantiene activo el mantenimiento automático (necesario para el TRIM).|Automatic maintenance|Keeps automatic maintenance on (needed for TRIM).
M15_hibernate|Hibernación|Desactiva la hibernación y libera el archivo hiberfil.sys.|Hibernation|Disables hibernation and frees hiberfil.sys.
M15_sync|Sincronización de cuenta|Desactiva la sincronización de ajustes con la cuenta Microsoft.|Account sync|Disables settings sync with the Microsoft account.
M15_transparency|Efectos de transparencia|Desactiva los efectos de transparencia de la interfaz.|Transparency effects|Disables interface transparency effects.
M15_bg_apps|Apps en segundo plano|Desactiva las aplicaciones que se ejecutan en segundo plano.|Background apps|Disables apps running in the background.
M15_input_telemetry|Datos de escritura y voz|Desactiva la recopilación de datos de escritura y voz.|Typing and voice data|Disables typing and voice data collection.
M15_hags|Programación GPU por hardware|Activa la aceleración de GPU por hardware (HAGS; requiere reinicio).|Hardware GPU scheduling|Enables hardware GPU scheduling (HAGS; needs restart).
M15_diag_svc|Servicios de diagnóstico|Ajusta o desactiva servicios de diagnóstico innecesarios.|Diagnostic services|Tunes or disables unneeded diagnostic services.
M15_bcd|Timer de hardware (BCD)|Fija el timer de hardware para reducir el jitter.|Hardware timer (BCD)|Pins the hardware timer to reduce jitter.
M16_nic|Adaptadores Ethernet|Configura las propiedades avanzadas de los adaptadores Ethernet.|Ethernet adapters|Configures advanced properties of Ethernet adapters.
M16_tcp_global|TCP global|Ajusta el autotuning y la configuración global de TCP.|Global TCP|Tunes TCP autotuning and global settings.
M16_tcp_nagle|Nagle y ACK inmediato|Desactiva el algoritmo de Nagle y fuerza el ACK inmediato.|Nagle and instant ACK|Disables Nagle and forces immediate ACK.
M16_ip_stack|Parámetros del stack IP|Ajusta puertos, TIME_WAIT y tablas del stack IP.|IP stack parameters|Tunes ports, TIME_WAIT and IP stack tables.
M16_qos|Reserva QoS|Elimina la reserva del 20% de ancho de banda de QoS.|QoS reservation|Removes the 20% QoS bandwidth reservation.
M16_dns|DNS rápido|Configura DNS de Google y Cloudflare en los adaptadores Ethernet.|Fast DNS|Sets Google and Cloudflare DNS on Ethernet adapters.
M16_cache_clear|Vaciar cachés de red|Vacía las cachés ARP, NetBIOS y DNS.|Clear network caches|Flushes the ARP, NetBIOS and DNS caches.
M17_mrt|MRT (antimalware)|Ejecuta la herramienta de eliminación de software malicioso.|MRT (antimalware)|Runs the Malicious Software Removal Tool.
M17_defender_sig|Firmas de Defender|Actualiza las firmas de virus de Windows Defender.|Defender signatures|Updates Windows Defender virus signatures.
M17_defender_scan|Escaneo de Defender|Lanza un escaneo rápido con Windows Defender.|Defender scan|Runs a quick scan with Windows Defender.
M17_smbv1|Desactivar SMBv1|Desactiva SMBv1, vector de ataques como WannaCry.|Disable SMBv1|Disables SMBv1, an attack vector like WannaCry.
M17_firewall|Firewall activo|Activa el firewall de Windows en todos los perfiles.|Firewall on|Enables the Windows firewall on all profiles.
M17_autorun|Desactivar AutoRun|Desactiva AutoRun y AutoPlay en todas las unidades.|Disable AutoRun|Disables AutoRun and AutoPlay on all drives.
M17_dep|DEP siempre activo|Activa la prevención de ejecución de datos en modo AlwaysOn.|DEP always on|Enables Data Execution Prevention in AlwaysOn mode.
M17_llmnr|Desactivar LLMNR|Desactiva la resolución de nombres LLMNR.|Disable LLMNR|Disables LLMNR name resolution.
M17_netbios|Desactivar NetBIOS|Desactiva NetBIOS sobre TCP/IP en todos los adaptadores.|Disable NetBIOS|Disables NetBIOS over TCP/IP on all adapters.
M17_pua|Protección PUA|Activa la protección contra aplicaciones potencialmente no deseadas.|PUA protection|Enables protection against potentially unwanted apps.
M17_rdp|Desactivar Escritorio remoto|Desactiva el Escritorio remoto (RDP).|Disable Remote Desktop|Disables Remote Desktop (RDP).
M17_uac|UAC recomendado|Pone el Control de cuentas de usuario en el nivel recomendado.|Recommended UAC|Sets User Account Control to the recommended level.
M18_trim_enable|Activar TRIM|Asegura que el TRIM esté activado a nivel de sistema.|Enable TRIM|Ensures TRIM is enabled at system level.
M18_trim_smart|TRIM y salud SMART|Lanza el TRIM en cada SSD y lee su salud SMART.|TRIM and SMART health|Runs TRIM on each SSD and reads its SMART health.
M19_scan_devices|Escanear hardware|Refresca el catálogo de hardware del sistema.|Scan hardware|Refreshes the system hardware catalog.
M19_enum_drivers|Listar drivers OEM|Lista los drivers OEM instalados (solo informa).|List OEM drivers|Lists installed OEM drivers (info only).
M19_wu_drivers|Drivers por Windows Update|Lanza Windows Update para buscar drivers.|Drivers via Windows Update|Triggers Windows Update to look for drivers.
M19_reminder|Recordatorio de fabricante|Recuerda dónde descargar drivers oficiales del fabricante.|Manufacturer reminder|Reminds where to download official manufacturer drivers.
"@ -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }

function Add-Keys($path, $nameIdx, $descIdx) {
    [xml]$doc = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $existing = @{}
    foreach ($d in $doc.root.data) { if ($d.name) { $existing[$d.name] = $true } }

    $added = 0
    foreach ($row in $rows) {
        $p = $row -split "\|"
        if ($p.Count -lt 5) { continue }
        $subId = $p[0]
        $pairs = @(
            @("Modules.Sub.$subId.Name", $p[$nameIdx]),
            @("Modules.Sub.$subId.Desc", $p[$descIdx])
        )
        foreach ($kv in $pairs) {
            $key = $kv[0]; $val = $kv[1]
            $node = $doc.root.data | Where-Object { $_.name -eq $key } | Select-Object -First 1
            if ($node) {
                # Reparar valor existente (p. ej. corregir mojibake de una ejecución previa).
                $node.value = $val
                $added++
                continue
            }
            $data = $doc.CreateElement("data")
            $data.SetAttribute("name", $key)
            $data.SetAttribute("xml:space", "preserve")
            $value = $doc.CreateElement("value")
            $value.InnerText = $val
            [void]$data.AppendChild($value)
            [void]$doc.root.AppendChild($data)
            $added++
        }
    }
    $doc.Save((Resolve-Path $path).Path)
    Write-Host "  $path -> $added claves añadidas"
}

Write-Host "Inyectando sub-opciones..."
Add-Keys $enPath 3 4   # EN neutral
Add-Keys $esPath 1 2   # ES
Write-Host "Hecho."
