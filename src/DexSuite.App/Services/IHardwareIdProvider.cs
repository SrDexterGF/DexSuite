namespace DexSuite.App.Services;

/// <summary>
/// Genera un identificador único del equipo a partir de hardware estable
/// (CPU + placa base + UUID del BIOS). El mismo equipo siempre produce el
/// mismo HWID; cambiar CPU o placa lo invalida — el usuario tendrá que
/// pedir una clave nueva.
/// </summary>
public interface IHardwareIdProvider
{
    /// <summary>
    /// Devuelve el HWID actual en formato visual <c>XXXX-XXXX-XXXX-XXXX-XXXX</c>
    /// (Base32, 20 caracteres + 4 guiones).
    /// </summary>
    string GetHardwareId();

    /// <summary>
    /// Devuelve el HWID en formato compacto sin guiones (20 caracteres
    /// Base32). Esta es la forma canónica que se firma dentro del payload
    /// de licencia.
    /// </summary>
    string GetCanonicalHardwareId();
}
