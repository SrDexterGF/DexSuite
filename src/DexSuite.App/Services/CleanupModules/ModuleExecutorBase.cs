using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;
using Microsoft.Win32;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// Helpers compartidos por los 19 módulos nativos: enumeración y borrado
/// de archivos/carpetas (con manejo silencioso de bloqueos y permisos),
/// control de servicios vía WMI (stop/start/start-mode), escritura de
/// registro y lanzamiento de procesos externos con streaming de stdout.
///
/// Cada módulo derivado solo tiene que producir su <see cref="IAsyncEnumerable{ModuleProgress}"/>
/// usando estos helpers; toda la fontanería común vive aquí.
/// </summary>
[SupportedOSPlatform("windows")]
public abstract class ModuleExecutorBase : IModuleExecutor
{
    public abstract int ModuleId { get; }
    public abstract IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        CancellationToken ct = default);

    /// <summary>
    /// True si la sub-operación debe ejecutarse: o bien estamos en vista simple
    /// (enabled == null → todo), o bien el usuario la marcó en la vista avanzada.
    /// </summary>
    protected static bool Want(IReadOnlySet<string>? enabled, string subOpId)
        => enabled is null || enabled.Contains(subOpId);

    /// <summary>
    /// Servicio de auditoría/reversión. Es opcional: los módulos de limpieza
    /// (borrado de archivos) no lo necesitan y llaman al ctor sin argumentos.
    /// Los módulos de ajustes (registro/servicios) lo reciben por DI para
    /// capturar el valor ORIGINAL antes de cambiarlo y poder revertir.
    /// </summary>
    protected IChangeTrackingService? Tracking { get; }

    protected ModuleExecutorBase(IChangeTrackingService? tracking = null) => Tracking = tracking;

    /// <summary>
    /// Nombre legible del módulo guardado junto a cada cambio (para la vista
    /// "Revertir"). Por defecto "MNN"; cada módulo lo sobreescribe con su nombre.
    /// </summary>
    protected virtual string ModuleName => $"M{ModuleId:00}";

    private string ModuleIdString => ModuleId.ToString();

    // Atajos de progreso
    protected ModuleProgress Header(string msg)    => ModuleProgress.Header(ModuleId, msg);
    protected ModuleProgress Step(string msg)      => ModuleProgress.Step(ModuleId, msg);
    protected ModuleProgress Ok(string msg)        => ModuleProgress.Ok(ModuleId, msg);
    protected ModuleProgress Warn(string msg)      => ModuleProgress.Warn(ModuleId, msg);
    protected ModuleProgress Err(string msg)       => ModuleProgress.Err(ModuleId, msg);
    protected ModuleProgress Info(string msg)      => ModuleProgress.Info(ModuleId, msg);
    protected ModuleProgress Heartbeat(string msg) => ModuleProgress.Heartbeat(ModuleId, msg);
    protected ModuleProgress Done(string msg)      => ModuleProgress.Done(ModuleId, msg);

    // Filesystem

    /// <summary>
    /// Borra un único archivo quitando el atributo ReadOnly si hace falta.
    /// Devuelve los bytes liberados, o 0 si el archivo no existe o está bloqueado.
    /// </summary>
    private static long DeleteFileSafe(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return 0;
            var size = fi.Length;
            if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                fi.Attributes &= ~FileAttributes.ReadOnly;
            fi.Delete();
            return size;
        }
        catch { return 0; }
    }

    /// <summary>Borra todo el contenido de un directorio (archivos + subdirs).</summary>
    protected static (int Files, long Bytes) PurgeDirectory(string path, CancellationToken ct = default)
    {
        if (!Directory.Exists(path)) return (0, 0);

        int  files = 0;
        long bytes = 0;

        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) break;
                var freed = DeleteFileSafe(f);
                if (freed > 0) { bytes += freed; files++; }
            }
        }
        catch { /* enumeración fallida; sigue */ }

        // Subdirs vacíos quedan limpios.
        try
        {
            foreach (var d in Directory.EnumerateDirectories(path))
            {
                if (ct.IsCancellationRequested) break;
                try { Directory.Delete(d, recursive: true); }
                catch { /* en uso */ }
            }
        }
        catch { /* idem */ }

        return (files, bytes);
    }

    /// <summary>Borra archivos que casen con un patrón (ej. "*.log") dentro de un directorio.</summary>
    protected static (int Files, long Bytes) PurgePattern(
        string dir,
        string pattern,
        bool recursive = false,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(dir)) return (0, 0);

        var opts = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        int  files = 0;
        long bytes = 0;

        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, pattern, opts))
            {
                if (ct.IsCancellationRequested) break;
                var freed = DeleteFileSafe(f);
                if (freed > 0) { bytes += freed; files++; }
            }
        }
        catch { /* enumeración fallida */ }

        return (files, bytes);
    }

    /// <summary>Borra un único archivo si existe. Devuelve los bytes liberados.</summary>
    protected static long PurgeFile(string path) => DeleteFileSafe(path);

    /// <summary>
    /// Formatea bytes con sufijo legible (KB/MB/GB).
    /// </summary>
    protected static string FormatBytes(long bytes)
    {
        if (bytes < 1024)            return $"{bytes} B";
        if (bytes < 1024L * 1024)    return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1_048_576.0:F1} MB";
        return $"{bytes / 1_073_741_824.0:F2} GB";
    }

    // Servicios (vía WMI Win32_Service)

    /// <summary>
    /// Para un servicio Windows. Devuelve true si el servicio existe y queda detenido
    /// (o ya lo estaba). Silencia errores de servicios ausentes.
    /// </summary>
    protected static bool StopService(string name)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{name}'");
            svc.Get();
            var state = svc["State"]?.ToString();
            if (string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase))
                return true;
            var result = svc.InvokeMethod("StopService", null);
            return result is uint u && u == 0;
        }
        catch { return false; }
    }

    /// <summary>Arranca un servicio Windows. Idempotente: si ya está arriba, devuelve true.</summary>
    protected static bool StartService(string name)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{name}'");
            svc.Get();
            var state = svc["State"]?.ToString();
            if (string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
                return true;
            var result = svc.InvokeMethod("StartService", null);
            return result is uint u && u == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Cambia el modo de arranque de un servicio (Automatic / Manual / Disabled).
    /// Valores válidos: "Boot", "System", "Automatic", "Manual", "Disabled".
    /// </summary>
    protected static bool SetServiceStartMode(string name, string mode)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{name}'");
            svc.Get();
            var parms = svc.GetMethodParameters("ChangeStartMode");
            parms["StartMode"] = mode;
            var result = svc.InvokeMethod("ChangeStartMode", parms, null);
            // result["ReturnValue"] == 0 → OK
            return result?["ReturnValue"] is uint u && u == 0;
        }
        catch { return false; }
    }

    /// <summary>Mata todas las instancias de un proceso por nombre (sin extensión).</summary>
    protected static int KillProcess(string imageName)
    {
        // Process.GetProcessesByName espera el nombre sin extensión.
        var key = imageName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? imageName[..^4]
            : imageName;
        int killed = 0;
        foreach (var p in Process.GetProcessesByName(key))
        {
            try { p.Kill(entireProcessTree: true); killed++; }
            catch { /* sin permisos / ya terminó */ }
            finally { p.Dispose(); }
        }
        return killed;
    }

    // Registro

    /// <summary>Resuelve hive desde un path tipo "HKLM\SOFTWARE\Foo".</summary>
    private static (RegistryKey Root, string SubPath) SplitHive(string fullPath)
    {
        var (prefix, remainder) = SplitPrefix(fullPath);
        var root = prefix.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER"  => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT"  => Registry.ClassesRoot,
            "HKU"  or "HKEY_USERS"         => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Hive no reconocida: {prefix}"),
        };
        return (root, remainder);
    }

    private static (string Prefix, string Remainder) SplitPrefix(string path)
    {
        var i = path.IndexOf('\\');
        if (i < 0) return (path, string.Empty);
        return (path[..i], path[(i + 1)..]);
    }

    /// <summary>Escribe un valor REG_SZ. Crea las claves intermedias si hace falta.</summary>
    protected static bool SetRegistryString(string keyPath, string valueName, string value)
    {
        try
        {
            var (root, sub) = SplitHive(keyPath);
            using var k = root.CreateSubKey(sub, writable: true);
            k?.SetValue(valueName, value, RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Escribe un valor REG_DWORD.</summary>
    protected static bool SetRegistryDword(string keyPath, string valueName, int value)
    {
        try
        {
            var (root, sub) = SplitHive(keyPath);
            using var k = root.CreateSubKey(sub, writable: true);
            k?.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Escribe un valor REG_BINARY.</summary>
    protected static bool SetRegistryBinary(string keyPath, string valueName, byte[] value)
    {
        try
        {
            var (root, sub) = SplitHive(keyPath);
            using var k = root.CreateSubKey(sub, writable: true);
            k?.SetValue(valueName, value, RegistryValueKind.Binary);
            return true;
        }
        catch { return false; }
    }

    // -------------------- captura + escritura con tracking --------------------

    /// <summary>
    /// Lee el valor actual del registro y lo serializa igual que espera el
    /// servicio de reversión (DWORD/QWORD→texto, MULTI_SZ→unión por espacios,
    /// BINARY→Base64). Devuelve (null, null) si la clave o el valor no existen,
    /// lo que el revertir interpreta como "borrar el valor".
    /// </summary>
    private static (string? Value, string? Kind) ReadRegistryValue(string keyPath, string? valueName)
    {
        try
        {
            var (root, sub) = SplitHive(keyPath);
            using var k = root.OpenSubKey(sub);
            if (k is null) return (null, null);

            var name = valueName ?? string.Empty;
            var raw = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is null) return (null, null);

            var kind = k.GetValueKind(name);
            string? serialized = kind switch
            {
                RegistryValueKind.DWord       => Convert.ToInt32(raw).ToString(),
                RegistryValueKind.QWord       => Convert.ToInt64(raw).ToString(),
                RegistryValueKind.MultiString => string.Join(" ", (string[])raw),
                RegistryValueKind.Binary      => Convert.ToBase64String((byte[])raw),
                _                             => raw.ToString(),
            };
            string kindStr = kind switch
            {
                RegistryValueKind.DWord        => "DWORD",
                RegistryValueKind.QWord        => "QWORD",
                RegistryValueKind.String       => "SZ",
                RegistryValueKind.ExpandString => "EXPAND_SZ",
                RegistryValueKind.MultiString  => "MULTI_SZ",
                RegistryValueKind.Binary       => "BINARY",
                _                              => "SZ",
            };
            return (serialized, kindStr);
        }
        catch { return (null, null); }
    }

    /// <summary>Lee el StartMode actual de un servicio (Auto/Manual/Disabled) vía WMI.</summary>
    private static string? ReadServiceStartMode(string name)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{name}'");
            svc.Get();
            return svc["StartMode"]?.ToString();
        }
        catch { return null; }
    }

    /// <summary>Captura (una sola vez) el valor original antes de modificar el registro.</summary>
    private void CaptureRegistryOriginal(string keyPath, string? valueName, string? newValue, string newKind)
    {
        if (Tracking is null) return;
        var (orig, origKind) = ReadRegistryValue(keyPath, valueName);
        _ = Tracking.RecordRegistryChangeIfFirstAsync(
            ModuleIdString, ModuleName, keyPath, valueName, orig, newValue, origKind ?? newKind);
    }

    /// <summary>Escribe un REG_DWORD capturando antes el valor original para poder revertir.</summary>
    protected void TrackedSetDword(string keyPath, string valueName, int value)
    {
        CaptureRegistryOriginal(keyPath, valueName, value.ToString(), "DWORD");
        SetRegistryDword(keyPath, valueName, value);
    }

    /// <summary>Escribe un REG_SZ capturando antes el valor original para poder revertir.</summary>
    protected void TrackedSetString(string keyPath, string valueName, string value)
    {
        CaptureRegistryOriginal(keyPath, valueName, value, "SZ");
        SetRegistryString(keyPath, valueName, value);
    }

    /// <summary>Escribe un REG_BINARY capturando antes el valor original para poder revertir.</summary>
    protected void TrackedSetBinary(string keyPath, string valueName, byte[] value)
    {
        CaptureRegistryOriginal(keyPath, valueName, Convert.ToBase64String(value), "BINARY");
        SetRegistryBinary(keyPath, valueName, value);
    }

    /// <summary>Cambia el modo de arranque de un servicio capturando antes el original.</summary>
    protected void TrackedSetServiceStartMode(string name, string mode)
    {
        if (Tracking is not null)
        {
            var orig = ReadServiceStartMode(name);
            _ = Tracking.RecordServiceChangeIfFirstAsync(ModuleIdString, ModuleName, name, orig, mode);
        }
        SetServiceStartMode(name, mode);
    }

    /// <summary>
    /// Recorre todas las subclaves de un path del registro (un nivel).
    /// Útil para iterar TCPIP\Interfaces\{guid}.
    /// </summary>
    protected static IEnumerable<string> EnumerateSubKeys(string keyPath)
    {
        var (root, sub) = SplitHive(keyPath);
        using var k = root.OpenSubKey(sub);
        if (k is null) yield break;
        foreach (var name in k.GetSubKeyNames())
            yield return name;
    }

    // Process streaming

    /// <summary>
    /// Lanza un proceso, redirige stdout/stderr y devuelve cada línea en orden.
    /// Si stderr produce líneas, las prefija con [ERR] para que la UI las distinga.
    /// </summary>
    protected static async IAsyncEnumerable<string> StreamProcessAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = fileName,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = workingDirectory ?? Environment.SystemDirectory,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            channel.Writer.TryWrite(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            channel.Writer.TryWrite("[ERR] " + e.Data);
        };
        process.Exited += (_, _) => channel.Writer.TryComplete();
        process.EnableRaisingEvents = true;

        if (!process.Start())
            throw new InvalidOperationException($"No se pudo lanzar {fileName}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var line))
                    yield return line;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ya terminó */ }
            }
        }
    }

    /// <summary>
    /// Lanza un proceso y espera a que termine, sin streaming. Devuelve el ExitCode.
    /// Para comandos rápidos donde no necesitamos cada línea (ej. fsutil, bcdedit).
    /// </summary>
    protected static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName        = fileName,
            Arguments       = arguments,
            UseShellExecute = false,
            CreateNoWindow  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return -1;
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            return p.ExitCode;
        }
        catch { return -1; }
    }

    // P/Invoke: SystemParametersInfo

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    private const uint SPI_SETMOUSE          = 0x0004;
    private const uint SPI_SETMOUSESPEED     = 0x0071;
    private const uint SPIF_UPDATEINIFILE    = 0x01;
    private const uint SPIF_SENDCHANGE       = 0x02;

    /// <summary>
    /// Vuelve a leer la configuración del ratón desde el registro y la aplica
    /// sin necesidad de cerrar sesión.
    /// </summary>
    protected static void ApplyMouseSettingsLive()
    {
        try { SystemParametersInfo(SPI_SETMOUSE, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE); }
        catch { /* no crítico */ }
    }
}
