namespace DexSuite.App.Services;

/// <summary>
/// Genera un correo pre-rellenado con la información del sistema y las últimas
/// líneas del historial para reportar bugs a suitedex@gmail.com.
/// Usa mailto: para abrir el cliente de correo del usuario — no requiere
/// infraestructura externa ni expone credenciales en el binario.
/// </summary>
public interface IBugReportService
{
    /// <summary>
    /// Construye y abre un mailto: con asunto y cuerpo pre-rellenados.
    /// El cuerpo incluye versión, OS, idioma y las últimas N entradas del log.
    /// </summary>
    Task OpenBugReportAsync(string? userDescription = null);

    /// <summary>
    /// Abre un mailto: para enviar una sugerencia o mejora.
    /// Sin datos de diagnóstico — solo una plantilla en blanco con versión.
    /// </summary>
    Task OpenSuggestionAsync();
}
