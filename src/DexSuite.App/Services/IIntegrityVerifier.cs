namespace DexSuite.App.Services;

/// <summary>
/// Verifica la integridad del ejecutable principal contra el archivo sibling
/// <c>DexSuite.App.exe.integrity</c> firmado en el pipeline de release.
/// </summary>
public interface IIntegrityVerifier
{
    /// <summary>
    /// True si el hash SHA-256 del .exe actual coincide con el hash firmado.
    /// False si: el archivo .integrity falta, la firma es inválida, los hashes
    /// no coinciden, o la clave pública no está configurada.
    ///
    /// <paramref name="reason"/> contiene un texto breve en caso de fallo
    /// (apto para mostrar al usuario antes de salir).
    /// </summary>
    bool Verify(out string reason);
}
